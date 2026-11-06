using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JumpKing.PauseMenu.BT.Actions;
using JumpKingMultiplayer;
using JumpKingMultiplayer.Menu.DisplayOptions;

namespace MultiplayerExpansion
{
    internal sealed class OpacityProfiles
    {
        internal float Ghost, Solid;
        internal OpacityProfiles(float ghost) { Ghost = Valid(ghost) ? ghost : .6f; Solid = 1; }
        internal static bool Valid(float value) { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1; }
        internal float Get(bool solid) { return solid ? Solid : Ghost; }
        internal static OpacityProfiles Read(string path, float ghost)
        {
            var result = new OpacityProfiles(ghost);
            if (!File.Exists(path)) return result;
            var fields = File.ReadAllText(path).Trim().Split(' ');
            float first, second;
            if (fields.Length != 2 || !float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out first)
                || !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out second) || !Valid(first) || !Valid(second))
                throw new FormatException("Invalid opacity profiles; original file kept.");
            result.Ghost = first; result.Solid = second; return result;
        }
        internal void Write(string path)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, Ghost.ToString("R", CultureInfo.InvariantCulture) + " " + Solid.ToString("R", CultureInfo.InvariantCulture));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
    }

    internal static class ModeOpacity
    {
        private static readonly FieldInfo raw = AccessTools.Field(typeof(Preferences), "_ghostPlayerOpacity");
        private static readonly FieldInfo option = AccessTools.Field(typeof(IOptions), "m_current");
        private static OpacityProfiles profiles;
        private static string path;
        private static bool blocked;
        internal static string Error = "";
        internal static void Reset() { path = null; profiles = null; blocked = false; Error = ""; }
        internal static void Install(Harmony hooks, string storage = null)
        {
            path = storage ?? Path.Combine(NativeMod.Package, "opacity-profiles.txt");
            profiles = null; blocked = false; Error = "";
            hooks.Patch(AccessTools.PropertyGetter(typeof(Preferences), "GhostPlayerOpacity"), prefix: new HarmonyMethod(typeof(ModeOpacity), "Read"));
            hooks.Patch(AccessTools.PropertySetter(typeof(Preferences), "GhostPlayerOpacity"), prefix: new HarmonyMethod(typeof(ModeOpacity), "Write"));
            hooks.Patch(AccessTools.PropertyGetter(typeof(IOptions), "CurrentOption"), prefix: new HarmonyMethod(typeof(ModeOpacity), "ReadOption"));
            hooks.Patch(AccessTools.PropertySetter(typeof(IOptions), "CurrentOption"), prefix: new HarmonyMethod(typeof(ModeOpacity), "PrepareOption"));
            hooks.Patch(AccessTools.Method(typeof(ModEntry), "BeforeLevelLoad"), postfix: new HarmonyMethod(typeof(ModeOpacity), "Sync"));
            Sync();
        }
        internal static void Sync()
        {
            var prefs = ModEntry.Preferences;
            if (path == null || prefs == null || !Ensure(prefs)) return;
            // native renderers may have inlined the getter before our hooks were installed
            raw.SetValue(prefs, profiles.Get(IsSolid));
        }
        private static bool Ensure(Preferences prefs)
        {
            // XML deserialization uses another instance; don't interpret it as a user edit
            if (!ReferenceEquals(prefs, ModEntry.Preferences)) return false;
            if (profiles != null) return true;
            float original = (float)raw.GetValue(prefs);
            try {
                profiles = OpacityProfiles.Read(path, original);
                // persist the original ghost value before native saves serialize Solid's value
                if (!File.Exists(path)) profiles.Write(path);
            }
            catch (Exception e) {
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is FormatException)) throw;
                profiles = new OpacityProfiles(original); blocked = true; Error = "Cannot save mode opacity: " + e.Message;
            }
            return true;
        }
        private static bool IsSolid { get { return AdvancedSession.Rules != InteractionRules.Ghosts; } }
        private static bool Read(Preferences __instance, ref float __result)
        {
            if (!Ensure(__instance)) return true;
            __result = profiles.Get(IsSolid); raw.SetValue(__instance, __result); return false;
        }
        private static bool Write(Preferences __instance, float value)
        {
            if (!Ensure(__instance)) return true;
            if (blocked || !OpacityProfiles.Valid(value)) return false;
            float old = profiles.Get(IsSolid);
            if (old == value) return true;
            if (IsSolid) profiles.Solid = value; else profiles.Ghost = value;
            try { profiles.Write(path); Error = ""; return true; }
            catch (Exception e) {
                if (!(e is IOException) && !(e is UnauthorizedAccessException)) throw;
                if (IsSolid) profiles.Solid = old; else profiles.Ghost = old;
                Error = "Cannot save mode opacity: " + e.Message; return false;
            }
        }
        private static bool ReadOption(IOptions __instance, ref int __result)
        {
            if (!(__instance is GhostPlayerOpacityOption) || ModEntry.Preferences == null) return true;
            __result = (int)Math.Round(ModEntry.Preferences.GhostPlayerOpacity * 10); return false;
        }
        private static void PrepareOption(IOptions __instance)
        {
            // existing menu nodes cache the old mode's value; synchronize without firing an edit
            if (__instance is GhostPlayerOpacityOption && ModEntry.Preferences != null)
                option.SetValue(__instance, (int)Math.Round(ModEntry.Preferences.GhostPlayerOpacity * 10));
        }
    }
}
