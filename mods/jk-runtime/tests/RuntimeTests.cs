using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using EntityComponent;
using JumpKing.Mods;
using JumpKing.Player;

namespace JKRuntime
{
    internal static class RuntimeTests
    {
        private static void Main()
        {
            try
            {
                GraphPermutations(); Rejections(); Transactions(); ContextLifetime();
                NativeContract(); DeferredLifecycle(); StartupTracing(); Diagnostics();
                Console.WriteLine("[OK] JK Runtime: graph permutations, capabilities/conflicts, rollback, reload, native contract, deferred lifecycle, diagnostics");
            }
            catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
        }
        private static ModuleDefinition Define(string id, Action<ModuleContext> install = null,
            IEnumerable<CapabilityRequirement> requires = null, IEnumerable<CapabilityDefinition> provides = null,
            string[] after = null, string[] claims = null, Action<ModuleContext> start = null, Action<ModuleContext> stop = null)
        {
            return new ModuleDefinition(id, new Version(1, 0), install ?? delegate { }, requires, provides,
                after: after, exclusiveResources: claims, start: start, stop: stop);
        }
        private static void GraphPermutations()
        {
            foreach (int[] permutation in new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } })
            {
                var trace = new List<string>();
                var host = new RuntimeKernel();
                object service = new object();
                ModuleDefinition[] definitions = {
                    Define("a.consumer", delegate(ModuleContext c) {
                        Check(ReferenceEquals(c.Require<object>("test.service"), service), "versioned service identity");
                        trace.Add("consumer"); }, new[] { new CapabilityRequirement("test.service", 1, 2) }),
                    Define("z.provider", delegate(ModuleContext c) { c.Publish("test.service", service); trace.Add("provider"); },
                        provides: new[] { new CapabilityDefinition("test.service", 1, 3) }),
                    Define("b.independent", delegate { trace.Add("independent"); })
                };
                foreach (int index in permutation) host.Register(definitions[index]);
                Check(host.Activate(), "permutation activation");
                Check(string.Join(",", host.Order) == "b.independent,z.provider,a.consumer", "stable topological order");
                Check(string.Join(",", trace) == "independent,provider,consumer", "actual install order");
                host.Deactivate();
            }
            var optional = new RuntimeKernel();
            optional.Register(Define("optional", delegate(ModuleContext c) { object unused; Check(!c.TryGetCapability("missing", out unused), "optional lookup"); },
                new[] { new CapabilityRequirement("missing", 1, 0, true) }));
            Check(optional.Activate() && optional.Errors.Length == 1, "optional absent reported without rejection");
            optional.Deactivate();
        }
        private static void Rejections()
        {
            int mutations = 0;
            Action<ModuleContext> mutate = delegate { mutations++; };
            var cases = new[] {
                new[] { Define("missing", mutate, new[] { new CapabilityRequirement("absent", 1, 0) }) },
                new[] { Define("a", mutate, after: new[] { "b" }), Define("b", mutate, after: new[] { "a" }) },
                new[] { Define("a", mutate, claims: new[] { "jump" }), Define("b", mutate, claims: new[] { "jump" }) },
                new[] { Define("a", mutate, provides: new[] { new CapabilityDefinition("same", 1, 0) }), Define("b", mutate, provides: new[] { new CapabilityDefinition("same", 1, 0) }) },
                new[] { Define("a", mutate, after: new[] { "missing" }) },
                new[] { Define("a", mutate, new[] { new CapabilityRequirement("api", 2, 0) }), Define("b", mutate, provides: new[] { new CapabilityDefinition("api", 1, 9) }) },
                new[] { Define("a", mutate, new[] { new CapabilityRequirement("api", 1, 9) }), Define("b", mutate, provides: new[] { new CapabilityDefinition("api", 1, 8) }) }
            };
            foreach (ModuleDefinition[] definitions in cases)
            {
                var host = new RuntimeKernel();
                foreach (var definition in definitions) host.Register(definition);
                Check(!host.Activate() && host.State == "rejected" && host.Errors.Length > 0, "clear graph rejection");
                // Valid independent providers may initialize; rejected nodes never do.
                Check(host.GetModules().Where(s => s.State == "active").All(s => s.Id == "b"), "only independent valid providers survive graph errors");
                host.Deactivate();
            }
            var duplicate = new RuntimeKernel();
            duplicate.Register(Define("same"));
            Throws(delegate { duplicate.Register(Define("same")); }, "duplicate module rejected");
        }
        private static void Transactions()
        {
            for (int failing = 0; failing < 3; failing++)
            {
                var host = new RuntimeKernel(); var trace = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    int n = i; int failure = failing;
                    host.Register(Define("module." + n, delegate(ModuleContext context)
                    {
                        trace.Add("add" + n);
                        context.Track(new ActionLease(delegate { trace.Add("undo" + n); }));
                        if (n == failure) throw new Exception("injected install failure");
                    }));
                }
                Check(!host.Activate(), "install failure rejected");
                var expected = new List<string>();
                for (int i = 0; i < 3; i++) { expected.Add("add" + i); if (i == failing) expected.Add("undo" + i); }
                Check(trace.SequenceEqual(expected) && host.State == "degraded", "failed module unwound; unrelated modules survive");
                for (int i = 2; i >= 0; i--) if (i != failing) expected.Add("undo" + i);
                host.Deactivate(); Check(trace.SequenceEqual(expected), "remaining modules released in reverse without double undo");
            }
            var start = new RuntimeKernel(); var log = new List<string>();
            start.Register(Define("a", delegate(ModuleContext c) { c.Track(new ActionLease(delegate { log.Add("undo-a"); })); },
                start: delegate { log.Add("start-a"); }, stop: delegate { log.Add("stop-a"); }));
            start.Register(Define("b", delegate(ModuleContext c) { c.Track(new ActionLease(delegate { log.Add("undo-b"); })); },
                start: delegate { log.Add("start-b"); throw new Exception("partial Start"); },
                stop: delegate { log.Add("stop-b"); throw new Exception("Stop failure"); }));
            Check(!start.Activate(), "failed Start rolls back");
            Check(string.Join(",", log) == "start-a,start-b,stop-b,undo-b,stop-a,undo-a", "stop failure does not interrupt reverse cleanup");
            Check(start.Errors.Any(e => e.Contains("Stop failure")), "cleanup error attributed");
            start.Deactivate();
            Check(start.State == "cleanup-failed", "cleanup failure is not advertised as restored");
            Throws(delegate { start.Activate(); }, "failed cleanup prevents unsafe reactivation");
            var absentPublish = new RuntimeKernel();
            absentPublish.Register(Define("bad", provides: new[] { new CapabilityDefinition("api", 1, 0) }));
            Check(!absentPublish.Activate(), "declared but unpublished service rejected");
        }
        private static void ContextLifetime()
        {
            var host = new RuntimeKernel(); int installs = 0, disposals = 0; ModuleContext saved = null;
            IDisposable registration = host.Register(Define("reload", delegate(ModuleContext c) {
                saved = c; installs++; c.Track(new ActionLease(delegate { disposals++; })); }));
            for (int i = 0; i < 3; i++)
            {
                Check(host.Activate(), "reload activation");
                Throws(delegate { host.Register(Define("late")); }, "late registration does not reorder live gameplay");
                host.Deactivate(); host.Deactivate();
                Throws(delegate { saved.Track(new ActionLease(delegate { })); }, "stale context rejected");
            }
            Check(installs == 3 && disposals == 3, "one context/cleanup per level");
            registration.Dispose(); registration.Dispose(); Check(host.GetModules().Length == 0, "idempotent registration lease");
            Exception threadError = null;
            var worker = new Thread(delegate() { try { host.Register(Define("worker")); } catch (Exception e) { threadError = e; } });
            worker.Start(); worker.Join(); Check(threadError is InvalidOperationException, "game-thread ownership enforced");
        }
        private static void NativeContract()
        {
            var contract = new GameContract();
            Check(contract.Available && contract.Fingerprint.Length == 64, "installed game structural contract/fingerprint");
            Check(contract.ReadChargeTimer(new JumpState(null)) == 0f, "native timer accessor");
            var assembly = typeof(JKRuntime.UI.ModEntry).Assembly;
            var attribute = (JumpKingModAttribute)typeof(JKRuntime.UI.ModEntry).GetCustomAttributes(typeof(JumpKingModAttribute), false)[0];
            Check(attribute.ModName == "JK Runtime", "one native mod identity");
            var mod = new ModAssembly(assembly, attribute);
            Check(mod.BeforeLevelLoadMethods.Count > 0
                && mod.BeforeLevelLoadMethods.All(method => method.DeclaringType == typeof(JKRuntime.UI.ModEntry))
                && mod.OnLevelStartMethods.Count == 1 && mod.OnLevelEndMethods.Count == 1
                && mod.OnLevelUnloadMethods.Count == 1, "single lifecycle owner with independent menu startup");
            Check(!assembly.GetReferencedAssemblies().Any(a => a.Name == "0Harmony"), "no Harmony dependency");
            Check(JKRuntime.UI.UIApi.Supports("physical-binding-resolution"), "built-in UIApi+ contract retained");
        }
        private static void DeferredLifecycle()
        {
            // Use installed EntityManager, not a guessed callback scheduler.
            new EntityManager();
            var events = new List<string>();
            IDisposable early = RuntimeApi.Register(Define("early", delegate { events.Add("early"); }));
            RuntimeHost.BeforeLevelLoad(Path.Combine(Path.GetTempPath(), "runtime-policy-absent-" + Guid.NewGuid().ToString("N")));
            RuntimeHost.ScheduleStart(); RuntimeHost.ScheduleStart();
            IDisposable late = RuntimeApi.Register(Define("late", delegate { events.Add("late"); }));
            Check(events.Count == 0, "all registration callbacks precede composition");
            RuntimeDiagnostics.OutputDirectory = Path.Combine(Path.GetDirectoryName(typeof(RuntimeTests).Assembly.Location), "diagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RuntimeDiagnostics.OutputDirectory);
            var activationTime = System.Diagnostics.Stopwatch.StartNew();
            EntityManager.instance.Update(1f / 60f);
            activationTime.Stop();
            Console.WriteLine("[PERF] First activation update: " + activationTime.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            Check(string.Join(",", events) == "early,late", "before/after runtime callback participants both composed");
            Check(EntityManager.instance.Entities.Count == 0, "activation barrier destroyed after first update");
            Check(Directory.GetFiles(RuntimeDiagnostics.OutputDirectory).Length == 0, "First playable update does not export diagnostics");
            RuntimeHost.Stop(); RuntimeHost.Stop();
            RuntimeHost.BeforeLevelLoad(Path.Combine(Path.GetTempPath(), "runtime-policy-absent-" + Guid.NewGuid().ToString("N"))); RuntimeHost.ScheduleStart(); EntityManager.instance.Update(1f / 60f);
            Check(events.Count == 4, "process registrations survive level reload");
            Check(Directory.GetFiles(RuntimeDiagnostics.OutputDirectory).Length == 0, "Restart does not export diagnostics");
            RuntimeHost.Stop(); early.Dispose(); late.Dispose();
            // Unload before the first update must cancel pending activation.
            RuntimeHost.ScheduleStart(); RuntimeHost.Stop(); EntityManager.instance.Update(1f / 60f);
            Check(RuntimeApi.State == "idle" && events.Count == 4, "pending unload cancellation");
        }
        private static void Diagnostics()
        {
            var exportTime = System.Diagnostics.Stopwatch.StartNew();
            string file = RuntimeHost.ExportDiagnostics();
            exportTime.Stop();
            Console.WriteLine("[PERF] Explicit diagnostics export: " + exportTime.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            Check(File.Exists(file) && File.Exists(Path.ChangeExtension(file, "json")), "text and JSON exports");
            var parsed = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.ChangeExtension(file, "json")));
            Check(parsed != null, "JSON is parseable");
            var absent = RuntimeDiagnostics.InspectHarmony(typeof(RuntimeTests).Assembly);
            Check(absent.ContainsKey("inspectionError"), "missing Harmony ABI produces diagnostic, not crash");
        }
        private static void StartupTracing()
        {
            var snapshots = new List<StartupTrace.Snapshot>();
            StartupTrace.ConfigureForTest(snapshots.Add);
            try
            {
                StartupTrace.BeginUpdate(); StartupTrace.BeginAttempt();
                using (StartupTrace.Measure("fixture.module")) { }
                StartupTrace.EndWork("fast", typeof(RuntimeTests), 1, 2, true);
                StartupTrace.EndWork("slow", typeof(RuntimeTests), 1, System.Diagnostics.Stopwatch.Frequency, true);
                StartupTrace.EndUpdate();
                StartupTrace.EndPreparation();
                for (int i = 0; i < 600; i++) { StartupTrace.BeginDraw(); StartupTrace.EndDraw(); }
                Check(snapshots.Count == 0 && StartupTrace.BeginWork() == 0, "A long intro does not consume the gameplay trace window");
                StartupTrace.BeginGameplay();
                for (int i = 0; i < 119; i++) { StartupTrace.BeginDraw(); StartupTrace.EndDraw(); }
                Check(snapshots.Count == 0, "Trace remains in memory throughout the capture window");
                StartupTrace.BeginDraw(); StartupTrace.EndDraw();
                Check(snapshots.Count == 1 && snapshots[0].Entries.Any(e => e.Name == "fixture.module"), "Capture includes module stages and closes after bounded frames");
                var saved = snapshots[0].Entries[0].Name;
                StartupTrace.BeginAttempt();
                for (int i = 0; i < StartupTrace.Capacity + 100; i++) StartupTrace.Record("overflow", 1, 2);
                StartupTrace.Finish("fixture restart");
                Check(snapshots[1].Entries.Length <= StartupTrace.Capacity && snapshots[1].Dropped == 100, "Trace overflow is bounded and reported");
                Check(snapshots[0].Entries[0].Name == saved, "Worker snapshot does not reference reused capture storage");
                Check(StartupTrace.Format(snapshots[0]).Contains("fixture.module"), "Trace retains human-readable stages");
                Check(!snapshots[0].Entries.Any(e => e.Name == "fast") && StartupTrace.Format(snapshots[0]).Contains("JKRuntime.RuntimeTests.slow"),
                    "Only slow objects are retained, with type names resolved in the report");
            }
            finally { StartupTrace.ResetForTest(); }
            StartupTrace.BeginAttempt(); StartupTrace.Record("disabled", 1, 2); StartupTrace.Finish("disabled");
            Check(StartupTrace.BeginWork() == 0, "Disabled detail hooks do not read the clock");
            Check(snapshots.Count == 2, "Disabled tracing produces no captures");
        }
        private static void Throws(Action action, string message)
        { try { action(); } catch (InvalidOperationException) { return; } throw new Exception(message); }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
