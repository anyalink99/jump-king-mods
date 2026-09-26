using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal static class RenderWarpPreview
    {
        private sealed class DeviceService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; set; }
            public event EventHandler<EventArgs> DeviceCreated { add {} remove {} }
            public event EventHandler<EventArgs> DeviceDisposing { add {} remove {} }
            public event EventHandler<EventArgs> DeviceReset { add {} remove {} }
            public event EventHandler<EventArgs> DeviceResetting { add {} remove {} }
        }
        [STAThread]
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs request) {
                string path=Path.Combine(args[0],new AssemblyName(request.Name).Name+".dll");
                return File.Exists(path)?Assembly.LoadFrom(path):null;
            };
            try { Render(args); return 0; }
            catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Render(string[] args)
        {
            // A hidden handle for offscreen D3D; never show a desktop window.
            using(var window=new Form { ShowInTaskbar=false })
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.Reach,
                new PresentationParameters { DeviceWindowHandle=window.Handle,BackBufferWidth=1024,BackBufferHeight=448,IsFullScreen=false }))
            using(var batch=new SpriteBatch(device))
            using(var target=new RenderTarget2D(device,1024,448))
            using(var white=new Texture2D(device,1,1))
            {
                var services=new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService),new DeviceService { GraphicsDevice=device });
                using(var content=new ContentManager(services,args[0]))
                {
                    var texture=content.Load<Texture2D>("Content/king/base");
                    var layer=new WarpImage.Layer { Texture=texture,Source=new Rectangle(224,48,48,48),Center=new Vector2(.5f,1),Tint=Color.White };
                    var idle=new WarpImage.Layer { Texture=texture,Source=new Rectangle(224,0,48,48),Center=new Vector2(.5f,1),Tint=Color.White };
                    WarpImage.ReadPixels(idle);
                    WarpImage.ReadPixels(layer); white.SetData(new[]{Color.White});
                    Game1.spriteBatch=batch;
                    device.SetRenderTarget(target); device.Clear(new Color(14,20,29));
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    for(int row=0;row<4;row++) for(int col=0;col<8;col++)
                    {
                        var anchor=new Vector2(col*128+64,row*112+100);
                        var pose=row%2==1?idle:layer;
                        PixelPath[] paths=null;
                        if(row>=2)
                        {
                            var floor=new Rectangle((int)anchor.X-48,(int)anchor.Y,96,4);
                            var wall=new Rectangle((int)anchor.X+17,(int)anchor.Y-45,3,45);
                            var screens=new[]{new LevelScreen(0,new IBlock[]{new BoxBlock(floor),new BoxBlock(wall)},new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)};
                            paths=WarpImage.PrepareLayer(pose,SpriteEffects.None,screens,anchor);
                            batch.Draw(white,floor,new Color(60,69,81)); batch.Draw(white,wall,new Color(60,69,81));
                        }
                        WarpVisual.DrawLayer(pose,SpriteEffects.None,anchor,col/7f,row%2==1,paths);
                    }
                    batch.End(); device.SetRenderTarget(null);
                    Directory.CreateDirectory(args[1]);
                    using(var output=File.Create(Path.Combine(args[1],"matrix-warp-contact.png"))) target.SaveAsPng(output,1024,448);
                    // Pixel-for-pixel endpoint comparisons against native Sprite.Draw.
                    foreach(var flip in new[]{SpriteEffects.None,SpriteEffects.FlipHorizontally})
                    foreach(var pose in new[]{layer,idle})
                    {
                        var native=Sprite.CreateSpriteWithCenter(texture,pose.Source,pose.Center);
                        device.SetRenderTarget(target); device.Clear(Color.Transparent);
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        native.Draw(new Vector2(64,100),flip); batch.End(); device.SetRenderTarget(null);
                        var expected=new Color[1024*448]; target.GetData(expected);
                        foreach(bool arriving in new[]{false,true})
                        {
                            device.SetRenderTarget(target); device.Clear(Color.Transparent);
                            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                            WarpVisual.DrawLayer(pose,flip,new Vector2(64,100),arriving?1:0,arriving); batch.End(); device.SetRenderTarget(null);
                            var actual=new Color[expected.Length]; target.GetData(actual);
                            for(int i=0;i<actual.Length;i++) if(actual[i]!=expected[i]) throw new Exception("Native sprite endpoint mismatch: "+flip+"/"+arriving+" at "+i);
                        }
                    }
                    // Mid-scatter must contain all three primary-colour families,
                    // including from fully black opaque texels (not just tinting).
                    using(var black=new Texture2D(device,12,12))
                    {
                        var blackPixels=new Color[144]; for(int i=0;i<144;i++) blackPixels[i]=Color.Black;
                        black.SetData(blackPixels);
                        var blackLayer=new WarpImage.Layer { Texture=black,Source=new Rectangle(0,0,12,12),Center=Vector2.Zero,Tint=Color.White };
                        device.SetRenderTarget(target); device.Clear(Color.Transparent);
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                        WarpVisual.DrawLayer(blackLayer,SpriteEffects.None,new Vector2(64,100),.6f,false);
                        batch.End(); device.SetRenderTarget(null);
                        var rendered=new Color[1024*448]; target.GetData(rendered);
                        int red=0,green=0,blue=0;
                        foreach(var colour in rendered)
                        {
                            if(colour.R>colour.G*2 && colour.R>colour.B*2) red++;
                            if(colour.G>colour.R*2 && colour.G>colour.B*2) green++;
                            if(colour.B>colour.R*2 && colour.B>colour.G*2) blue++;
                        }
                        if(red<8 || green<8 || blue<8) throw new Exception("RGB palette lost a primary colour on black sprite texels");
                    }
                    Console.WriteLine("[OK] Actual GPU sprite fragments: native endpoint pixel equality, both facing directions; offscreen preview saved");
                }
            }
        }
    }
}
