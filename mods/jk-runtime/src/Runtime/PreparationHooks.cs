using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.GameManager;

namespace JKRuntime
{
    // Process-lifetime hooks: a same-world restart does not call BeforeLevelLoad.
    // The shared engine is already loaded by native discovery. Never load another.
    internal static class PreparationHooks
    {
        internal const string Id = "jk-runtime.preparation";
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool installed;
        private static bool preparedIntro;
        internal static string Status = "Not installed; synchronous fallback available";
        internal static void Install()
        {
            if (installed) return;
            var engines = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            if (engines.Length != 1) { Status = "Synchronous fallback: expected one Harmony engine, found " + engines.Length; return; }
            Type harmony = null;
            object owner = null;
            var applied = new List<Tuple<MethodInfo, MethodInfo>>();
            try
            {
                harmony = engines[0].GetType("HarmonyLib.Harmony", true);
                var metadata = engines[0].GetType("HarmonyLib.HarmonyMethod", true);
                owner = Activator.CreateInstance(harmony, new object[] { Id });
                var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
                foreach (var pair in new[] {
                    Tuple.Create(typeof(IntroState).GetMethod("OnNewRun", Flags), "PrepareIntro"),
                    Tuple.Create(typeof(GameLoop).GetMethod("OnPreGameStart", Flags), "Prepare"),
                    Tuple.Create(typeof(JKContentManager).GetMethod("LoadAssets", Flags), "LevelChanged"),
                    Tuple.Create(typeof(JumpGame).GetMethod("OnExit", Flags), "Exit"),
                    Tuple.Create(typeof(GameLoop).GetMethod("OnNewRun", Flags), "Handoff") })
                {
                    if (pair.Item1 == null) throw new MissingMethodException(pair.Item2);
                    var callback = typeof(PreparationHooks).GetMethod(pair.Item2, Flags);
                    var entry = Activator.CreateInstance(metadata, new object[] { callback });
                    metadata.GetField("priority").SetValue(entry, 800);
                    applied.Add(Tuple.Create(pair.Item1, callback));
                    patch.Invoke(owner, new object[] { pair.Item1, entry, null, null, null });
                }
                installed = true;
                Status = "Early preparation active: " + engines[0].FullName;
            }
            catch (Exception error)
            {
                if (applied.Count != 0)
                {
                    var unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) });
                    foreach (var pair in applied) unpatch.Invoke(owner, new object[] { pair.Item1, pair.Item2 });
                }
                Status = "Synchronous fallback: " + error.GetBaseException().Message;
                Console.WriteLine("[JK Runtime] " + Status);
            }
        }
        private static void PrepareIntro() { preparedIntro = false; RuntimeHost.PrepareAttempt(); preparedIntro = true; }
        private static void LevelChanged(JKContentManager __instance)
        // SetContentNode has selected the new root at this point. Hook the
        // nontrivial asset loader because the tiny SetLevel setter can be inlined.
        { preparedIntro = false; RuntimeHost.BeforeLevelLoad(__instance.root); }
        private static void Prepare() { if (preparedIntro) { preparedIntro = false; return; } RuntimeHost.PrepareAttempt(); }
        private static void Exit() { preparedIntro = false; RuntimeHost.ExitWorld(); }
        private static void Handoff() { StartupTrace.BeginGameplay(); }
    }
}
