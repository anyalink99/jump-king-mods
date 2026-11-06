using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using JKRuntime.Particles;

internal static partial class WardrobeTests
{
    private static void RuntimeParticleBatchGraphics(GraphicsDevice device,SpriteBatch batch,RenderTarget2D outer)
    {
        using(var target=new RenderTarget2D(device,64,64))using(var pixel=new Texture2D(device,1,1))using(var commands=new ParticleBatch(3))
        {
            pixel.SetData(new[]{Color.White});
            foreach(var blend in new[]{BlendState.AlphaBlend,BlendState.Additive})
            {
                Color[] reference=null;
                for(int mode=0;mode<2;mode++)
                {
                    device.SetRenderTarget(target);device.Clear(new Color(12,8,16));
                    batch.Begin(SpriteSortMode.Deferred,blend,SamplerState.PointClamp,null,null,null,Matrix.CreateTranslation(3,5,0));
                    for(int i=0;i<3;i++)
                    {
                        var position=new Vector2(16+i*3,18+i*3);var color=new Color(i==0?200:20,i==1?180:10,i==2?150:20)*.5f;
                        if(mode==0)batch.Draw(pixel,position,new Rectangle(0,0,1,1),color,.1f*i,Vector2.Zero,new Vector2(20),SpriteEffects.None,0);
                        else commands.Add(pixel,position,new Rectangle(0,0,1,1),color,.1f*i,Vector2.Zero,new Vector2(20));
                    }
                    if(mode==1)
                    {Check(!commands.Add(pixel,Vector2.Zero,new Rectangle(0,0,1,1),Color.Red,0,Vector2.Zero,Vector2.One),"Particle render budget rejects overflow without flushing");commands.Flush(batch);}
                    batch.Draw(pixel,new Rectangle(24,24,5,5),Color.Cyan);batch.End();device.SetRenderTarget(null);
                    var pixels=new Color[4096];target.GetData(pixels);
                    if(mode==0)reference=pixels;else Check(reference.SequenceEqual(pixels)&&commands.Count==0,"Runtime particle batch preserves translucent order, transform, blend and following sprites");
                }
            }
        }
        device.SetRenderTarget(outer);
    }
}
