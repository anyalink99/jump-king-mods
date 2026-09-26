using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    internal static class PointerHooks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static bool installed;
        private static readonly FieldInfo Items = typeof(MenuSelector).GetField("m_menu_items", Flags);
        private static readonly FieldInfo ChildResult = typeof(MenuSelector).GetField("m_last_child_result", Flags);
        private static readonly PropertyInfo Index = typeof(MenuSelector).GetProperty("Index", Flags);
        private static readonly FieldInfo MenuState = typeof(MenuController).GetField("_menu_state", Flags);
        internal static void Install()
        {
            StartupTrace.Initialize();
            if (installed) { PointerDiagnostics.Status("Installed: hooks already active"); return; }
            var engines = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            if (engines.Length != 1) { PointerDiagnostics.Status("Waiting: expected one loaded Harmony engine, found " + engines.Length); return; }
            StartupDiagnosticHooks.Configure(StartupTrace.Enabled);
            var engine = engines[0]; Type harmony = engine.GetType("HarmonyLib.Harmony"), metadata = engine.GetType("HarmonyLib.HarmonyMethod");
            var owner = Activator.CreateInstance(harmony, new object[] { "jk-runtime.ui.pointer" });
            var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            var applied = new List<Tuple<MethodInfo, MethodInfo>>();
            Action<Type, string, string, bool> hook = (type, method, callback, prefix) => {
                var target = type.GetMethod(method, Flags); var handler = typeof(PointerHooks).GetMethod(callback, Flags);
                if (target == null || handler == null) throw new MissingMethodException(type.FullName, method);
                applied.Add(Tuple.Create(target, handler));
                var entry = Activator.CreateInstance(metadata, new object[] { handler });
                patch.Invoke(owner, new[] { (object)target, prefix ? entry : null, prefix ? null : entry, null, null });
            };
            try
            {
                // Game1.MyUpdate/MyDraw are tiny forwarding methods. The JIT may
                // inline them during loading, before Runtime installs its hooks.
                // Hook the actual scene loop, still inside the native sprite batch.
                hook(typeof(JumpGame), "Update", "Update", true);
                hook(typeof(JumpGame), "Draw", "BeginDraw", true);
                hook(typeof(JumpGame), "Draw", "EndDraw", false);
                hook(typeof(MenuSelector), "Draw", "MenuDrawBegin", true);
                hook(typeof(MenuSelector), "Draw", "MenuDraw", false);
                hook(typeof(MenuSelectorClosePopup), "Draw", "MenuDrawBegin", true);
                hook(typeof(MenuSelectorClosePopup), "Draw", "MenuDraw", false);
                hook(typeof(MenuSelector), "MyRun", "MenuInput", true);
                hook(typeof(MenuSelector), "MyRun", "MenuInputDone", false);
                var fitText = typeof(TextInfo).GetMethod("CreateOneFittedInfo", new[] { typeof(GuiFormat), typeof(string), typeof(Color), typeof(Microsoft.Xna.Framework.Graphics.SpriteFont), typeof(int), typeof(char) });
                var rememberText = typeof(PointerHooks).GetMethod("RememberText", Flags);
                if (fitText == null) throw new MissingMethodException(typeof(TextInfo).FullName, "CreateOneFittedInfo");
                applied.Add(Tuple.Create(fitText, rememberText));
                patch.Invoke(owner, new[] { (object)fitText, null, Activator.CreateInstance(metadata, new object[] { rememberText }), null, null });
                installed = true;
                PointerDiagnostics.Status("Installed: " + engine.FullName);
            }
            catch (Exception error)
            {
                var unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) });
                foreach (var entry in applied) unpatch.Invoke(owner, new object[] { entry.Item1, entry.Item2 });
                PointerDiagnostics.Status("Install failed: " + error.GetBaseException().Message);
                throw;
            }
        }
        private static void Update() { using (StartupTrace.Measure("ui.pointer.update")) UiPointer.Update(); }
        private static void BeginDraw() { UiPointer.BeginDraw(); }
        private static void EndDraw() { UiPointer.EndDraw(); }
        private static void RememberText(string __1, TextInfo __result) { NativeMenuText.Remember(__1, __result); }
        private static void TraceUpdateBegin() { StartupTrace.BeginUpdate(); }
        private static void TraceUpdateEnd() { StartupTrace.EndUpdate(); }
        private static void TraceDrawBegin() { StartupTrace.BeginDraw(); }
        private static void TraceDrawEnd() { StartupTrace.EndDraw(); }
        private static void TraceWorkBegin(out long __state) { __state = StartupTrace.BeginWork(); }
        private static void TraceObjectEnd(object __instance, MethodBase __originalMethod, long __state)
        {
            if (__state == 0) return;
            long end = System.Diagnostics.Stopwatch.GetTimestamp();
            StartupTrace.EndWork(__originalMethod.Name, __instance.GetType(), __state, end, true);
        }
        private static void TracePhaseEnd(MethodBase __originalMethod, long __state)
        {
            if (__state == 0) return;
            long end = System.Diagnostics.Stopwatch.GetTimestamp();
            StartupTrace.EndWork(__originalMethod.Name, __originalMethod.DeclaringType, __state, end, false);
        }
        private static void MenuInput(MenuSelector __instance, ref IMenuItem[] ___m_menu_items,
            ref int ____index, ref BTresult ___m_last_child_result, out bool __state)
        {
            VanillaMenuAdapter.ApplyPending(__instance);
            VanillaMenuAdapter.RepairIdleSelection(___m_menu_items, ____index, ___m_last_child_result);
            var input = UiPointer.Read(__instance, new UiInput());
            __state = input.Action != UiAction.None && ___m_last_child_result != BTresult.Running;
            if (__state) {
                PadState state = ControllerManager.instance.MenuController.GetPadState();
                __state = UiInputRouter.Resolve(state, true) == UiAction.None;
                if (__state) { state = UiInputRouter.ToPadState(input); MenuState.SetValue(ControllerManager.instance.MenuController, state); }
            }
            if (CompactInventory.Handles(__instance) && CompactInventory.Navigate(__instance, ___m_menu_items,
                ref ____index, ___m_last_child_result, ControllerManager.instance.MenuController.GetPadState())) {
                UiInputRouter.Consume(); __state = true;
            }
        }
        private static void MenuInputDone(IMenuItem[] ___m_menu_items, bool __state, BTresult __result)
        {
            if (__state) UiInputRouter.Consume();
            if (__result != BTresult.Running) VanillaMenuAdapter.ResetRunningRows(___m_menu_items);
        }
        private static bool IsDrawn(MenuSelector menu, int count, int index, BTresult child)
        {
            return menu.last_result == BTresult.Running && !(menu is MenuSelectorClosePopup
                && child == BTresult.Running && count > 2 && index == count - 2);
        }
        private static bool MenuDrawBegin(MenuSelector __instance)
        {
            if (__instance.last_result != BTresult.Running) return true;
            var items = (IMenuItem[])Items.GetValue(__instance);
            if (items == null || items.Length == 0 || !IsDrawn(__instance, items.Length, (int)Index.GetValue(__instance, null), (BTresult)ChildResult.GetValue(__instance))) return true;
            if (CompactInventory.Draw(__instance, items, (int)Index.GetValue(__instance, null), (BTresult)ChildResult.GetValue(__instance) == BTresult.Running)) return false;
            return !NativeMenuViewport.Draw(__instance, items, (int)Index.GetValue(__instance, null));
        }
        private static void MenuDraw(MenuSelector __instance)
        {
            if (CompactInventory.IsAdapted(__instance)) return;
            if (__instance.last_result != BTresult.Running || (BTresult)ChildResult.GetValue(__instance) == BTresult.Running) return;
            var items = (IMenuItem[])Items.GetValue(__instance); if (items == null || items.Length == 0) return;
            var format = VanillaMenuAdapter.GetFormat(__instance); var bounds = __instance.GetBounds();
            NativeMenuViewport.Layout layout;
            NativeMenuViewport.TryGet(__instance, out layout);
            UiPointer.BeginSurface(__instance);
            int y = bounds.Y + format.padding.top;
            int first = layout == null ? 0 : layout.First, last = layout == null ? items.Length : layout.Last;
            if (layout != null) y = layout.ContentY;
            for (int i = first; i < last; i++)
            {
                int selected = i; int height = items[i].GetSize().Y;
                var row = new Rectangle(bounds.X + format.padding.left, y, Math.Max(20, bounds.Width - format.padding.width), height);
                Action hover = () => Index.SetValue(__instance, selected, null);
                if (items[i] is ISlider || items[i] is IOptions)
                {
                    int split = row.X + items[i].GetSize().X / 2 + 5;
                    UiPointer.ActionRegion(new Rectangle(row.X, y, split - row.X, height), UiAction.Left, hover);
                    UiPointer.ActionRegion(new Rectangle(split, y, Math.Max(1, row.Right - split), height), UiAction.Right, hover);
                }
                else if (!(items[i] is UnSelectable)) UiPointer.ActionRegion(row, UiAction.Confirm, hover);
                y += height + format.element_margin;
            }
            UiPointer.ScrollRegion(bounds, delta => {
                int step = delta > 0 ? -1 : 1, current = (int)Index.GetValue(__instance, null);
                for (int n = 0; n < Math.Abs(delta); n++)
                {
                    int next = current + step;
                    while (next >= 0 && next < items.Length && items[next] is UnSelectable) next += step;
                    if (next < 0 || next >= items.Length) break; current = next;
                }
                Index.SetValue(__instance, current, null);
            });
        }
    }
}
