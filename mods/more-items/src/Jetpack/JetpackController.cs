using System;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace JumpKingJetpack
{
    internal sealed class JetpackController : IBodyCompBehaviour
    {
        private readonly InputComponent input;
        private readonly Action markAssistedUse;
        private readonly JetpackActivationState activation =
            new JetpackActivationState();
        private readonly JKRuntime.Gameplay.AreaEntryObserver areaEntryDetector =
            new JKRuntime.Gameplay.AreaEntryObserver();

        private JetpackItem item;
        private bool airborneInputArmed;
        private bool jumpWasHeld;
        private float verticalVelocityBeforeCollision;
        private int morphJumpSequence;
        internal JKRuntime.State.IStateParticipant CreateStateParticipant()
        {
            return new JKRuntime.State.FieldStateParticipant("more-items.jetpack", 1, delegate {
                morphJumpSequence = JKRuntime.Gameplay.GameFeatures.FormJumpSequence;
                ThrustState.SetActive(activation.Active);
                if (item != null) item.SetThrustActive(activation.Active);
            }, new JKRuntime.State.StateFields(this, "airborneInputArmed", "jumpWasHeld", "verticalVelocityBeforeCollision", "morphJumpSequence"),
                new JKRuntime.State.StateFields(activation));
        }

        internal JetpackController(PlayerEntity player, Action markUse)
        {
            if (player == null)
            {
                throw new ArgumentNullException("player");
            }
            input = player.GetComponent<InputComponent>();
            markAssistedUse = markUse ?? delegate { };
            if (input == null)
            {
                throw new InvalidOperationException(
                    "Jump King player input is unavailable");
            }
            morphJumpSequence = JKRuntime.Gameplay.GameFeatures.FormJumpSequence;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            BodyComp body = context.BodyComp;
            InputComponent.State state = input.GetState();
            bool supported = IsSupported(body, context);
            bool airborne = !supported
                && (!body.IsOnGround || body.Velocity.Y < 0f);
            int nextMorphJumpSequence =
                JKRuntime.Gameplay.GameFeatures.FormJumpSequence;
            bool morphJumped =
                nextMorphJumpSequence != morphJumpSequence;
            morphJumpSequence = nextMorphJumpSequence;

            areaEntryDetector.Update();
            if (JKRuntime.Gameplay.GameFeatures.IsAttached || morphJumped
                // Free-flight thrust deliberately composes with Ball; attached
                // Ball, Hammer and Dash use their own movement response.
                || !JKRuntime.Gameplay.PlayerControl.Available(body, "morph-ball"))
            {
                activation.Stop(false);
                airborneInputArmed = false;
                jumpWasHeld = state.jump;
                if (item != null)
                {
                    item.SetThrustActive(false);
                }
                ThrustState.SetActive(false);
                return true;
            }
            if (supported)
            {
                airborneInputArmed = false;
            }
            else if (!state.jump)
            {
                airborneInputArmed = true;
            }

            bool pressed = airborneInputArmed
                && state.jump
                && !jumpWasHeld;
            bool thrusting = activation.Update(
                true,
                airborne,
                supported,
                state.jump,
                pressed,
                false);
            jumpWasHeld = state.jump;

            if (activation.SuppressLandingJump
                || activation.BlockedUntilLanding)
            {
                input.TryConsumeJump();
            }
            if (thrusting)
            {
                markAssistedUse();
                float gravity = PlayerValues.GRAVITY * body.GetMultipliers();
                body.Velocity.Y = JetpackPhysics.ApplyThrust(
                    body.Velocity.Y,
                    gravity,
                    activation.HeldFrames,
                    Math.Abs(PlayerValues.JUMP));
            }
            if (item != null)
            {
                item.SetThrustActive(activation.Active);
            }
            ThrustState.SetActive(activation.Active);
            return true;
        }

        internal void AttachItem(JetpackItem jetpackItem)
        {
            if (jetpackItem == null)
            {
                throw new ArgumentNullException("jetpackItem");
            }
            item = jetpackItem;
            item.SetThrustActive(false);
        }

        internal void ObserveVelocityBeforeMovement(BehaviourContext context)
        {
            verticalVelocityBeforeCollision = context.BodyComp.Velocity.Y;
        }

        internal void ObserveYCollision(BehaviourContext context)
        {
            if (!context.ContainsKey(
                ResolveYCollisionBehaviour.TouchYCollisionFlag)
                || verticalVelocityBeforeCollision >= -0.0001f)
            {
                return;
            }
            activation.Stop((JKRuntime.Gameplay.GameFeatures.Movement == JKRuntime.Gameplay.MovementMode.Vanilla));
            ThrustState.SetActive(false);
            if (item != null)
            {
                item.SetThrustActive(false);
            }
        }

        private static bool IsSupported(
            BodyComp body,
            BehaviourContext context)
        {
            AdvCollisionInfo collision =
                context.CollisionInfo.StartOfFrameCollisionInfo;
            return body.IsOnGround
                || body.IsOnBlock(typeof(SandBlock))
                || (collision != null && collision.Sand);
        }
    }
}
