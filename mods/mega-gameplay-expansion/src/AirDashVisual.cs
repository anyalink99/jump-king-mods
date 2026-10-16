using System;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    // short outfit-aware echoes, a tapered cyan/violet wake and a pixel shock
    // ring. the live king is still the native sprite, not a recoloured replacement
    internal sealed class AirDashVisual : Component
    {
        internal const float TailTime=.18f;
        private readonly PlayerEntity player;
        private WarpImage image;
        private DashSprite wrapper;
        private IDisposable visual;
        private DashTrail trail;
        private Vector2 origin, end;
        private float age, endAge;
        private int direction;
        private bool ended, impact;
        internal AirDashVisual(PlayerEntity value) { player=value; }
        internal static Sprite Unwrap(Sprite sprite)
        { var dash=sprite as DashSprite; return dash==null?sprite:dash.Current; }
        internal void Begin(int value, bool restoring=false)
        {
            Clear();
            var frame=JKRuntime.Presentation.PlayerAppearance.Resolve(player);
            image=new WarpImage(frame.Geometry.AnchorOffset,frame.Geometry.CenterOffset);
            trail=new DashTrail(player,"mega-gameplay-expansion.dash.trail",TailTime,7,
                remaining=>Color.Lerp(new Color(145,100,255),new Color(65,225,255),remaining)*(.32f*remaining*remaining),100);
            if(!trail.Capture()) WarpDiagnostics.Write("Dash image unavailable: "+trail.LastError);
            direction=value; origin=end=player.m_body.Position; age=endAge=0; ended=impact=false;
            JKRuntime.Presentation.PlayerAppearance.Signal(player,"mega-gameplay-expansion/dash",restoring?JKRuntime.Presentation.PresentationDelivery.Restore:JKRuntime.Presentation.PresentationDelivery.Live);
        }
        internal void Sample(Vector2 position)
        {
            if (image==null) return;
            end=position;
            if(trail!=null) trail.Emit();
        }
        internal void End(bool hit)
        { if (image==null) return; end=player.m_body.Position; ended=true; impact=hit; endAge=0; }
        internal void Clear()
        {
            if(visual!=null)visual.Dispose();visual=null;
            if(trail!=null)trail.Dispose();trail=null;
            wrapper=null; image=null;
        }
        internal void Advance(float delta)
        {
            if (image!=null && delta>0 && !float.IsNaN(delta) && !float.IsInfinity(delta))
            {
                float step=Math.Min(.05f,delta); age+=step; if (ended) endAge+=step;
                if(ended && endAge>=TailTime) Clear();
            }
        }
        protected override void LateUpdate(float delta)
        {
            if(image==null) return;
            if(visual==null)visual=JKRuntime.Presentation.PlayerVisuals.Register(player,"mega-gameplay-expansion.dash",JKRuntime.Presentation.VisualPhase.Behind,
                source=>{if(wrapper==null)wrapper=new DashSprite(this,source);else wrapper.Current=source;return wrapper;});
        }
        protected override void OnOwnerDestroy(){Clear();}
        private sealed class DashSprite : Sprite, JKRuntime.Presentation.IAppearanceProjection
        {
            private readonly AirDashVisual owner;
            internal Sprite Current;
            internal DashSprite(AirDashVisual value,Sprite current) { owner=value; Current=current; }
            public Vector2 Project(Vector2 worldAnchor)
            { var custom=Current as JKRuntime.Presentation.IAppearanceProjection; return custom==null?Camera.TransformVector2(worldAnchor):custom.Project(worldAnchor); }
            public override void Draw(Vector2 position,SpriteEffects effects=SpriteEffects.None)
            {
                DrawWake(owner.image,Camera.TransformVector2(owner.origin),Camera.TransformVector2(owner.end),owner.direction,owner.age,owner.ended,owner.endAge,owner.impact);
                if(Current!=null) Current.Draw(position,effects);
            }
            public override void Draw(float x,float y,SpriteEffects effects=SpriteEffects.None) { Draw(new Vector2(x,y),effects); }
            public override void Draw(Point point,SpriteEffects effects=SpriteEffects.None) { Draw(point.ToVector2(),effects); }
            public override void Draw(Rectangle rectangle,SpriteEffects effects=SpriteEffects.None) { Draw(rectangle.Location.ToVector2(),effects); }
        }
        private static void DrawWake(WarpImage image,Vector2 origin,Vector2 end,int direction,float age,bool ended,float endAge,bool impact)
        {
            Texture2D white=null;
            foreach(var layer in image.Layers) if(layer.Texture!=null) { white=WarpVisual.WhitePixel(layer.Texture.GraphicsDevice); break; }
            if(white==null) return;
            var cyan=new Color(65,225,255); var violet=new Color(145,100,255); var ice=new Color(222,255,255);
            float fade=ended?Math.Max(0,1-endAge/TailTime):1;
            // the wake occupies the distance actually travelled, never the far
            // side of a wall. Offset strands break up a flat laser-beam silhouette
            float span=Math.Abs(end.X-origin.X);
            for(int i=0;i<7;i++)
            {
                int length=(int)(span*(1-i*.09f)); if(length<1) continue;
                float x=direction>0?end.X+image.CenterOffset.X-length:end.X+image.CenterOffset.X;
                int y=(int)(origin.Y+image.CenterOffset.Y+(i-3)*2);
                Game1.spriteBatch.Draw(white,new Rectangle((int)x,y,length,1),(i==3?ice:i%2==0?cyan:violet)*(fade*(i==3?.85f:.4f)));
            }
            float ring=Math.Min(1,age/.1f);
            for(int i=0;i<12 && ring<1;i++)
            {
                double angle=i*Math.PI/6;
                var p=origin+image.CenterOffset+new Vector2((float)Math.Cos(angle)*(3+ring*4),(float)Math.Sin(angle)*(7+ring*6));
                Game1.spriteBatch.Draw(white,new Rectangle((int)p.X,(int)p.Y,2,2),cyan*((1-ring)*.8f));
            }
            if(impact)
            {
                for(int i=0;i<12;i++)
                {
                    float t=Math.Min(1,endAge/.14f); float reach=(5+i%4*2)*t;
                    var p=end+new Vector2(direction>0?image.CenterOffset.X*2:0,image.CenterOffset.Y)+new Vector2(-direction*reach,(i-5.5f)*t*2);
                    Game1.spriteBatch.Draw(white,new Rectangle((int)p.X,(int)p.Y,i%3==0?2:1,1),(i%3==0?ice:cyan)*(1-t));
                }
            }
        }
    }
}
