using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.Level;
using JumpKing.MiscSystems.LocationText;
using Microsoft.Xna.Framework;

namespace JKRuntime.Compatibility
{
    // Detached names and indices only; never retains a player or loaded geometry.
    internal sealed class ManagerMap
    {
        internal sealed class Entry
        {
            internal readonly string Name;
            internal readonly int First, Last;
            internal Entry(string name, int first, int last) { Name = name; First = first; Last = last; }
            public override string ToString() { return Name; }
        }
        internal readonly Entry[] Areas, Screens;
        internal ManagerMap(Location[] locations, int count)
        {
            var areas = new List<Entry>();
            foreach (var location in locations ?? new Location[0])
            {
                int first = Math.Max(1, location.start), last = Math.Min(count, location.end);
                if (last < first) continue;
                string name = location.name;
                if (string.IsNullOrWhiteSpace(name)) name = "Unnamed area";
                else name = LanguageJK.language.ResourceManager.GetString(name) ?? name;
                areas.Add(new Entry(name, first - 1, last - 1));
            }
            Areas = areas.ToArray();
            Screens = Enumerable.Range(0, Math.Max(0, count)).Select(index => {
                string name = string.Join(" / ", Areas.Where(a => index >= a.First && index <= a.Last).Select(a => a.Name).Distinct());
                return new Entry("Screen " + (index + 1) + (name.Length == 0 ? "" : " - " + name), index, index);
            }).ToArray();
        }

        // Explicit navigation work only. Prefer an unobstructed supported spot,
        // then free space. This does not promise immunity to map-specific hazards.
        internal static bool TryPosition(LevelScreen screen, int width, int height, out Vector2 position)
        {
            position = Vector2.Zero;
            if (screen == null || width <= 0 || width > 480 || height <= 0 || height > 352) return false;
            int top = -screen.GetIndex0() * 360;
            Vector2? free = null;
            int center = (480 - width) / 2;
            for (int y = 360 - height - 8; y >= 8; y -= 8)
                for (int delta = 0; delta < 480; delta += 8)
                    for (int side = 0; side < (delta == 0 ? 1 : 2); side++)
                    {
                        int x = center + (side == 0 ? delta : -delta);
                        if (x < 0 || x + width > 480) continue;
                        var box = new Rectangle(x, top + y, width, height);
                        Rectangle overlap; AdvCollisionInfo info;
                        if (screen.TryCollision(box, out overlap, out info)) continue;
                        if (!free.HasValue) free = new Vector2(x, top + y);
                        box.Y += 1;
                        if (!screen.TryCollision(box, out overlap, out info)) continue;
                        position = new Vector2(x, top + y); return true;
                    }
            if (!free.HasValue) return false;
            position = free.Value; return true;
        }
    }
}
