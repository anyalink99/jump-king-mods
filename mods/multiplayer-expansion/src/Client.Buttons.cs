using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JumpKing.Controller;

namespace MultiplayerExpansion
{
    internal static partial class Client
    {
        private static ConditionalWeakTable<object,FocusButtons> focusButtons=new ConditionalWeakTable<object,FocusButtons>();
        private static bool inputWasAvailable;
        private static readonly FieldInfo overlay=AccessTools.Field(typeof(PadInstance),"_steam_overlay_active");
        internal static bool OverlayActive {get{return (bool)overlay.GetValue(null);}}
        internal static bool InputAvailable {get{return Selected && !OverlayActive;}}
        private static IEnumerable<MethodInfo> PadButtonMethods()
        {
            var contract=typeof(IPad).GetMethod("GetPressedButtons");
            return typeof(PadInstance).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(IPad).IsAssignableFrom(t)).Select(t=> {
                var map=t.GetInterfaceMap(typeof(IPad));return map.TargetMethods[Array.IndexOf(map.InterfaceMethods,contract)];
            }).Distinct().ToArray();
        }
        private static void InstallPadFocus()
        {
            if(overlay==null) throw new MissingFieldException(typeof(PadInstance).FullName,"_steam_overlay_active");
            focusButtons=new ConditionalWeakTable<object,FocusButtons>();
            inputWasAvailable=false;
            foreach(var method in PadButtonMethods())
                harmony.Patch(method,prefix:new HarmonyMethod(typeof(Client),"ButtonsAvailable"),postfix:new HarmonyMethod(typeof(Client),"ButtonsFocused"));
        }
        private static bool ObserveInputFocus()
        {
            bool available=InputAvailable;
            // native GetPadState skips device polling while unfocused, so reset the inner gates here too
            if(!available && inputWasAvailable) focusButtons=new ConditionalWeakTable<object,FocusButtons>();
            inputWasAvailable=available;return available;
        }
        private static bool ButtonsAvailable(ref int[] __result)
        {if(ObserveInputFocus()) return true;__result=new int[0];return false;}
        private static void ButtonsFocused(object __instance,ref int[] __result)
        {__result=focusButtons.GetOrCreateValue(__instance).Filter(InputAvailable,__result);}
        private static void RouteRuntimeMouse(object __instance)
        {
            var readField=AccessTools.Field(__instance.GetType(),"read");var focusField=AccessTools.Field(__instance.GetType(),"focused");
            var reader=(Func<int,short>)readField.GetValue(__instance);object oldFocus=focusField.GetValue(__instance);
            // leave explicit test/device readers alone, adapt Runtime's default desktop source
            if(reader.Method.DeclaringType.FullName!="JKRuntime.Input.MouseButtons" || reader.Method.Name!="GetAsyncKeyState") return;
            readField.SetValue(__instance,new Func<int,short>(key=>InputAvailable ? Native.GetAsyncKeyState(key) : (short)0));
            focusField.SetValue(__instance,new Func<bool>(delegate {return InputAvailable && PointerForeground()!=IntPtr.Zero;}));
            if(nativeHost) restoreInput.Add(delegate {readField.SetValue(__instance,reader);focusField.SetValue(__instance,oldFocus);});
        }
        private static void RouteExistingMouse(Assembly runtime)
        {
            if(ControllerManager.instance==null) return;
            var layer=runtime.GetType("JKRuntime.UI.IInputPadLayer",true);var inner=layer.GetProperty("Inner");
            var mouse=runtime.GetType("JKRuntime.UI.KeyboardMousePad",true);
            foreach(var instance in ControllerManager.instance.GetConnectedPads()) {
                object pad=instance.GetPad();var seen=new HashSet<object>();
                while(pad!=null && layer.IsInstanceOfType(pad) && seen.Add(pad)) {
                    if(mouse.IsInstanceOfType(pad)) RouteRuntimeMouse(pad);
                    pad=inner.GetValue(pad,null);
                }
            }
        }
    }
}
