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
        private void UpdateBall(
            BodyComp body,
            int direction,
            bool jumpHeld,
            bool jumpPressed,
            bool supportedBySand,
            AdvCollisionInfo nativeCollision)
        {
            surfaceRuntime.ManualGroundContact = false;
            surfaceRuntime.ReleasedFromGroundSurface = false;
            bool applyingTeleportCarry = frameRuntime.HasTeleportCarry;
            Vector2 carriedTeleportVelocity = frameRuntime.TeleportCarryVelocity;
            if (frameRuntime.HasTeleportCarry)
            {
                body.Velocity = carriedTeleportVelocity;
                frameRuntime.HasTeleportCarry = false;
                frameRuntime.TeleportCarryVelocity = Vector2.Zero;
            }
            bool doubleJumpEnabled = BallKingMapRules.DoubleJumpEnabled(
                Camera.CurrentScreen,
                SettingsStore.Current.EnableDoubleJump);
            bool globalStickyMode = GlobalStickyEnabled;
            bool startingStickyMode = globalStickyMode
                || BallKingMapRules.IsStickySurface(surfaceRuntime.ActiveBlock);
            bool stickyMode = startingStickyMode;
            bool startingOnRollingSlope = !stickyMode
                && IsSlope(surfaceRuntime.ActiveNormal);
            MorphSlopeContact slopeContact = stickyMode
                ? default(MorphSlopeContact)
                : MorphSlopeContactDetector.Detect(
                    body,
                    nativeCollision);
            MorphSurfaceState flatContact = !stickyMode
                    && body.IsOnGround
                    && !slopeContact.Supported
                    && !supportedBySand
                ? MorphSurfaceFollower.Acquire(body, false)
                : default(MorphSurfaceState);
            bool nonStickySupported = !stickyMode
                && (flatContact.Attached
                    || supportedBySand
                    || slopeContact.Supported
                    || (surfaceRuntime.Surface != MorphSurface.None
                        && startingOnRollingSlope));
            bool bufferEnabled = !doubleJumpEnabled
                || !jumpRuntime.DoubleJumpAvailable;
            landingJumpBuffer.Update(
                bufferEnabled,
                stickyMode
                    ? surfaceRuntime.Surface == MorphSurface.None
                    : !nonStickySupported,
                jumpHeld,
                jumpPressed);

            MorphSurface startingSurface = surfaceRuntime.Surface;
            Vector2 previousNormal = surfaceRuntime.ActiveNormal;
            Vector2 previousTangent = surfaceRuntime.ActiveTangent;
            MorphSurfaceState resolved = default(MorphSurfaceState);
            bool followsSurface = stickyMode
                && !supportedBySand
                && startingSurface != MorphSurface.None
                && surfaceRuntime.ActiveBlock != null;
            bool followsRollingSlope = !stickyMode
                && startingSurface != MorphSurface.None
                && startingOnRollingSlope
                && surfaceRuntime.ActiveBlock != null;
            if (followsSurface
                && BallKingMapRules.IsStickySurface(surfaceRuntime.ActiveBlock))
            {
                resolved = new MorphSurfaceState
                {
                    Surface = startingSurface,
                    Normal = surfaceRuntime.ActiveNormal,
                    Block = surfaceRuntime.ActiveBlock,
                    Segment = surfaceRuntime.ActiveSegment,
                    HasSegment = surfaceRuntime.HasActiveSegment
                };
            }
            else if (!stickyMode)
            {
                if (followsRollingSlope)
                {
                    resolved = new MorphSurfaceState
                    {
                        Surface = MorphSurface.Ground,
                        Normal = surfaceRuntime.ActiveNormal,
                        Block = surfaceRuntime.ActiveBlock,
                        Segment = surfaceRuntime.ActiveSegment,
                        HasSegment = surfaceRuntime.HasActiveSegment
                    };
                }
                else if (slopeContact.Supported)
                {
                    resolved = MorphSurfaceFollower.ResolveRollingSurface(
                        body,
                        slopeContact.Normal,
                        slopeContact.Block);
                }
                else if (flatContact.Attached)
                {
                    resolved = flatContact;
                }
                else if (supportedBySand)
                {
                    resolved = new MorphSurfaceState
                    {
                        Surface = MorphSurface.Ground,
                        Normal = new Vector2(0f, -1f)
                    };
                }
            }
            else if (supportedBySand)
            {
                resolved = new MorphSurfaceState
                {
                    Surface = MorphSurface.Ground,
                    Normal = new Vector2(0f, -1f)
                };
            }
            else if (followsSurface)
            {
                resolved = new MorphSurfaceState
                {
                    Surface = startingSurface,
                    Normal = surfaceRuntime.ActiveNormal,
                    Block = surfaceRuntime.ActiveBlock,
                    Segment = surfaceRuntime.ActiveSegment,
                    HasSegment = surfaceRuntime.HasActiveSegment
                };
            }
            else
            {
                resolved = default(MorphSurfaceState);
            }
            surfaceRuntime.Surface = resolved.Surface;
            if (startingSurface != MorphSurface.None
                && surfaceRuntime.Surface == MorphSurface.None)
            {
                CaptureDepartureSurface(
                    previousNormal,
                    previousTangent,
                    surfaceRuntime.RollingSpeed);
            }
            else if (surfaceRuntime.Surface != MorphSurface.None)
            {
                jumpRuntime.HasDepartureSurface = false;
            }
            stickyMode = globalStickyMode
                || BallKingMapRules.IsStickySurface(resolved.Block);
            coyoteJumpWindow.Update(
                surfaceRuntime.Surface != MorphSurface.None,
                jumpRuntime.AssistedJumpActive);

            if (startingStickyMode
                && surfaceRuntime.Surface == MorphSurface.None
                && startingSurface != MorphSurface.None
                && IsSlope(previousNormal))
            {
                body.Velocity = previousTangent * surfaceRuntime.RollingSpeed;
            }
            else if (!stickyMode
                && surfaceRuntime.Surface == MorphSurface.None
                && startingSurface != MorphSurface.None)
            {
                body.Velocity = previousTangent
                    * surfaceRuntime.RollingSpeed
                    * body.GetMultipliers();
                floorBounce.BeginFall(true);
            }

            if (surfaceRuntime.Surface != MorphSurface.None)
            {
                surfaceRuntime.ActiveNormal = resolved.Normal;
                surfaceRuntime.ActiveTangent = SurfaceMotion.GetTangent(
                    surfaceRuntime.ActiveNormal);
                surfaceRuntime.ActiveBlock = resolved.Block;
                surfaceRuntime.ActiveSegment = resolved.Segment;
                surfaceRuntime.HasActiveSegment = resolved.HasSegment;
                if (startingSurface == MorphSurface.None)
                {
                    Vector2 landingVelocity =
                        MorphSurfaceMomentum.ResolveLandingVelocity(
                            applyingTeleportCarry,
                            carriedTeleportVelocity,
                            stickyMode,
                            frameRuntime.HorizontalVelocityBeforeCollision,
                            body.LastVelocity);
                    bool landedOnRollingSlope = !stickyMode
                        && IsSlope(resolved.Normal);
                    float landingSpeedLimit = landedOnRollingSlope
                            ? MorphSurfaceMomentum.SlopeTerminalSpeed(
                                resolved.Normal,
                                PlayerValues.MAX_FALL)
                            : SurfaceSpeed * body.GetMultipliers();
                    surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.ProjectLandingSpeed(
                        landingVelocity,
                        surfaceRuntime.ActiveTangent,
                        landingSpeedLimit);
                    if (stickyMode && !body.IsOnGround)
                    {
                        MorphLandingEffects.Play();
                    }
                }
                jumpRuntime.DoubleJumpAvailable = true;
                jumpRuntime.TrackingFall = false;
                floorBounce.Cancel();
                ResetAssistedJump();
                airControl.Reset();
            }
            else
            {
                surfaceRuntime.ActiveBlock = null;
                surfaceRuntime.HasActiveSegment = false;
            }

            bool bufferedJump = landingJumpBuffer.TryConsume(
                surfaceRuntime.Surface != MorphSurface.None,
                jumpHeld);
            bool jumpSurfaceIsSlope = !stickyMode
                && IsSlope(surfaceRuntime.ActiveNormal);
            if (surfaceRuntime.Surface != MorphSurface.None
                && MorphSurfaceJumpPolicy.AllowsJump(
                    surfaceRuntime.ActiveNormal,
                    stickyMode,
                    jumpSurfaceIsSlope)
                && (jumpPressed || bufferedJump))
            {
                if (StartSurfaceJump(body, bufferedJump))
                {
                    input.TryConsumeJump();
                    return;
                }
            }
            if (surfaceRuntime.Surface == MorphSurface.None
                && coyoteJumpWindow.TryConsume(jumpPressed))
            {
                if (StartCoyoteJump(body))
                {
                    input.TryConsumeJump();
                    return;
                }
            }
            if (surfaceRuntime.Surface == MorphSurface.None
                && jumpPressed
                && doubleJumpEnabled
                && jumpRuntime.DoubleJumpAvailable)
            {
                if (StartDoubleJump(body))
                {
                    input.TryConsumeJump();
                    return;
                }
            }
            if (surfaceRuntime.Surface == MorphSurface.None && jumpPressed)
            {
                input.TryConsumeJump();
            }

            if (jumpRuntime.AssistedJumpActive)
            {
                input.TryConsumeJump();
                UpdateAssistedJump(body, jumpHeld);
            }

            if (surfaceRuntime.Surface == MorphSurface.None)
            {
                TrackFall(body);
                if (wallBounce.Sliding)
                {
                    body.Velocity.X = 0f;
                    airControl.Reset();
                }
                else
                {
                    int airDirection = MorphSlopeInputPolicy.FilterDirection(
                        direction,
                        !stickyMode && slopeContact.Supported,
                        slopeContact.Normal);
                    ApplyAirControl(body, airDirection);
                }
                UpdateAirRotation(
                    direction,
                    body.GetMultipliers());
                return;
            }

            if (jumpHeld)
            {
                input.TryConsumeJump();
            }
            if (!stickyMode)
            {
                int controlledDirection = direction;
                if (wallBounce.Sliding)
                {
                    controlledDirection = 0;
                }
                UpdateNonStickySurface(
                    body,
                    controlledDirection,
                    body.GetMultipliers(),
                    IsSnowTopSurface(body, surfaceRuntime.ActiveNormal));
                return;
            }
            if (supportedBySand)
            {
                UpdateNonStickySurface(
                    body,
                    direction,
                    body.GetMultipliers(),
                    false);
                return;
            }
            float stickyMaximumSpeed = SurfaceSpeed
                * (IsSnowTopSurface(body, surfaceRuntime.ActiveNormal)
                    ? SnowSurfaceScale
                    : 1f);
            surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.LimitSpeed(
                surfaceRuntime.RollingSpeed,
                stickyMaximumSpeed);
            surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.Update(
                surfaceRuntime.RollingSpeed,
                direction,
                stickyMaximumSpeed,
                1f);
            float movementScale = body.GetMultipliers();
            float travelSpeed = surfaceRuntime.RollingSpeed * movementScale;
            MorphSurfaceState moved =
                MorphSurfaceFollower.MoveSticky(
                    body,
                    new MorphSurfaceState
                    {
                        Surface = surfaceRuntime.Surface,
                        Normal = surfaceRuntime.ActiveNormal,
                        Block = surfaceRuntime.ActiveBlock,
                        Segment = surfaceRuntime.ActiveSegment,
                        HasSegment = surfaceRuntime.HasActiveSegment
                    },
                    travelSpeed,
                    !globalStickyMode);
            if (moved.Attached
                && !SurfaceMotion.IsHorizontalGround(surfaceRuntime.ActiveNormal)
                && SurfaceMotion.IsHorizontalGround(moved.Normal))
            {
                frameRuntime.SuppressContinuousStickyLandingSound = true;
            }
            surfaceRuntime.Surface = moved.Surface;
            if (moved.Attached)
            {
                surfaceRuntime.ActiveNormal = moved.Normal;
                surfaceRuntime.ActiveTangent = SurfaceMotion.GetTangent(
                    surfaceRuntime.ActiveNormal);
                surfaceRuntime.ActiveBlock = moved.Block;
                surfaceRuntime.ActiveSegment = moved.Segment;
                surfaceRuntime.HasActiveSegment = moved.HasSegment;
            }
            else
            {
                CaptureDepartureSurface(
                    surfaceRuntime.ActiveNormal,
                    surfaceRuntime.ActiveTangent,
                    surfaceRuntime.RollingSpeed);
                surfaceRuntime.ActiveBlock = null;
                surfaceRuntime.HasActiveSegment = false;
            }
            surfaceRuntime.RollAngle += travelSpeed / 11f;
            surfaceRuntime.AngularVelocity = travelSpeed / 11f;
        }

        private void UpdateNonStickySurface(
            BodyComp body,
            int direction,
            float movementScale,
            bool snowTopSurface)
        {
            bool followsSlope = IsSlope(surfaceRuntime.ActiveNormal);
            float maximumSpeed;
            if (followsSlope)
            {
                maximumSpeed =
                    MorphSurfaceMomentum.SlopeTerminalSpeed(
                        surfaceRuntime.ActiveNormal,
                        PlayerValues.MAX_FALL);
                surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.ApplySlopeMotion(
                    surfaceRuntime.RollingSpeed,
                    direction,
                    surfaceRuntime.ActiveNormal,
                    PlayerValues.GRAVITY,
                    maximumSpeed);
            }
            else
            {
                maximumSpeed = SurfaceSpeed
                    * (snowTopSurface ? SnowSurfaceScale : 1f);
                surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.LimitSpeed(
                    surfaceRuntime.RollingSpeed,
                    maximumSpeed);
                surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.Update(
                    surfaceRuntime.RollingSpeed,
                    direction,
                    maximumSpeed,
                    1f);
            }

            float travelSpeed = surfaceRuntime.RollingSpeed * movementScale;
            MorphSurfaceState moved =
                MorphSurfaceFollower.MoveRollingSurface(
                body,
                new MorphSurfaceState
                {
                    Surface = surfaceRuntime.Surface,
                    Normal = surfaceRuntime.ActiveNormal,
                    Block = surfaceRuntime.ActiveBlock,
                    Segment = surfaceRuntime.ActiveSegment,
                    HasSegment = surfaceRuntime.HasActiveSegment
                },
                travelSpeed,
                direction);
            if (moved.Attached)
            {
                if (moved.MovementBlocked)
                {
                    surfaceRuntime.RollingSpeed = 0f;
                }
                surfaceRuntime.Surface = MorphSurface.Ground;
                surfaceRuntime.ActiveNormal = moved.Normal;
                surfaceRuntime.ActiveTangent = SurfaceMotion.GetTangent(
                    surfaceRuntime.ActiveNormal);
                surfaceRuntime.ActiveBlock = moved.Block;
                surfaceRuntime.ActiveSegment = moved.Segment;
                surfaceRuntime.HasActiveSegment = moved.HasSegment;
                body.Velocity = Vector2.Zero;
                surfaceRuntime.ManualGroundContact = true;
            }
            else
            {
                CaptureDepartureSurface(
                    surfaceRuntime.ActiveNormal,
                    surfaceRuntime.ActiveTangent,
                    surfaceRuntime.RollingSpeed);
                surfaceRuntime.Surface = MorphSurface.None;
                surfaceRuntime.ActiveNormal = Vector2.Zero;
                surfaceRuntime.ActiveTangent = Vector2.Zero;
                surfaceRuntime.ActiveBlock = null;
                surfaceRuntime.HasActiveSegment = false;
                surfaceRuntime.ReleasedFromGroundSurface = true;
                floorBounce.BeginFall(true);
            }
            surfaceRuntime.RollAngle += travelSpeed / 11f;
            surfaceRuntime.AngularVelocity = travelSpeed / 11f;
        }

        private static void SetControlledGroundContact(BodyComp body)
        {
            IsOnGroundField.SetValue(body, true);
            KnockedField.SetValue(body, false);
        }

        private void CompleteContinuousStickyLanding(BodyComp body)
        {
            if (!frameRuntime.SuppressContinuousStickyLandingSound)
            {
                return;
            }
            if (!frameRuntime.UsingBallHitbox
                || !StickyPhysicsActive
                || surfaceRuntime.Surface == MorphSurface.None
                || !SurfaceMotion.IsHorizontalGround(surfaceRuntime.ActiveNormal))
            {
                frameRuntime.SuppressContinuousStickyLandingSound = false;
                return;
            }
            if (!body.IsOnGround)
            {
                return;
            }
            frameRuntime.SuppressContinuousStickyLandingSound = false;
            MorphLandingEffects.CompleteWithoutSound(
                body,
                isOnGroundState);
        }

    }
}
