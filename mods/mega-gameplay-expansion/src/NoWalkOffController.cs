using System;
using System.Linq;
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
    // Observe native displacement after water/sand modifiers and wall resolution,
    // then stop before Y resolution can turn a grounded walk into a fall.
    internal sealed class NoWalkOffController : IDisposable, IStateParticipant
    {
        private readonly PlayerEntity player;
        private readonly InputComponent input;
        private readonly BodyPipeline pipeline;
        private readonly CommitComponent commit;
        private readonly WalkMotionGuard motionGuard;
        private bool sampled, disposed, marked, pendingMark;
        private Vector2 origin;
        private Rectangle startBox, edgeBox;
        private int screen, blockedDirection, bypassDirection;
        private bool released;
        internal MechanicState Describe()
        {
            var body = player.m_body;
            var box = body.GetHitbox();
            bool enabled = Active(box);
            bool available = body.IsOnGround && body.Velocity.Y >= 0 && motionGuard.Verified
                && !GameFeatures.IsMorphed && !GameFeatures.ThrustActive && Supported(box);
            var source = Settings.Current.NoWalkOff ? MechanicSource.Setting : MapPixels.NoWalkOff.Screens.Contains(Camera.CurrentScreen)
                ? MechanicSource.Screen : LevelManager.GetCollisionInfo(box).GetCollidedBlocks().Any(b => b is NoWalkOffZoneBlock) ? MechanicSource.Zone : MechanicSource.Surface;
            return new MechanicState(enabled, available, enabled && available, source,
                !available ? "Requires verified direct walking without external motion or inertia" : blockedDirection != 0 ? "Edge reached; release and repress to walk off" : "Flat support guard ready");
        }

        private sealed class CommitComponent : EntityComponent.Component
        {
            internal Action Flush;
            protected override void Update(float delta) { if (Flush != null) Flush(); }
        }

        private sealed class Phase : IBodyCompBehaviour
        {
            private readonly Action<BehaviourContext> action;
            internal Phase(Action<BehaviourContext> value) { action = value; }
            public bool ExecuteBehaviour(BehaviourContext context) { action(context); return true; }
        }

        internal NoWalkOffController(PlayerEntity value)
        {
            player = value;
            input = player.GetComponent<InputComponent>();
            if (input == null) throw new InvalidOperationException("No Walk Off requires native player input");
            motionGuard=new WalkMotionGuard(player.m_body);
            try
            {
                pipeline = new BodyPipeline(player.m_body, false, 100, "mega-gameplay-expansion.no-walk-off");
                commit = player.GetComponent<CommitComponent>() ?? new CommitComponent();
                pipeline.Register(BodyPhase.BeforeWind,new Phase(c=>motionGuard.Start(c.BodyComp)));
                pipeline.Register(BodyPhase.BeforeXMovement, new Phase(BeforeMove));
                pipeline.Register(BodyPhase.AfterXCollision, new Phase(AfterMove));
                var materialStage=player.m_body.GetBehaviourList().Single(b=>b.GetType().FullName=="JumpKing.BodyCompBehaviours.ExecuteBlockBehaviours");
                if (!pipeline.RegisterAfter(new Phase(c=>motionGuard.AfterMaterials(c.BodyComp)),materialStage))
                    throw new InvalidOperationException("Native material phase unavailable");
                if (player.GetComponent<CommitComponent>() == null) player.AddComponents(commit);
                commit.Flush = AfterPlayerUpdate; commit.Enabled = true;
            }
            catch { try { if(pipeline!=null) pipeline.Dispose(); } finally { motionGuard.Dispose(); } throw; }
        }

        private void Reset()
        {
            sampled = false;
            blockedDirection = bypassDirection = 0;
            released = false;
        }

        private void BeforeMove(BehaviourContext context)
        {
            sampled = false;
            BodyComp body = context.BodyComp;
            Rectangle box = body.GetHitbox();
            InputComponent.State state = input.GetState();
            bool controlled=motionGuard.Before(context,Active(box));
            if (!body.IsOnGround || body.Velocity.Y < 0 || !controlled
                || GameFeatures.IsMorphed || GameFeatures.ThrustActive
                || !Active(box) || !Supported(box))
            { Reset(); return; }

            // A relocated body, retreat, landing or scope exit needs a new stop.
            if (blockedDirection != 0 && (box != edgeBox || state.dpad.X == -blockedDirection)) Reset();
            if (blockedDirection != 0)
            {
                bool held = blockedDirection > 0 ? state.right : state.left;
                if (!held) { released = true; bypassDirection = 0; }
                else if (released) { bypassDirection = blockedDirection; released = false; }
            }
            origin = body.Position;
            startBox = box;
            screen = Camera.CurrentScreen;
            sampled = true;
        }

        private void AfterMove(BehaviourContext context)
        {
            bool controlled=motionGuard.After(context.BodyComp);
            if (!controlled) { Reset(); return; }
            if (!sampled) return;
            sampled = false;
            BodyComp body = context.BodyComp;
            if (context.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag)
                || Camera.CurrentScreen != screen || body.Position.Y != origin.Y
                || body.Velocity.Y < 0 || context.ContainsKey(ResolveXCollisionBehaviour.TouchedXSlopeFlag))
            { Reset(); return; }
            Rectangle destination = body.GetHitbox();
            int motion = Math.Sign(body.Position.X - origin.X);
            if (blockedDirection != 0 && motion == blockedDirection && bypassDirection != motion)
            {
                // Fractional water movement can remain in the same collision
                // column. Freeze it too, rather than visibly oscillating there.
                body.Position.X = edgeBox.X; body.Velocity.X = 0;
                motionGuard.Stopped(body);
                return;
            }
            int direction = Math.Sign(destination.X - startBox.X);
            if (direction == 0 || bypassDirection == direction) return;

            // Sweep every crossed collision column, including gaps crossed in a
            // single fast tick. Touching platforms are one continuous support.
            Rectangle probe = startBox;
            while (probe.X != destination.X)
            {
                Rectangle next = probe; next.X += direction;
                if (!Supported(next))
                {
                    if (!Active(probe)) { Reset(); return; }
                    body.Position.X = probe.X;
                    body.Velocity.X = 0;
                    motionGuard.Stopped(body);
                    edgeBox = probe;
                    blockedDirection = direction;
                    bypassDirection = 0;
                    bool held = direction > 0 ? input.GetState().right : input.GetState().left;
                    released = !held;
                    if (!Authored(probe) && !marked && !AllowsMap())
                    {
                        pendingMark = true;
                    }
                    return;
                }
                probe = next;
            }
        }

        private static Rectangle Feet(Rectangle box) { return new Rectangle(box.X, box.Bottom, box.Width, 1); }
        internal bool Supported(Rectangle box)
        {
            Rectangle feet = Feet(box), overlap;
            var info = LevelManager.GetCollisionInfo(feet);
            // Blocking is not the same as standing: native Y resolution turns
            // a slope-only contact into sliding. Keep its flat/slope precedence
            // so the last pixel of a joined flat platform is still reachable.
            if (info.SlopeType != SlopeType.None) return false;
            foreach (IBlock block in info.GetCollidedBlocks())
                if (block.Intersects(feet, out overlap) == BlockCollisionType.Collision_Blocking) return true;
            if (motionGuard.Plus.Supports(box,info)) return true;
            var budget=new SupportBudget(64);
            // Participating providers expose read-only state capture and a pure
            // support predicate; arbitrary collision callbacks are never replayed.
            var candidates=info.GetCollidedBlocks();
            for(int i=0;i<candidates.Count && i<64;i++)
                if(JKRuntime.RuntimeApi.Materials.Resolve(candidates[i].GetType())!=null && JKRuntime.RuntimeApi.Materials.QuerySupport(candidates[i],box,player.m_body.Velocity,
                    candidates[i].GetRect().Intersects(box),ref budget)==SupportKind.Supported)return true;
            return false;
        }
        internal static bool Authored(Rectangle box)
        {
            if (MapPixels.NoWalkOff.Screens.Contains(Camera.CurrentScreen)) return true;
            foreach (IBlock block in LevelManager.GetCollisionInfo(box).GetCollidedBlocks())
                if (block is NoWalkOffZoneBlock) return true;
            foreach (IBlock block in LevelManager.GetCollisionInfo(Feet(box)).GetCollidedBlocks())
                if (block is NoWalkOffSurfaceBlock) return true;
            return false;
        }
        private static bool Active(Rectangle box) { return Settings.Current.NoWalkOff || Authored(box); }
        internal static bool AllowsMap()
        {
            var level = Game1.instance.contentManager.level;
            return level != null && level.Info.Tags != null
                && Array.IndexOf(level.Info.Tags, "AllowMegaGameplayExpansion") >= 0;
        }
        private sealed class UsedNoWalkOff : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private void AfterPlayerUpdate()
        {
            motionGuard.End(player.m_body,input.GetState().dpad.X);
            // Warp can stop the pipeline before BeforeMove runs. A new jump or
            // transition must discard the old edge permit even in that case.
            if (!player.m_body.Enabled || !player.m_body.IsOnGround || player.m_body.Velocity.Y < 0 || !motionGuard.Verified) Reset();
            FlushMark();
        }
        private void FlushMark()
        {
            if (!pendingMark) return;
            // Native BodyComp enumerates a LinkedList. Registering even a
            // transient marker inside that enumeration invalidates it.
            var marker = new UsedNoWalkOff();
            if (RunModifiers.Register(player.m_body, marker))
            { RunModifiers.Remove(player.m_body, marker); marked = true; pendingMark = false; }
        }

        public string Id { get { return "mega-gameplay-expansion.no-walk-off"; } }
        public int Version { get { return 2; } }
        private sealed class Snapshot
        {
            internal int Blocked, Bypass, Command;
            internal bool Released, Verified;
            internal Rectangle Edge;
        }
        public object Capture()
        { return new Snapshot { Blocked = blockedDirection, Bypass = bypassDirection, Released = released, Edge = edgeBox, Verified=motionGuard.Verified, Command=motionGuard.CommandDirection }; }
        public void Validate(object snapshot)
        {
            var value = snapshot as Snapshot;
            if (value == null || Math.Abs(value.Blocked) > 1 || Math.Abs(value.Bypass) > 1 || Math.Abs(value.Command)>1
                || (value.Bypass != 0 && value.Bypass != value.Blocked))
                throw new ArgumentException("Invalid No Walk Off state");
        }
        public void Restore(object snapshot)
        {
            Validate(snapshot); var value = (Snapshot)snapshot;
            sampled = false;
            motionGuard.Restore(value.Verified,value.Command);
            blockedDirection = value.Blocked; bypassDirection = value.Bypass;
            released = value.Released; edgeBox = value.Edge;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { FlushMark(); }
            finally { Reset(); commit.Flush = null; commit.Enabled = false; try { pipeline.Dispose(); } finally { motionGuard.Dispose(); } }
        }
        internal void BindMotion() { motionGuard.Bind(player.m_body); }
    }
}
