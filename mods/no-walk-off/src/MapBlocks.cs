using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace NoWalkOff
{
    public static class MapPixels
    {
        public static readonly Color NoWalkOffSurface = new Color(173, 50, 211, 255);
        public static readonly Color NoWalkOffScreen = new Color(173, 51, 211, 255);
        public static readonly Color NoWalkOffZone = new Color(173, 52, 211, 255);
        internal static readonly MapVariantSet NoWalkOff = new MapVariantSet("no-walk-off", NoWalkOffSurface, NoWalkOffZone, NoWalkOffScreen,
            rectangle => new NoWalkOffSurfaceBlock(rectangle), rectangle => new NoWalkOffZoneBlock(rectangle));
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


    public sealed class BlockFactory : IBlockFactory
    {
        internal static bool NoWalkOffAuthored;
        private static readonly MapVariantSet[] variants = { MapPixels.NoWalkOff };
        internal static IEnumerable<MapVariantSet> Variants { get { return variants; } }
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
        internal static void ResetScreens() { NoWalkOffAuthored=false; foreach(var variant in variants) { variant.Screens.Clear(); JKRuntime.Gameplay.MapMechanics.ClearMarkers(MapActivation.Id(variant.Id)); } }
        internal static void AssertExclusive(JumpKing.Workshop.Level level)
        {
            var field = typeof(LevelManager).GetField("BlockFactories", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Native block factory registry unavailable");
            foreach (IBlockFactory factory in (IEnumerable<IBlockFactory>)field.GetValue(null))
                if (!(factory is BlockFactory)) foreach(var colour in variants.SelectMany(v=>v.Colours))
                    if(factory.CanMakeBlock(colour,level))
                        throw new InvalidOperationException("No Walk Off colour collision with " + factory.GetType().FullName + "; update the shared block registry");
        }
        public bool CanMakeBlock(Color code, JumpKing.Workshop.Level level)
        { foreach(var variant in variants) if(variant.Contains(code)) return true; return false; }
        public bool IsSolidBlock(Color code) { foreach(var variant in variants) if(code==variant.Solid) return true; return false; }
        public IBlock GetBlock(Color code, Rectangle rectangle, JumpKing.Workshop.Level level,
            LevelTexture texture, int screen, int x, int y)
        {
            JKRuntime.Gameplay.MapMechanics.BeginPixelDecode(texture);
            if(MapPixels.NoWalkOff.Contains(code)) NoWalkOffAuthored=true;
            foreach(var variant in variants) if(variant.Contains(code)) return variant.Create(code,rectangle,screen);
            throw new InvalidOperationException("No Walk Off does not own this block colour");
        }
    }
}
