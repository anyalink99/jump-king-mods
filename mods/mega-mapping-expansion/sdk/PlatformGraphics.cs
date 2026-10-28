using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Construction;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion
{
    internal static class PlatformGraphics
    {
        internal static void Lab(GraphicsDevice device,RenderTarget2D target,string root,string map,string gameDir)
        {
            var plan=MovingPlatformPlan.Read(map,3);var layout=MapLayout.Read(map);
            using(var collision=new System.Drawing.Bitmap(Path.Combine(map,"level+.png")))
                plan.AddPainted(PlatformImageScan.Collect(plan,layout,(x,y)=>unchecked((uint)collision.GetPixel(x,y).ToArgb())));
            if(plan.Platforms.Any(p=>p.Label==""&&plan.Profiles[p.Profile].Label==""))throw new Exception("A lab platform has no caption");
            if(plan.Platforms.Any(p=>string.IsNullOrEmpty(plan.Profiles[p.Profile].Texture)||!plan.Profiles[p.Profile].Texture.EndsWith(".png",StringComparison.OrdinalIgnoreCase))||plan.Platforms.Select(p=>plan.Profiles[p.Profile].Texture).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=plan.Platforms.Count)throw new Exception("Each lab platform needs its own editable PNG");
            LabMotion(plan,map);
            var content=Game1.instance.contentManager;
            var backgrounds=typeof(JKContentManager).GetField("m_backbackgrounds",BindingFlags.Instance|BindingFlags.NonPublic);
            var midgrounds=typeof(JKContentManager).GetField("m_backgrounds",BindingFlags.Instance|BindingFlags.NonPublic);
            var oldBackgrounds=backgrounds.GetValue(content);var oldMidgrounds=midgrounds.GetValue(content);var oldFont=content.font.MenuFontSmall;
            var textures=new List<Texture2D>();
            using(var native=new ContentManager(Game1.instance.Services,Path.Combine(gameDir,"Content")))
            using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Read(map)))
            using(var renderer=new NativeRenderer(scene,device,map))
            using(var platforms=new NativeMovingPlatforms(plan,map,true,false))
            try
            {
                content.font.MenuFontSmall=native.Load<SpriteFont>("font/sf_small");
                foreach(var layer in new[]{"background","midground"})
                {
                    var images=new Dictionary<string,Texture2D>();
                    foreach(string path in Directory.GetFiles(Path.Combine(map,"screens/"+layer),"*.png")){var image=Load(device,path);textures.Add(image);images.Add(Path.GetFileNameWithoutExtension(path),image);}
                    (layer=="background"?backgrounds:midgrounds).SetValue(content,images);
                }
                scene.Signals.Publish("player.position",SceneValue.Vector(600,600),ObservationQuality.Sampled);
                scene.Signals.Publish("player.screen",SceneValue.Integer(1),ObservationQuality.Sampled);
                scene.Tick(.01,.01);renderer.Tick();renderer.Prime();
                for(int room=1;room<=2;room++)
                {
                    var background=content.GetBackbackground(room-1);var terrain=content.GetBackground(room-1);
                    if(background==null||terrain==null)throw new Exception("Native texture lookup omitted lab room "+room);
                    device.SetRenderTarget(target);device.Clear(Color.Black);Game1.instance.StartBatch();
                    Game1.spriteBatch.Draw(background,Vector2.Zero,Color.White);Game1.spriteBatch.Draw(terrain,Vector2.Zero,Color.White);platforms.DrawScreen(room-1);Game1.instance.EndBatch();
                    var unlit=new Color[480*360];target.GetData(unlit);
                    Game1.instance.StartBatch();renderer.DrawLayer("background",room);renderer.DrawLayer("world",room);renderer.DrawLayer("foreground",room);Game1.instance.EndBatch();
                    var lit=new Color[480*360];target.GetData(lit);int changed=0;
                    for(int i=0;i<lit.Length;i++)if(lit[i]!=unlit[i])changed++;
                    if(changed<1000)throw new Exception("Lab crystal light didn't illuminate room "+room);
                    device.SetRenderTarget(null);using(var file=File.Create(Path.Combine(root,"platform-lab-room-"+room+".png")))target.SaveAsPng(file,480,360);
                    if(room==1)
                    {
                        scene.Signals.Publish("player.position",SceneValue.Vector(340,103),ObservationQuality.Sampled);scene.Tick(.01,.01);renderer.Tick();
                        device.SetRenderTarget(target);device.Clear(Color.Black);Game1.instance.StartBatch();Game1.spriteBatch.Draw(background,Vector2.Zero,Color.White);Game1.spriteBatch.Draw(terrain,Vector2.Zero,Color.White);platforms.DrawScreen(0);renderer.DrawLayer("foreground",1);Game1.instance.EndBatch();
                        var shadow=new Color[480*360];target.GetData(shadow);int pixel=116*480+320;
                        if(shadow[pixel].G+3>=lit[pixel].G)throw new Exception("The King's bound receiver didn't block the crystal light");
                        device.SetRenderTarget(null);using(var file=File.Create(Path.Combine(root,"platform-lab-king-shadow.png")))target.SaveAsPng(file,480,360);
                        scene.Signals.Publish("player.position",SceneValue.Vector(600,600),ObservationQuality.Sampled);scene.Tick(.01,.01);renderer.Tick();
                        device.SetRenderTarget(target);device.Clear(Color.Black);Game1.instance.StartBatch();Game1.spriteBatch.Draw(background,Vector2.Zero,Color.White);Game1.spriteBatch.Draw(terrain,Vector2.Zero,Color.White);platforms.DrawScreen(0);renderer.DrawLayer("foreground",1);Game1.instance.EndBatch();target.GetData(shadow);
                        if(shadow[pixel].G!=lit[pixel].G)throw new Exception("Moving the King left a stale light occluder");device.SetRenderTarget(null);
                    }
                }
            }
            finally{backgrounds.SetValue(content,oldBackgrounds);midgrounds.SetValue(content,oldMidgrounds);content.font.MenuFontSmall=oldFont;foreach(var texture in textures)texture.Dispose();}
            Console.WriteLine("[OK] Platform lab: native background lookup, real captions, crystal lights and moving King occlusion.");
        }
        private static Texture2D Load(GraphicsDevice device,string path){using(var file=File.OpenRead(path))return Texture2D.FromStream(device,file);}
        private static void LabMotion(MovingPlatformPlan plan,string map)
        {
            var screensField=typeof(LevelManager).GetField("m_screens",BindingFlags.Static|BindingFlags.NonPublic);
            var countField=typeof(LevelManager).GetField("_total_screens",BindingFlags.Static|BindingFlags.NonPublic);
            var oldScreens=screensField.GetValue(null);var oldCount=countField.GetValue(null);
            try
            {
                var screens=new LevelScreen[3];
                using(var image=new System.Drawing.Bitmap(Path.Combine(map,"level+.png")))
                for(int room=0;room<3;room++)
                {
                    var blocks=new List<IBlock>();
                    for(int y=0;y<45;y++)for(int x=0;x<60;x++)if(image.GetPixel(room*60+x,y).ToArgb()==System.Drawing.Color.Black.ToArgb())blocks.Add(new BoxBlock(new Rectangle(x*8,y*8-room*360,8,8)));
                    screens[room]=new LevelScreen(room,blocks.ToArray(),new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);
                }
                screensField.SetValue(null,screens);countField.SetValue(null,screens.Length);
                var player=(PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PlayerEntity));player.m_body=new BodyComp(new Vector2(240,318),18,26);
                using(var owner=new NativeMovingPlatforms(plan,map,false))
                using(owner.Activate(player,false))
                using(var scene=new SceneProgram(new RecipeCompiler(PrimitiveRegistry.Create()).Read(map)))
                {
                    var parts=(IBlock[])typeof(LevelScreen).GetField("m_hitboxes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(screens[1]);
                    var shuttle=parts.OfType<NativePlatformPart>().Single(p=>p.Platform.Definition.Id=="shuttle");
                    owner.Tick(player.m_body,1.0/60);if(shuttle.Bounds.X==shuttle.Origin.X)throw new Exception("The lab circuit didn't start automatically");
                    scene.Tick(0,0);var transforms=new TransformSystem(scene);var sensors=new SensorSystem(scene,transforms);
                    Action<int,int> observe=(room,x)=>{transforms.Refresh();sensors.Observe(new ActorObservation{Id="player",Screen=room,Bounds=new SceneBox{X=x,Y=318,Width=18,Height=26},Velocity=SceneValue.Vector(0,0)},new SurfaceObservation[0],"scene",1.0/60);scene.Tick(1.0/60,1.0/60);};
                    observe(1,240);observe(1,95);if(shuttle.Platform.Motion.Velocity.X>=0)throw new Exception("Room 1's circuit switch didn't reverse the deck");
                    observe(1,95);if(shuttle.Platform.Motion.Velocity.X>=0)throw new Exception("A held circuit switch retriggered without leaving");
                    observe(2,287);if(shuttle.Platform.Motion.Velocity.X<=0)throw new Exception("Room 2's circuit switch didn't reverse the deck");
                    var route=new HashSet<Point>();
                    for(int tick=0;tick<480;tick++){owner.Tick(player.m_body,1.0/60);route.Add(shuttle.Bounds.Location);}
                    if(route.Count<100||route.Max(p=>p.X)-route.Min(p=>p.X)!=64||route.Max(p=>p.Y)-route.Min(p=>p.Y)!=48)throw new Exception("The native circuit didn't cover its complete four-point route");
                }
            }
            finally{screensField.SetValue(null,oldScreens);countField.SetValue(null,oldCount);}
            Console.WriteLine("[OK] Lab circuit: automatic start, both physical switches, held-contact debounce and complete native route.");
        }
        internal static void Run(GraphicsDevice device,RenderTarget2D target,string root)
        {
            string atlas=Path.Combine(root,"platform-atlas.png");
            using(var image=new System.Drawing.Bitmap(32,8))using(var g=System.Drawing.Graphics.FromImage(image))
            {g.Clear(System.Drawing.Color.Red);g.FillRectangle(System.Drawing.Brushes.Blue,16,0,16,8);image.Save(atlas);}
            var plan=MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='a' axis='horizontal' distance='96' phase='0.5'/><Profile id='a' motion='a' texture='platform-atlas.png' columns='2' frames='2' fps='2' fit='stretch'/><Platform id='a' profile='a' rect='100,200,48,8'/></MovingPlatforms>",1);
            using(var platforms=new NativeMovingPlatforms(plan,root,true,false))
            {
                device.SetRenderTarget(target);device.Clear(Color.Transparent);Game1.spriteBatch.Begin();platforms.DrawScreen(0);Game1.spriteBatch.End();device.SetRenderTarget(null);
                var pixels=new Color[480*360];target.GetData(pixels);
                if(pixels[202*480+132].B<240||pixels[202*480+132].R!=0||pixels[202*480+100].A!=0)throw new Exception("Animated platform graphics lost their frame, offset or collision-sized placement");
                using(var stream=File.Create(Path.Combine(root,"moving-platform-render.png")))target.SaveAsPng(stream,480,360);
            }
            string fallbackPath=Path.Combine(Path.GetDirectoryName(typeof(PlatformGraphics).Assembly.Location),"assets/platforms/default.png");byte[] original=File.ReadAllBytes(fallbackPath);
            var fallback=MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='a' axis='horizontal' distance='96' playing='false'/><Profile id='a' motion='a'/><Platform id='a' profile='a' rect='100,200,48,8'/></MovingPlatforms>",1);
            MovingPlatformPacket saved;
            try
            {
                using(var expected=Load(device,fallbackPath))
                using(var platforms=new NativeMovingPlatforms(fallback,root,true,false))
                {
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);Game1.spriteBatch.Begin();platforms.DrawScreen(0);Game1.spriteBatch.End();device.SetRenderTarget(null);
                    var pixels=new Color[480*360];target.GetData(pixels);var source=new Color[expected.Width*expected.Height];expected.GetData(source);
                    for(int y=0;y<8;y++)for(int x=0;x<48;x++)if(pixels[(200+y)*480+100+x]!=source[y%expected.Height*expected.Width+x%expected.Width])throw new Exception("The default deck did not tile the packaged PNG");
                    saved=(MovingPlatformPacket)platforms.Capture();
                }
                using(var replacement=new System.Drawing.Bitmap(16,8))using(var ink=System.Drawing.Graphics.FromImage(replacement))
                {ink.Clear(System.Drawing.Color.Magenta);replacement.Save(fallbackPath,System.Drawing.Imaging.ImageFormat.Png);}
                using(var platforms=new NativeMovingPlatforms(fallback,root,true,false))
                {
                    if(((MovingPlatformPacket)platforms.Capture()).Identity==saved.Identity)throw new Exception("Default PNG edits didn't change platform graphics identity");
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);Game1.spriteBatch.Begin();platforms.DrawScreen(0);Game1.spriteBatch.End();device.SetRenderTarget(null);
                    var pixels=new Color[480*360];target.GetData(pixels);if(pixels[202*480+102]!=Color.Magenta)throw new Exception("Restarting platforms ignored the edited default PNG");
                }
            }
            finally{File.WriteAllBytes(fallbackPath,original);}
            Console.WriteLine("[OK] Moving-platform GPU rendering: animated atlas, stretch fit, route phase, packaged default PNG and edited-image reload.");
        }
    }
}
