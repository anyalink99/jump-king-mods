using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// Render the shipped native-pose texture through the shipped vessel shader
internal static class VesselPreview
{
    [STAThread] private static void Main(string[] args)
    {
        string root=args[0];
        using(var window=new Form{ShowInTaskbar=false})
        using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
            new PresentationParameters{DeviceWindowHandle=window.Handle,BackBufferWidth=256,BackBufferHeight=256,IsFullScreen=false}))
        using(var batch=new SpriteBatch(device))
        using(var texture=Load(device,Path.Combine(root,"wardrobe/animations.png"),true))
        using(var mask=Load(device,Path.Combine(root,"wardrobe/water-mask.png"),false))
        using(var effect=new Effect(device,File.ReadAllBytes(Path.Combine(root,"wardrobe/visor.mgfxo"))))
        using(var target=new RenderTarget2D(device,256,256))
        {
            var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
            int left=48,top=48,right=0,bottom=0;
            for(int y=0;y<48;y++)for(int x=0;x<48;x++)if(pixels[y*texture.Width+x].A>0)
            {left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            var source=new Rectangle(left,top,right-left+1,bottom-top+1);
            int scale=216/Math.Max(source.Width,source.Height);
            var destination=new Rectangle((256-source.Width*scale)/2,(256-source.Height*scale)/2,source.Width*scale,source.Height*scale);
            effect.Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(0,256,256,0,0,-1));
            effect.Parameters["SpriteTexture"].SetValue(texture);effect.Parameters["Mask"].SetValue(mask);
            effect.Parameters["LiquidLevel"].SetValue(.58f);effect.Parameters["LiquidTilt"].SetValue(.19f);
            effect.Parameters["LiquidWave"].SetValue(.14f);effect.Parameters["Time"].SetValue(.3f);effect.Parameters["Facing"].SetValue(1f);
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,null,null,effect);
            batch.Draw(texture,destination,source,Color.White);batch.End();device.SetRenderTarget(null);
            var output=new Color[256*256];target.GetData(output);
            using(var image=new System.Drawing.Bitmap(256,256,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                {
                    var c=output[y*256+x];
                    bool expected=destination.Contains(x,y)&&pixels[(source.Y+(y-destination.Y)/scale)*texture.Width+source.X+(x-destination.X)/scale].A>0;
                    if((c.A>0)!=expected)throw new InvalidDataException("Preview changed the native sprite silhouette");
                    image.SetPixel(x,y,c.A==0?System.Drawing.Color.Transparent:System.Drawing.Color.FromArgb(c.A,Math.Min(255,c.R*255/c.A),Math.Min(255,c.G*255/c.A),Math.Min(255,c.B*255/c.A)));
                }
                image.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
            }
            Console.WriteLine("[OK] Native King pose, exact silhouette, integer scale "+scale+"; shipped water shader, transparent 256x256 output");
        }
    }
    private static Texture2D Load(GraphicsDevice device,string path,bool premultiply)
    {
        Texture2D texture;using(var file=File.OpenRead(path))texture=Texture2D.FromStream(device,file);
        if(premultiply){var data=new Color[texture.Width*texture.Height];texture.GetData(data);for(int i=0;i<data.Length;i++){var c=data[i];data[i]=new Color((byte)(c.R*c.A/255),(byte)(c.G*c.A/255),(byte)(c.B*c.A/255),c.A);}texture.SetData(data);}
        return texture;
    }
}
