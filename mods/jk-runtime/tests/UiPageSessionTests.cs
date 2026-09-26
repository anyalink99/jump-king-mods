using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using JumpKing.Controller;
using JKRuntime.UI;

namespace JKRuntime
{
    internal static class UiPageSessionTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Pad : IPad
        {
            public int[] GetPressedButtons() { return new int[0]; }
            public string ButtonToString(int value) { return value.ToString(); }
            public PadBinding GetDefaultBind() { return new PadBinding(); }
            public string GetSaveIdentifier() { return "page-session-fixture"; }
            public string GetPrintName() { return "Fixture"; }
            public bool IsConnected() { return true; }
        }
        private sealed class Factory { public void AddDrawable(object value) { } }
        private class Page : IUiPage
        {
            internal int Opens, Closes, Updates, Draws;
            internal bool FailOpen, FailDraw, FailUpdate, FailClose;
            internal Action Action;
            public bool WantsClose { get; set; }
            public void OnOpen() { Opens++; WantsClose = false; if (FailOpen) throw new Exception("open fixture"); }
            public void OnClose() { Closes++; if (FailClose) throw new Exception("close fixture"); }
            public void Update(UiInput input, float delta) { Updates++; if (Action != null) Action(); if (FailUpdate) throw new Exception("update fixture"); }
            public void Draw() { Draws++; if (FailDraw) throw new Exception("draw fixture"); }
        }
        private sealed class OwnCancelPage : Page, IUiPageInputPolicy
        { public bool HandlesCancel { get { return true; } } }
        private static UiInput Cancel { get { return new UiInput { Action = UiAction.Cancel, Cancel = true }; } }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws(Action work) { try { work(); } catch (Exception) { return; } throw new Exception("Expected failure"); }
        public static void Main()
        {
            var previous = ControllerManager.instance; var previousMenu = MenuController.instance;
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            ControllerManager.instance = manager;
            typeof(ControllerManager).GetField("_menu_controller", Fields).SetValue(manager, new MenuController(manager));
            typeof(ControllerManager).GetField("m_pads", Fields).SetValue(manager, new List<PadInstance> { new PadInstance(new Pad()) });
            UiPageInput.ReleaseProbe = () => true;
            try { Sessions(); Hosts(); Navigation(); }
            finally { UiPageInput.ReleaseProbe = null; ControllerManager.instance = previous; MenuController.instance = previousMenu; }
            Console.WriteLine("[OK] Page sessions: host parity, release boundaries, terminal faults, nested ownership and cleanup retry");
        }
        private static void Sessions()
        {
            var page = new Page(); var session = new UiPageSession(page); session.Open();
            session.Update(Cancel, .016f, false); Check(page.Updates == 0, "Opening input leaked");
            session.Update(default(UiInput), .016f, true);
            session.Update(Cancel, .016f, false); Check(!session.ReadyToClose, "Closed before release");
            session.Update(Cancel, .016f, false); Check(page.Updates == 1, "Repeated closing action");
            session.Update(default(UiInput), .016f, true); Check(session.ReadyToClose, "Cancel did not close");
            session.Dispose(); session.Dispose(); Check(page.Closes == 1, "Double close");
            var owned = new UiPageSession(new OwnCancelPage()); owned.Open(); owned.Update(default(UiInput), 0, true); owned.Update(Cancel, 0, true);
            Check(!owned.ReadyToClose, "Host overrode explicit Cancel policy"); owned.Dispose();
            var failed = new Page { FailOpen = true }; var partial = new UiPageSession(failed);
            Throws(partial.Open); Check(failed.Closes == 1 && partial.Faulted, "Partial open not cleaned");
            page = new Page { FailClose = true }; session = new UiPageSession(page); session.Open();
            Throws(session.Dispose); page.FailClose = false; session.Dispose(); Check(page.Closes == 2, "Cleanup lost its retry");
        }
        private static void Hosts()
        {
            // Real hosts, not only the shared session: default Back has identical results.
            var page = new Page(); var node = new EmbeddedMenuPageNode(new Factory(), page);
            node.Run(new TickData(.016f, 1));
            typeof(MenuController).GetField("_menu_state", Fields).SetValue(MenuController.instance, new PadState { cancel = true });
            Check(node.Run(new TickData(.016f, 2)) == BTresult.Success && page.Closes == 1, "Embedded Cancel contract");
            var button = new JumpKing.PauseMenu.BT.TextButton("Page", node, (Microsoft.Xna.Framework.Graphics.SpriteFont)null);
            Check(button.Run(new TickData(.016f, 3)) == BTresult.Failure, "Idle native row reopened a page");
            typeof(MenuController).GetField("_menu_state", Fields).SetValue(MenuController.instance, new PadState { confirm = true });
            Check(button.Run(new TickData(.016f, 4)) == BTresult.Running && page.Opens == 2, "Native Confirm could not reopen a completed page");
            node.ResetResult();
            var host = new ModalHost(); page = new Page(); Check(host.Open(page, UiModalOptions.Default), "Modal open");
            var update = typeof(ModalHost).GetMethod("Update", Fields);
            update.Invoke(host, new object[] { .016f });
            typeof(MenuController).GetField("_menu_state", Fields).SetValue(MenuController.instance, new PadState { cancel = true });
            update.Invoke(host, new object[] { .016f });
            Check(host.Depth == 0 && page.Closes == 1, "Modal Cancel contract");
            foreach (bool draw in new[] { true, false })
            {
                page = new Page { FailDraw = draw, FailUpdate = !draw };
                node = new EmbeddedMenuPageNode(new Factory(), page);
                node.Run(new TickData(.016f, 3)); if (draw) node.Draw();
                node.Run(new TickData(.016f, 4));
                node.Run(new TickData(.016f, 5)); node.Draw();
                Check(page.Opens == 1 && page.Closes == 1, "Fault automatically reopened page");
                node.ResetResult(); page.FailDraw = page.FailUpdate = false;
                node.Run(new TickData(.016f, 6)); Check(page.Opens == 2, "Explicit reentry blocked"); node.ResetResult();
            }
        }
        private static void Navigation()
        {
            var root = new Page(); var child = new Page();
            var stack = new UiPageStack(nav => root); stack.OnOpen(); stack.Update(default(UiInput), 0);
            root.Action = () => { root.Action = null; stack.Push(child); };
            stack.Update(default(UiInput), 0); stack.Draw();
            Check(stack.Depth == 2 && child.Updates == 0 && child.Draws == 1 && root.Draws == 0, "Only top owns input/draw");
            UiPageInput.ReleaseProbe = () => false; stack.Update(Cancel, 0); Check(child.Updates == 0, "Child received opening input");
            UiPageInput.ReleaseProbe = () => true; stack.Update(default(UiInput), 0); stack.Update(Cancel, 0);
            Check(stack.Depth == 1 && child.Closes == 1 && root.Updates == 1, "Back did not return exactly one layer");
            stack.Update(Cancel, 0); Check(root.Updates == 1, "Closing input leaked to parent");
            stack.Update(Cancel, 0); Check(stack.WantsClose && root.Closes == 1, "Root Back did not close stack");
            stack.OnClose(); stack.OnOpen(); Check(root.Opens == 2, "Fresh root lifetime missing");
            child = new Page { FailClose = true }; stack.Push(child);
            Throws(stack.OnClose); Check(root.Closes == 2 && stack.Depth == 1, "Child failure skipped parent cleanup");
            child.FailClose = false; stack.OnClose(); Check(stack.Depth == 0 && child.Closes == 2, "Failed cleanup was discarded");
            stack.OnOpen(); Throws(() => stack.Push(root)); Throws(() => stack.Push(stack)); stack.OnClose();
            var partial = new Page { FailOpen = true }; stack = new UiPageStack(nav => partial);
            Throws(stack.OnOpen); Check(partial.Closes == 1 && stack.Depth == 0, "Partial stack root leaked");
        }
    }
}
