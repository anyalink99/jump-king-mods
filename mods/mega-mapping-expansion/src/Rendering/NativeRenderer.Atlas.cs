using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MegaMappingExpansion.Api;
using JumpKing;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeRenderer
    {
        private sealed class AtlasState
        {
            internal int Current,Previous;
            internal double Started;
            internal bool Initialized;
            internal RenderTarget2D BlendTarget;
        }
        private readonly Dictionary<string,AtlasState> atlasStates=new Dictionary<string,AtlasState>(StringComparer.Ordinal);
        private BlendState coverageAdd;
        private double SpriteTime(string id){return Text(id,"clock")=="presentation"?scene.PresentationTime:scene.GameplayTime;}
        private void PrepareAtlases()
        {
            coverageAdd=resources.Own(new BlendState{ColorSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.One,ColorDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.One,AlphaSourceBlend=Microsoft.Xna.Framework.Graphics.Blend.One,AlphaDestinationBlend=Microsoft.Xna.Framework.Graphics.Blend.One});
            long bytes=0;
            foreach(var component in scene.Components.Values)
            {
                if(component.Descriptor.Kind!="sprite")continue;var state=new AtlasState();string asset=Text(component.Id,"asset");
                if(asset!=""&&Text(component.Id,"transition")=="crossfade")
                {
                    var bitmap=assets.Bitmaps[asset];bytes+=(long)bitmap.FrameWidth*bitmap.FrameHeight*4;if(bytes>64L*1024*1024)throw new System.IO.InvalidDataException("Atlas transition targets exceed 64 MiB");
                    state.BlendTarget=resources.Own(new RenderTarget2D(device,bitmap.FrameWidth,bitmap.FrameHeight,false,SurfaceFormat.Color,DepthFormat.None));
                }
                atlasStates.Add(component.Id,state);
            }
            TickAtlases();
        }
        private void TickAtlases()
        {
            foreach(var pair in atlasStates)
            {
                string id=pair.Key,asset=Text(id,"asset");if(asset=="")continue;var bitmap=assets.Bitmaps[asset];double value=Number(id,"frame");string playback=Text(id,"playback");
                if(playback!="manual")value+=SpriteTime(id)*bitmap.Definition.Fps;
                int frame;
                if(playback=="once"||playback=="manual")frame=Math.Max(0,Math.Min(bitmap.Frames-1,(int)Math.Floor(value)));
                else if(playback=="pingpong")
                {int period=Math.Max(1,(bitmap.Frames-1)*2);int phase=((int)Math.Floor(value)%period+period)%period;frame=phase<bitmap.Frames?phase:period-phase;}
                else frame=((int)Math.Floor(value)%bitmap.Frames+bitmap.Frames)%bitmap.Frames;
                var state=pair.Value;if(!state.Initialized){state.Initialized=true;state.Current=state.Previous=frame;}
                else if(state.Current!=frame){state.Previous=state.Current;state.Current=frame;state.Started=SpriteTime(id);}
            }
        }
        private void DrawAnimatedSprite(CompiledComponent component,ComponentPose pose,string material,PreparedBitmap bitmap)
        {
            Texture2D texture=material!=""&&Text(material,"channel")=="alpha"?masks[Text(component.Id,"asset")]:bitmap.Texture;
            string id=component.Id;var state=atlasStates[id];var origin=Value(id,"origin");var topLeft=SceneValue.Vector(-origin.X,-origin.Y);double duration=Number(id,"frameTransition");string transition=Text(id,"transition");
            double t=duration<=0?1:Math.Max(0,Math.Min(1,(SpriteTime(id)-state.Started)/duration));t=t*t*(3-2*t);
            if(state.Current==state.Previous||t>=1||transition=="none")
            {DrawQuad(texture,bitmap.Frame(state.Current),pose,topLeft,SceneValue.Vector(bitmap.FrameWidth,bitmap.FrameHeight),Tint(material),material,null);return;}
            if(transition=="slide")
            {
                int shift=(int)Math.Round(t*bitmap.FrameHeight);var old=bitmap.Frame(state.Previous);old.Y+=shift;old.Height-=shift;
                if(old.Height>0)DrawQuad(texture,old,pose,topLeft,SceneValue.Vector(bitmap.FrameWidth,old.Height),Tint(material),material,null);
                var next=bitmap.Frame(state.Current);next.Height=shift;
                if(shift>0)DrawQuad(texture,next,pose,SceneValue.Vector(topLeft.X,topLeft.Y+bitmap.FrameHeight-shift),SceneValue.Vector(bitmap.FrameWidth,shift),Tint(material),material,null);
            }
            else
            {
                FlushQuads();
                var targets=device.GetRenderTargets();var viewport=device.Viewport;bool saved=false;
                try
                {
                    saved=SaveBorrowedFrame(targets);
                    device.SetRenderTarget(state.BlendTarget);device.Clear(Color.Transparent);
                    Game1.spriteBatch.Begin(SpriteSortMode.Deferred,coverageAdd,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                    try
                    {
                        Game1.spriteBatch.Draw(texture,new Rectangle(0,0,bitmap.FrameWidth,bitmap.FrameHeight),bitmap.Frame(state.Previous),new Color((float)(1-t),(float)(1-t),(float)(1-t),(float)(1-t)));
                        Game1.spriteBatch.Draw(texture,new Rectangle(0,0,bitmap.FrameWidth,bitmap.FrameHeight),bitmap.Frame(state.Current),new Color((float)t,(float)t,(float)t,(float)t));
                    }
                    finally{Game1.spriteBatch.End();}
                }
                finally{device.SetRenderTargets(targets);device.Viewport=viewport;RestoreBorrowedFrame(saved);}
                DrawQuad(state.BlendTarget,new Rectangle(0,0,bitmap.FrameWidth,bitmap.FrameHeight),pose,topLeft,SceneValue.Vector(bitmap.FrameWidth,bitmap.FrameHeight),Tint(material),material,null);
            }
        }
    }
}
