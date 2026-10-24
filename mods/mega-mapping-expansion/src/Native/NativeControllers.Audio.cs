using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using JumpKing;
using JumpKing.XnaWrappers;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class AudioEnvelope
    {
        internal double Gain;
        internal double Advance(bool playing,double fadeIn,double fadeOut,double delta)
        {double duration=playing?fadeIn:fadeOut;Gain=duration==0?(playing?1:0):playing?Math.Min(1,Gain+delta/duration):Math.Max(0,Gain-delta/duration);return Gain;}
    }
    internal sealed partial class NativeControllers
    {
        private sealed class Voice
        {
            internal JKRuntime.Audio.PreparedSound Sound;
            internal readonly AudioEnvelope Envelope=new AudioEnvelope();
            internal bool Requested,Started;
        }
        private readonly Dictionary<string,Voice> voices=new Dictionary<string,Voice>(StringComparer.Ordinal);
        internal string AudioIdentity {get;private set;}
        private void PrepareAudio(string root,NativeSoundPool pool)
        {
            var identities=new List<string>();
            foreach(string id in Ids("audio"))
            {
                var lease=resources.Own(pool.Acquire(root,Text(id,"path"),(SoundType)Enum.Parse(typeof(SoundType),Text(id,"bus"))));
                identities.Add(id+":"+lease.Identity);voices.Add(id,new Voice{Sound=lease.Sound});
            }
            AudioIdentity=string.Join(";",identities);
        }
        internal bool PlayVoice(string id)
        {
            Voice voice;if(!active||!MappingSettings.Enabled||!voices.TryGetValue(id,out voice))return false;
            // use the owned native instance, so master volume and bus toggles still apply
            voice.Sound.Volume=(float)Number(id,"volume");voice.Sound.IsLooped=false;
            if(voice.Sound.State!=JKSoundState.Playing)voice.Sound.Play();return true;
        }
        private void TickAudio(double delta,bool enabled,bool paused)
        {
            foreach(var pair in voices)
            {
                string id=pair.Key;var voice=pair.Value;var sound=voice.Sound;
                if(!enabled){sound.Stop();voice.Started=voice.Requested=false;voice.Envelope.Gain=0;Publish(id,"audible",SceneValue.Boolean(false));continue;}
                sound.SetPaused(paused);
                if(paused){Publish(id,"audible",SceneValue.Boolean(false));continue;}
                bool requested=Flag(id,"playing");
                if(requested&&!voice.Requested){sound.IsLooped=Flag(id,"loop");voice.Started=true;sound.Volume=0;sound.Play();}
                voice.Requested=requested;
                if(voice.Started)
                {
                    sound.IsLooped=Flag(id,"loop");
                    double gain=voice.Envelope.Advance(requested,Number(id,"fadeIn"),Number(id,"fadeOut"),delta);
                    sound.Volume=(float)(gain*Number(id,"volume"));
                    if(!requested&&gain==0){sound.Stop();voice.Started=false;}
                }
                else sound.Volume=(float)Number(id,"volume");
                Publish(id,"audible",SceneValue.Boolean(sound.State==JKSoundState.Playing&&sound.Volume>0));
            }
        }
        private void StopAudio()
        {foreach(var voice in voices.Values){voice.Sound.Stop();voice.Started=voice.Requested=false;voice.Envelope.Gain=0;}}
    }
}
