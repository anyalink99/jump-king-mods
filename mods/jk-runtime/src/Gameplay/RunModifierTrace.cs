using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace JKRuntime.Gameplay
{
    // Opt-in registration diagnostics, never a behaviour-execution/use detector.
    internal static class RunModifierTrace
    {
        private static bool initialized;
        private static ModifierTraceLog log;
        internal static bool Enabled { get { return log != null; } }
        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                string root = Path.GetDirectoryName(typeof(RunModifiers).Assembly.Location);
                if (!File.Exists(Path.Combine(root, "JKRuntime.RunModifiersTrace.enabled"))) return;
                string path = Path.Combine(root, "JKRuntime.RunModifiersTrace." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".txt");
                log = new ModifierTraceLog(path, 4096);
                AppDomain.CurrentDomain.ProcessExit += delegate { log.Wait(2000); };
                Record("trace-start", "Runtime " + RuntimeApi.Version + "; MVID=" + typeof(RunModifiers).Module.ModuleVersionId
                    + "; game MVID=" + typeof(JumpKing.Player.BodyComp).Module.ModuleVersionId
                    + "; registration evidence only, not proof of feature use");
            }
            catch (Exception error) { Console.WriteLine("[JK Runtime] Modifier trace unavailable: " + error.Message); }
        }
        internal static string Stack() { return Enabled ? new StackTrace(2, false).ToString() : null; }
        internal static void Record(string kind, string detail)
        {
            if (log != null) log.Record(kind + " " + detail);
        }
    }

    // Retain the first events (including startup and the first unknown), then an
    // explicit truncation marker. One bounded queue; disk I/O is off the game thread.
    internal sealed class ModifierTraceLog
    {
        private readonly object gate = new object();
        private readonly Queue<string> pending = new Queue<string>();
        private readonly ManualResetEvent idle = new ManualResetEvent(true);
        private readonly string path;
        private readonly int limit;
        private int count;
        private bool writing, failed;
        internal ModifierTraceLog(string path, int limit) { this.path = path; this.limit = limit; }
        internal void Record(string text)
        {
            if (text.Length > 8192) text = text.Substring(0, 8192) + " [event truncated]";
            lock (gate)
            {
                if (failed || count > limit) return;
                if (count++ == limit) text = "TRUNCATED: event limit reached; later events are not captured";
                pending.Enqueue(DateTime.UtcNow.ToString("o") + " " + text);
                if (writing) return;
                writing = true; idle.Reset();
            }
            try
            {
                if (!ThreadPool.QueueUserWorkItem(delegate { Drain(); })) throw new IOException("Trace worker unavailable");
            }
            catch (Exception error) { Fail(error); }
        }
        private void Drain()
        {
            try
            {
                while (true)
                {
                    string[] batch;
                    lock (gate)
                    {
                        if (pending.Count == 0) { writing = false; idle.Set(); return; }
                        batch = pending.ToArray(); pending.Clear();
                    }
                    File.AppendAllLines(path, batch);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private void Fail(Exception error)
        {
            lock (gate) { failed = true; pending.Clear(); writing = false; idle.Set(); }
            Console.WriteLine("[JK Runtime] Modifier trace write failed: " + error.Message);
        }
        internal bool Wait(int milliseconds) { return idle.WaitOne(milliseconds); }
    }
}
