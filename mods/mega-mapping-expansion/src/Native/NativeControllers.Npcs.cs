using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using EntityComponent;
using JumpKing;
using JumpKing.MiscEntities.OldMan;
using JumpKing.Util;
using JumpKing.Util.DrawBT;
using JumpKing.XnaWrappers;
using JKRuntime;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed partial class NativeControllers
    {
        private sealed class Npc
        {
            internal string Id,Name;
            internal int Screen;
            internal ISpriteEntity Entity;
            internal SayLine Say;
            internal Timer Pause;
            internal float OriginalPause;
        }
        private readonly Dictionary<string,Npc> npcs=new Dictionary<string,Npc>(StringComparer.Ordinal);
        private static readonly Type OldMan=typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.OldManEntity",true);
        private static readonly Type Merchant=typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.Merchant.MerchantEntity",true);
        private static readonly FieldInfo OldSettings=OldMan.GetField("m_settings",OwnedPatches.Members),MerchantSettings=Merchant.GetField("m_settings",OwnedPatches.Members);
        private static readonly FieldInfo PauseTimer=typeof(PauseNode).GetField("m_timer",OwnedPatches.Members);
        [ThreadStatic] private static Npc speaking;
        private static NativeControllers Current {get{return NativeScene.Current==null||!NativeScene.Current.Active?null:NativeScene.Current.Controllers;}}
        private void PrepareNpcs()
        {
            foreach(string id in Ids("npc"))
            {
                string name=Text(id,"name");OldManSettings settings;
                try{settings=Game1.instance.contentManager.oldMan.GetSetting(name);}
                catch(Exception error){throw new System.IO.InvalidDataException(id+": no native NPC named "+name,error);}
                if(npcs.ContainsKey(name))throw new System.IO.InvalidDataException("Only one adapter may own NPC "+name);
                npcs.Add(name,new Npc{Id=id,Name=name,Screen=settings.home_screen});
            }
            if(npcs.Count==0)return;
            if(PauseTimer==null||PauseTimer.FieldType!=typeof(Timer))throw new NotSupportedException("Native NPC pause contract changed");
            PreparedNativeHooks.Acquire("mega-mapping-expansion.npc",resources,delegate(OwnedPatches hooks){
            hooks.Add(typeof(SayLine).GetMethod("Reset",OwnedPatches.Members),postfix:Callback("AfterLineReset"));
            var run=typeof(SayLine).GetMethod("MyRun",OwnedPatches.Members);
            hooks.Add(run,prefix:Callback("BeforeLine"),postfix:Callback("AfterLine"),finalizer:Callback("EndLineScope"));
            hooks.ReplaceCalls(run,typeof(JKSound).GetMethod("PlayOneShot"),Callback("Speak"),1);
            });
        }
        private static MethodInfo Callback(string name){return typeof(NativeControllers).GetMethod(name,OwnedPatches.Members);}
        private Npc Bind(Entity entity)
        {
            OldManSettings settings;
            if(OldMan.IsInstanceOfType(entity))settings=(OldManSettings)OldSettings.GetValue(entity);
            else if(Merchant.IsInstanceOfType(entity))
            {var value=MerchantSettings.GetValue(entity);settings=(OldManSettings)value.GetType().GetField("settings").GetValue(value);}
            else return null;
            Npc npc;if(!npcs.TryGetValue(settings.name,out npc))return null;
            if(npc.Entity==entity)return npc;
            npc.Entity=(ISpriteEntity)entity;npc.Say=(SayLine)entity.GetType().GetField("m_say",OwnedPatches.Members).GetValue(entity);
            var pause=entity.GetType().GetField("m_say_pause",OwnedPatches.Members).GetValue(entity);
            npc.Pause=(Timer)PauseTimer.GetValue(pause);npc.OriginalPause=npc.Pause.Duration;return npc;
        }
        private void BindNpcs()
        {if(npcs.Count>0&&EntityManager.instance!=null)foreach(var entity in EntityManager.instance.Entities)Bind(entity);}
        private void UnbindNpcs()
        {foreach(var npc in npcs.Values){if(npc.Pause!=null)npc.Pause.Duration=npc.OriginalPause;npc.Entity=null;npc.Say=null;npc.Pause=null;}}
        private void SampleNpcs(bool emit)
        {
            foreach(var npc in npcs.Values)
            {
                bool present=npc.Entity!=null&&npc.Entity.IsAlive;Publish(npc.Id,"present",SceneValue.Boolean(present));
                if(!present)continue;
                Publish(npc.Id,"line",SceneValue.String(npc.Say.GetFullLine()??""));
                Publish(npc.Id,"revealed",SceneValue.Integer((npc.Say.GetLine()??"").Length));
                Publish(npc.Id,"position",SceneValue.Vector(npc.Entity.Position.X,npc.Entity.Position.Y+(npc.Screen-1)*360));
                Publish(npc.Id,"screen",SceneValue.Integer(npc.Screen));
                double pause=Number(npc.Id,"linePause");npc.Pause.Duration=active&&MappingSettings.Enabled&&pause>=0?(float)pause:npc.OriginalPause;
                bool talking=npc.Say.IsRunning();bool old=scene.Signals.Read(npc.Id+".talking").Value.X!=0;
                Publish(npc.Id,"talking",SceneValue.Boolean(talking));
                if(!baseline&&emit&&old&&!talking)Event(npc.Id,"stopped",npc.Screen);
            }
        }
        private static void AfterLineReset(SayLine __instance)
        {
            var owner=Current;if(owner==null)return;var npc=owner.Bind(__instance.game_object);if(npc==null)return;
            owner.Publish(npc.Id,"line",SceneValue.String(__instance.GetFullLine()??""));owner.Publish(npc.Id,"revealed",SceneValue.Integer(0));
            owner.Publish(npc.Id,"talking",SceneValue.Boolean(true));owner.Event(npc.Id,"started",npc.Screen);
        }
        private static void BeforeLine(SayLine __instance,out Npc __state)
        {__state=speaking;var owner=Current;speaking=owner==null?null:owner.Bind(__instance.game_object);}
        private static void EndLineScope(Npc __state){speaking=__state;}
        private static void Speak(JKSound sound)
        {
            var owner=Current;var npc=speaking;if(owner==null||npc==null){sound.PlayOneShot();return;}
            if(owner.Flag(npc.Id,"muted"))return;
            string voice=owner.Text(npc.Id,"voice");if(voice=="")sound.PlayOneShot();else owner.PlayVoice(voice);
        }
        private static void AfterLine(SayLine __instance,BTresult __result)
        {
            var owner=Current;var npc=speaking;if(owner==null||npc==null)return;
            int count=(__instance.GetLine()??"").Length;int previous=(int)owner.scene.Signals.Read(npc.Id+".revealed").Value.X;
            owner.Publish(npc.Id,"revealed",SceneValue.Integer(count));
            if(count>previous)owner.Event(npc.Id,"character",npc.Screen,new SignalSample("index",SceneValue.Integer(count),ObservationQuality.Exact,0));
            if(__result==BTresult.Success){owner.Publish(npc.Id,"talking",SceneValue.Boolean(false));owner.Event(npc.Id,"finished",npc.Screen);}
        }
    }
}
