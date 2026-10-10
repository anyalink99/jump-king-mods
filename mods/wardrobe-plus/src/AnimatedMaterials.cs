using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus.Advanced
{
    internal static class AnimatedMaterials
    {
        internal static void Prepare(PresentationAssets assets,AnimationBinding binding)
        {
            if(binding.Material==MaterialKind.Original)return;
            var cache=new Dictionary<Texture2D,Color[]>();
            foreach(var clip in binding.Definition.clips)foreach(var frame in clip.frames)
            {
                if(binding.MaterialFrames.ContainsKey(frame))continue;
                var source=assets.Texture(binding.Package,clip.texture);Color[] original;
                if(!cache.TryGetValue(source,out original)){original=new Color[source.Width*source.Height];source.GetData(original);cache.Add(source,original);}
                int w=frame.width,h=frame.height;var data=new Color[w*h];var mask=binding.Material==MaterialKind.Cosmic?new Color[w*h]:null;
                Func<int,int,Color> sample=(x,y)=>x<0||y<0||x>=w||y>=h?Color.Transparent:original[(frame.y+y)*source.Width+frame.x+x];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    var input=sample(x,y);if(input.A==0)continue;
                    bool edge=sample(x-1,y).A==0||sample(x+1,y).A==0||sample(x,y-1).A==0||sample(x,y+1).A==0;
                    data[y*w+x]=MaterialBaker.Shade(binding.Material,input,x,y,edge,(dx,dy)=>Color.Transparent);
                    if(mask!=null)mask[y*w+x]=new Color(edge?255:0,(int)Math.Round((input.R*.2126+input.G*.7152+input.B*.0722)*255/input.A),0,(int)input.A);
                }
                var texture=Own(assets,w,h);texture.SetData(data);
                Sprite sprite=Sprite.CreateSpriteWithCenter(texture,new Rectangle(0,0,w,h),new Vector2(frame.originX/w,frame.originY/h));
                if(mask!=null){var map=Own(assets,w,h);map.SetData(mask);sprite=new CosmicSprite(sprite,map,sprite.source,Point.Zero);}
                binding.MaterialFrames.Add(frame,sprite);
            }
        }
        private static Texture2D Own(PresentationAssets assets,int width,int height)
        {
            assets.TextureBytes+=(long)width*height*4;
            ManifestIO.Require(assets.TextureBytes<=128*1024*1024,"Selected textures exceed 128 MiB");
            var texture=new Texture2D(Game1.spriteBatch.GraphicsDevice,width,height);
            assets.Textures.Add("@material/"+assets.Textures.Count,texture);return texture;
        }
    }
}
