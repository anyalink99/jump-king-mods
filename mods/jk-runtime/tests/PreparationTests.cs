using System;
using System.Collections.Generic;
using System.Linq;

namespace JKRuntime
{
    internal static class PreparationTests
    {
        private static void Check(bool value, string message)
        { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        private static void Main()
        {
            var events = new List<string>();
            int worlds = 0, attempts = 0;
            var lifetime = new PreparationLifetime("fixture", delegate(RuntimeScope scope) {
                worlds++; events.Add("world"); scope.Defer(delegate { events.Add("world-release"); });
            }, delegate(RuntimeScope scope) {
                attempts++; events.Add("attempt"); scope.Defer(delegate { events.Add("attempt-release"); });
            });
            for (int i = 0; i < 3; i++) { lifetime.Prepare(); lifetime.ThrowIfFailed(); lifetime.ReleaseAttempt(); }
            Check(worlds == 1 && attempts == 3, "World preparation is reused across same-world attempts");
            lifetime.Prepare(); lifetime.ReleaseWorld(); lifetime.ReleaseWorld();
            Check(events[events.Count - 2] == "attempt-release" && events.Last() == "world-release",
                "Cancelled intro releases attempt before world, once");
            lifetime.Prepare(); Check(worlds == 2, "Another loaded world is prepared again"); lifetime.ReleaseWorld();
            lifetime.EnsurePrepared(); lifetime.EnsurePrepared();
            Check(worlds == 3 && attempts == 6, "Late activation fallback prepares once without repeating successful work");
            lifetime.ReleaseWorld();

            bool fail = true; int released = 0;
            var broken = new PreparationLifetime("broken", null, delegate(RuntimeScope scope) {
                scope.Defer(delegate { released++; }); if (fail) throw new Exception("fixture");
            });
            broken.Prepare(); Check(broken.Error != null && released == 1, "Partial preparation rolls back immediately");
            try { broken.ThrowIfFailed(); throw new Exception("Failure was ignored"); } catch (InvalidOperationException) { }
            fail = false; broken.Prepare(); broken.ThrowIfFailed(); broken.ReleaseWorld();
            Check(released == 2, "A preparation failure is retryable on the next attempt");

            int acquisitions = 0; bool releaseFails = true;
            var rollback = new PreparationLifetime("rollback", delegate(RuntimeScope scope) {
                acquisitions++; scope.Defer(delegate { if (releaseFails) throw new Exception("release"); });
                throw new Exception("acquire");
            }, null);
            rollback.Prepare(); rollback.Prepare();
            Check(acquisitions == 1 && rollback.Error != null, "Failed cleanup blocks replacement resources");
            releaseFails = false; rollback.Prepare();
            Check(acquisitions == 2, "Successful cleanup retry permits another preparation"); rollback.ReleaseWorld();

            var kernel = new RuntimeKernel();
            kernel.Register(new ModuleDefinition("consumer", new Version(1, 0), delegate { },
                requires: new[] { new CapabilityRequirement("fixture.service", 1, 0) }));
            kernel.Register(new ModuleDefinition("provider", new Version(1, 0), delegate(ModuleContext c) { c.Publish("fixture.service", new object()); },
                provides: new[] { new CapabilityDefinition("fixture.service", 1, 0) }));
            kernel.Prepare(delegate(string[] order) {
                Check(order.SequenceEqual(new[] { "provider", "consumer" }), "Preparation uses capability dependency order");
                Check(kernel.State == "idle", "Preparation does not activate gameplay");
                try { kernel.Register(new ModuleDefinition("late", new Version(1, 0), delegate { })); throw new Exception("Reentrant registration allowed"); }
                catch (InvalidOperationException) { }
            });
            object service;
            Check(!kernel.TryGetCapability("fixture.service", 1, 0, out service), "Prepared resources do not publish services");
            Check(kernel.Activate(), "Normal activation works after preparation"); kernel.Deactivate();
            kernel.Prepare(delegate { }); Check(kernel.Activate(), "Restart preserves the existing activation protocol"); kernel.Deactivate();
            Check(RuntimeApi.Supports("module-preparation-v1"), "Preparation is discoverable through Supports");
            Console.WriteLine("[OK] Preparation lifetimes, rollback, cancellation and dependency ordering");
        }
    }
}
