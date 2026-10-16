using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using JumpKing;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static readonly MethodInfo PixelBlocks=typeof(LevelManager).GetMethod("LoadBlocksInterval",BindingFlags.Static|BindingFlags.NonPublic);
        private static IBlock[] ReadPixelBlocks(LevelTexture texture,int screen)
        {object[] values={texture,null,screen,false,null,0f,null};return (IBlock[])PixelBlocks.Invoke(null,values);}
        private static void PixelCollisionChecks(string scratch)
        {
            PixelCollision.Install();
            foreach(int width in new[]{60,120,240,480})foreach(string source in new[]{"atlas","strip"})
            {
                int height=width*3/4,scale=480/width;
                var layout=new MapLayout{Version=2,Screens=3,Columns=source=="strip"?1:2,Rows=source=="strip"?3:2,CellWidth=width,CellHeight=height,Source=source};
                layout.ValidatePixel();var data=new Color[layout.ImageWidth*layout.ImageHeight];
                for(int s=0;s<3;s++)
                {
                    int ox,oy;layout.Origin(s,out ox,out oy);
                    data[(oy+3)*layout.ImageWidth+ox+2]=Color.Black;
                    for(int y=6;y<9;y++)for(int x=5;x<10;x++)data[(oy+y)*layout.ImageWidth+ox+x]=Color.Cyan;
                }
                var texture=new PixelLevelTexture(layout,data);
                Require(texture.GetTotalScreensPotential()==3,"pixel sampler returns exact content count without square padding");
                for(int s=0;s<3;s++)
                {
                    Require(texture.GetColor(s,2,3)==Color.Black,"native sampler respects atlas and bottom-up strip order");
                    var blocks=ReadPixelBlocks(texture,s);
                    Require(blocks.Length==2&&blocks.Any(b=>b.GetRect()==new Rectangle(5*scale,6*scale-s*360,5*scale,3*scale)),"native materials merge into exact screen-local rectangles");
                    var screen=new LevelScreen(s,blocks,new LevelScreen.Graphics(),false,new[]{new TeleportLink(),new TeleportLink()},0,null);
                    Rectangle overlap;AdvCollisionInfo info;
                    Require(screen.TryCollision(new Rectangle(2*scale,3*scale-s*360,1,1),out overlap,out info),"isolated pixel reaches native collision query");
                    Require(!screen.TryCollision(new Rectangle(3*scale,3*scale-s*360,1,1),out overlap,out info),"empty neighbour stays empty");
                    Require(screen.TryCollision(new Rectangle(5*scale,6*scale-s*360,1,1),out overlap,out info)&&info.Ice,"merged native ice keeps its material");
                }
                Require(texture.GetColor(0,2,-height+3)==Color.Black&&texture.GetColor(1,2,height+3)==Color.Black,"vertical neighbour sampling crosses logical screens");
                Require(texture.GetColor(2,2,-1)==Color.Transparent&&texture.GetColor(0,2,height)==Color.Transparent,"sampling beyond content stays empty");
            }
            var native=LevelTexture.FromDimensions(60,45);
            Require(native.GetTotalScreensPotential()==1&&ReadPixelBlocks(native,0).Length==0,"dormant pixel hooks leave legacy sampler and native loader intact");
            PixelMaterials();
            MixedPixelScreens();
            string folder=Path.Combine(scratch,"pixel-layout-test");Directory.CreateDirectory(Path.Combine(folder,"props/mega-mapping-expansion"));
            string xml=Path.Combine(folder,MapLayout.RelativePath);
            WriteLayout(xml,"<MapLayout version='2' screens='260' columns='20' rows='13' cellWidth='480' cellHeight='360' source='atlas'><SideLink screen='1' side='right' target='260'/></MapLayout>");
            var parsed=MapLayout.Read(folder);parsed.ValidateAtlas(9600,4680);Require(parsed.Links[0][1]==260,"pixel atlas supports links above 255");
            foreach(string bad in new[]{"columns='19'","cellWidth='481'","source='other'"})
            {
                string good=File.ReadAllText(xml);string broken=bad.StartsWith("columns")?good.Replace("columns='20'",bad):bad.StartsWith("cellWidth")?good.Replace("cellWidth='480'",bad):good.Replace("source='atlas'",bad);
                File.WriteAllText(xml,broken);bool refused=false;try{MapLayout.Read(folder);}catch(InvalidDataException){refused=true;}
                Require(refused,"invalid pixel layout is rejected before allocating pixels");File.WriteAllText(xml,good);
            }
            var small=new MapLayout{Version=2,Screens=1,Columns=1,Rows=1,CellWidth=60,CellHeight=45};
            string xnb=Path.Combine(folder,"level+.xnb");WritePixelXnb(xnb,small);
            int countRead=0;CollisionXnb.Read(xnb,small,(reader,count)=>{for(int i=0;i<count;i++){Require(CollisionXnb.Colour(reader.ReadUInt32())==0xff000000,"CPU XNB preserves opaque collision colours");countRead++;}});
            Require(countRead==2700,"CPU XNB consumes exact payload");
            var nativeLayout=new MapLayout{Version=2,Screens=3,Columns=2,Rows=2,CellWidth=60,CellHeight=45};
            WritePixelXnb(xnb,nativeLayout);NativePixelScreens(folder,nativeLayout);WritePixelXnb(xnb,small);
            byte[] original=File.ReadAllBytes(xnb);foreach(int offset in new[]{5,6,original.Length-1})
            {
                byte[] bad=(byte[])original.Clone();bad[offset]^=1;File.WriteAllBytes(xnb,bad);bool refused=false;
                try{CollisionXnb.Read(xnb,small,(r,n)=>{for(int i=0;i<n;i++)CollisionXnb.Colour(r.ReadUInt32());});}catch(InvalidDataException){refused=true;}
                Require(refused,"bad compression, length or alpha is rejected");
            }
            Console.WriteLine("[OK] Pixel collision: four resolutions, both layouts, exact screen count, native rectangles/materials, CPU XNB and legacy fallback");
        }
        private static void WritePixelXnb(string path,MapLayout layout)
        {
            using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream,Encoding.UTF8))
            {
                writer.Write(new byte[]{88,78,66,119,5,0});writer.Write(0);writer.Write((byte)1);
                writer.Write("Microsoft.Xna.Framework.Content.Texture2DReader");writer.Write(0);writer.Write((byte)0);writer.Write((byte)1);writer.Write(0);
                writer.Write(layout.ImageWidth);writer.Write(layout.ImageHeight);writer.Write(1);writer.Write(layout.ImageWidth*layout.ImageHeight*4);
                for(int i=0;i<layout.ImageWidth*layout.ImageHeight;i++)writer.Write(0xff000000u);
                writer.Flush();int size=(int)stream.Length;stream.Position=6;writer.Write(size);
            }
        }
        private static void NativePixelScreens(string root,MapLayout layout,int probeX=1,int probeY=1,bool detailed=false)
        {
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var gameField=typeof(Game1).GetField("_instance",flags);object oldGame=gameField.GetValue(null);
            var countField=typeof(LevelManager).GetField("_total_screens",flags);object oldCount=countField.GetValue(null);
            var screensField=typeof(LevelManager).GetField("m_screens",flags);object oldScreens=screensField.GetValue(null);
            var oldWeather=WeatherManager.instance;
            var settingField=typeof(MappingSettings).GetField("store",flags);object oldSettings=settingField.GetValue(null);
            var disabled=new MappingSettingsStore(Path.Combine(root,"disabled-settings.xml"));disabled.Set(false);settingField.SetValue(null,disabled);
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));GC.SuppressFinalize(game);
            game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
            foreach(var field in typeof(JKContentManager).GetFields(BindingFlags.Instance|BindingFlags.NonPublic))
                if(field.FieldType.IsGenericType&&field.FieldType.GetGenericTypeDefinition()==typeof(System.Collections.Generic.Dictionary<,>))field.SetValue(game.contentManager,Activator.CreateInstance(field.FieldType));
            game.contentManager.root=Path.GetFullPath(root);gameField.SetValue(null,game);countField.SetValue(null,0);
            WeatherManager.Load(new WeatherManager.WeatherEffect{weathers=new WeatherManager.Weather[0]});
            Directory.CreateDirectory(Path.Combine(root,"screens/scrolling"));
            WriteLayout(Path.Combine(root,MapLayout.RelativePath),"<MapLayout version='2' screens='"+layout.Screens+"' columns='"+layout.Columns+"' rows='"+layout.Rows+"' cellWidth='"+layout.CellWidth+"' cellHeight='"+layout.CellHeight+"' source='"+layout.Source+"'>"+(layout.Screens==3?"<SideLink screen='1' side='right' target='3'/>":"")+"</MapLayout>");
            try
            {
                PixelCollision.Install();LevelManager.LoadScreens();
                Require(LevelManager.TotalScreens==layout.Screens,"real native LoadScreens consumes CPU collision and exact count without a GPU texture");
                Require(!MappingSettings.Enabled,"native collision construction works with Mapping visuals disabled");
                var screens=(LevelScreen[])screensField.GetValue(null);
                for(int i=0;i<screens.Length;i++)
                {
                    Rectangle overlap;AdvCollisionInfo info;
                    Require(screens[i].TryCollision(new Rectangle(probeX,probeY-i*360,1,1),out overlap,out info),"native screen construction keeps collision on every logical screen");
                    if(detailed)
                    {
                        Require(!screens[i].TryCollision(new Rectangle(probeX+1,probeY-i*360,1,1),out overlap,out info),"native loaded atlas keeps the adjacent colour-key pixel empty");
                        Require(screens[i].TryCollision(new Rectangle(210,201-i*360,1,1),out overlap,out info)&&info.Ice,"native loaded atlas keeps ice on every logical screen");
                    }
                }
                NativeMapLayout.BeginRun(MapLayout.Read(root));
                if(layout.Screens==3)Require(screens[0].teleport[1].GetIndex1()==3,"exact-count native screen arrays accept integer side links");
                NativeMapLayout.Release();
                if(layout.Screens==3)Require(!screens[0].teleport[1].IsEnabled,"unload restores owned side links on exact-count screens");
            }
            finally
            {NativeMapLayout.Release();gameField.SetValue(null,oldGame);countField.SetValue(null,oldCount);screensField.SetValue(null,oldScreens);WeatherManager.instance=oldWeather;settingField.SetValue(null,oldSettings);}
        }
        private static void PixelCompiledAtlasChecks(string scratch,string compiled,string provider)
        {
            if(provider!=null)new HarmonyLib.Harmony("pixel.tests.installed-sizes").PatchAll(Assembly.LoadFrom(provider));
            string root=Path.Combine(scratch,"compiled-pixel-native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"props/mega-mapping-expansion"));
            File.Copy(Path.Combine(compiled,MapLayout.RelativePath),Path.Combine(root,MapLayout.RelativePath));File.Copy(Path.Combine(compiled,"level+.xnb"),Path.Combine(root,"level+.xnb"));
            NativePixelScreens(root,MapLayout.Read(root),17,23,true);
            Console.WriteLine("[OK] Compiled pixel atlas loaded through native LoadScreens; checks="+checks+"; More Block Sizes="+(provider!=null));
        }
        private sealed class PixelTestFactory:JumpKing.API.IBlockFactory
        {
            internal static readonly Color Code=new Color(12,34,56);
            internal int Queries,Created;
            public bool CanMakeBlock(Color colour,JumpKing.Workshop.Level level)
            {Require(Queries==Created,"foreign factory discovery remains interleaved with construction");Queries++;return colour==Code;}
            public bool IsSolidBlock(Color colour){return colour==Code;}
            public IBlock GetBlock(Color colour,Rectangle bounds,JumpKing.Workshop.Level level,LevelTexture source,int screen,int x,int y)
            {Created++;Require(source.GetColor(screen,x,y)==Code,"opted-in custom factories receive source coordinates at declared resolution");return new BoxBlock(bounds);}
        }
        private sealed class AlternatingPixelFactory:JumpKing.API.IBlockFactory
        {
            internal int Queries,Created;
            internal bool Alternating;
            public bool CanMakeBlock(Color colour,JumpKing.Workshop.Level level)
            {if(colour!=Color.Black)return false;Queries++;return Alternating&&Queries%2==0;}
            public bool IsSolidBlock(Color colour){return colour==Color.Black;}
            public IBlock GetBlock(Color colour,Rectangle bounds,JumpKing.Workshop.Level level,LevelTexture source,int screen,int x,int y)
            {Created++;return new BoxBlock(bounds);}
        }
        private static void PixelMaterials()
        {
            foreach(int width in new[]{60,480})
            {
                int height=width*3/4,scale=480/width;
                var layout=new MapLayout{Version=2,Screens=1,Columns=1,Rows=1,CellWidth=width,CellHeight=height};
                var codes=new[]{Color.Black,Color.Cyan,Color.Yellow,new Color(255,106,0),Color.White,new Color(0,170,170),new Color(182,255,0)};
                var types=new[]{typeof(BoxBlock),typeof(IceBlock),typeof(SnowBlock),typeof(SandBlock),typeof(NoWindBlock),typeof(WaterBlock),typeof(QuarkBlock)};
                var data=new Color[width*height];
                for(int i=0;i<codes.Length;i++)data[width*4+i*2+2]=codes[i];
                var blocks=ReadPixelBlocks(new PixelLevelTexture(layout,data),0);
                Require(blocks.Select(b=>b.GetType()).SequenceEqual(types),"all seven native rectangular materials keep native block types");
                for(int orientation=0;orientation<4;orientation++)
                {
                    data=new Color[width*height];data[20*width+20]=Color.Red;
                    data[20*width+20+(orientation%2==0?1:-1)]=Color.Black;
                    data[(20+(orientation<2?1:-1))*width+20]=Color.Black;
                    var slope=ReadPixelBlocks(new PixelLevelTexture(layout,data),0).OfType<SlopeBlock>().Single();
                    var actual=(SlopeType)typeof(SlopeBlock).GetField("m_type",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(slope);
                    var expected=new[]{SlopeType.TopLeft,SlopeType.TopRight,SlopeType.BottomLeft,SlopeType.BottomRight};
                    Require(actual==expected[orientation],"native slope orientation uses resolution-aware neighbour sampling");
                    Require(slope.GetRect().Width==scale,"red slope is one declared cell, never merged");
                }
            }
            var list=(System.Collections.Generic.List<JumpKing.API.IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            var factory=new PixelTestFactory();list.Add(factory);
            try
            {
                var layout=new MapLayout{Version=2,Screens=1,Columns=1,Rows=1,CellWidth=480,CellHeight=360};var data=new Color[480*360];data[7*480+9]=PixelTestFactory.Code;
                bool refused=false;try{ReadPixelBlocks(new PixelLevelTexture(layout,data),0);}catch(TargetInvocationException error){refused=error.InnerException is NotSupportedException;}
                Require(refused,"unreviewed custom factories cannot silently assume eight-pixel cells");
                Api.PixelCollisionFactories.Register(typeof(PixelTestFactory));
                factory.Queries=factory.Created=0;data[7*480+10]=PixelTestFactory.Code;data[7*480+11]=PixelTestFactory.Code;
                var blocks=ReadPixelBlocks(new PixelLevelTexture(layout,data),0);
                Require(blocks.Length==3&&blocks[0].GetRect()==new Rectangle(9,7,1,1)&&factory.Queries==3&&factory.Created==3,"opted-in custom factory receives exact cells without merging or cached discovery");
            }
            finally{list.Remove(factory);}
            var alternating=new AlternatingPixelFactory();list.Insert(0,alternating);
            try
            {
                Api.PixelCollisionFactories.Register(typeof(AlternatingPixelFactory));
                var layout=new MapLayout{Version=2,Screens=1,Columns=1,Rows=1,CellWidth=480,CellHeight=360};var data=new Color[480*360];
                for(int x=9;x<13;x++)data[7*480+x]=Color.Black;
                var merged=ReadPixelBlocks(new PixelLevelTexture(layout,data),0);
                Require(merged.Length==1&&merged[0].GetRect()==new Rectangle(9,7,4,1)&&alternating.Queries==4,"native merging still queries an earlier foreign factory for every cell");
                alternating.Queries=0;alternating.Alternating=true;
                var split=ReadPixelBlocks(new PixelLevelTexture(layout,data),0);
                Require(split.Length==4&&alternating.Queries==4&&alternating.Created==2,"a changing foreign factory wins its cells without duplicate discovery calls");
            }
            finally{list.Remove(alternating);}
        }
        private static void MixedPixelScreens()
        {
            var layout=new MapLayout{Version=2,Screens=2,Columns=1,Rows=2,CellWidth=480,CellHeight=360};
            layout.Resolutions.Add(0,60);layout.ValidatePixel();
            var data=new Color[480*720];
            for(int y=24;y<32;y++)for(int x=16;x<24;x++)data[y*480+x]=Color.Black;
            data[(360+352)*480+16]=Color.Black;data[(360+23)*480+17]=Color.Cyan;
            var texture=new PixelLevelTexture(layout,data);
            Require(texture.GetColor(0,2,3)==Color.Black&&texture.GetColor(1,17,23)==Color.Cyan,"mixed-resolution screens share one unmodified atlas grid");
            Require(texture.GetCoord(0,2,3)==new Point(16,24),"instance coordinates locate scaled coarse cells in the physical atlas");
            Require(texture.GetColor(0,2,-1)==Color.Black&&texture.GetColor(1,17,384)==Color.Black,"mixed-resolution vertical neighbours use world-pixel alignment");
            Require(ReadPixelBlocks(texture,0).Single().GetRect()==new Rectangle(16,24,8,8),"coarse screen keeps its original eight-pixel geometry");
            Require(ReadPixelBlocks(texture,1).Any(b=>b.GetRect()==new Rectangle(17,23-360,1,1)),"fine screen in the same image keeps one-pixel geometry");
            data[25*480+17]=Color.Cyan;bool refused=false;
            try{new PixelLevelTexture(layout,data);}catch(InvalidDataException){refused=true;}
            Require(refused,"coarse declaration cannot silently discard detail inside a scaled cell");
        }
        private static void PixelCompatibilityChecks(string scratch,string provider,bool foreignFirst)
        {
            var gameField=typeof(Game1).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic);object old=gameField.GetValue(null);
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));GC.SuppressFinalize(game);
            game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));gameField.SetValue(null,game);
            string root=Path.Combine(scratch,"pixel-mbs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"props/mega-mapping-expansion"));
            game.contentManager.root=root;
            try
            {
                Assembly foreign=null;
                var foreignHarmony=new HarmonyLib.Harmony("pixel.tests.more-block-sizes");
                if(foreignFirst){foreign=Assembly.LoadFrom(provider);foreignHarmony.PatchAll(foreign);}
                PixelCollision.Install();
                if(!foreignFirst){foreign=Assembly.LoadFrom(provider);foreignHarmony.PatchAll(foreign);PixelCollision.Install();}
                var prefix=foreign.GetType("MoreBlockSizes.Patches.PatchLoadScreens",true).GetMethod("Prefix",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                WriteLayout(Path.Combine(root,MapLayout.RelativePath),"<MapLayout version='2' screens='1' columns='1' rows='1' cellWidth='480' cellHeight='360' source='atlas'/>");
                File.WriteAllBytes(Path.Combine(root,"level+.xnb"),new byte[]{1});
                Require((bool)prefix.Invoke(null,null),"MBS prefix is skipped for v2 before it can load a GPU texture");
                File.Delete(Path.Combine(root,MapLayout.RelativePath));File.Delete(Path.Combine(root,"level+.xnb"));
                Require((bool)prefix.Invoke(null,null),"MBS prefix retains the legacy no-override path");
                PixelCollisionChecks(scratch);
            }
            finally{gameField.SetValue(null,old);}
        }
    }
}
