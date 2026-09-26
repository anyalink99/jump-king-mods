using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using HarmonyLib;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private static void GraphicsTests()
    {
        using(var window=new Form { ShowInTaskbar=false })
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
            new PresentationParameters { DeviceWindowHandle=window.Handle, BackBufferWidth=480, BackBufferHeight=360 }))
        using(var batch=new SpriteBatch(device))
        using(var target=new RenderTarget2D(device,480,360))
        using(var white=new Texture2D(device,1,1))
        {
            var game=MakeGame(); Game1.spriteBatch=batch; white.SetData(new[]{Color.White});
            SettingsStore.Current.SetRefresh(true); PerformanceFeatures.Install(); FeatureClock.BeforeTick(game);
            Camera.Offset=Vector2.Zero; AccessTools.Field(typeof(Camera),"_current_screen").SetValue(null,0);
            var player=EmptyPlayer(); player.m_body=(BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
            player.m_body.Position=new Vector2(91,74); player.m_body.Velocity=new Vector2(20,0);
            AccessTools.Field(typeof(PlayerEntity),"m_sprite").SetValue(player,Sprite.CreateSprite(white));
            AccessTools.Field(typeof(PlayerPresentation),"body").SetValue(null,player.m_body);
            var history=(PositionHistory)AccessTools.Field(typeof(PlayerPresentation),"history").GetValue(null);
            history.Observe(new Vector2(71,74),new Vector2(20,0),1,false);
            history.Observe(player.m_body.Position,player.m_body.Velocity,1,false);
            var path=(PredictionPath)AccessTools.Field(typeof(PlayerPresentation),"path").GetValue(null);
            path.Begin(player.m_body.Position,history.Velocity,18,26,false); path.Build();
            var cadence=AccessTools.Field(typeof(JKRuntime.PresentationScheduling),"remainder");
            string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"presentation-graphics"); Directory.CreateDirectory(directory);
            try
            {
                for(int frame=0;frame<4;frame++)
                {
                    cadence.SetValue(null,42500L*frame);
                    device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch();
                    // Exercise native PlayerEntity.Draw with the real patched
                    // position load and normal virtual sprite dispatch.
                    player.Draw();
                    batch.Draw(white,new Rectangle(20,20,3,3),Color.Magenta);
                    game.EndBatch(); device.SetRenderTarget(null);
                    var pixels=new Color[480*360]; target.GetData(pixels);
                    Check(pixels[100*480+100+5*frame]==Color.White,"GPU draws predicted native king position at quarter tick "+frame);
                    Check(pixels[20*480+20]==Color.Magenta,"HUD remains stationary between presentation frames");
                    Check(player.m_body.Position==new Vector2(91,74)&&player.m_body.Velocity==new Vector2(20,0),"Native Draw leaves physics/save state untouched");
                    using(var stream=File.Create(Path.Combine(directory,"prediction-"+frame+".png"))) target.SaveAsPng(stream,480,360);
                }
                device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch(); player.Draw();
                batch.Draw(white,new Rectangle(90,90,40,40),Color.Cyan); game.EndBatch(); device.SetRenderTarget(null);
                var covered=new Color[480*360]; target.GetData(covered);
                Check(covered[100*480+115]==Color.Cyan,"Native foreground/menu pass still occludes predicted king");
                Check(device.GetRenderTargets().Length==0,"No presentation target leak");
                path.Begin(player.m_body.Position,new Vector2(0,40),18,26,false);
                path.Add(new JumpKing.Level.BoxBlock(new Rectangle(0,110,480,10))); path.Build();
                for(int frame=1;frame<4;frame++)
                {
                    cadence.SetValue(null,42500L*frame);
                    device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch(); player.Draw(); game.EndBatch(); device.SetRenderTarget(null);
                    var pixels=new Color[480*360]; target.GetData(pixels);
                    int marker=-1;
                    for(int y=0;y<360;y++) if(pixels[y*480+100]==Color.White) marker=y;
                    Check(marker>=100 && marker<=110,"Native GPU drawing stops the predicted foot marker at the floor");
                    Check(player.m_body.Position==new Vector2(91,74)&&player.m_body.Velocity==new Vector2(20,0),"Collision-limited Draw never writes the native body");
                }
                using(JKRuntime.Gameplay.PresentationActivity.Begin("test.replay",player.m_body))
                {
                    device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch(); player.Draw(); game.EndBatch(); device.SetRenderTarget(null);
                    var pixels=new Color[480*360]; target.GetData(pixels);
                    Check(pixels[100*480+100]==Color.White,"A physics-suspended presentation lease draws the authoritative pose without prediction");
                }
                player.m_body.Position=new Vector2(141,74);
                device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch(); player.Draw(); game.EndBatch(); device.SetRenderTarget(null);
                var relocated=new Color[480*360]; target.GetData(relocated);
                Check(relocated[100*480+150]==Color.White,"A late external pose change cannot be overwritten by the previous prediction path");
            }
            finally { PerformanceFeatures.Uninstall(); Game1.spriteBatch=null; AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null); }
        }
    }
}
