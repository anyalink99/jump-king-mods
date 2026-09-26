using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void InnocentPostfix() { }
        private static void ExpectPatchRefusal(params string[] owners)
        {
            try { SolveCapture.AuditPatches(typeof(BodyComp).Assembly, new VerticalWindAdapter()); }
            catch (NotSupportedException e)
            {
                Check(owners.All(o => e.Message.Contains(o)), "Patch report dropped an owner: " + e.Message);
                return;
            }
            throw new Exception("An unknown patch bypassed the adapter audit");
        }
        private static void VerticalWindParity()
        {
            var assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VerticalWindMod.dll"));
            var transpiler = assembly.GetType(VerticalWindAdapter.PatchType, true).GetMethod("Transpiler");
            var target = typeof(WindVelocityUpdateBehaviour).GetMethod("ExecuteBehaviour");
            var managerType = assembly.GetType("VerticalWindMod.VerticalWindManager", true);
            var singleton = managerType.GetField("_instance", NativeWorld.Flags);
            object previous = singleton.GetValue(null);
            var harmony = new Harmony(VerticalWindAdapter.Owner);
            var stranger = new Harmony("fixture.unknown-wind");
            var another = new Harmony("fixture.unknown-gravity");
            var postfix = new HarmonyMethod(typeof(Tests).GetMethod("InnocentPostfix", NativeWorld.Flags));
            try
            {
                var adapter = new VerticalWindAdapter();
                Check(!adapter.Accepts(target, "Prefixes", VerticalWindAdapter.Owner, transpiler) &&
                    !adapter.Accepts(target, "Transpilers", "wrong-owner", transpiler) &&
                    !adapter.Accepts(target, "Transpilers", VerticalWindAdapter.Owner, postfix.method), "Adapter accepted a lookalike patch");
                Check(adapter.Accepts(target, "Transpilers", VerticalWindAdapter.Owner, transpiler), "Installed Vertical Wind fixture was not recognized");
                singleton.SetValue(null, null);
                Check(!adapter.Capture(3).Any(v => v) && singleton.GetValue(null) == null, "Capture initialized the foreign singleton");
                // Install the actual third-party transpiler, not a test reimplementation.
                harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));
                SolveCapture.AuditPatches(typeof(BodyComp).Assembly, new VerticalWindAdapter());
                var manager = Activator.CreateInstance(managerType); singleton.SetValue(null, manager);
                var screens = (HashSet<int>)managerType.GetField("_screensWithWind", NativeWorld.Flags).GetValue(manager);
                screens.Add(1);
                var mask = adapter.Capture(3); screens.Clear();
                Check(mask.SequenceEqual(new[] { false, true, false }), "Screen marker capture aliases live mod state");
                WorldParity(adapter.Capture(3)); // Installed patch, no marker: vanilla.
                screens.Add(1);
                WorldParity(mask); // Marked screen and unmarked teleport destinations.
                screens.Add(99);
                bool invalid = false;
                try { adapter.Capture(3); } catch (NotSupportedException) { invalid = true; }
                Check(invalid, "Invalid foreign screen marker ignored"); screens.Remove(99);

                stranger.Patch(target, postfix: postfix);
                another.Patch(typeof(ApplyGravityBehaviour).GetMethod("ExecuteBehaviour"), postfix: postfix);
                ExpectPatchRefusal(stranger.Id, another.Id);
                stranger.UnpatchAll(stranger.Id); another.UnpatchAll(another.Id);
                // Sharing a trusted owner is not sufficient for acceptance.
                harmony.Patch(target, postfix: postfix);
                ExpectPatchRefusal(VerticalWindAdapter.Owner);
                harmony.Unpatch(target, HarmonyPatchType.Postfix, harmony.Id);
                stranger.Patch(managerType.GetMethod("HasWind"), postfix: postfix);
                ExpectPatchRefusal(stranger.Id);
                Console.WriteLine("[OK] Vertical Wind identity, immutable markers, unknown/co-owned/manager patch rejection and aggregated diagnostics");
            }
            finally
            {
                stranger.UnpatchAll(stranger.Id); another.UnpatchAll(another.Id); harmony.UnpatchAll(harmony.Id);
                singleton.SetValue(null, previous);
            }
        }
    }
}
