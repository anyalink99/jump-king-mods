using System;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    // Action-time capture, never per frame. Only CPU pixels survive the call,
    // so saved Warp plans do not retain transient render targets or GPU leases.
    internal static class SpriteCapture
    {
        private const int Size=128;
        private static readonly FieldInfo Begun=typeof(SpriteBatch).GetField("_beginCalled",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo VertexBindings=typeof(GraphicsDevice).GetField("_vertexBuffers",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly MethodInfo ReadBindings=VertexBindings==null ? null : VertexBindings.FieldType.GetMethod("Get",
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
        internal static WarpImage.Layer Capture(Sprite sprite,SpriteEffects flip,Vector2 screenAnchor)
        {
            var previous=Game1.spriteBatch;
            if(previous==null || Begun==null || ReadBindings==null || (bool)Begun.GetValue(previous))
                throw new InvalidOperationException("Sprite capture requires an idle render batch");
            var device=previous.GraphicsDevice;
            // Do not unbind a caller's discard-content target or interrupt its
            // drawing. Warp normally captures between updates and rendering.
            var targets=device.GetRenderTargets();
            foreach(var binding in targets)
                if(((RenderTarget2D)binding.RenderTarget).RenderTargetUsage!=RenderTargetUsage.PreserveContents)
                    throw new InvalidOperationException("Sprite capture cannot preserve the active render target");
            var viewport=device.Viewport;var scissor=device.ScissorRectangle;
            var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
            var vertices=(VertexBufferBinding[])ReadBindings.Invoke(VertexBindings.GetValue(device),null);var indices=device.Indices;
            var textures=new Texture[16];var samplers=new SamplerState[16];
            for(int i=0;i<16;i++){textures[i]=device.Textures[i];samplers[i]=device.SamplerStates[i];}
            var anchor=new Vector2(64,96);
            // Translate by whole pixels only. Moving a fractional screen anchor
            // onto an integer tile anchor changes rasterization by one pixel.
            var screenPixel=new Vector2((float)Math.Floor(screenAnchor.X),(float)Math.Floor(screenAnchor.Y));
            var pixels=new Color[Size*Size];
            using(var target=new RenderTarget2D(device,Size,Size,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents))
            using(var batch=new SpriteBatch(device))
            {
                try
                {
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);
                    Game1.spriteBatch=batch;
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,
                        RasterizerState.CullNone,null,Matrix.CreateTranslation(anchor.X-screenPixel.X,anchor.Y-screenPixel.Y,0));
                    // Keep actual screen coordinates for world-space shaders;
                    // the batch transform alone relocates the capture to its tile.
                    sprite.Draw(screenAnchor,flip);
                    if((bool)Begun.GetValue(batch))batch.End();
                    device.SetRenderTarget(null);target.GetData(pixels);
                }
                finally
                {
                    // Do not flush queued foreign work after a failed Draw.
                    Game1.spriteBatch=previous;
                    device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;
                    device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;
                    device.SetVertexBuffers(vertices);device.Indices=indices;
                    for(int i=0;i<16;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
                }
            }
            int left=Size,top=Size,right=-1,bottom=-1;
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(pixels[y*Size+x].A!=0)
            {left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            if(right<left)return new WarpImage.Layer {Source=new Rectangle(0,0,1,1),Pixels=new Color[1],Tint=Color.White};
            if(left==0 || top==0 || right==Size-1 || bottom==Size-1)
                throw new InvalidOperationException("Custom sprite exceeds the bounded capture canvas");
            int width=right-left+1,height=bottom-top+1;var cropped=new Color[width*height];
            for(int y=0;y<height;y++)Array.Copy(pixels,(top+y)*Size+left,cropped,y*width,width);
            return new WarpImage.Layer {Texture=WarpVisual.WhitePixel(device),Source=new Rectangle(0,0,width,height),
                CapturedOffset=new Vector2(left,top)-anchor,
                Center=new Vector2((anchor.X-left)/width,(anchor.Y-top)/height),Tint=Color.White,Pixels=cropped,Rasterized=true};
        }
    }
}
