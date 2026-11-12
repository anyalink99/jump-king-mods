using System;
using System.Linq;
using System.Reflection;
using BehaviorTree;
using HarmonyLib;
using JumpKing.GameManager.MultiEnding.NormalEnding;
using MegaMappingExpansion.Endings;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        [HarmonyPatch(typeof(NormalEnding), "MakeBT")]
        private static class ClassEndingPatch
        {
            public static void Postfix(ref BTmanager __result) { }
        }

        [HarmonyPatch(typeof(NormalEnding))]
        private static class MethodEndingPatch
        {
            [HarmonyPatch("MakeBT"), HarmonyPostfix]
            private static void Renamed(object __instance, MethodBase __originalMethod, ref BTmanager __result) { }

            [HarmonyPatch("CheckWin"), HarmonyPostfix]
            private static void DifferentTarget() { }
        }

        private static void ManualEndingPostfix(ref BTmanager __result) { }

        private static void EndingCallbackDiscoveryChecks()
        {
            NativeEndings.AssertContracts();
            var binding = NativeEndings.Bindings.Single(b => b.Role == "main_ending");
            var assembly = typeof(Tests).Assembly;
            var legacy = typeof(ClassEndingPatch).GetMethod("Postfix");
            var renamed = typeof(MethodEndingPatch).GetMethod("Renamed", BindingFlags.Static | BindingFlags.NonPublic);
            var manual = typeof(Tests).GetMethod("ManualEndingPostfix", BindingFlags.Static | BindingFlags.NonPublic);
            var expected = new[] { legacy, renamed };
            Require(NativeEndings.ForeignCallbacks(assembly, binding).OrderBy(m => m.Name).SequenceEqual(expected.OrderBy(m => m.Name)),
                "Ending discovery accepts class/method targets and renamed private callbacks without claiming CheckWin");
            Require(NativeEndings.ForeignCallbacks(assembly, NativeEndings.Bindings.Single(b => b.Role == "nbp_crow")).Length == 0,
                "A provider without a role callback has no ending overlap");
            var owner = new Harmony("mme.tests.ending-discovery");
            try
            {
                owner.Patch(binding.Method, postfix: new HarmonyMethod(renamed));
                owner.Patch(binding.Method, postfix: new HarmonyMethod(manual));
                var found = NativeEndings.ForeignCallbacks(assembly, binding);
                Require(found.Length == 3 && found.Contains(manual) && found.Count(m => m == renamed) == 1,
                    "Registered targets find manual patches and deduplicate annotated callbacks");
            }
            finally { owner.UnpatchAll(owner.Id); }
        }
    }
}
