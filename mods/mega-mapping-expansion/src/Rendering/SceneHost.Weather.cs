using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private sealed class PuddleState
        {
            internal PuddleGeometry Geometry;
            internal readonly List<FootRing> Footsteps=new List<FootRing>(16);
            internal float Cooldown;
            internal bool WasTouching;
        }
        private struct FootRing { internal Vector2 Center; internal float Age,Strength; }
        private readonly Dictionary<string,PuddleState> puddleStates=new Dictionary<string,PuddleState>();
        private readonly Dictionary<int,float[]> rainRoofs=new Dictionary<int,float[]>();

        private void PrepareWeather()
        {
            foreach(PuddleData puddle in scene.Puddles)
                puddleStates.Add(puddle.Id,new PuddleState { Geometry=new PuddleGeometry(SceneValidation.ParsePath(puddle.Outline,puddle.Id)) });
            foreach(RainData rain in scene.Rains)
            {
                if(rainRoofs.ContainsKey(rain.Screen))continue;
                var roofs=new float[480]; for(int x=0;x<480;x++)roofs[x]=380;
                foreach(SceneAnchor anchor in scene.Anchors)
                    if(anchor.Screen==rain.Screen && anchor.Kind=="solid")
                        for(int x=Math.Max(0,anchor.X);x<Math.Min(480,anchor.X+anchor.Width);x++)roofs[x]=Math.Min(roofs[x],anchor.Y);
                rainRoofs.Add(rain.Screen,roofs);
            }
        }

        private void DrawRain(RainData rain)
        {
            if (rain.Opacity <= 0 || rain.Count == 0) return;
            int seed=StableHash(rain.Id);
            Color color=prepared.Color(rain.Color);
            float[] roofs=rainRoofs[rain.Screen];
            // Each drop wraps above/below the viewport, never across a visible
            // mid-air endpoint. Depth planes have independent deterministic seeds.
            for(int i=0;i<rain.Count;i++)
            {
                float speed=rain.Speed*(.8f+Hash01(seed+i*79)*.4f);
                float y=PositiveModulo(time*speed+Hash01(seed+i*163)*400,400)-20;
                float x=PositiveModulo(Hash01(seed+i*311)*560+time*rain.Wind,560)-40;
                x+=rain.Drift*(float)(Math.Sin(time*.7+i*1.91)*.7+Math.Sin(time*1.13+i*.93)*.3);
                if(x<0 || x>=480)continue;
                float length=rain.Length*(.65f+Hash01(seed+i*67)*.7f);
                float roof=rain.Collide?roofs[(int)x]:380;
                if(y>=roof || y<0)continue;
                length=Math.Min(length,roof-y);
                Vector2 a=new Vector2(x,y),b=a+new Vector2(rain.Wind/speed*length,length);
                if(rain.Snow)
                {
                    float size=Math.Min(3,length);
                    Color snow=MultiplyAlpha(color,rain.Opacity*(.55f+Hash01(seed+i*53)*.45f));
                    DrawLine(a,a+new Vector2(size,0),snow,size>.9f?1f:.7f);
                    if(size>1.5f)DrawLine(a+new Vector2(size*.5f,-.8f),a+new Vector2(size*.5f,.8f),MultiplyAlpha(snow,.6f),1);
                    continue;
                }
                DrawLine(a,b,MultiplyAlpha(color,rain.Opacity*(.55f+Hash01(seed+i*53)*.45f)),rain.Length>=7?1f:.7f);
            }
        }

        private void UpdatePuddles(int current)
        {
            var king=GameLoopPlayer();
            Rectangle body=king==null?Rectangle.Empty:Camera.TransformRect(king.m_body.GetHitbox());
            foreach(PuddleData puddle in work.Puddles.At(current))
            {
                PuddleState state=puddleStates[puddle.Id];
                for(int i=state.Footsteps.Count-1;i>=0;i--)
                {
                    FootRing ring=state.Footsteps[i]; ring.Age+=frameDelta;
                    if(ring.Age>1.25f)state.Footsteps.RemoveAt(i); else state.Footsteps[i]=ring;
                }
                state.Cooldown=Math.Max(0,state.Cooldown-frameDelta);
                bool touching=king!=null && Math.Abs(body.Bottom-puddle.PlaneY)<3
                    && state.Geometry.Contains(body.Center.X,puddle.PlaneY+2);
                if(touching && (!state.WasTouching || state.Cooldown<=0 && Math.Abs(king.m_body.Velocity.X)>.1f))
                {
                    if(state.Footsteps.Count<16)state.Footsteps.Add(new FootRing {
                        Center=new Vector2(body.Center.X,puddle.PlaneY+3), Age=0, Strength=state.WasTouching?.65f:1 });
                    state.Cooldown=.17f;
                }
                state.WasTouching=touching;
            }
        }

        private void DrawPuddle(RenderTarget2D frame,PuddleData puddle)
        {
            if (puddle.Opacity <= 0) return;
            PuddleState state=puddleStates[puddle.Id];
            Color tint=prepared.Color(puddle.Color);
            // The source is the immutable pre-UI world snapshot. Distort source
            // coordinates, not destination geometry, so reflections cannot leak
            // outside the concave shore or grow over neighbouring platform faces.
            foreach(Rectangle span in state.Geometry.Spans)
            {
                float depth=span.Y-puddle.PlaneY;
                float reflectedTop=PuddleGeometry.ReflectedY(span.Y+1,puddle.PlaneY,puddle.ReflectionScaleY,puddle.Perspective);
                float reflectedBottom=PuddleGeometry.ReflectedY(span.Y,puddle.PlaneY,puddle.ReflectionScaleY,puddle.Perspective);
                if(reflectedBottom<0 || reflectedTop>=frame.Height)continue;
                int sourceY=Math.Max(0,(int)Math.Floor(reflectedTop));
                float wave=(float)(Math.Sin(time*1.6+span.Y*.41)*.6+Math.Sin(time*.91+span.Y*.17)*.4)*puddle.Ripple;
                foreach(FootRing ring in state.Footsteps)
                    wave+=(float)(Math.Sin(depth*.65-ring.Age*13)*Math.Exp(-ring.Age*3))*ring.Strength*.7f;
                int sx=Math.Max(0,Math.Min(frame.Width-span.Width,span.X+(int)Math.Round(wave)));
                int sh=Math.Min(frame.Height-sourceY,Math.Max(1,(int)Math.Ceiling(reflectedBottom)-sourceY));
                float fresnel=MathHelper.Clamp(.96f-depth*.005f,.42f,.96f);
                Game1.spriteBatch.Draw(frame,span,new Rectangle(sx,sourceY,span.Width,sh),
                    MultiplyAlpha(tint,puddle.Opacity*fresnel),0,Vector2.Zero,SpriteEffects.FlipVertically,0);
            }
            if(previewMode!="scene")return;
            int seed=StableHash(puddle.Id);
            for(int i=0;i<puddle.RainRings;i++)
            {
                float period=.85f+Hash01(seed+i*71)*.8f;
                float clock=time/period+Hash01(seed+i*97),age=PositiveModulo(clock,1);
                int cycle=(int)Math.Floor(clock);
                int pick=(int)(Hash01(seed+i*131+cycle*317)*state.Geometry.Spans.Length);
                Rectangle span=state.Geometry.Spans[Math.Min(pick,state.Geometry.Spans.Length-1)];
                float x=span.X+Hash01(seed+i*191+cycle*113)*span.Width;
                DrawPuddleRing(state.Geometry,new Vector2(x,span.Y),1+age*6,MultiplyAlpha(tint,(1-age)*.34f*puddle.Opacity));
            }
            foreach(FootRing ring in state.Footsteps)
            {
                DrawPuddleRing(state.Geometry,ring.Center,2+ring.Age*23,MultiplyAlpha(tint,(1-ring.Age/1.25f)*.55f*ring.Strength*puddle.Opacity));
                if(ring.Age<.3f)
                    for(int i=-1;i<=1;i+=2)
                    {
                        Vector2 p=ring.Center+new Vector2(i*(2+ring.Age*15),-ring.Age*24+ring.Age*ring.Age*75);
                        DrawLine(p,p+new Vector2(i,2),MultiplyAlpha(tint,(1-ring.Age/.3f)*.65f*puddle.Opacity),1);
                    }
            }
        }

        private void DrawPuddleRing(PuddleGeometry geometry,Vector2 center,float radius,Color color)
        {
            const int segments=16;
            Vector2 previous=center+new Vector2(radius,0);
            for(int i=1;i<=segments;i++)
            {
                double angle=i*Math.PI*2/segments;
                Vector2 next=center+new Vector2((float)Math.Cos(angle)*radius,(float)Math.Sin(angle)*radius*.23f);
                if(geometry.Contains(previous.X,previous.Y) && geometry.Contains(next.X,next.Y))DrawLine(previous,next,color,.6f);
                previous=next;
            }
        }
    }
}
