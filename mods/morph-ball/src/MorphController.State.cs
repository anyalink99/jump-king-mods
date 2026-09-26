using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    internal sealed class MorphSurfaceRuntimeState
    {
        internal MorphSurface Surface;
        internal Vector2 ActiveNormal;
        internal Vector2 ActiveTangent;
        internal IBlock ActiveBlock;
        internal MorphContourSegment ActiveSegment;
        internal bool HasActiveSegment;
        internal float RollingSpeed;
        internal float RollAngle;
        internal float AngularVelocity;
        internal bool ManualGroundContact;
        internal bool ReleasedFromGroundSurface;
        internal Vector2 ControlledSurfacePosition;
        internal IBlock NativeShadowBlock;
        internal Vector2 NativeShadowNormal;
        internal Vector2 NativeShadowOffset;

        internal void Detach()
        {
            Surface = MorphSurface.None;
            ActiveNormal = Vector2.Zero;
            ActiveTangent = Vector2.Zero;
            ActiveBlock = null;
            HasActiveSegment = false;
        }

        internal void ClearNativeShadow()
        {
            NativeShadowBlock = null;
            NativeShadowNormal = Vector2.Zero;
            NativeShadowOffset = Vector2.Zero;
        }

        internal void ResetForUnmorph()
        {
            Detach();
            RollingSpeed = 0f;
            AngularVelocity = 0f;
            ManualGroundContact = false;
            ReleasedFromGroundSurface = false;
        }
    }

    internal sealed class MorphFrameRuntimeState
    {
        internal float HorizontalVelocityBeforeCollision;
        internal float VerticalVelocityBeforeCollision;
        internal Vector2 PositionBeforeXMovement;
        internal Vector2 PositionBeforeYMovement;
        internal Vector2 VelocityBeforeYMovement;
        internal bool TeleportedDuringNativeMovement;
        internal bool MorphWasHeld;
        internal bool JumpWasHeld;
        internal bool UsingBallHitbox;
        internal bool HasTeleportCarry;
        internal Vector2 TeleportCarryVelocity;
        internal bool NativeContactProbeActive;
        internal Vector2 PositionBeforeNativeCap;
        internal bool NativeCapSampleActive;
        internal bool SuppressContinuousStickyLandingSound;

        internal void ResetForUnmorph()
        {
            HasTeleportCarry = false;
            TeleportCarryVelocity = Vector2.Zero;
            NativeContactProbeActive = false;
            NativeCapSampleActive = false;
            SuppressContinuousStickyLandingSound = false;
            TeleportedDuringNativeMovement = false;
        }
    }

    internal sealed class MorphJumpRuntimeState
    {
        internal Vector2 JumpNormal;
        internal float FallApexY;
        internal bool TrackingFall;
        internal bool AssistedJumpActive;
        internal bool EarlyReleaseActive;
        internal bool DoubleJumpAvailable;
        internal int JumpFrame;
        internal Vector2 DepartureNormal;
        internal Vector2 DepartureTangent;
        internal float DepartureRollingSpeed;
        internal bool HasDepartureSurface;
        internal bool PendingFloorBounce;
        internal Vector2 PendingFloorBounceNormal;
        internal float PendingFloorBounceSpeed;
        internal float AssistedJumpSpeed;
        internal MorphJumpProfile AssistedJumpProfile;

        internal void ResetForUnmorph()
        {
            DoubleJumpAvailable = false;
            DepartureNormal = Vector2.Zero;
            DepartureTangent = Vector2.Zero;
            DepartureRollingSpeed = 0f;
            HasDepartureSurface = false;
            TrackingFall = false;
            FallApexY = 0f;
            PendingFloorBounce = false;
        }
    }
}
