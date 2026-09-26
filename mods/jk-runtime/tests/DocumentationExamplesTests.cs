using System;
using System.Reflection;
using JKRuntime.State;
using RuntimeExamples;

namespace JKRuntime
{
    internal static class DocumentationExamplesTests
    {
        private static void Check(bool value, string message)
        { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        private static object Prepared(string field)
        { return typeof(PreparationExample).GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); }
        private static void Main()
        {
            var lifetime = new PreparationLifetime("example.preparation",
                PreparationExample.PrepareWorld, PreparationExample.PrepareAttempt);
            lifetime.Prepare(); lifetime.ThrowIfFailed();
            object firstWorld = Prepared("world");
            var firstAttempt = (int[])Prepared("attempt");
            firstAttempt[0] = 99;
            var kernel = new RuntimeKernel();
            kernel.Register(new ModuleDefinition("example.preparation", new Version(1, 0), PreparationExample.Start));
            Check(kernel.Activate(), "Prepared example activates through module ownership");
            kernel.Deactivate(); lifetime.ReleaseAttempt();
            Check(Prepared("attempt") == null, "Attempt cleanup clears retained data");
            lifetime.Prepare(); lifetime.ThrowIfFailed();
            Check(ReferenceEquals(Prepared("world"), firstWorld), "Example reuses world resources on restart");
            Check(((int[])Prepared("attempt"))[0] == 0 && !ReferenceEquals(Prepared("attempt"), firstAttempt),
                "Restart creates isolated attempt data");
            // Cancellation before activation still releases preparation resources.
            lifetime.ReleaseWorld();
            Check(Prepared("world") == null && Prepared("attempt") == null, "Cancelled example releases both lifetimes");

            var state = new ExampleState();
            object payload = state.Capture();
            state.SetCharge(0, 8); state.Restore(payload);
            Check(state.ReadCharge(0) == 1, "Capture is independent of later live mutation");
            state.SetCharge(0, 9); state.Restore(payload);
            Check(state.ReadCharge(0) == 1, "Restoring does not alias mutable snapshot storage");
            try { state.Restore(new object()); throw new Exception("Invalid payload accepted"); }
            catch (ArgumentException) { }
            Check(state.ReadCharge(0) == 1, "Invalid payload is rejected before mutation");
            var snapshots = new SnapshotService();
            var registration = snapshots.Register(state);
            var snapshot = snapshots.Capture();
            state.SetCharge(0, 6); snapshots.Restore(snapshot);
            Check(state.ReadCharge(0) == 1 && snapshots.RestoreEpoch == 1, "Example participates in Runtime restoration");
            registration.Dispose();
            Check(!snapshots.IsCurrent(snapshot), "Unloading participant invalidates its old snapshots");
            Console.WriteLine("[OK] Compiled handbook preparation/state examples");
        }
    }
}
