using System;
using System.IO;
using System.Reflection;
using JKRuntime.UI;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void ToggleChecks(string output)
        {
            string path = Path.Combine(output, "toggle-" + Guid.NewGuid().ToString("N") + ".xml");
            var store = new MappingSettingsStore(path);
            Require(store.Enabled, "Mapping defaults to enabled");
            var field = typeof(MappingSettings).GetField("store", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = field.GetValue(null);
            field.SetValue(null, store);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            SceneFile scene = new SceneFile(); scene.Options.Timer = "hidden"; scene.Options.IntroText = "Custom intro";
            MappingState.Commit(scene);
            try
            {
                var checkbox = ModEntry.MainMenuEnabled(null, new JumpKing.PauseMenu.GuiFormat());
                Require(checkbox is JumpKing.PauseMenu.BT.Actions.IToggle && checkbox.toggle, "Mapping menu uses the native checkbox");
                checkbox.OverrideToggle(false);
                typeof(SettingToggle).GetMethod("OnToggle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(checkbox, null);
                Require(!ModEntry.IsPresentationEnabled && !new MappingSettingsStore(path).Enabled, "Checkbox disables presentation and persists across reload");
                Require(!ModEntry.PauseMenuEnabled(null, new JumpKing.PauseMenu.GuiFormat()).toggle, "Pause checkbox shares the persisted setting");
                Require((bool)typeof(NativeHooks).GetMethod("BeforeOverlay", flags).Invoke(null, null), "Disabled Mapping restores native timer");
                var text = new object[] { "Native intro" };
                typeof(NativeHooks).GetMethod("AfterIntroText", flags).Invoke(null, text);
                Require((string)text[0] == "Native intro", "Disabled Mapping restores native intro");
                Require((bool)typeof(NativeHooks).GetMethod("BeforeFinalBlit", flags).Invoke(null, new object[] { null }), "Disabled Mapping bypasses tint/mirror without touching render target");
                Require(ReferenceEquals(MappingState.Pending, scene), "Disabling retains scene state");
                checkbox.OverrideToggle(true);
                typeof(SettingToggle).GetMethod("OnToggle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(checkbox, null);
                Require(ModEntry.IsPresentationEnabled && new MappingSettingsStore(path).Enabled, "Re-enabling persists and restores presentation");
                Require(!(bool)typeof(NativeHooks).GetMethod("BeforeOverlay", flags).Invoke(null, null), "Re-enabling restores scene timer override");
            }
            finally { field.SetValue(null, previous); MappingState.Reset(); }
        }
    }
}
