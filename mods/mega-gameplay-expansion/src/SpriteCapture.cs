using System;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    // Action-time capture, never per frame. only CPU pixels survive the call,
    // so saved Warp plans don't keep transient render targets or GPU leases
    internal static class SpriteCapture
    {
        internal static WarpImage.Layer Capture(Sprite sprite,SpriteEffects flip,Vector2 screenAnchor,string pose=null)
        {
            var frame=JKRuntime.Presentation.PlayerAppearance.FromSprite(sprite,screenAnchor-Camera.Offset,flip,pose);
            var pixels=JKRuntime.Presentation.AppearanceCapture.Read(frame,128,false,screenAnchor);
            return new WarpImage.Layer {Texture=WarpVisual.WhitePixel(Game1.spriteBatch.GraphicsDevice),
                Source=new Rectangle(0,0,pixels.Width,pixels.Height),Pixels=pixels.Pixels,
                CapturedOffset=pixels.Offset.ToVector2(),Center=new Vector2(-pixels.Offset.X/(float)pixels.Width,-pixels.Offset.Y/(float)pixels.Height),
                Tint=Color.White,Rasterized=true};
        }
    }
}
