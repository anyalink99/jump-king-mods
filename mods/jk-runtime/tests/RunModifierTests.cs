using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Gameplay;
using JKRuntime.Settings;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.Player;
using JumpKing.SaveThread.SaveComponents;
using JumpKing.MiscSystems.Achievements;

namespace JKRuntime
{
    internal static class RunModifierTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private sealed class Behaviour : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private static void Model()
        {
            var ledger = new RunModifierLedger("run1", 0, null);
            Check(ledger.Names().Length == 0, "Clean run has no attribution");
            Check(ledger.Add("sfc", "Subframe Charge") && !ledger.Add("sfc", "Subframe Charge"), "Sources deduplicated");
            ledger.Observe(1, 1, 1, true);
            ledger.Observe(0, 0, 1, false);
            Check(!ledger.Record.Unknown && ledger.Names().Single() == "Subframe Charge", "Removing marker retains exact attribution");
            var resume = new RunModifierLedger("run1", 1, ledger.Record);
            Check(resume.Names().Single() == "Subframe Charge", "Resume retains history");
            var reset = new RunModifierLedger("run2", 0, ledger.Record);
            Check(reset.Names().Length == 0, "New attempt resets history");
            var lost = new RunModifierLedger("run1", 2, ledger.Record);
            Check(lost.Record.Unknown, "Unrecorded native peak adds unknown, not invented mod name");
            Check(lost.Record.UnknownReasons.Any(r => r.StartsWith("saved-peak-increased:")), "Higher saved peak has a distinct reason");
            var old = new RunModifierLedger("old", 1, null);
            Check(old.Record.Unknown && old.Record.Sources.Count == 0, "Pre-runtime saved flag cannot identify mods");
            Check(old.Record.UnknownReasons.Single().StartsWith("initial-native-flag:"), "Initial native flag has an actionable explanation");
            var legacy = new RunModifierLedger("legacy", 1, new RunModifierRecord { RunKey = "legacy", NativePeak = 1, Unknown = true, UnknownReasons = null });
            Check(legacy.Record.UnknownReasons.Single().StartsWith("legacy-unknown:"), "Legacy unknown history stays unknown without inventing its cause");
            var mismatch = new ModifierBodyEvidence();
            ModifierRegistrationEvidence.CheckUnobserved(mismatch, 2, 3);
            ModifierRegistrationEvidence.CheckUnobserved(mismatch, 2, 3);
            Check(mismatch.UnknownReasons.Count == 2 && mismatch.UnknownReasons["external-count-mismatch"].Contains("external=2"),
                "First mismatch and native increase are retained without per-frame duplicates");
            var foreign = new RunModifierLedger("foreign", 0, null);
            foreign.Observe(0, 0, 1, false);
            Check(foreign.Record.Unknown, "Unobserved transient peak is unknown");
            var malformed = new RunModifierRecord { RunKey = "bad" }; malformed.Sources.Add(null);
            Check(new RunModifierLedger("bad", 1, malformed).Record.Unknown, "Malformed sidecar does not crash or name innocent mods");
            Check(new RunModifierLedger("other-map", 1, resume.Record).Record.Sources.Count == 0, "Map isolation");
            var snapshot = new PlayerStats { attempts = 3, session = 2, _ticks = 500, jumps = 10 };
            string key = RunModifiers.RunKey("Content", snapshot);
            Check(key == RunModifiers.RunKey("Content", snapshot), "Stable attempt snapshot key");
            snapshot.attempts++;
            Check(key != RunModifiers.RunKey("Content", snapshot), "New attempt gets new key");
            var resetEvidence = new ModifierResetEvidence { PreviousKey = "map|old", Count = 2 };
            resetEvidence.Sources.Add(new RunModifierSource { Id = "foreign", Name = "Foreign mod" });
            var carried = new RunModifierLedger("map|new", 2, null, resetEvidence);
            Check(!carried.Record.Unknown && carried.Names().Single() == "Foreign mod" && carried.Record.InheritedNativePeak == 2,
                "Witnessed reset attributes the inherited flag to its actual remaining source");
            carried.Add("foreign", "Foreign mod");
            Check(carried.Names().Length == 1, "Inherited and new registration deduplicate names");
            Check(!new RunModifierLedger("map|new", 2, carried.Record).Record.Unknown, "Inherited attribution survives resume");
            Check(new RunModifierLedger("other-map|new", 2, null, resetEvidence).Record.Unknown, "Reset metadata cannot cross maps");
            Check(new RunModifierLedger("map|old", 2, null, resetEvidence).Record.Unknown, "Reset metadata cannot overwrite the same attempt");
            Check(new RunModifierLedger("map|new", 3, null, resetEvidence).Record.Unknown, "Unexpected initial peak remains unknown");
            var beforeQuit = new RunModifierRecord { RunKey = "map|old", NativePeak = 11 };
            resetEvidence.StoreIn(beforeQuit);
            var afterLaunch = new RunModifierLedger("map|new", 2, beforeQuit, ModifierResetEvidence.FromSaved(beforeQuit));
            Check(!afterLaunch.Record.Unknown && afterLaunch.Names().Single() == "Foreign mod", "Persisted reset survives quitting before next attempt");
            beforeQuit.ResetSources.Add(null);
            Check(ModifierResetEvidence.FromSaved(beforeQuit) == null, "Malformed persisted reset cannot invent sources");
        }
        private static void Native()
        {
            RunModifiers.ValidateContract();
            var loop = new GameLoop();
            var completionField = typeof(GameLoop).GetField("m_ending_body_modifiers", BindingFlags.Instance | BindingFlags.NonPublic);
            completionField.SetValue(loop, Activator.CreateInstance(completionField.FieldType, true));
            var saveLube = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            saveLube.GetProperty("CombinedSave").SetValue(null, new JumpKing.SaveThread.CombinedSaveFile
                { full_run = new SaveCompCushion<FullRunSave> { initialized = true } }, null);
            FullRunSave.fullRunSave = new FullRunSave();
            var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
            var list = new LinkedList<IBodyCompBehaviour>();
            var anchor = new Behaviour(); list.AddLast(anchor);
            typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, list);
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "attribution-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "record.xml");
            RunModifiers.Begin(body, "native", 0, path);
            using (var allowed = new BodyPipeline(body, false))
            {
                allowed.RegisterBefore(new Behaviour(), anchor);
                RunModifiers.Observe();
                Check(FullRunSave.fullRunSave.CurrentBodyCompModifiers == 0 && RunModifiers.GetContributors().Length == 0, "Map-authorized behaviour does not flag or attribute");
            }
            Check(!RunModifiers.RegisterBefore(body, new Behaviour(), new Behaviour()), "Missing anchor fails natively");
            Check(RunModifiers.GetContributors().Length == 0, "Failed registration does not attribute");
            using (var modified = new BodyPipeline(body, true)) modified.RegisterBefore(new Behaviour(), anchor);
            Check(list.Count == 1 && FullRunSave.fullRunSave.CurrentBodyCompModifiers == 1, "Native removed marker leaves high-water flag");
            Check(RunModifiers.GetContributors().Single() == "JK Runtime", "Test assembly title attributed, not all loaded mods");
            var detached = RunModifiers.GetEvidence();
            detached.Sources[0].Name = "Changed by consumer";
            detached.Sources.Clear(); detached.UnknownReasons.Add("Changed by consumer");
            Check(RunModifiers.GetEvidence().Sources.Count == 1 && RunModifiers.GetContributors().Single() == "JK Runtime"
                && !RunModifiers.GetEvidence().UnknownReasons.Contains("Changed by consumer"), "Evidence snapshot is deeply detached");
            var marker = new Behaviour();
            RunModifiers.RegisterBefore(body, marker, anchor); RunModifiers.Remove(body, marker);
            Check(RunModifiers.WaitForWrites(5000), "Background attribution write completed");
            var saved = AtomicXmlFile.Load<RunModifierRecord>(path);
            Check(saved.Sources.Count == 1 && !saved.Unknown, "Same-tick Jetpack-style marker persisted without false unknown");
            RunModifiers.Finish();
            Check(RunModifiers.GetContributors().Single() == "JK Runtime", "End teardown retains results");
            RunModifiers.Begin(body, "native", 1, path);
            Check(RunModifiers.GetContributors().Single() == "JK Runtime", "Native resume loads sidecar");
            var untracked = new Behaviour(); body.RegisterBehaviourBefore(untracked, anchor);
            RunModifiers.Observe();
            Check(RunModifiers.GetContributors().Any(s => s.StartsWith("Unknown")), "Foreign registration detected without guessing its assembly");
            body.RemoveBehaviour(untracked);
            RunModifiers.Finish();
            FullRunSave.fullRunSave = new FullRunSave();
            completionField.SetValue(loop, Activator.CreateInstance(completionField.FieldType, true));
            RunModifiers.Begin(body, "new-attempt", 0, path);
            Check(RunModifiers.GetContributors().Length == 0, "New native attempt does not inherit prior contributor names");
            // A registered no-op never executes in this fixture, yet native policy
            // marks the run. This is exactly why attribution cannot imply feature use.
            RunModifiers.Register(body, new Behaviour());
            Check(RunModifiers.GetContributors().Single() == "JK Runtime", "Registration alone is attributed without executing a feature");
            RunModifiers.Finish();
            var resetEvidence = RunModifiers.CaptureReset();
            Check(resetEvidence != null && resetEvidence.Count == 1, "Known active native count can be captured before reset");
            var inherited = default(FullRunSave).GetDefault();
            Check(inherited.CurrentBodyCompModifiers == 1, "Installed native new-save default inherits current modifier count");
            FullRunSave.fullRunSave = inherited;
            RunModifiers.CompleteReset(resetEvidence);
            var freshBody = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
            typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(freshBody, new LinkedList<IBodyCompBehaviour>());
            completionField.SetValue(loop, Activator.CreateInstance(completionField.FieldType, true));
            RunModifiers.Begin(freshBody, "native-inherited-count", inherited.CurrentBodyCompModifiers, path);
            Check(RunModifiers.GetUnknownReasons().Length == 0 && RunModifiers.GetContributors().Single() == "JK Runtime",
                "Observed reset retains the inherited source without false Unknown");
            RunModifiers.Unload();
        }
        private static void Layout()
        {
            Check(!ModifierResultsOverlay.ShouldDisplay(new[] { "Clean finish" }), "No overlay on clean finish");
            Check(ModifierResultsOverlay.ShouldDisplay(new[] { ModifierResultsOverlay.Warning }), "Only native warning enables overlay");
            var wrapped = ModifierResultsOverlay.Wrap(new[] { "Long mod title", "Bad\nname" }, s => s.Length * 6, 36);
            Check(wrapped.All(s => s.Length <= 6 && !s.Contains('\n')), "Long/untrusted titles fit row bounds");
            for (int lines = 5; lines <= 15; lines++)
            {
                int y = ModifierResultsOverlay.StartY(lines, 16);
                int count = ModifierResultsOverlay.Capacity(y, 16);
                Check(y + (count + 1) * 16 <= (y == 20 ? 110 : 350), "Results pagination stays on screen and outside stats");
            }
            Check(typeof(JumpKing.Util.Tags.IForeground).IsAssignableFrom(typeof(ModifierResultsOverlay)), "Overlay uses native after-StatsScreen foreground pass");
        }
        private static void Persistence()
        {
            Action work = null; int queued = 0;
            var written = new List<RunModifierRecord>();
            var store = new RunModifierStore("fixture", written.Add, action => { work = action; queued++; });
            var record = new RunModifierRecord { RunKey = "first" };
            record.Sources.Add(new RunModifierSource { Id = "one", Name = "One" });
            record.UnknownReasons.Add("fixture original reason");
            record.InheritedNativePeak = 2;
            record.InheritedSources.Add(new RunModifierSource { Id = "old", Name = "Old source" });
            record.ResetNativePeak = 1;
            record.ResetSources.Add(new RunModifierSource { Id = "next", Name = "Next source" });
            store.Submit(record);
            record.Sources[0].Name = "Mutated";
            record.UnknownReasons[0] = "mutated reason";
            record.InheritedSources[0].Name = "mutated source";
            record.ResetSources[0].Name = "mutated next source";
            Check(written.Count == 0 && store.Read().Sources[0].Name == "One", "Registration only queues an immutable snapshot");
            Check(store.Read().UnknownReasons.Single() == "fixture original reason", "Unknown reasons also cross threads as an immutable copy");
            Check(store.Read().InheritedNativePeak == 2 && store.Read().InheritedSources.Single().Name == "Old source", "Inherited evidence is copied for the background writer");
            Check(store.Read().ResetSources.Single().Name == "Next source", "Save-worker reset metadata is copied too");
            for (int i = 0; i < 1000; i++) { record.NativePeak = (uint)i; store.Submit(record); }
            record.RunKey = "second"; record.Sources.Clear(); record.NativePeak = 0; store.Submit(record);
            Check(queued == 1 && !store.Wait(0) && store.Read().RunKey == "second", "One worker, coalesced writes and current attempt visible before disk catches up");
            work();
            Check(store.Wait(0) && written.Count == 1 && written[0].RunKey == "second" && written[0].Sources.Count == 0,
                "Old pending attempts cannot overwrite the latest one");
            var restored = store.Read(); restored.Sources.Add(new RunModifierSource());
            Check(store.Read().Sources.Count == 0, "Resume cannot mutate the worker snapshot");
            RunModifierStore duringWrite = null;
            duringWrite = new RunModifierStore("fixture", value => {
                written.Add(value);
                if (value.RunKey == "old") duringWrite.Submit(new RunModifierRecord { RunKey = "new" });
            }, action => work = action);
            duringWrite.Submit(new RunModifierRecord { RunKey = "old" }); work();
            Check(written[written.Count - 1].RunKey == "new" && duringWrite.Wait(0), "A submission during I/O is drained after the in-flight record");
            int saves = 0;
            var failure = new RunModifierStore("fixture", value => { if (++saves == 1) throw new IOException("fixture"); }, action => work = action);
            failure.Submit(record); work(); failure.Submit(record); work();
            Check(saves == 2 && failure.Wait(0), "Write failure does not wedge subsequent saves");
            var concurrent = new RunModifierStore("fixture", written.Add, action => work = action);
            var initial = new RunModifierRecord { RunKey = "same", NativePeak = 11 };
            concurrent.Submit(initial);
            var reset = new ModifierResetEvidence { PreviousKey = "same", Count = 2 };
            reset.Sources.Add(new RunModifierSource { Id = "remaining", Name = "Remaining mod" });
            concurrent.SubmitReset(reset);
            initial.Sources.Add(new RunModifierSource { Id = "last", Name = "Last registration" });
            concurrent.Submit(initial); // older game-thread snapshot after save-worker update
            work();
            Check(concurrent.Read().ResetNativePeak == 2 && concurrent.Read().Sources.Single().Name == "Last registration",
                "Interleaved reset and final ledger writes preserve both registration and reset evidence");
            concurrent.Submit(new RunModifierRecord { RunKey = "next" });
            concurrent.SubmitReset(reset); work();
            Check(concurrent.Read().RunKey == "next" && concurrent.Read().ResetNativePeak == 0,
                "Late save-worker reset cannot overwrite a newer attempt");
        }
        private static void Trace()
        {
            string path = Path.Combine(Path.GetTempPath(), "runtime-modifier-trace-" + Guid.NewGuid().ToString("N") + ".txt");
            var trace = new ModifierTraceLog(path, 3);
            Check(!File.Exists(path), "Trace does not write until an event arrives");
            trace.Record("first"); trace.Record("second"); trace.Record("third");
            for (int i = 0; i < 100; i++) trace.Record("overflow");
            Check(trace.Wait(5000), "Trace background writes drain");
            string[] lines = File.ReadAllLines(path);
            Check(lines.Length == 4 && lines[0].EndsWith("first") && lines[2].EndsWith("third") && lines[3].Contains("TRUNCATED"),
                "Bounded trace preserves first cause and explicitly signals omitted events");
            File.Delete(path);
        }
        public static int Main()
        {
            try { Model(); Persistence(); Trace(); Native(); Layout(); Console.WriteLine("[OK] Run attribution: native counters, allowed/failed/transient markers, resume/reset, unknown reasons, bounded trace/persistence and foreground layout"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
