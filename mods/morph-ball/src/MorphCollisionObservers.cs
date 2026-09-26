using JumpKing.API;
using JumpKing.BodyCompBehaviours;

namespace MorphBallMod
{
    internal sealed class MorphPreMovementObserver : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphPreMovementObserver(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveVelocityBeforeMovement(context);
            return true;
        }
    }

    internal sealed class MorphXCollisionObserver : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphXCollisionObserver(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveXCollision(context);
            return true;
        }
    }

    internal sealed class MorphPreCapObserver : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphPreCapObserver(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.BeforeNativePositionCap(context.BodyComp);
            return true;
        }
    }

    internal sealed class MorphPostCapObserver : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphPostCapObserver(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.AfterNativePositionCap(context.BodyComp);
            return true;
        }
    }

    internal sealed class MorphYCollisionObserver : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphYCollisionObserver(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.AfterYMovement(context);
            return true;
        }
    }

    internal sealed class MorphTeleportObserver : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphTeleportObserver(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            if (context.ContainsKey("TeleportedPlayer"))
            {
                controller.ReleaseSurfaceAfterTeleport();
            }
            return true;
        }
    }

    internal sealed class MorphBumpSuppressor : IBodyCompBehaviour
    {
        private readonly MorphController controller;

        internal MorphBumpSuppressor(MorphController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.SuppressAttachedBump(context);
            return true;
        }
    }
}
