using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JKRuntime.Gameplay;
using JKRuntime.State;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal sealed class AirDashController : IBodyCompBehaviour, IDisposable, IStateParticipant
    {
        internal const float Distance = 80f, Speed = 6f, ExitSpeed = Speed * .5f;
        private readonly PlayerEntity player;
        private readonly InputComponent input;
        private readonly BodyPipeline pipeline;
        private readonly AirDashVisual visual;
        private readonly Action playSound, stopSound;
        private readonly DashInput dashInput = new DashInput();
        private readonly Func<bool, DashPress> readDash;
        private readonly JKRuntime.RuntimeScope resources = new JKRuntime.RuntimeScope();
        private bool pauseObserved, wasPaused;
        private readonly IBodyCompBehaviour resolveX, cap, teleport, bump, cacheScreen;
        private readonly LinkedList<IBlockBehaviour> blocks;
        private bool disposed, active, used, grounded, surface, grant, global, pendingMark, marked;
        private float remaining;
        private int direction;
        private Vector2 originalVelocity;
        private PlayerControl.Lease movementLease;
        internal bool Active { get { return active; } }
        internal bool Used { get { return used; } }
        internal float Remaining { get { return remaining; } }
        internal AirDashController(PlayerEntity value, Action playAudio=null, Action stopAudio=null, Func<bool, DashPress> inputReader=null)
        {
            readDash = inputReader ?? dashInput.Read;
            playSound=playAudio; stopSound=stopAudio;
            player=value; input=player.GetComponent<InputComponent>();
            if (input==null) throw new InvalidOperationException("Air Dash requires native input");
            var list=player.m_body.GetBehaviourList();
            resolveX=list.Single(b=>b is ResolveXCollisionBehaviour);
            cap=list.Single(b=>b is CapPositionBehaviour);
            teleport=list.Single(b=>b is HandlePlayerTeleportBehaviour);
            bump=list.Single(b=>b.GetType().Name=="PlayBumpSFXBehaviour");
            cacheScreen=list.Single(b=>b.GetType().Name=="CacheLastScreenBehaviour");
            blocks=NativeFlight.Get<LinkedList<IBlockBehaviour>>(player.m_body,"m_blockBehaviours");
            visual=player.GetComponent<AirDashVisual>() ?? new AirDashVisual(player);
            var components=typeof(EntityComponent.Entity).GetField("m_components",NativeFlight.Fields).GetValue(player) as List<EntityComponent.Component>;
            var brain=typeof(PlayerEntity).GetField("m_bt",NativeFlight.Fields).GetValue(player) as EntityComponent.Component;
            if (components==null || brain==null || components.IndexOf(input)<0 || components.IndexOf(brain)<=components.IndexOf(input))
                throw new InvalidOperationException("Air Dash needs native input-before-tree component order");
            pipeline=resources.Own(new BodyPipeline(player.m_body,false,400,"mega-gameplay-expansion.air-dash"));
            try
            {
                pipeline.Register(BodyPhase.BeforeWind,this);
                if (player.GetComponent<AirDashVisual>()==null) resources.Own(new ComponentAttachment(player,visual));
                resources.Defer(delegate { visual.AfterInput=null; visual.Enabled=false; });
                components.Remove(visual); components.Insert(components.IndexOf(input)+1,visual);
                visual.AfterInput=AfterInput; visual.Enabled=true;
                resources.Own(NativePause.Subscribe("mega-gameplay-expansion.air-dash", ObservePause));
                resources.Defer(delegate { Cancel(); });
                resources.Defer(FlushMark);
            }
            catch (Exception failure) { try { resources.Dispose(); } catch (Exception cleanup) { throw new AggregateException("Air Dash installation and cleanup failed", failure, cleanup); } throw; }
        }
        private void ObservePause(bool paused, long timestamp)
        {
            // NativePause reports every tick. Only a transition invalidates
            // input edges; repeated unpaused observations must retain them.
            if (!pauseObserved || paused != wasPaused) dashInput.Reset();
            pauseObserved=true; wasPaused=paused;
        }
        private bool Available()
        {
            return player.m_body.Enabled && !PresentationActivity.IsActive(player.m_body)
                && PlayerControl.Available(player.m_body, Id)
                && !GameFeatures.IsMorphed && !GameFeatures.ThrustActive && GameFeatures.Movement==MovementMode.Vanilla;
        }
        private bool Authored()
        {
            return grant || MapPixels.AirDash.Screens.Contains(Camera.CurrentScreen)
                || LevelManager.GetCollisionInfo(player.m_body.GetHitbox()).GetCollidedBlocks().Any(b=>b is AirDashZoneBlock);
        }
        internal MechanicState Describe()
        {
            bool authored=Authored(), enabled=Settings.Current.AirDash || authored;
            return new MechanicState(enabled || active,active || (Available() && !used),active,
                authored?MechanicSource.Controller:MechanicSource.Setting,
                active?"Horizontal dash":used?"Dash spent until landing":"Press the dash binding in flight");
        }
        // Runs AFTER native InputComponent.Update and BEFORE the behaviour tree.
        // Consume only the activation press. Later presses, even during the dash
        // movement ticks, remain native buffer tokens (including with SFC).
        internal void AfterInput(float delta)
        {
            if (disposed) return;
            DashPress press = readDash(input.GetPressedState().jump);
            FlushMark();
            if (!Available()) { Cancel(false); grounded=false; surface=grant=false; return; }
            var body=player.m_body;
            bool support=body.IsOnGround || body.IsOnBlock(typeof(SandBlock));
            if (!active && support && body.Velocity.Y>=0)
            {
                used=false; grant=false; grounded=true;
                var feet=body.GetHitbox(); feet.Y++;
                surface=LevelManager.GetCollisionInfo(feet).GetCollidedBlocks().Any(b=>b is AirDashSurfaceBlock);
            }
            else if (!support && grounded)
            {
                grant=surface && (body.Velocity.Y<0 || body.LastVelocity.Y<0); grounded=false; surface=false;
            }
            if (active || used || support || !press.Pressed) return;
            bool authored=Authored();
            if (!authored && !Settings.Current.AirDash) return;
            originalVelocity=body.Velocity;
            if (float.IsNaN(originalVelocity.X) || float.IsInfinity(originalVelocity.X) || float.IsNaN(originalVelocity.Y) || float.IsInfinity(originalVelocity.Y)) return;
            direction=ChooseDirection(input.GetState().dpad.X, originalVelocity.X,
                ((SpriteEffects)typeof(PlayerEntity).GetField("m_flip",NativeFlight.Fields).GetValue(player) & SpriteEffects.FlipHorizontally)!=0 ? -1 : 1);
            if (!PlayerControl.TryAcquire(player.m_body, Id, out movementLease)) return;
            try { visual.Begin(direction); }
            catch (Exception error) { ReleaseControl(); WarpDiagnostics.Write("Air Dash visual unavailable: "+error.Message); return; }
            if (press.SharedJump) input.TryConsumeJump();
            used=active=true; global=!authored; remaining=Distance;
            WarpDiagnostics.Write("Air Dash begun: direction="+direction+" distance="+Distance+" speed="+Speed);
        }
        internal static int ChooseDirection(int held, float velocity, int facing)
        { return held != 0 ? Math.Sign(held) : velocity != 0 ? Math.Sign(velocity) : facing; }
        public bool ExecuteBehaviour(BehaviourContext context)
        {
            if (!active) return true;
            if (!Available()) { Cancel(false); return true; }
            if (global && !Settings.Current.AirDash) { Cancel(); return true; }
            // Sound follows actual movement, not the earlier input observation.
            // Restoring a partially completed dash must not replay its attack.
            if (remaining==Distance && playSound!=null) playSound();
            var body=context.BodyComp;
            if (global && !marked && !NoWalkOffController.AllowsMap()) pendingMark=true;
            body.Velocity=new Vector2(direction*Speed,0);
            NativeFlight.Set(body,"_last_velocity",body.Velocity);
            NativeFlight.Set(body,"_is_on_ground",false);
            cacheScreen.ExecuteBehaviour(context);
            float distance=Math.Min(Speed,remaining);
            for (float travelled=0; travelled<distance; )
            {
                float step=Math.Min(1,distance-travelled);
                float previous=body.Position.X;
                body.Position.X+=direction*step;
                cap.ExecuteBehaviour(context);
                teleport.ExecuteBehaviour(context);
                if (context.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag))
                { Finish(); visual.Clear(); return false; }
                if (Math.Abs(body.Position.X-previous)<step-.00001f)
                { Finish(); visual.End(false); return false; }
                Rectangle overlap; AdvCollisionInfo info;
                bool hit=LevelManager.CheckCollision(body.GetHitbox(),out overlap,out info);
                if (!hit) foreach(var block in blocks) if(block.AdditionalXCollisionCheck(info,context)) { hit=true; break; }
                if (hit)
                {
                    // Native walls multiply Vx by -PlayerValues.BOUNCE (0.5).
                    // Slopes use the native normal/projection instead of a fake
                    // rectangle wall. Sweeping <=1 px prevents thin-wall tunnelling.
                    resolveX.ExecuteBehaviour(context);
                    Finish(); visual.End(true);
                    try { bump.ExecuteBehaviour(context); }
                    catch (Exception error) { WarpDiagnostics.Write("Air Dash bump audio failed: "+error.Message); }
                    return false;
                }
                travelled+=step; remaining=Math.Max(0,remaining-step);
            }
            visual.Sample(body.Position);
            if (remaining<=0)
            {
                // Reduce only a completed free dash, never a native collision
                // response or a velocity assigned by a teleport/controller.
                body.Velocity=new Vector2(direction*ExitSpeed,0);
                Finish(); visual.End(false);
            }
            // No wind, gravity or material displacement multipliers during dash.
            // Native input and tree components are deliberately not suspended.
            return false;
        }
        private void Finish()
        {
            // Preserve the chosen exit impulse or native collision response.
            active=false;
            ReleaseControl();
        }
        private void Cancel(bool restoreVelocity=true)
        {
            // An alternative controller or Warp may already own the body.
            // Never overwrite its newly established velocity on handoff.
            if (active && restoreVelocity) player.m_body.Velocity=originalVelocity;
            active=false;
            ReleaseControl();
            visual.Clear();
            if (stopSound!=null) stopSound();
        }
        private sealed class UsedAirDash : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private void FlushMark()
        {
            if (!pendingMark) return;
            var marker=new UsedAirDash();
            if (RunModifiers.Register(player.m_body,marker)) { RunModifiers.Remove(player.m_body,marker); pendingMark=false; marked=true; }
        }
        public string Id { get { return "mega-gameplay-expansion.air-dash"; } }
        private void ReleaseControl() { if (movementLease != null) { movementLease.Dispose(); movementLease=null; } }
        public int Version { get { return 1; } }
        private sealed class Snapshot
        {
            internal bool Active, Used, Grounded, Surface, Grant, Global;
            internal int Direction;
            internal float Remaining;
            internal Vector2 Velocity;
        }
        public object Capture()
        { return new Snapshot { Active=active,Used=used,Grounded=grounded,Surface=surface,Grant=grant,Global=global,Direction=direction,Remaining=remaining,Velocity=originalVelocity }; }
        public void Validate(object value)
        {
            var state=value as Snapshot;
            if (state==null || Math.Abs(state.Direction)>1 || (state.Active && (state.Direction==0 || !state.Used))
                || float.IsNaN(state.Remaining) || state.Remaining<0 || state.Remaining>Distance
                || float.IsNaN(state.Velocity.X) || float.IsNaN(state.Velocity.Y) || float.IsInfinity(state.Velocity.X) || float.IsInfinity(state.Velocity.Y))
                throw new ArgumentException("Invalid Air Dash state");
        }
        public void Restore(object value)
        {
            Validate(value); var state=(Snapshot)value;
            if (state.Active && movementLease==null) movementLease=PlayerControl.Acquire(player.m_body, Id);
            if (!state.Active) ReleaseControl();
            dashInput.Reset();
            if (stopSound!=null) stopSound();
            visual.Clear(); active=state.Active; used=state.Used; grounded=state.Grounded; surface=state.Surface;
            grant=state.Grant; global=state.Global; direction=state.Direction; remaining=state.Remaining; originalVelocity=state.Velocity;
            if (active) visual.Begin(direction);
        }
        public void Dispose()
        {
            disposed=true; resources.Dispose();
        }
    }
}
