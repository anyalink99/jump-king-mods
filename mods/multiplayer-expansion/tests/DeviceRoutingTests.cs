using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing.Controller;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class DeviceRoutingTests
    {
        private static int checks;
        private static bool available;
        private static int[] held=new int[0];
        private static void Check(bool value,string message) {checks++;if(!value) throw new Exception(message);}
        private static bool Available(ref bool __result) {__result=available;return false;}
        private static bool Buttons(ref int[] __result) {__result=held;return false;}
        internal static void Run()
        {
            NativeDevices();MouseLayers();
            Console.WriteLine("[OK] Device routing: "+checks+" checks (all native pad adapters, controller focus/release, mouse layers, existing and new wrappers, explicit sources and detach restoration)");
        }
        private static void NativeDevices()
        {
            var methods=(IEnumerable<MethodInfo>)AccessTools.Method(typeof(Client),"PadButtonMethods").Invoke(null,null);
            foreach(var method in methods) {
                var info=Harmony.GetPatchInfo(method);
                Check(info!=null && info.Prefixes.Any(p=>p.PatchMethod.Name=="ButtonsAvailable") && info.Postfixes.Any(p=>p.PatchMethod.Name=="ButtonsFocused"),"A native input device escaped the selected-client adapter");
            }
            var hooks=new Harmony("multiplayer-expansion.device-routing-test");
            var type=typeof(PadInstance).Assembly.GetType("JumpKing.Controller.XboxPad",true);
            var pad=(IPad)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{PlayerIndex.One},null);
            hooks.Patch(AccessTools.PropertyGetter(typeof(Client),"InputAvailable"),prefix:new HarmonyMethod(typeof(DeviceRoutingTests),"Available"));
            hooks.Patch(AccessTools.Method(type,"GetPressedButtons"),prefix:new HarmonyMethod(typeof(DeviceRoutingTests),"Buttons"));
            try {
                held=new[]{4096};available=false;Check(pad.GetPressedButtons().Length==0,"Unselected controller leaked an action");
                available=true;Check(pad.GetPressedButtons().Length==0,"Switching clients replayed a held controller button");
                held=new int[0];Check(pad.GetPressedButtons().Length==0,"Neutral controller invented an action");
                held=new[]{4096};Check(pad.GetPressedButtons().SequenceEqual(held),"Fresh controller press was lost after focus return");
                for(int tick=0;tick<60;tick++) Check(pad.GetPressedButtons().SequenceEqual(held),"Normal controller hold was interrupted");
                available=false;
                object[] args={new PadInstance(pad),default(PadState)};
                Check(!(bool)AccessTools.Method(typeof(Client),"Pad").Invoke(null,args),"Native pad state wasn't gated while unfocused");
                available=true;Check(pad.GetPressedButtons().Length==0,"Skipped native polling preserved a held action across focus loss");
                held=new int[0];pad.GetPressedButtons();held=new[]{4096};pad.GetPressedButtons();
                available=false;Check(pad.GetPressedButtons().Length==0,"External focus or overlay leaked controller input");
                available=true;Check(pad.GetPressedButtons().Length==0,"External focus return replayed a held action");
                held=new int[0];pad.GetPressedButtons();held=new[]{(int)Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickUp};
                Check(pad.GetPressedButtons().SequenceEqual(held),"Controller axis code was truncated by the focus adapter");
            } finally {hooks.UnpatchAll(hooks.Id);available=false;held=new int[0];}
            var keyboard=new FocusButtons();
            Check(keyboard.Filter(false,new[]{75}).Length==0 && keyboard.Filter(true,new[]{75}).Length==0,"Held keyboard action crossed client selection");
            keyboard.Filter(true,new int[0]);Check(keyboard.Filter(true,new[]{75}).SequenceEqual(new[]{75}),"Fresh keyboard action didn't recover");
        }
        private static void MouseLayers()
        {
            var runtime=typeof(UIApi).Assembly;var mouse=runtime.GetType("JKRuntime.UI.KeyboardMousePad",true);var chord=runtime.GetType("JKRuntime.UI.ChordPad",true);
            var readerField=AccessTools.Field(mouse,"read");var focusField=AccessTools.Field(mouse,"focused");
            var reader=(Func<int,short>)Delegate.CreateDelegate(typeof(Func<int,short>),AccessTools.Method(runtime.GetType("JKRuntime.Input.MouseButtons",true),"GetAsyncKeyState"));
            Func<int,short> explicitReader=k=>0;Func<bool> oldFocus=()=>false;
            var keyboard=(IPad)Activator.CreateInstance(typeof(PadInstance).Assembly.GetType("JumpKing.Controller.KeyboardPad",true),true);
            var existing=(IPad)Activator.CreateInstance(mouse,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{keyboard,explicitReader,oldFocus},null);
            Check(ReferenceEquals(readerField.GetValue(existing),explicitReader) && ReferenceEquals(focusField.GetValue(existing),oldFocus),"Routing replaced an explicit input source");
            readerField.SetValue(existing,reader);
            var nested=(IPad)Activator.CreateInstance(chord,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{existing},null);
            var instance=new PadInstance(nested);var oldManager=ControllerManager.instance;
            var nativeHost=AccessTools.Field(typeof(Client),"nativeHost");object previousHost=nativeHost.GetValue(null);
            var restore=(List<Action>)AccessTools.Field(typeof(Client),"restoreInput").GetValue(null);int previousCount=restore.Count;
            try {
                ControllerManager.instance=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
                AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(ControllerManager.instance,new List<PadInstance>{instance});nativeHost.SetValue(null,true);
                AccessTools.Method(typeof(Client),"RouteExistingMouse").Invoke(null,new object[]{runtime});
                Check(!ReferenceEquals(readerField.GetValue(existing),reader) && !ReferenceEquals(focusField.GetValue(existing),oldFocus),"Existing nested mouse layer wasn't routed on attach");
                Check(((Func<int,short>)readerField.GetValue(existing))(1)==0 && !((Func<bool>)focusField.GetValue(existing))(),"Unselected mouse layer accepted raw input");
                var fresh=Activator.CreateInstance(mouse,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{keyboard,reader,oldFocus},null);
                Check(!ReferenceEquals(readerField.GetValue(fresh),reader),"A newly constructed mouse layer escaped routing");
                Check(restore.Count==previousCount+2,"Mouse routing didn't own restoration for both layer lifetimes");
                foreach(var release in restore.Skip(previousCount).Reverse().ToArray()) release();restore.RemoveRange(previousCount,restore.Count-previousCount);
                Check(ReferenceEquals(readerField.GetValue(existing),reader) && ReferenceEquals(focusField.GetValue(existing),oldFocus),"Detach didn't restore the permanent mouse layer");
                Check(ReferenceEquals(readerField.GetValue(fresh),reader) && ReferenceEquals(focusField.GetValue(fresh),oldFocus),"Detach left a new mouse layer bound to the closed test window");
            } finally {
                foreach(var release in restore.Skip(previousCount).Reverse().ToArray()) release();restore.RemoveRange(previousCount,restore.Count-previousCount);
                ControllerManager.instance=oldManager;nativeHost.SetValue(null,previousHost);
            }
        }
    }
}
