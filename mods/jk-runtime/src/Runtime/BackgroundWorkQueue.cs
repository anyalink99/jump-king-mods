using System;
using System.Collections.Generic;
using System.Threading;

namespace JKRuntime
{
    /// <summary>Observable completion of a bounded managed background operation.</summary>
    public enum BackgroundWorkState { Queued, Running, Succeeded, Failed }

    /// <summary>A memory-only completion record; it never retains the submitted payload after execution.</summary>
    public sealed class BackgroundWork
    {
        private int state;
        private string error;
        internal Action Action;
        public BackgroundWorkState State { get { return (BackgroundWorkState)Volatile.Read(ref state); } }
        public string Error { get { return error ?? ""; } }
        public bool IsCompleted { get { return State == BackgroundWorkState.Succeeded || State == BackgroundWorkState.Failed; } }
        internal void Execute()
        {
            Volatile.Write(ref state, (int)BackgroundWorkState.Running);
            try { Action(); Volatile.Write(ref state, (int)BackgroundWorkState.Succeeded); }
            catch (Exception failure) { error = failure.GetBaseException().Message; Volatile.Write(ref state, (int)BackgroundWorkState.Failed); }
            finally { Action = null; }
        }
    }

    /// <summary>Serial bounded work on immutable managed data. Lazy foreground workers drain accepted writes before process exit, then terminate.</summary>
    public sealed class BackgroundWorkQueue : IDisposable
    {
        private readonly object sync = new object();
        private readonly Queue<BackgroundWork> queue = new Queue<BackgroundWork>();
        private readonly string name;
        private readonly int capacity;
        private int outstanding;
        private bool running, closed;
        public int PendingCount { get { return Volatile.Read(ref outstanding); } }

        public BackgroundWorkQueue(string owner, int maximumPending = 2)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Work owner is required", "owner");
            if (maximumPending < 1) throw new ArgumentOutOfRangeException("maximumPending");
            name = owner; capacity = maximumPending;
        }
        public bool TryEnqueue(Action action, out BackgroundWork work)
        {
            if (action == null) throw new ArgumentNullException("action");
            return TryPrepare(delegate { return action; }, out work);
        }

        /// <summary>Reserves capacity before synchronous snapshot preparation. The returned action executes on the worker and must not access engine objects.</summary>
        public bool TryPrepare(Func<Action> prepare, out BackgroundWork work)
        {
            if (prepare == null) throw new ArgumentNullException("prepare");
            work = null;
            lock (sync)
            {
                if (closed || outstanding >= capacity) return false;
                outstanding++;
            }
            BackgroundWork candidate = null;
            bool published = false;
            try
            {
                candidate = new BackgroundWork { Action = prepare() };
                if (candidate.Action == null) throw new InvalidOperationException("Prepared work cannot be null");
                lock (sync)
                {
                    queue.Enqueue(candidate);
                    if (!running)
                    {
                        var thread = new Thread(Run) { Name = "JK Runtime: " + name, IsBackground = false };
                        try { thread.Start(); running = true; }
                        catch { queue.Dequeue(); throw; }
                    }
                    published = true;
                }
                work = candidate;
                return true;
            }
            finally
            {
                if (!published)
                {
                    if (candidate != null) candidate.Action = null;
                    lock (sync) { outstanding--; Monitor.PulseAll(sync); }
                }
            }
        }

        private void Run()
        {
            while (true)
            {
                BackgroundWork work;
                lock (sync)
                {
                    if (queue.Count == 0) { running = false; Monitor.PulseAll(sync); return; }
                    work = queue.Dequeue();
                }
                work.Execute();
                if (work.State == BackgroundWorkState.Failed) Console.WriteLine("[JK Runtime " + name + "] " + work.Error);
                lock (sync) { outstanding--; Monitor.PulseAll(sync); }
            }
        }

        /// <summary>Waits at an explicit loading/shutdown boundary. Never call from Draw or a playable update.</summary>
        public bool Drain(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            lock (sync)
            {
                while (outstanding != 0)
                {
                    int remaining = timeoutMilliseconds - (int)Math.Min(int.MaxValue, elapsed.ElapsedMilliseconds);
                    if (remaining <= 0) return false;
                    Monitor.Wait(sync, remaining);
                }
                return true;
            }
        }
        /// <summary>Rejects new submissions; accepted immutable jobs still drain. This does not block the game thread.</summary>
        public void Dispose() { lock (sync) closed = true; }
    }
}
