using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Inspection
{
    // Uses native contracts and observed objects, no foreign assembly/type allowlist
    internal static class GimmickBlocks
    {
        internal static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", Gimmicks.Members);
        internal static readonly FieldInfo Hitboxes = typeof(LevelScreen).GetField("m_hitboxes", Gimmicks.Members);
        private static readonly FieldInfo Factories = typeof(LevelManager).GetField("BlockFactories", Gimmicks.Members);
        private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", Gimmicks.Members);
        private static readonly HashSet<MethodInfo> hooked = new HashSet<MethodInfo>();
        private static JKRuntime.OwnedPatches harmony;
        private static bool installed;
        private static readonly Dictionary<Type, HashSet<uint>> observed = new Dictionary<Type, HashSet<uint>>();
        internal static string HookStatus = "Block observation has not started";
        internal static IBlockFactory[] ReadFactories()
        { return ((IEnumerable<IBlockFactory>)Factories.GetValue(null)).ToArray(); }
        internal static void Install()
        {
            if (installed) return;
            try { BlockObservation.Install(); installed = true; HookStatus = BlockObservation.Status; }
            catch (Exception error) { HookStatus = "Loaded geometry only: " + error.GetBaseException().Message; }
        }
        internal static void HookGetter(MethodInfo target, MethodInfo callback)
        {
            if (hooked.Contains(target)) return;
            if (harmony == null) harmony = new OwnedPatches("jk-runtime.inspector.overrides");
            harmony.Add(target, postfix: callback); hooked.Add(target);
        }
        internal static void Observe(Type factory, Color colour, IBlock block)
        {
            HashSet<uint> colours;
            if (!observed.TryGetValue(factory, out colours)) observed.Add(factory, colours = new HashSet<uint>());
            if (!colours.Add(colour.PackedValue)) return;
            string id = Gimmicks.BlockId(factory, colour);
            string reason;
            bool portable = CanCopy(block, out reason);
            Gimmicks.Add(new GimmickEntry { Id = id, Label = block == null ? "Screen marker " + RGB(colour) : Gimmicks.Human(block.GetType().Name) + " " + RGB(colour),
                Owner = factory.Assembly.GetName().Name, Kind = "Block", Colour = colour,
                Template = portable ? Copy(block, block.GetRect()) : null, Error = portable ? null : reason,
                Detail = "Observed factory: " + factory.FullName + ". Native handler registrations are resolved when enabled." });
        }
        internal static string RGB(Color c) { return c.R + "," + c.G + "," + c.B; }
        internal static bool CanCopy(IBlock block, out string reason)
        {
            reason = null;
            if (block == null) { reason = "Metadata-only factory result. Inspect its live screen state; no collider to copy."; return false; }
            if (block.GetType() == typeof(SlopeBlock)) return true;
            var rectangles = new List<FieldInfo>();
            for (Type type = block.GetType(); type != null && type != typeof(object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(Rectangle)) rectangles.Add(field);
                    else if (!Scalar(field.FieldType)) { reason = "Block owns non-scalar instance state: " + field.Name; return false; }
            if (rectangles.Count == 0 || rectangles.Any(f => (Rectangle)f.GetValue(block) != block.GetRect()))
            { reason = "Block does not expose one consistent rectangular collider."; return false; }
            return true;
        }
        private static bool Scalar(Type type)
        { return (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr)) || type.IsEnum || type == typeof(decimal) || type == typeof(Color) || type == typeof(string); }
        internal static IBlock Copy(IBlock source, Rectangle rect)
        {
            string reason; if (!CanCopy(source, out reason)) throw new InvalidOperationException(reason);
            if (source.GetType() == typeof(SlopeBlock)) {
                var slope = (SlopeBlock)source;
                var copySlope = Geometry.NativeWorldGeometry.CopySlopeCollision(slope);
                var old = slope.GetRect();
                if (old == rect) return copySlope;
                if (old.Width <= 0 || old.Height <= 0) throw new InvalidOperationException("Cannot resize an empty slope");
                var field = typeof(SlopeBlock).GetField("m_lines", Gimmicks.Members);
                var lines = (ErikMaths.Line[])field.GetValue(copySlope);
                for (int i = 0; i < lines.Length; i++) {
                    lines[i].p0 = Scale(lines[i].p0, old, rect);
                    lines[i].p1 = Scale(lines[i].p1, old, rect);
                }
                typeof(SlopeBlock).GetField("m_box", Gimmicks.Members).SetValue(copySlope, rect);
                return copySlope;
            }
            var copy = (IBlock)Clone.Invoke(source, null);
            for (Type type = copy.GetType(); type != null && type != typeof(object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(Rectangle)) field.SetValue(copy, rect);
            return copy;
        }
        private static Point Scale(Point point, Rectangle from, Rectangle to)
        { return new Point(to.X + (int)Math.Round((double)(point.X - from.X) * to.Width / from.Width),
            to.Y + (int)Math.Round((double)(point.Y - from.Y) * to.Height / from.Height)); }
        internal static void ClearWorld()
        {
            observed.Clear();
            foreach (var key in Gimmicks.Entries.Where(p => p.Value.Kind == "Block" || p.Value.Kind == "Colour" || p.Value.Slot != null).Select(p => p.Key).ToArray())
                Gimmicks.Entries.Remove(key);
            Gimmicks.Generation++;
        }
        internal static void DiscoverPalette(IEnumerable<Color> mapColours)
        {
            var candidates = new HashSet<Color>(mapColours);
            // Encoded colours may have come from a source-map search in a previous
            // process. keep saved rules discoverable before that index finishes
            foreach (string id in (InspectorSettings.Current.GimmickRules ?? new GimmickRule[0]).Where(r => r != null).Select(r => r.SourceId ?? r.Id))
            {
                uint packed;
                if (id != null && id.StartsWith("block:", StringComparison.Ordinal)
                    && uint.TryParse(id.Substring(id.LastIndexOf(':') + 1), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out packed)) candidates.Add(new Color { PackedValue = packed });
            }
            foreach (var factory in ReadFactories())
            {
                for (Type type = factory.GetType(); type != null; type = type.BaseType)
                    foreach (var field in type.GetFields(Gimmicks.Members | BindingFlags.DeclaredOnly))
                    {
                        if (field.FieldType != typeof(Color) && !typeof(IEnumerable<Color>).IsAssignableFrom(field.FieldType)) continue;
                        try
                        {
                            object value = field.GetValue(factory);
                            if (value is Color) candidates.Add((Color)value);
                            var palette = value as IEnumerable<Color>;
                            if (palette != null) foreach (Color c in palette.Take(16384)) candidates.Add(c);
                        }
                        catch { }
                    }
            }
            foreach (var factory in ReadFactories()) foreach (Color colour in candidates)
            {
                if (colour.A != 255) continue;
                string id = Gimmicks.BlockId(factory.GetType(), colour);
                if (Gimmicks.Entries.ContainsKey(id)) continue;
                try
                {
                    var level = Game1.instance == null || Game1.instance.contentManager == null ? null : Game1.instance.contentManager.level;
                    if (!factory.CanMakeBlock(colour, level)) continue;
                    Gimmicks.Add(new GimmickEntry { Id = id, Owner = factory.GetType().Assembly.GetName().Name,
                        Kind = "Block", Colour = colour, Factory = factory, Label = Gimmicks.Human(factory.GetType().Name) + " " + RGB(colour),
                        Detail = "Select Prepare material sample to construct from the installed factory. No source-map visit required." });
                }
                catch (Exception error) { HookStatus = "Palette query: " + error.GetBaseException().Message; }
            }
        }
        internal static void DiscoverLoaded()
        {
            BlockObservation.ReadFinalized();
            var screens = Screens.GetValue(null) as LevelScreen[]; if (screens == null) return;
            var seen = new HashSet<Type>(Gimmicks.Entries.Values.Where(e => e.Template != null).Select(e => e.Template.GetType()));
            foreach (var screen in screens) foreach (IBlock block in (IBlock[])Hitboxes.GetValue(screen))
            {
                if (!seen.Add(block.GetType())) continue;
                string reason; bool available = CanCopy(block, out reason);
                var debug = block as IBlockDebugColor;
                string id = "loaded:" + Gimmicks.TypeId(block.GetType());
                Gimmicks.Add(new GimmickEntry { Id = id, Label = Gimmicks.Human(block.GetType().Name), Owner = block.GetType().Assembly.GetName().Name,
                    Kind = "Block", Template = available ? Copy(block, block.GetRect()) : null, Error = reason,
                    Colour = debug == null ? (Color?)null : debug.DebugColor,
                    Detail = "Loaded collider; colour/factory provenance may be unavailable." });
            }
        }
    }
}
