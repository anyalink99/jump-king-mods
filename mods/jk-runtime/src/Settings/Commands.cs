using System;
using System.Collections.Generic;
using EntityComponent;

namespace JKRuntime.Settings
{
    public static class Commands
    {
        private sealed class Command { internal Action Run, Cancel; }
        private static readonly Queue<Command> pending = new Queue<Command>();
        private static Pump pump;
        private static bool poisoned;
        private static bool cancelling, stopping;
        public static string LastError { get; private set; }
        internal static void CheckAccepting()
        {
            RuntimeApi.Kernel.CheckThread();
            if (cancelling || stopping) throw new InvalidOperationException("Runtime commands are being cancelled");
            if (poisoned) throw new InvalidOperationException("A runtime command failed; restart before applying further changes. " + LastError);
        }
        public static void Enqueue(Action action, Action cancelled = null)
        {
            CheckAccepting();
            if (action == null) throw new ArgumentNullException("action");
            if (pump == null) { action(); return; }
            pending.Enqueue(new Command { Run = action, Cancel = cancelled });
        }
        internal static void Start()
        {
            CheckAccepting();
            if (pump == null) pump = new Pump();
        }
        internal static void Stop()
        {
            RuntimeApi.Kernel.CheckThread();
            if (stopping || cancelling) throw new InvalidOperationException("Reentrant command teardown");
            stopping = true;
            var failures = new List<Exception>();
            try
            {
                try { CancelPending(); } catch (Exception error) { failures.Add(error); }
                try { if (pump != null && pump.IsAlive) pump.Destroy(); }
                catch (Exception error) { failures.Add(error); }
                if (failures.Count == 0) pump = null;
                else if (pump != null && !pump.IsAlive) pump = null;
            }
            finally { stopping = false; }
            if (failures.Count != 0)
            {
                var error = new AggregateException("Runtime command teardown failed", failures);
                RecordFailure(error);
                throw error;
            }
        }
        internal static void Drain()
        {
            RuntimeApi.Kernel.CheckThread();
            int count = pending.Count;
            while (count-- > 0 && pending.Count != 0)
            {
                try { pending.Dequeue().Run(); }
                catch (Exception error)
                {
                    // Reject new work before invoking user-supplied cancellation.
                    poisoned = true;
                    Exception failure = error;
                    try { CancelPending(); }
                    catch (Exception cleanup)
                    { failure = new AggregateException("Command and cancellation failed", error, cleanup); }
                    RecordFailure(failure);
                    break;
                }
            }
        }
        private static void CancelPending()
        {
            Command[] cancelled = pending.ToArray();
            pending.Clear();
            cancelling = true;
            var failures = new List<Exception>();
            try
            {
                foreach (Command command in cancelled)
                    try { if (command.Cancel != null) command.Cancel(); }
                    catch (Exception error) { failures.Add(error); }
            }
            finally { cancelling = false; }
            if (failures.Count != 0)
                throw new AggregateException("Runtime command cancellation failed", failures);
        }
        private static void RecordFailure(Exception error)
        {
            LastError = error.ToString();
            poisoned = true;
            Console.WriteLine("[JK Runtime] Command failed: " + error);
        }
        private sealed class Pump : Entity
        {
            protected override void Update(float delta) { Drain(); }
        }
    }

    public sealed class Setting<T>
    {
        private readonly Func<T> read;
        private readonly Action<T> persist;
        private readonly Action apply;
        private readonly Func<T, bool> validate;
        private int revision;
        private bool queued;
        private T appliedValue;
        public string Id { get; private set; }
        public string Label { get; private set; }
        public T Value { get { return read(); } }
        public Setting(string id, string label, Func<T> get, Action<T> save, Action changed = null, Func<T, bool> valid = null)
        {
            Id = ModuleDefinition.ValidId(id); Label = label;
            if (get == null || save == null) throw new ArgumentNullException("get/save");
            read = get; persist = save; apply = changed; validate = valid;
        }
        // UI, key bindings and automation use the same validation/persistence/
        // application path. The game tree is never edited inside its callbacks.
        public void Set(T value)
        {
            RuntimeApi.Kernel.CheckThread();
            if (validate != null && !validate(value)) throw new ArgumentOutOfRangeException("value", Id);
            if (apply != null) Commands.CheckAccepting();
            if (!queued) appliedValue = read();
            T previous = read();
            try { persist(value); }
            catch (Exception failure)
            {
                try { persist(previous); }
                catch (Exception rollback) { throw new AggregateException("Setting persistence and rollback failed: " + Id, failure, rollback); }
                throw;
            }
            if (apply == null) return;
            queued = true; int request = ++revision;
            Commands.Enqueue(delegate {
                if (request != revision) return;
                try { apply(); }
                catch (Exception failure)
                {
                    var failures = new List<Exception> { failure };
                    try { persist(appliedValue); } catch (Exception rollback) { failures.Add(rollback); }
                    try { apply(); } catch (Exception rollback) { failures.Add(rollback); }
                    throw new AggregateException("Setting application failed; rollback " + (failures.Count == 1 ? "completed" : "incomplete") + ": " + Id, failures);
                }
                finally { queued = false; }
            }, delegate { if (request == revision) queued = false; });
        }
    }
}
