using System;
using JumpKing.Level;

namespace SmoothCamera
{
    internal static class PortalViews
    {
        internal static int Destination(LevelScreen[] screens, int source, bool left, float y = float.NaN)
        {
            if (screens == null || source < 0 || source >= screens.Length || screens[source] == null) return -1;
            var links = screens[source].teleport;
            if (links == null) return ExpansionPortals.Destination(screens, source, left, y);
            // the native body uses a single enabled link for BOTH exits
            TeleportLink first = links.Length > 0 ? links[0] : null, second = links.Length > 1 ? links[1] : null;
            TeleportLink link = first != null && first.IsEnabled && second != null && second.IsEnabled
                ? (left ? first : second) : first != null && first.IsEnabled ? first : second;
            int index = link == null || !link.IsEnabled ? -1 : link.GetIndex0();
            return index >= 0 && index < screens.Length ? index : ExpansionPortals.Destination(screens, source, left, y);
        }
        internal static bool Connects(LevelScreen[] screens, int source, bool left, float y, int destination)
        {
            if (screens == null || source < 0 || source >= screens.Length || screens[source] == null
                || destination < 0 || destination >= screens.Length) return false;
            return Destination(screens, source, left, y) == destination
                || ExpansionPortals.Connects(screens, source, left, y, destination);
        }
    }

    internal sealed class HorizontalMotion
    {
        private bool initialized, frozen, guardLeft, guardRight;
        private int screen, left = -1, right = -1;
        private float previousX, previousY = float.NaN;
        private readonly AxisMotion axis = new AxisMotion { Options = AxisSettings.HorizontalDefault() };
        internal float Translation { get { return axis.Value; } }
        internal AxisSettings Options { get { return axis.Options; } set { axis.Options = value; } }
        internal int Left { get { return left; } }
        internal int Right { get { return right; } }
        internal int Anchor { get; private set; }
        internal void Reset() { initialized = false; axis.Reset(); left = right = -1; }

        // returns the vertical coordinate rebase for a native side teleport
        internal float Observe(float x, int current, LevelScreen[] screens, bool enabled, bool paused, int anchor = -1, float influence = 1, float speed = 0, float y = float.NaN)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) return 0;
            frozen = paused;
            if (anchor < 0) anchor = current;
            if (initialized && enabled && influence <= 0 && Math.Abs(Translation) > .0001f) anchor = Anchor;
            int nextLeft = enabled ? MapPolicy.PortalDestination(screens, anchor, true, y) : -1;
            int nextRight = enabled ? MapPolicy.PortalDestination(screens, anchor, false, y) : -1;
            float rebase = 0;
            if (initialized && enabled)
            {
                bool crossedRight = previousX > 360 && x < 120 && PortalViews.Connects(screens, screen, false, previousY, current);
                bool crossedLeft = previousX < 120 && x > 360 && PortalViews.Connects(screens, screen, true, previousY, current);
                if (crossedRight || crossedLeft)
                {
                    float shift = crossedRight ? 480 : -480;
                    axis.Rebase(shift);
                    rebase = (current - screen) * 360f;
                    // keep the departure view while crossing a one-way link
                    anchor = current;
                    nextLeft = MapPolicy.PortalDestination(screens, current, true, y); nextRight = MapPolicy.PortalDestination(screens, current, false, y);
                    if (crossedRight) nextLeft = screen; else nextRight = screen;
                }
                else if (Translation != 0 &&
                    (Translation > 0 ? nextLeft < 0 || left - Anchor != nextLeft - anchor : nextRight < 0 || right - Anchor != nextRight - anchor))
                {
                    // hide the old side completely before changing its destination
                    anchor = Anchor; nextLeft = left; nextRight = right; influence = 0;
                }
                else if (current == screen && Anchor == anchor)
                {
                    if (Translation > 0 && left >= 0) nextLeft = left;
                    if (Translation < 0 && right >= 0) nextRight = right;
                }
                else if (Math.Abs(previousX - x) > 240) { axis.Reset(); }
            }
            if (!enabled) { axis.Reset(); }
            initialized = true; screen = current; previousX = x; previousY = y; left = nextLeft; right = nextRight; Anchor = anchor;
            if (!paused)
            {
                // A screen without its own teleport only settles residual motion;
                // it never starts following horizontal player movement
                int followLeft = enabled ? MapPolicy.PortalDestination(screens, anchor, true, y) : -1;
                int followRight = enabled ? MapPolicy.PortalDestination(screens, anchor, false, y) : -1;
                bool canFollow = enabled && influence > 0 && (followLeft >= 0 || followRight >= 0);
                guardLeft = canFollow && followLeft >= 0; guardRight = canFollow && followRight >= 0;
                axis.Observe(x, speed, 480, followRight >= 0 ? -240 : 0, followLeft >= 0 ? 240 : 0,
                    0, paused, false, canFollow, CameraMotion.Clamp(influence, 0, 1), false);
            }
            else axis.Observe(x, speed, 480, -480, 480, 0, true, false, true, 1, false);
            return rebase;
        }
        internal void Advance(float delta)
        {
            if (!initialized || frozen) return;
            axis.Advance(delta);
            if ((previousX < 48 && guardLeft) || (previousX > 432 && guardRight))
                axis.KeepVisible(48, guardRight ? -480 : 0, guardLeft ? 480 : 0);
        }
        internal void Drag(float view, float player)
        {
            if (!initialized || frozen) return;
            // a retained departure can shrink, but only open exits may reveal more
            float min = guardRight ? -480 : right >= 0 ? Math.Min(0, view) : 0;
            float max = guardLeft ? 480 : left >= 0 ? Math.Max(0, view) : 0;
            axis.Place(DebugDragState.Frame(view, player, Options, 480, min, max, 0));
        }
    }
}
