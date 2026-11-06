using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JKRuntime.UI;
using SubframeCharge;
using SettingsStore = SubframeCharge.SettingsStore;

internal static partial class PerformanceTests
{
    private static void ModalInputRoutingTests()
    {
        foreach (bool inputs in new[] { false, true })
        foreach (bool refresh in new[] { false, true })
        {
            var fixture = new Harmony("sfc.modal-input.tests");
            var game = MakeGame();
            var context = typeof(UIApi).Assembly.GetType("JKRuntime.UI.InputContextLease", true);
            fixture.Patch(AccessTools.Method(context, "ReadModal"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(ResponsiveInput), "Poll"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(PadInstance), "GetPadState"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(ControllerManager), "Update"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessController")));
            fixture.Patch(AccessTools.Method(typeof(SubframeKeyboard), "Refresh"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "KeepKeyboardWorker")));
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            ControllerManager.instance = manager;
            int[] keys = {38,40,37,39,32,27,13,8,46,65,36};
            var binding = new PadBinding { up=new[]{38},down=new[]{40},left=new[]{37},right=new[]{39},jump=new[]{32},
                pause=new[]{27},confirm=new[]{13},cancel=new[]{8},boots=new[]{46},snake=new[]{65},restart=new[]{36} };
            var device = new Pad { Identifier="pc_keyboard_jump_king", Binding=binding };
            var wrapperType = typeof(UIApi).Assembly.GetType("JKRuntime.UI.KeyboardMousePad", true);
            var wrapped = (IPad)Activator.CreateInstance(wrapperType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { device, new Func<int,short>(key => 0), new Func<bool>(() => true) }, null);
            var pad = new PadInstance(wrapped);
            AccessTools.Field(typeof(ControllerManager), "m_pads").SetValue(manager, new List<PadInstance> {pad});
            AccessTools.Field(typeof(ControllerManager), "_current_main").SetValue(manager, pad);
            AccessTools.Field(typeof(ControllerManager), "_menu_controller").SetValue(manager, new MenuController(manager));
            int physical = 0;
            var worker = new KeyboardActionEdges(keys.Select(key => new[] {new[] {key}}).ToArray(), new IntPtr(1),
                key => (short)((physical & (1 << Array.IndexOf(keys,key))) != 0 ? -32768 : 0), () => new IntPtr(1), false);
            IDisposable modal = null;
            Action tick = () => { if (inputs) ResponsiveInput.Poll(); manager.Update(); };
            Func<UiInput> read = () => (UiInput)AccessTools.Method(context,"ReadModal").Invoke(modal,new object[]{manager.MenuController.GetPadState()});
            try
            {
                PerformanceFeatures.Install(); SettingsStore.Current.Enabled=false;
                SettingsStore.Current.SetInputs(inputs); SettingsStore.Current.HighRefresh=inputs && refresh; FeatureClock.BeforeTick(game);
                if (inputs) { AccessTools.Field(typeof(SubframeKeyboard),"worker").SetValue(null,worker); AccessTools.Field(typeof(SubframeKeyboard),"owner").SetValue(null,pad); }
                tick(); tick();
                modal = (IDisposable)AccessTools.Method(context,"AcquireModal").Invoke(null,null);
                UiAction[] actions = {UiAction.Up,UiAction.Down,UiAction.Left,UiAction.Right,UiAction.Confirm,UiAction.Cancel,UiAction.Confirm,UiAction.Cancel,UiAction.Secondary,UiAction.None,UiAction.None};
                for (int i=0;i<keys.Length;i++)
                {
                    physical=0; device.Buttons=new int[0]; tick(); read();
                    physical=1<<i; device.Buttons=new[]{keys[i]}; tick();
                    Check(ResponsiveInput.Bits(pad.GetState())==0 && ResponsiveInput.Bits(pad.GetPressed())==0,
                        "Modal masks native held/pressed with SFC inputs="+inputs+", refresh="+refresh+", action="+i);
                    using (NativeInputFrames.BeginMenu(manager.MenuController, ResponsiveInput.MenuEdges()))
                        Check(ResponsiveInput.Bits(manager.MenuController.GetPadState())==0,"accelerated pause menu cannot steal modal Escape/HUD");
                    Check(read().Action==actions[i],"viewer receives its raw command exactly once: "+i);
                    tick(); Check(read().Action==UiAction.None,"held controls don't repeat through SFC");
                }
                // close while Restart is held, then toggle the input producer off
                modal.Dispose(); modal=null;
                tick(); Check(ResponsiveInput.Bits(pad.GetPressed())==0,"held Restart stays blocked after forced modal close");
                SettingsStore.Current.SetInputs(false); FeatureClock.BeforeTick(game); manager.Update();
                Check(ResponsiveInput.Bits(pad.GetPressed())==0,"disabling SFC cannot replay the closing gesture");
                physical=0; device.Buttons=new int[0]; manager.Update(); manager.Update();
                device.Buttons=new[]{27}; manager.Update();
                Check(pad.GetPressed().pause,"native Escape works again after release and handoff");
            }
            finally
            {
                physical=0; device.Buttons=new int[0]; if(modal!=null)modal.Dispose();
                worker.Dispose(); PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id);
                ControllerManager.instance=null; AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            }
        }
        Console.WriteLine("[OK] Modal replay input isolation with native/SFC/240 Hz publication and mode handoff");
    }

    private static void TextInputRoutingTests()
    {
        foreach (bool inputs in new[] { false, true })
        foreach (bool refresh in new[] { false, true })
        {
            var fixture = new Harmony("sfc.text-input.tests");
            var game = MakeGame();
            fixture.Patch(AccessTools.Method(typeof(ResponsiveInput), "Poll"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(PadInstance), "GetPadState"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(ControllerManager), "Update"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessController")));
            fixture.Patch(AccessTools.Method(typeof(SubframeKeyboard), "Refresh"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "KeepKeyboardWorker")));
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            ControllerManager.instance = manager;
            // actual default editing keys and a remapped letter/chord. every
            // native action is included, not only Cancel/Backspace
            int[] keys = { 38,40,37,39,32,27,13,8,46,65,36 };
            var binding = new PadBinding { up=new[]{38}, down=new[]{40}, left=new[]{37}, right=new[]{39}, jump=new[]{32},
                pause=new[]{27}, confirm=new[]{13}, cancel=new[]{8}, boots=new[]{46}, snake=new[]{65}, restart=new[]{36} };
            var device = new Pad { Identifier="pc_keyboard_jump_king", Binding=binding };
            var wrapperType = typeof(UIApi).Assembly.GetType("JKRuntime.UI.KeyboardMousePad", true);
            var wrapped = (IPad)Activator.CreateInstance(wrapperType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { device, new Func<int,short>(key => 0), new Func<bool>(() => true) }, null);
            var pad = new PadInstance(wrapped);
            var controllerDevice = new Pad();
            var controller = new PadInstance(controllerDevice);
            AccessTools.Field(typeof(ControllerManager), "m_pads").SetValue(manager, new List<PadInstance> { pad, controller });
            AccessTools.Field(typeof(ControllerManager), "_current_main").SetValue(manager, pad);
            AccessTools.Field(typeof(ControllerManager), "_menu_controller").SetValue(manager, new MenuController(manager));
            int physical = 0;
            var actions = keys.Select(key => new[] { new[] { key } }).ToArray();
            actions[9] = new[] { new[] { 17,65 } };
            var worker = new KeyboardActionEdges(actions, new IntPtr(1), key => (short)(key == 17
                ? ((physical & 512) != 0 ? -32768 : 0)
                : ((physical & (1 << Array.IndexOf(keys,key))) != 0 ? -32768 : 0)), () => new IntPtr(1), false);
            UiTextEntryPage page = null;
            PadState delivered = new PadState();
            Action tick = () => {
                if (inputs) { ResponsiveInput.Poll(); delivered=ResponsiveInput.MenuEdges(); }
                manager.Update();
                if (!inputs) delivered=manager.MenuController.GetPadState();
            };
            Func<PadState> menu = () => delivered;
            Action release = () => { physical=0; device.Buttons=new int[0]; tick(); tick(); };
            try
            {
                PerformanceFeatures.Install(); SettingsStore.Current.Enabled=false;
                SettingsStore.Current.SetInputs(inputs); SettingsStore.Current.HighRefresh=inputs && refresh; FeatureClock.BeforeTick(game);
                if (inputs)
                {
                    AccessTools.Field(typeof(SubframeKeyboard), "worker").SetValue(null,worker);
                    AccessTools.Field(typeof(SubframeKeyboard), "owner").SetValue(null,pad);
                }
                release();
                for (int action=0; action<keys.Length; action++)
                {
                    // Queue an edge before capture starts, then open within the
                    // same simulation interval. Cached native frames must clear
                    physical=1<<action; device.Buttons=new[]{keys[action]}; tick();
                    page = new UiTextEntryPage("Name", "abc", value => {});
                    page.OnOpen();
                    Check(ResponsiveInput.Bits(pad.GetState()) == 0 && ResponsiveInput.Bits(pad.GetPressed()) == 0,
                        "Opening editor clears both already-published native keyboard fields");
                    Check(ResponsiveInput.Bits(inputs ? ResponsiveInput.MenuEdges() : manager.MenuController.GetPadState()) == 0,
                        "Opening editor clears queued menu edges");
                    release();
                    physical=1<<action; device.Buttons=new[]{keys[action]}; tick();
                    Check(ResponsiveInput.Bits(pad.GetState()) == 0 && ResponsiveInput.Bits(menu()) == 0,
                        "Editing key/chord cannot activate any native binding; Inputs="+inputs+", refresh="+refresh+", action="+action);
                    // Forced parent teardown while a key is still held
                    page.OnClose(); page=null;
                    for (int i=0; i<3; i++) { tick(); Check(ResponsiveInput.Bits(menu()) == 0, "Held editor key cannot escape forced close"); }
                    release();
                    physical=1<<action; device.Buttons=new[]{keys[action]}; tick();
                    int actual = ResponsiveInput.Bits(menu());
                    Check(actual == (1<<action), "Fresh press works after text capture without restarting input: inputs="+inputs+", refresh="+refresh+", action="+action+", actual="+actual);
                    release();
                }
                page=new UiTextEntryPage("Name","",value => {}); page.OnOpen(); release();
                controllerDevice.Buttons=new[]{5}; tick();
                Check(menu().confirm, "Gamepad remains available for the on-screen keyboard while text capture owns physical keys");
                controllerDevice.Buttons=new int[0]; release();
                if (inputs)
                {
                    // A complete tap exists only in the worker, never MonoGame
                    worker.Sample(Stopwatch.GetTimestamp()); physical=128; worker.Sample(Stopwatch.GetTimestamp());
                    physical=0; worker.Sample(Stopwatch.GetTimestamp()); page.OnClose(); page=null;
                    tick(); Check(ResponsiveInput.Bits(menu()) == 0, "Completed subframe Backspace cannot replay after close");
                    release();
                    physical=64; worker.Sample(Stopwatch.GetTimestamp());
                    page=new UiTextEntryPage("Name","",value => {}); page.OnOpen(); page.OnClose(); page=null;
                    tick(); Check(ResponsiveInput.Bits(menu()) == 0, "An entire capture between worker deliveries invalidates old Confirm");
                    release();
                    page=new UiTextEntryPage("Name","",value => {}); page.OnOpen();
                    physical=128; device.Buttons=new[]{8}; tick();
                    SettingsStore.Current.SetInputs(false); FeatureClock.BeforeTick(game);
                    manager.Update();
                    Check(ResponsiveInput.Bits(manager.MenuController.GetPadState()) == 0, "Disabling Inputs during editing cannot restore native Cancel");
                    page.OnClose(); page=null; manager.Update();
                    Check(ResponsiveInput.Bits(manager.MenuController.GetPadState()) == 0, "Capture release guard survives disabling the subframe owner");
                    physical=0; device.Buttons=new int[0]; manager.Update(); manager.Update();
                    device.Buttons=new[]{8}; manager.Update();
                    Check(manager.MenuController.GetPadState().cancel, "Ordinary native navigation rearms after editing and mode handoff");
                }
            }
            finally
            {
                if (page != null) page.OnClose(); worker.Dispose();
                PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id);
                ControllerManager.instance=null; AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            }
        }
    }
}
