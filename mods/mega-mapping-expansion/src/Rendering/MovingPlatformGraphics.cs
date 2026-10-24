using System;
using System.Collections.Generic;
using JKRuntime;
using System.Linq;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal sealed partial class NativeMovingPlatforms
    {
        private NativeAssets images;
        private NativeAssets defaultImages;
        private string GraphicsIdentity{get{return images.ContentIdentity+(defaultImages==null?"":defaultImages.ContentIdentity);}}
        private void PrepareGraphics(string root)
        {
            var profiles=platforms.Select(p=>p.Profile).Distinct().Where(p=>p.Texture!=""&&p.Texture!="none").ToArray();
            images=resources.Own(new NativeAssets(Game1.instance.GraphicsDevice,root,profiles.Select(p=>new AssetData{Id=p.Id,Path=p.Texture,Columns=p.Columns,Rows=p.Rows,Frames=p.Frames,Fps=p.Fps}).ToArray()));
            if(platforms.Any(p=>p.Profile.Texture==""))
                defaultImages=resources.Own(new NativeAssets(Game1.instance.GraphicsDevice,PackageHost.GetDataDirectory(typeof(ModEntry).Assembly),new[]{new AssetData{Id="default",Path="assets/platforms/default.png"}}));
        }
        private static void Draw(LevelScreen __instance)
        {
            var owner=Current;if(owner==null||owner.images==null)return;
            owner.DrawScreen(__instance.GetIndex0());
        }
        internal void DrawScreen(int screen)
        {
            if(images==null)return;
            foreach(var platform in platforms)
            {
                if(platform.Profile.Texture=="none")continue;
                var image=platform.Profile.Texture==""?defaultImages.Bitmaps["default"]:images.Bitmaps[platform.Profile.Id];
                Texture2D texture=image.Texture;Rectangle source=image.Frame((int)(platform.Motion.State.Time*platform.Profile.Fps)%platform.Profile.Frames);
                foreach(var part in platform.Parts)
                {
                    var bounds=part.Bounds;bounds.Y+=screen*360;if(bounds.Bottom<=0||bounds.Top>=360)continue;
                    if(platform.Profile.Fit=="stretch")Game1.spriteBatch.Draw(texture,bounds,source,Color.White);
                    else if(platform.Profile.Fit=="native")Game1.spriteBatch.Draw(texture,new Vector2(bounds.X,bounds.Y),source,Color.White);
                    else for(int y=0;y<bounds.Height;y+=source.Height)for(int x=0;x<bounds.Width;x+=source.Width)
                    {
                        int width=Math.Min(source.Width,bounds.Width-x),height=Math.Min(source.Height,bounds.Height-y);
                        Game1.spriteBatch.Draw(texture,new Rectangle(bounds.X+x,bounds.Y+y,width,height),new Rectangle(source.X,source.Y,width,height),Color.White);
                    }
                }
                string label=platform.Definition.Label!=""?platform.Definition.Label:platform.Profile.Label;
                var fonts=Game1.instance.contentManager.font;var font=fonts==null?null:fonts.MenuFontSmall??fonts.MenuFont;
                if(label!=""&&font!=null)
                {
                    var box=platform.Parts.Select(p=>p.Bounds).Aggregate(Rectangle.Union);int y=box.Bottom+screen*360+3;
                    if(y>=0&&y<350)
                    {
                        label=new string(label.Select(c=>font.Characters.Contains(c)?c:font.DefaultCharacter??' ').ToArray());
                        var size=font.MeasureString(label);float scale=Math.Min(1,464/Math.Max(1,size.X));
                        var at=new Vector2((float)Math.Round(Math.Max(8,Math.Min(472-size.X*scale,box.Center.X-size.X*scale/2))),y);
                        Game1.spriteBatch.DrawString(font,label,at+new Vector2(1,1),Color.Black,0,Vector2.Zero,scale,SpriteEffects.None,0);
                        Game1.spriteBatch.DrawString(font,label,at,new Color(210,226,232),0,Vector2.Zero,scale,SpriteEffects.None,0);
                    }
                }
            }
        }
    }
}
