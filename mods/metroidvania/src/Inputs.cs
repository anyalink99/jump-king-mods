using System;
using System.Collections.Generic;
using JKRuntime;
using JKRuntime.UI;
using JumpKing.Controller;
using Microsoft.Xna.Framework.Input;

namespace Metroidvania
{
    internal static class Inputs
    {
        internal static readonly string[] Actions={"character","light","heavy","guard"};
        internal static readonly string[] Labels={"Character menu","Light attack","Heavy attack","Guard / parry"};
        private static readonly Keys[] Defaults={Keys.I,Keys.J,Keys.L,Keys.U};
        private static readonly UiChord[] Empty=new UiChord[0];
        private static readonly Dictionary<string,UiChord[]> cached=new Dictionary<string,UiChord[]>(StringComparer.Ordinal);
        private static string cachedDevice;
        private static Preferences cachedPreferences;
        internal static bool Known(string action){return Array.IndexOf(Actions,action)>=0;}
        private static IPad Pad(){var manager=ControllerManager.instance;var p=manager==null?null:manager.GetMain();return p!=null&&p.IsValid&&p.IsConnected?p.GetPad():null;}
        internal static UiChord[] Get(string action)
        {
            IPad pad=Pad();if(pad==null)return Empty;string device=pad.GetSaveIdentifier();var preferences=ModEntry.Preferences;
            if(device!=cachedDevice||!ReferenceEquals(preferences,cachedPreferences)){
                cached.Clear();cachedDevice=device;cachedPreferences=preferences;
                for(int i=0;i<Actions.Length;i++)cached[Actions[i]]=device=="pc_keyboard_jump_king"?new[]{new UiChord((int)Defaults[i])}:Empty;
                foreach(var saved in preferences.Bindings)if(saved.Device==device)cached[saved.Action]=new[]{new UiChord(saved.Primary),new UiChord(saved.Secondary)};
            }
            return cached[action];
        }
        private static void Set(string action,UiChord[] chords,bool reset)
        {
            IPad pad=Pad();if(pad==null)return;var prefs=ModEntry.Preferences.Copy();var values=new List<SavedBinding>(prefs.Bindings);
            values.RemoveAll(b=>b.Device==pad.GetSaveIdentifier()&&b.Action==action);
            if(!reset)values.Add(new SavedBinding{Device=pad.GetSaveIdentifier(),Action=action,Primary=Buttons(chords,0),Secondary=Buttons(chords,1)});
            prefs.Bindings=values.ToArray();ModEntry.SavePreferences(prefs);
        }
        private static int[] Buttons(UiChord[] values,int index){return values==null||values.Length<=index||values[index]==null?new int[0]:values[index].Buttons;}
        internal static void Register()
        {
            for(int i=0;i<Actions.Length;i++){
                string action=Actions[i];UIApi.RegisterBinding(new UiBindingDefinition("metroidvania."+action,"Metroidvania",Labels[i],()=>Get(action),v=>Set(action,v,false),()=>Set(action,null,true))
                    {Mode=new UiBindingModeOption(UiBindingMode.Press,"One action per press")});
            }
        }
        internal static void Activate(ModuleContext context,GameSession session)
        {
            var scope=context.Track(new UiRegistrationScope("metroidvania.attempt"));
            for(int i=0;i<Actions.Length;i++){
                string action=Actions[i];scope.RegisterInputAction(UiInputActionDefinition.FromChords("metroidvania."+action,Labels[i],120,()=>Get(action),()=>session.CanAct,
                    delegate {if(action=="character")UIApi.Open(new UiPageStack(p=>new CharacterPage(p)));else session.Command(action=="light"?CombatCommandKind.Light:action=="heavy"?CombatCommandKind.Heavy:CombatCommandKind.Guard);}));
            }
        }
    }
}
