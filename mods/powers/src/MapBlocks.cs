using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace Powers
{
    public static class MapPixels
    {
        public static readonly Color WarpSurface = new Color(173, 47, 211, 255);
        public static readonly Color WarpScreen = new Color(173, 48, 211, 255);
        public static readonly Color WarpZone = new Color(173, 49, 211, 255);
        internal static readonly MapVariantSet Warp = new MapVariantSet("warp-jump",WarpSurface,WarpZone,WarpScreen,
            rectangle=>new WarpSurfaceBlock(rectangle),rectangle=>new WarpZoneBlock(rectangle));
        public static readonly Color AirDashSurface = new Color(173, 53, 211, 255);
        public static readonly Color AirDashScreen = new Color(173, 54, 211, 255);
        public static readonly Color AirDashZone = new Color(173, 55, 211, 255);
        internal static readonly MapVariantSet AirDash = new MapVariantSet("air-dash", AirDashSurface, AirDashZone, AirDashScreen,
            rectangle => new AirDashSurfaceBlock(rectangle), rectangle => new AirDashZoneBlock(rectangle));
    }
    public sealed class AirDashSurfaceBlock : BoxBlock, IBlockDebugColor
    {
        public AirDashSurfaceBlock(Rectangle rectangle) : base(rectangle) { }
        public Color DebugColor { get { return MapPixels.AirDashSurface; } }
    }
    public sealed class AirDashZoneBlock : BoxBlock, IBlockDebugColor
    {
        public AirDashZoneBlock(Rectangle rectangle) : base(rectangle) { }
        protected override bool canBlockPlayer { get { return false; } }
        public Color DebugColor { get { return MapPixels.AirDashZone; } }
    }


    public sealed class WarpSurfaceBlock : BoxBlock, IBlockDebugColor
    {
        public WarpSurfaceBlock(Rectangle rectangle) : base(rectangle) { }
        public Color DebugColor { get { return MapPixels.WarpSurface; } }
    }
    public sealed class WarpZoneBlock : BoxBlock, IBlockDebugColor
    {
        public WarpZoneBlock(Rectangle rectangle) : base(rectangle) { }
        protected override bool canBlockPlayer { get { return false; } }
        public Color DebugColor { get { return MapPixels.WarpZone; } }
    }
    public sealed class BlockFactory : IBlockFactory
    {
        private static readonly MapVariantSet[] variants = { MapPixels.Warp, MapPixels.AirDash };
        internal static IEnumerable<MapVariantSet> Variants { get { return variants; } }
        internal static HashSet<int> WarpScreens { get { return MapPixels.Warp.Screens; } }
        static BlockFactory()
        {
            var colours=new HashSet<Color>(); var ids=new HashSet<string>();
            foreach(var variant in variants)
            {
                if(!ids.Add(variant.Id)) throw new InvalidOperationException("Duplicate map mechanic: "+variant.Id);
                foreach(var colour in variant.Colours)
                    if(!colours.Add(colour)) throw new InvalidOperationException("Duplicate mechanic variant colour: "+colour);
            }
        }
        internal static void ResetScreens() { foreach(var variant in variants) { variant.Screens.Clear(); JKRuntime.Gameplay.MapMechanics.ClearMarkers(MapActivation.Id(variant.Id)); } }
        internal static void AssertExclusive(JumpKing.Workshop.Level level)
        {
            var field = typeof(LevelManager).GetField("BlockFactories", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Native block factory registry unavailable");
            foreach (IBlockFactory factory in (IEnumerable<IBlockFactory>)field.GetValue(null))
                if (!(factory is BlockFactory)) foreach(var colour in variants.SelectMany(v=>v.Colours))
                    if(factory.CanMakeBlock(colour,level))
                        throw new InvalidOperationException("Powers colour collision with " + factory.GetType().FullName + "; update the shared block registry");
        }
        public bool CanMakeBlock(Color code, JumpKing.Workshop.Level level)
        { foreach(var variant in variants) if(variant.Contains(code)) return true; return false; }
        public bool IsSolidBlock(Color code) { foreach(var variant in variants) if(code==variant.Solid) return true; return false; }
        public IBlock GetBlock(Color code, Rectangle rectangle, JumpKing.Workshop.Level level,
            LevelTexture texture, int screen, int x, int y)
        {
            JKRuntime.Gameplay.MapMechanics.BeginPixelDecode(texture);
            foreach(var variant in variants) if(variant.Contains(code)) return variant.Create(code,rectangle,screen);
            throw new InvalidOperationException("Powers does not own this block colour");
        }
    }
}
