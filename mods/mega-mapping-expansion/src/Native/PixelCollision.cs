using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Level.Factory;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed class PixelLevelTexture:LevelTexture
    {
        internal readonly MapLayout Layout;
        internal int BlockCount;
        internal CollisionShapes Shapes=new CollisionShapes();
        private readonly Color[] data;
        internal PixelLevelTexture(MapLayout layout,Color[] pixels):base(pixels,layout.ImageWidth,layout.ImageHeight)
        {Layout=layout;data=pixels;layout.ValidateSampling((x,y)=>pixels[y*layout.ImageWidth+x].PackedValue);}
        internal Color Sample(int screen,int x,int y)
        {
            int px,py;if(!Layout.Coordinate(screen,x,y,out px,out py))return Color.Transparent;
            return data[py*Layout.ImageWidth+px];
        }
    }

    internal static class PixelCollision
    {
        private static OwnedPatches hooks;
        private static readonly HashSet<MethodBase> guarded=new HashSet<MethodBase>();
        internal static bool RegisterFactory(Type type)
        {
            return Api.PixelCollisionFactories.Register(type);
        }
        private static readonly FieldInfo Factories=typeof(LevelManager).GetField("BlockFactories",OwnedPatches.Members);
        private static MethodInfo Method(string name){return typeof(PixelCollision).GetMethod(name,OwnedPatches.Members);}
        internal static void Install()
        {
            if(hooks==null)
            {
                var pending=new OwnedPatches("mega-mapping-expansion.collision");
                try
                {
                    pending.ReplaceCalls(typeof(LevelManager).GetMethod("LoadScreens",OwnedPatches.Members),typeof(LevelTexture).GetMethod("FromTexture"),Method("Read"),1);
                    pending.Add(typeof(LevelTexture).GetMethod("GetTotalScreensPotential"),prefix:Method("Count"));
                    pending.Add(typeof(LevelTexture).GetMethod("GetColor"),prefix:Method("Sample"));
                    pending.Add(typeof(LevelTexture).GetMethod("GetCoord",new[]{typeof(int),typeof(int),typeof(int)}),prefix:Method("Coord"));
                    pending.Add(typeof(LevelManager).GetMethod("LoadBlocksInterval",OwnedPatches.Members),prefix:Method("Blocks"),priority:1000);
                    pending.Add(typeof(LevelManager).GetMethod("IsBlockEdge"),prefix:Method("Edge"));
                    hooks=pending;
                }
                catch{pending.Dispose();throw;}
            }
            // MBS and Mapping can be installed together, but only one loader owns level+
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="MoreBlockSizes"))
                foreach(string name in new[]{"PatchLoadScreens","PatchLoadBlocksInterval"})
                {
                    var type=assembly.GetType("MoreBlockSizes.Patches."+name);
                    var prefix=type==null?null:type.GetMethod("Prefix",OwnedPatches.Members);
                    if(prefix==null)throw new NotSupportedException("This More Block Sizes loader is incompatible with MapLayout version 2.");
                    if(guarded.Add(prefix))hooks.Add(prefix,prefix:Method("GuardSizes"),priority:1000);
                }
        }
        private static bool GuardSizes(ref bool __result)
        {
            var layout=MapLayout.Read(MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root));
            if(layout==null||!layout.Pixel)return true;
            __result=true;return false;
        }
        private static LevelTexture Read(Texture2D texture)
        {
            SlopeClipping.BeginWorld();
            string root=MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root);
            var layout=MapLayout.Read(root);
            if(layout==null||!layout.Pixel)return LevelTexture.FromTexture(texture);
            Color[] data=null;
            using(RuntimeApi.MeasureStartup("mega-mapping.collision-read"))
                CollisionXnb.Read(Path.Combine(root,"level+.xnb"),layout,delegate(BinaryReader reader,int count){
                    data=new Color[count];for(int i=0;i<count;i++)data[i]=new Color{PackedValue=CollisionXnb.Colour(reader.ReadUInt32())};
                });
            ModEntry.Log("Pixel collision: "+layout.Screens+" screens, "+layout.CellWidth+"x"+layout.CellHeight+" cells, "+layout.Source);
            return new PixelLevelTexture(layout,data) { Shapes=CollisionShapes.Read(root) };
        }
        private static bool Count(LevelTexture __instance,ref int __result)
        {var p=__instance as PixelLevelTexture;if(p==null)return true;__result=p.Layout.Screens;return false;}
        private static bool Sample(LevelTexture __instance,int p_screen,int p_x,int p_y,ref Color __result)
        {
            var p=__instance as PixelLevelTexture;if(p==null)return true;
            __result=p.Sample(p_screen,p_x,p_y);
            var alias=CollisionShapes.Sampling==null?null:CollisionShapes.Sampling.Find(__result);
            if(alias!=null)__result=alias.Source;
            return false;
        }
        private static bool Coord(LevelTexture __instance,int p_screen,int x,int y,ref Point __result)
        {
            var p=__instance as PixelLevelTexture;if(p==null)return true;
            int px,py;__result=p.Layout.Coordinate(p_screen,x,y,out px,out py)?new Point(px,py):new Point(-1,-1);return false;
        }
        private static bool Edge(LevelTexture p_src,int p_screen,int p_x,int p_y,ref bool __result)
        {
            var p=p_src as PixelLevelTexture;if(p==null)return true;
            Color c=p.Sample(p_screen,p_x,p_y);
            var alias=p.Shapes.Find(c);if(alias!=null)c=alias.Source;
            __result=p_x<0||p_x>=p.Layout.Width(p_screen)||c==new Color(128,128,128)||((List<IBlockFactory>)Factories.GetValue(null)).Any(f=>f.IsSolidBlock(c));
            return false;
        }
        private sealed class Run
        {internal Color Code;internal Rectangle Bounds;internal IBlockFactory Factory;internal int X,Y;internal IBlock Block;internal bool Created;}
        // merge only native rectangular materials. foreign factories still receive one cell
        private static bool Merge(IBlockFactory factory,Color c)
        {return factory.GetType()==typeof(BaseBlockFactory)&&c!=Color.Red;}
        private static IBlockFactory Choose(List<IBlockFactory> factories,Dictionary<uint,IBlockFactory> choices,Color code,JumpKing.Workshop.Level level)
        {
            IBlockFactory factory;
            if(!choices.TryGetValue(code.PackedValue,out factory))
            {
                factory=factories.FirstOrDefault(f=>f.CanMakeBlock(code,level));
                if(factory!=null&&factory.GetType()==typeof(BaseBlockFactory)&&ReferenceEquals(factory,factories[0]))choices[code.PackedValue]=factory;
            }
            return factory;
        }
        private static bool Blocks(LevelTexture p_src,JumpKing.Workshop.Level level,int p_screen,ref bool wind_enabled,ref TeleportLink[] teleport,ref float wind_int,ref bool? wind_direction,ref IBlock[] __result)
        {
            var p=p_src as PixelLevelTexture;if(p==null)return true;
            wind_enabled=false;wind_int=0;wind_direction=null;teleport=new[]{new TeleportLink(),new TeleportLink()};
            int width=p.Layout.Width(p_screen),height=p.Layout.Height(p_screen),scale=480/width;
            var factories=(List<IBlockFactory>)Factories.GetValue(null);
            var choices=new Dictionary<uint,IBlockFactory>();var runs=new List<Run>();
            var previous=new Dictionary<string,Run>();
            for(int y=0;y<height;y++)
            {
                var current=new Dictionary<string,Run>();
                bool pending=false;IBlockFactory nextFactory=null;
                for(int x=0;x<width;x++)
                {
                    Color c=p.Sample(p_screen,x,y);if(c.A==0)continue;
                    var alias=p.Shapes.Find(c);
                    if(alias!=null)
                    {
                        if(factories.Any(f=>f.CanMakeBlock(c,level)))throw new InvalidDataException("Collision slope alias already owned by a block factory: "+c);
                        var sourceFactory=Choose(factories,choices,alias.Source,level);
                        if(sourceFactory==null)throw new InvalidDataException("Install the source block mod for collision slope "+c);
                        if(scale!=8 && sourceFactory.GetType()!=typeof(BaseBlockFactory) && !Api.PixelCollisionFactories.Supports(sourceFactory.GetType()))
                            throw new NotSupportedException("Slope source factory hasn't declared pixel collision support: "+sourceFactory.GetType().FullName);
                        var bounds=new Rectangle(x*scale,(y-p_screen*height)*scale,scale,scale);
                        var sourceBlock=p.Shapes.Create(sourceFactory,alias,bounds,level,p_src,p_screen,x,y);
                        foreach(var block in SlopeClipping.Apply(sourceBlock,alias.Shape))runs.Add(new Run{Block=block,Created=true,Bounds=bounds});
                        if(runs.Count>32768)throw new InvalidDataException("Too many collision rectangles and slopes");
                        continue;
                    }
                    IBlockFactory factory=pending?nextFactory:Choose(factories,choices,c,level);pending=false;
                    if(factory==null)
                    {
                        if(c.G==0&&c.B==255){teleport[x>=width/2?1:0]=new TeleportLink(c.R);continue;}
                        if(c.G==255&&c.B==0)
                        {
                            wind_enabled=true;
                            if(c.R>191&&c.R<208)wind_int=c.R-191;
                            if(c.R>207&&c.R<224){wind_int=c.R-207;wind_direction=true;}
                            if(c.R>223&&c.R<240){wind_int=c.R-223;wind_direction=false;}
                            continue;
                        }
                        if(c==new Color(128,128,128))continue;
                        throw new InvalidDataException("Unsupported collision colour #"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2")+" on screen "+(p_screen+1)+" at "+x+","+y+". Install its block mod or correct level+.png.");
                    }
                    int start=x;
                    if(scale!=8&&factory.GetType()!=typeof(BaseBlockFactory)&&!Api.PixelCollisionFactories.Supports(factory.GetType()))
                        throw new NotSupportedException("Block factory "+factory.GetType().FullName+" has not declared pixel collision support. Use 60x45 cells or ask its author to register with Mega Mapping Expansion.");
                    bool merge=Merge(factory,c);
                    if(merge)while(x+1<width&&p.Sample(p_screen,x+1,y)==c)
                    {
                        // an earlier foreign factory can change its answer for the next cell
                        if(!ReferenceEquals(factory,factories[0]))
                        {
                            nextFactory=Choose(factories,choices,c,level);
                            if(!ReferenceEquals(factory,nextFactory)){pending=true;break;}
                        }
                        x++;
                    }
                    string key=start+":"+(x-start+1)+":"+c.PackedValue;
                    Run run;
                    if(merge&&previous.TryGetValue(key,out run))run.Bounds.Height+=scale;
                    else
                    {
                        run=new Run{Code=c,Bounds=new Rectangle(start*scale,(y-p_screen*height)*scale,(x-start+1)*scale,scale),Factory=factory,X=start,Y=y};runs.Add(run);
                        if(runs.Count>32768)throw new InvalidDataException("Screen "+(p_screen+1)+" exceeds 32768 collision rectangles. Simplify tiny isolated cells or slopes.");
                        // foreign factories keep native CanMakeBlock/GetBlock interleaving
                        if(factory.GetType()!=typeof(BaseBlockFactory))
                        {run.Block=JKRuntime.Geometry.NativeWorldGeometry.CreateBlock(factory,c,run.Bounds,level,p_src,p_screen,start,y);run.Created=true;}
                    }
                    if(merge)current.Add(key,run);
                }
                previous=current;
            }
            var result=new List<IBlock>();
            p.BlockCount+=runs.Count;
            if(p.BlockCount>131072)throw new InvalidDataException("Map exceeds 131072 collision rectangles. Simplify isolated cells or split the map.");
            foreach(var run in runs)
            {
                var block=run.Created?run.Block:JKRuntime.Geometry.NativeWorldGeometry.CreateBlock(run.Factory,run.Code,run.Bounds,level,p_src,p_screen,run.X,run.Y);
                if(block!=null)result.Add(block);
            }
            __result=result.ToArray();return false;
        }
    }
}
