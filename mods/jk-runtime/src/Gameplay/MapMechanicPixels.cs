using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    public static partial class MapMechanics
    {
        private static readonly string[] palette = { "ball-king.form", "smooth-camera.tracking", "mega.warp", "mega.no-walk-off", "mega.air-dash",
            "more-items.jetpack", "more-items.hammer", "more-items.rewinder", "casual.controls", "subframe-charge.timing" };
        private static readonly PixelFactory factory = new PixelFactory();
        private static bool factoryInstalled;
        private static object decodedTexture;
        private static string decodedRoot;
        /// <summary>Associate marker metadata with the native decode, including loaders without early preparation hooks</summary>
        public static void BeginPixelDecode(LevelTexture texture)
        {
            var game = JumpKing.Game1.instance;
            if (texture != null && game != null && game.contentManager != null)
                RecordDecode(game.contentManager.root, texture);
        }
        internal static void RecordDecode(string root, object texture)
        {
            RuntimeApi.Kernel.CheckThread();
            if (ReferenceEquals(decodedTexture, texture)) return;
            decodedTexture = texture; decodedRoot = System.IO.Path.GetFullPath(root);
            pixels.Clear();
        }
        internal static Rule[] CaptureDecoded(string root)
        { return string.Equals(decodedRoot, System.IO.Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) ? pixels.Select(Copy).ToArray() : new Rule[0]; }
        internal static void RestoreDecoded(string root, Rule[] rules)
        {
            if (rules.Length == 0) return;
            decodedRoot = System.IO.Path.GetFullPath(root);
            foreach (var rule in rules) { ValidateValues(rule); MergePixel(rule); }
            Generation++;
        }
        /// <summary>reviewed opaque screen marker colour. on/Off/Local never create terrain</summary>
        public static Color ScreenColor(string id, MapMechanicMode mode)
        {
            int index = Array.IndexOf(palette, id), offset = mode == MapMechanicMode.On ? 0 : mode == MapMechanicMode.Off ? 1 : mode == MapMechanicMode.Local ? 2 : -1;
            if (index < 0 || offset < 0) throw new ArgumentException("No built-in marker for " + id + " / " + mode);
            return new Color(173, 80 + index * 3 + offset, 211, 255);
        }
        private static void EnsureFactory()
        { if (!factoryInstalled) { LevelManager.RegisterBlockFactory(factory); factoryInstalled = true; } }
        internal static void ValidateFactory()
        {
            if (!factoryInstalled) return;
            var field = typeof(LevelManager).GetField("BlockFactories", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new NotSupportedException("Native factory registry unavailable");
            foreach (var other in (IEnumerable<IBlockFactory>)field.GetValue(null)) if (!ReferenceEquals(other, factory))
                foreach (string id in palette) foreach (var mode in new[] { MapMechanicMode.On, MapMechanicMode.Off, MapMechanicMode.Local })
                    if (other.CanMakeBlock(ScreenColor(id, mode), JumpKing.Game1.instance.contentManager.level))
                        throw new InvalidOperationException("Mechanic marker colour conflict with " + other.GetType().FullName + ": " + id);
        }
        private sealed class PixelFactory : IBlockFactory
        {
            public bool CanMakeBlock(Color code, JumpKing.Workshop.Level level)
            { return code.A == 255 && code.R == 173 && code.B == 211 && code.G >= 80 && code.G < 80 + palette.Length * 3; }
            public bool IsSolidBlock(Color code) { return false; }
            public IBlock GetBlock(Color code, Rectangle rectangle, JumpKing.Workshop.Level level, LevelTexture texture, int screen, int x, int y)
            {
                if (!CanMakeBlock(code, level)) throw new ArgumentException("Unknown marker colour");
                BeginPixelDecode(texture);
                int value = code.G - 80;
                DeclareScreen(palette[value / 3], screen, (MapMechanicMode)(value % 3 + 1), "screen pixel " + code);
                return null;
            }
        }
    }
}
