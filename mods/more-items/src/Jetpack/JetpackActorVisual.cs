using System;
using System.IO;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.JKMemory.KingSpriteLayers;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JumpKingJetpack
{
    internal sealed class JetpackActorVisual : IActorVisual,IActorVisualAnimation
    {
        private readonly JetpackFlameAnimation animation=new JetpackFlameAnimation();
        private JetpackFireTrail trail;
        private ActorVisualFrame clock;
        private double lastTime=double.NaN,remainder;
        private bool active;
        internal int ParticleCount {get{return trail==null?0:trail.Count;}}
        private sealed class Art : IDisposable
        {
            internal readonly JKRuntime.RuntimeScope Resources=new JKRuntime.RuntimeScope();
            internal readonly Sprite[] Bodies=new Sprite[13];
            internal readonly Texture2D[][] Flames=new Texture2D[4][];
            internal Art()
            {
                try {
                    var texture=Resources.Own(new Texture2D(Game1.instance.GraphicsDevice,JetpackAtlasData.Width,JetpackAtlasData.Height));
                    char[] atlas=JetpackAtlasData.BuildBodyAtlas();var pixels=new Color[atlas.Length];
                    for(int i=0;i<atlas.Length;i++)pixels[i]=JetpackArt.PixelColor(atlas[i]);texture.SetData(pixels);
                    var king=new KingSprites(texture);
                    for(int i=0;i<13;i++)Bodies[i]=king.regular.GetSprite((Regular.SpriteKey)Enum.Parse(typeof(Regular.SpriteKey),JetpackAtlasData.GetPoseName(i)));
                    Flames[0]=new Texture2D[0];
                    Flames[1]=Build(JetpackFlameData.IgnitionFrameCount,JetpackFlameData.GetIgnitionFrame);
                    Flames[2]=Build(JetpackFlameData.FrameCount,JetpackFlameData.GetFrame);
                    Flames[3]=Build(JetpackFlameData.ShutdownFrameCount,JetpackFlameData.GetShutdownFrame);
                }catch{Resources.Dispose();throw;}
            }
            private Texture2D[] Build(int count,Func<int,string[]> read)
            {var textures=new Texture2D[count];for(int i=0;i<count;i++)textures[i]=Resources.Own(JetpackFlameLayer.CreateTexture(read(i)));return textures;}
            public void Dispose(){Resources.Dispose();}
        }
        private sealed class Pool : IDisposable
        {
            internal Art Art;
            public void Dispose(){if(Art!=null)Art.Dispose();Art=null;}
        }
        private static Pool prepared;
        internal static void Prepare(JKRuntime.RuntimeScope scope)
        {
            var pool=scope.Own(new Pool());prepared=pool;
            scope.Defer(delegate {if(object.ReferenceEquals(prepared,pool))prepared=null;});
        }
        internal static void Validate(byte[] state)
        {
            if(state==null||state.Length!=4||state[0]>=13||state[1]>1||state[2]>1||state[3]>1)
                throw new InvalidDataException("Invalid Jetpack visual state");
        }
        public void Advance(ActorVisualFrame frame,byte[] state)
        {
            Validate(state);
            bool reset=frame.Cut||double.IsNaN(lastTime)||frame.Time<lastTime||frame.Time-lastTime>.25;
            if(reset){if(trail!=null){trail.Dispose();trail=null;}animation.Reset();remainder=0;active=false;}
            bool thrust=state[2]!=0;clock=frame;
            if(frame.Paused){lastTime=frame.Time;return;}
            int steps;
            if(reset)steps=1;
            else {remainder+=Math.Max(0,frame.Time-lastTime);steps=Math.Min(8,(int)Math.Floor(remainder*60+1e-6));remainder-=steps/60d;}
            if(steps==0&&thrust!=active)steps=1;
            lastTime=frame.Time;active=thrust;
            bool particles=frame.EffectsVisible&&state[1]!=0&&MoreItems.SettingsStore.Current.ShowJetpackTrail;
            if(particles&&trail==null&&thrust)trail=new JetpackFireTrail(null,96);
            if(trail!=null)trail.SetEnabled(particles);
            int x,y;bool rotated;JetpackAtlasData.GetNozzle(state[3]!=0?4:state[0],out x,out y,out rotated);
            int direction=(frame.Facing&SpriteEffects.FlipHorizontally)!=0?-1:1;if(direction<0)x=47-x;
            var emitter=frame.WorldAnchor-new Vector2(24,48)+new Vector2(x,y+18)+(state[3]!=0?new Vector2(0,-4):Vector2.Zero);
            for(int i=0;i<steps;i++){animation.Update(thrust);if(trail!=null){trail.Seed(unchecked((int)(frame.Time*60)-steps+i));trail.Update(thrust,emitter,frame.Velocity*25,direction);}}
        }
        public Sprite Compose(Sprite outfit,Sprite chargeOutfit,byte[] state)
        {
            Validate(state);if(prepared==null||state[1]==0)return outfit;
            if(prepared.Art==null)prepared.Art=new Art();
            return ActorVisuals.Layers(new TrailSprite(this),new PackSprite(this,prepared.Art,state,true),new PackSprite(this,prepared.Art,state,false),outfit);
        }
        public void Dispose(){if(trail!=null)trail.Dispose();trail=null;animation.Reset();lastTime=double.NaN;}
        private sealed class TrailSprite : Sprite,IAppearanceLayer
        {
            public AppearanceLayerRole Role {get{return AppearanceLayerRole.WorldEffect|AppearanceLayerRole.ExcludeFromForm;}}
            private readonly JetpackActorVisual owner;
            internal TrailSprite(JetpackActorVisual value){owner=value;}
            public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None)
            {if(owner.trail!=null)owner.trail.Draw(GetColor(),anchor-Camera.TransformVector2(owner.clock.WorldAnchor));}
        }
        private sealed class PackSprite : Sprite,IAppearanceLayer
        {
            public AppearanceLayerRole Role {get{return (fire?AppearanceLayerRole.WorldEffect:AppearanceLayerRole.Equipment)|AppearanceLayerRole.ExcludeFromForm;}}
            private readonly Art art;
            private readonly bool fire;
            private readonly JetpackActorVisual owner;
            private readonly byte[] state;
            internal PackSprite(JetpackActorVisual actor,Art resources,byte[] value,bool flame){owner=actor;art=resources;state=value;fire=flame;}
            public override void Draw(Vector2 anchor,SpriteEffects facing=SpriteEffects.None)
            {
                if(state[3]!=0)anchor.Y-=4;
                if(fire&&owner.animation.Visible)
                {
                    int x,y;bool rotated;JetpackAtlasData.GetNozzle(state[0],out x,out y,out rotated);
                    if((facing&SpriteEffects.FlipHorizontally)!=0)x=47-x;
                    float rotation=rotated?((facing&SpriteEffects.FlipHorizontally)!=0?-MathHelper.PiOver2:MathHelper.PiOver2):0;
                    Game1.spriteBatch.Draw(art.Flames[(int)owner.animation.Phase][owner.animation.Frame],anchor-new Vector2(24,48)+new Vector2(x,y),null,GetColor(),rotation,
                        new Vector2(JetpackFlameData.Width/2,0),1,facing,0);
                }
                if(!fire)ActorVisuals.Draw(art.Bodies[state[0]],anchor,facing,GetColor());
            }
        }
    }
}
