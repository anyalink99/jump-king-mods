using System;
using System.IO;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing.PauseMenu.BT.Actions;
using JumpKingMultiplayer;
using JumpKingMultiplayer.Menu.DisplayOptions;

namespace MultiplayerExpansion
{
    internal static class ModeOpacityTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        // invoke patched accessors even when this test method was JITted before Install
        private static float Get(Preferences prefs) { return (float)AccessTools.Property(typeof(Preferences), "GhostPlayerOpacity").GetValue(prefs,null); }
        private static void Set(Preferences prefs,float value) { AccessTools.Property(typeof(Preferences), "GhostPlayerOpacity").SetValue(prefs,value,null); }
        private static int Get(IOptions menu) { return (int)AccessTools.Property(typeof(IOptions), "CurrentOption").GetValue(menu,null); }
        private static void Set(IOptions menu,int value) { AccessTools.Property(typeof(IOptions), "CurrentOption").SetValue(menu,value,null); }
        internal static void Run(string root)
        {
            string path = Path.Combine(root, "opacity-profiles.txt");
            var rules = AccessTools.Property(typeof(AdvancedSession), "Rules");
            var prefs = AccessTools.Property(typeof(ModEntry), "Preferences");
            var oldRules = rules.GetValue(null, null); var oldPrefs = prefs.GetValue(null, null);
            var hooks = new Harmony("multiplayer-expansion.opacity-test");
            try
            {
                var original = new Preferences { GhostPlayerOpacity=.4f };
                prefs.SetValue(null, original, null); rules.SetValue(null, (InteractionRules)3, null);
                ModeOpacity.Install(hooks, path);
                Check(Get(original) == 1, "Solid didn't start at full opacity");
                Check(OpacityProfiles.Read(path, 0).Ghost == .4f, "Starting in Solid lost the original Ghost opacity");
                // avoid graphics, but exercise the installed native menu getters/setters and callback
                var menu = (IOptions)FormatterServices.GetUninitializedObject(typeof(GhostPlayerOpacityOption));
                AccessTools.Field(typeof(IOptions), "m_option_count").SetValue(menu, 11);
                Check(Get(menu) == 10, "Existing native menu didn't show Solid opacity");
                Set(menu, 7);
                Check(Math.Abs(Get(original)-.7f)<.0001f, "Native Multiplayer control didn't edit Solid");
                rules.SetValue(null, InteractionRules.Ghosts, null);
                Check(Get(original) == .4f && Get(menu) == 4, "Ghost didn't restore its independent value");
                Set(menu, 2);
                rules.SetValue(null, (InteractionRules)3, null);
                Check(Get(menu) == 7, "Mode switch left the native option stale");
                Set(menu, 2);
                Check(Math.Abs(Get(original)-.2f)<.0001f, "Cached option suppressed an edit after switching modes");
                Set(menu, 8);
                var replacement = new Preferences { GhostPlayerOpacity=.9f };
                prefs.SetValue(null, replacement, null);
                Check(Math.Abs(Get(replacement)-.8f)<.0001f, "Preferences reload replaced the Solid profile");
                hooks.UnpatchAll(hooks.Id); ModeOpacity.Install(hooks, path);
                rules.SetValue(null, InteractionRules.Ghosts, null);
                Check(Math.Abs(Get(replacement)-.2f)<.0001f, "Restart lost the Ghost profile");
                rules.SetValue(null, InteractionRules.Solid, null);
                Check(Math.Abs(Get(replacement)-.8f)<.0001f, "Restart lost the Solid profile");
                Set(replacement, float.NaN);
                Check(Math.Abs(Get(replacement)-.8f)<.0001f, "Invalid opacity replaced a valid value");
                Set(replacement, 0); Check(Get(replacement)==0, "Zero opacity was rejected");
                Set(replacement, 1); Check(Get(replacement)==1, "Full opacity was rejected");
                string corrupt = Path.Combine(root,"bad-opacity.txt"); File.WriteAllText(corrupt,"broken");
                bool refused=false; try { OpacityProfiles.Read(corrupt,.6f); } catch(FormatException) {refused=true;}
                Check(refused && File.ReadAllText(corrupt)=="broken", "Corrupt profiles were overwritten");

                string settings = Path.Combine(root,"mode-migration.txt");
                File.WriteAllText(settings,"1"); Check(InteractionSettings.Read(settings)==(InteractionRules)3,"Legacy Platforms didn't migrate to Solid without pushing");
                InteractionSettings.Write(settings,(InteractionRules)3); InteractionSettings.Write(settings,InteractionRules.Ghosts);
                Check(InteractionSettings.Read(settings)==InteractionRules.Ghosts && InteractionSettings.ReadSolid(settings)==(InteractionRules)3,"Ghost mode forgot Push off");
                InteractionSettings.Write(settings,InteractionRules.Solid); InteractionSettings.Write(settings,InteractionRules.Ghosts);
                Check(InteractionSettings.ReadSolid(settings)==InteractionRules.Solid,"Ghost mode forgot Push on");
            }
            finally { hooks.UnpatchAll(hooks.Id); ModeOpacity.Reset(); rules.SetValue(null,oldRules,null); prefs.SetValue(null,oldPrefs,null); }
            Console.WriteLine("[OK] Modes/opacity: old settings migration, push memory, original Multiplayer control, separate values, live switching, restart and invalid data");
        }
    }
}
