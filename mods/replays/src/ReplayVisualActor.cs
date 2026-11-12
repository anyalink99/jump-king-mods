using System;
using JumpKing;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Replays
{
    internal sealed class ReplayVisualActor : IDisposable
    {
        private readonly ReplayData replay;
        private readonly ActorVisuals.Actor actor=ActorVisuals.Create();
        private int last=-1;
        private Sprite current;
        internal ReplayVisualActor(ReplayData value){replay=value;}
        internal Sprite Present(int index,bool cut=false)
        {
            // particle lifetimes are bounded; seeking only rebuilds the recent emitter history
            bool reset=cut||index<last||index>last+8;
            if(reset)last=Math.Max(-1,index-76);
            int first=last+1;
            if(first>index)first=index;
            for(int frame=first;frame<=index;frame++)
            {
                var value=replay.Frames[frame];var items=ReplayAppearanceTrack.AtFrame(replay,frame);var state=ReplayVisualTrack.AtFrame(replay,frame);
                var velocity=frame==0?Vector2.Zero:value.Position-replay.Frames[frame-1].Position;
                bool relocated=velocity.LengthSquared()>128*128;if(relocated)velocity=Vector2.Zero;
                velocity=new Vector2(Math.Max(-100,Math.Min(100,velocity.X)),Math.Max(-100,Math.Min(100,velocity.Y)));
                var facing=(value.Flags&ReplayFrameFlags.FacingLeft)!=0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
                current=actor.Present(state,ReplayGhostRenderer.ResolveSprite(value.Pose,items),state.Length>0?ReplayGhostRenderer.ResolveSprite(ReplayPose.Charge,items):null,
                    new ActorVisualFrame(frame/60d,value.DrawAnchor,velocity,facing,relocated||reset&&frame==first));
            }
            last=index;return current;
        }
        public void Dispose(){actor.Dispose();last=-1;current=null;}
    }
}
