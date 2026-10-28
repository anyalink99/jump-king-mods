using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal sealed class ScreenImage
    {
        internal readonly int Screen;
        internal readonly Vector2 Offset;
        internal ScreenImage(int screen, Vector2 offset)
        { Screen = screen; Offset = offset; }
        internal Rectangle Bounds { get { return new Rectangle((int)Offset.X, (int)Offset.Y - Screen * 360, 480, 360); } }
    }

    // native screen numbers aren't world coordinates. keep each visible image explicit
    internal sealed class CameraScene
    {
        internal LevelScreen[] Screens;
        internal CameraSettings Settings;
        internal MapRules Rules;
        internal bool Practice;
        private readonly List<ScreenImage> images = new List<ScreenImage>();
        private ScreenImage departure;
        internal bool Allowed(int screen)
        { return Screens != null && screen >= 0 && screen < Screens.Length && Screens[screen] != null && (Practice || Rules.FullScreenActive(Settings, screen)); }
        internal void Reset() { departure = null; images.Clear(); }
        internal void Cross(int source, Vector2 nativeShift)
        {
            departure = new ScreenImage(source, nativeShift);
        }
        internal int Destination(int screen, bool left, float y)
        {
            if (!Allowed(screen)) return -1;
            int target = JKRuntime.Geometry.MapTopology.PreviewDestination(Screens, screen, left, y);
            return Allowed(target) ? target : -1;
        }
        internal void Neighborhood(int current, float translation, float playerY, out int anchor, out float influence)
        {
            anchor = current; influence = 0;
            int bottom = Math.Max(0, (int)Math.Floor(translation / 360));
            for (int row = bottom; row <= bottom + 1 && Screens != null && row < Screens.Length; row++)
            {
                float top = Math.Max(-row * 360, -translation);
                float end = Math.Min(360 - row * 360, 360 - translation);
                if (end <= top) continue;
                // sample the visible portal interval, not the subject on another row
                bool portal = false;
                for (float y = top + 4; y < end && !portal; y += 8)
                    portal = Destination(row, true, y) >= 0 || Destination(row, false, y) >= 0;
                float weight = (end - top) / 360;
                if (portal && weight > influence) { anchor = row; influence = weight; }
            }
        }
        internal int ExposedDestination(int anchor, bool left, float translation, float playerY)
        {
            int direct = Destination(anchor, left, playerY);
            if (direct >= 0) return direct;
            float top = Math.Max(-anchor * 360, -translation), end = Math.Min(360 - anchor * 360, 360 - translation);
            for (float y = top + 4; y < end; y += 8)
            { int target = Destination(anchor, left, y); if (target >= 0) return target; }
            return -1;
        }
        internal ScreenImage[] Plan(int current, Vector2 view, int anchor, int left, int right)
        {
            images.Clear();
            if (Screens == null || current < 0 || current >= Screens.Length || Screens[current] == null) return images.ToArray();
            int first, last;
            JKRuntime.Geometry.MapTopology.VerticalRange(Screens, current, out first, out last);
            AddColumn(current, Vector2.Zero, view, first, last);
            if (view.X > .0001f && left >= 0) AddLinked(left, departure != null && departure.Screen == left && departure.Offset.X < 0
                ? departure.Offset : LinkOffset(anchor, left, true), view);
            if (view.X < -.0001f && right >= 0) AddLinked(right, departure != null && departure.Screen == right && departure.Offset.X > 0
                ? departure.Offset : LinkOffset(anchor, right, false), view);
            if (departure != null)
            {
                Rectangle viewport = new Rectangle((int)Math.Floor(-view.X), (int)Math.Floor(-view.Y), 481, 361);
                if (departure.Bounds.Intersects(viewport)) AddLinked(departure.Screen, departure.Offset, view);
                else departure = null;
            }
            return images.ToArray();
        }
        private Vector2 LinkOffset(int source, int target, bool left)
        {
            // ask topology for the same shift used by a real body, including MultiWarp's inset
            for (float y = 4 - source * 360; y < 360 - source * 360; y += 8)
            {
                if (!JKRuntime.Geometry.MapTopology.Connects(Screens, source, left, y, target)) continue;
                Vector2 shift;
                if (JKRuntime.Geometry.MapTopology.TryCrossing(Screens, new Vector2(left ? 1 : 479, y - 13),
                    new Vector2(left ? 479 : 1, y - 13 + (source - target) * 360), out shift)) return -shift;
            }
            return new Vector2(left ? -480 : 480, (target - source) * 360);
        }
        private void AddLinked(int screen, Vector2 offset, Vector2 view)
        {
            if (!Allowed(screen)) return;
            int first, last; JKRuntime.Geometry.MapTopology.VerticalRange(Screens, screen, out first, out last);
            AddColumn(screen, offset, view, first, last);
        }
        private void AddColumn(int root, Vector2 offset, Vector2 view, int first, int last)
        {
            int bottom = Math.Max(first, (int)Math.Floor((view.Y + offset.Y) / 360));
            int top = Math.Min(last, (int)Math.Ceiling((view.Y + offset.Y) / 360));
            for (int row = bottom; row <= top && images.Count < 12; row++)
            {
                if (!Allowed(row)) continue;
                bool exists = false;
                foreach (var image in images) if (image.Screen == row && image.Offset == offset) { exists = true; break; }
                if (!exists) images.Add(new ScreenImage(row, offset));
            }
        }
        internal Vector2 Project(Vector2 point, Vector2 view)
        {
            int screen = JKRuntime.Geometry.MapTopology.ScreenAt(point.Y, Screens.Length);
            Vector2 best = point; float distance = float.MaxValue;
            foreach (var image in images) if (image.Screen == screen)
            {
                var candidate = point + image.Offset;
                float next = Vector2.DistanceSquared(candidate + view, new Vector2(240, 180));
                if (next < distance) { distance = next; best = candidate; }
            }
            return best + view;
        }
    }
}
