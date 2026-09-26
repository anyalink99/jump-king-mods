using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JKRuntime
{
    public sealed class RuntimeNotice
    {
        public long Sequence { get; internal set; }
        public string Owner { get; internal set; }
        public string Operation { get; internal set; }
        public string Detail { get; internal set; }
        public double Milliseconds { get; internal set; }
    }
    /// <summary>Bounded local diagnostics. Timing is opt-in and never starts a worker.</summary>
    public static class RuntimeJournal
    {
        private static readonly Queue<RuntimeNotice> notices = new Queue<RuntimeNotice>();
        private static long sequence;
        private static bool profiling;
        public static bool ProfileCallbacks { get { RuntimeApi.Kernel.CheckThread(); return profiling; } set { RuntimeApi.Kernel.CheckThread(); profiling = value; } }
        public static void Record(string owner, string operation, string detail)
        {
            RuntimeApi.Kernel.CheckThread();
            Add(owner, operation, detail, 0);
        }
        private static void Add(string owner, string operation, string detail, double milliseconds)
        {
            if (notices.Count == 256) notices.Dequeue();
            string value = detail ?? "";
            notices.Enqueue(new RuntimeNotice { Sequence = ++sequence, Owner = owner, Operation = operation,
                Detail = value.Length > 2048 ? value.Substring(0, 2048) : value, Milliseconds = milliseconds });
        }
        /// <summary>Return up to 256 recent records. Per-record details are capped at 2048 characters.</summary>
        public static RuntimeNotice[] Read()
        { RuntimeApi.Kernel.CheckThread(); return notices.ToArray(); }
        internal static bool Observe(string owner, string operation, Action callback)
        {
            long start = ProfileCallbacks ? Stopwatch.GetTimestamp() : 0;
            try { callback(); return true; }
            catch (Exception error) { Add(owner, operation, error.ToString(), 0); return false; }
            finally { if (start != 0) Add(owner, operation, "callback timing", (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency); }
        }
    }

    /// <summary>Tracks resources which have not completed release, including failed cleanup.</summary>
    public static class RuntimeResources
    {
        private sealed class Lease : IDisposable
        {
            internal string Owner, Kind;
            internal IDisposable Value;
            internal bool Releasing;
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (Value == null) return;
                if (Releasing) throw new InvalidOperationException("Reentrant resource release");
                Releasing = true;
                try { Value.Dispose(); Value = null; leases.Remove(this); }
                catch (Exception error) { RuntimeJournal.Record(Owner, "release:" + Kind, error.ToString()); throw; }
                finally { Releasing = false; }
            }
        }
        private static readonly List<Lease> leases = new List<Lease>();
        public static IDisposable Track(string owner, string kind, IDisposable value)
        {
            RuntimeApi.Kernel.CheckThread();
            ModuleDefinition.ValidId(owner);
            if (value == null) throw new ArgumentNullException("value");
            var lease = new Lease { Owner = owner, Kind = kind ?? "resource", Value = value };
            leases.Add(lease); return lease;
        }
        /// <summary>List outstanding tracked resources by owner. Compare before and after teardown; live resources are not automatically leaks.</summary>
        public static string[] Inspect()
        {
            RuntimeApi.Kernel.CheckThread();
            return leases.Select(l => l.Owner + ":" + l.Kind).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }
    }
}
