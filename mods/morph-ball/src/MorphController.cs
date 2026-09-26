using System;
using System.Reflection;
using BehaviorTree;
using EntityComponent.BT;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal sealed partial class MorphController : IBodyCompBehaviour
    {
        private const int NormalHeight = 26;
        private const int BallHeight = 18;
        private const float SurfaceSpeed = 3f;
        private const float AirAccelerationDivisor = 6f;
        private const float AirSpeedScale = 70f / 77f;
        private const float AirAngularAcceleration = 0.018f;
        private const float MaximumAngularSpeed = SurfaceSpeed / 11f;
        private const float SnowSurfaceScale = 0.25f;
        private const float NativeContactProbeSpeed = 2.5f;

        private static readonly FieldInfo HeightField =
            typeof(BodyComp).GetField(
                "m_height",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LastVelocityField =
            typeof(BodyComp).GetField(
                "_last_velocity",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IsOnGroundField =
            typeof(BodyComp).GetField(
                "_is_on_ground",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo KnockedField =
            typeof(BodyComp).GetField(
                "_knocked",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PlayerEntity player;
        private readonly InputComponent input;
        private JumpState jumpState
        {
            get
            {
                BehaviorTreeComp current = player.GetComponent<BehaviorTreeComp>();
                return current == null ? null : MorphJumpNodeResolver.Find(current.GetRaw());
            }
        }
        private readonly FailState failState;
        private readonly IsOnGround isOnGroundState;
        private readonly MorphTransition transition = new MorphTransition();
        private readonly MorphWallBounceState wallBounce =
            new MorphWallBounceState();
        private readonly JKRuntime.Gameplay.AreaEntryObserver areaEntryDetector =
            new JKRuntime.Gameplay.AreaEntryObserver();
        private readonly MorphLandingJumpBuffer landingJumpBuffer =
            new MorphLandingJumpBuffer();
        private readonly MorphCoyoteJumpWindow coyoteJumpWindow =
            new MorphCoyoteJumpWindow();
        private readonly MorphAirControlState airControl =
            new MorphAirControlState();
        private readonly MorphFloorBounceState floorBounce =
            new MorphFloorBounceState();
        private readonly MorphSound morphSound;
        private readonly bool ownsMorphSound;
        private JKRuntime.Gameplay.PlayerControl.Lease movementLease;
        private void ReconcileControl()
        {
            if (transition.Amount > 0f || transition.TargetMorphed)
            {
                if (movementLease == null) movementLease = JKRuntime.Gameplay.PlayerControl.Acquire(player.m_body, "morph-ball");
            }
            else ReleaseControl();
        }
        private void ReleaseControl()
        { if (movementLease != null) { movementLease.Dispose(); movementLease = null; } }

        private readonly MorphSurfaceRuntimeState surfaceRuntime =
            new MorphSurfaceRuntimeState();
        private readonly MorphFrameRuntimeState frameRuntime =
            new MorphFrameRuntimeState();
        private readonly MorphJumpRuntimeState jumpRuntime =
            new MorphJumpRuntimeState();

        internal float MorphAmount { get { return transition.Amount; } }
        internal JKRuntime.State.IStateParticipant CreateStateParticipant()
        {
            return new JKRuntime.State.FieldStateParticipant("morph-ball.controller", 1, delegate {
                ReconcileControl();
                HeightField.SetValue(player.m_body, frameRuntime.UsingBallHitbox ? BallHeight : NormalHeight);
                BallFormState.Morphed = transition.Amount > 0f;
                BallFormState.Attached = frameRuntime.UsingBallHitbox && StickyPhysicsActive && surfaceRuntime.Surface != MorphSurface.None;
            }, new JKRuntime.State.StateFields(surfaceRuntime), new JKRuntime.State.StateFields(frameRuntime),
                new JKRuntime.State.StateFields(jumpRuntime), new JKRuntime.State.StateFields(transition),
                new JKRuntime.State.StateFields(wallBounce), new JKRuntime.State.StateFields(landingJumpBuffer),
                new JKRuntime.State.StateFields(coyoteJumpWindow), new JKRuntime.State.StateFields(airControl), new JKRuntime.State.StateFields(floorBounce));
        }
        internal float RollAngle { get { return surfaceRuntime.RollAngle; } }
        internal Vector2 ContactNormal
        {
            get
            {
                return surfaceRuntime.Surface == MorphSurface.None
                    ? Vector2.Zero
                    : surfaceRuntime.ActiveNormal;
            }
        }
        internal bool StickyAttached
        {
            get
            {
                return frameRuntime.UsingBallHitbox
                    && StickyPhysicsActive
                    && surfaceRuntime.Surface != MorphSurface.None;
            }
        }
        internal bool SurfaceAttached
        {
            get
            {
                return frameRuntime.UsingBallHitbox
                    && surfaceRuntime.Surface != MorphSurface.None;
            }
        }

        internal Vector2 GetVisualContactOffset(float radius)
        {
            if (!SurfaceAttached)
            {
                return Vector2.Zero;
            }
            return StickyPhysicsActive
                ? MorphSurfaceFollower.GetStickyVisualContactOffset(
                    player.m_body,
                    surfaceRuntime.ActiveBlock,
                    surfaceRuntime.ActiveNormal,
                    radius)
                : IsSlope(surfaceRuntime.ActiveNormal)
                    ? MorphSurfaceFollower.GetStickyVisualContactOffset(
                        player.m_body,
                        surfaceRuntime.ActiveBlock,
                        surfaceRuntime.ActiveNormal,
                        radius)
                    : MorphSurfaceFollower.GetRollingVisualContactOffset(
                        player.m_body,
                        surfaceRuntime.ActiveNormal,
                        radius);
        }

        internal MorphController(PlayerEntity playerEntity)
        {
            ValidateContract();
            if (playerEntity == null)
            {
                throw new ArgumentNullException("playerEntity");
            }
            player = playerEntity;
            input = player.GetComponent<InputComponent>();
            BehaviorTreeComp tree = player.GetComponent<BehaviorTreeComp>();
            failState = tree == null
                ? null
                : tree.GetRaw().FindNode<FailState>();
            isOnGroundState = tree == null
                ? null
                : tree.GetRaw().FindNode<IsOnGround>();
            if (input == null
                || failState == null
                || isOnGroundState == null)
            {
                throw new InvalidOperationException(
                    "Jump King player input is unavailable");
            }
            MorphLandingEffects.ValidateContract();
            ownsMorphSound = ModEntry.PreparedSound == null;
            morphSound = ModEntry.PreparedSound ?? new MorphSound();
            try
            {
                if (MorphStateStore.Load() && JKRuntime.Gameplay.PlayerControl.TryAcquire(player.m_body, "morph-ball", out movementLease))
                {
                    transition.KeepMorphed();
                    RestoreCollapsed(player.m_body);
                }
            }
            catch (Exception failure)
            {
                var rollback = new JKRuntime.RuntimeScope();
                if (ownsMorphSound) rollback.Own(morphSound);
                rollback.Defer(ReleaseControl);
                rollback.Defer(delegate { HeightField.SetValue(player.m_body, NormalHeight); });
                try { rollback.Dispose(); } catch (Exception cleanup) { throw new AggregateException("Ball restoration and cleanup failed", failure, cleanup); }
                throw;
            }
        }

        internal static void ValidateContract()
        {
            if (HeightField == null
                || LastVelocityField == null
                || IsOnGroundField == null
                || KnockedField == null)
            {
                throw new InvalidOperationException(
                    "Jump King body physics contract is unavailable");
            }
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            BodyComp body = context.BodyComp;
            if (!JKRuntime.Gameplay.PlayerControl.Available(body, "morph-ball"))
            {
                frameRuntime.MorphWasHeld = MorphInput.IsMorphHeld(); frameRuntime.JumpWasHeld = input.GetState().jump;
                return true;
            }
            ApplyPendingFloorBounce(body);
            InputComponent.State state = input.GetState();
            int direction = state.dpad.X;
            bool morphHeld = MorphInput.IsMorphHeld();
            bool morphPressed = morphHeld && !frameRuntime.MorphWasHeld;
            bool jumpPressed = state.jump && !frameRuntime.JumpWasHeld;
            frameRuntime.MorphWasHeld = morphHeld;
            frameRuntime.JumpWasHeld = state.jump;
            bool stickyMode = StickyPhysicsActive;
            if (frameRuntime.UsingBallHitbox)
            {
                wallBounce.BeginFrame(
                    direction,
                    body.IsOnGround,
                    stickyMode);
                if (wallBounce.Sliding)
                {
                    wallBounce.ReleaseIfDetached(
                        IsWallAdjacent(body, wallBounce.WallDirection));
                }
            }
            else
            {
                wallBounce.Reset();
            }

            if (morphPressed)
            {
                if (!transition.TargetMorphed && movementLease == null
                    && !JKRuntime.Gameplay.PlayerControl.TryAcquire(body, "morph-ball", out movementLease)) return true;
                bool previousTarget = transition.TargetMorphed;
                bool canUnfold = !transition.TargetMorphed
                    || CanExpand(body, surfaceRuntime.Surface);
                transition.Toggle(canUnfold);
                if (previousTarget != transition.TargetMorphed)
                {
                    morphSound.Play(transition.TargetMorphed);
                }
                MorphStateStore.Save(transition.TargetMorphed);
            }

            bool previousBallHitbox = transition.UsesBallHitbox;
            transition.Update();
            bool nextBallHitbox = transition.UsesBallHitbox;
            if (!previousBallHitbox && nextBallHitbox)
            {
                Collapse(body);
            }
            else if (previousBallHitbox && !nextBallHitbox)
            {
                if (!Expand(body, surfaceRuntime.Surface, false))
                {
                    transition.KeepMorphed();
                    MorphStateStore.Save(true);
                    nextBallHitbox = true;
                }
            }
            frameRuntime.UsingBallHitbox = nextBallHitbox;

            if (frameRuntime.UsingBallHitbox)
            {
                if (failState.IsRunning())
                {
                    failState.ResetResult();
                }
                UpdateBall(
                    body,
                    direction,
                    state.jump,
                    jumpPressed,
                    IsSupportedBySand(body, context),
                    context.CollisionInfo.StartOfFrameCollisionInfo);
            }
            else if (transition.Amount <= 0f)
            {
                ResetBallState();
            }

            BallFormState.Enabled = true;
            ReconcileControl();
            BallFormState.Morphed = transition.Amount > 0f;
            BallFormState.Attached = frameRuntime.UsingBallHitbox
                && StickyPhysicsActive
                && surfaceRuntime.Surface != MorphSurface.None;
            return true;
        }

    }
}
