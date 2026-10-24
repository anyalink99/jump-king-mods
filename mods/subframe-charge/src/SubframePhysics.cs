using System;
using System.Diagnostics;
using System.Collections.Generic;
using EntityComponent;
using HarmonyLib;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Input;
using JKRuntime.World;
using JumpKing;
using JumpKing.API;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;

namespace SubframeCharge
{
    internal static class SubframePhysics
    {
        private static PlayerEntity player;
        private static PlayerUpdates.SubstepLease lease;
        private static BodyPipeline marker;
        private static IDisposable dynamics;
        private static long timestamp, restoreEpoch, groundEpoch;
        private static bool attempted, selected, interrupted;
        private static bool groundedObserved, activationRefused;
        private static Microsoft.Xna.Framework.Vector2 lastPosition;
        private static readonly System.Reflection.FieldInfo Jump=AccessTools.Field(typeof(PlayerEntity),"m_jump_state");
        private static readonly List<PlayerPadFrame> inputs=new List<PlayerPadFrame>();
        private static RuntimeScope world;
        private static IDisposable preparedDynamics;
        private static readonly Action<bool> SetPause=(Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>),AccessTools.Method(typeof(PlayerEntity).Assembly.GetType("JumpKing.PauseMenu.PauseManager",true),"SetPause"));
        internal static string Reason { get; private set; }
        internal static bool Active { get { return lease!=null; } }
        internal static long FrameTimestamp { get { return Active && timestamp>0 ? timestamp : Stopwatch.GetTimestamp(); } }
        internal static string Status
        {
            get
            {
                bool requested=SettingsStore.Current.Mode==SubframeMode.InputsAndPhysics;
                if((requested || Active) && Reason!=null) return Reason;
                return attempted && requested!=Active ? "waiting for safe moment" : null;
            }
        }
        internal static void PrepareWorld(RuntimeScope scope)
        {
            world=scope;
            scope.Defer(delegate {
                if(ReferenceEquals(world,scope)) { world=null; preparedDynamics=null; }
            });
        }
        internal static void Prepare(RuntimeScope scope)
        {
            if(world==null) PrepareWorld(scope);
            if(SettingsStore.Current.Mode==SubframeMode.InputsAndPhysics) PrepareResources(world);
        }
        private static void PrepareResources(RuntimeScope scope)
        {
            if(preparedDynamics!=null) return;
            PhysicsCallbacks.Prepare(scope);
            using(RuntimeApi.MeasureStartup("subframe-physics.prepare")) preparedDynamics=scope.Own(PhysicsDynamics.Acquire());
            MotionObservation.RefreshPhysicsAdapters();
        }
        internal static void Start(JKRuntime.ModuleContext context)
        {
            Stop(); attempted=true; selected=SettingsStore.Current.Mode==SubframeMode.InputsAndPhysics;
            player=GameLoop.m_player;
            context.Track(new Lifetime());
            context.Track(RuntimeApi.Mechanics.RegisterControlled("subframe-charge",
                new MechanicDefinition("subframe-charge.physics",new Version(1,0),MechanicEffects.Input | MechanicEffects.Movement | MechanicEffects.Collision),
                delegate { return new MechanicState(SettingsStore.Current.Mode==SubframeMode.InputsAndPhysics,Reason==null,Active && Reason==null,MechanicSource.Setting,Status ?? "Fractional native player steps; world retains native cadence"); }));
            if(selected) Enable();
        }
        private static void Enable(bool switching=false)
        {
            if(world!=null) PrepareResources(world);
            Reason=Unsupported(player);
            if(Reason!=null) { DiagnosticLog.Write("physics unavailable: "+Reason); return; }
            dynamics=PhysicsDynamics.Acquire();
            try
            {
                lease=PlayerUpdates.RegisterSubsteps(player,"subframe-charge",Include,NativeControls);
                timestamp=0;
                marker=new BodyPipeline(player.m_body,true,400,"subframe-charge");
                marker.RegisterSubstep(BodyPhase.BeforeCollisionCache,new PhysicsMarker());
                restoreEpoch=JKRuntime.State.GameState.Snapshots.RestoreEpoch;
                PhysicsDynamics.Reset(); PlayerPresentation.Reset();
                DiagnosticLog.Write("physics active: fractional player movement; mod tick callbacks retain native cadence");
                ChargeFrameComponents.Clock=delegate { return FrameTimestamp; };
                if(switching) ResetTransition();
            }
            catch { Deactivate(); throw; }
        }
        internal static void AfterNativeUpdate()
        {
            if(!attempted || player==null || !player.IsAlive || !ReferenceEquals(player,GameLoop.m_player)) return;
            bool requested=SettingsStore.Current.Mode==SubframeMode.InputsAndPhysics;
            if(requested!=selected) { selected=requested; activationRefused=false; if(!Active) Reason=null; }
            var body=player.m_body;
            bool available=!PerformanceSuspension.Active && WorldExecution.CanSimulate && !NativePause.IsPaused
                && Game1.instance!=null && Game1.instance.IsActive && JumpGame.instance!=null && JumpGame.instance.IsPlaying();
            long epoch=JKRuntime.State.GameState.Snapshots.RestoreEpoch;
            bool grounded=available && body.Enabled && body.IsOnGround && !body.IsKnocked
                && Math.Abs(body.Velocity.X)<.0001f && body.Velocity.Y>=0 && body.Velocity.Y<=PlayerValues.GRAVITY+.0001f;
            bool stable=grounded && groundedObserved && lastPosition==body.Position && groundEpoch==epoch;
            groundedObserved=grounded; lastPosition=body.Position; groundEpoch=epoch;
            if(requested==Active || !stable || activationRefused) return;
            var jump=Jump.GetValue(player) as JumpState;
            var charge=jump as SubframeChargeState;
            var input=player.GetComponent<InputComponent>().GetState();
            if(jump==null || jump.IsRunning() || charge!=null && charge.Charging || input.jump || input.left || input.right
                || ResponsiveInput.HasPlayerInput || DebugJumpPrompt.IsControlling(jump)) return;
            string unsupported=Unsupported(player);
            if(unsupported!=null) { if(requested) Reason=unsupported; return; }
            // all player slots and their late updates have finished before ownership changes
            if(requested) Enable(true);
            else { Deactivate(); Reason=null; ResetTransition(); }
        }
        private static void ResetTransition()
        {
            ResponsiveInput.ResetPhysics(); PlayerPresentation.Reset();
            var jump=Jump.GetValue(player) as JumpState;
            if(jump!=null) jump.ResetResult();
            var charge=jump as SubframeChargeState;
            if(charge!=null) charge.ResetCadence();
            groundedObserved=false; interrupted=false;
        }
        private sealed class PhysicsMarker : IBodyCompBehaviour { public bool ExecuteBehaviour(JumpKing.BodyCompBehaviours.BehaviourContext context) { return true; } }
        private static bool NativeControls()
        {
            var jump=Jump.GetValue(player);
            return jump==null || jump.GetType()!=typeof(JumpState) && jump.GetType()!=typeof(SubframeChargeState)
                || JumpSlot.ChargePolicySuspended || GameFeatures.Movement!=MovementMode.Vanilla
                || GameFeatures.IsMorphed || GameFeatures.IsAttached || GameFeatures.ThrustEnabled;
        }
        private sealed class Lifetime : IDisposable { public void Dispose() { Stop(); } }
        private static bool Include(Component component)
        {
            return component is JumpTrajectoryProbe
                || component.GetType().Assembly==typeof(BodyComp).Assembly && component.GetType().Name=="CameraFollowComp";
        }
        internal static string Unsupported(PlayerEntity value)
        {
            if(value==null || !value.IsAlive) return "player unavailable";
            if(PerformanceSuspension.Active || !WorldExecution.CanSimulate) return "external world control";
            if(ActionInputs.PlayerUsesToggle) return "movement must use Hold";
            return null;
        }
        internal static void Advance(float delta,long boundary)
        {
            if(!Active || player==null || !player.IsAlive) return;
            if(Reason!=null) { if(!NativePause.IsPaused) SetPause(true); return; }
            if(PerformanceSuspension.Active || !WorldExecution.CanSimulate || NativePause.IsPaused
                || Game1.instance==null || !Game1.instance.IsActive || JumpGame.instance==null || !JumpGame.instance.IsPlaying())
            { interrupted=true; return; }
            string unsupported=Unsupported(player);
            if(unsupported!=null)
            {
                Reason=unsupported; DiagnosticLog.Write("physics paused until next attempt: "+Reason);
                SetPause(true); return;
            }
            long epoch=JKRuntime.State.GameState.Snapshots.RestoreEpoch;
            if(interrupted || restoreEpoch!=epoch)
            {
                ResponsiveInput.ResetPhysics();
                if(restoreEpoch!=epoch) PhysicsDynamics.Reset();
                interrupted=false; restoreEpoch=epoch;
            }
            timestamp=boundary;
            ResponsiveInput.PhysicsFrames(boundary,!MapActivation.Enabled,inputs);
            using(NativeInputFrames.BeginPlayerFrame(inputs))
            {
                PhysicsDynamics.Begin(player.m_body,delta);
                try { lease.Advance(delta); }
                finally { PhysicsDynamics.End(); }
            }
        }
        private static void Deactivate()
        {
            bool hadPhysics=Active;
            if(lease!=null) { lease.Dispose(); lease=null; }
            if(marker!=null) { marker.Dispose(); marker=null; }
            ChargeFrameComponents.Clock=Stopwatch.GetTimestamp;
            if(hadPhysics) PhysicsDynamics.Reset();
            if(dynamics!=null) { dynamics.Dispose(); dynamics=null; }
        }
        internal static void Stop()
        {
            Deactivate(); player=null; attempted=selected=interrupted=groundedObserved=activationRefused=false; Reason=null;
        }
    }
}
