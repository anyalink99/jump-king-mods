using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JKRuntime;
using JKRuntime.Presentation;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

internal static class ActorVisualGraphicsTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private sealed class Pixel : PixelTexture
    {internal Pixel(Texture2D value){_texture=value;}protected override void CreatePixel(){}protected override void UnloadPixel(){}}
    private sealed class DeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice {get;set;}
        public event EventHandler<EventArgs> DeviceCreated {add{}remove{}}
        public event EventHandler<EventArgs> DeviceDisposing {add{}remove{}}
        public event EventHandler<EventArgs> DeviceReset {add{}remove{}}
        public event EventHandler<EventArgs> DeviceResetting {add{}remove{}}
    }
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    private static byte[] Bytes(Action<BinaryWriter> write)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){write(writer);return stream.ToArray();}}
    private static byte[] Snapshot(params Tuple<string,byte[]>[] states)
    {return Bytes(w=>{w.Write((byte)1);w.Write((byte)states.Length);foreach(var state in states){w.Write(state.Item1);w.Write((ushort)(state.Item1=="more-items.jetpack"?2:1));w.Write((ushort)state.Item2.Length);w.Write(state.Item2);}});}
    private static byte[] Ball(float amount,float angle)
    {return Bytes(w=>{w.Write(amount);w.Write(angle);w.Write(true);w.Write(0f);w.Write(-1f);w.Write(0f);w.Write(0f);});}
    private static IActorVisual Visual(Assembly assembly,string name)
    {return (IActorVisual)Activator.CreateInstance(assembly.GetType(name,true),true);}
    [STAThread]
    private static int Main(string[] args)
    {
        try{Render(args);return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static void Render(string[] args)
    {
        var ballAssembly=Assembly.LoadFrom(args[1]);var items=Assembly.LoadFrom(args[2]);var mega=Assembly.LoadFrom(args[3]);
        using(var window=new Form {ShowInTaskbar=false})
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters {DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360}))
        using(var batch=new SpriteBatch(device))using(var target=new RenderTarget2D(device,480,360))
        using(var pixel=new Texture2D(device,1,1))using(var scope=new RuntimeScope())
        {
            var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),new DeviceService {GraphicsDevice=device});
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            foreach(var field in typeof(Game).GetFields(Flags).Where(f=>f.FieldType==typeof(IGraphicsDeviceService)))field.SetValue(game,services.GetService(typeof(IGraphicsDeviceService)));
            typeof(Game1).GetField("_instance",Flags).SetValue(null,game);Game1.spriteBatch=batch;
            game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));pixel.SetData(new[]{Color.White});game.contentManager.Pixel=new Pixel(pixel);
            var screen=(JumpKing.Level.LevelScreen)FormatterServices.GetUninitializedObject(typeof(JumpKing.Level.LevelScreen));
            typeof(JumpKing.Level.LevelScreen).GetField("m_hitboxes",Flags).SetValue(screen,new JumpKing.Level.IBlock[0]);
            typeof(JumpKing.Level.LevelManager).GetField("m_screens",Flags).SetValue(null,new[]{screen});
            typeof(JumpKing.Level.LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
            using(var content=new ContentManager(services,args[0]))
            {
                var atlas=content.Load<Texture2D>("Content/king/base");var kingSprites=new JumpKing.JKMemory.KingSprites(atlas);
                var king=kingSprites.regular.GetSprite(JumpKing.JKMemory.KingSpriteLayers.Regular.SpriteKey.idle);
                var charge=kingSprites.regular.GetSprite(JumpKing.JKMemory.KingSpriteLayers.Regular.SpriteKey.jump_charge);
                var font=content.Load<SpriteFont>("Content/font/sf_small");
                var settingsType=items.GetType("MoreItems.SettingsStore",true);settingsType.GetMethod("EnsureLoaded",Flags).Invoke(null,null);
                var preferences=settingsType.GetProperty("Current",Flags).GetValue(null,null);
                var trailOption=preferences.GetType().GetProperty("ShowJetpackTrail");bool oldTrail=(bool)trailOption.GetValue(preferences,null);
                trailOption.SetValue(preferences,true,null);scope.Defer(()=>trailOption.SetValue(preferences,oldTrail,null));
                items.GetType("JumpKingJetpack.JetpackActorVisual",true).GetMethod("Prepare",Flags).Invoke(null,new object[]{scope});
                IActorVisual ball=null,pack=null;
                scope.Own(ActorVisuals.Register("morph-ball",1,VisualPhase.Form,p=>null,()=>ball=Visual(ballAssembly,"MorphBallMod.BallActorVisual")));
                scope.Own(ActorVisuals.Register("more-items.jetpack",2,VisualPhase.Attachment,p=>null,()=>pack=Visual(items,"JumpKingJetpack.JetpackActorVisual")));
                scope.Own(ActorVisuals.Register("more-items.hammer",1,VisualPhase.Attachment,p=>null,()=>Visual(items,"HammerKing.HammerActorVisual")));
                scope.Own(ActorVisuals.Register("powers.dash",1,VisualPhase.Behind,p=>null,()=>Visual(mega,"Powers.ActorVisualState+Dash")));
                scope.Own(ActorVisuals.Register("powers.warp",1,VisualPhase.Replacement,p=>null,()=>Visual(mega,"Powers.ActorVisualState+Warp")));
                byte[][] states={
                    Snapshot(Tuple.Create("morph-ball",Ball(1,.5f))),
                    Snapshot(Tuple.Create("more-items.jetpack",new byte[]{0,1,1,0})),
                    Snapshot(Tuple.Create("morph-ball",Ball(1,1.2f)),Tuple.Create("more-items.jetpack",new byte[]{5,1,1,1})),
                    Snapshot(Tuple.Create("more-items.hammer",Bytes(w=>{w.Write(28f);w.Write(-25f);w.Write(true);}))),
                    Snapshot(Tuple.Create("powers.dash",Bytes(w=>{w.Write(-60f);w.Write(-26f);w.Write(-9f);w.Write(-26f);w.Write(9f);w.Write(13f);w.Write(.09f);w.Write(.01f);w.Write((sbyte)1);w.Write(false);w.Write(false);}))),
                    Snapshot(Tuple.Create("powers.warp",Bytes(w=>{w.Write(0f);w.Write(0f);w.Write(.04f);w.Write((byte)0);})))
                };
                var actors=new ActorVisuals.Actor[states.Length];var sprites=new Sprite[states.Length];
                for(int i=0;i<actors.Length;i++){actors[i]=scope.Own(ActorVisuals.Create());sprites[i]=actors[i].Present(states[i],king,charge,new ActorVisualFrame(0,new Vector2(80+(i%3)*160,130+(i/3)*170),Vector2.Zero,SpriteEffects.None));}
                var textureField=ball.GetType().GetField("texture",Flags);var original=(Texture2D)textureField.GetValue(ball);
                // roll samples must reuse the existing art, including after repeated draws
                for(int frame=0;frame<120;frame++)sprites[2]=actors[2].Present(Snapshot(Tuple.Create("morph-ball",Ball(1,frame*.1f)),Tuple.Create("more-items.jetpack",new byte[]{5,1,1,1})),king,charge,new ActorVisualFrame(frame/60d,new Vector2(400,130),Vector2.Zero,SpriteEffects.None));
                Check(ReferenceEquals(original,textureField.GetValue(ball)),"Ball art was rebuilt during ordinary rotation");
                var countProperty=pack.GetType().GetProperty("ParticleCount",Flags);int particles=(int)countProperty.GetValue(pack,null);
                if(particles==0)foreach(var notice in RuntimeJournal.Read())Console.WriteLine(notice.Owner+" "+notice.Operation+": "+notice.Detail);
                Check(particles>0&&particles<=96,"thrust must generate a bounded local particle system without particle payloads");
                device.SetRenderTarget(target);device.Clear(new Color(20,25,34));
                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null);
                string[] labels={"BALL","JETPACK","BALL + JETPACK","HAMMER","AIR DASH","WARP PHASE"};
                for(int i=0;i<sprites.Length;i++)
                {
                    var anchor=new Vector2(80+(i%3)*160,130+(i/3)*170);
                    batch.DrawString(font,labels[i],new Vector2((i%3)*160+10,(i/3)*170+18),Color.White);
                    ActorVisuals.Draw(sprites[i],anchor,SpriteEffects.None,Color.White);
                    ActorVisuals.Draw(sprites[i],anchor+new Vector2(35,15),SpriteEffects.FlipHorizontally,new Color(90,225,240)*.55f);
                }
                batch.End();device.SetRenderTarget(null);
                Check((int)countProperty.GetValue(pack,null)==particles,"repeated camera/ghost draws must not emit or advance particles");
                var pixels=new Color[480*360];target.GetData(pixels);
                for(int i=0;i<6;i++){
                    int visible=0;for(int y=65+(i/3)*170;y<155+(i/3)*170;y++)for(int x=(i%3)*160+10;x<(i%3)*160+150;x++)
                        if(pixels[y*480+x]!=new Color(20,25,34))visible++;
                    Check(visible>30,"Actor visual is blank: "+labels[i]);
                }
                using(var stream=File.Create(args[4]))target.SaveAsPng(stream,480,360);
                actors[2].Present(states[2],king,charge,new ActorVisualFrame(0,new Vector2(400,130),Vector2.Zero,SpriteEffects.None,true));
                Check(ReferenceEquals(original,textureField.GetValue(ball)),"seeking within a form must reset particles without rebuilding ball art");
                actors[2].Dispose();Check(original.IsDisposed,"Ball actor teardown retained its GPU texture");
                Console.WriteLine("[OK] GPU actor forms, combined equipment, independent ghosts, rotation art reuse and texture disposal");
            }
        }
    }
}
