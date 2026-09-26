using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace JKRuntime.UI
{
    // A small local status file also works before level diagnostics are available.
    // No writes on unchanged frames; rapid changes are coalesced to four per second.
    internal static class PointerDiagnostics
    {
        private static bool enabled, previousLeft;
        private static string status = "Not initialized", written = "";
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long lastWrite, lastObservation;
        private static long polls, draws, presses;
        private static readonly LatestDiagnosticWriter writer = new LatestDiagnosticWriter(
            delegate(string text) { File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(UIApi).Assembly.Location), "JKRuntime.Pointer.txt"), text); },
            delegate(Action work) { ThreadPool.QueueUserWorkItem(delegate { work(); }); });
        internal static void Start() { enabled = true; Status("Starting menu mouse"); }
        internal static void Frame() { draws++; }
        internal static void Status(string value)
        {
            if (value == status) return;
            status = value;
            Write(value, "", true);
        }
        internal static void Observe(object owner, bool allowed, bool focus, bool inside, bool left, bool right, bool keyboard)
        {
            polls++;
            if (left && !previousLeft) presses++;
            previousLeft = left;
            long now = Clock.ElapsedMilliseconds;
            if (!enabled || now - lastObservation < 250) return;
            lastObservation = now;
            string state = "Surface: " + (owner == null ? "none" : owner.GetType().FullName)
                + "\r\nAccepts pointer: " + allowed + "; focused: " + focus + "; inside viewport: " + inside
                + "\r\nCursor visible: " + UiPointer.Visible + "; keyboard/controller active: " + keyboard
                + "\r\nLeft: " + left + "; right: " + right + "; left presses seen: " + presses;
            Write(status + "\r\n" + state, "\r\nPolls: " + polls + "; draws: " + draws + "; position: " + UiPointer.Position, false);
        }
        private static void Write(string state, string detail, bool force)
        {
            if (!enabled || (!force && (state == written || Clock.ElapsedMilliseconds - lastWrite < 250))) return;
            try
            {
                writer.Publish("JK Runtime " + RuntimeApi.Version + " mouse\r\n" + DateTime.UtcNow.ToString("O") + "\r\n" + state + detail);
                written = state; lastWrite = Clock.ElapsedMilliseconds;
            }
            catch { enabled = false; } // Diagnostics must never interrupt UI or gameplay.
        }
    }

    // One worker and one replaceable pending snapshot; slow storage must not
    // block a frame or build an unbounded queue. Only immutable text crosses threads.
    internal sealed class LatestDiagnosticWriter
    {
        private readonly object gate = new object();
        private readonly Action<string> write;
        private readonly Action<Action> schedule;
        private string pending;
        private bool running, failed;
        internal LatestDiagnosticWriter(Action<string> sink, Action<Action> queue) { write = sink; schedule = queue; }
        internal void Publish(string text)
        {
            lock (gate)
            {
                if (failed) return;
                pending = text;
                if (running) return;
                running = true;
            }
            try { schedule(Drain); }
            catch { Fail(); }
        }
        private void Drain()
        {
            while (true)
            {
                string text;
                lock (gate)
                {
                    text = pending; pending = null;
                    if (text == null) { running = false; return; }
                }
                try { write(text); }
                catch { Fail(); return; }
            }
        }
        private void Fail() { lock (gate) { failed = true; running = false; pending = null; } }
    }
}
