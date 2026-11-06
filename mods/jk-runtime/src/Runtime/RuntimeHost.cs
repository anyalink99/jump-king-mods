using System;
using System.Collections.Generic;
using System.Linq;
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
        private static RuntimeScope worldBoundaries;
        private static bool attemptPrepared;
        private static string preparedWorldRoot;
        private static bool attemptCleanupFailed, worldCleanupFailed, releasing;
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
            ExitWorld(); Initialize(); PreparationHooks.Install();
            Gameplay.ModifierRegistrationObserver.TryInstall();
            Compatibility.MovingPlatformCompatibility.Apply();
            MapPolicy.Load(root);
            PackageHost.DiscoverMap(root);
            PackageHost.BeforeLevel();
            Inspection.InspectorHost.Install();
            if (decoded != null) Gameplay.MapMechanics.RestoreDecoded(root, decoded);
            Gameplay.MapMechanics.ValidateFactory();
            preparedWorldRoot = System.IO.Path.GetFullPath(root);
            // don't clear registrations here, another mod may already have registered
            // for this level in its BeforeLevelLoad callback
        }
        internal static void ScheduleStart()
        {
            Initialize();
            CheckCleanup();
            if (pending != null && pending.IsAlive || RuntimeApi.State != "idle") return;
            pending = new ActivationBarrier();
            RuntimeJournal.Record("jk-runtime", "activation-scheduled", "waiting for native entity update");
        }
        internal static void RecoverStartBeforePlayer()
        {
            // native entity clearing can kill the barrier without ending the attempt
            // this hook already runs after every synchronous OnLevelStart callback
            if (pending == null || pending.IsAlive) return;
            RuntimeJournal.Record("jk-runtime", "activation-recovered", "native entity clearing removed the barrier");
            pending.RunActivation();
        }
        internal static void StartLevel()
        {
            if (!attemptPrepared) PrepareAttempt();
            CheckCleanup();
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
            if (player != null) level.Own(Gameplay.DebugJumpPrompt.Install(player));
            using(StartupTrace.Measure("runtime.world-boundaries"))
                World.WorldObservation.TryInstall(level);
            UI.BindingConversionContext.FinalizeOwnHooks();
            ScheduleStart();
            }
            catch (Exception failure)
            {
                RecordActivationFailure(failure);
                try { UnloadLevel(); }
                catch (Exception cleanup) { throw new AggregateException("Runtime startup and cleanup failed", failure, cleanup); }
                throw;
            }
        }
        internal static void Stop()
        {
            RuntimeApi.Kernel.CheckThread();
            if (releasing) throw new InvalidOperationException("Reentrant Runtime teardown");
            releasing = true;
            try { StopAttempt(); }
            finally { releasing = false; }
        }
        private static void StopAttempt()
        {
            attemptPrepared = false;
            var errors = new List<Exception>();
            try
            {
                RuntimeJournal.Record("jk-runtime", "attempt-stop", "state=" + RuntimeApi.State);
                Release(errors, Inspection.InspectorHost.Stop);
                Release(errors, RuntimePage.StopCapture);
                Release(errors, RuntimeApi.InvalidateSimulation);
                Release(errors, delegate { if (pending != null) pending.Cancel(); pending = null; });
                Release(errors, World.WorldControl.EndAttempt);
                Release(errors, RuntimeApi.Kernel.Deactivate);
                Release(errors, delegate { if (level != null) { level.Dispose(); level = null; } });
                Release(errors, delegate { PackageHost.ReleasePreparation(false); });
                if (RuntimeApi.State == "cleanup-failed")
                    errors.Add(new InvalidOperationException("Module cleanup failed; restart required. " + string.Join("; ", RuntimeApi.GetErrors())));
            }
            finally { attemptCleanupFailed = errors.Count != 0; }
            if (errors.Count != 0) throw new AggregateException("Runtime attempt cleanup incomplete", errors);
        }
        internal static void UnloadLevel()
        {
            var errors = new List<Exception>();
            Release(errors, Gameplay.RunModifiers.Unload);
            Release(errors, Stop);
            if (errors.Count != 0) throw new AggregateException("Runtime unload incomplete", errors);
        }
        internal static void EndLevel()
        {
            var errors = new List<Exception>();
            Release(errors, Gameplay.RunModifiers.Finish);
            Release(errors, PackageHost.EndLevel);
            Release(errors, Stop);
            if (errors.Count != 0) throw new AggregateException("Runtime completion incomplete", errors);
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
            CheckCleanup();
            StartupTrace.BeginAttempt();
            Initialize();
            using (StartupTrace.Measure("runtime.prepare-attempt"))
            {
                using (StartupTrace.Measure("runtime.prepare.item-read-cache")) Simulation.NativeItemReadCache.Ensure();
                using (StartupTrace.Measure("runtime.prepare.text-compatibility")) Compatibility.MoreTextOptionsCompatibility.TryInstall();
                using (StartupTrace.Measure("runtime.prepare.conveyor-compatibility")) Compatibility.ConveyorCompatibility.TryInstall();
                using (StartupTrace.Measure("runtime.prepare.mod-compatibility")) Compatibility.JumpKingManagerCompatibility.Prepare();
                using (StartupTrace.Measure("runtime.prepare.moving-platform-compatibility")) Compatibility.MovingPlatformCompatibility.Apply();
                NativePerformance.Prepare();
                using (StartupTrace.Measure("runtime.prepare.run-modifiers")) Gameplay.RunModifiers.Prepare();
                var content = JumpKing.Game1.instance == null ? null : JumpKing.Game1.instance.contentManager;
                using (StartupTrace.Measure("runtime.prepare.map-mechanics"))
                    if (content != null) { Gameplay.MapMechanics.ValidateFactory(); Gameplay.MapMechanics.Prepare(content.root, content.level == null ? null : content.level.Info.Tags, JumpKing.Level.LevelManager.TotalScreens); }
                PackageHost.PrepareAttempt();
                Inspection.InspectorHost.PrepareAttempt();
                // these hooks stay dormant until WorldControl has an active owner
                if(worldBoundaries==null) {
                    worldBoundaries=new RuntimeScope();
                    using(StartupTrace.Measure("runtime.prepare.world-boundaries")) World.WorldObservation.TryInstall(worldBoundaries);
                }
                using (StartupTrace.Measure("runtime.prepare.binding-context")) UI.BindingConversionContext.Prepare();
            }
            attemptPrepared = true;
            RuntimeJournal.Record("jk-runtime", "attempt-prepared", preparedWorldRoot);
            StartupTrace.EndPreparation();
        }
        internal static void ExitWorld()
        {
            RuntimeApi.Kernel.CheckThread();
            if (releasing) throw new InvalidOperationException("Reentrant Runtime teardown");
            releasing = true;
            preparedWorldRoot = null;
            var errors = new List<Exception>();
            try
            {
                Release(errors, Gameplay.RunModifiers.Unload);
                Release(errors, StopAttempt);
                Release(errors, Geometry.MapTopology.ClearWorld);
                Release(errors, UI.BindingConversionContext.Clear);
                Release(errors, Inspection.InspectorHost.ClearWorld);
                Release(errors, delegate { if (worldBoundaries != null) { worldBoundaries.Dispose(); worldBoundaries = null; } });
                Release(errors, Compatibility.JumpKingManagerCompatibility.ClearWorld);
                Release(errors, NativeCaches.Clear);
                Release(errors, delegate { PackageHost.ReleasePreparation(true); });
                Release(errors, PackageHost.ReleaseMapPackages);
                Release(errors, MapPolicy.Clear);
                Release(errors, Gameplay.MapMechanics.ClearWorld);
                Release(errors, delegate { StartupTrace.Finish("world exit"); });
            }
            finally { releasing = false; worldCleanupFailed = errors.Count != 0; }
            if (errors.Count != 0) throw new AggregateException("Runtime world cleanup incomplete", errors);
        }
        private static void Release(List<Exception> errors, Action release)
        { try { release(); } catch (Exception error) { errors.Add(error); } }
        private static void CheckCleanup()
        {
            if (releasing || attemptCleanupFailed || worldCleanupFailed)
                throw new InvalidOperationException("Runtime cleanup incomplete; finish teardown before activation");
        }
        internal static string ExportDiagnostics()
        {
            Initialize();
            return RuntimeDiagnostics.Export(RuntimeApi.Kernel, contract);
        }
        private static void RecordActivationFailure(Exception failure)
        {
            RuntimeJournal.Record("jk-runtime", "activation-failed", failure.ToString());
            foreach (string error in RuntimeApi.GetErrors()) Console.WriteLine("[JK Runtime] Activation failed: " + error);
            foreach (string error in PackageHost.Errors) Console.WriteLine("[JK Runtime] Package failed: " + error);
            try {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location), "JKRuntime.ActivationError.log"),
                    DateTime.UtcNow.ToString("o") + "\r\nRoot: " + preparedWorldRoot + "\r\n" + failure + "\r\n"
                    + string.Join("\r\n", RuntimeApi.GetErrors()) + "\r\n" + string.Join("\r\n", PackageHost.Errors));
            } catch (Exception logging) { Console.WriteLine("[JK Runtime] Cannot save activation error: " + logging.Message); }
        }
        private sealed class ActivationBarrier : Entity
        {
            private bool cancelled;
            internal void Cancel() { cancelled = true; if (IsAlive) Destroy(); }
            protected override void Update(float delta) { RunActivation(); }
            internal void RunActivation()
            {
                if (cancelled || !ReferenceEquals(pending, this)) return;
                CheckCleanup();
                // all synchronous OnLevelStart callbacks have finished before
                // EntityManager starts updating. don't move activation into loader hooks
                try
                {
                    bool activated;
                    using (StartupTrace.Measure("runtime.activate")) activated = RuntimeApi.Kernel.Activate();
                    // rejected modules return false rather than throwing
                    if (!activated) RecordActivationFailure(new InvalidOperationException("Module activation returned " + RuntimeApi.State));
                    RuntimeJournal.Record("jk-runtime", "activation-finished", "state=" + RuntimeApi.State + "; modules=" + string.Join(",", RuntimeApi.GetOrder()));
                    Inspection.InspectorHost.Activate();
                    UI.BindingConversionContext.FinalizeOwnHooks();
                    MapPolicy.RequireAvailable(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(RuntimeApi.Kernel.GetModules(), m => m.State == "active"), m => m.Id));
                    // assembly/patch inventory, hashes and file exports belong in requested Runtime diagnostics
                    // doing that here stalls the first playable frame on every restart
                }
                catch (Exception failure) {
                    // keep the module's actual failure reason before teardown clears it
                    // map-policy errors alone hide what went wrong
                    RecordActivationFailure(failure);
                    try { RuntimeHost.Stop(); }
                    catch (Exception cleanup) { throw new AggregateException("Runtime activation and cleanup failed", failure, cleanup); }
                    throw;
                }
                finally { pending = null; if (IsAlive) Destroy(); }
            }
        }
    }
}
