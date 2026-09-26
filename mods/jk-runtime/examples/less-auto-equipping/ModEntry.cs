using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JKRuntime;
using JKRuntime.Modules;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
using LessAutoEquipping.Patches;

[assembly: AssemblyTitle("LessAutoEquipping Runtime Example")]
[assembly: AssemblyCopyright("Original mod Copyright (c) 2025 Zebra; MIT License")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace LessAutoEquipping
{
    // Derived from Zebra's LessAutoEquipping. See LICENSE.md and README.md.
    [RuntimeModule("zebra.less-auto-equipping", "LessAutoEquipping Runtime Example")]
    public static class ModEntry
    {
        public const string HarmonyOwner = "Zebra.LessAutoEquipping.Harmony";
        private static PreferencesStore store;
        public static Preferences Preferences { get { return Store.Current; } }
        private static PreferencesStore Store
        {
            get
            {
                if (store == null) store = new PreferencesStore(PackageHost.GetDataDirectory(typeof(ModEntry).Assembly));
                return store;
            }
        }

        // Install before intro rewards can execute. The world owns the patches;
        // attempts reuse them and world exit/failed preparation releases them.
        [OnWorldReady]
        public static void PrepareWorld(RuntimeScope scope)
        {
            foreach (var mod in JumpKing.Mods.ModLoader.Instance.LoadedMods)
            {
                var entry = mod.Assembly.GetType("LessAutoEquipping.ModEntry");
                if (entry != null && entry.IsDefined(typeof(JumpKing.Mods.JumpKingModAttribute), false))
                    throw new InvalidOperationException("Disable the original LessAutoEquipping before enabling its Runtime example.");
            }
            var preferences = Preferences; // Read first; malformed settings must not leave installed patches.
            var targets = new[] {
                AccessTools.Method("JumpKing.GameManager.MultiEnding.GiveWearableItemNode:MyRun"),
                AccessTools.Method("JumpKing.MiscEntities.Merchant.MerchantComp:OnSell"),
                AccessTools.Method("JumpKing.MiscEntities.WorldItems.WorldItemComp:OnPickup")
            };
            foreach (var target in targets)
            {
                if (target == null) throw new MissingMethodException("LessAutoEquipping native target is unavailable");
                var patches = Harmony.GetPatchInfo(target);
                if (patches != null && patches.Owners.Contains(HarmonyOwner))
                    throw new InvalidOperationException("LessAutoEquipping patches are already active; keep only one copy enabled.");
            }
            var owned = scope.Own(new OwnedPatches(HarmonyOwner));
            owned.Add(targets[0], transpiler: typeof(PatchGiveWearableItemNode).GetMethod("Transpiler"));
            owned.Add(targets[1], transpiler: typeof(PatchMerchantComp).GetMethod("Transpiler"));
            owned.Add(targets[2], transpiler: typeof(PatchWorldItemComp).GetMethod("Transpiler"));
        }

        // Separate exported factories preserve both locations in the package shell.
        [MainMenuItemSetting]
        public static TogglePreventAutoEquip MainMenu(object factory, GuiFormat format) { return new TogglePreventAutoEquip(); }
        [PauseMenuItemSetting]
        public static TogglePreventAutoEquip PauseMenu(object factory, GuiFormat format) { return new TogglePreventAutoEquip(); }

        public static void SetPreventAutoEquip(bool value) { Store.Set(value); }
    }

    public sealed class TogglePreventAutoEquip : ITextToggle
    {
        public TogglePreventAutoEquip() : base(ModEntry.Preferences.ShouldPreventAutoEquip) { }
        protected override string GetName() { return "Disable auto-equip"; }
        protected override void OnToggle() { ModEntry.SetPreventAutoEquip(!ModEntry.Preferences.ShouldPreventAutoEquip); }
    }
}
