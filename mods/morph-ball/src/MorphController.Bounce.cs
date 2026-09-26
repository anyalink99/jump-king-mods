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
    internal sealed partial class MorphController
    {
        private void TrackFall(BodyComp body)
        {
            if (!jumpRuntime.TrackingFall)
            {
                jumpRuntime.TrackingFall = true;
                jumpRuntime.FallApexY = body.Position.Y;
                floorBounce.BeginFall(!GlobalStickyEnabled);
                return;
            }
            jumpRuntime.FallApexY = Math.Min(jumpRuntime.FallApexY, body.Position.Y);
        }

        private bool TryBounceFromFloor(BehaviourContext context)
        {
            BodyComp body = context.BodyComp;
            Vector2 landingNormal;
            if (!frameRuntime.UsingBallHitbox
                || GlobalStickyEnabled
                || !jumpRuntime.TrackingFall
                || context.ContainsKey("TeleportedPlayer")
                || !context.ContainsKey(
                    ResolveYCollisionBehaviour.TouchYCollisionFlag)
                || !context.ContainsKey(
                    ResolveYCollisionBehaviour.YStepFlag)
                || (int)context[ResolveYCollisionBehaviour.YStepFlag] <= 0
                || frameRuntime.VerticalVelocityBeforeCollision <= 0f
                || !TryGetLandingNormal(body, out landingNormal))
            {
                return false;
            }
            if (!MorphBouncePhysics.IsFloorBounceSurface(landingNormal))
            {
                return false;
            }

            bool splatImpact = MorphBouncePhysics.IsSplatImpact(
                frameRuntime.VerticalVelocityBeforeCollision,
                PlayerValues.MAX_FALL);
            if (splatImpact)
            {
                SuppressVanillaSplat(body);
            }

            bool bufferedJump = landingJumpBuffer.TryConsume(
                true,
                input.GetState().jump);
            if (bufferedJump)
            {
                input.TryConsumeJump();
                Vector2 tangent = SurfaceMotion.GetTangent(landingNormal);
                Vector2 preservedVelocity = tangent
                    * Vector2.Dot(body.Velocity, tangent);
                airControl.Reset();
                return BeginAssistedJump(
                    body,
                    landingNormal,
                    preservedVelocity,
                    false,
                    true);
            }

            float gravity = PlayerValues.GRAVITY * body.GetMultipliers();
            float fallHeight = body.Position.Y - jumpRuntime.FallApexY;
            int bounceCount = MorphBouncePhysics.FloorBounceCount(
                fallHeight,
                gravity,
                PlayerValues.MAX_FALL);
            bool stickyImpact = GlobalStickyEnabled
                || MorphSurfaceFollower.ResolveStickySurface(body).Attached;
            bool continuation = floorBounce.IsContinuation;
            if (stickyImpact && bounceCount < 2 && !continuation)
            {
                jumpRuntime.TrackingFall = false;
                floorBounce.Cancel();
                return false;
            }
            if (!floorBounce.PrepareImpact(bounceCount))
            {
                jumpRuntime.TrackingFall = false;
                floorBounce.Cancel();
                return false;
            }

            float bounceSpeed = continuation
                ? MorphBouncePhysics.ContinuationFloorSpeed(
                    fallHeight,
                    gravity)
                : MorphBouncePhysics.FloorSpeed(
                    fallHeight,
                    gravity);
            if (bounceSpeed <= 0f)
            {
                jumpRuntime.TrackingFall = false;
                floorBounce.Cancel();
                return false;
            }

            jumpRuntime.PendingFloorBounce = true;
            jumpRuntime.PendingFloorBounceNormal = landingNormal;
            jumpRuntime.PendingFloorBounceSpeed = bounceSpeed;
            // A bounce is still a complete landing.  Preserve one grounded
            // player-state tick so Jump King's IsOnGround node plays the
            // correct material sound/particles and third-party blocks see
            // their normal landing contract.  The impulse is applied at the
            // start of the following BodyComp frame.
            SetControlledGroundContact(body);
            return true;
        }

        private static void SuppressVanillaSplat(BodyComp body)
        {
            Vector2 lastVelocity = body.LastVelocity;
            lastVelocity.Y = PlayerValues.MAX_FALL - 0.001f;
            LastVelocityField.SetValue(body, lastVelocity);
        }

        private static bool TryGetLandingNormal(
            BodyComp body,
            out Vector2 normal)
        {
            Rectangle hitbox = body.GetHitbox();
            Rectangle probe = hitbox;
            probe.Offset(0, 1);
            AdvCollisionInfo collision = LevelManager.GetCollisionInfo(probe);
            if (collision != null
                && collision.SlopeType != SlopeType.None
                && collision.SlopeNormal.Y < -0.05f
                && SurfaceMotion.TryNormalize(
                    collision.SlopeNormal,
                    out normal))
            {
                return true;
            }
            if (body.IsOnGround)
            {
                normal = new Vector2(0f, -1f);
                return true;
            }
            normal = Vector2.Zero;
            return false;
        }

        private bool IsSnowTopSurface(
            BodyComp body,
            Vector2 normal)
        {
            if (normal.Y > -0.75f)
            {
                return false;
            }
            if (surfaceRuntime.ActiveBlock is SnowBlock)
            {
                return true;
            }
            Rectangle hitbox = body.GetHitbox();
            Rectangle probe = new Rectangle(
                hitbox.Left + 2,
                hitbox.Bottom,
                Math.Max(1, hitbox.Width - 4),
                3);
            AdvCollisionInfo collision = LevelManager.GetCollisionInfo(probe);
            return collision != null && collision.Snow;
        }

        private static bool IsSupportedBySand(
            BodyComp body,
            BehaviourContext context)
        {
            AdvCollisionInfo collision =
                context.CollisionInfo.StartOfFrameCollisionInfo;
            return body.IsOnBlock(typeof(SandBlock))
                || (collision != null && collision.Sand);
        }

        private static float MoveTowards(
            float current,
            float target,
            float maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }
            return current + Math.Sign(target - current) * maximumDelta;
        }

        private static bool IsWallAdjacent(
            BodyComp body,
            int wallDirection)
        {
            if (wallDirection == 0)
            {
                return false;
            }
            Rectangle hitbox = body.GetHitbox();
            Rectangle probe = new Rectangle(
                wallDirection > 0 ? hitbox.Right : hitbox.Left - 1,
                hitbox.Top + 1,
                1,
                Math.Max(1, hitbox.Height - 2));
            return MorphCollisionWorld.HasBlockingCollision(probe);
        }
    }
}
