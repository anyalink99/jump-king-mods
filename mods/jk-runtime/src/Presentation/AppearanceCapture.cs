using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.Presentation
{
    /// <summary>Owned frozen GPU image. No readback until ReadPixels; dispose on the game thread.</summary>
    public sealed class AppearanceImage : IDisposable
    {
        private RenderTarget2D target;
        private readonly Point offset;
        private readonly AppearanceCoverage coverage;
        private readonly string reason;
        internal AppearanceImage(RenderTarget2D value,Point crop,AppearanceCoverage quality,string message)
        {target=value;offset=crop;coverage=quality;reason=message;}
        public bool IsValid {get{return target!=null&&!target.IsDisposed&&!target.IsContentLost;}}
        public void Draw(Vector2 anchor,Color tint)
        {
            RuntimeApi.Kernel.CheckThread();if(!IsValid)return;
            Game1.spriteBatch.Draw(target,new Vector2((float)Math.Floor(anchor.X)+offset.X,(float)Math.Floor(anchor.Y)+offset.Y),tint);
        }
        public void Draw(Vector2 anchor,Color tint,Vector2 scale,float rotation=0)
        {
            RuntimeApi.Kernel.CheckThread();if(!IsValid)return;
            Game1.spriteBatch.Draw(target,anchor,null,tint,rotation,new Vector2(-offset.X,-offset.Y),scale,SpriteEffects.None,0);
        }
        public AppearancePixels ReadPixels()
        {
            RuntimeApi.Kernel.CheckThread();if(!IsValid)throw new ObjectDisposedException("AppearanceImage");
            var data=new Color[target.Width*target.Height];target.GetData(data);
            return AppearanceCapture.Crop(data,target.Width,target.Height,offset,coverage,reason);
        }
        public void Dispose(){RuntimeApi.Kernel.CheckThread();if(target==null)return;target.Dispose();target=null;}
    }

    public static class AppearanceCapture
    {
        private static readonly FieldInfo Begun=typeof(SpriteBatch).GetField("_beginCalled",OwnedPatches.Members);
        private static readonly FieldInfo VertexBindings=typeof(GraphicsDevice).GetField("_vertexBuffers",OwnedPatches.Members);
        private static readonly MethodInfo ReadBindings=VertexBindings==null?null:VertexBindings.FieldType.GetMethod("Get",OwnedPatches.Members,null,Type.EmptyTypes,null);
        /// <summary>Capture at action time with an idle batch. A full bounded surface stays on the GPU.</summary>
        public static AppearanceImage Freeze(AppearanceFrame frame,int size=128,Vector2? screenAnchor=null)
        {
            RuntimeApi.Kernel.CheckThread();if(frame==null)throw new ArgumentNullException("frame");
            if(size<32||size>512)throw new ArgumentOutOfRangeException("size");
            var previous=Game1.spriteBatch;
            if(previous==null||Begun==null||ReadBindings==null||(bool)Begun.GetValue(previous))throw new InvalidOperationException("Appearance capture requires an idle render batch");
            var device=previous.GraphicsDevice;var targets=device.GetRenderTargets();
            foreach(var binding in targets)if(((RenderTarget2D)binding.RenderTarget).RenderTargetUsage!=RenderTargetUsage.PreserveContents)
                throw new InvalidOperationException("Appearance capture cannot preserve the active render target");
            var viewport=device.Viewport;var scissor=device.ScissorRectangle;var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
            var vertices=(VertexBufferBinding[])ReadBindings.Invoke(VertexBindings.GetValue(device),null);var indices=device.Indices;
            var textures=new Texture[16];var samplers=new SamplerState[16];
            for(int i=0;i<16;i++){textures[i]=device.Textures[i];samplers[i]=device.SamplerStates[i];}
            Vector2 screen=screenAnchor??PlayerAppearance.Project(frame);
            var tile=new Point(size/2,size*3/4);
            var pixel=new Vector2((float)Math.Floor(screen.X),(float)Math.Floor(screen.Y));
            var target=new RenderTarget2D(device,size,size,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            bool success=false,oldCapture=frame.IsCapture;
            try {
                using(var batch=new SpriteBatch(device))
                using(RuntimeApi.MeasurePerformance("runtime.appearance.capture"))
                {
                    try {
                        device.SetRenderTarget(target);device.Clear(Color.Transparent);Game1.spriteBatch=batch;frame.IsCapture=true;
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,
                            Matrix.CreateTranslation(tile.X-pixel.X,tile.Y-pixel.Y,0));
                        PlayerAppearance.Draw(frame,screen);if((bool)Begun.GetValue(batch))batch.End();
                    } finally {
                        frame.IsCapture=oldCapture;Game1.spriteBatch=previous;device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;
                        device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;device.SetVertexBuffers(vertices);device.Indices=indices;
                        for(int i=0;i<16;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
                    }
                }
                success=true;return new AppearanceImage(target,new Point(-tile.X,-tile.Y),frame.Coverage,frame.Reason);
            } finally {if(!success)target.Dispose();}
        }
        /// <summary>Detached CPU capture with explicit approximate static fallback. Never changes gameplay eligibility.</summary>
        public static AppearancePixels Read(AppearanceFrame frame,int size=128,bool allowStaticFallback=false,Vector2? screenAnchor=null)
        {
            try {using(var image=Freeze(frame,size,screenAnchor))return image.ReadPixels();}
            catch(Exception error) {
                if(!allowStaticFallback)throw;
                var fallback=ReadStatic(frame.Sprite,size,frame.Facing);fallback.Coverage=AppearanceCoverage.Approximate;fallback.Reason=error.GetBaseException().Message;return fallback;
            }
        }
        internal static AppearancePixels Crop(Color[] data,int width,int height,Point offset,AppearanceCoverage coverage,string reason)
        {
            int left=width,top=height,right=-1,bottom=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(data[y*width+x].A!=0){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            if(right<left)return new AppearancePixels {Width=1,Height=1,Pixels=new Color[1],Offset=Point.Zero,Coverage=coverage,Reason=reason};
            if(left==0||top==0||right==width-1||bottom==height-1)throw new InvalidOperationException("Appearance touches the capture boundary; request a larger canvas");
            int w=right-left+1,h=bottom-top+1;var cropped=new Color[w*h];
            for(int y=0;y<h;y++)Array.Copy(data,(top+y)*width+left,cropped,y*w,w);
            return new AppearancePixels {Width=w,Height=h,Pixels=cropped,Offset=new Point(offset.X+left,offset.Y+top),Coverage=coverage,Reason=reason};
        }
        /// <summary>Texture-field fallback for native static art. It does not claim to reproduce a custom Draw override.</summary>
        public static AppearancePixels ReadStatic(Sprite sprite,int size=512,SpriteEffects facing=SpriteEffects.None)
        {
            RuntimeApi.Kernel.CheckThread();if(size<32||size>512)throw new ArgumentOutOfRangeException("size");
            var data=new Color[size*size];var anchor=new Point(size/2,size*3/4);int layers=0;bool approximate=false;
            Collect(sprite,data,size,anchor,0,facing,ref layers,ref approximate);
            return Crop(data,size,size,new Point(-anchor.X,-anchor.Y),approximate?AppearanceCoverage.Approximate:AppearanceCoverage.Exact,approximate?"Texture-field fallback for a custom sprite":null);
        }
        private static void Collect(Sprite sprite,Color[] canvas,int size,Point anchor,int depth,SpriteEffects facing,ref int count,ref bool approximate)
        {
            if(sprite==null)return;if(depth>8||++count>32)throw new InvalidOperationException("Appearance layer budget exceeded");
            var layers=PlayerAppearance.Layers(sprite);
            if(layers.Length!=1||!ReferenceEquals(layers[0],sprite)){foreach(var layer in layers)Collect(layer,canvas,size,anchor,depth+1,facing,ref count,ref approximate);return;}
            approximate|=sprite.GetType()!=typeof(Sprite);
            if(sprite.texture==null||sprite.source.Width<1||sprite.source.Height<1)return;
            if(sprite.source.Width>size||sprite.source.Height>size)throw new InvalidOperationException("Appearance layer exceeds canvas");
            var pixels=new Color[sprite.source.Width*sprite.source.Height];sprite.texture.GetData(0,sprite.source,pixels,0,pixels.Length);
            var top=(anchor.ToVector2()-sprite.source.Size.ToVector2()*sprite.center).ToPoint();var tint=sprite.GetColor();
            for(int y=0;y<sprite.source.Height;y++)for(int x=0;x<sprite.source.Width;x++)
            {
                var p=pixels[y*sprite.source.Width+x];if(p.A==0)continue;
                int dx=top.X+((facing&SpriteEffects.FlipHorizontally)!=0?sprite.source.Width-1-x:x),dy=top.Y+((facing&SpriteEffects.FlipVertically)!=0?sprite.source.Height-1-y:y);if(dx<0||dy<0||dx>=size||dy>=size)throw new InvalidOperationException("Appearance exceeds canvas");
                p=new Color(p.R*tint.R/255,p.G*tint.G/255,p.B*tint.B/255,p.A*tint.A/255);
                int i=dy*size+dx,inv=255-p.A;var b=canvas[i];canvas[i]=new Color(Math.Min(255,p.R+b.R*inv/255),Math.Min(255,p.G+b.G*inv/255),Math.Min(255,p.B+b.B*inv/255),Math.Min(255,p.A+b.A*inv/255));
            }
        }
    }
}
