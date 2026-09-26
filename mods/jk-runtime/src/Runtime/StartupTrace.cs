using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JKRuntime
{
    // Opt-in investigation of real restart frames. Fixed storage on the game
    // thread; formatting and disk access happen only on a bounded worker.
    internal static class StartupTrace
    {
        internal struct Entry { internal string Name; internal Type Subject; internal long Start, End; }
        internal sealed class Snapshot
        {
            internal Entry[] Entries;
            internal long Started;
            internal int Attempt, Dropped, Gen0, Gen1, Gen2;
            internal string Reason;
        }
        internal struct Span : IDisposable
        {
            private readonly string name;
            private readonly long start;
            internal Span(string value) { name = value; start = BeginWork(); }
            public void Dispose() { if (start != 0) Record(name, start, Stopwatch.GetTimestamp()); }
        }
        internal const int Capacity = 8192;
        private static readonly Entry[] entries = new Entry[Capacity];
        private static bool markerEnabled;
        private static bool initialized, enabled, capturing, gameplay, preparing;
        private static int count, dropped, frames, attempt, gen0, gen1, gen2;
        private static long started, updateStart, drawStart, previousDraw;
        private static string directory;
        private static Action<Snapshot> submit = Enqueue;
        private static readonly object gate = new object();
        private static Snapshot pending;
        private static bool writing;
        private static readonly Queue<string> history = new Queue<string>();
        internal static bool Enabled { get { return enabled; } }

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                directory = Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location);
                enabled = markerEnabled = File.Exists(Path.Combine(directory, "JKRuntime.StartupTrace.enabled"));
            }
            catch { enabled = false; }
        }
        internal static void SetDiagnosticMode(bool value)
        {
            Initialize();
            if (!value && !markerEnabled) Finish("diagnostics disabled");
            enabled = value || markerEnabled;
            StartupDiagnosticHooks.Configure(enabled);
        }
        internal static void ConfigureForTest(Action<Snapshot> sink)
        { enabled = initialized = true; capturing = false; submit = sink; }
        internal static void ResetForTest()
        { capturing = enabled = false; submit = Enqueue; }
        internal static Span Measure(string name) { return new Span(name); }
        internal static void BeginAttempt()
        {
            if (!enabled) return;
            Finish("next attempt");
            started = Stopwatch.GetTimestamp(); count = dropped = frames = 0; attempt++;
            gen0 = GC.CollectionCount(0); gen1 = GC.CollectionCount(1); gen2 = GC.CollectionCount(2);
            capturing = true; preparing = true; gameplay = false;
        }
        internal static void EndPreparation() { preparing = false; }
        internal static void BeginGameplay()
        {
            if (gameplay) return;
            gameplay = true;
            if (capturing) { long now = Stopwatch.GetTimestamp(); Record("runtime.gameplay-handoff", now, now); }
        }
        internal static void Record(string name, long begin, long end)
        { Record(name, null, begin, end); }
        private static void Record(string name, Type subject, long begin, long end)
        {
            if (!capturing || (!preparing && !gameplay) || begin == 0) return;
            if (count == entries.Length) { dropped++; return; }
            entries[count++] = new Entry { Name = name, Subject = subject, Start = begin, End = end };
        }
        internal static long BeginWork() { return capturing && (preparing || gameplay) ? Stopwatch.GetTimestamp() : 0; }
        internal static void EndWork(string name, Type subject, long begin, long end, bool slowOnly)
        {
            if (slowOnly && end - begin < Stopwatch.Frequency / 2000) return;
            // Keep type metadata, not live entities. Resolve names on the worker.
            Record(name, subject, begin, end);
        }
        internal static void BeginUpdate() { if (enabled) updateStart = Stopwatch.GetTimestamp(); }
        internal static void EndUpdate() { if (capturing) Record("frame.update", updateStart, Stopwatch.GetTimestamp()); }
        internal static void BeginDraw()
        {
            if (!enabled) return;
            drawStart = Stopwatch.GetTimestamp();
            if (capturing && previousDraw != 0) Record("frame.gap", previousDraw, drawStart);
            previousDraw = drawStart;
        }
        internal static void EndDraw()
        {
            if (!capturing) return;
            Record("frame.draw", drawStart, Stopwatch.GetTimestamp());
            if (gameplay && ++frames >= 120) Finish("120 presented gameplay frames");
        }
        internal static void Finish(string reason)
        {
            if (!capturing) return;
            capturing = false;
            var copy = new Entry[count]; Array.Copy(entries, copy, count);
            var snapshot = new Snapshot { Entries = copy, Started = started, Attempt = attempt,
                Dropped = dropped, Reason = reason, Gen0 = GC.CollectionCount(0) - gen0,
                Gen1 = GC.CollectionCount(1) - gen1, Gen2 = GC.CollectionCount(2) - gen2 };
            try { submit(snapshot); } catch { enabled = false; }
        }
        private static void Enqueue(Snapshot snapshot)
        {
            lock (gate) { pending = snapshot; if (writing) return; writing = true; }
            try { if (!ThreadPool.QueueUserWorkItem(delegate { Drain(); })) throw new InvalidOperationException("Trace worker unavailable"); }
            catch { lock (gate) { pending = null; writing = false; } }
        }
        private static void Drain()
        {
            while (true)
            {
                Snapshot snapshot;
                lock (gate) { snapshot = pending; pending = null; if (snapshot == null) { writing = false; return; } }
                try
                {
                    if (history.Count == 6) history.Dequeue();
                    history.Enqueue(Format(snapshot));
                    File.WriteAllText(Path.Combine(directory, "JKRuntime.Startup.txt"), string.Join("\r\n", history.ToArray()));
                }
                catch { /* A diagnostic failure must not interrupt gameplay. */ }
            }
        }
        internal static string Format(Snapshot snapshot)
        {
            var text = new StringBuilder();
            text.AppendLine("JK Runtime " + RuntimeApi.Version + " startup trace; attempt " + snapshot.Attempt + "; " + snapshot.Reason);
            text.AppendLine("GC collections: " + snapshot.Gen0 + "/" + snapshot.Gen1 + "/" + snapshot.Gen2 + "; dropped records: " + snapshot.Dropped);
            text.AppendLine("start_ms\tduration_ms\tstage (nested timings overlap; negative start includes restart work before Runtime startup)");
            foreach (var entry in snapshot.Entries)
                text.AppendLine(((entry.Start - snapshot.Started) * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture)
                    + "\t" + ((entry.End - entry.Start) * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) + "\t"
                    + (entry.Subject == null ? "" : entry.Subject.FullName + ".") + entry.Name);
            return text.ToString();
        }
    }
}
