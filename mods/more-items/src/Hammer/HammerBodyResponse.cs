using System;
using JKRuntime.Gameplay;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace HammerKing
{
    internal static class HammerBodyResponse
    {
        internal static void Register(BodyPipeline pipeline)
        {
            pipeline.Register(BodyPhase.AfterXCollision, new WallContact());
            pipeline.Register(BodyPhase.AfterGravity, new GroundFriction());
        }

        internal sealed class WallContact : IBodyCompBehaviour
        {
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                BodyComp body = context.BodyComp;
                if (!context.ContainsKey(ResolveXCollisionBehaviour.TouchedXSlopeFlag)
                    && body.LastVelocity.X * body.Velocity.X < 0f)
                {
                    // Native collision has already separated the body. Remove
                    // only its elastic wall rebound, preserving slope response.
                    body.Velocity.X = 0f;
                }
                return true;
            }
        }

        internal sealed class GroundFriction : IBodyCompBehaviour
        {
            public bool ExecuteBehaviour(BehaviourContext context)
            {
                BodyComp body = context.BodyComp;
                // Native ice already applies its own friction later in the
                // pipeline; do not charge that material twice.
                if (!body.IsOnGround || body.IsOnBlock<IceBlock>()) return true;
                float speed = Math.Abs(body.Velocity.X);
                float loss = 0.65f + speed * 0.08f;
                body.Velocity.X = Math.Sign(body.Velocity.X) * Math.Max(0f, speed - loss);
                return true;
            }
        }
    }
}
