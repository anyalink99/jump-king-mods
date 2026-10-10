using System;
using System.Collections.Generic;
using HarmonyLib;
using EntityComponent;
using JumpKing;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class PlayerEffects
    {
        private sealed class Effect { internal Vector2 Origin,Hit;internal int Direction;internal double Time;internal bool Impact; }
        private static readonly List<Effect> effects=new List<Effect>();
        internal static void Install(Harmony hooks)
        { hooks.Patch(AccessTools.Method(typeof(EntityManager),"Draw"),postfix:new HarmonyMethod(typeof(PlayerEffects),"Draw")); }
        internal static void Reset() {effects.Clear();}
        internal static void Swing(Vector2 origin,int direction,double now) {Add(new Effect{Origin=origin,Direction=direction,Time=now});}
        internal static void Hit(Vector2 origin,Vector2 hit,int direction,double now) {Add(new Effect{Origin=origin,Hit=hit,Direction=direction,Time=now,Impact=true});}
        private static void Add(Effect value) { if(effects.Count==32) effects.RemoveAt(0);effects.Add(value); }
        private static void Pixel(Vector2 position,int width,int height,Color color)
        {
            var point=Camera.TransformVector2(position).ToPoint();
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,new Rectangle(point.X,point.Y,width,height),color);
        }
        private static void Draw()
        {
            if(effects.Count==0) return;
            double now=AdvancedSession.Now;
            effects.RemoveAll(e=>now-e.Time>.42);
            foreach(var effect in effects) {
                float t=(float)((now-effect.Time)/.42),fade=1-t;
                Color gold=new Color(255,196,64)*fade,white=new Color(255,255,235)*fade,cyan=new Color(95,235,255)*fade;
                float distance=16+t*72;
                // an expanding pixel arc, with three trailing cuts
                for(int y=-17;y<=17;y+=2) {
                    float curve=(1-y*y/(17f*17f))*8;
                    Pixel(effect.Origin+new Vector2(effect.Direction*(distance+curve),y),3,2,Math.Abs(y)<9 ? white : gold);
                }
                for(int trail=0;trail<3;trail++)
                    Pixel(effect.Origin+new Vector2(effect.Direction*(distance-24-trail*7),trail*8-8),12,2,cyan);
                if(!effect.Impact) continue;
                // the burst uses the accepted hit position, not a speculative ghost
                for(int ray=0;ray<8;ray++) {
                    float angle=ray*(float)Math.PI/4;
                    var direction=new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle));
                    for(int step=0;step<3;step++) Pixel(effect.Hit+direction*(4+t*28+step*3),step==0 ? 4 : 2,2,ray%2==0 ? white : gold);
                }
            }
        }
    }
}
