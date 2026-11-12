using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using JKRuntime;
using JKRuntime.UI;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.PauseMenu.BT.Actions.ScreenOptions;
using Microsoft.Xna.Framework;

namespace FreeScaling
{
    internal static class Menu
    {
        internal static void Install(OwnedPatches hooks)
        {
            var factory = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.MenuFactory", true);
            hooks.Add(factory.GetMethod("CreateGraphicsOptions", OwnedPatches.Members), postfix: Method("Graphics"));
            hooks.Add(typeof(SizeModeOption).GetMethod("CanChange", OwnedPatches.Members), prefix: Method("CanResize"));
            hooks.Add(typeof(ScreenOptionsLabelButton).GetMethod("MyRun", OwnedPatches.Members), prefix: Method("ModeRun"));
            hooks.Add(typeof(ScreenOptionsLabelButton).GetMethod("Draw"), prefix: Method("ModeDraw"));
            // other native preference edits mustn't snap the live window to 2x
            var prefs = typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.Persocom.GraphicsPreferencesRuntime", true);
            hooks.Add(prefs.GetMethod("ApplySizeMode", OwnedPatches.Members), prefix: Method("NativeSize"));
        }
        private static MethodInfo Method(string name) { return typeof(Menu).GetMethod(name, OwnedPatches.Members); }
        private static bool NativeSize() { return !ModEntry.Enabled; }
        private static bool CanResize(ref bool __result)
        { if (!ModEntry.Enabled) return true; __result = false; return false; }
        private static bool ModeRun(ref BTresult __result)
        { if (!ModEntry.Enabled) return true; __result = BTresult.Failure; return false; }
        private static bool ModeDraw(int x, int y)
        { if (!ModEntry.Enabled) return true; MenuItemHelper.Draw(x, y, "Windowed", Color.Gray); return false; }
        internal static void Graphics(IBTnode __result)
        {
            var menu = __result as MenuSelector;
            if (menu == null) throw new NotSupportedException("The graphics menu layout changed");
            var rows = new List<IBTnode>(NativeMenuRows.Read(menu));
            if (rows.Exists(row => row is FilterOption)) return;
            int index = rows.FindIndex(row => row is SizeModeOption);
            if (index < 0) throw new NotSupportedException("The native size selector is missing");
            rows.Insert(index + 1, new SettingToggle(ModEntry.EnableSetting, "Free scaling", () => Settings.File.CanSave));
            rows.Insert(index + 2, new FilterOption());
            rows.Insert(index + 3, new SettingToggle(JKRuntime.Presentation.SmoothRendering.Setting));
            NativeMenuRows.Replace(menu, rows.ToArray());
        }
        internal sealed class FilterOption : IOptions
        {
            internal FilterOption() : base(2, (int)Settings.Current.Sampling, EdgeMode.Wrap) { }
            protected override bool CanChange() { return ModEntry.Enabled && Settings.File.CanSave; }
            protected override string CurrentOptionName()
            { return "Pixels: " + Settings.Current.Sampling; }
            protected override void OnOptionChange(int option)
            { if (CanChange()) ModEntry.SetSampling((PixelSampling)option); }
        }
    }
}
