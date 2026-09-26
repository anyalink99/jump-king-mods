using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JKRuntime.State;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed class WarpController : IBodyCompBehaviour, IDisposable, IStateParticipant
    {
        private readonly PlayerEntity player;
        private readonly BodyPipeline pipeline;
        private readonly WarpVisual visual;
        private readonly BehaviorTreeComp brain;
        private readonly InputComponent input;
        private readonly WarpLandingSound landingSound;
        private Plan plan;
        private FlightJob forecast;
        private IDisposable presentation;
        private JKRuntime.RuntimeScope suspension;
        private float age;
        private bool transferred;
        private sealed class Plan
        {
            internal BodyComp Launch, Landing;
            internal WarpImage Image;
            internal FlightJob.Seed Seed;
            internal bool BodyEnabled, BrainEnabled, MarkModified, Global;
        }
        private bool armed, attempted, disposed;
        private string lastFailure;
        internal MechanicState Describe()
        {
            var box = player.m_body.GetHitbox();
            bool zone = LevelManager.GetCollisionInfo(box).GetCollidedBlocks().Any(b => b is WarpZoneBlock);
            bool screenRule = MegaBlockFactory.WarpScreens.Contains(Camera.CurrentScreen);
            bool enabled = Settings.Current.WarpJump || armed || zone || screenRule || plan != null;
            bool available = !GameFeatures.IsMorphed && !GameFeatures.ThrustEnabled && GameFeatures.Movement == MovementMode.Vanilla;
            var source = plan != null ? (plan.Global ? MechanicSource.Setting : MechanicSource.Controller)
                : Settings.Current.WarpJump ? MechanicSource.Setting : screenRule ? MechanicSource.Screen : zone ? MechanicSource.Zone : MechanicSource.Surface;
            return new MechanicState(enabled, available, enabled && available && plan != null, source,
                !available ? "Alternative movement or equipped Jetpack" : lastFailure ?? (plan != null ? "Warp transition" : "Waiting for supported departure"));
        }
        internal WarpController(PlayerEntity value)
        {
            player = value;
            var brainField=typeof(PlayerEntity).GetField("m_bt",BindingFlags.Instance|BindingFlags.NonPublic);
            if(brainField==null) throw new InvalidOperationException("Native player behaviour tree contract unavailable");
            brain=(BehaviorTreeComp)brainField.GetValue(player);
            input=player.GetComponent<InputComponent>();
            if(brain==null || input==null) throw new InvalidOperationException("Native player input/tree unavailable for warp transition");
            landingSound=new WarpLandingSound(player);
            visual = player.GetComponent<WarpVisual>() ?? new WarpVisual(player);
            var componentField=typeof(EntityComponent.Entity).GetField("m_components",BindingFlags.Instance|BindingFlags.NonPublic);
            var components=componentField==null?null:componentField.GetValue(player) as List<EntityComponent.Component>;
            if(components==null || components.IndexOf(player.m_body)<0 || components.IndexOf(input)<=components.IndexOf(player.m_body) || components.IndexOf(brain)<=components.IndexOf(input))
                throw new InvalidOperationException("Native body/input/tree component order unavailable for warp handoff");
            pipeline = new BodyPipeline(player.m_body, false, -100, "mega-gameplay-expansion");
            try
            {
                // Let current-frame form/thrust/controller decisions run first.
                // Forecast continues this tick from X, rather than repeating
                // the wind and water-state cache that already ran on the body.
                pipeline.Register(BodyPhase.BeforeXMovement, this);
                if (player.GetComponent<WarpVisual>() == null) player.AddComponents(visual);
                // Complete the landing AFTER this tick's body slot but BEFORE
                // input/BT. Native Walk/charge then process the landing in this
                // same tick, before the next body update can reuse flight Vx.
                // Move only our component; all existing relative order is kept.
                components.Remove(visual); components.Insert(components.IndexOf(input),visual);
                visual.Reset(); visual.Enabled = true; visual.Advance=Advance;
            }
            catch { pipeline.Dispose(); throw; }
        }
        public bool ExecuteBehaviour(BehaviourContext context)
        {
            if(plan!=null) return false;
            BodyComp body = context.BodyComp;
            bool supported = body.IsOnGround || body.IsOnBlock(typeof(SandBlock));
            bool departing = !supported || body.Velocity.Y < 0;
            bool inZone = LevelManager.GetCollisionInfo(body.GetHitbox()).GetCollidedBlocks().Any(b => b is WarpZoneBlock);
            bool onSurface = false;
            if (supported)
            {
                Rectangle feet = body.GetHitbox(); feet.Y++;
                onSurface = LevelManager.GetCollisionInfo(feet).GetCollidedBlocks().Any(b => b is WarpSurfaceBlock);
                armed = onSurface || inZone;
                if (!departing) attempted = false;
            }
            bool authored = armed || inZone || MegaBlockFactory.WarpScreens.Contains(Camera.CurrentScreen);
            // Entering a zone during an ordinary fall is a new activation.
            // A failed forecast is not retried every tick while staying inside it.
            if(!authored && !Settings.Current.WarpJump) attempted=false;
            if (!departing || attempted || !(authored || Settings.Current.WarpJump)) return true;
            attempted = true;
            if (GameFeatures.IsMorphed || GameFeatures.ThrustEnabled || GameFeatures.Movement != MovementMode.Vanilla)
            { Report("Active alternative movement controller"); return true; }
            try
            {
                var world = new FlightWorld(FlightWorld.InstalledScreens, Camera.CurrentScreen, 0);
                world.CaptureWindClock();
                var seed = new FlightJob.Seed(body, world);
                forecast = new FlightJob(seed);
                plan = new Plan { Launch=seed.Launch, Image=visual.CaptureImage(), Seed=seed,
                    BodyEnabled=body.Enabled, BrainEnabled=brain.Enabled,
                    MarkModified=!authored && !AllowsMap(), Global=!authored };
                plan.Image.PrepareDeparture(FlightWorld.InstalledScreens,plan.Launch.Position);
                if (!Compute()) { plan=null; forecast=null; return true; }
            }
            catch(Exception error) { plan=null; forecast=null; Report(error.Message); return true; }
            age=0; transferred=false; armed=false;
            try {
                Freeze();
                visual.Present(plan.Image,plan.Launch.Position,plan.Landing == null ? plan.Launch.Position : plan.Landing.Position,age);
            }
            catch (Exception error) { Report("Warp departure failed: " + error.Message); Cancel(); return true; }
            lastFailure = null;
            return false;
        }
        private bool Compute()
        {
            forecast.Step();
            if (!forecast.Done) return true;
            if (forecast.Failure != null) { Report(forecast.Failure); return false; }
            // Match native FailState's landing test; do not approximate it by
            // height or by the zero vertical speed after collision resolution.
            var sprites=Game1.instance.contentManager.playerSprites;
            var image=plan.Image.WithArrival(forecast.Landing.LastVelocity.Y==PlayerValues.MAX_FALL ? sprites.splat : sprites.idle,
                Camera.TransformVector2(forecast.Landing.Position+new Vector2(9,26)));
            image.PrepareArrival(FlightWorld.InstalledScreens, forecast.Landing.Position);
            // Never mutate a plan already retained by a Runtime snapshot.
            plan = new Plan { Launch=plan.Launch, Landing=forecast.Landing, Image=image,
                BodyEnabled=plan.BodyEnabled, BrainEnabled=plan.BrainEnabled,
                MarkModified=plan.MarkModified, Global=plan.Global };
            WarpDiagnostics.Write("Warp begun: nativeTicks=" + forecast.Ticks + " screen=" + (Camera.CurrentScreen+1)
                + " origin=" + plan.Launch.Position + " landing=" + plan.Landing.Position
                + " forecastSlices=" + forecast.Slices + " workMs=" + forecast.WorkMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)
                + " maxSliceMs=" + forecast.MaxSliceMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
            forecast=null;
            return true;
        }
        private void Freeze()
        {
            if (suspension == null)
            {
                suspension = new JKRuntime.RuntimeScope();
                try
                {
                    suspension.Own(ComponentSuspension.Acquire("mega-gameplay-expansion.warp", player.m_body, plan.BodyEnabled));
                    suspension.Own(ComponentSuspension.Acquire("mega-gameplay-expansion.warp", brain, plan.BrainEnabled));
                }
                catch { suspension.Dispose(); suspension = null; throw; }
            }
            if (presentation == null) presentation = PresentationActivity.Begin("mega-gameplay-expansion.warp", player.m_body);
            player.m_body.Enabled=false; brain.Enabled=false;
            // Native input keeps polling every game tick. It latches a fresh
            // held press and cancels it on release, just like an airborne buffer.
            // Only the tree is paused, so charge cannot accrue during assembly.
            player.m_body.Velocity=Vector2.Zero;
        }
        // Driven by a normal player component: pauses stop the transition too.
        // The body stays at origin until the whole dissolve and blank interval end.
        internal void Advance(float delta)
        {
            try { AdvanceCore(delta); }
            catch (Exception error)
            {
                Report("Warp transition failed: " + error.Message);
                Cancel();
            }
        }
        private void AdvanceCore(float delta)
        {
            if(plan==null) return;
            if((plan.Global && !Settings.Current.WarpJump) || GameFeatures.IsMorphed || GameFeatures.ThrustEnabled || GameFeatures.Movement!=MovementMode.Vanilla)
            { Cancel(); return; }
            if(float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (plan.Landing == null)
            {
                if (delta <= 0) return;
                try { if (!Compute()) { Cancel(); return; } }
                catch (Exception error) { Report(error.Message); Cancel(); return; }
                if (plan.Landing == null)
                {
                    age=Math.Min(MatrixPixels.Transfer-.0001f,age+Math.Max(0,Math.Min(.05f,delta)));
                    visual.Present(plan.Image,plan.Launch.Position,plan.Launch.Position,age);
                    return;
                }
            }
            age=Math.Min(MatrixPixels.Duration,age+Math.Max(0,Math.Min(.05f,delta)));
            if(!transferred && age>=MatrixPixels.Transfer)
            {
                NativeFlight.Commit(plan.Landing,player.m_body,NativeFlight.Get<BehaviourContext>(player.m_body,"m_behaviourContext"));
                transferred=true;
                Camera.UpdateCamera(player.m_body.GetHitbox().Center);
                GameplayEvents.NotifyTeleport("mega-gameplay-expansion.warp", player.m_body.Position, player.m_body.Velocity, Camera.CurrentScreen);
                if(plan.MarkModified)
                { var marker=new UsedWarpJump(); if(RunModifiers.Register(player.m_body,marker)) RunModifiers.Remove(player.m_body,marker); }
                WarpDiagnostics.Write("Warp transferred: position="+player.m_body.Position);
            }
            player.m_body.Velocity=Vector2.Zero;
            visual.Present(plan.Image,plan.Launch.Position,plan.Landing.Position,age);
            if(age>=MatrixPixels.Duration) Finish();
        }
        private void Finish()
        {
            // Restore the native landing state only after the last pixel returns.
            try {
                NativeFlight.Commit(plan.Landing,player.m_body,NativeFlight.Get<BehaviourContext>(player.m_body,"m_behaviourContext"));
                landingSound.Play(player.m_body);
            }
            catch (Exception error) {
                NativeFlight.CopyState(plan.Landing,player.m_body);
                Report("Landing state changed during Warp: " + error.Message);
            }
            finally { Release(); plan=null; forecast=null; visual.Reset(true); attempted=false; }
        }
        private void Release()
        {
            if (suspension != null) { suspension.Dispose(); suspension = null; }
            if (presentation != null) { presentation.Dispose(); presentation = null; }
        }
        private void Cancel()
        {
            if(plan==null) return;
            try {
                NativeFlight.Commit(transferred?plan.Landing:plan.Launch,player.m_body,NativeFlight.Get<BehaviourContext>(player.m_body,"m_behaviourContext"));
                if(transferred) landingSound.Play(player.m_body);
            }
            catch (Exception error) {
                // A foreign controller can replace block behaviours mid-effect.
                // Restore pose/momentum even when its new material state cannot
                // accept the old forecast, and always release our suspensions.
                NativeFlight.CopyState(transferred?plan.Landing:plan.Launch,player.m_body);
                Report("Warp cancellation restored body only: " + error.Message);
            }
            finally { Release(); plan=null; forecast=null; visual.Reset(transferred); }
        }
        private void Report(string reason)
        {
            if (reason == lastFailure) return;
            lastFailure = reason;
            WarpDiagnostics.Write("Warp skipped; ordinary flight retained: " + reason + "; screen=" + (Camera.CurrentScreen + 1)
                + " position=" + player.m_body.Position + " velocity=" + player.m_body.Velocity);
        }
        private static bool AllowsMap()
        {
            var level = Game1.instance.contentManager.level;
            return level != null && level.Info.Tags != null && level.Info.Tags.Contains("AllowMegaGameplayExpansion");
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { Cancel(); }
            finally { visual.Advance=null; visual.Reset(); visual.Enabled = false; pipeline.Dispose(); }
        }
        public string Id { get { return "mega-gameplay-expansion.warp"; } }
        public int Version { get { return 6; } }
        private sealed class Snapshot
        { internal bool Armed,Attempted,Transferred; internal float Age; internal Plan Plan; }
        public object Capture() { return new Snapshot { Armed=armed,Attempted=attempted,Transferred=transferred,Age=age,Plan=plan }; }
        public void Validate(object snapshot)
        {
            var value=snapshot as Snapshot;
            if(value==null || float.IsNaN(value.Age) || value.Age<0 || value.Age>MatrixPixels.Duration)
                throw new ArgumentException("Invalid warp state");
        }
        public void Restore(object snapshot)
        {
            Validate(snapshot); var value=(Snapshot)snapshot;
            if(plan!=null) Release();
            visual.Reset(); plan=value.Plan; age=value.Age; transferred=value.Transferred; armed=value.Armed; attempted=value.Attempted;
            forecast=plan!=null && plan.Landing==null ? new FlightJob(plan.Seed) : null;
            if(plan!=null)
            {
                NativeFlight.Commit(transferred?plan.Landing:plan.Launch,player.m_body,NativeFlight.Get<BehaviourContext>(player.m_body,"m_behaviourContext"));
                Freeze(); Camera.UpdateCamera(player.m_body.GetHitbox().Center);
                visual.Present(plan.Image,plan.Launch.Position,plan.Landing == null ? plan.Launch.Position : plan.Landing.Position,age);
            }
        }
        private sealed class UsedWarpJump : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
    }
}
