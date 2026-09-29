using System;
using EntityComponent;
using JumpKing.Player;

namespace JKRuntime
{
    internal static class RuntimeHost
    {
        private static bool initialized;
        private static ActivationBarrier pending;
        private static GameContract contract;
        private static RuntimeScope level;
        private static bool attemptPrepared;
        private static string preparedWorldRoot;
        internal static GameContract Contract { get { Initialize(); return contract; } }
        internal static void Initialize()
        {
            RuntimeApi.Kernel.CheckThread();
            if (initialized) return;
            contract = new GameContract();
            RuntimeApi.Kernel.AddBuiltin(new CapabilityDefinition("jk.ui", 1, 0), new UiServices());
            if (contract.Available)
                RuntimeApi.Kernel.AddBuiltin(new CapabilityDefinition("jk.game.readonly", 1, 0), contract);
            initialized = true;
        }
        internal static void BeforeLevelLoad()
        { BeforeLevelLoad(JumpKing.Game1.instance.contentManager.root); }
        internal static void BeforeLevelLoad(string root) { BeforeWorld(root, false); }
        private static void BeforeWorld(string root, bool preserveDecoded)
        {
            var decoded = preserveDecoded ? Gameplay.MapMechanics.CaptureDecoded(root) : null;
            Gameplay.RunModifiers.Unload();
            Gameplay.ModifierRegistrationObserver.TryInstall();
            ExitWorld(); Initialize(); PreparationHooks.Install();
            MapPolicy.Load(root);
            PackageHost.DiscoverMap(root);
            PackageHost.BeforeLevel();
            if (decoded != null) Gameplay.MapMechanics.RestoreDecoded(root, decoded);
            Gameplay.MapMechanics.ValidateFactory();
            preparedWorldRoot = System.IO.Path.GetFullPath(root);
            // don't clear registrations here, another mod may already have registered
            // for this level in its BeforeLevelLoad callback
        }
        internal static void ScheduleStart()
        {
            Initialize();
            if (pending != null || RuntimeApi.State != "idle") return;
            pending = new ActivationBarrier();
        }
        internal static void StartLevel()
        {
            if (!attemptPrepared) PrepareAttempt();
            StartupTrace.BeginGameplay();
            if (level != null) throw new InvalidOperationException("Runtime level already started");
            level = new RuntimeScope();
            try
            {
            using (StartupTrace.Measure("runtime.map-mechanics-entry")) Gameplay.MapMechanics.CommitEntry();
            // every mod's synchronous BeforeLevelLoad (including MTO PatchAll)
            // has completed. no entity update/NPC dialogue has run yet
            using (StartupTrace.Measure("runtime.item-read-cache")) Simulation.NativeItemReadCache.Ensure();
            using (StartupTrace.Measure("runtime.text-compatibility")) Compatibility.MoreTextOptionsCompatibility.TryInstall();
            using (StartupTrace.Measure("runtime.conveyor-compatibility")) Compatibility.ConveyorCompatibility.TryInstall();
            using (StartupTrace.Measure("runtime.modifier-observer")) Gameplay.ModifierRegistrationObserver.TryInstall();
            using (StartupTrace.Measure("runtime.run-modifiers")) Gameplay.RunModifiers.Start();
            level.Defer(JKRuntime.UI.UIApiInstaller.Uninstall);
            using (StartupTrace.Measure("runtime.ui-install")) JKRuntime.UI.UIApiInstaller.Install();
            JKRuntime.Settings.Commands.Start();
            level.Defer(JKRuntime.Settings.Commands.Stop);
            PlayerEntity player = EntityManager.instance == null ? null : EntityManager.instance.Find<PlayerEntity>();
            using (StartupTrace.Measure("runtime.player-observer"))
                if (player != null) level.Own(Gameplay.NativeGameplayObserver.Install(player));
            ScheduleStart();
            }
            catch (Exception failure)
            {
                Gameplay.RunModifiers.Unload();
                try { Stop(); }
                catch (Exception cleanup) { throw new AggregateException("Runtime startup and cleanup failed", failure, cleanup); }
                throw;
            }
        }
        internal static void Stop()
        {
            RuntimePage.StopCapture();
            RuntimeApi.InvalidateSimulation();
            if (pending != null && pending.IsAlive) pending.Destroy();
            pending = null;
            RuntimeApi.Kernel.Deactivate();
            if (level != null) { level.Dispose(); level = null; }
            attemptPrepared = false;
            PackageHost.ReleasePreparation(false);
        }
        internal static void PrepareAttempt()
        {
            RuntimeApi.Kernel.CheckThread();
            // also cover loaders that directly replace root without SetLevel
            if (JumpKing.Game1.instance != null && JumpKing.Game1.instance.contentManager != null) {
                string root = JumpKing.Game1.instance.contentManager.root;
                if (!string.Equals(preparedWorldRoot, System.IO.Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                    BeforeWorld(root, true);
            }
            Stop();
            StartupTrace.BeginAttempt();
            Initialize();
            using (StartupTrace.Measure("runtime.prepare-attempt"))
            {
                using (StartupTrace.Measure("runtime.prepare.item-read-cache")) Simulation.NativeItemReadCache.Ensure();
                using (StartupTrace.Measure("runtime.prepare.text-compatibility")) Compatibility.MoreTextOptionsCompatibility.TryInstall();
                using (StartupTrace.Measure("runtime.prepare.conveyor-compatibility")) Compatibility.ConveyorCompatibility.TryInstall();
                using (StartupTrace.Measure("runtime.prepare.mod-compatibility")) Compatibility.JumpKingManagerCompatibility.Prepare();
                NativePerformance.Prepare();
                var content = JumpKing.Game1.instance == null ? null : JumpKing.Game1.instance.contentManager;
                using (StartupTrace.Measure("runtime.prepare.map-mechanics"))
                    if (content != null) { Gameplay.MapMechanics.ValidateFactory(); Gameplay.MapMechanics.Prepare(content.root, content.level == null ? null : content.level.Info.Tags, JumpKing.Level.LevelManager.TotalScreens); }
                PackageHost.PrepareAttempt();
            }
            attemptPrepared = true;
            StartupTrace.EndPreparation();
        }
        internal static void ExitWorld()
        {
            preparedWorldRoot = null;
            try { Stop(); }
            finally { Compatibility.JumpKingManagerCompatibility.ClearWorld(); NativeCaches.Clear(); PackageHost.ReleasePreparation(true); PackageHost.ReleaseMapPackages(); MapPolicy.Clear(); Gameplay.MapMechanics.ClearWorld(); StartupTrace.Finish("world exit"); }
        }
        internal static string ExportDiagnostics()
        {
            Initialize();
            return RuntimeDiagnostics.Export(RuntimeApi.Kernel, contract);
        }
            private sealed class ActivationBarrier : Entity
        {
            protected override void Update(float delta)
            {
                // all synchronous OnLevelStart callbacks have finished before
                // EntityManager starts updating. never patch the loader/Harmony
                try
                {
                    using (StartupTrace.Measure("runtime.activate")) RuntimeApi.Kernel.Activate();
                    MapPolicy.RequireAvailable(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(RuntimeApi.Kernel.GetModules(), m => m.State == "active"), m => m.Id));
                    // assembly/patch inventory, hashes and file exports belong in requested Runtime diagnostics
                    // doing that here stalls the first playable frame on every restart
                }
                catch (Exception failure) {
                    // keep the module's actual failure reason before teardown clears it
                    // map-policy errors alone hide what went wrong
                    foreach (string error in RuntimeApi.GetErrors()) Console.WriteLine("[JK Runtime] Activation failed: " + error);
                    foreach (string error in PackageHost.Errors) Console.WriteLine("[JK Runtime] Package failed: " + error);
                    try {
                        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location), "JKRuntime.ActivationError.log"),
                            DateTime.UtcNow.ToString("o") + "\r\nRoot: " + preparedWorldRoot + "\r\n" + failure + "\r\n"
                            + string.Join("\r\n", RuntimeApi.GetErrors()) + "\r\n" + string.Join("\r\n", PackageHost.Errors));
                    } catch (Exception logging) { Console.WriteLine("[JK Runtime] Cannot save activation error: " + logging.Message); }
                    RuntimeHost.Stop(); throw;
                }
                finally { pending = null; if (IsAlive) Destroy(); }
            }
        }
    }
}
