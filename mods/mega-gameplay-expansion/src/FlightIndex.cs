using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // Keep native collision code and original block order. Only its broad phase
    // changes: each private screen copy receives blocks near the queried hitbox.
    internal sealed class FlightIndex
    {
        private static readonly FieldInfo Blocks = typeof(LevelScreen).GetField("m_hitboxes", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IBlock[] blocks;
        private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        private readonly Dictionary<Rectangle, IBlock[]> selections = new Dictionary<Rectangle, IBlock[]>();
        private Rectangle lastRegion;
        private bool selectedOnce;
        internal readonly LevelScreen Screen;
        private static int Cell(int value) { return (int)Math.Floor(value / 64.0); }
        private static long Key(int x, int y) { return ((long)x << 32) ^ (uint)y; }
        internal FlightIndex(LevelScreen source, IBlock[] all)
        {
            if (all.Length > 16384) throw new InvalidOperationException("Flight screen exceeds 16384 collision blocks");
            blocks = all; Screen = (LevelScreen)Clone.Invoke(source, null);
            long entries = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                Rectangle box = blocks[i].GetRect();
                CheckRect(box);
                entries += ((long)Cell(box.Right) - Cell(box.Left) + 1) * ((long)Cell(box.Bottom) - Cell(box.Top) + 1);
                if (entries > 32768) throw new InvalidOperationException("Flight collision index budget exceeded");
                for (int y = Cell(box.Top); y <= Cell(box.Bottom); y++)
                for (int x = Cell(box.Left); x <= Cell(box.Right); x++)
                {
                    long key = Key(x,y); List<int> list;
                    if (!cells.TryGetValue(key, out list)) cells.Add(key, list = new List<int>());
                    list.Add(i);
                }
            }
        }
        internal void Select(Rectangle box)
        {
            CheckRect(box);
            var region = new Rectangle(Cell(box.Left - 1), Cell(box.Top - 1), Cell(box.Right + 1) - Cell(box.Left - 1) + 1, Cell(box.Bottom + 1) - Cell(box.Top - 1) + 1);
            if ((long)region.Width * region.Height > 256) throw new InvalidOperationException("Flight collision query is too large");
            if (selectedOnce && region == lastRegion) return;
            IBlock[] selected;
            if (!selections.TryGetValue(region, out selected))
            {
                var ids = new SortedSet<int>();
                for (int y = region.Top; y < region.Bottom; y++)
                for (int x = region.Left; x < region.Right; x++)
                {
                    List<int> list;
                    if (cells.TryGetValue(Key(x,y), out list)) foreach (int i in list) ids.Add(i);
                }
                selected = new IBlock[ids.Count]; int n = 0;
                foreach (int i in ids) selected[n++] = blocks[i];
                // A long unsuccessful flight must not retain an unbounded cache.
                if (selections.Count < 2048) selections.Add(region, selected);
            }
            Blocks.SetValue(Screen, selected);
            selectedOnce = true; lastRegion = region;
        }
        private static void CheckRect(Rectangle box)
        {
            if (box.Width < 0 || box.Height < 0 || Math.Abs((long)box.X) > 10000000 || Math.Abs((long)box.Y) > 10000000
                || (long)box.X + box.Width > 10000000 || (long)box.Y + box.Height > 10000000)
                throw new InvalidOperationException("Invalid flight collision rectangle");
        }
    }
}
