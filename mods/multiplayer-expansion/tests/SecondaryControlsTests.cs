using System;
using HarmonyLib;
using JumpKing.Controller;

namespace MultiplayerExpansion
{
    internal static class SecondaryControlsTests
    {
        private static SecondaryControls controls;
        private static bool enabled, left, right, jump;
        private static bool Sample(PadInstance __instance, ref PadState __result)
        {
            __result = new PadState();
            if (SecondaryControls.IsKeyboard(__instance.GetPad())) controls.Apply(ref __result, enabled, left, right, jump);
            return false;
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Run()
        {
            var hooks = new Harmony("multiplayer-expansion.secondary-controls-test");
            hooks.Patch(AccessTools.Method(typeof(PadInstance), "GetPadState"), prefix: new HarmonyMethod(typeof(SecondaryControlsTests), "Sample"));
            try
            {
                controls = new SecondaryControls();
                var pad = new PadInstance((IPad)Activator.CreateInstance(typeof(PadInstance).Assembly.GetType("JumpKing.Controller.KeyboardPad", true), true));
                var wrapped = (IPad)Activator.CreateInstance(typeof(JKRuntime.UI.UIApi).Assembly.GetType("JKRuntime.UI.ChordPad", true),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[]{pad.GetPad()}, null);
                Check(SecondaryControls.IsKeyboard(wrapped) && wrapped.GetType() != pad.GetPad().GetType(), "Runtime keyboard layers lost dedicated input");
                AccessTools.Field(typeof(PadInstance), "m_pad").SetValue(pad, wrapped);
                Check(SecondaryControls.Accepts(2,1) && SecondaryControls.Accepts(1,2), "Dedicated controls didn't follow the unselected client");
                Check(!SecondaryControls.Accepts(1,1) && !SecondaryControls.Accepts(2,2) && !SecondaryControls.Accepts(1,0) && !SecondaryControls.Accepts(0,3), "Selected or unavailable client accepted dedicated controls");
                enabled = true; left = right = jump = true; pad.Update();
                Check(!pad.GetState().jump && !pad.GetState().left, "Startup accepted keys already held");
                left = right = jump = false; pad.Update();
                left = jump = true; pad.Update();
                Check(pad.GetState().left && pad.GetState().jump && pad.GetPressed().jump, "Dedicated keys didn't reach native held/pressed actions");
                for (int i=0; i<60; i++) { pad.Update(); Check(pad.GetState().jump && !pad.GetPressed().jump, "Jump hold repeated a press or lost its charge"); }
                left = false; right = true; pad.Update();
                Check(pad.GetState().right && !pad.GetState().left && pad.GetState().jump, "Direction change interrupted charging");
                Check(!pad.GetState().confirm && !pad.GetState().pause && !pad.GetState().restart && !pad.GetState().boots && !pad.GetState().snake, "Movement controls activated another action");
                enabled = false; pad.Update();
                Check(!pad.GetState().jump && !pad.GetState().right, "Focus/menu gate left actions held");
                enabled = true; pad.Update();
                Check(!pad.GetState().jump && !pad.GetState().right, "Returning to gameplay replayed held keys");
                right = jump = false; pad.Update(); jump = true; pad.Update();
                Check(pad.GetPressed().jump, "Fresh jump didn't recover after release");
                jump = false; pad.Update(); Check(!pad.GetState().jump, "Jump release was lost");
                var normal = new PadState { left=true, pause=true };
                controls.Apply(ref normal, true, false, true, true);
                Check(normal.left && normal.right && normal.jump && normal.pause, "Dedicated controls overwrote ordinary selected-client input");
                Check(Client.ReservedKey(0xDB) && Client.ReservedKey(0xDD) && Client.ReservedKey(0xDC) && !Client.ReservedKey(27), "Dedicated keys leaked into raw/menu bindings");
            }
            finally { hooks.UnpatchAll(hooks.Id); }
            Console.WriteLine("[OK] Secondary controls: real Runtime keyboard layers, native press/hold/release, movement only, focus/menu re-arm and input composition");
        }
    }
}
