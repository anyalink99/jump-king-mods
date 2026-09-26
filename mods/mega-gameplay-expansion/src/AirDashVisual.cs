using System;
using System.Collections.Generic;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    // Short outfit-aware echoes, a tapered cyan/violet wake and a pixel shock
    // ring. The live king is still the native sprite, not a recoloured replacement.
    internal sealed class AirDashVisual : Component
    {
        internal struct Echo { internal Vector2 Position; internal float Age; }
        internal const float TailTime=.18f;
        private static readonly System.Reflection.FieldInfo SpriteField=typeof(PlayerEntity).GetField("m_sprite",NativeFlight.Fields);
        private readonly PlayerEntity player;
        private readonly List<Echo> echoes=new List<Echo>();
        private WarpImage image;
        private DashSprite wrapper;
        private Vector2 origin, end;
        private float age, endAge;
        private int direction;
        private bool ended, impact;
        internal Action<float> AfterInput;
        internal AirDashVisual(PlayerEntity value) { player=value; }
        internal static Sprite Unwrap(Sprite sprite)
        { var dash=sprite as DashSprite; return dash==null?sprite:dash.Current; }
        internal void Begin(int value)
        {
            Clear(); image=new WarpVisual(player).CaptureImage();
            direction=value; origin=end=player.m_body.Position; age=endAge=0; ended=impact=false;
            echoes.Add(new Echo { Position=origin });
        }
        internal void Sample(Vector2 position)
        {
            if (image==null) return;
            end=position;
            if (echoes.Count>=7) echoes.RemoveAt(0);
            echoes.Add(new Echo { Position=position });
        }
        internal void End(bool hit)
        { if (image==null) return; end=player.m_body.Position; ended=true; impact=hit; endAge=0; }
        internal void Clear()
        {
            if (wrapper!=null && ReferenceEquals(SpriteField.GetValue(player),wrapper)) player.SetSprite(wrapper.Current);
            wrapper=null; image=null; echoes.Clear();
        }
        protected override void Update(float delta)
        {
            if (image!=null && delta>0 && !float.IsNaN(delta) && !float.IsInfinity(delta))
            {
                float step=Math.Min(.05f,delta); age+=step; if (ended) endAge+=step;
                for(int i=echoes.Count-1;i>=0;i--)
                {
                    var echo=echoes[i]; echo.Age+=step;
                    if(echo.Age>=TailTime) echoes.RemoveAt(i); else echoes[i]=echo;
                }
                if(ended && endAge>=TailTime) Clear();
            }
            if(AfterInput!=null) AfterInput(delta);
        }
        protected override void LateUpdate(float delta)
        {
            if(image==null) return;
            var current=(Sprite)SpriteField.GetValue(player);
            if(wrapper==null) wrapper=new DashSprite(this,current);
            else if(!ReferenceEquals(current,wrapper)) wrapper.Current=current;
            player.SetSprite(wrapper);
        }
        private sealed class DashSprite : Sprite
        {
            private readonly AirDashVisual owner;
            internal Sprite Current;
            internal DashSprite(AirDashVisual value,Sprite current) { owner=value; Current=current; }
            public override void Draw(Vector2 position,SpriteEffects effects=SpriteEffects.None)
            {
                var trail=owner.echoes.ToArray();
                for(int i=0;i<trail.Length;i++) trail[i].Position=Camera.TransformVector2(trail[i].Position);
                DrawEffect(owner.image,trail,Camera.TransformVector2(owner.origin),Camera.TransformVector2(owner.end),owner.direction,owner.age,owner.ended,owner.endAge,owner.impact);
                if(Current!=null) Current.Draw(position,effects);
            }
            public override void Draw(float x,float y,SpriteEffects effects=SpriteEffects.None) { Draw(new Vector2(x,y),effects); }
            public override void Draw(Point point,SpriteEffects effects=SpriteEffects.None) { Draw(point.ToVector2(),effects); }
            public override void Draw(Rectangle rectangle,SpriteEffects effects=SpriteEffects.None) { Draw(rectangle.Location.ToVector2(),effects); }
        }
        internal static void DrawEffect(WarpImage image,Echo[] echoes,Vector2 origin,Vector2 end,int direction,float age,bool ended,float endAge,bool impact)
        {
            Texture2D white=null;
            foreach(var layer in image.Layers) if(layer.Texture!=null) { white=WarpVisual.WhitePixel(layer.Texture.GraphicsDevice); break; }
            if(white==null) return;
            var cyan=new Color(65,225,255); var violet=new Color(145,100,255); var ice=new Color(222,255,255);
            foreach(var echo in echoes)
            {
                float opacity=Math.Max(0,1-echo.Age/TailTime);
                foreach(var layer in image.Layers)
                {
                    if(layer.Texture==null) continue;
                    var tint=Color.Lerp(violet,cyan,opacity)*(.32f*opacity*opacity);
                    if(layer.Rasterized)
                    {
                        var top=layer.TopLeft(echo.Position+new Vector2(9,26));
                        for(int y=0;y<layer.Source.Height;y++)for(int x=0;x<layer.Source.Width;x++)
                            if(layer.Pixels[y*layer.Source.Width+x].A!=0)
                                Game1.spriteBatch.Draw(white,new Rectangle((int)top.X+x,(int)top.Y+y,1,1),tint*(layer.Pixels[y*layer.Source.Width+x].A/255f));
                        continue;
                    }
                    Game1.spriteBatch.Draw(layer.Texture,echo.Position+new Vector2(9,26),layer.Source,tint,0,
                        new Vector2(layer.Source.Width,layer.Source.Height)*layer.Center,1,image.Flip,0);
                }
            }
            float fade=ended?Math.Max(0,1-endAge/TailTime):1;
            // The wake occupies the distance actually travelled, never the far
            // side of a wall. Offset strands break up a flat laser-beam silhouette.
            float span=Math.Abs(end.X-origin.X);
            for(int i=0;i<7;i++)
            {
                int length=(int)(span*(1-i*.09f)); if(length<1) continue;
                float x=direction>0?end.X+9-length:end.X+9;
                int y=(int)(origin.Y+13+(i-3)*2);
                Game1.spriteBatch.Draw(white,new Rectangle((int)x,y,length,1),(i==3?ice:i%2==0?cyan:violet)*(fade*(i==3?.85f:.4f)));
            }
            float ring=Math.Min(1,age/.1f);
            for(int i=0;i<12 && ring<1;i++)
            {
                double angle=i*Math.PI/6;
                var p=origin+new Vector2(9+(float)Math.Cos(angle)*(3+ring*4),(13+(float)Math.Sin(angle)*(7+ring*6)));
                Game1.spriteBatch.Draw(white,new Rectangle((int)p.X,(int)p.Y,2,2),cyan*((1-ring)*.8f));
            }
            if(impact)
            {
                for(int i=0;i<12;i++)
                {
                    float t=Math.Min(1,endAge/.14f); float reach=(5+i%4*2)*t;
                    var p=end+new Vector2(direction>0?18:0,13)+new Vector2(-direction*reach,(i-5.5f)*t*2);
                    Game1.spriteBatch.Draw(white,new Rectangle((int)p.X,(int)p.Y,i%3==0?2:1,1),(i%3==0?ice:cyan)*(1-t));
                }
            }
        }
    }
}
