using System;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace JumpKingJetpack
{
    internal sealed class JetpackUsageMarker : Entity
    {
        private readonly BodyComp body;
        private readonly bool allowed;
        private bool pending;
        private bool marked;

        internal JetpackUsageMarker(BodyComp bodyComp, bool levelAllowsJetpack)
        {
            if (bodyComp == null) throw new ArgumentNullException("bodyComp");
            body = bodyComp;
            allowed = levelAllowsJetpack;
        }

        internal void MarkUsed()
        {
            if (!allowed && !marked) pending = true;
        }

        protected override void Update(float delta)
        {
            if (!pending || marked) return;
            IBodyCompBehaviour first = null;
            foreach (IBodyCompBehaviour behaviour in body.GetBehaviourList())
            {
                first = behaviour;
                break;
            }
            if (first == null) return;
            JetpackUsageBehaviour marker = new JetpackUsageBehaviour();
            if (!JKRuntime.Gameplay.RunModifiers.RegisterBefore(body, marker, first)) return;
            JKRuntime.Gameplay.RunModifiers.Remove(body, marker);
            pending = false;
            marked = true;
        }
    }

    internal sealed class JetpackUsageBehaviour : IBodyCompBehaviour
    {
        public bool ExecuteBehaviour(BehaviourContext context)
        {
            return true;
        }
    }
}
