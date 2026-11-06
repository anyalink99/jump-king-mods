using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime;
using JKRuntime.Audio;
using JKRuntime.Gameplay;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace Metroidvania
{
    internal static partial class MetroidvaniaTests
    {
        private static ulong CueMask(Battle b,string weapon)
        {ulong mask=0;for(int i=0;i<b.EventCount;i++)mask=CombatAudio.Add(mask,CombatAudio.Select(b.Event(i),weapon,b.Player.Action));return mask;}
        private static void AudioEvents()
        {
            var b=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);Send(b,1,CombatCommandKind.Light);Step(b,7);Check(CueMask(b,"worn-blade")==0,"No attack sound during light startup");
            Step(b,1);Check(CueMask(b,"worn-blade")==1UL<<(int)ContentCatalog.Cue("side-light"),"Missed swing still sounds at its active frame");
            Check(!b.Command(new CombatCommand{Actor=1,Serial=2,Kind=CombatCommandKind.Light,Facing=1}),"Busy attack rejected");Step(b,1);Check(CueMask(b,"worn-blade")==0,"Active window doesn't repeat swing sound");
            b=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);Send(b,1,CombatCommandKind.Heavy);Step(b,19);Check(CueMask(b,"iron-blade")==0,"Heavy startup stays silent");Step(b,1);Check(CueMask(b,"iron-blade")==1UL<<(int)ContentCatalog.Cue("bastard-heavy"),"Bastard heavy selects its own Quest event");
            var swing=new CombatEvent{Kind=CombatEventKind.Swing,Actor=1,Amount=(int)CombatAction.Heavy};
            Check(CombatAudio.Select(swing,"reed-spear",CombatAction.Heavy)==ContentCatalog.Cue("spear")&&CombatAudio.Select(swing,"",CombatAction.Heavy)==ContentCatalog.Cue("punch"),"Spear and unarmed have distinct cues");
            var hit=new CombatEvent{Kind=CombatEventKind.Hit,Actor=1,Target=2,Amount=16};
            Check(CombatAudio.Select(hit,"reed-spear",CombatAction.Light)==ContentCatalog.Cue("stab-hit")&&CombatAudio.Select(hit,"worn-blade",CombatAction.Light)==ContentCatalog.Cue("slash-hit"),"Hit sounds follow damage type");
            hit.Target=1;hit.Amount=0;hit.Guarded=true;Check(CombatAudio.Select(hit,"worn-blade",CombatAction.Guard)==ContentCatalog.Cue("parry"),"Parry has its own cue");hit.Amount=3;Check(CombatAudio.Select(hit,"worn-blade",CombatAction.Guard)==ContentCatalog.Cue("block"),"Late block has its own cue");
            b=NewBattle();b.Player.Health=1;Step(b,90);Send(b,1,CombatCommandKind.Guard);Step(b,15);ulong blockMask=CueMask(b,"worn-blade");Check(b.Player.Action==CombatAction.Down&&(blockMask&(1UL<<(int)ContentCatalog.Cue("block")))!=0,"Lethal chip still sounds like a shield block");
            Check(CombatAudio.Select(new CombatEvent{Kind=CombatEventKind.FallImpact},"worn-blade",CombatAction.Hurt)==ContentCatalog.Cue("blunt-hit"),"Hard landing uses blunt impact");
            b=new Battle(CombatStats.From(new CharacterProfile()),CrawlerData);b.Player.Invulnerable=1000;for(int i=0;i<16;i++)b.Spawn(new Microsoft.Xna.Framework.Vector2(30,78),0,80);Send(b,1,CombatCommandKind.Light);Step(b,8);
            Check(b.EventCount==33&&CueMask(b,"worn-blade")==((1UL<<(int)ContentCatalog.Cue("side-light"))|(1UL<<(int)ContentCatalog.Cue("slash-hit"))|(1UL<<(int)CrawlerData.Recipe.HurtSounds[0].Cue)),"Sixteen simultaneous hits use one voice per cue");
            var snapshot=b.Capture();b.Restore(snapshot);Check(CueMask(b,"worn-blade")==0,"Restoring doesn't replay sounds");
        }
        private static void Waves()
        {
            foreach(string name in QuestAudio.Names)using(var stream=typeof(QuestAudio).Assembly.GetManifestResourceStream("Metroidvania.Audio."+name)){
                Check(stream!=null,"Embedded Quest audio: "+name);using(var reader=new BinaryReader(stream)){
                    Check(new string(reader.ReadChars(4))=="RIFF"&&reader.ReadInt32()==stream.Length-8&&new string(reader.ReadChars(4))=="WAVE","Complete WAV: "+name);
                    Check(new string(reader.ReadChars(4))=="fmt "&&reader.ReadInt32()==16&&reader.ReadInt16()==1&&reader.ReadInt16()==1&&reader.ReadInt32()==48000&&reader.ReadInt32()==96000&&reader.ReadInt16()==2&&reader.ReadInt16()==16,"Mono PCM 16-bit 48k: "+name);
                    Check(new string(reader.ReadChars(4))=="data","PCM payload: "+name);int bytes=reader.ReadInt32();Check(bytes>9600&&bytes<=96000*8&&bytes==stream.Length-stream.Position,"Bounded complete PCM: "+name);
                    int peak=0;long energy=0;for(int i=0;i<bytes/2;i++){int sample=reader.ReadInt16();peak=Math.Max(peak,Math.Abs(sample));energy+=(long)sample*sample;}
                    Check(peak>100&&energy/(bytes/2)>100,"Non-silent Quest capture: "+name);
                }
            }
        }
        private static void NativeAudio()
        {
            typeof(SoundEffect).GetMethod("InitializeSoundEffect",Members).Invoke(null,null);
            var native=typeof(JumpKing.Game1).Assembly;var prefsType=native.GetType("JumpKing.PlayerPreferences.SoundPrefs",true);var managerType=native.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime",true);var baseType=managerType.BaseType;
            var instance=baseType.GetField("instance",Members);object previous=instance.GetValue(null),prefs=Activator.CreateInstance(prefsType),manager=FormatterServices.GetUninitializedObject(managerType);
            prefsType.GetField("master",Members).SetValue(prefs,0f);prefsType.GetField("sfx_on",Members).SetValue(prefs,true);baseType.GetField("m_settings",Members).SetValue(manager,prefs);instance.SetValue(null,manager);
            try{using(var audio=new QuestAudio()){audio.PrepareAll();
                var sounds=(PreparedSound[])typeof(QuestAudio).GetField("sounds",Members).GetValue(audio);
                var voice=typeof(PreparedSound).GetProperty("sound",Members);var first=voice.GetValue(sounds[0],null);
                for(int attempt=0;attempt<3;attempt++){
                    using(audio.BindToAttempt()){
                        audio.Play(ulong.MaxValue);audio.Play(ulong.MaxValue);
                        Check(ReferenceEquals(first,voice.GetValue(sounds[0],null)),"Busy cue never recreates or decodes a voice");
                        foreach(var sound in sounds)Check(sound.State!=JKSoundState.Stopped,"Native voice plays decoded Quest PCM");
                        typeof(QuestAudio).GetMethod("Pause",Members).Invoke(audio,new object[]{true});foreach(var sound in sounds)Check(sound.State==JKSoundState.Paused,"Pause holds each native voice");
                        audio.Play(ulong.MaxValue);typeof(QuestAudio).GetMethod("Pause",Members).Invoke(audio,new object[]{false});
                        Check(ReferenceEquals(first,voice.GetValue(sounds[0],null)),"Paused requests don't rebuild voices");
                        typeof(GameplayEvents).GetMethod("Emit",Members).Invoke(null,new object[]{GameplayEventKind.RestoreStarted,"metroidvania.tests",Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Vector2.Zero,1,null,false});
                        foreach(var sound in sounds)Check(System.Threading.SpinWait.SpinUntil(()=>sound.State==JKSoundState.Stopped,500),"Restore stops previous-timeline audio");
                        audio.Play(ulong.MaxValue);
                    }
                    foreach(var sound in sounds)Check(System.Threading.SpinWait.SpinUntil(()=>sound.State==JKSoundState.Stopped,500),"Attempt teardown stops all voices");
                }
                foreach(var sound in sounds){var current=(JKSound)voice.GetValue(sound,null);var settings=(Microsoft.Xna.Framework.Audio.SoundEffectInstance)typeof(JKSound).GetField("m_instance",Members).GetValue(current);Check(settings.Volume==0,"Native master mute applies to Quest sounds");}
                prefsType.GetField("master",Members).SetValue(prefs,.37f);prefsType.GetField("sfx_on",Members).SetValue(prefs,false);typeof(JKSound).GetMethod("OnSoundSettingsChange",Members).Invoke(null,new[]{prefs});
                foreach(var sound in sounds){var current=(JKSound)voice.GetValue(sound,null);var settings=(SoundEffectInstance)typeof(JKSound).GetField("m_instance",Members).GetValue(current);Check(settings.Volume==0,"Native SFX toggle mutes Quest sounds");}
                prefsType.GetField("sfx_on",Members).SetValue(prefs,true);typeof(JKSound).GetMethod("OnSoundSettingsChange",Members).Invoke(null,new[]{prefs});
                foreach(var sound in sounds){var current=(JKSound)voice.GetValue(sound,null);var settings=(SoundEffectInstance)typeof(JKSound).GetField("m_instance",Members).GetValue(current);Check(Math.Abs(settings.Volume-.37f)<.001f,"Native master level reaches Quest sounds");}
                audio.Dispose();foreach(var sound in sounds)Check(sound.IsDisposed,"World audio releases native resources");
            }}finally{instance.SetValue(null,previous);}
            Console.WriteLine("[OK] Quest audio: "+QuestAudio.Names.Length+" native WAV decodes, bounded reuse, pause, mute, restart teardown and disposal");
        }
    }
}
