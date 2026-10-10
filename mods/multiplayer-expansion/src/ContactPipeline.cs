using System;
using System.Collections.Generic;
using JKRuntime.Gameplay;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    // support belongs before gravity and materials, not after the whole body tick
    internal sealed class ContactPipeline : IDisposable
    {
        private sealed class Phase : IBodyCompBehaviour
        {
            private readonly Action action;
            internal Phase(Action value) { action=value; }
            public bool ExecuteBehaviour(BehaviourContext context) { action(); return true; }
        }
        private readonly BodyPipeline pipeline;
        private Vector2 before;
        internal ContactPipeline(BodyComp body, PlayerContacts solver, Func<IList<InteractionPeer>> peers,
            Func<InteractionRules> rules, Func<ulong> self, Func<Rectangle,bool> blocked)
        {
            pipeline=new BodyPipeline(body,false,-100,"anyalink.multiplayer-expansion.world");
            try {
                pipeline.Register(BodyPhase.BeforeCollisionCache,new Phase(delegate {
                    before=solver.BeginStep(body,peers(),rules(),blocked);
                }));
                pipeline.Register(BodyPhase.AfterYCollision,new Phase(delegate {
                    solver.Apply(body,before,peers(),rules(),self(),blocked,true);
                }));
            } catch { pipeline.Dispose(); throw; }
        }
        public void Dispose() { pipeline.Dispose(); }
    }
}
