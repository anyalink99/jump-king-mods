using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // Uses native contracts and observed objects; no foreign assembly/type allowlist.
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
            try
            {
                if (harmony != null) { harmony.Dispose(); harmony = null; hooked.Clear(); }
                harmony = new JKRuntime.OwnedPatches("mega-gameplay-expansion.discovery");
                Hook(typeof(LevelManager).GetMethod("RegisterBlockFactory"), "Registered");
                Hook(typeof(LevelManager).GetMethod("LoadScreens"), "FactoryBoundary", true);
                foreach (var factory in ReadFactories()) HookFactory(factory);
                installed = true; HookStatus = "Native factory results observed without replaying factory calls";
            }
            catch (Exception error) { HookFailed(error); }
        }
        private static void HookFailed(Exception error)
        {
            installed = false; HookStatus = error.GetBaseException().Message;
            try { if (harmony != null) harmony.Dispose(); harmony = null; hooked.Clear(); }
            catch (Exception cleanup) { HookStatus += "; cleanup pending: " + cleanup.GetBaseException().Message; }
        }
        private static void Hook(MethodInfo target, string callback, bool prefix = false)
        {
            if (hooked.Contains(target)) return;
            var method = typeof(GimmickBlocks).GetMethod(callback, Gimmicks.Members);
            harmony.Add(target, prefix: prefix ? method : null, postfix: prefix ? null : method); hooked.Add(target);
        }
        internal static void FactoryBoundary()
        {
            // RegisterBlockFactory can be inlined in an already-JITted mod. The
            // public load boundary sees the complete registry before any pixels.
            try { using (JKRuntime.RuntimeApi.MeasureStartup("mega-gameplay.observe-factories")) foreach (var factory in ReadFactories()) HookFactory(factory); }
            catch (Exception error) { HookFailed(error); }
        }
        internal static void HookGetter(MethodInfo target, MethodInfo callback)
        {
            Install();
            if (!installed || harmony == null) throw new InvalidOperationException(HookStatus);
            if (hooked.Contains(target)) return;
            try { harmony.Add(target, postfix: callback); hooked.Add(target); }
            catch (Exception error) { HookFailed(error); throw; }
        }
        private static void HookFactory(IBlockFactory factory)
        {
            var mapping = factory.GetType().GetInterfaceMap(typeof(IBlockFactory));
            for (int i = 0; i < mapping.InterfaceMethods.Length; i++)
                if (mapping.InterfaceMethods[i].Name == "GetBlock") Hook(mapping.TargetMethods[i], "Observed");
        }
        private static void Registered(IBlockFactory __0)
        { try { HookFactory(__0); } catch (Exception error) { HookFailed(error); } }
        private static void Observed(IBlockFactory __instance, Color __0, IBlock __result)
        {
            // A postfix must never make an otherwise successful level load fail.
            try { Observe(__instance.GetType(), __0, __result); }
            catch (Exception error) { HookStatus = "Observation: " + error.GetBaseException().Message; }
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
            if (source.GetType() == typeof(SlopeBlock)) return new SlopeBlock(rect, ((SlopeBlock)source).GetSlopeType());
            var copy = (IBlock)Clone.Invoke(source, null);
            for (Type type = copy.GetType(); type != null && type != typeof(object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(Rectangle)) field.SetValue(copy, rect);
            return copy;
        }
        internal static void DiscoverPalette(IEnumerable<Color> mapColours)
        {
            var candidates = new HashSet<Color>(mapColours);
            // Encoded colours may have come from a source-map search in a previous
            // process. Keep saved pins/rules discoverable before that index finishes.
            foreach (string id in Gimmicks.Pins.Concat((Settings.Current.GimmickRules ?? new GimmickRule[0]).Where(r => r != null).Select(r => r.SourceId ?? r.Id)))
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
                        Detail = "Open to construct from the installed factory. No source-map visit required." });
                }
                catch (Exception error) { HookStatus = "Palette query: " + error.GetBaseException().Message; }
            }
        }
        internal static void DiscoverLoaded()
        {
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
