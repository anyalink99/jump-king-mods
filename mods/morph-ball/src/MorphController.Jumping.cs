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
        private bool StartSurfaceJump(
            BodyComp body,
            bool playLandingEffect)
        {
            Vector2 normal;
            if (!SurfaceMotion.TryNormalize(surfaceRuntime.ActiveNormal, out normal))
            {
                return false;
            }
            if (!PrepareNativeSurfaceDeparture(body, normal))
            {
                return false;
            }
            Vector2 tangent = SurfaceMotion.GetTangent(normal);
            float tangentSpeed = StickyPhysicsActive
                || IsSlope(normal)
                ? surfaceRuntime.RollingSpeed * body.GetMultipliers()
                : Vector2.Dot(body.Velocity, tangent);
            CaptureDepartureSurface(normal, tangent, surfaceRuntime.RollingSpeed);
            return BeginAssistedJump(
                body,
                normal,
                tangent * tangentSpeed,
                false,
                playLandingEffect);
        }

        private bool PrepareNativeSurfaceDeparture(
            BodyComp body,
            Vector2 normal)
        {
            Vector2 preferredOffset = ReferenceEquals(
                    surfaceRuntime.NativeShadowBlock,
                    surfaceRuntime.ActiveBlock)
                && Vector2.Dot(surfaceRuntime.NativeShadowNormal, normal) > 0.999f
                    ? surfaceRuntime.NativeShadowOffset
                    : Vector2.Zero;
            Vector2 departurePosition;
            if (!MorphSurfaceFollower.TryGetNativeContactPose(
                body,
                body.Position,
                normal,
                preferredOffset,
                out departurePosition))
            {
                return false;
            }
            body.Position = departurePosition;
            return true;
        }

        private bool StartCoyoteJump(BodyComp body)
        {
            if (!jumpRuntime.HasDepartureSurface)
            {
                return false;
            }
            return BeginAssistedJump(
                body,
                jumpRuntime.DepartureNormal,
                jumpRuntime.DepartureTangent
                    * jumpRuntime.DepartureRollingSpeed
                    * body.GetMultipliers(),
                false,
                false);
        }

        private bool StartDoubleJump(BodyComp body)
        {
            Vector2 horizontalVelocity = new Vector2(body.Velocity.X, 0f);
            return BeginAssistedJump(
                body,
                new Vector2(0f, -1f),
                horizontalVelocity,
                true,
                false);
        }

        private bool BeginAssistedJump(
            BodyComp body,
            Vector2 normal,
            Vector2 preservedVelocity,
            bool consumeDoubleJump,
            bool playLandingEffect)
        {
            if (!SurfaceMotion.TryNormalize(normal, out jumpRuntime.JumpNormal)
                || !SurfaceMotion.IsFinite(preservedVelocity))
            {
                return false;
            }
            jumpRuntime.AssistedJumpSpeed = Math.Abs(PlayerValues.JUMP);
            bool doubleJumpMode = BallKingMapRules.DoubleJumpEnabled(
                Camera.CurrentScreen,
                SettingsStore.Current.EnableDoubleJump);
            jumpRuntime.AssistedJumpProfile = MorphJumpPhysics.CreateProfile(
                BallKingMapRules.JumpHeightScale(
                    Camera.CurrentScreen,
                    doubleJumpMode));
            float speed = MorphJumpPhysics.TakeoffSpeed(
                1,
                jumpRuntime.AssistedJumpSpeed,
                PlayerValues.GRAVITY,
                jumpRuntime.AssistedJumpProfile);
            body.Velocity = preservedVelocity + jumpRuntime.JumpNormal * speed;
            IsOnGroundField.SetValue(body, false);
            KnockedField.SetValue(body, false);
            surfaceRuntime.Surface = MorphSurface.None;
            jumpRuntime.TrackingFall = true;
            jumpRuntime.FallApexY = body.Position.Y;
            floorBounce.BeginFall(!GlobalStickyEnabled);
            jumpRuntime.AssistedJumpActive = true;
            jumpRuntime.EarlyReleaseActive = false;
            jumpRuntime.JumpFrame = 1;
            landingJumpBuffer.Reset();
            coyoteJumpWindow.Reset();
            if (consumeDoubleJump)
            {
                jumpRuntime.DoubleJumpAvailable = false;
            }
            if (playLandingEffect)
            {
                MorphLandingEffects.Play();
            }
            BallFormState.NotifyJump();
            MorphJumpEffects.Play(body, jumpState);
            return true;
        }

        private void CaptureDepartureSurface(
            Vector2 normal,
            Vector2 tangent,
            float speed)
        {
            Vector2 unitNormal;
            if (!SurfaceMotion.TryNormalize(normal, out unitNormal)
                || !SurfaceMotion.IsFinite(tangent)
                || !SurfaceMotion.IsFinite(speed))
            {
                jumpRuntime.HasDepartureSurface = false;
                return;
            }
            jumpRuntime.DepartureNormal = unitNormal;
            jumpRuntime.DepartureTangent = SurfaceMotion.GetTangent(unitNormal);
            jumpRuntime.DepartureRollingSpeed = speed;
            jumpRuntime.HasDepartureSurface = true;
        }

        private void UpdateAssistedJump(BodyComp body, bool jumpHeld)
        {
            float component = Vector2.Dot(body.Velocity, jumpRuntime.JumpNormal);
            if (component <= 0f)
            {
                ResetAssistedJump();
                return;
            }

            float gravity = PlayerValues.GRAVITY * body.GetMultipliers();
            if (jumpRuntime.EarlyReleaseActive || !jumpHeld)
            {
                jumpRuntime.EarlyReleaseActive = true;
                DecelerateAssistedJump(body, component, gravity);
                return;
            }

            jumpRuntime.JumpFrame++;
            if (jumpRuntime.JumpFrame <= jumpRuntime.AssistedJumpProfile.TakeoffFrames)
            {
                float next = MorphJumpPhysics.TakeoffSpeed(
                    jumpRuntime.JumpFrame,
                    jumpRuntime.AssistedJumpSpeed,
                    PlayerValues.GRAVITY,
                    jumpRuntime.AssistedJumpProfile);
                body.Velocity += jumpRuntime.JumpNormal * (next - component);
                return;
            }
            if (jumpRuntime.JumpFrame <= jumpRuntime.AssistedJumpProfile.TakeoffFrames
                + jumpRuntime.AssistedJumpProfile.SustainedFrames)
            {
                body.Velocity += jumpRuntime.JumpNormal
                    * MorphJumpPhysics.HeldAcceleration(
                        jumpRuntime.AssistedJumpSpeed,
                        PlayerValues.GRAVITY,
                        jumpRuntime.AssistedJumpProfile)
                    * body.GetMultipliers();
                return;
            }
            jumpRuntime.EarlyReleaseActive = true;
            DecelerateAssistedJump(body, component, gravity);
        }

        private void DecelerateAssistedJump(
            BodyComp body,
            float component,
            float gravity)
        {
            float next = Math.Max(
                0f,
                component
                    - MorphJumpPhysics.EarlyReleaseDeceleration(gravity));
            body.Velocity += jumpRuntime.JumpNormal * (next - component);
            if (next <= 0f)
            {
                ResetAssistedJump();
            }
        }

        private void ApplyAirControl(BodyComp body, int direction)
        {
            float speedLimit = PlayerValues.SPEED * AirSpeedScale;
            body.Velocity.X = airControl.Apply(
                body.Velocity.X,
                direction,
                speedLimit,
                speedLimit / AirAccelerationDivisor,
                speedLimit / 30f);
            if (direction != 0)
            {
                player.SetDirection(direction);
            }
        }

        private void UpdateAirRotation(
            int direction,
            float movementScale)
        {
            if (direction != 0)
            {
                surfaceRuntime.AngularVelocity = MoveTowards(
                    surfaceRuntime.AngularVelocity,
                    direction * MaximumAngularSpeed * movementScale,
                    AirAngularAcceleration * movementScale);
            }
            surfaceRuntime.RollAngle += surfaceRuntime.AngularVelocity;
            if (Math.Abs(surfaceRuntime.RollAngle) > MathHelper.TwoPi)
            {
                surfaceRuntime.RollAngle %= MathHelper.TwoPi;
            }
        }

    }
}
