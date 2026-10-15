using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static class ConstructionGraphics
    {
        private const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private sealed class DeviceService:IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice{get;set;}
            public event EventHandler<EventArgs> DeviceCreated{add{}remove{}}
            public event EventHandler<EventArgs> DeviceDisposing{add{}remove{}}
            public event EventHandler<EventArgs> DeviceReset{add{}remove{}}
            public event EventHandler<EventArgs> DeviceResetting{add{}remove{}}
        }
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
        private static int Main(string[] args)
        {
            try{Run(Path.GetFullPath(args[0]),args[1],args[2]);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        private static void Run(string root,string gameDir,string exampleDir)
        {
            Directory.CreateDirectory(root);
            using(var image=new System.Drawing.Bitmap(64,32))
            {
                for(int y=0;y<32;y++)for(int x=0;x<64;x++)image.SetPixel(x,y,x<32?System.Drawing.Color.FromArgb(255,48,196,128):System.Drawing.Color.FromArgb(255,220,80,64));
                image.Save(Path.Combine(root,"surface.png"),System.Drawing.Imaging.ImageFormat.Png);
            }
            using(var image=new System.Drawing.Bitmap(9,9))
            {for(int y=0;y<9;y++)for(int x=0;x<9;x++)image.SetPixel(x,y,System.Drawing.Color.Yellow);image.Save(Path.Combine(root,"stamp.png"),System.Drawing.Imaging.ImageFormat.Png);}
            using(var image=new System.Drawing.Bitmap(480,360))
            {for(int y=100;y<109;y++)for(int x=100;x<=y;x++)image.SetPixel(x,y,System.Drawing.Color.FromArgb(255,32,180,80));image.Save(Path.Combine(root,"mask.png"),System.Drawing.Imaging.ImageFormat.Png);}
            using(var image=new System.Drawing.Bitmap(2,1))
            {image.SetPixel(0,0,System.Drawing.Color.White);image.Save(Path.Combine(root,"window.png"),System.Drawing.Imaging.ImageFormat.Png);}
            using(var window=new Form{ShowInTaskbar=false})
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters{DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360}))
            using(var target=new RenderTarget2D(device,480,360))
            using(var white=new Texture2D(device,1,1))
            using(var glyph=new Texture2D(device,9,9))
            using(var batch=new SpriteBatch(device))
            {
                var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),new DeviceService{GraphicsDevice=device});
                var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));typeof(Game1).GetField("_instance",All).SetValue(null,game);typeof(Game).GetField("_services",All).SetValue(game,services);
                foreach(var field in typeof(Game).GetFields(All).Where(f=>f.FieldType==typeof(IGraphicsDeviceService)))field.SetValue(game,services.GetService(typeof(IGraphicsDeviceService)));
                game.contentManager=new JKContentManager();Game1.spriteBatch=batch;
                NativeAdapterGraphics.Run(game,root,gameDir,exampleDir);
                glyph.SetData(Enumerable.Repeat(Color.White,81).ToArray());game.contentManager.font=new JKContentManager.Font{MenuFont=new SpriteFont(glyph,new System.Collections.Generic.List<Rectangle>{new Rectangle(0,0,9,9)},new System.Collections.Generic.List<Rectangle>{new Rectangle(0,0,9,9)},new System.Collections.Generic.List<char>{'X'},9,0,new System.Collections.Generic.List<Vector3>{new Vector3(0,9,0)},'X')};
                var pixel=(PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture));GC.SuppressFinalize(pixel);white.SetData(new[]{Color.White});typeof(PixelTexture).GetField("_texture",All).SetValue(pixel,white);game.contentManager.Pixel=pixel;
                var xml=System.Xml.Linq.XDocument.Parse("<MappingSettings version='1'><Assets><Asset id='surface' path='surface.png'/><Asset id='stamp' path='stamp.png'/><Asset id='mask' path='mask.png'/></Assets><Components><Component id='mark' kind='stamp'><Value name='asset' value='stamp'/><Value name='mask' value='mask'/></Component></Components><Instances><Instance id='orb' recipe='planet'><Arg name='surface' value='surface'/><Arg name='size' value='160,160'/><Arg name='rate' value='0.5'/></Instance></Instances></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(xml)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    device.SetRenderTarget(target);device.Clear(new Color(8,12,24));scene.Tick(.01,.01);renderer.Tick();game.StartBatch();renderer.DrawLayer("background",1);game.EndBatch();
                    var pixels=new Color[480*360];target.GetData(pixels);
                    Check(pixels[180*480+240]!=new Color(8,12,24),"PNG material renders through a general spherical mesh projection");
                    Check(pixels[20*480+20]==new Color(8,12,24),"Projection leaves the area outside its geometry intact");
                    Color before=pixels[180*480+240];using(var file=File.Create(Path.Combine(root,"projection-before.png")))target.SaveAsPng(file,480,360);
                    scene.Tick(1,1);renderer.Tick();device.Clear(new Color(8,12,24));game.StartBatch();renderer.DrawLayer("background",1);game.EndBatch();target.GetData(pixels);
                    Check(pixels[180*480+240]!=before,"UV phase binding rotates the PNG without a semantic planet renderer");
                    using(var file=File.Create(Path.Combine(root,"projection-after.png")))target.SaveAsPng(file,480,360);
                    renderer.HandleAction(new NativeSceneAction{Operation="stamp",Target="mark",Event=new MegaMappingExpansion.Api.SceneEventSample("hit","wall","player",1,1,scene.GameplayTime,
                        new[]{new MegaMappingExpansion.Api.SignalSample("position",MegaMappingExpansion.Api.SceneValue.Vector(104,104),MegaMappingExpansion.Api.ObservationQuality.Sampled,1)})});
                    game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Check(pixels[106*480+102].R>200&&pixels[106*480+102].G>200,"GPU stamp masking uses alpha without tinting the PNG by mask RGB");
                    Check(pixels[102*480+106]==new Color(8,12,24),"GPU stamp remains outside the empty half of a slope mask");
                    using(var file=File.Create(Path.Combine(root,"masked-stamp.png")))target.SaveAsPng(file,480,360);
                }
                var pixelScene=System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'><Assets><Asset id='surface' path='surface.png'/></Assets><Components>
<Component id='pose' kind='transform'><Value name='position' value='80.3,100.6'/><Value name='scale' value='1.15,1.07'/><Value name='rotation' value='13.3'/></Component>
<Component id='material' kind='material'><Value name='sampling' value='linear'/></Component>
<Component id='image' kind='sprite'><Value name='transform' value='pose'/><Value name='asset' value='surface'/><Value name='material' value='material'/><Value name='origin' value='0.3,0.7'/></Component>
<Component id='mesh-pose' kind='transform'><Value name='position' value='340,100'/></Component><Component id='static' kind='mesh'><Value name='transform' value='mesh-pose'/><Value name='asset' value='surface'/><Value name='size' value='64,32'/></Component>
</Components></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(pixelScene)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    var pixels=new Color[480*360];device.SetRenderTarget(target);device.Clear(new Color(8,12,24));scene.Tick(.01,.01);renderer.Tick();game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Func<Color,bool> palette=p=>p==new Color(8,12,24)||p==new Color(48,196,128)||p==new Color(220,80,64);
                    Check(pixels.All(palette),"Default pixel policy preserves the PNG palette through fractional origin, scale and rotation");
                    scene.Properties.Set(new PropertyContribution{Id="smooth",Owner="test",Target="pose/pixelSnap",Mode="set",Value=MegaMappingExpansion.Api.SceneValue.Boolean(false)});scene.Properties.Compose();
                    renderer.Tick();device.Clear(new Color(8,12,24));game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Check(pixels.Any(p=>!palette(p)),"Linear subpixel rendering requires an explicit pixelSnap=false opt-in");
                    Check(renderer.MeshVertexUpdates==0,"An unchanged mesh reuses prepared vertices on the next frame");
                }
                var nativeLabel=System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'><Assets><Asset id='mask' path='mask.png'/></Assets><Components>
<Component id='pose' kind='transform'><Value name='position' value='104.3,104.7'/></Component>
<Component id='material' kind='material'><Value name='mask' value='mask'/><Value name='maskSpace' value='screen'/></Component>
<Component id='label' kind='text'><Value name='transform' value='pose'/><Value name='material' value='material'/><Value name='value' value='X'/></Component>
</Components></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(nativeLabel)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    var pixels=new Color[480*360];device.SetRenderTarget(target);device.Clear(new Color(8,12,24));scene.Tick(.01,.01);renderer.Tick();game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Check(pixels[106*480+102]==Color.White&&pixels[102*480+106]==new Color(8,12,24),"Native font text uses the same alpha-mask material path as PNG sprites");
                }
                var reveal=System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'><Variables><Variable id='count' kind='Number' value='1'/></Variables>
<Components><Component id='pose' kind='transform'><Value name='position' value='240.3,180.7'/></Component>
<Component id='label' kind='text'><Value name='transform' value='pose'/><Value name='value' value='XX'/></Component></Components>
<Graph><Node id='count' op='input' source='variable:count' units='characters'/></Graph><Bindings><Bind id='show' source='count' target='label/visibleCharacters'/></Bindings></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(reveal)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    var pixels=new Color[480*360];var full=new Color[480*360];
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);scene.Tick(.01,.01);renderer.Tick();game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    scene.SetVariable("count",MegaMappingExpansion.Api.SceneValue.Number(2));scene.Tick(.01,.01);renderer.Tick();device.Clear(Color.Transparent);game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(full);
                    Check(pixels.Count(p=>p.A>0)==81&&full.Count(p=>p.A>0)==162,"Native text reveals exactly one glyph at a time");
                    Check(pixels.Select((p,i)=>p.A==0||p==full[i]).All(v=>v),"Revealing a centered label keeps previously visible glyphs fixed");
                    Check(full.All(p=>p.A==0||p.A==255),"Default text reveal preserves whole opaque pixels at fractional authored positions");
                    using(var file=File.Create(Path.Combine(root,"text-reveal.png")))target.SaveAsPng(file,480,360);
                }
                var streamScene=System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'><Assets><Asset id='mote' path='stamp.png'/></Assets><Components>
<Component id='pose' kind='transform'><Value name='position' value='240,300'/></Component>
<Component id='stream' kind='emitter'><Value name='transform' value='pose'/><Value name='asset' value='mote'/><Value name='rate' value='12'/><Value name='lifetime' value='3'/><Value name='area' value='40,0'/><Value name='shape' value='line'/><Value name='velocity' value='0,-80'/><Value name='clock' value='presentation'/></Component>
</Components></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(streamScene)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    var pixels=new Color[480*360];device.SetRenderTarget(target);device.Clear(new Color(8,12,24));scene.Tick(0,1);renderer.Tick();game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Check(pixels.Where((p,i)=>i/480<290&&p.R>100).Any(),"Presentation-clock particles travel upward while the gameplay clock is held");
                    Check(renderer.QuadDrawCalls==1,"Compatible particles share one GPU draw instead of one draw per particle");
                    var snapshot=scene.Capture();var original=(Color[])pixels.Clone();
                    for(int i=0;i<3;i++)
                    {
                        scene.Tick(0,.2);scene.Restore(snapshot);renderer.ClearTransient();renderer.Tick();device.Clear(new Color(8,12,24));game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                        Check(pixels.SequenceEqual(original),"Continuous particles reconstruct exactly after restore "+(i+1));
                    }
                    scene.Tick(0,.2);renderer.Tick();device.Clear(new Color(8,12,24));game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Check(!pixels.SequenceEqual(original),"Reconstructed particles keep advancing after restoration");
                    using(var file=File.Create(Path.Combine(root,"restored-updraft.png")))target.SaveAsPng(file,480,360);
                }
                var stress=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(streamScene);
                var particleDefinition=stress.Components.First(c=>c.Kind=="emitter");
                foreach(var value in particleDefinition.Values)if(value.Name=="rate")value.Value="4096";
                particleDefinition.Values=particleDefinition.Values.Concat(new[]{new ComponentValueData{Name="capacity",Value="4096"}}).ToArray();
                using(var scene=new SceneProgram(stress))using(var renderer=new NativeRenderer(scene,device,root))
                {
                    scene.Tick(0,1);var times=new double[40];bool batched=true;
                    for(int frame=0;frame<45;frame++)
                    {
                        var watch=System.Diagnostics.Stopwatch.StartNew();scene.Tick(0,1.0/60);renderer.Tick();device.SetRenderTarget(target);device.Clear(new Color(8,12,24));game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();watch.Stop();
                        batched&=renderer.QuadDrawCalls==1;
                        if(frame>=5)times[frame-5]=watch.Elapsed.TotalMilliseconds;
                    }
                    Check(batched,"4096 steady particles stay within one prepared quad batch across advancing frames");
                    Array.Sort(times);string report="4096 PNG particles, 480x360 target; CPU update and submission, excluding readback/present. Median="+times[20].ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" ms; p95="+times[37].ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" ms; quad draws=1.\n";
                    File.WriteAllText(Path.Combine(root,"performance.txt"),report);Console.WriteLine(report);
                }
                var surfaces=System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'>
<Assets><Asset id='surface' path='surface.png'/><Asset id='font' path='surface.png' columns='2'/><Asset id='window' path='window.png'/></Assets>
<Components>
<Component id='source-left' kind='transform'><Value name='position' value='80,100'/></Component>
<Component id='source-right' kind='transform'><Value name='position' value='300,100'/></Component>
<Component id='left' kind='sprite'><Value name='transform' value='source-left'/><Value name='asset' value='surface'/></Component>
<Component id='right' kind='sprite'><Value name='transform' value='source-right'/><Value name='asset' value='surface'/></Component>
<Component id='source' kind='capture'><Value name='selection' value='left;right'/></Component>
<Component id='mirror-mat' kind='material'><Value name='capture' value='source'/><Value name='mask' value='window'/></Component>
<Component id='shadow-mat' kind='material'><Value name='capture' value='source'/><Value name='color' value='#3040FF'/></Component>
<Component id='plane' kind='transform'><Value name='position' value='0,180'/></Component>
<Component id='mirror' kind='receiver'><Value name='transform' value='plane'/><Value name='material' value='mirror-mat'/><Value name='mode' value='reflection'/><Value name='opacity' value='1'/><Value name='projection' value='0,1'/><Value name='outline' value='0,0;200,0;200,180;0,180'/></Component>
<Component id='shadow' kind='receiver'><Value name='transform' value='plane'/><Value name='material' value='shadow-mat'/><Value name='opacity' value='1'/><Value name='projection' value='0,1'/><Value name='outline' value='200,0;480,0;480,180;200,180'/></Component>
<Component id='label-pose' kind='transform'><Value name='position' value='100,300'/></Component>
<Component id='label' kind='text'><Value name='transform' value='label-pose'/><Value name='font' value='font'/><Value name='characters' value='AB'/><Value name='value' value='BA'/><Value name='width' value='40'/><Value name='layer' value='foreground'/></Component>
</Components></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(surfaces)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    var pixels=new Color[480*360];device.SetRenderTarget(target);device.Clear(new Color(8,12,24));scene.Tick(.01,.01);renderer.Tick();game.StartBatch();renderer.DrawLayer("world",1);renderer.CaptureStage("world",target,1);renderer.DrawLayer("surface",1);renderer.DrawLayer("foreground",1);game.EndBatch();target.GetData(pixels);
                    using(var file=File.Create(Path.Combine(root,"surface-captures.png")))target.SaveAsPng(file,480,360);
                    Check(pixels[244*480+90]==new Color(48,196,128),"General receiver reflects a selected PNG capture about its authored plane: "+pixels[244*480+90]);
                    Check(pixels[244*480+110]==new Color(8,12,24),"Projected receivers obey the same material alpha mask as other drawing components");
                    Check(pixels[244*480+310]==new Color(48,64,255),"Shadow projection uses captured alpha without carrying source RGB");
                    Check(pixels[280*480+90]==new Color(220,80,64)&&pixels[312*480+90]==new Color(48,196,128),"PNG font wraps and centers each line with whole pixel cells");
                    using(var file=File.Create(Path.Combine(root,"surface-captures.png")))target.SaveAsPng(file,480,360);
                    Check(pixels[340*480+400]==new Color(8,12,24),"Transparent capture borders don't smear the last captured pixels across the receiver: "+pixels[340*480+400]);
                }
                var lighting=System.Xml.Linq.XDocument.Parse(@"<MappingSettings version='1'><Components>
<Component id='lamp-pose' kind='transform'><Value name='position' value='240,180'/></Component>
<Component id='wall' kind='receiver'><Value name='mode' value='occlusion'/><Value name='outline' value='280,150;300,150;300,210;280,210'/></Component>
<Component id='lamp' kind='light'><Value name='transform' value='lamp-pose'/><Value name='radius' value='140'/><Value name='cone' value='90'/><Value name='softness' value='0'/><Value name='occluders' value='wall'/></Component>
</Components></MappingSettings>");
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Compile(lighting)))
                using(var renderer=new NativeRenderer(scene,device,root))
                {
                    var pixels=new Color[480*360];device.SetRenderTarget(target);device.Clear(new Color(8,12,24));scene.Tick(.01,.01);renderer.Tick();game.StartBatch();renderer.DrawLayer("world",1);game.EndBatch();target.GetData(pixels);
                    Check(pixels[180*480+260].R>60,"General light illuminates the area before a selected occlusion contour");
                    Check(pixels[180*480+320]==new Color(8,12,24),"Occlusion contour blocks light beyond the surface");
                    Check(pixels[180*480+180]==new Color(8,12,24),"Light cone respects its transform direction");
                    using(var file=File.Create(Path.Combine(root,"cone-occlusion.png")))target.SaveAsPng(file,480,360);
                }
            }
        }
    }
}
