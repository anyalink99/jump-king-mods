using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
namespace JKRuntime.Input
{
    public sealed class KeyboardActionEdges : IDisposable
    {
        private readonly object sync = new object();
        private readonly Func<int, short> read;
        private readonly Func<IntPtr> foreground;
        private readonly int[][][] bindings;
        private readonly IntPtr window;
        private volatile bool running;
        private bool ready;
        private int held, blocked, pending;
        private long lastSample;
        private readonly long[] pendingAt;
        private readonly KeyboardSubscription physical;
        private readonly KeyboardInputGate textInput = new KeyboardInputGate();
        public KeyboardActionEdges(int[][][] actions, IntPtr handle, Func<int, short> reader, Func<IntPtr> getForeground, bool start)
        {
            if (actions == null || actions.Length == 0 || actions.Length > 31) throw new ArgumentException("One to 31 actions are required", "actions");
            bindings = actions.Select(alternatives => (alternatives ?? new int[0][]).Select(chord => (int[])(chord ?? new int[0]).Clone()).ToArray()).ToArray();
            pendingAt = new long[bindings.Length];
            window = handle; read = reader; foreground = getForeground;
            if (start && reader == null) { physical = SharedKeyboard.Subscribe(bindings.SelectMany(x=>x).SelectMany(x=>x).ToArray()); return; }
            if (reader == null || getForeground == null) throw new ArgumentException("An injected reader and foreground provider are required for manual sampling");
            if (start) { running = true; new Thread(Run) { IsBackground=true, Name="JK Runtime injected keyboard actions" }.Start(); }
        }
        private void Run()
        {
            while(running)
            {
                try { Sample(Stopwatch.GetTimestamp()); }
                catch { lock(sync) { ready=false; pending=held=0; } }
                Thread.Sleep(1);
            }
        }
        public void Sample(long now)
        {
            if (physical != null)
            {
                KeyboardSample sample;
                while (physical.TryRead(out sample))
                {
                    bool sampleActive = sample.Reliable && sample.Foreground == window;
                    int sampleState = 0;
                    if (sampleActive) for (int action=0; action<bindings.Length; action++) foreach (var chord in bindings[action])
                    {
                        bool down=chord.Length!=0;
                        foreach (int key in chord) if (!sample.IsDown(key)) { down=false; break; }
                        if (down) { sampleState|=1<<action; break; }
                    }
                    Publish(sampleState,sampleActive,sample.Timestamp);
                }
                return;
            }
            bool active=foreground()==window;
            int state=0;
            if(active)
            {
                for(int action=0;action<bindings.Length;action++) foreach(var chord in bindings[action])
                {
                    bool down=chord.Length!=0;
                    foreach(int key in chord) down &= key >= 0 && key < 256 && (read(key)&0x8000)!=0;
                    if(down) { state|=1<<action; break; }
                }
            }
            Publish(state,active,now);
        }
        public void Publish(int state,bool active,long now)
        {
            lock(sync)
            {
                if(now<lastSample) return;
                if(textInput.Suppress(state != 0))
                { blocked=state; held=pending=0; ready=active; lastSample=now; return; }
                if(!active || !ready || now-lastSample>Stopwatch.Frequency/20)
                { blocked=state; held=pending=0; ready=active; }
                blocked &= state; state &= ~blocked;
                int edges=state&~held;
                pending|=edges;
                for(int action=0;action<bindings.Length;action++) if((edges&(1<<action))!=0) pendingAt[action]=now;
                held=state; lastSample=now;
            }
        }
        public int Take(long now)
        {
            lock(sync)
            {
                if(textInput.Suppress((held | blocked) != 0))
                { pending=0; blocked|=held; held=0; return 0; }
                int result=0;
                if(ready && now-lastSample<=Stopwatch.Frequency/20)
                    for(int action=0;action<bindings.Length;action++) if((pending&(1<<action))!=0 && now-pendingAt[action]<=Stopwatch.Frequency/20) result|=1<<action;
                pending=0; return result;
            }
        }
        public void Clear() { lock(sync) { pending=0; blocked|=held; held=0; } }
        public int Down { get { lock(sync) return held | blocked; } }
        public void Dispose() { running=false; if (physical != null) physical.Dispose(); Clear(); }
    }

}
