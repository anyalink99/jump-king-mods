using System;
using System.IO;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.JKMemory.KingSpriteLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private static void ArtMasks(ContentManager content)
        {
            var mask=content.Load<Texture2D>("Content/king/tunic");var pixels=new Color[mask.Width*mask.Height];mask.GetData(pixels);
            int[] poseX={0,1,2,3,0,1,2,0,3,1,2,3,0},poseY={0,0,0,0,1,1,1,2,1,2,2,2,3};
            using(var stream=typeof(EquipmentArt).Assembly.GetManifestResourceStream("Metroidvania.Art.armor"))
            using(var armor=new System.Drawing.Bitmap(stream)){
                Check(armor.Width==384&&armor.Height==192,"Armor atlas matches the native pose layout");
                for(int body=0;body<2;body++)for(int pose=0;pose<13;pose++){
                    int opaque=0;
                    for(int y=0;y<48;y++)for(int x=0;x<48;x++){
                        var c=armor.GetPixel(body*192+(pose%4)*48+x,(pose/4)*48+y);
                        if(c.A==0){Check(c.R==0&&c.G==0&&c.B==0,"Transparent armor is premultiplied-safe");continue;}
                        opaque++;Check(pixels[(poseY[pose]*48+y)*mask.Width+224+poseX[pose]*48+x].A>96,"Armor cannot paint outside the native Tunic silhouette");
                    }
                    Check(opaque>0,"Armor covers native pose "+pose);
                }
            }
            foreach(string name in new[]{"icons","weapons"})using(var stream=typeof(EquipmentArt).Assembly.GetManifestResourceStream("Metroidvania.Art."+name))using(var image=new System.Drawing.Bitmap(stream)){
                for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var c=image.GetPixel(x,y);Check(c.A==0||c.A==255,"Gear uses crisp alpha");if(c.A==0)Check(c.R==0&&c.G==0&&c.B==0,"Transparent gear has no white fringe");}
            }
        }
        private static void PoseSheet(GraphicsDevice device,ContentManager content,string scratch)
        {
            using(var target=new RenderTarget2D(device,936,240))using(var batch=new SpriteBatch(device)){
                Game1.spriteBatch=batch;using(var art=EquipmentArt.Acquire()){
                    device.SetRenderTarget(target);device.Clear(new Color(29,34,40));batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                    for(int body=1;body<=2;body++)for(byte pose=0;pose<13;pose++){
                        var anchor=new Vector2(pose*72+36,body*120-15);var native=Game1.instance.contentManager.playerSprites._CurrentSprites.regular.GetSprite((Regular.SpriteKey)pose);
                        foreach(var layer in PlayerAppearance.Layers(native))if(layer.texture!=null)Game1.spriteBatch.Draw(layer.texture,anchor-layer.source.Size.ToVector2()*layer.center*2,layer.source,Color.White,0,Vector2.Zero,2,SpriteEffects.None,0);
                        EquipmentArt.Draw(anchor,1,new[]{(byte)1,(byte)0,(byte)CombatAction.Idle,(byte)0,(byte)body,pose},Color.White,2);
                    }
                    batch.End();device.SetRenderTarget(null);var colors=new Color[936*240];target.GetData(colors);
                    using(var image=new System.Drawing.Bitmap(936,240)){
                        for(int y=0;y<240;y++)for(int x=0;x<936;x++){var c=colors[y*936+x];image.SetPixel(x,y,System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B));}
                        image.Save(Path.Combine(scratch,"king-poses.png"),System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            }
            Console.WriteLine("[OK] Native King identity and 26 fitted armor poses");
        }
    }
}
