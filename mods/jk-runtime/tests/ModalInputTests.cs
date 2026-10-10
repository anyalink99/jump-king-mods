using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Input;
using JKRuntime.UI;
using JumpKing.Controller;

namespace JKRuntime
{
    internal static class ModalInputTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private sealed class Pad : IPad
        {
            internal int[] Down = new int[0];
            public int[] GetPressedButtons() { return Down; }
            public string ButtonToString(int button) { return button.ToString(); }
            public string GetSaveIdentifier() { return "modal-test-pad"; }
            public string GetPrintName() { return "Controller"; }
            public bool IsConnected() { return true; }
            public PadBinding GetDefaultBind()
            {
                return new PadBinding { up=new[]{1}, down=new[]{2}, left=new[]{3}, right=new[]{4}, jump=new[]{5},
                    pause=new[]{6}, confirm=new[]{7}, cancel=new[]{8}, boots=new[]{9}, snake=new[]{10}, restart=new[]{11} };
            }
        }
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static bool Empty(PadState value)
        { return !value.up && !value.down && !value.left && !value.right && !value.jump && !value.pause && !value.confirm && !value.cancel && !value.boots && !value.snake && !value.restart; }
        private static readonly PadState All = new PadState { up=true,down=true,left=true,right=true,jump=true,pause=true,confirm=true,cancel=true,boots=true,snake=true,restart=true };

        private static void Main()
        {
            var previous = ControllerManager.instance; var previousMenu = MenuController.instance;
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            var device = new Pad(); var pad = new PadInstance(device); var pads = new List<PadInstance> { pad };
            ControllerManager.instance = manager;
            typeof(ControllerManager).GetField("m_pads", Flags).SetValue(manager, pads);
            typeof(ControllerManager).GetField("_current_main", Flags).SetValue(manager, pad);
            var menu = new MenuController(manager);
            typeof(ControllerManager).GetField("_menu_controller", Flags).SetValue(manager, menu);
            var worker = new KeyboardInputGate();
            try
            {
                NativeInputFrames.Publish(pad, All, All); menu.Update();
                using (var modal = InputContextLease.AcquireModal())
                {
                    Check(Empty(pad.GetState()) && Empty(pad.GetPressed()) && Empty(menu.GetPadState()), "opening clears already-published gameplay and menu input");
                    UiAction[] actions = { UiAction.Up, UiAction.Down, UiAction.Left, UiAction.Right, UiAction.Confirm,
                        UiAction.Cancel, UiAction.Confirm, UiAction.Cancel, UiAction.Secondary, UiAction.None, UiAction.None };
                    for (int i = 0; i < actions.Length; i++)
                    {
                        device.Down = new int[0]; modal.ReadModal(new PadState());
                        device.Down = new[] { i + 1 };
                        Check(pad.GetPad().GetPressedButtons().Length == 0, "all native actions are blocked at the device layer: " + i);
                        NativeInputFrames.Publish(pad, All, All); menu.Update();
                        Check(Empty(pad.GetState()) && Empty(pad.GetPressed()) && Empty(menu.GetPadState()), "a later publisher cannot bypass the modal: " + i);
                        using (NativeInputFrames.BeginMenu(menu, All)) Check(Empty(menu.GetPadState()), "fast menu injection cannot consume replay controls");
                        Check(modal.ReadModal(new PadState()).Action == actions[i], "modal alone receives the mapped command: " + i);
                        Check(modal.ReadModal(new PadState()).Action == UiAction.None, "held HUD/seek/Cancel is not repeated");
                        Check(!modal.IsCancelReleased(), "closing waits for every device control to release");
                        Check(worker.Suppress(true), "physical worker cannot bypass modal ownership");
                    }
                    device.Down = new int[0]; modal.ReadModal(new PadState());
                    int[] chord = ChordVirtualizer.Apply(pad, "test.hud", new[] { new UiChord(30,31) });
                    var binding = pad.GetBind().CreateCopy(); binding.SetButtonBind(JKpadButtons.Boots, chord); pad.SetBind(binding);
                    device.Down = new[] {30,31}; InputContextLease.PreparePoll();
                    Check(pad.GetPad().GetPressedButtons().Length == 0 && modal.ReadModal(new PadState()).Secondary,
                        "new chord layers stay beneath modal input and current rebindings work");
                    device.Down = new int[0]; modal.ReadModal(new PadState());
                    var hotplug = new Pad { Down = new[] { 6 } }; var connected = new PadInstance(hotplug); pads.Add(connected);
                    typeof(PadInstance).GetField("current_state", Flags).SetValue(connected, All);
                    InputContextLease.AfterPoll();
                    Check(Empty(connected.GetState()), "a pad discovered inside native polling cannot publish its first frame to gameplay");
                    Check(connected.GetPad().GetPressedButtons().Length == 0 && !modal.ReadModal(new PadState()).Cancel, "hotplug starts blocked and must release its held Escape");
                    hotplug.Down = new int[0]; modal.ReadModal(new PadState()); hotplug.Down = new[] {6};
                    Check(modal.ReadModal(new PadState()).Cancel && ReferenceEquals(manager.GetMain(), connected), "fresh controller Cancel reaches the modal and switches its button hints");
                    hotplug.Down = new int[0]; device.Down = new[] {3};
                    // force teardown while a direction is held, as on a failed page
                }
                Check(!InputContextLease.Active && pad.GetPad().GetPressedButtons().Length == 0, "forced close retains a release guard");
                NativeInputFrames.Publish(pad, All, All);
                Check(Empty(pad.GetState()) && worker.Suppress(true), "held gameplay and worker inputs cannot escape teardown");
                device.Down = new int[0]; pad.GetPad().GetPressedButtons();
                Check(worker.Suppress(false) && !worker.Suppress(true), "worker rearms only after its own release sample");
                device.Down = new[] {3};
                Check(Array.IndexOf(pad.GetPad().GetPressedButtons(), 3) >= 0, "ordinary controls return after release");
                NativeInputFrames.Publish(pad, new PadState {left=true}, new PadState {left=true});
                Check(pad.GetPressed().left, "normal publication resumes after modal disposal");
                device.Down = new int[0];
                using (var modal = InputContextLease.AcquireModal()) { Check(modal.IsCancelReleased(), "reopening acquires a clean input context"); }
                ChordVirtualizer.Remove(pad, "test.hud");
                Check(ReferenceEquals(pad.GetPad(), device), "modal teardown preserves and later removes the independent chord layer");
            }
            finally { ControllerManager.instance = previous; MenuController.instance = previousMenu; }
            Console.WriteLine("[OK] Modal controls: exclusive native/fast publication, HUD/Escape, remaps, chords, hotplug and release guards");
        }
    }
}
