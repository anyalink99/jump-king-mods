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
        private void Collapse(BodyComp body)
        {
            MorphSurfaceState initial = AcquireSurface(body);
            surfaceRuntime.Surface = initial.Surface;
            int offset = GetCollapseOffset(surfaceRuntime.Surface);
            body.Position.Y += offset;
            HeightField.SetValue(body, BallHeight);
            frameRuntime.UsingBallHitbox = true;
            MorphSurfaceState collapsed = AcquireSurface(body);
            if (collapsed.Attached)
            {
                initial = collapsed;
                surfaceRuntime.Surface = collapsed.Surface;
            }
            ApplyInitialSurface(body, initial);
        }

        private void RestoreCollapsed(BodyComp body)
        {
            HeightField.SetValue(body, BallHeight);
            frameRuntime.UsingBallHitbox = true;
            MorphSurfaceState initial = AcquireSurface(body);
            surfaceRuntime.Surface = initial.Surface;
            ApplyInitialSurface(body, initial);
        }

        private void ApplyInitialSurface(
            BodyComp body,
            MorphSurfaceState initial)
        {
            if (surfaceRuntime.Surface != MorphSurface.None)
            {
                surfaceRuntime.ActiveNormal = initial.Normal;
                surfaceRuntime.ActiveTangent = SurfaceMotion.GetTangent(
                    surfaceRuntime.ActiveNormal);
                surfaceRuntime.ActiveBlock = initial.Block;
                surfaceRuntime.ActiveSegment = initial.Segment;
                surfaceRuntime.HasActiveSegment = initial.HasSegment;
                surfaceRuntime.AngularVelocity = surfaceRuntime.RollingSpeed / 11f;
            }
            else
            {
                surfaceRuntime.AngularVelocity = body.Velocity.X / 11f;
            }
        }

        private MorphSurfaceState AcquireSurface(BodyComp body)
        {
            if (GlobalStickyEnabled)
            {
                return MorphSurfaceFollower.Acquire(body, true);
            }
            MorphSurfaceState forced =
                MorphSurfaceFollower.ResolveStickySurface(body);
            return forced.Attached
                ? forced
                : MorphSurfaceFollower.Acquire(body, false);
        }

        private bool Expand(
            BodyComp body,
            MorphSurface currentSurface,
            bool force)
        {
            Rectangle expanded = GetExpandedHitbox(body, currentSurface);
            if (!force && IsBlocked(expanded))
            {
                return false;
            }
            body.Position.Y = expanded.Y;
            HeightField.SetValue(body, NormalHeight);
            frameRuntime.UsingBallHitbox = false;
            surfaceRuntime.Surface = MorphSurface.None;
            return true;
        }

        private bool CanExpand(BodyComp body, MorphSurface currentSurface)
        {
            return !frameRuntime.UsingBallHitbox
                || !IsBlocked(GetExpandedHitbox(body, currentSurface));
        }

        private static Rectangle GetExpandedHitbox(
            BodyComp body,
            MorphSurface currentSurface)
        {
            Rectangle hitbox = body.GetHitbox();
            int y = hitbox.Y;
            if (currentSurface == MorphSurface.Ground)
            {
                y -= NormalHeight - BallHeight;
            }
            else if (currentSurface != MorphSurface.Ceiling)
            {
                y -= (NormalHeight - BallHeight) / 2;
            }
            return new Rectangle(hitbox.X, y, hitbox.Width, NormalHeight);
        }

        private static int GetCollapseOffset(MorphSurface currentSurface)
        {
            if (currentSurface == MorphSurface.Ground)
            {
                return NormalHeight - BallHeight;
            }
            if (currentSurface == MorphSurface.Ceiling)
            {
                return 0;
            }
            return (NormalHeight - BallHeight) / 2;
        }

        private static bool IsBlocked(Rectangle hitbox)
        {
            return MorphCollisionWorld.HasBlockingCollision(hitbox);
        }

        private static bool IsSlope(Vector2 normal)
        {
            return Math.Abs(normal.X) > 0.05f
                && Math.Abs(normal.Y) > 0.05f;
        }

        private bool GlobalStickyEnabled
        {
            get
            {
                return SettingsStore.Current.StickyMode
                    && !BallKingMapRules.DisablesSticky(
                        Camera.CurrentScreen);
            }
        }

        private bool StickyPhysicsActive
        {
            get
            {
                return GlobalStickyEnabled
                    || BallKingMapRules.IsStickySurface(surfaceRuntime.ActiveBlock);
            }
        }

        private bool TryAttachSweptSurface(
            BodyComp body,
            Vector2 start,
            Vector2 displacement,
            Vector2 impactVelocity)
        {
            if (!frameRuntime.UsingBallHitbox || surfaceRuntime.Surface != MorphSurface.None)
            {
                return false;
            }
            bool stickySurfacesOnly = !GlobalStickyEnabled;
            MorphSurfaceState contact;
            Vector2 contactPosition;
            if (!MorphSurfaceFollower.TryResolveSweep(
                body,
                start,
                displacement,
                stickySurfacesOnly,
                out contact,
                out contactPosition))
            {
                return false;
            }

            body.Position = contactPosition;
            Vector2 nextTangent = SurfaceMotion.GetTangent(contact.Normal);
            surfaceRuntime.RollingSpeed = MorphSurfaceMomentum.ProjectLandingSpeed(
                impactVelocity,
                nextTangent,
                SurfaceSpeed * body.GetMultipliers());
            if (!body.IsOnGround && !landingJumpBuffer.Buffered)
            {
                MorphLandingEffects.Play();
            }

            surfaceRuntime.Surface = contact.Surface;
            surfaceRuntime.ActiveNormal = contact.Normal;
            surfaceRuntime.ActiveTangent = nextTangent;
            surfaceRuntime.ActiveBlock = contact.Block;
            surfaceRuntime.ActiveSegment = contact.Segment;
            surfaceRuntime.HasActiveSegment = contact.HasSegment;
            body.Velocity = Vector2.Zero;
            KnockedField.SetValue(body, false);
            jumpRuntime.DoubleJumpAvailable = true;
            jumpRuntime.TrackingFall = false;
            ResetAssistedJump();
            airControl.Reset();
            return true;
        }

        private void ResetAssistedJump()
        {
            jumpRuntime.AssistedJumpActive = false;
            jumpRuntime.EarlyReleaseActive = false;
            jumpRuntime.AssistedJumpSpeed = 0f;
            jumpRuntime.JumpFrame = 0;
        }

        private void ResetBallState()
        {
            surfaceRuntime.ResetForUnmorph();
            wallBounce.Reset();
            landingJumpBuffer.Reset();
            coyoteJumpWindow.Reset();
            airControl.Reset();
            jumpRuntime.ResetForUnmorph();
            frameRuntime.ResetForUnmorph();
            floorBounce.Cancel();
            ResetAssistedJump();
        }

    }
}
