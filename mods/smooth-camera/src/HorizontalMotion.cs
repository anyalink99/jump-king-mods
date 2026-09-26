using System;
using JumpKing.Level;

namespace SmoothCamera
{
    internal static class PortalViews
    {
        internal static int Destination(LevelScreen[] screens, int source, bool left)
        {
            if (screens == null || source < 0 || source >= screens.Length || screens[source] == null) return -1;
            var links = screens[source].teleport;
            if (links == null) return -1;
            // The native body uses a single enabled link for BOTH exits.
            TeleportLink first = links.Length > 0 ? links[0] : null, second = links.Length > 1 ? links[1] : null;
            TeleportLink link = first != null && first.IsEnabled && second != null && second.IsEnabled
                ? (left ? first : second) : first != null && first.IsEnabled ? first : second;
            int index = link == null || !link.IsEnabled ? -1 : link.GetIndex0();
            return index >= 0 && index < screens.Length ? index : -1;
        }
    }

    internal sealed class HorizontalMotion
    {
        private bool initialized, frozen;
        private int screen, left = -1, right = -1;
        private float previousX, target, velocity;
        internal float Translation { get; private set; }
        internal int Left { get { return left; } }
        internal int Right { get { return right; } }
        internal int Anchor { get; private set; }
        internal void Reset() { initialized = false; Translation = target = velocity = 0; left = right = -1; }

        // Returns the vertical coordinate rebase for a native side teleport.
        internal float Observe(float x, int current, LevelScreen[] screens, bool enabled, bool paused, int anchor = -1, float influence = 1)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) return 0;
            frozen = paused;
            if (anchor < 0) anchor = current;
            if (initialized && enabled && influence <= 0 && Math.Abs(Translation) > .0001f) anchor = Anchor;
            int nextLeft = enabled ? PortalOpenings.Destination(screens, anchor, true) : -1;
            int nextRight = enabled ? PortalOpenings.Destination(screens, anchor, false) : -1;
            float rebase = 0;
            if (initialized && enabled)
            {
                bool crossedRight = previousX > 360 && x < 120 && PortalViews.Destination(screens, screen, false) == current;
                bool crossedLeft = previousX < 120 && x > 360 && PortalViews.Destination(screens, screen, true) == current;
                if (crossedRight || crossedLeft)
                {
                    float shift = crossedRight ? 480 : -480;
                    Translation += shift; target += shift;
                    rebase = (current - screen) * 360f;
                    // Preserve the departure view while crossing a one-way link.
                    anchor = current;
                    nextLeft = PortalOpenings.Destination(screens, current, true); nextRight = PortalOpenings.Destination(screens, current, false);
                    if (crossedRight) nextLeft = screen; else nextRight = screen;
                }
                else if (anchor != Anchor && Math.Abs(Translation) > .001f &&
                    (Translation > 0 ? nextLeft < 0 || left - Anchor != nextLeft - anchor : nextRight < 0 || right - Anchor != nextRight - anchor))
                {
                    // Different nearby portal graphs need not form a consistent
                    // plane. Hide the old side view before switching its identity.
                    anchor = Anchor; nextLeft = left; nextRight = right; influence = 0;
                }
                else if (current == screen && Anchor == anchor)
                {
                    if (Translation > 0 && left >= 0) nextLeft = left;
                    if (Translation < 0 && right >= 0) nextRight = right;
                }
                else if (Math.Abs(previousX - x) > 240) { Translation = target = velocity = 0; }
            }
            if (!enabled) { Translation = target = velocity = 0; }
            initialized = true; screen = current; previousX = x; left = nextLeft; right = nextRight; Anchor = anchor;
            if (!paused)
            {
                // A screen without its own teleport only settles residual motion;
                // it never starts following horizontal player movement.
                int followLeft = enabled ? PortalOpenings.Destination(screens, anchor, true) : -1;
                int followRight = enabled ? PortalOpenings.Destination(screens, anchor, false) : -1;
                bool canFollow = enabled && influence > 0 && (followLeft >= 0 || followRight >= 0);
                float desired = canFollow ? (240 - x) * CameraMotion.Clamp(influence, 0, 1) : 0;
                if (!canFollow) target = 0;
                else if (desired > target + 24) target = desired - 24;
                else if (desired < target - 24) target = desired + 24;
                target = CameraMotion.Clamp(target, followRight >= 0 ? -240 : 0, followLeft >= 0 ? 240 : 0);
            }
            return rebase;
        }
        internal void Advance(float delta)
        {
            if (!initialized || frozen || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float dt = Math.Min(delta, .1f), d = Translation - target;
            float impulse = (velocity + 12 * d) * dt, decay = (float)Math.Exp(-12 * dt);
            float next = target + (d + impulse) * decay;
            velocity = (velocity - 12 * impulse) * decay;
            if ((d < 0 && next > target) || (d > 0 && next < target)) { next = target; velocity = 0; }
            Translation = next;
            if (Math.Abs(next - target) < .0001f && Math.Abs(velocity) < .001f) { Translation = target; velocity = 0; }
        }
    }
}
