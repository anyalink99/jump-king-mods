using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JumpKing;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using RunVerifier;

internal static class GraphicsTests
{
    private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private sealed class Service:IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice {get;set;}
        public event EventHandler<EventArgs> DeviceCreated {add{}remove{}}
        public event EventHandler<EventArgs> DeviceDisposing {add{}remove{}}
        public event EventHandler<EventArgs> DeviceReset {add{}remove{}}
        public event EventHandler<EventArgs> DeviceResetting {add{}remove{}}
    }
    [STAThread] private static void Main(string[] args)
    {
        string gameDir=args[0],output=args[1];Directory.CreateDirectory(output);Directory.SetCurrentDirectory(output);
        using(var form=new Form {ShowInTaskbar=false})
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,new PresentationParameters {DeviceWindowHandle=form.Handle,BackBufferWidth=480,BackBufferHeight=360}))
        using(var target=new RenderTarget2D(device,480,360))
        using(var white=new Texture2D(device,1,1))
        {
            var services=new GameServiceContainer();services.AddService(typeof(IGraphicsDeviceService),new Service {GraphicsDevice=device});
            var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));typeof(Game1).GetField("_instance",F).SetValue(null,game);typeof(Game).GetField("_services",F).SetValue(game,services);
            foreach(var f in typeof(Game).GetFields(F).Where(f=>f.FieldType==typeof(IGraphicsDeviceService)))f.SetValue(game,services.GetService(typeof(IGraphicsDeviceService)));
            game.contentManager=new JKContentManager();var pixel=(PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture));GC.SuppressFinalize(pixel);white.SetData(new[]{Color.White});typeof(PixelTexture).GetField("_texture",F).SetValue(pixel,white);game.contentManager.Pixel=pixel;
            using(var content=new ContentManager(services,gameDir))using(var batch=new SpriteBatch(device))
            {
                game.contentManager.font.MenuFont=content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");game.contentManager.font.MenuFontSmall=content.Load<SpriteFont>("Content/font/sf_small");game.contentManager.font.LocationFont=content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
                var texture=content.Load<Texture2D>("Content/gui/frame");int cell=texture.Width/3;game.contentManager.gui.FrameSprites=new Sprite[3,3];for(int x=0;x<3;x++)for(int y=0;y<3;y++)game.contentManager.gui.FrameSprites[x,y]=Sprite.CreateSprite(texture,new Rectangle(x*cell,y*cell,cell,cell));
                Game1.spriteBatch=batch;
                var record=new RunRecord {id="00112233445566778899aabbccddeeff",steamId="76561198000000000",mapId="local:test",mapName="Jump King",revision=new string('a',64),category="modified",time=1234.567,nativePeak=3,complete=true,finished=DateTime.UtcNow.ToString("o"),ending="MainBabe"};
                RunVerifier.ModEntry.Repository=new Repository(Path.Combine(output,"history"));RunVerifier.ModEntry.Client=new Client(RunVerifier.ModEntry.Repository);RunVerifier.ModEntry.Repository.Save(record);RunVerifier.ModEntry.Repository.Flush();
                var seal=(ResultsSeal)FormatterServices.GetUninitializedObject(typeof(ResultsSeal));
                Action<string,Action> render=delegate(string name,Action draw){device.SetRenderTarget(target);device.Clear(new Color(24,24,34));batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);draw();batch.End();device.SetRenderTarget(null);using(var file=File.Create(Path.Combine(output,name+".png")))target.SaveAsPng(file,480,360);};
                render("results-seal",delegate{seal.DrawRecord(record,"online-observed");HistoryPage.Text("TIME 00:20:34.567",145,140,Color.White);HistoryPage.Text("JUMPS 548   FALLS 41",135,160,Color.White);});
                var page=new HistoryPage(null);page.OnOpen();render("history",page.Draw);typeof(HistoryPage).GetField("selected",F).SetValue(page,record);render("run-details",page.Draw);
                Console.WriteLine("PASS: native fonts, seal and history rendered to "+output);
            }
        }
    }
}

