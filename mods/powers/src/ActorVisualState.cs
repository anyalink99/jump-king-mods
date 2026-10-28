using System;
using System.IO;
using JumpKing;
using JumpKing.Player;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Powers
{
    internal static class ActorVisualState
    {
        internal static byte[] CaptureDash(PlayerEntity player)
        {var visual=player.GetComponent<AirDashVisual>();return visual==null?null:visual.CaptureVisual();}
        internal static byte[] CaptureWarp(PlayerEntity player)
        {var visual=player.GetComponent<WarpVisual>();return visual==null?null:visual.CaptureVisual();}
        internal static float Read(BinaryReader reader,float limit)
        {float value=reader.ReadSingle();if(float.IsNaN(value)||Math.Abs(value)>limit)throw new InvalidDataException("Invalid action visual number");return value;}
        internal sealed class Dash : IActorVisual,IActorVisualAnimation
        {
            private struct Echo {internal Vector2 Anchor;internal float Age;}
            private readonly System.Collections.Generic.List<Echo> echoes=new System.Collections.Generic.List<Echo>(7);
            private AppearanceImage image;
            private ActorVisualFrame clock;
            private double lastTime=double.NaN;
            private float lastAge=float.MaxValue;
            public void Advance(ActorVisualFrame frame,byte[] state)
            {
                if(state.Length!=35)throw new InvalidDataException("Invalid dash visual state");
                float age=BitConverter.ToSingle(state,24);bool reset=frame.Cut||double.IsNaN(lastTime)||frame.Time<lastTime||frame.Time-lastTime>.25||age<lastAge;
                if(reset){if(image!=null)image.Dispose();image=null;echoes.Clear();}
                float delta=reset?0:(float)Math.Min(.05,Math.Max(0,frame.Time-lastTime));
                bool step=reset||frame.Time>lastTime;clock=frame;lastTime=frame.Time;lastAge=age;
                if(frame.Paused)return;
                if(!frame.EffectsVisible){echoes.Clear();return;}
                for(int i=echoes.Count-1;i>=0;i--){var echo=echoes[i];echo.Age+=delta;if(echo.Age>=AirDashVisual.TailTime)echoes.RemoveAt(i);else echoes[i]=echo;}
                if(step&&state[33]==0){if(echoes.Count==7)echoes.RemoveAt(0);echoes.Add(new Echo {Anchor=frame.WorldAnchor});}
            }
            public Sprite Compose(Sprite outfit,Sprite chargeOutfit,byte[] state)
            {
                if(state.Length!=35)throw new InvalidDataException("Invalid dash visual state");
                using(var reader=new BinaryReader(new MemoryStream(state)))
                {
                    var origin=new Vector2(Read(reader,512),Read(reader,512));var end=new Vector2(Read(reader,512),Read(reader,512));
                    var center=new Vector2(Read(reader,512),Read(reader,512));float age=Read(reader,1000),endAge=Read(reader,1000);
                    int direction=reader.ReadSByte();byte ended=reader.ReadByte(),impact=reader.ReadByte();
                    if(age<0||endAge<0||Math.Abs(direction)!=1||ended>1||impact>1)throw new InvalidDataException("Invalid dash visual state");
                    if(image==null&&clock.EffectsVisible)
                        image=AppearanceCapture.Freeze(PlayerAppearance.FromSprite(PlayerAppearance.Filter(outfit,AppearanceLayerRole.WorldEffect),clock.WorldAnchor,clock.Facing),128);
                    return ActorVisuals.Layers(new EchoSprite(this),new Wake(origin,end,center,age,endAge,direction,ended!=0,impact!=0),outfit);
                }
            }
            public void Dispose(){if(image!=null)image.Dispose();image=null;echoes.Clear();lastTime=double.NaN;}
            private sealed class EchoSprite : Sprite
            {
                private readonly Dash owner;
                internal EchoSprite(Dash value){owner=value;}
                public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None)
                {
                    if(owner.image==null||!owner.image.IsValid)return;
                    var shift=anchor-Camera.TransformVector2(owner.clock.WorldAnchor);
                    foreach(var echo in owner.echoes){float life=Math.Max(0,1-echo.Age/AirDashVisual.TailTime);
                        var color=Color.Lerp(new Color(145,100,255),new Color(65,225,255),life)*(.32f*life*life);
                        owner.image.Draw(Camera.TransformVector2(echo.Anchor)+shift,new Color(color.ToVector4()*GetColor().ToVector4()));}
                }
            }
        }
        private sealed class Wake : Sprite
        {
            private readonly WarpImage image;
            private readonly Vector2 origin,end;
            private readonly float age,endAge;
            private readonly int direction;
            private readonly bool ended,impact;
            internal Wake(Vector2 start,Vector2 finish,Vector2 center,float time,float tail,int facing,bool complete,bool hit)
            {origin=start;end=finish;age=time;endAge=tail;direction=facing;ended=complete;impact=hit;image=new WarpImage(Vector2.Zero,center);}
            public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None)
            {AirDashVisual.DrawWake(image,anchor+origin,anchor+end,direction,age,ended,endAge,impact,GetColor());}
        }
        internal sealed class Warp : IActorVisual
        {
            private WarpImage image;
            private Sprite source;
            private float lastAge=float.MaxValue;
            private long revision=-1;
            public Sprite Compose(Sprite outfit,Sprite chargeOutfit,byte[] state)
            {
                if(state.Length!=13)throw new InvalidDataException("Invalid warp visual state");
                using(var reader=new BinaryReader(new MemoryStream(state)))
                {
                    var offset=new Vector2(Read(reader,10000000),Read(reader,10000000));float age=Read(reader,1000);byte flip=reader.ReadByte();
                    if(age<0||flip>3)throw new InvalidDataException("Invalid warp visual state");
                    if(image==null||age<lastAge||!object.ReferenceEquals(source,outfit)||revision!=PlayerAppearance.Revision){
                        var body=PlayerAppearance.Filter(outfit,AppearanceLayerRole.WorldEffect);image=new WarpImage(body,body,(SpriteEffects)flip);source=outfit;revision=PlayerAppearance.Revision;
                    }
                    lastAge=age;return new WarpPose(image,offset,age,(SpriteEffects)flip);
                }
            }
            public void Dispose(){image=null;source=null;}
        }
        private sealed class WarpPose : Sprite
        {
            private readonly WarpImage image;
            private readonly Vector2 offset;
            private readonly float age;
            private readonly SpriteEffects facing;
            internal WarpPose(WarpImage source,Vector2 placement,float time,SpriteEffects flip){image=source;offset=placement;age=time;facing=flip;}
            public override void Draw(Vector2 anchor,SpriteEffects ignored=SpriteEffects.None)
            {
                if(age>=MatrixPixels.Dissolve&&age<MatrixPixels.Transfer)return;
                bool arriving=age>=MatrixPixels.Transfer;
                float progress=arriving?(age-MatrixPixels.Transfer)/MatrixPixels.Assemble:age/MatrixPixels.Dissolve;
                foreach(var layer in arriving?image.ArrivalLayers:image.Layers)
                    WarpVisual.DrawLayer(layer,facing,anchor+offset,progress,arriving,null,GetColor());
            }
        }
    }
}
