using System;
using System.Reflection;
using BehaviorTree;
using EntityComponent.BT;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace CasualJumping
{
    internal sealed class CasualController : IBodyCompBehaviour
    {
        private const float AirAccelerationDivisor = 6f;

        private static readonly MethodInfo DoJumpMethod = typeof(JumpState).GetMethod(
            "DoJump",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PlayerEntity player;
        private readonly InputComponent input;
        private readonly JumpState jumpState;
        private readonly FailState failState;
        private readonly SurfaceControlState surfaceControl =
            new SurfaceControlState();
        private readonly AirControlState airControl =
            new AirControlState();
        private readonly LandingJumpBuffer landingJumpBuffer =
            new LandingJumpBuffer();
        private readonly CoyoteJumpWindow coyoteJumpWindow =
            new CoyoteJumpWindow();

        private bool assistedJumpActive;
        private bool assistedFlightActive;
        private bool earlyReleaseActive;
        private int heldFrame;
        private int heldDirection;
        private float horizontalVelocityBeforeCollision;
        internal JKRuntime.State.IStateParticipant CreateStateParticipant()
        {
            return new JKRuntime.State.FieldStateParticipant("casual-jumping.controller", 1, null,
                new JKRuntime.State.StateFields(this), new JKRuntime.State.StateFields(surfaceControl),
                new JKRuntime.State.StateFields(airControl), new JKRuntime.State.StateFields(landingJumpBuffer),
                new JKRuntime.State.StateFields(coyoteJumpWindow));
        }

        internal CasualController(PlayerEntity playerEntity)
        {
            if (playerEntity == null)
            {
                throw new ArgumentNullException("playerEntity");
            }
            if (DoJumpMethod == null)
            {
                throw new MissingMethodException(
                    typeof(JumpState).FullName,
                    "DoJump(float)");
            }

            player = playerEntity;
            input = player.GetComponent<InputComponent>();
            BehaviorTreeComp tree = player.GetComponent<BehaviorTreeComp>();
            jumpState = tree == null
                ? null
                : tree.GetRaw().FindNode<JumpState>();
            failState = tree == null
                ? null
                : tree.GetRaw().FindNode<FailState>();

            if (input == null || jumpState == null || failState == null)
            {
                throw new InvalidOperationException(
                    "Jump King player controls are unavailable");
            }
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            if (JKRuntime.Gameplay.GameFeatures.IsMorphed || !JKRuntime.Gameplay.PlayerControl.Available(player.m_body, "casual-jumping"))
            {
                SuspendForMorph();
                return true;
            }
            BodyComp body = context.BodyComp;
            InputComponent.State state = input.GetState();
            InputComponent.State pressed = input.GetPressedState();
            ControlMode mode = SettingsStore.Current.Mode;
            bool jumpSupported = IsJumpSupported(body, context);
            bool jetpackEnabled = JKRuntime.Gameplay.GameFeatures.ThrustEnabled;
            if (mode == ControlMode.CasualPlus
                && JKRuntime.Gameplay.GameFeatures.ThrustActive)
            {
                CancelAssistedJumpForJetpack();
            }

            coyoteJumpWindow.Update(
                mode == ControlMode.CasualPlus,
                jumpSupported,
                assistedJumpActive);

            heldDirection = state.dpad.X;
            if (mode == ControlMode.CasualPlus && !jetpackEnabled)
            {
                landingJumpBuffer.Update(
                    !jumpSupported,
                    state.jump,
                    pressed.jump);
            }
            else
            {
                landingJumpBuffer.Reset();
            }
            UpdateSurfaceState(body, context);

            bool assistedMode = mode == ControlMode.CasualPlus;
            bool launchedThisFrame = assistedMode
                && UpdateCasualPlusJump(
                    body,
                    state,
                    pressed,
                    jumpSupported);

            if (!assistedMode)
            {
                ResetAssistedJump();
                landingJumpBuffer.Reset();
            }

            bool airborne = launchedThisFrame
                || !body.IsOnGround
                || body.Velocity.Y < 0f;
            if (airborne
                && surfaceControl.AllowsAirControl(heldDirection))
            {
                float speedLimit = PlayerValues.SPEED;
                if (assistedMode && assistedFlightActive)
                {
                    speedLimit *= CasualPhysics.CasualPlusHorizontalSpeedScale;
                }
                ApplyAirControl(body, heldDirection, speedLimit);
            }
            else if (surfaceControl.WallSliding)
            {
                body.Velocity.X = 0f;
                airControl.Reset();
            }
            else if (!airborne)
            {
                airControl.Reset();
            }

            return true;
        }

        internal void ObserveVelocityBeforeMovement(BehaviourContext context)
        {
            if (JKRuntime.Gameplay.GameFeatures.IsMorphed || !JKRuntime.Gameplay.PlayerControl.Available(player.m_body, "casual-jumping"))
            {
                return;
            }
            horizontalVelocityBeforeCollision = context.BodyComp.Velocity.X;
        }

        internal void ObserveXCollision(BehaviourContext context)
        {
            if (JKRuntime.Gameplay.GameFeatures.IsMorphed || !JKRuntime.Gameplay.PlayerControl.Available(player.m_body, "casual-jumping"))
            {
                return;
            }
            BodyComp body = context.BodyComp;
            if (context.ContainsKey(ResolveXCollisionBehaviour.TouchedXSlopeFlag))
            {
                airControl.Reset();
                surfaceControl.ObserveSlopeContact(
                    SlopeContactDetector.GetBlockedDirection(
                        context.CollisionInfo.PreResolutionCollisionInfo));
                return;
            }

            bool bounced = body.IsKnocked
                && Math.Abs(horizontalVelocityBeforeCollision) >= 0.0001f
                && horizontalVelocityBeforeCollision * body.Velocity.X < 0f;
            if (!bounced)
            {
                return;
            }

            airControl.Reset();

            int travelDirection = horizontalVelocityBeforeCollision > 0f
                ? 1
                : -1;
            if (surfaceControl.ObserveWallCollision(
                travelDirection,
                heldDirection))
            {
                body.Velocity.X = 0f;
            }
        }

        internal void ObserveYCollision(BehaviourContext context)
        {
            if (JKRuntime.Gameplay.GameFeatures.IsMorphed || !JKRuntime.Gameplay.PlayerControl.Available(player.m_body, "casual-jumping"))
            {
                return;
            }
            if (!context.ContainsKey(
                ResolveYCollisionBehaviour.TouchYCollisionFlag))
            {
                return;
            }

            AdvCollisionInfo collision =
                context.CollisionInfo.PreResolutionCollisionInfo;
            bool touchedSlope =
                SlopeContactDetector.ContainsTopSlope(collision);
            if (touchedSlope)
            {
                surfaceControl.ObserveSlopeContact(
                    SlopeContactDetector.GetBlockedDirection(collision));
            }
            else
            {
                ResetAssistedJump();
            }
            airControl.Reset();
        }

        private void UpdateSurfaceState(
            BodyComp body,
            BehaviourContext context)
        {
            SlopeContact slope = SlopeContactDetector.Detect(
                body,
                context.CollisionInfo.StartOfFrameCollisionInfo);
            surfaceControl.BeginFrame(
                heldDirection,
                slope.Touching,
                slope.BlockedDirection,
                body.IsOnGround);

            if (surfaceControl.SlopeControlLocked)
            {
                surfaceControl.ReleaseSlopeIfDetached(slope.Adjacent);
            }
            if (surfaceControl.WallSliding)
            {
                surfaceControl.ReleaseWallIfDetached(
                    IsWallAdjacent(body, surfaceControl.WallDirection));
            }
        }

        private bool UpdateCasualPlusJump(
            BodyComp body,
            InputComponent.State state,
            InputComponent.State pressed,
            bool jumpSupported)
        {
            if (assistedJumpActive || !jumpSupported)
            {
                input.TryConsumeJump();
            }

            bool bufferedLandingJump = !assistedJumpActive
                && landingJumpBuffer.TryConsumeOnGround(
                    jumpSupported,
                    state.jump);
            bool nativeBufferedJump = !assistedJumpActive
                && jumpSupported
                && state.jump
                && input.TryConsumeJump();
            bool coyoteJump = !assistedJumpActive
                && !jumpSupported
                && coyoteJumpWindow.TryConsume(pressed.jump);
            if (!assistedJumpActive
                && (jumpSupported || coyoteJump)
                && (pressed.jump
                    || bufferedLandingJump
                    || nativeBufferedJump))
            {
                CancelFallenState();
                StartCasualPlusJump();
                landingJumpBuffer.Reset();
                coyoteJumpWindow.Reset();
                assistedJumpActive = true;
                assistedFlightActive = true;
                earlyReleaseActive = false;
                heldFrame = 1;
                return true;
            }

            if (assistedJumpActive)
            {
                UpdateAssistedJump(body, state.jump);
            }
            return false;
        }

        private static bool IsJumpSupported(
            BodyComp body,
            BehaviourContext context)
        {
            AdvCollisionInfo collision =
                context.CollisionInfo.StartOfFrameCollisionInfo;
            return JumpSupport.IsSupported(
                body.IsOnGround,
                body.IsOnBlock(typeof(SandBlock)),
                collision != null && collision.Sand);
        }

        private void StartCasualPlusJump()
        {
            try
            {
                float takeoffSpeed = CasualPhysics.TakeoffUpwardSpeed(
                    1,
                    Math.Abs(PlayerValues.JUMP),
                    PlayerValues.GRAVITY);
                DoJumpMethod.Invoke(
                    jumpState,
                    new object[]
                    {
                        takeoffSpeed / Math.Abs(PlayerValues.JUMP)
                    });
            }
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException(
                    "Jump King's native jump routine failed",
                    error.InnerException ?? error);
            }
        }

        private void CancelFallenState()
        {
            if (failState.IsRunning())
            {
                failState.ResetResult();
            }
        }

        private void UpdateAssistedJump(BodyComp body, bool jumpHeld)
        {
            float gravityMultiplier = body.GetMultipliers();
            float normalGravity = PlayerValues.GRAVITY * gravityMultiplier;

            if (earlyReleaseActive || !jumpHeld)
            {
                earlyReleaseActive = true;
                body.Velocity.Y = CasualPhysics.ApplyEarlyReleaseGravity(
                    body.Velocity.Y,
                    normalGravity);
                if (body.Velocity.Y >= 0f)
                {
                    assistedJumpActive = false;
                    earlyReleaseActive = false;
                }
                return;
            }

            if (body.Velocity.Y >= 0f)
            {
                assistedJumpActive = false;
                return;
            }

            heldFrame++;
            if (heldFrame <= CasualPhysics.TakeoffFrameCount)
            {
                body.Velocity.Y = -CasualPhysics.TakeoffUpwardSpeed(
                    heldFrame,
                    Math.Abs(PlayerValues.JUMP),
                    PlayerValues.GRAVITY);
                return;
            }

            float heldGravity = CasualPhysics.HeldAscentGravity(
                Math.Abs(PlayerValues.JUMP),
                PlayerValues.GRAVITY) * gravityMultiplier;
            body.Velocity.Y = CasualPhysics.ApplyHeldAscentGravity(
                body.Velocity.Y,
                normalGravity,
                heldGravity);
        }

        private void ResetAssistedJump()
        {
            assistedJumpActive = false;
            assistedFlightActive = false;
            earlyReleaseActive = false;
            heldFrame = 0;
        }

        private void SuspendForMorph()
        {
            heldDirection = 0;
            horizontalVelocityBeforeCollision = 0f;
            ResetAssistedJump();
            landingJumpBuffer.Reset();
            coyoteJumpWindow.Reset();
            surfaceControl.Reset();
            airControl.Reset();
        }

        private void CancelAssistedJumpForJetpack()
        {
            assistedJumpActive = false;
            assistedFlightActive = true;
            earlyReleaseActive = false;
            heldFrame = 0;
            landingJumpBuffer.Reset();
            coyoteJumpWindow.Reset();
        }

        private void ApplyAirControl(
            BodyComp body,
            int direction,
            float speedLimit)
        {
            body.Velocity.X = airControl.Apply(
                body.Velocity.X,
                direction,
                speedLimit,
                speedLimit / AirAccelerationDivisor);
            if (direction != 0)
            {
                player.SetDirection(direction);
            }
        }

        private static bool IsWallAdjacent(BodyComp body, int wallDirection)
        {
            if (wallDirection == 0)
            {
                return false;
            }

            Microsoft.Xna.Framework.Rectangle hitbox = body.GetHitbox();
            Microsoft.Xna.Framework.Rectangle sideProbe =
                new Microsoft.Xna.Framework.Rectangle(
                    wallDirection > 0 ? hitbox.Right : hitbox.Left - 1,
                    hitbox.Top + 1,
                    1,
                    Math.Max(1, hitbox.Height - 2));
            Microsoft.Xna.Framework.Rectangle overlap;
            AdvCollisionInfo collision;
            return LevelManager.CheckCollision(
                sideProbe,
                out overlap,
                out collision);
        }
    }
}
