using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using SmoothCamera;

internal static partial class CameraTests
{
    private static int startupContractChecks;
    private static void CountStartupContracts() { startupContractChecks++; }
    private static bool SkipStartupWorldEntities() { return false; }

    private static void StartupLifecycleTests()
    {
        var fixture = new Harmony("smooth-camera.startup-fixture");
        var runtime = typeof(JKRuntime.RuntimeApi).Assembly;
        var runtimeHost = runtime.GetType("JKRuntime.RuntimeHost", true);
        var sharedHooks = runtime.GetType("JKRuntime.PreparationHooks", true);
        const string sharedOwner = "jk-runtime.preparation";
        // Use the real SDK preparation dispatcher with this source-built module.
        var packageHost = typeof(JKRuntime.PackageHost);
        var packageType = packageHost.GetNestedType("Package", Flags);
        var packages = (IDictionary)packageHost.GetField("packages", Flags).GetValue(null);
        var package = Activator.CreateInstance(packageType, true);
        packageType.GetField("Id", Flags).SetValue(package, "smooth-camera");
        var preparation = Activator.CreateInstance(runtime.GetType("JKRuntime.PreparationLifetime"), Flags, null,
            new object[] { "smooth-camera", (Action<JKRuntime.RuntimeScope>)ModEntry.PrepareWorld,
                (Action<JKRuntime.RuntimeScope>)ModEntry.PrepareAttempt }, null);
        packageType.GetField("Preparation", Flags).SetValue(package, preparation);
        packages.Add("smooth-camera", package);
        var registration = JKRuntime.RuntimeApi.Register(new JKRuntime.ModuleDefinition("smooth-camera", new Version(1, 0), delegate { }));
        // Native save/cache dependencies are covered by Runtime's own fixtures.
        fixture.Patch(runtime.GetType("JKRuntime.Simulation.NativeItemReadCache").GetMethod("Ensure", Flags),
            prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("SkipStartupWorldEntities", Flags)));
        sharedHooks.GetMethod("Install", Flags).Invoke(null, null);
        fixture.Patch(typeof(Hooks).GetMethod("AssertContracts", Flags),
            prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("CountStartupContracts", Flags)));
        var introPreparation = typeof(GameLoop).GetMethod("OnPreGameStart");
        var worldExit = typeof(JumpGame).GetMethod("OnExit");
        foreach (var method in new[] { introPreparation, worldExit })
            fixture.Patch(method, prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("SkipStartupWorldEntities", Flags))
                { priority = Priority.Last });
        var loop = FormatterServices.GetUninitializedObject(typeof(GameLoop));
        var world = FormatterServices.GetUninitializedObject(typeof(JumpGame));
        var intro = AccessTools.Method(typeof(IntroState), "OnNewRun");
        var introState = FormatterServices.GetUninitializedObject(typeof(IntroState));
        var loopInstance = typeof(GameLoop).GetField("_instance", Flags);
        object previousLoop = loopInstance.GetValue(null);
        foreach (var name in new[] { "MakeBT", "InitializeEntities" })
            fixture.Patch(AccessTools.Method(typeof(IntroState), name),
                prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("SkipStartupWorldEntities", Flags)));
        var nativeCalls = PatchProcessor.GetOriginalInstructions(intro).Select(x => x.operand as MethodInfo).Where(x => x != null).ToList();
        Check(nativeCalls.IndexOf(introPreparation) >= 0 && nativeCalls.IndexOf(introPreparation) <
            nativeCalls.IndexOf(AccessTools.Method(typeof(IntroState), "InitializeEntities")),
            "Installed native intro prepares each attempt before constructing visible entities");
        Settings.Load();
        bool previous = Settings.Current.Smooth;
        try
        {
            loopInstance.SetValue(null, loop);
            Settings.Current.Smooth = true;
            ModEntry.Prepare(); // Once per loaded world, never once per restart.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int before = startupContractChecks;
                var preparationTimer = Stopwatch.StartNew();
                intro.Invoke(introState, null);
                preparationTimer.Stop();
                Console.WriteLine("[PERF] Native-intro preparation attempt " + attempt + ": "
                    + preparationTimer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms");
                int expected = before + (attempt == 0 ? 1 : 0);
                Check(Hooks.Installed && startupContractChecks == expected,
                    "Native intro reuses world patches when restart skips BeforeLevelLoad");
                Check(!Renderer.Running && !Renderer.HighRefreshRequested && !PresentationClock.Active,
                    "Prepared hooks leave intro presentation and cadence native");
                var timer = Stopwatch.StartNew();
                ModEntry.Start(); timer.Stop();
                Check(Renderer.Running && startupContractChecks == expected,
                    "Player handoff reuses prepared patches without recompilation");
                Console.WriteLine("[PERF] Prepared camera handoff: " + timer.Elapsed.TotalMilliseconds.ToString("F3",
                    System.Globalization.CultureInfo.InvariantCulture) + " ms");
                ModEntry.End(); ModEntry.Unload();
                runtimeHost.GetMethod("Stop", Flags).Invoke(null, null);
                Check(Hooks.Installed && !Renderer.Running && !PresentationClock.Active && CameraControls.Binding == null,
                    "Restart releases renderer and bindings while world patches remain dormant");
                Check(Harmony.GetPatchInfo(introPreparation).Owners.Contains(sharedOwner),
                    "Attempt cleanup retains the next-intro preparation hook");
            }
            intro.Invoke(introState, null);
            Settings.Current.Smooth = false;
            Settings.ApplyPresentationSetting();
            Check(!Hooks.Installed, "Disabling between loading and handoff removes prepared hooks");
            ModEntry.Start();
            Check(!Hooks.Installed, "Disabled handoff does not restore prepared hooks");
            ModEntry.Unload();
            int disabledChecks = startupContractChecks;
            intro.Invoke(introState, null); ModEntry.Start(); ModEntry.Unload();
            Check(startupContractChecks == disabledChecks && !Hooks.Installed,
                "Disabled loading and startup never compile camera patches");
            Settings.Current.Smooth = true;
            intro.Invoke(introState, null); worldExit.Invoke(world, null);
            Check(!Harmony.GetAllPatchedMethods().Any(x => Harmony.GetPatchInfo(x).Owners.Any(
                owner => owner == Hooks.Id)),
                "Native world exit before handoff releases prepared camera resources");
            Check(Harmony.GetPatchInfo(introPreparation).Owners.Contains(sharedOwner),
                "Shared process hook survives world exit without recompilation");
            ModEntry.Prepare(); intro.Invoke(introState, null);
            Check(Hooks.Installed, "Loading another world restores intro preparation");
            worldExit.Invoke(world, null);
        }
        finally
        {
            runtimeHost.GetMethod("ExitWorld", Flags).Invoke(null, null);
            packages.Remove("smooth-camera"); registration.Dispose();
            Settings.Current.Smooth = previous;
            loopInstance.SetValue(null, previousLoop);
            fixture.UnpatchAll(fixture.Id);
        }
    }
}
