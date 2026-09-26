using System;
using System.IO;
using System.Windows.Forms;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal static class RenderWorkshopPreview
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
            try { Render(args); return 0; }
            catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Render(string[] args)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
            // Native textures/effect code, offscreen only; no game or visible window.
            using(var window=new Form { ShowInTaskbar=false })
            using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.Reach,
                new PresentationParameters { DeviceWindowHandle=window.Handle,BackBufferWidth=256,BackBufferHeight=256,IsFullScreen=false }))
            using(var batch=new SpriteBatch(device))
            using(var target=new RenderTarget2D(device,256,256))
            {
                var services=new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService),new DeviceService { GraphicsDevice=device });
                using(var content=new ContentManager(services,args[0]))
                {
                    var texture=content.Load<Texture2D>("Content/king/base");
                    var king=new KingSprites(texture).regular;
                    var idle=king.GetSprite(Regular.SpriteKey.idle);
                    var flying=king.GetSprite(Regular.SpriteKey.jump_up);
                    Game1.spriteBatch=batch;
                    device.SetRenderTarget(target); device.Clear(new Color(2,5,13));
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,
                        Matrix.CreateScale(3)*Matrix.CreateTranslation(73,174,0));
                    DrawDissolve(new WarpImage(idle,idle,SpriteEffects.None));
                    batch.End();
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,null,
                        Matrix.CreateScale(3)*Matrix.CreateTranslation(186,235,0));
                    var end=new Vector2(-9,-26); var origin=end-new Vector2(31,0);
                    var image=new WarpImage(flying,flying,SpriteEffects.None);
                    var echoes=new[] {
                        new AirDashVisual.Echo { Position=end-new Vector2(20,0),Age=.14f },
                        new AirDashVisual.Echo { Position=end-new Vector2(12,0),Age=.09f },
                        new AirDashVisual.Echo { Position=end-new Vector2(6,0),Age=.05f } };
                    AirDashVisual.DrawEffect(image,echoes,origin,end,1,.12f,false,0,false);
                    flying.Draw(Vector2.Zero);
                    batch.End(); device.SetRenderTarget(null);
                    using(var stream=new MemoryStream())
                    {
                        target.SaveAsPng(stream,256,256); stream.Position=0;
                        using(var bitmap=new System.Drawing.Bitmap(stream))
                        {
                            Title(bitmap,Path.Combine(args[0],"Content/font/ttf_litter_lover2_bold.ttf"));
                            bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                            using(var large=new System.Drawing.Bitmap(1024,1024))
                            using(var graphics=System.Drawing.Graphics.FromImage(large))
                            {
                                graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                                graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
                                graphics.DrawImage(bitmap,new System.Drawing.Rectangle(0,0,1024,1024));
                                large.Save(args[2],System.Drawing.Imaging.ImageFormat.Png);
                            }
                        }
                    }
                }
            }
            if(new FileInfo(args[1]).Length>=34*1024) throw new Exception("Workshop card exceeds 34 KiB");
            Console.WriteLine("[OK] Native idle/dash Workshop card: "+args[1]+" ("+new FileInfo(args[1]).Length+" bytes)");
        }
        private static void DrawDissolve(WarpImage image)
        {
            // Cover illustration: stagger the native particle phase across the
            // standing silhouette so the pose and disassembly read in one still.
            foreach(var layer in image.Layers)
            {
                WarpImage.ReadPixels(layer);
                var white=WarpVisual.WhitePixel(layer.Texture.GraphicsDevice);
                var origin=-new Vector2(layer.Source.Width,layer.Source.Height)*layer.Center;
                for(int y=0;y<layer.Source.Height;y++) for(int x=0;x<layer.Source.Width;x++)
                {
                    var pixel=layer.Pixels[y*layer.Source.Width+x]; if(pixel.A==0) continue;
                    float progress=MathHelper.Clamp((x-20)/17f,0,.82f);
                    var motion=MatrixPixels.At(x,y,layer.Source.Width,layer.Source.Height,progress,false,layer.ScatterCenter);
                    var point=(origin+new Vector2(x,y)+motion.Offset).ToPoint();
                    var rect=new Rectangle(point.X,point.Y,1,1);
                    float rgb=Math.Min(1,motion.Scatter*5);
                    if(rgb<1) Game1.spriteBatch.Draw(layer.Texture,rect,new Rectangle(layer.Source.X+x,layer.Source.Y+y,1,1),Color.White*(motion.Alpha*(1-rgb)));
                    if(rgb>0) Game1.spriteBatch.Draw(white,rect,MatrixPixels.Colour(x,y)*(motion.Alpha*rgb*pixel.A/255f));
                }
            }
        }
        private static void Title(System.Drawing.Bitmap bitmap,string fontPath)
        {
            using(var fonts=new System.Drawing.Text.PrivateFontCollection())
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
            {
                fonts.AddFontFile(fontPath);
                graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                TitleLine(graphics,fonts.Families[0],"MEGA GAMEPLAY",25,13);
                TitleLine(graphics,fonts.Families[0],"EXPANSION",31,40);
            }
        }
        private static void TitleLine(System.Drawing.Graphics graphics,System.Drawing.FontFamily family,string text,float size,float top)
        {
            using(var font=new System.Drawing.Font(family,size,System.Drawing.FontStyle.Regular,System.Drawing.GraphicsUnit.Pixel))
            using(var format=new System.Drawing.StringFormat())
            using(var ink=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255,232,177)))
            using(var shadow=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(61,42,24)))
            {
                format.FormatFlags=System.Drawing.StringFormatFlags.NoWrap|System.Drawing.StringFormatFlags.MeasureTrailingSpaces;
                format.SetMeasurableCharacterRanges(new[]{new System.Drawing.CharacterRange(0,text.Length)});
                var regions=graphics.MeasureCharacterRanges(text,font,new System.Drawing.RectangleF(0,0,512,128),format);
                var box=regions[0].GetBounds(graphics); regions[0].Dispose();
                if(box.Width>236) throw new Exception("Title exceeds safe area: "+text);
                float x=(256-box.Width)/2-box.X, y=top-box.Y;
                graphics.DrawString(text,font,shadow,x+2,y+2,format);
                graphics.DrawString(text,font,ink,x,y,format);
            }
        }
    }
}
