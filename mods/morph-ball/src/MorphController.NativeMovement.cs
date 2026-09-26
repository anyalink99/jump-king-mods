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
        internal void ObserveVelocityBeforeMovement(BehaviourContext context)
        {
            BodyComp body = context.BodyComp;
            frameRuntime.NativeContactProbeActive = false;
            frameRuntime.NativeCapSampleActive = false;
            frameRuntime.TeleportedDuringNativeMovement = false;
            frameRuntime.PositionBeforeXMovement = body.Position;
            frameRuntime.VerticalVelocityBeforeCollision = body.Velocity.Y;
            if (frameRuntime.UsingBallHitbox
                && MorphBouncePhysics.IsSplatImpact(
                    frameRuntime.VerticalVelocityBeforeCollision,
                    PlayerValues.MAX_FALL))
            {
                SuppressVanillaSplat(context.BodyComp);
            }
            if (frameRuntime.UsingBallHitbox
                && surfaceRuntime.Surface != MorphSurface.None)
            {
                Vector2 normal;
                if (!SurfaceMotion.TryNormalize(surfaceRuntime.ActiveNormal, out normal))
                {
                    ReleaseInvalidSurface();
                    frameRuntime.HorizontalVelocityBeforeCollision = body.Velocity.X;
                    return;
                }
                // Surface-path movement owns the persistent position.  The
                // native pass gets a short inward probe solely so its
                // collision cache, block behaviours and grounded flags still
                // observe the real contact; AfterYMovement restores the
                // authoritative pose after native resolution.
                surfaceRuntime.ControlledSurfacePosition = body.Position;
                Vector2 nativePosition;
                Vector2 preferredNativeOffset = ReferenceEquals(
                        surfaceRuntime.NativeShadowBlock,
                        surfaceRuntime.ActiveBlock)
                    && Vector2.Dot(surfaceRuntime.NativeShadowNormal, normal) > 0.999f
                    ? surfaceRuntime.NativeShadowOffset
                    : Vector2.Zero;
                if (!MorphSurfaceFollower.TryGetNativeContactPose(
                    body,
                    surfaceRuntime.ControlledSurfacePosition,
                    normal,
                    preferredNativeOffset,
                    out nativePosition))
                {
                    ReleaseInvalidSurface();
                    frameRuntime.HorizontalVelocityBeforeCollision = body.Velocity.X;
                    return;
                }
                surfaceRuntime.NativeShadowBlock = surfaceRuntime.ActiveBlock;
                surfaceRuntime.NativeShadowNormal = normal;
                surfaceRuntime.NativeShadowOffset = nativePosition
                    - surfaceRuntime.ControlledSurfacePosition;
                frameRuntime.NativeContactProbeActive = true;
                body.Position = nativePosition;
                Vector2 probeDirection = surfaceRuntime.ControlledSurfacePosition
                    - nativePosition;
                if (!SurfaceMotion.TryNormalize(
                    probeDirection,
                    out probeDirection))
                {
                    probeDirection = -normal;
                }
                body.Velocity = probeDirection * NativeContactProbeSpeed;
                frameRuntime.HorizontalVelocityBeforeCollision = 0f;
                return;
            }
            frameRuntime.HorizontalVelocityBeforeCollision =
                body.Velocity.X;
        }

        internal void BeforeNativePositionCap(BodyComp body)
        {
            if (!frameRuntime.NativeContactProbeActive)
            {
                frameRuntime.NativeCapSampleActive = false;
                return;
            }
            frameRuntime.PositionBeforeNativeCap = body.Position;
            frameRuntime.NativeCapSampleActive = true;
        }

        internal void AfterNativePositionCap(BodyComp body)
        {
            if (!frameRuntime.NativeContactProbeActive || !frameRuntime.NativeCapSampleActive)
            {
                return;
            }
            surfaceRuntime.ControlledSurfacePosition += body.Position
                - frameRuntime.PositionBeforeNativeCap;
            frameRuntime.NativeCapSampleActive = false;
        }

        internal void ObserveXCollision(BehaviourContext context)
        {
            BodyComp body = context.BodyComp;
            if (frameRuntime.NativeContactProbeActive)
            {
                frameRuntime.PositionBeforeYMovement = body.Position;
                frameRuntime.VelocityBeforeYMovement = body.Velocity;
                return;
            }
            bool touchedXSlope = context.ContainsKey(
                ResolveXCollisionBehaviour.TouchedXSlopeFlag);
            if (!frameRuntime.TeleportedDuringNativeMovement
                && TryAttachSweptSurface(
                body,
                frameRuntime.PositionBeforeXMovement,
                new Vector2(frameRuntime.HorizontalVelocityBeforeCollision, 0f),
                new Vector2(
                    frameRuntime.HorizontalVelocityBeforeCollision,
                    frameRuntime.VerticalVelocityBeforeCollision)))
            {
                wallBounce.Reset();
                airControl.Reset();
                frameRuntime.PositionBeforeYMovement = body.Position;
                frameRuntime.VelocityBeforeYMovement = body.Velocity;
                return;
            }
            if (MorphBouncePhysics.ShouldHandleWallCollision(
                frameRuntime.UsingBallHitbox,
                StickyPhysicsActive,
                surfaceRuntime.Surface != MorphSurface.None))
            {
                if (touchedXSlope)
                {
                    Vector2 slopeNormal;
                    if (frameRuntime.UsingBallHitbox
                        && !StickyPhysicsActive
                        && MorphSlopeContactDetector.TryGetTopSlopeNormal(
                            context.CollisionInfo.PreResolutionCollisionInfo,
                            out slopeNormal))
                    {
                        // Native ResolveX projects horizontal velocity onto a
                        // slope tangent. For a rolling ball that would turn
                        // held air control into upward motion after our
                        // contour has already released at the low edge.
                        body.Velocity =
                            MorphSlopeInputPolicy
                                .RemoveUphillControlFromCollision(
                                    new Vector2(
                                        frameRuntime.HorizontalVelocityBeforeCollision,
                                        frameRuntime.VerticalVelocityBeforeCollision),
                                    input.GetState().dpad.X,
                                    slopeNormal);
                    }
                    wallBounce.Reset();
                    airControl.Reset();
                }
                else
                {
                    bool bounced =
                        Math.Abs(frameRuntime.HorizontalVelocityBeforeCollision) > 0.0001f
                        && frameRuntime.HorizontalVelocityBeforeCollision
                            * body.Velocity.X < 0f;
                    if (bounced)
                    {
                        airControl.Reset();
                        int travelDirection =
                            frameRuntime.HorizontalVelocityBeforeCollision > 0f
                                ? 1
                                : -1;
                        bool sliding = wallBounce.ObserveCollision(
                            travelDirection,
                            input.GetState().dpad.X);
                        if (sliding && surfaceRuntime.ReleasedFromGroundSurface)
                        {
                            surfaceRuntime.RollingSpeed = 0f;
                        }
                        body.Velocity.X = sliding
                            ? 0f
                            : MorphBouncePhysics.WallVelocity(
                                frameRuntime.HorizontalVelocityBeforeCollision);
                    }
                }
            }
            frameRuntime.PositionBeforeYMovement = body.Position;
            frameRuntime.VelocityBeforeYMovement = body.Velocity;
        }

        internal void AfterYMovement(BehaviourContext context)
        {
            bool nativeProbe = frameRuntime.NativeContactProbeActive;
            if (frameRuntime.NativeContactProbeActive)
            {
                context.BodyComp.Position = surfaceRuntime.ControlledSurfacePosition;
                context.BodyComp.Velocity = Vector2.Zero;
                frameRuntime.NativeContactProbeActive = false;
            }
            bool attached = !nativeProbe
                && !frameRuntime.TeleportedDuringNativeMovement
                && TryAttachSweptSurface(
                    context.BodyComp,
                    frameRuntime.PositionBeforeYMovement,
                    new Vector2(0f, frameRuntime.VelocityBeforeYMovement.Y),
                    frameRuntime.VelocityBeforeYMovement);
            if (!attached)
            {
                TryBounceFromFloor(context);
            }
            if (surfaceRuntime.ManualGroundContact)
            {
                SetControlledGroundContact(context.BodyComp);
            }
            CompleteContinuousStickyLanding(context.BodyComp);
            if (!frameRuntime.UsingBallHitbox
                || !StickyPhysicsActive
                || surfaceRuntime.Surface == MorphSurface.None
                || surfaceRuntime.Surface == MorphSurface.Ground)
            {
                return;
            }
            Camera.UpdateCamera(context.BodyComp.GetHitbox().Center);
            areaEntryDetector.Update();
        }

        private void ApplyPendingFloorBounce(BodyComp body)
        {
            if (!jumpRuntime.PendingFloorBounce)
            {
                return;
            }
            jumpRuntime.PendingFloorBounce = false;
            body.Velocity = MorphBouncePhysics.SurfaceVelocity(
                body.Velocity,
                jumpRuntime.PendingFloorBounceNormal,
                jumpRuntime.PendingFloorBounceSpeed);
            IsOnGroundField.SetValue(body, false);
            surfaceRuntime.Detach();
            jumpRuntime.TrackingFall = true;
            jumpRuntime.FallApexY = body.Position.Y;
            floorBounce.Consume();
            airControl.Reset();
            ResetAssistedJump();
        }

        private void ReleaseInvalidSurface()
        {
            surfaceRuntime.Detach();
            surfaceRuntime.ClearNativeShadow();
            frameRuntime.SuppressContinuousStickyLandingSound = false;
        }

        internal void ReleaseSurfaceAfterTeleport()
        {
            frameRuntime.TeleportedDuringNativeMovement = true;
            frameRuntime.NativeContactProbeActive = false;
            frameRuntime.NativeCapSampleActive = false;
            if (surfaceRuntime.Surface != MorphSurface.None)
            {
                frameRuntime.TeleportCarryVelocity = surfaceRuntime.ActiveTangent
                    * surfaceRuntime.RollingSpeed
                    * player.m_body.GetMultipliers();
                frameRuntime.HasTeleportCarry = true;
            }
            surfaceRuntime.Detach();
            jumpRuntime.HasDepartureSurface = false;
            jumpRuntime.TrackingFall = false;
            coyoteJumpWindow.Reset();
            airControl.Reset();
            frameRuntime.SuppressContinuousStickyLandingSound = false;
            areaEntryDetector.Update();
        }

        internal void SuppressAttachedBump(BehaviourContext context)
        {
            if (frameRuntime.UsingBallHitbox
                && ((StickyPhysicsActive
                        && surfaceRuntime.Surface != MorphSurface.None)
                    || wallBounce.Sliding))
            {
                context.Remove("PlayBumpSFX");
            }
        }

        internal void ForceUnmorph()
        {
            if (frameRuntime.UsingBallHitbox)
            {
                Expand(player.m_body, surfaceRuntime.Surface, true);
            }
            transition.ForceUnmorphed();
            ResetBallState();
            frameRuntime.UsingBallHitbox = false;
            BallFormState.Reset();
            ReleaseControl();
        }

        internal void Dispose()
        {
            if (ownsMorphSound) morphSound.Dispose(); else morphSound.Stop();
        }

    }
}
