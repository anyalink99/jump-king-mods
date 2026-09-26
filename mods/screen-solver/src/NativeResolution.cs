using System;
using System.Collections.Generic;
using System.Linq;
using ErikMaths;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    // Audited native resolution. Never enter patched ExecuteBehaviour methods:
    // some otherwise inactive mods retain the supplied body in static fields.
    internal static class NativeResolution
    {
        internal static bool Run(IBodyCompBehaviour stage, BehaviourContext context, NativeWorld world, float walkSpeed)
        {
            var body = context.BodyComp;
            var blocks = NativePlayer.Field<LinkedList<IBlockBehaviour>>(body, "m_blockBehaviours");
            if (stage is ApplyGravityBehaviour)
            {
                float gravity = PlayerValues.GRAVITY;
                foreach (var block in blocks) gravity = block.ModifyGravity(gravity, context);
                body.Velocity.Y += gravity;
                body.Velocity.Y = Math.Min(body.Velocity.Y, PlayerValues.MAX_FALL);
            }
            else if (stage is ResolveXCollisionBehaviour) ResolveX(body, blocks, context, world, walkSpeed);
            else if (stage is ResolveYCollisionBehaviour) ResolveY(body, blocks, context, world);
            else if (stage is GuardtowerSoulBugFixBehaviour)
            {
                if (context.ContainsKey("TouchedXSlope") && !context.ContainsKey("TouchYCollision") &&
                    context.ContainsKey("YStep") && (int)context["YStep"] > 0 && !body.IsKnocked)
                { body.Velocity.X *= -PlayerValues.BOUNCE; NativePlayer.Set(body, "_knocked", true); context["PlayBumpSFX"] = new object(); }
            }
            else return stage.ExecuteBehaviour(context);
            return true;
        }
        private static bool Collision(BodyComp body, LinkedList<IBlockBehaviour> blocks, BehaviourContext context, NativeWorld world, bool x, out AdvCollisionInfo info)
        {
            Rectangle overlap;
            return ((ICollisionQuery)world).CheckCollision(body.GetHitbox(), out overlap, out info) || Extra(blocks, context, info, x);
        }
        private static bool Extra(LinkedList<IBlockBehaviour> blocks, BehaviourContext context, AdvCollisionInfo info, bool x)
        { foreach (var block in blocks) if (x ? block.AdditionalXCollisionCheck(info, context) : block.AdditionalYCollisionCheck(info, context)) return true; return false; }
        private static void ResolveX(BodyComp body, LinkedList<IBlockBehaviour> blocks, BehaviourContext context, NativeWorld world, float walkSpeed)
        {
            AdvCollisionInfo info;
            if (!Collision(body, blocks, context, world, true, out info)) return;
            body.Position.X = body.Position.ToPoint().ToVector2().X;
            int step = body.Velocity.X > 0 ? 1 : -1, iterations = 0;
            AdvCollisionInfo next = info;
            do
            {
                info = next; body.Position.X -= step;
                if (++iterations > 4096) throw new NotSupportedException("X collision resolution exceeded the safe bound");
            } while (Collision(body, blocks, context, world, true, out next));
            if (info.SlopeType == SlopeType.None)
            {
                body.Velocity.X *= -PlayerValues.BOUNCE; NativePlayer.Set(body, "_knocked", true);
                if (!body.IsOnGround || body.Velocity.Y <= 0) context["PlayBumpSFX"] = new object();
            }
            else
            {
                context["TouchedXSlope"] = new object();
                if (body.IsOnGround && Math.Abs(body.Velocity.X) <= walkSpeed && body.Velocity.Y > 0) body.Velocity.X = 0;
                else if (Vector2.Dot(body.LastVelocity, info.SlopeNormal) < 0) body.Velocity = VectorMath.MakeComposite(body.Velocity, info.SlopeNormal).i;
            }
        }
        private static void ResolveY(BodyComp body, LinkedList<IBlockBehaviour> blocks, BehaviourContext context, NativeWorld world)
        {
            var ice = NativePlayer.Block(body, typeof(IceBlock)); var snow = NativePlayer.Block(body, typeof(SnowBlock));
            int step = body.Velocity.Y > 0 ? 1 : -1; context["YStep"] = step;
            if (snow != null) snow.IsPlayerOnBlock = false;
            AdvCollisionInfo info;
            if (Collision(body, blocks, context, world, false, out info))
            {
                context["TouchYCollision"] = new object();
                if (ice != null) ice.IsPlayerOnBlock = info.Ice;
                body.Position.Y = body.Position.ToPoint().ToVector2().Y;
                AdvCollisionInfo next = info; int iterations = 0;
                do
                {
                    info = next; body.Position.Y -= step;
                    if (++iterations > 4096) throw new NotSupportedException("Y collision resolution exceeded the safe bound");
                } while (Collision(body, blocks, context, world, false, out next));
                if (step > 0)
                {
                    bool slope = info.SlopeType != SlopeType.None;
                    NativePlayer.Set(body, "_is_on_ground", true);
                    if (slope)
                    {
                        NativePlayer.Set(body, "_is_on_ground", false);
                        if (Vector2.Dot(body.LastVelocity, info.SlopeNormal) < 0)
                        { body.Velocity = VectorMath.MakeComposite(body.Velocity, info.SlopeNormal).i; NativePlayer.Set(body, "_knocked", true); }
                        else slope = false;
                    }
                    if (!slope)
                    { NativePlayer.Set(body, "_knocked", false); if (info.Snow && snow != null) snow.IsPlayerOnBlock = true; }
                }
                else
                {
                    context["PlayBumpSFX"] = new object();
                    if (info.SlopeType == SlopeType.None) body.Velocity.X *= PlayerValues.BOUNCE;
                    else if (Vector2.Dot(body.LastVelocity, info.SlopeNormal) < 0)
                    { body.Velocity.Y = VectorMath.MakeComposite(body.Velocity, info.SlopeNormal).i.Y; body.Velocity.X *= PlayerValues.BOUNCE; }
                }
                if (info.SlopeType == SlopeType.None || body.Velocity.Y <= 0 || body.IsOnGround) body.Velocity.Y = 0;
            }
            else
            {
                if (info.Quark && body.Velocity.Y == PlayerValues.MAX_FALL)
                {
                    if (context.ContainsKey("CappedXPosition")) body.Velocity.X = 0;
                    body.Position.Y = (int)Math.Ceiling(body.Position.Y / 8f) * 8;
                }
                NativePlayer.Set(body, "_is_on_ground", false); if (ice != null) ice.IsPlayerOnBlock = false;
            }
        }
    }
}
