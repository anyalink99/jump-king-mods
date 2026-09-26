using System;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.Props.RaymanWall;

namespace MegaMappingExpansion
{
    internal static class NativeHiddenWalls
    {
        private static OwnedPatches patches;
        private static System.Collections.Generic.HashSet<string> requested = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        internal static bool IsEvent(string name)
        { return name != null && (name.StartsWith("hiddenwallenter:", StringComparison.Ordinal) || name.StartsWith("hiddenwallexit:", StringComparison.Ordinal)); }
        internal static void Configure(SceneFile scene)
        {
            var events = scene == null ? new string[0] : BehaviorTreeCompiler.Events(scene).Where(IsEvent).ToArray();
            if (events.Length == 0) { Release(); return; }
            requested = new System.Collections.Generic.HashSet<string>(events, StringComparer.Ordinal);
            if (patches != null) return;
            Type type = typeof(Game1).Assembly.GetType("JumpKing.Props.RaymanWall.RaymanWallEntity", true);
            MethodInfo method = type.GetMethod("TouchPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo data = type.GetField("m_data", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo touching = type.GetField("m_last_touch", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null || method.ReturnType != typeof(bool) || method.GetParameters().Length != 0 || data == null
                || data.FieldType != typeof(RaymanData) || touching == null || touching.FieldType != typeof(bool))
                throw new NotSupportedException("Installed Jump King hidden-wall contact contract is incompatible");
            var owned = new OwnedPatches("mega-mapping-expansion.hidden-walls");
            try { owned.Add(method, postfix: typeof(NativeHiddenWalls).GetMethod("Observe", BindingFlags.NonPublic | BindingFlags.Static)); patches = owned; }
            catch { owned.Dispose(); throw; }
        }
        private static void Observe(bool __result, RaymanData ___m_data, bool ___m_last_touch)
        {
            SceneHost host = SceneHost.Current;
            if (!MappingSettings.Enabled || host == null || __result == ___m_last_touch) return;
            // The native hitboxes, fade and sound remain untouched. Observe the exact
            // contact result that drives Hidden Kingdom's mushrooms and illusion walls.
            string name = (__result ? "hiddenwallenter:" : "hiddenwallexit:") + ___m_data.texture_name;
            if (requested.Contains(name)) host.behaviors.Emit(name, Camera.CurrentScreenIndex1);
        }
        internal static void Release() { if (patches != null) { patches.Dispose(); patches = null; } requested.Clear(); }
    }
}
