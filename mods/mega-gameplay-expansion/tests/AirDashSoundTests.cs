using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.Player;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void DashSoundRegression(bool nativeAudio)
        {
            using(var stream=typeof(AirDashSound).Assembly.GetManifestResourceStream(AirDashSound.Resource))
            using(var reader=new BinaryReader(stream))
            {
                Require(new string(reader.ReadChars(4))=="RIFF","Dash WAV RIFF header");
                reader.ReadInt32(); Require(new string(reader.ReadChars(4))=="WAVE","Dash WAV type");
                bool format=false, data=false;
                while(stream.Position+8<=stream.Length)
                {
                    string id=new string(reader.ReadChars(4)); int length=reader.ReadInt32();
                    long end=stream.Position+length;
                    if(id=="fmt ")
                    {
                        Require(reader.ReadInt16()==1 && reader.ReadInt16()==1 && reader.ReadInt32()==44100,"Dash must be mono PCM at 44100 Hz");
                        reader.ReadInt32(); reader.ReadInt16(); Require(reader.ReadInt16()==16,"Native playback needs 16-bit storage");
                        format=true;
                    }
                    if(id=="data")
                    {
                        Require(length==13230*2,"Dash sound must last 300 ms");
                        int first=0,last=0,peak=0,earlyPeak=0;
                        for(int i=0;i<length/2;i++)
                        {
                            int sample=reader.ReadInt16(); if(i==0) first=sample; last=sample;
                            peak=Math.Max(peak,Math.Abs(sample)); if(i<882) earlyPeak=Math.Max(earlyPeak,Math.Abs(sample));
                        }
                        Require(Math.Abs(first)<4 && Math.Abs(last)<4,"Dash sound has a hard-cut endpoint");
                        Require(peak>1000 && peak<30000 && earlyPeak>100,"Dash sound is silent, clipped or delayed");
                        data=true;
                    }
                    stream.Position=end+(length&1);
                }
                Require(format && data,"Dash WAV chunks missing");
            }

            Console.WriteLine("[OK] Embedded dash PCM: duration, format, clean endpoints, headroom and attack");
            if(!nativeAudio) { Console.WriteLine("[SKIP Integration] Native XAudio playback and mixer"); return; }
            // The headless fixture does not run Game.Initialize, which normally
            // creates the native XAudio engine before any SoundEffect instances.
            typeof(SoundEffect).GetMethod("InitializeSoundEffect",Flags).Invoke(null,null);
            Require(typeof(SoundEffect).GetProperty("MasterVoice",Flags).GetValue(null,null)!=null,"Native XAudio device unavailable for audio verification");
            // Isolated in-memory preferences; never read or save user settings.
            var assembly=typeof(PlayerEntity).Assembly;
            var prefsType=assembly.GetType("JumpKing.PlayerPreferences.SoundPrefs",true);
            var managerType=assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime",true);
            var baseType=managerType.BaseType;
            var instance=baseType.GetField("instance",Flags); object old=instance.GetValue(null);
            object prefs=Activator.CreateInstance(prefsType);
            prefsType.GetField("master",Flags).SetValue(prefs,.25f);
            prefsType.GetField("sfx_on",Flags).SetValue(prefs,false);
            object manager=FormatterServices.GetUninitializedObject(managerType);
            baseType.GetField("m_settings",Flags).SetValue(manager,prefs);
            instance.SetValue(null,manager);
            try
            {
                using (var world = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareWorld(world);
                    var resource = typeof(ModEntry).GetField("preparedDashSound", Flags);
                    var prepared = (AirDashSound)resource.GetValue(null);
                    Require(prepared != null, "World preparation creates the reusable audio resource");
                    for (int i = 0; i < 3; i++)
                    {
                        using (var attempt = new JKRuntime.RuntimeScope())
                        {
                            ModEntry.PrepareAttempt(attempt); prepared.Play();
                            Require(ReferenceEquals(prepared, resource.GetValue(null)), "Restart reuses world audio");
                        }
                        var nativeSound = (JKSound)typeof(AirDashSound).GetField("sound", Flags).GetValue(prepared);
                        Require(System.Threading.SpinWait.SpinUntil(() => nativeSound.State == JKSoundState.Stopped, 250), "Attempt cleanup stops prepared audio");
                    }
                    world.Dispose();
                    Require(resource.GetValue(null) == null, "World exit clears the prepared sound reference");
                    Require((bool)typeof(AirDashSound).GetField("disposed", Flags).GetValue(prepared), "World exit disposes native audio");
                }
                using(var audio=new AirDashSound())
                {
                    var sound=(JKSound)typeof(AirDashSound).GetField("sound",Flags).GetValue(audio);
                    Require(sound!=null && sound.soundType==SoundType.SFX && Math.Abs(sound.Duration.TotalMilliseconds-300)<1,"Native dash audio decoding/type failed");
                    var voice=(SoundEffectInstance)typeof(JKSound).GetField("m_instance",Flags).GetValue(sound);
                    Require(voice.Volume==0,"Native SFX mute ignored");
                    audio.Play(); audio.SetPaused(true); Require(sound.State==JKSoundState.Paused,"Dash audio did not pause");
                    audio.SetPaused(false); Require(sound.State==JKSoundState.Playing,"Dash audio did not resume");
                    audio.Stop();
                    Require(System.Threading.SpinWait.SpinUntil(()=>sound.State==JKSoundState.Stopped,250),"Dash audio did not stop");
                    audio.Play(); audio.Play();
                    sound=(JKSound)typeof(AirDashSound).GetField("sound",Flags).GetValue(audio);
                    voice=(SoundEffectInstance)typeof(JKSound).GetField("m_instance",Flags).GetValue(sound);
                    Require(sound.State==JKSoundState.Playing,"Rapid restart swallowed the next whoosh");
                    audio.Stop(); audio.SetPaused(true); audio.SetPaused(false);
                    Require(System.Threading.SpinWait.SpinUntil(()=>sound.State==JKSoundState.Stopped,250),"Pause callback revived cancelled audio");
                    prefsType.GetField("sfx_on",Flags).SetValue(prefs,true);
                    baseType.GetField("m_settings",Flags).SetValue(manager,prefs);
                    typeof(JKSound).GetMethod("OnSoundSettingsChange",Flags).Invoke(null,new[]{prefs});
                    Require(voice.Volume==.25f,"Dash does not follow native master volume");
                    audio.Dispose(); audio.Stop(); audio.SetPaused(false); audio.Play();
                }
            }
            finally { instance.SetValue(null,old); }
            Console.WriteLine("[OK] Air Dash audio: embedded 300ms PCM, clean cut/headroom, native decoder, SFX mute/master, pause/resume/stop and idempotent disposal");
        }
    }
}
