using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework.Audio;

namespace WardrobePlus.Advanced
{
    internal static class NativeEffects
    {
        private static readonly Dictionary<string,string> sounds=new Dictionary<string,string> {
            {"jump","Jump"},{"land","Land"},{"splat","Splat"},{"water-jump","WaterJump"},{"water-land","WaterLand"},{"water-splat","WaterSplat"},
            {"ice-jump","IceJump"},{"ice-land","IceLand"},{"snow-jump","SnowJump"},{"snow-land","SnowLand"},{"snow-splat","SnowSplat"},
            {"sand-land","SandLand"},{"heavy-land","IronLand"},{"heavy-splat","IronSplat"},{"water-enter","WaterSplashEnter"},{"water-exit","WaterSplashExit"} };
        internal static bool Supports(string kind,string id)
        { return kind=="sound"?sounds.ContainsKey(id):kind=="particles"?(id=="jump"||id=="water-jump"||id=="water-splash"||id=="snow"):id=="heavy-land"; }
        internal static Sprite[] ParticleSprites(string id)
        {
            var p=Game1.instance.contentManager.particles;
            switch(id) { case "jump": return p.JumpParticleSprites; case "water-jump": return p.JumpParticleSpritesWater; case "water-splash": return p.WaterSplashSprites;
                case "snow": return p.GetSnowParticleSprites(0); default: return null; }
        }
        internal static object SoundWrapper(string id)
        {
            string field; if(!sounds.TryGetValue(id,out field)) return null;
            object owner=id.StartsWith("water-e")? (object)Game1.instance.contentManager.audio : Game1.instance.contentManager.audio.player;
            var member=owner.GetType().GetField(field); return member==null?null:member.GetValue(owner);
        }
        internal static SoundEffect Sound(string id)
        {
            object wrapper=SoundWrapper(id); if(wrapper==null) return null;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var pool=wrapper.GetType().GetField("m_sound",flags); if(pool==null) return null;
            object value=pool.GetValue(wrapper); if(value is SoundEffect) return (SoundEffect)value;
            var list=value as IList; if(list==null||list.Count==0) return null;
            var sound=list[0].GetType().GetField("m_sound",flags); return sound==null?null:sound.GetValue(list[0]) as SoundEffect;
        }
    }
}
