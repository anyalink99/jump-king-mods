using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    public static class MapPixels
    {
        public static readonly Color WarpSurface = new Color(173, 47, 211, 255);
        public static readonly Color WarpScreen = new Color(173, 48, 211, 255);
        public static readonly Color WarpZone = new Color(173, 49, 211, 255);
        internal static readonly MapVariantSet Warp = new MapVariantSet("warp-jump",WarpSurface,WarpZone,WarpScreen,
            rectangle=>new WarpSurfaceBlock(rectangle),rectangle=>new WarpZoneBlock(rectangle));
        public static readonly Color NoWalkOffSurface = new Color(173, 50, 211, 255);
        public static readonly Color NoWalkOffScreen = new Color(173, 51, 211, 255);
        public static readonly Color NoWalkOffZone = new Color(173, 52, 211, 255);
        internal static readonly MapVariantSet NoWalkOff = new MapVariantSet("no-walk-off", NoWalkOffSurface, NoWalkOffZone, NoWalkOffScreen,
            rectangle => new NoWalkOffSurfaceBlock(rectangle), rectangle => new NoWalkOffZoneBlock(rectangle));
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
    public sealed class NoWalkOffSurfaceBlock : BoxBlock, IBlockDebugColor
    {
        public NoWalkOffSurfaceBlock(Rectangle rectangle) : base(rectangle) { }
        public Color DebugColor { get { return MapPixels.NoWalkOffSurface; } }
    }
    public sealed class NoWalkOffZoneBlock : BoxBlock, IBlockDebugColor
    {
        public NoWalkOffZoneBlock(Rectangle rectangle) : base(rectangle) { }
        protected override bool canBlockPlayer { get { return false; } }
        public Color DebugColor { get { return MapPixels.NoWalkOffZone; } }
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
    public sealed class MegaBlockFactory : IBlockFactory
    {
        internal static bool NoWalkOffAuthored;
        private static readonly MapVariantSet[] variants = { MapPixels.Warp, MapPixels.NoWalkOff, MapPixels.AirDash };
        internal static IEnumerable<MapVariantSet> Variants { get { return variants; } }
        internal static HashSet<int> WarpScreens { get { return MapPixels.Warp.Screens; } }
        static MegaBlockFactory()
        {
            var colours=new HashSet<Color>(); var ids=new HashSet<string>();
            foreach(var variant in variants)
            {
                if(!ids.Add(variant.Id)) throw new InvalidOperationException("Duplicate map mechanic: "+variant.Id);
                foreach(var colour in variant.Colours)
                    if(!colours.Add(colour)) throw new InvalidOperationException("Duplicate mechanic variant colour: "+colour);
            }
        }
        internal static void ResetScreens() { NoWalkOffAuthored=false; foreach(var variant in variants) variant.Screens.Clear(); }
        internal static void AssertExclusive(JumpKing.Workshop.Level level)
        {
            var field = typeof(LevelManager).GetField("BlockFactories", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Native block factory registry unavailable");
            foreach (IBlockFactory factory in (IEnumerable<IBlockFactory>)field.GetValue(null))
                if (!(factory is MegaBlockFactory)) foreach(var variant in variants) foreach(var colour in variant.Colours)
                    if(factory.CanMakeBlock(colour,level))
                        throw new InvalidOperationException("Mega Gameplay Expansion colour collision with " + factory.GetType().FullName + "; update the shared block registry");
        }
        public bool CanMakeBlock(Color code, JumpKing.Workshop.Level level)
        { foreach(var variant in variants) if(variant.Contains(code)) return true; return false; }
        public bool IsSolidBlock(Color code) { foreach(var variant in variants) if(code==variant.Solid) return true; return false; }
        public IBlock GetBlock(Color code, Rectangle rectangle, JumpKing.Workshop.Level level,
            LevelTexture texture, int screen, int x, int y)
        {
            if(MapPixels.NoWalkOff.Contains(code)) NoWalkOffAuthored=true;
            foreach(var variant in variants) if(variant.Contains(code)) return variant.Create(code,rectangle,screen);
            throw new InvalidOperationException("Mega Gameplay Expansion does not own this block colour");
        }
    }
}
