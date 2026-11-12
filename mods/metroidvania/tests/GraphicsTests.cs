using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private sealed class GraphicsService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice{get;set;}
            public event EventHandler<EventArgs> DeviceCreated{add{}remove{}}
            public event EventHandler<EventArgs> DeviceDisposing{add{}remove{}}
            public event EventHandler<EventArgs> DeviceReset{add{}remove{}}
            public event EventHandler<EventArgs> DeviceResetting{add{}remove{}}
        }
        private static void Graphics(string gameDir,string scratch,int start)
        {
            using(var window=new Form{ShowInTaskbar=false})
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters{DeviceWindowHandle=window.Handle,BackBufferWidth=480,BackBufferHeight=360}))
            using(var target=new RenderTarget2D(device,480,360))
            using(var white=new Texture2D(device,1,1)){
                var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),new GraphicsService{GraphicsDevice=device});
                using(var content=new ContentManager(services,gameDir)){
                    var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));GC.SuppressFinalize(game);
                    typeof(Game1).GetField("_instance",Members).SetValue(null,game);typeof(Game).GetField("_services",Members).SetValue(game,services);
                    foreach(var field in typeof(Game).GetFields(Members))if(field.FieldType==typeof(IGraphicsDeviceService))field.SetValue(game,services.GetService(typeof(IGraphicsDeviceService)));
                    game.contentManager=new JKContentManager();white.SetData(new[]{Color.White});
                    var pixel=(PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture));GC.SuppressFinalize(pixel);typeof(PixelTexture).GetField("_texture",Members).SetValue(pixel,white);game.contentManager.Pixel=pixel;
                    game.contentManager.font.MenuFont=content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");game.contentManager.font.MenuFontSmall=content.Load<SpriteFont>("Content/font/sf_small");game.contentManager.font.LocationFont=content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
                    game.contentManager.playerSprites=new JKContentManager.PlayerSprites{_CurrentSprites=new JumpKing.JKMemory.LayeredKingSprites(content.Load<Texture2D>("Content/king/base"))};
                    if(start==4){ArtMasks(content);PoseSheet(device,content,scratch);EnemySheet(device,scratch);return;}
                    var frame=content.Load<Texture2D>("Content/gui/frame");int cell=frame.Width/3;game.contentManager.gui.FrameSprites=new Sprite[3,3];
                    for(int x=0;x<3;x++)for(int y=0;y<3;y++)game.contentManager.gui.FrameSprites[x,y]=Sprite.CreateSprite(frame,new Rectangle(x*cell,y*cell,cell,cell));
                    typeof(UiSounds).GetField("TestPlayback",Members).SetValue(null,new Action<UiSound>(s=>{}));
                    typeof(ModEntry).GetField("settings",Members).SetValue(null,new SettingsFile<Preferences>(Path.Combine(scratch,"ui-prefs.xml"),()=>new Preferences(),Preferences.Validate));
                    ModEntry.Store=new CharacterStore(Path.Combine(scratch,"ui-character-"+start+".xml"));ModEntry.Store.Edit(p=>{p.Victory();p.Victory();p.Victory();return true;});ModEntry.Store.Save();
                    using(var batch=new SpriteBatch(device)){
                    Game1.spriteBatch=batch;var stack=new UiPageStack(p=>new CharacterPage(p));var page=new CharacterPage(stack);page.OnOpen();
                    string[] names={"character","inventory","equipment","camp"};
                    for(int i=0;i<start;i++)page.Update(new UiInput{Action=UiAction.Right,Right=true},0);
                    for(int tab=0;tab<1;tab++){string name=names[start];
                            device.SetRenderTarget(target);device.Clear(new Color(8,11,17));batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);page.Draw();batch.End();device.SetRenderTarget(null);
                        var colors=new Color[480*360];target.GetData(colors);bool title=false,border=false;
                        for(int y=28;y<44;y++)for(int x=28;x<160;x++){var c=colors[y*480+x];if(c.R>200&&c.G>140&&c.B<120)title=true;}
                        for(int y=30;y<330;y++)for(int x=463;x<468;x++)if(colors[y*480+x].R>80)border=true;
                        Check(title&&border,"Complete menu frame and title: "+name);
                        using(var image=new System.Drawing.Bitmap(480,360,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
                            for(int y=0;y<360;y++)for(int x=0;x<480;x++){var c=colors[y*480+x];image.SetPixel(x,y,System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B));}
                            image.Save(Path.Combine(scratch,name+".png"),System.Drawing.Imaging.ImageFormat.Png);
                        }
                        page.Update(new UiInput{Action=UiAction.Right,Right=true},0);
                    }
                    page.OnClose();
                    Console.WriteLine("[OK] Native-font menu render: "+names[start]+" "+scratch);
                    }
                }
            }
        }
    }
}
