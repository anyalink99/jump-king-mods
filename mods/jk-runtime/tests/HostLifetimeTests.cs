using System;
using System.Collections.Generic;
using System.Reflection;

namespace JKRuntime
{
    internal static class HostLifetimeTests
    {
        private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static void Set(Type type, string field, object value) { type.GetField(field, Flags).SetValue(null, value); }
        private static void Fails(Action action, string reason)
        { try { action(); } catch (InvalidOperationException) { return; } throw new Exception(reason); }
        private static RuntimeScope Scope(Action release)
        { var scope = new RuntimeScope(); scope.Defer(release); return scope; }
        private static void Main()
        {
            new EntityComponent.EntityManager();
            RuntimeHost.Initialize();
            var releases = new List<string>();
            int attemptCalls = 0, inspectorCalls = 0;
            Set(typeof(RuntimeHost), "level", Scope(delegate {
                releases.Add("attempt"); if (++attemptCalls == 1) throw new Exception("attempt release failed");
            }));
            Set(typeof(Inspection.InspectorHost), "active", Scope(delegate {
                releases.Add("inspector.active"); if (++inspectorCalls == 1) throw new Exception("inspector release failed");
            }));
            Set(typeof(Inspection.InspectorHost), "attempt", Scope(delegate { releases.Add("inspector.prepared"); }));
            Set(typeof(RuntimeHost), "worldBoundaries", Scope(delegate { releases.Add("world"); }));
            try { RuntimeHost.ExitWorld(); throw new Exception("Expected failed exit"); }
            catch (AggregateException error) {
                string text = error.ToString();
                Check(text.Contains("attempt release failed") && text.Contains("inspector release failed"), "Exit lost a cleanup failure");
            }
            Check(releases.Contains("world") && releases.Contains("inspector.prepared"), "Failed owner skipped independent releases");
            Fails(RuntimeHost.ScheduleStart, "Incomplete cleanup allowed activation");
            RuntimeHost.ExitWorld(); RuntimeHost.ExitWorld();
            Check(attemptCalls == 2 && inspectorCalls == 2 && releases.FindAll(v => v == "world").Count == 1,
                "Retry lost a failed owner or repeated a successful release");
            RuntimeHost.ScheduleStart(); RuntimeHost.Stop();
            Check(RuntimeApi.State == "idle" && EntityComponent.EntityManager.instance.Entities.Count == 0,
                "Recovered exit retained a pending activation");
            int worldCalls = 0;
            Set(typeof(RuntimeHost), "worldBoundaries", Scope(delegate {
                if (++worldCalls == 1) throw new Exception("world release failed");
            }));
            try { RuntimeHost.ExitWorld(); throw new Exception("Expected world failure"); } catch (AggregateException) { }
            RuntimeHost.Stop();
            Fails(RuntimeHost.ScheduleStart, "Attempt stop cleared a failed world cleanup");
            RuntimeHost.ExitWorld(); RuntimeHost.ScheduleStart(); RuntimeHost.Stop();
            Check(worldCalls == 2, "World cleanup wasn't retryable");
            MapOwners();
            Console.WriteLine("[OK] Host cleanup attempts independent owners, retains failures and blocks incomplete activation");
        }
        private static void MapOwners()
        {
            var packages = (System.Collections.IDictionary)typeof(PackageHost).GetField("packages", Flags).GetValue(null);
            var packageType = typeof(PackageHost).GetNestedType("Package", BindingFlags.NonPublic);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            bool fail = true;
            int released = 0;
            foreach (string id in new[] { "test.failed-world", "test.clean-world" }) {
                var preparation = new PreparationLifetime(id, scope => scope.Defer(delegate {
                    if (id == "test.failed-world" && fail) throw new Exception("map world release failed");
                    released++;
                }), null);
                preparation.Prepare();
                object package = Activator.CreateInstance(packageType, true);
                packageType.GetField("Id", fields).SetValue(package, id);
                packageType.GetField("MapOwned", fields).SetValue(package, true);
                packageType.GetField("Preparation", fields).SetValue(package, preparation);
                packageType.GetField("Registration", fields).SetValue(package, new ActionLease(delegate { }));
                packages.Add(id, package);
            }
            try { PackageHost.ReleaseMapPackages(); throw new Exception("Expected map cleanup failure"); }
            catch (AggregateException) { }
            Check(packages.Contains("test.failed-world") && !packages.Contains("test.clean-world") && released == 1,
                "Map cleanup lost a failed preparation or skipped the next owner");
            fail = false; PackageHost.ReleaseMapPackages();
            Check(!packages.Contains("test.failed-world") && released == 2, "Map preparation couldn't retry its retained release");
        }
    }
}
