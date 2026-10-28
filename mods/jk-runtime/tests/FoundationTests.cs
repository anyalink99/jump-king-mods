using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.State;
using JKRuntime.Settings;
using JumpKing.API;
using JumpKing.Player;
using JumpKing.BodyCompBehaviours;
using System.Reflection;
using System.Runtime.Serialization;
using System.IO;

namespace JKRuntime
{
    internal static class FoundationTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected failure"); }
        private sealed class Participant : IStateParticipant
        {
            public string Id { get; set; }
            public int Version { get; set; }
            public Participant() { Version = 1; }
            public int Value;
            public bool FailTarget;
            public object Capture() { return Value; }
            public void Validate(object value) { if (!(value is int)) throw new Exception("Invalid snapshot"); }
            public void Restore(object value) { Value = (int)value; if (FailTarget && Value == 1) throw new Exception("Partial restore"); }
        }
        private sealed class Behaviour : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private static void DataMigrationTests()
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migration-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string old = Path.Combine(directory, "UIApiPlus.Settings.xml");
            const string xml = "<UIApiSettings><UseCompactModGrid>false</UseCompactModGrid><InteractBindings><int>69</int><int>70</int></InteractBindings><PinnedSettings><Setting>ui-api-plus.test</Setting></PinnedSettings></UIApiSettings>";
            File.WriteAllText(old, xml);
            Check(DataMigration.MigrateUi(directory), "First launch migrates old UI settings");
            string current = Path.Combine(directory, "JKRuntime.Settings.xml");
            var settings = AtomicXmlFile.Load<UI.UIApiSettings>(current);
            Check(!settings.UseCompactWorkshopGrids && settings.InteractChords.Length == 2 && settings.InteractChords[1][0] == 70, "Old grid and bindings converted to current schema");
            Check(settings.PinnedSettings[0] == "jk.runtime.test", "Runtime binding/pin namespace migrated");
            Check(File.ReadAllText(old) == xml && Directory.GetFiles(directory, "*.bak").Length == 1, "Original and migration backup preserved");
            string first = File.ReadAllText(current);
            File.WriteAllText(old, "corrupt");
            Check(!DataMigration.MigrateUi(directory) && File.ReadAllText(current) == first, "Migration never overwrites existing runtime preferences");
            string invalid = Path.Combine(directory, "invalid"); Directory.CreateDirectory(invalid);
            File.WriteAllText(Path.Combine(invalid, "UIApiPlus.Settings.xml"), "corrupt");
            Throws(delegate { DataMigration.MigrateUi(invalid); });
            Check(!File.Exists(Path.Combine(invalid, "JKRuntime.Settings.xml")), "Bad migration cannot write replacement defaults");
        }
        private static void PhasePermutations()
        {
            foreach (int[] order in new[] { new[] {0,1,2}, new[] {0,2,1}, new[] {1,0,2}, new[] {1,2,0}, new[] {2,0,1}, new[] {2,1,0} })
            {
                var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
                var anchor = new Behaviour();
                var list = new LinkedList<IBodyCompBehaviour>(); list.AddLast(anchor);
                typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, list);
                var behaviours = new[] { new Behaviour(), new Behaviour(), new Behaviour() };
                var registries = new[] { new Gameplay.BodyPipeline(body, false, 0, "form"), new Gameplay.BodyPipeline(body, false, 100, "thrust"), new Gameplay.BodyPipeline(body, false, 200, "movement") };
                foreach (int i in order) registries[i].RegisterBefore(behaviours[i], anchor);
                Check(list.SequenceEqual(new IBodyCompBehaviour[] { behaviours[0], behaviours[1], behaviours[2], anchor }), "Body phase order does not depend on mod install order");
                foreach (var registry in registries) registry.Dispose();
                Check(list.Count == 1 && list.First.Value == anchor, "Body phase leases restored native chain");
            }
        }
        public static int Main()
        {
            try
            {
                PhasePermutations();
                DataMigrationTests();
                var log = new List<int>();
                var scope = new RuntimeScope();
                scope.Defer(delegate { log.Add(1); });
                scope.Child().Defer(delegate { log.Add(2); });
                scope.Dispose(); scope.Dispose();
                Check(log.SequenceEqual(new[] { 2, 1 }), "Nested reverse disposal");
                Throws(delegate { scope.Child(); });

                var state = new SnapshotService();
                var a = new Participant { Id = "a", Value = 1 };
                var b = new Participant { Id = "b", Value = 1 };
                using (state.Register(a)) using (state.Register(b))
                {
                    var snapshot = state.Capture(); a.Value = b.Value = 2; b.FailTarget = true;
                    Throws(delegate { state.Restore(snapshot); });
                    Check(a.Value == 2 && b.Value == 2, "Partial participant and prior participant both rolled back");
                    Check(state.RestoreEpoch == 1, "Attempted rollback invalidates monotonic input associations");
                    b.FailTarget = false; state.Restore(snapshot);
                    Check(a.Value == 1 && b.Value == 1, "Successful atomic restore");
                    b.Version = 2; Throws(delegate { state.Restore(snapshot); }); b.Version = 1;
                    using (state.Register(new Participant { Id = "c" })) Throws(delegate { state.Restore(snapshot); });
                    Throws(delegate { state.Restore(snapshot); });
                }

                int settingValue = 1, applies = 0;
                int persisted = 1;
                var diskFailure = new Setting<int>("test.disk", "Disk", delegate { return persisted; }, delegate(int v) { persisted = v; if (v == 9) throw new IOException("Disk failure"); });
                Throws(delegate { diskFailure.Set(9); });
                Check(persisted == 1, "Persistence failure restores in-memory value too");
                var setting = new Setting<int>("test.value", "Value", delegate { return settingValue; }, delegate(int v) { settingValue = v; },
                    delegate { applies++; if (settingValue == 3) throw new Exception("Cannot apply"); }, delegate(int v) { return v >= 0; });
                Throws(delegate { setting.Set(-1); }); Check(applies == 0, "Validate before persist/apply");
                setting.Set(2); Check(settingValue == 2 && applies == 1, "Single command path");
                Throws(delegate { setting.Set(3); }); Check(settingValue == 2 && applies == 3, "Setting and controller rolled back together");
                new EntityComponent.EntityManager();
                Commands.Start();
                setting.Set(4); setting.Set(5);
                Check(settingValue == 5 && applies == 3, "Pause settings persist immediately; no callback mutation");
                Commands.Drain();
                Check(applies == 4, "Queued setting changes coalesce");
                setting.Set(6); Commands.Stop();
                Check(settingValue == 6 && applies == 4, "Teardown cancels application but preserves saved preference");
                Commands.Start(); setting.Set(3); Commands.Drain();
                Check(settingValue == 6, "Queued failure restores previous value");
                Throws(delegate { setting.Set(9); });
                Check(settingValue == 6, "Rejected command must not persist an unapplied value");
                Commands.Stop();

                var kernel = new RuntimeKernel(); bool independent = false, consumer = false;
                kernel.Register(new ModuleDefinition("bad", new Version(1, 0), delegate { throw new Exception("No device"); }, provides: new[] { new CapabilityDefinition("device", 1, 0) }));
                kernel.Register(new ModuleDefinition("consumer", new Version(1, 0), delegate { consumer = true; }, new[] { new CapabilityRequirement("device", 1, 0) }));
                kernel.Register(new ModuleDefinition("independent", new Version(1, 0), delegate { independent = true; }));
                Check(!kernel.Activate() && independent && !consumer && kernel.State == "degraded", "Failure isolation follows capability dependencies");
                kernel.Deactivate();
                Console.WriteLine("[OK] Runtime scopes, typed settings rollback, snapshot transactions/generation, dependency failure isolation");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
