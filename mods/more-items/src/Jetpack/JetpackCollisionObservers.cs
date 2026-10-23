using JumpKing.API;
using JumpKing.BodyCompBehaviours;

namespace JumpKingJetpack
{
    internal sealed class JetpackPreMovementObserver : IBodyCompBehaviour
    {
        private readonly JetpackController controller;

        internal JetpackPreMovementObserver(JetpackController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveVelocityBeforeMovement(context);
            return true;
        }
    }

    internal sealed class JetpackYCollisionObserver : IBodyCompBehaviour
    {
        private readonly JetpackController controller;

        internal JetpackYCollisionObserver(JetpackController owner)
        {
            controller = owner;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveYCollision(context);
            return true;
        }
    }
}
