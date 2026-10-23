using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using JKRuntime;
using JumpKing.API;
using JumpKing.Level.Sampler;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class CollisionSlope
    {
        internal Color Color, Source;
        internal SlopeType Shape;
    }

    internal sealed class CollisionShapes
    {
        internal const string PathName="screens/mega-mapping-expansion/collision-shapes.xml";
        private readonly Dictionary<Color,CollisionSlope> slopes=new Dictionary<Color,CollisionSlope>();
        [ThreadStatic] internal static CollisionShapes Sampling;
        internal IBlock Create(IBlockFactory factory,CollisionSlope alias,Rectangle bounds,JumpKing.Workshop.Level level,LevelTexture texture,int screen,int x,int y)
        {
            var previous=Sampling;Sampling=this;
            try { return JKRuntime.Geometry.NativeWorldGeometry.CreateBlock(factory,alias.Source,bounds,level,texture,screen,x,y); }
            finally {Sampling=previous;}
        }
        internal CollisionSlope Find(Color color) { CollisionSlope slope; return slopes.TryGetValue(color,out slope)?slope:null; }
        internal static CollisionShapes Read(string root)
        {
            var result=new CollisionShapes();string file=Path.Combine(root,PathName);
            if(!File.Exists(file))return result;
            var xml=MappingFiles.Open(file).DocumentElement;
            if(xml.Name!="CollisionShapes" || xml.GetAttribute("version")!="1" || xml.Attributes.Count!=1)
                throw new InvalidDataException("Expected CollisionShapes version 1");
            foreach(System.Xml.XmlNode node in xml.ChildNodes)
            {
                var item=node as System.Xml.XmlElement;if(item==null)continue;
                SlopeType shape;
                if(item.Name!="Slope" || item.Attributes.Count!=3 || item.HasChildNodes
                    || !Enum.TryParse<SlopeType>(item.GetAttribute("shape"),false,out shape)
                    || shape==SlopeType.None || !Enum.IsDefined(typeof(SlopeType),shape))
                    throw new InvalidDataException("Slope requires color, source and TopLeft/TopRight/BottomLeft/BottomRight shape");
                var slope=new CollisionSlope { Color=Colour(item.GetAttribute("color")),Source=Colour(item.GetAttribute("source")),Shape=shape };
                if(slope.Color==slope.Source || result.slopes.ContainsKey(slope.Color))throw new InvalidDataException("Slope aliases must be unique and differ from their source");
                result.slopes.Add(slope.Color,slope);
                if(result.slopes.Count>1024)throw new InvalidDataException("Too many collision slope aliases");
            }
            if(result.slopes.Values.Any(s=>result.slopes.ContainsKey(s.Source)))throw new InvalidDataException("Slope aliases cannot reference other aliases");
            return result;
        }
        private static Color Colour(string text)
        {
            uint rgb;
            if(text.Length!=7 || text[0]!='#' || !uint.TryParse(text.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))
                throw new InvalidDataException("Collision colors require #RRGGBB");
            return new Color((int)(rgb>>16),(int)((rgb>>8)&255),(int)(rgb&255),255);
        }
    }

    internal static class SlopeClipping
    {
        private sealed class Clip { internal IBlock Original,Mask; internal SlopeBlock Slope; }
        private static ConditionalWeakTable<IBlock,Clip> originals=new ConditionalWeakTable<IBlock,Clip>();
        private static ConditionalWeakTable<IBlock,Clip> geometry=new ConditionalWeakTable<IBlock,Clip>();
        private static readonly HashSet<MethodInfo> patched=new HashSet<MethodInfo>();
        private static readonly OwnedPatches hooks=new OwnedPatches("mega-mapping-expansion.collision-shapes");
        internal static void BeginWorld()
        {
            // foreign factories may cache objects; a new map mustn't inherit the last map's clipping
            originals=new ConditionalWeakTable<IBlock,Clip>();geometry=new ConditionalWeakTable<IBlock,Clip>();
        }
        internal static IBlock[] Apply(IBlock original,SlopeType shape)
        {
            if(original==null || original.GetType().IsValueType)throw new NotSupportedException("Slope source must create a reference-type IBlock");
            Clip ignored;
            if(originals.TryGetValue(original,out ignored))throw new InvalidDataException("Slope factory reused a block instance");
            var clip=new Clip { Original=original,Slope=new SlopeBlock(original.GetRect(),shape),Mask=new SlopeBlock(original.GetRect(),shape) };
            Patch(original.GetType());Patch(typeof(SlopeBlock));
            originals.Add(original,clip);geometry.Add(clip.Slope,clip);
            // keep the foreign instance and type for its body behavior; native slopes supply sliding
            return new IBlock[] { original,clip.Slope };
        }
        internal static IBlock SourceOf(IBlock block)
        {Clip clip;return geometry.TryGetValue(block,out clip)?clip.Original:block;}
        internal static Vector2? NormalOf(IBlock block)
        {
            Clip clip;if(originals.TryGetValue(block,out clip)||geometry.TryGetValue(block,out clip))return clip.Slope.GetNormal();
            var slope=block as SlopeBlock;return slope==null?(Vector2?)null:slope.GetNormal();
        }
        private static void Patch(Type type)
        {
            var map=type.GetInterfaceMap(typeof(IBlock));
            var method=map.TargetMethods[Array.FindIndex(map.InterfaceMethods,m=>m.Name=="Intersects")];
            // interface maps can reflect an inherited method through the derived type
            method=method.DeclaringType.GetMethods(OwnedPatches.Members|BindingFlags.DeclaredOnly).Single(m=>m.MetadataToken==method.MetadataToken);
            if(patched.Contains(method))return;
            if(method.IsAbstract || method.ContainsGenericParameters)throw new NotSupportedException("Cannot clip "+type.FullName+".Intersects");
            hooks.Add(method,postfix:typeof(SlopeClipping).GetMethod("AfterContact",OwnedPatches.Members));
            patched.Add(method);
        }
        private static void AfterContact(IBlock __instance,Rectangle __0,ref Rectangle __1,ref BlockCollisionType __result)
        {
            if(sourcePolicy)return;
            Clip clip;
            if(originals.TryGetValue(__instance,out clip))
            {
                if(__result==BlockCollisionType.NoCollision)return;
                Rectangle area;
                // query an unbound native triangle so the geometry helper doesn't recurse
                if(clip.Mask.Intersects(__0,out area)==BlockCollisionType.NoCollision)
                { __result=BlockCollisionType.NoCollision;__1=Rectangle.Empty;return; }
                __1=Rectangle.Intersect(__1,area);
                __result=BlockCollisionType.Collision_NonBlocking;
            }
            else if(geometry.TryGetValue(__instance,out clip) && __result!=BlockCollisionType.NoCollision)
            {
                // evaluate the source's real blocking policy before its clipping postfix
                Rectangle area;
                var policy=SourcePolicy(clip.Original,__0,out area);
                if(policy!=BlockCollisionType.Collision_Blocking){__result=BlockCollisionType.NoCollision;__1=Rectangle.Empty;}
            }
        }
        [ThreadStatic] private static bool sourcePolicy;
        private static BlockCollisionType SourcePolicy(IBlock block,Rectangle hitbox,out Rectangle area)
        {
            bool previous=sourcePolicy;sourcePolicy=true;
            try { return block.Intersects(hitbox,out area); }
            finally {sourcePolicy=previous;}
        }
    }
}
