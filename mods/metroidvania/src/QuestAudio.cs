using System;
using JKRuntime;
using JKRuntime.Audio;
using JKRuntime.Gameplay;
using JumpKing.XnaWrappers;

namespace Metroidvania
{
    internal static class CombatAudio
    {
        internal static int Select(CombatEvent e,string weapon,CombatAction action)
        {
            if(e.Kind==CombatEventKind.EnemyCue)return e.Amount>=0&&e.Amount<QuestAudio.Names.Length?e.Amount:-1;
            var item=Catalog.Find(weapon);
            if(e.Kind==CombatEventKind.Swing){
                if(e.Actor!=1)return -1;if(item==null)return ContentCatalog.Cue("punch");
                bool heavy=e.Amount==(int)CombatAction.Heavy;
                return heavy?item.HeavyCue:item.LightCue;
            }
            if(e.Kind==CombatEventKind.FallImpact)return ContentCatalog.Cue("blunt-hit");
            if(e.Kind!=CombatEventKind.Hit)return -1;
            if(e.Target==1)return ContentCatalog.Cue(e.Amount==0?"parry":e.Guarded?"block":"blunt-hit");
            return item==null?ContentCatalog.Cue("blunt-hit"):item.HitCue;
        }
        internal static ulong Add(ulong mask,int cue){return cue<0?mask:mask|(1UL<<cue);}
    }
    internal sealed class QuestAudio : IDisposable
    {
        internal static readonly string[] Names=ReadNames();
        private static string[] ReadNames(){ContentCatalog.Prepare();return ContentCatalog.CueNames;}
        private readonly PreparedSound[] sounds=new PreparedSound[Names.Length];
        private bool disposed,paused;
        internal QuestAudio()
        {
            try{for(int i=0;i<sounds.Length;i++)if(!Names[i].StartsWith("jkq.",StringComparison.Ordinal))PrepareCue(i);}
            catch{Dispose();throw;}
        }
        private void PrepareCue(int cue){if(cue>=0&&sounds[cue]==null)sounds[cue]=PreparedSound.FromResource(typeof(QuestAudio).Assembly,"Metroidvania.Audio."+Names[cue]);}
        internal void PrepareAll(){for(int i=0;i<sounds.Length;i++)PrepareCue(i);}
        internal void Ensure(EnemyDefinition definition)
        {foreach(var attack in definition.Attacks)foreach(var marker in attack.Sounds)PrepareCue(marker.Cue);foreach(var markers in new[]{definition.Recipe.IdleSounds,definition.Recipe.WalkSounds,definition.Recipe.HurtSounds,definition.Recipe.DeathSounds})foreach(var marker in markers)PrepareCue(marker.Cue);}
        internal void Play(ulong mask)
        {
            if(mask==0||disposed||paused)return;
            for(int i=0;i<sounds.Length;i++)if((mask&(1UL<<i))!=0&&sounds[i]!=null&&sounds[i].State==JKSoundState.Stopped)sounds[i].Play();
        }
        private void Pause(bool value){if(disposed||paused==value)return;paused=value;foreach(var sound in sounds)if(sound!=null)sound.SetPaused(value);}
        internal void Stop(){if(disposed)return;foreach(var sound in sounds)if(sound!=null)sound.Stop();}
        internal IDisposable BindToAttempt()
        {
            var scope=new RuntimeScope();
            try{
                scope.Defer(delegate{Stop();Pause(false);});
                scope.Own(NativePause.Subscribe("metroidvania.audio",(value,time)=>Pause(value)));
                scope.Own(GameplayEvents.Subscribe("metroidvania.audio",e=>{if(e.Kind==GameplayEventKind.RestoreStarted)Stop();}));return scope;
            }catch{scope.Dispose();throw;}
        }
        public void Dispose(){if(disposed)return;foreach(var sound in sounds)if(sound!=null)sound.Dispose();disposed=true;}
    }
}
