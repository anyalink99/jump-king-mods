using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.GameManager.MultiEnding;
using JumpKing.MiscSystems.LocationText;
using JumpKing.MiscSystems.Achievements;
using JumpKing.Player.Skins;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace OverlayPlus
{
    internal static class Native
    {
        internal const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static readonly Assembly Game=typeof(Game1).Assembly;
        internal static readonly Type Pause=Game.GetType("JumpKing.PauseMenu.PauseManager",true);
        private static readonly MethodInfo setPause=Pause.GetMethod("SetPause",Flags);
        private static readonly Type locations=Game.GetType("JumpKing.MiscSystems.LocationText.LocationTextManager",true);
        private static readonly PropertyInfo settings=locations.GetProperty("SETTINGS",Flags);
        private static readonly Type title=Game.GetType("JumpKing.GameManager.TitleScreenResultUtil",true);
        private static readonly MethodInfo startType=title.GetMethod("GetTitleScreenResult",Flags);
        private static readonly Type flagsType=Game.GetType("JumpKing.SaveThread.EventFlagsSave",true);
        private static readonly MethodInfo hasFlag=flagsType.GetMethod("ContainsFlag",Flags);
        private static readonly Type story=Game.GetType("JumpKing.SaveThread.StoryEventFlags",true);
        private static readonly PropertyInfo snapshot=Game.GetType("JumpKing.SaveThread.SaveLube",true).GetProperty("PlayerStatsAttemptSnapshot",Flags);
        private static readonly FieldInfo achievementInstance=Game.GetType("JumpKing.MiscSystems.Achievements.AchievementManager",true).GetField("instance",Flags);
        private static readonly MethodInfo currentStats=achievementInstance.FieldType.GetMethod("GetCurrentStats",Flags);
        private static readonly FieldInfo modifiers=typeof(JumpKing.Player.BodyComp).GetField("m_externalBehavioursCount",Flags);
        private static readonly FieldInfo jumpState=typeof(JumpKing.Player.PlayerEntity).GetField("m_jump_state",Flags);
        private static readonly FieldInfo chargeTimer=typeof(JumpKing.Player.JumpState).GetField("m_timer",Flags);
        internal static void Validate()
        {
            if(setPause==null||settings==null||startType==null||hasFlag==null||snapshot==null||currentStats==null||modifiers==null)throw new NotSupportedException("Overlay+: unsupported native game contract");
            JKRuntime.State.GameClock.ValidateContract();JKRuntime.Gameplay.NativePause.ValidateContract();
        }
        internal static void SetPause(bool pause){setPause.Invoke(null,new object[]{pause});}
        internal static Campaign StartCampaign(bool custom)
        {
            if(custom)return Campaign.Custom;
            var result=(TitleScreenResult)startType.Invoke(null,null);
            bool ghost=result==TitleScreenResult.Continue&&(bool)hasFlag.Invoke(null,new[]{Enum.Parse(story,"StartedGhost")});
            bool plus=result==TitleScreenResult.Continue&&(bool)hasFlag.Invoke(null,new[]{Enum.Parse(story,"StartedNBP")});
            return ResolveCampaign(custom,result,plus,ghost);
        }
        internal static Campaign ResolveCampaign(bool custom,TitleScreenResult result,bool plus,bool ghost)
        {
            if(custom)return Campaign.Custom;
            if(result==TitleScreenResult.StartOwlGame)return Campaign.GhostOfTheBabe;
            if(result==TitleScreenResult.StartNewBabePlus)return Campaign.NewBabePlus;
            if(result==TitleScreenResult.StartNormalGame)return Campaign.MainBabe;
            if(ghost)return Campaign.GhostOfTheBabe;
            if(plus)return Campaign.NewBabePlus;
            return Campaign.MainBabe;
        }
        internal static string AttemptKey(){var s=(PlayerStats)snapshot.GetValue(null,null);return s.attempts+"|"+s.session+"|"+s._ticks+"|"+s.jumps+"|"+s.falls;}
        internal static PlayerStats Stats(){return (PlayerStats)currentStats.Invoke(achievementInstance.GetValue(null),null);}
        internal static uint Modifiers(){return GameLoop.m_player==null?0:(uint)modifiers.GetValue(GameLoop.m_player.m_body);}
        internal static float Charge(){if(GameLoop.m_player==null||jumpState==null||chargeTimer==null)return 0;var jump=jumpState.GetValue(GameLoop.m_player) as JumpKing.Player.JumpState;return jump==null||!jump.IsRunning()?0:Math.Max(0,Math.Min(1,(float)chargeTimer.GetValue(jump)/jump.CHARGE_TIME));}
        internal static double Time(){var t=JKRuntime.State.GameClock.ReadCurrent();return t.Time+t.Ticks*Game1.instance.TargetElapsedTime.TotalSeconds;}
        private static readonly MethodInfo wearing=Game.GetType("JumpKing.Player.Skins.SkinManager",true).GetMethod("IsWearingSkin",Flags);
        internal static bool Equipped(string source){return (bool)wearing.Invoke(null,new object[]{source=="Boots"?Items.GiantBoots:Items.SnakeRing});}
        internal static Route ReadRoute()
        {
            var native=(LocationSettings)settings.GetValue(null,null);
            var areas=new List<Area>();var names=new Dictionary<string,int>();
            foreach(var a in native.locations??new Location[0]){string id=a.name??"Area";int count;names.TryGetValue(id,out count);names[id]=count+1;string name;try{name=LanguageJK.language.ResourceManager.GetString(id)??id;}catch{name=id;}areas.Add(new Area {Id=count==0?id:id+"#"+count,Name=name,Start=a.start,End=a.end,Unlock=a.unlock});}
            string root=Game1.instance.contentManager.root;string gameDir=Path.GetDirectoryName(Game.Location);string full=Path.GetFullPath(Path.IsPathRooted(root)?root:Path.Combine(gameDir,root));
            bool custom=!string.Equals(full.TrimEnd('\\','/'),Path.Combine(gameDir,"Content"),StringComparison.OrdinalIgnoreCase);
            var level=Game1.instance.contentManager.level;string world=custom?(level!=null&&level.ID!=0?"workshop:"+level.ID:"local:"+full.ToLowerInvariant()):"jump-king";
            var digest=new StringBuilder();foreach(string file in new[]{"level.xnb","slopes.xnb","level_settings.xml","gui/location_settings.xml"}){string path=Path.Combine(full,file);if(File.Exists(path))using(var f=File.OpenRead(path))using(var hash=SHA256.Create())digest.Append(file).Append(BitConverter.ToString(hash.ComputeHash(f)));}
            return AreaRoutes.Create(areas,world,AreaRoutes.Hash(digest.ToString()),custom&&level!=null?level.Name:"Jump King",StartCampaign(custom));
        }
    }
    internal static class Hooks
    {
        internal const string Id="overlay-plus.presentation";
        internal static bool Installed;
        private static JKRuntime.OwnedPatches patches;
        internal static void Install()
        {
            if(Installed)return;Remove();Native.Validate();patches=new JKRuntime.OwnedPatches(Id);
            try{
                patches.Add(typeof(Game1).GetMethod("Draw",Native.Flags),prefix:Patch("BeforeDraw"),priority:Priority.First);
                patches.Add(typeof(Game1).GetMethod("DrawRenderTarget",Native.Flags),postfix:Patch("Draw"),priority:Priority.Last);
                patches.Add(typeof(JumpGame).GetMethod("Draw"),transpiler:Patch("SeparateUi"),priority:Priority.Last);
                patches.Add(typeof(Game1).GetMethod("Update",Native.Flags),postfix:Patch("AfterUpdate"),priority:Priority.Last);
                patches.Add(typeof(JumpKing.Controller.ControllerManager).GetMethod("Update"),prefix:typeof(Inputs).GetMethod("BeginNativeSample",Native.Flags));
                patches.Add(typeof(JumpKing.Controller.ControllerManager).GetMethod("Update"),postfix:Patch("BeforeUpdate"),priority:Priority.Last);
                patches.Add(typeof(JumpKing.Controller.PadInstance).GetMethod("GetPadState",Native.Flags),transpiler:typeof(Inputs).GetMethod("ObserveNative",Native.Flags));
                patches.Add(Native.Pause.GetMethod("PauseUpdate",Native.Flags),prefix:Patch("PauseUpdate"),priority:Priority.First);
                patches.Add(Native.Pause.GetMethod("Draw",Native.Flags),prefix:Patch("PauseDraw"),priority:Priority.First);
                patches.Add(typeof(GameLoop).GetMethod("DrawIngameOverlayItems"),transpiler:typeof(GameTimer).GetMethod("Redirect",Native.Flags));
                patches.Add(Native.Game.GetType("JumpKing.MiscSystems.Achievements.AchievementManager",true).GetMethod("OnVictory",Native.Flags),prefix:Patch("Victory"),priority:Priority.First);
                Installed=true;
            }catch{Remove();throw;}
        }
        private static MethodInfo Patch(string name){return typeof(Hooks).GetMethod(name,Native.Flags);}
        internal static void Remove(){if(patches!=null){patches.Dispose();patches=null;}Installed=false;}
        private static void Draw(){try{Controller.Draw();}catch(Exception e){Controller.FailRendering(e);}}
        private static void BeforeDraw(){try{Controller.BeginFrame();}catch(Exception e){Controller.FailUpdate(e);}}
        private static void BeforeGameUi(){try{Controller.BeginUi();}catch(Exception e){Controller.FailRendering(e);}}
        private static IEnumerable<CodeInstruction> SeparateUi(IEnumerable<CodeInstruction> instructions)
        {
            var result=instructions.ToList();var field=typeof(JumpGame).GetField("m_nexile_logo",Native.Flags);
            int index=result.FindIndex(i=>i.opcode==OpCodes.Ldfld&&i.operand as FieldInfo==field);
            if(field==null||index<1||result[index-1].opcode!=OpCodes.Ldarg_0)
                throw new NotSupportedException("Overlay+: native world/UI boundary changed");
            // The common branch target follows world entities/foreground and starts
            // the state UIs. Keep all existing calls, including other mods' hooks.
            var original=result[index-1];var call=new CodeInstruction(OpCodes.Call,typeof(Hooks).GetMethod("BeforeGameUi",Native.Flags));
            call.labels.AddRange(original.labels);original.labels.Clear();call.blocks.AddRange(original.blocks);original.blocks.Clear();
            result.Insert(index-1,call);return result;
        }
        private static void BeforeUpdate(){try{Controller.BeforeUpdate();}catch(Exception e){Controller.FailEditor(e);}}
        private static void AfterUpdate(){try{Controller.AfterUpdate();}catch(Exception e){Controller.FailUpdate(e);}}
        private static bool PauseUpdate(){return !Controller.CapturesInput;}
        private static bool PauseDraw(){return !Editor.Open;}
        private static void Victory(){try{Controller.Victory(GameEnding.GetEnding());}catch(Exception e){Controller.FailUpdate(e);}}
    }
}
