using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal static class ExpansionPortals
    {
        private struct Link { internal Rectangle Bounds; internal int Destination; }
        private sealed class Fields
        {
            internal FieldInfo Bounds, Screen, Offset;
            internal bool Valid { get { return Bounds != null && Screen != null && Offset != null
                && Bounds.FieldType == typeof(Rectangle) && Screen.FieldType == typeof(int) && Offset.FieldType == typeof(int); } }
        }
        private static readonly Dictionary<Type, Fields> layouts = new Dictionary<Type, Fields>();
        private static readonly Dictionary<LevelScreen, List<Link>> links = new Dictionary<LevelScreen, List<Link>>();
        internal static void Reset() { links.Clear(); }
        private static List<Link> Read(LevelScreen screen)
        {
            List<Link> result;
            if (links.TryGetValue(screen, out result)) return result;
            result = new List<Link>(); links.Add(screen, result);
            foreach (var block in JKRuntime.Geometry.NativeWorldGeometry.ReadBlocks(screen))
            {
                if (block == null) continue;
                var type = block.GetType();
                if (type.FullName != "JumpKing_Expansion_Blocks.Blocks.MultiWarp"
                    || type.Assembly.GetName().Name != "JumpKing-Expansion-Blocks") continue;
                Fields fields;
                if (!layouts.TryGetValue(type, out fields))
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    fields = new Fields { Bounds = type.GetField("m_collider", flags),
                        Screen = type.GetField("<ToScreenNo>k__BackingField", flags), Offset = type.GetField("<Offset>k__BackingField", flags) };
                    layouts.Add(type, fields);
                }
                if (!fields.Valid) continue;
                // read stored geometry only, never execute foreign collision code in Draw
                int target = (int)fields.Screen.GetValue(block), offset = (int)fields.Offset.GetValue(block);
                if (target < 0 || target > 255 || offset < 0 || offset > 255) continue;
                result.Add(new Link { Bounds = (Rectangle)fields.Bounds.GetValue(block), Destination = target + offset * 255 - 1 });
            }
            return result;
        }
        private static bool Covers(Link link, bool left, float y)
        {
            // MultiWarp uses contact and the king's center at the map edge
            var r = link.Bounds;
            return r.Width > 0 && r.Height > 0 && (left ? r.Left < 9 && r.Right > -9 : r.Left < 489 && r.Right > 471)
                && (float.IsNaN(y) || r.Top < y + 13 && r.Bottom > y - 13);
        }
        internal static int Destination(LevelScreen[] screens, int source, bool left, float y)
        {
            int result = -1;
            foreach (var link in Read(screens[source]))
            {
                if (!Covers(link, left, y) || link.Destination < 0 || link.Destination >= screens.Length) continue;
                // one viewport column can't display two incompatible destinations
                if (result >= 0 && result != link.Destination) return -1;
                result = link.Destination;
            }
            return result;
        }
        internal static bool Connects(LevelScreen[] screens, int source, bool left, float y, int destination)
        {
            foreach (var link in Read(screens[source]))
                if (Covers(link, left, y) && link.Destination == destination) return true;
            return false;
        }
    }
}
