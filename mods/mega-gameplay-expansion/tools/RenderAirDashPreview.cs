using System;
using System.IO;
using System.Windows.Forms;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal static class RenderAirDashPreview
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
            AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs request) {
                var path=Path.Combine(args[0],new System.Reflection.AssemblyName(request.Name).Name+".dll");
                return File.Exists(path)?System.Reflection.Assembly.LoadFrom(path):null;
            };
            try { Render(args); return 0; } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Render(string[] args)
        {
            using(var window=new Form { ShowInTaskbar=false })
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.Reach,
                new PresentationParameters { DeviceWindowHandle=window.Handle,BackBufferWidth=960,BackBufferHeight=384,IsFullScreen=false }))
            using(var batch=new SpriteBatch(device))
            using(var target=new RenderTarget2D(device,960,384))
            {
                var services=new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService),new DeviceService { GraphicsDevice=device });
                using(var content=new ContentManager(services,args[0]))
                {
                    var texture=content.Load<Texture2D>("Content/king/base");
                    var sprite=Sprite.CreateSpriteWithCenter(texture,new Rectangle(224,48,48,48),new Vector2(.5f,1));
                    Game1.spriteBatch=batch;
                    device.SetRenderTarget(target); device.Clear(new Color(13,18,28));
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,Matrix.CreateScale(2));
                    float[] times={0,.033f,.067f,.1f,.15f,.23f};
                    for(int row=0;row<3;row++) for(int col=0;col<times.Length;col++)
                    {
                        int direction=row==1?-1:1;
                        float age=times[col], travel=Math.Min(30,age*360);
                        if(row==2) travel=Math.Min(12,travel);
                        bool ended=travel>=(row==2?12:30);
                        float endAge=Math.Max(0,age-(row==2?12:30)/360f);
                        var origin=new Vector2(col*80+(direction>0?13:48),row*64+24);
                        var end=origin+new Vector2(direction*travel,0);
                        var flip=direction>0?SpriteEffects.None:SpriteEffects.FlipHorizontally;
                        var image=new WarpImage(sprite,sprite,flip);
                        var echoes=new System.Collections.Generic.List<AirDashVisual.Echo>();
                        for(int tick=0;tick<=5;tick++) if(tick*6<=travel)
                            echoes.Add(new AirDashVisual.Echo { Position=origin+new Vector2(direction*tick*6,0),Age=Math.Max(0,age-tick/60f) });
                        AirDashVisual.DrawEffect(image,echoes.ToArray(),origin,end,direction,age,ended,endAge,row==2 && ended);
                        sprite.Draw(end+new Vector2(9,26),flip);
                        if(row==2) batch.Draw(WarpVisual.WhitePixel(device),new Rectangle((int)origin.X+30,(int)origin.Y-12,2,43),new Color(62,71,88));
                    }
                    batch.End(); device.SetRenderTarget(null);
                    Directory.CreateDirectory(args[1]);
                    using(var stream=File.Create(Path.Combine(args[1],"air-dash-contact.png"))) target.SaveAsPng(stream,960,384);
                    var pixels=new Color[960*384]; target.GetData(pixels);
                    int cyan=0,violet=0;
                    foreach(var p in pixels) { if(p.G>p.R*1.5f && p.B>p.R*1.5f) cyan++; if(p.B>p.G*1.3f && p.R>p.G*1.2f) violet++; }
                    if(cyan<30 || violet<10) throw new Exception("Dash trail palette missing from GPU output");
                    Console.WriteLine("[OK] Air Dash native layered sprite + GPU trail preview, both directions and wall impact");
                }
            }
        }
    }
}
