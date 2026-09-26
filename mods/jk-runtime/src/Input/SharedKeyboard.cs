using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace JKRuntime.Input
{
    /// <summary>Immutable physical keyboard/mouse observation. Timestamps use Stopwatch ticks; unreliable samples invalidate edge evidence.</summary>
    public struct KeyboardSample
    {
        private readonly ulong a, b, c, d;
        public readonly long Timestamp;
        public readonly IntPtr Foreground;
        public readonly bool Reliable;
        internal readonly long Generation;
        internal KeyboardSample(ulong first, ulong second, ulong third, ulong fourth, IntPtr foreground, long timestamp, bool reliable, long generation)
        { a=first; b=second; c=third; d=fourth; Foreground=foreground; Timestamp=timestamp; Reliable=reliable; Generation=generation; }
        public bool IsDown(int virtualKey)
        {
            if (virtualKey < 0 || virtualKey >= 256) return false;
            ulong bits = virtualKey < 64 ? a : virtualKey < 128 ? b : virtualKey < 192 ? c : d;
            return (bits & (1UL << (virtualKey & 63))) != 0;
        }
    }
    /// <summary>One physical reader for the union of requested virtual keys. Consumers have independent cursors over a bounded observation history, not separate OS pollers.</summary>
    public static class SharedKeyboard
    {
        private static readonly KeyboardSource source = new KeyboardSource(GetAsyncKeyState, GetForegroundWindow, true);
        public static KeyboardSubscription Subscribe(int[] virtualKeys) { return source.Subscribe(virtualKeys); }
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    }
    public sealed class KeyboardSubscription : IDisposable
    {
        internal readonly int[] Keys;
        internal long Cursor, Generation;
        private readonly KeyboardSource source;
        internal bool Disposed;
        internal KeyboardSubscription(KeyboardSource value, int[] keys) { source=value; Keys=keys; }
        /// <summary>Consume one observation without OS calls. Overflow is reported as unreliable before remaining observations.</summary>
        public bool TryRead(out KeyboardSample sample) { return source.Read(this, out sample); }
        public void Dispose() { source.Remove(this); }
    }
    internal sealed class KeyboardSource
    {
        internal const int Capacity = 256;
        private readonly object sync = new object();
        private readonly Func<int, short> reader;
        private readonly Func<IntPtr> foreground;
        private readonly bool automatic;
        private readonly KeyboardSample[] history = new KeyboardSample[Capacity];
        private readonly List<KeyboardSubscription> subscribers = new List<KeyboardSubscription>();
        private int[] keys = new int[0];
        private long sequence, generation;
        private Thread worker;
        private bool reading;
        internal KeyboardSource(Func<int, short> read, Func<IntPtr> focus, bool start)
        { reader=read; foreground=focus; automatic=start; }
        internal KeyboardSubscription Subscribe(int[] requested)
        {
            if (requested == null) throw new ArgumentNullException("requested");
            if (requested.Any(k => k < 0 || k >= 256)) throw new ArgumentOutOfRangeException("requested");
            var subscription = new KeyboardSubscription(this, requested.Distinct().OrderBy(k=>k).ToArray());
            lock (sync)
            {
                subscribers.Add(subscription); RebuildKeys();
                subscription.Cursor=sequence; subscription.Generation=generation;
                if (automatic && worker == null)
                {
                    worker=new Thread(Run) { IsBackground=true, Name="JK Runtime physical keyboard" };
                    try { worker.Start(); }
                    catch { worker=null; subscribers.Remove(subscription); RebuildKeys(); throw; }
                }
            }
            return subscription;
        }
        private void RebuildKeys() { keys=subscribers.SelectMany(s=>s.Keys).Distinct().OrderBy(k=>k).ToArray(); generation++; }
        internal void Remove(KeyboardSubscription subscription)
        {
            lock (sync)
            {
                if (subscription.Disposed) return;
                subscription.Disposed=true; subscribers.Remove(subscription); RebuildKeys();
            }
        }
        private void Run()
        {
            uint timer=TimeBeginPeriod(1);
            try
            {
                while (true)
                {
                    lock (sync) { if (subscribers.Count == 0) { worker=null; return; } }
                    Poll(); Thread.Sleep(1);
                }
            }
            finally { if (timer==0) TimeEndPeriod(1); }
        }
        internal void Poll()
        {
            int[] requested; long observed;
            lock (sync)
            {
                if (reading || subscribers.Count==0) return;
                reading=true; requested=keys; observed=generation;
            }
            ulong a=0,b=0,c=0,d=0; IntPtr focus=IntPtr.Zero; bool reliable=true;
            try
            {
                focus=foreground();
                foreach (int key in requested)
                {
                    if ((reader(key)&0x8000)==0) continue;
                    ulong bit=1UL<<(key&63);
                    if (key<64) a|=bit; else if (key<128) b|=bit; else if (key<192) c|=bit; else d|=bit;
                }
            }
            catch { reliable=false; }
            finally
            {
                var sample=new KeyboardSample(a,b,c,d,focus,Stopwatch.GetTimestamp(),reliable,observed);
                lock (sync) { history[(int)(sequence%Capacity)]=sample; sequence++; reading=false; }
            }
        }
        internal bool Read(KeyboardSubscription subscription, out KeyboardSample sample)
        {
            lock (sync)
            {
                sample=default(KeyboardSample);
                if (subscription.Disposed) return false;
                if (sequence-subscription.Cursor>Capacity)
                {
                    subscription.Cursor=sequence-Capacity;
                    var oldest=history[(int)(subscription.Cursor%Capacity)];
                    sample=new KeyboardSample(0,0,0,0,oldest.Foreground,oldest.Timestamp,false,generation); return true;
                }
                while (subscription.Cursor<sequence)
                {
                    sample=history[(int)(subscription.Cursor++%Capacity)];
                    if (sample.Generation>=subscription.Generation) return true;
                }
                return false;
            }
        }
        [DllImport("winmm.dll", EntryPoint="timeBeginPeriod")] private static extern uint TimeBeginPeriod(uint milliseconds);
        [DllImport("winmm.dll", EntryPoint="timeEndPeriod")] private static extern uint TimeEndPeriod(uint milliseconds);
    }
}
