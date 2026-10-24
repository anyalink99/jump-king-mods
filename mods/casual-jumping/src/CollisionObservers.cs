using JumpKing.API;
using JumpKing.BodyCompBehaviours;

namespace CasualJumping
{
    internal sealed class PreMovementVelocityObserver : IBodyCompBehaviour
    {
        private readonly CasualController controller;

        internal PreMovementVelocityObserver(CasualController controller)
        {
            this.controller = controller;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveVelocityBeforeMovement(context);
            return true;
        }
    }

    internal sealed class XCollisionObserver : IBodyCompBehaviour
    {
        private readonly CasualController controller;

        internal XCollisionObserver(CasualController controller)
        {
            this.controller = controller;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveXCollision(context);
            return true;
        }
    }

    internal sealed class YCollisionObserver : IBodyCompBehaviour
    {
        private readonly CasualController controller;

        internal YCollisionObserver(CasualController controller)
        {
            this.controller = controller;
        }

        public bool ExecuteBehaviour(BehaviourContext context)
        {
            controller.ObserveYCollision(context);
            return true;
        }
    }

}
