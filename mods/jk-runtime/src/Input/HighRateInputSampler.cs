using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime.Input
{
    public struct JumpInputTransition
    {
        public readonly bool IsDown;
        public readonly long Timestamp;
        public readonly bool Reliable;

        public JumpInputTransition(bool isDown, long timestamp)
        {
            IsDown = isDown;
            Timestamp = timestamp;
            Reliable = true;
        }
        public JumpInputTransition(long timestamp) { IsDown = false; Timestamp = timestamp; Reliable = false; }
    }

    public sealed class XInputJumpBinding
    {
        public readonly int UserIndex;
        public readonly int[][] Alternatives;

        public XInputJumpBinding(int userIndex, int[] buttons)
            : this(userIndex, ToSingleButtonAlternatives(buttons))
        {
        }

        public XInputJumpBinding(int userIndex, int[][] alternatives)
        {
            if (userIndex < 0 || userIndex > 3)
            {
                throw new ArgumentOutOfRangeException("userIndex");
            }
            UserIndex = userIndex;
            Alternatives = CloneAlternatives(alternatives);
        }

        private static int[][] ToSingleButtonAlternatives(int[] buttons)
        {
            if (buttons == null || buttons.Length == 0)
            {
                return new int[0][];
            }
            int[][] result = new int[buttons.Length][];
            for (int index = 0; index < buttons.Length; index++)
            {
                result[index] = new[] { buttons[index] };
            }
            return result;
        }

        private static int[][] CloneAlternatives(int[][] alternatives)
        {
            if (alternatives == null || alternatives.Length == 0)
            {
                return new int[0][];
            }
            int[][] result = new int[alternatives.Length][];
            for (int index = 0; index < alternatives.Length; index++)
            {
                result[index] = alternatives[index] == null
                    ? new int[0]
                    : (int[])alternatives[index].Clone();
            }
            return result;
        }
    }

    // Drivers run outside sync, one worker per physical source. The only
    // shared critical section publishes small snapshots and drains bounded edges.
    public sealed class HighRateInputSampler : IHighRateInput
    {
        // Explicit forwarding keeps previously shipped public method flags intact.
        bool IHighRateInput.Enabled { get { return Enabled; } }
        bool IHighRateInput.RequestedEnabled { get { return RequestedEnabled; } }
        bool IHighRateInput.Available { get { return Available; } }
        void IHighRateInput.Start() { Start(); }
        void IHighRateInput.Configure(int[][] keys, XInputJumpBinding[] xbox, DirectInputJumpBinding[] legacy, IntPtr handle, bool enabled)
        { Configure(keys, xbox, legacy, handle, enabled); }
        bool IHighRateInput.CanMeasureDirectInput(Guid id) { return CanMeasureDirectInput(id); }
        bool IHighRateInput.CanMeasurePhysical(int source) { return CanMeasurePhysical(source); }
        bool IHighRateInput.TryDequeue(out JumpInputTransition edge) { return TryDequeue(out edge); }
        void IHighRateInput.Reset() { Reset(); }
        void IHighRateInput.LogHealth(long now) { LogHealth(now); }
        private sealed class Source
        {
            public bool Ready, Down;
            public long Timestamp;
            public long Reads, MaximumGap;
            public int Losses;
            public string Name;
        }
        private readonly object sync = new object();
        private static readonly object ReadGate = new object();
        private static readonly bool[] Reading = new bool[5];
        private readonly Queue<JumpInputTransition> transitions = new Queue<JumpInputTransition>();
        private readonly HashSet<long> observedPressedControls = new HashSet<long>();
        private readonly Dictionary<Guid, Source> legacyStates = new Dictionary<Guid, Source>();
        private readonly Source[] physical = new Source[5];
        private readonly bool manual;
        private readonly DirectInputPoller directInput;
        private readonly Func<int, GamePadState>[] xboxReaders = new Func<int, GamePadState>[4];
        private readonly Func<int, GamePadState> injectedXbox;
        private readonly Func<int, short> readKey;
        private readonly bool sharedKeyboard;
        private KeyboardSubscription keyboard;
        private volatile bool running, requestedEnabled, samplerAvailable;
        private int[][] keyboardJumpAlternatives = new int[0][];
        private XInputJumpBinding[] xinputJumpBindings = new XInputJumpBinding[0];
        private long configurationGeneration, lastEdgeTimestamp;
        private IntPtr window;
        private bool wasDown;
        private readonly long[] nextProbe = new long[5];
        private readonly long[] probeGeneration = new long[5];
        private readonly bool[] sourceWorkers = new bool[5];
        internal int SourceWorkerCount { get { lock (sync) { int count = 0; foreach (bool active in sourceWorkers) if (active) count++; return count; } } }
        private long nextHealthLog;
        public const int MaximumQueuedEdges = 256;
        public static readonly long MaximumObservationGap = Stopwatch.Frequency / 20;

        public HighRateInputSampler() : this(false) { }
        public HighRateInputSampler(bool useObservationOnly)
            : this(useObservationOnly, null, null) { }
        public HighRateInputSampler(bool manualPolling, Func<int, short> keyReader,
            Func<int, GamePadState> xboxReader)
        {
            manual = manualPolling;
            injectedXbox = xboxReader;
            readKey = keyReader ?? GetAsyncKeyState;
            sharedKeyboard = !manualPolling && keyReader == null;
            for (int i = 0; i < physical.Length; i++) physical[i] = NewSource(i, false);
            if (!manual) directInput = new DirectInputPoller(DirectInputDevice.Open, ObserveDirectInput, false);
        }
        public bool Enabled { get { return requestedEnabled && samplerAvailable; } }
        public bool RequestedEnabled { get { return requestedEnabled; } }
        public bool Available { get { return samplerAvailable; } }
        public long ConfigurationGeneration { get { lock (sync) return configurationGeneration; } }

        public void Start()
        {
            if (samplerAvailable) return;
            samplerAvailable = true;
            running = true;
            if (manual) return;
            RefreshKeyboard();
            EnsureWorkers();
        }
        private bool WantsSource(int id)
        {
            if (!requestedEnabled) return false;
            if (id == 0) return !sharedKeyboard && keyboardJumpAlternatives.Length != 0;
            foreach (var binding in xinputJumpBindings) if (binding.UserIndex == id - 1) return true;
            return false;
        }
        private void EnsureWorkers()
        {
            if (manual) return;
            lock (sync)
            {
                if (!running) return;
                for (int i = 0; i < sourceWorkers.Length; i++)
                {
                    if (sourceWorkers[i] || !WantsSource(i)) continue;
                    int source = i; sourceWorkers[i] = true;
                    try { new Thread(delegate() { Run(source); }) { IsBackground = true, Name = "JKRuntime input " + source }.Start(); }
                    catch { sourceWorkers[i] = false; throw; }
                }
                Monitor.PulseAll(sync);
            }
        }
        public void Configure(int[] keys, bool enabled)
        { Configure(ToAlternatives(keys), new XInputJumpBinding[0], enabled); }
        public void Configure(int[] keys, XInputJumpBinding[] xbox, bool enabled)
        { Configure(ToAlternatives(keys), xbox, enabled); }
        public void Configure(int[][] keys, XInputJumpBinding[] xbox, bool enabled)
        { Configure(keys, xbox, new DirectInputJumpBinding[0], IntPtr.Zero, enabled); }
        public void Configure(int[][] keys, XInputJumpBinding[] xbox,
            DirectInputJumpBinding[] legacy, IntPtr handle, bool enabled)
        {
            long generation;
            lock (sync)
            {
                keyboardJumpAlternatives = Clone(keys);
                xinputJumpBindings = new XInputJumpBinding[xbox.Length];
                for (int i = 0; i < xbox.Length; i++)
                    xinputJumpBindings[i] = new XInputJumpBinding(xbox[i].UserIndex, xbox[i].Alternatives);
                requestedEnabled = enabled && (keys.Length + xbox.Length + legacy.Length != 0);
                generation = ++configurationGeneration;
                window = handle;
                for (int i = 0; i < physical.Length; i++) physical[i] = NewSource(i, manual);
                legacyStates.Clear();
                foreach (DirectInputJumpBinding binding in legacy) legacyStates[binding.DeviceId] = new Source { Name = "directinput:" + binding.DeviceId };
                transitions.Clear();
                observedPressedControls.Clear();
                wasDown = false;
                lastEdgeTimestamp = 0;
            }
            if (directInput != null) directInput.Configure(legacy, handle, generation);
            RefreshKeyboard();
            EnsureWorkers();
        }

        private void RefreshKeyboard()
        {
            if (!sharedKeyboard) return;
            lock (sync)
            {
                if (keyboard != null) { keyboard.Dispose(); keyboard = null; }
                if (!running || !requestedEnabled || keyboardJumpAlternatives.Length == 0) return;
                var keys = new HashSet<int>();
                foreach (var chord in keyboardJumpAlternatives) foreach (int key in chord)
                {
                    int physicalKey=MouseButtons.ToVirtualKey(key);
                    if (physicalKey>=0 && physicalKey<256) keys.Add(physicalKey);
                }
                var array=new int[keys.Count]; keys.CopyTo(array); keyboard=SharedKeyboard.Subscribe(array);
            }
        }
        private void PumpKeyboard()
        {
            KeyboardSubscription subscription; int[][] keys; IntPtr handle; long generation;
            lock (sync) { subscription=keyboard; keys=keyboardJumpAlternatives; handle=window; generation=configurationGeneration; }
            if (subscription == null) return;
            KeyboardSample sample;
            while (subscription.TryRead(out sample))
            {
                bool reliable=sample.Reliable && (handle==IntPtr.Zero || sample.Foreground==handle), down=false;
                if (reliable) foreach (var chord in keys)
                {
                    bool held=chord.Length>0;
                    foreach (int key in chord) if (!sample.IsDown(MouseButtons.ToVirtualKey(key))) { held=false; break; }
                    if (held) { down=true; break; }
                }
                ObservePhysical(generation,0,down,reliable,sample.Timestamp);
            }
        }

        public bool CanMeasureDirectInput(Guid id)
        {
            lock (sync)
            {
                Source state;
                return legacyStates.TryGetValue(id, out state) && Fresh(state);
            }
        }
        public bool CanMeasurePhysical(int source)
        { if (source == 0 && sharedKeyboard) PumpKeyboard(); lock (sync) return Fresh(physical[source]); }
        private bool Fresh(Source source)
        { return source.Ready && (manual || Stopwatch.GetTimestamp() - source.Timestamp <= MaximumObservationGap); }

        public void ObserveDirectInput(long generation, Guid id, bool down, bool reliable, long timestamp)
        {
            lock (sync)
            {
                Source source;
                if (!requestedEnabled || generation != configurationGeneration || !legacyStates.TryGetValue(id, out source)) return;
                Accept(source, down, reliable, timestamp);
            }
        }
        public void ObservePhysical(long generation, int id, bool down, bool reliable, long timestamp)
        {
            lock (sync)
            {
                if (!requestedEnabled || generation != configurationGeneration) return;
                Accept(physical[id], down, reliable, timestamp);
            }
        }

        private void Accept(Source source, bool down, bool reliable, long timestamp)
        {
            if (!manual && timestamp < source.Timestamp) return; // obsolete observation, never rewind a device
            source.Reads++;
            if (source.Ready && source.Timestamp != 0) source.MaximumGap = Math.Max(source.MaximumGap, timestamp - source.Timestamp);
            bool gap = !manual && source.Timestamp != 0 && timestamp - source.Timestamp > MaximumObservationGap;
            if (gap && source.Ready) Lose(source, timestamp, "observation-gap");
            bool previouslyReady = source.Ready;
            if (!reliable) Lose(source, timestamp, "unavailable");
            else
            {
                // Following a gap/disconnect/rebind, a held control has no known
                // press. Re-arm only at neutral, for EVERY backend.
                source.Ready = source.Ready || !down;
                source.Down = source.Ready && down;
            }
            source.Timestamp = timestamp;
            bool combined = Combined();
            if (previouslyReady && reliable && combined != wasDown)
                Enqueue(new JumpInputTransition(combined, timestamp));
            wasDown = combined;
        }

        private void Lose(Source source, long timestamp, string reason)
        {
            if (source.Ready && source.Down)
                Enqueue(new JumpInputTransition(timestamp));
            if (source.Ready)
            {
                source.Losses++;
                InputLog.Write("input source lost source=" + source.Name + " reason=" + reason + " contributed=" + source.Down);
            }
            source.Ready = source.Down = false;
            wasDown = Combined();
        }
        private bool Combined()
        {
            foreach (Source s in physical) if (s.Ready && s.Down) return true;
            foreach (Source s in legacyStates.Values) if (s.Ready && s.Down) return true;
            return false;
        }
        private void Enqueue(JumpInputTransition edge)
        {
            if (transitions.Count >= MaximumQueuedEdges || (!manual && edge.Timestamp < lastEdgeTimestamp))
            {
                transitions.Clear();
                // Never silently drop edges and keep presenting a precise hold.
                transitions.Enqueue(new JumpInputTransition(Math.Max(lastEdgeTimestamp, edge.Timestamp)));
                InputLog.Write("input evidence lost reason=queue-overflow-or-cross-source-reordering");
                lastEdgeTimestamp = Math.Max(lastEdgeTimestamp, edge.Timestamp);
                return;
            }
            transitions.Enqueue(edge);
            lastEdgeTimestamp = edge.Timestamp;
        }
        private void Expire(long now)
        {
            foreach (Source s in physical)
                if (s.Ready && s.Timestamp != 0 && now - s.Timestamp > MaximumObservationGap) Lose(s, now, "stalled-worker");
            foreach (Source s in legacyStates.Values)
                if (s.Ready && s.Timestamp != 0 && now - s.Timestamp > MaximumObservationGap) Lose(s, now, "stalled-worker");
        }
        public bool TryDequeue(out JumpInputTransition transition)
        {
            if (sharedKeyboard) PumpKeyboard();
            lock (sync)
            {
                if (!manual) Expire(Stopwatch.GetTimestamp());
                if (transitions.Count == 0) { transition = default(JumpInputTransition); return false; }
                transition = transitions.Dequeue();
                return true;
            }
        }
        public void Reset() { lock (sync) transitions.Clear(); }
        private static Source NewSource(int id, bool ready)
        { return new Source { Ready = ready, Name = id == 0 ? "keyboard" : "xinput:" + (id - 1) }; }

        public void LogHealth(long now)
        {
            if (now < nextHealthLog) return;
            nextHealthLog = now + Stopwatch.Frequency * 5;
            lock (sync)
            {
                foreach (Source source in physical) WriteHealth(source);
                foreach (Source source in legacyStates.Values) WriteHealth(source);
            }
        }
        private static void WriteHealth(Source s)
        {
            if (s.Reads == 0) return;
            InputLog.Write("input health source=" + s.Name + " reads=" + s.Reads
                + " ready=" + s.Ready + " down=" + s.Down + " losses=" + s.Losses
                + " maxObservationGapMs=" + (s.MaximumGap * 1000.0 / Stopwatch.Frequency).ToString("F2"));
        }
        public void Dispose()
        {
            running = requestedEnabled = samplerAvailable = false;
            RefreshKeyboard();
            if (directInput != null) directInput.Dispose();
            lock (sync) { configurationGeneration++; transitions.Clear(); observedPressedControls.Clear(); Monitor.PulseAll(sync); }
            // No wait and no driver disposal on the caller. Workers own eventual
            // cleanup; a broken native driver cannot hang unload or rebind.
        }

        private void Run(int id)
        {
            uint timer = 1;
            try
            {
                while (running)
                {
                    lock (sync)
                    {
                        while (running && !WantsSource(id))
                        {
                            if (timer == 0) { TimeEndPeriod(1); timer = 1; }
                            Monitor.Wait(sync);
                        }
                        if (!running) break;
                    }
                    if (timer != 0) timer = TimeBeginPeriod(1);
                    try { PollSource(id); }
                    catch (Exception error)
                    {
                        InputLog.Write("input worker error source=" + id + " type=" + error.GetType().Name);
                        Thread.Sleep(500);
                    }
                    Thread.Sleep(1);
                }
            }
            finally
            {
                if (timer == 0) TimeEndPeriod(1);
                lock (sync) sourceWorkers[id] = false;
                EnsureWorkers();
            }
        }
        public void PollSource(int id)
        {
            if (id == 0 && sharedKeyboard) { PumpKeyboard(); return; }
            long generation;
            int[][] keys;
            XInputJumpBinding binding = null;
            IntPtr handle;
            lock (sync)
            {
                if (!requestedEnabled) return;
                generation = configurationGeneration;
                keys = keyboardJumpAlternatives;
                handle = window;
                if (id != 0)
                    foreach (XInputJumpBinding candidate in xinputJumpBindings)
                        if (candidate.UserIndex == id - 1) { binding = candidate; break; }
            }
            // All OS/driver calls are below, outside sync.
            lock (ReadGate)
            {
                if (Reading[id])
                {
                    ObservePhysical(generation, id, false, false, Stopwatch.GetTimestamp());
                    return;
                }
                Reading[id] = true;
            }
            try
            {
                bool down = false, reliable = true;
                if (id == 0)
                {
                    if (keys.Length == 0) return;
                    reliable = handle == IntPtr.Zero || GetForegroundWindow() == handle;
                    if (reliable)
                        foreach (int[] chord in keys)
                        {
                            bool held = chord.Length > 0;
                            foreach (int key in chord) if ((readKey(MouseButtons.ToVirtualKey(key)) & 0x8000) == 0) { held = false; break; }
                            if (held) { down = true; break; }
                        }
                }
                else
                {
                    if (binding == null) return;
                    if (probeGeneration[id] == generation && Stopwatch.GetTimestamp() < nextProbe[id]) return;
                    probeGeneration[id] = generation;
                    Func<int, GamePadState> reader = injectedXbox;
                    if (reader == null)
                    {
                        if (xboxReaders[id - 1] == null)
                        {
                            Func<GamePadState> native = NativeXInputReader.Create(id - 1);
                            xboxReaders[id - 1] = delegate(int unused) { return native(); };
                        }
                        reader = xboxReaders[id - 1];
                    }
                    GamePadState state = reader(id - 1);
                    reliable = state.IsConnected;
                    nextProbe[id] = reliable ? 0 : Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
                    down = reliable && IsXInputBindingDown(state, binding);
                }
                ObservePhysical(generation, id, down, reliable, Stopwatch.GetTimestamp());
            }
            catch
            {
                // A failing read from an old binding epoch cannot invalidate
                // its replacement's newly armed input stream.
                ObservePhysical(generation, id, false, false, Stopwatch.GetTimestamp());
                throw;
            }
            finally { lock (ReadGate) Reading[id] = false; }
        }

        // Test events use the same per-source health/OR/queue implementation as
        // production. Only the actual device read is replaced.
        public void ObserveKey(int key, bool down, long timestamp)
        { ObserveControl(0, key, down, timestamp); }
        public void ObserveXInputButton(int slot, int button, bool down, long timestamp)
        { ObserveControl(slot + 1, button, down, timestamp); }
        private void ObserveControl(int id, int button, bool down, long timestamp)
        {
            lock (sync)
            {
                if (!requestedEnabled) return;
                long key = ((long)id << 32) | (uint)button;
                if (down) observedPressedControls.Add(key); else observedPressedControls.Remove(key);
                int[][] alternatives = id == 0 ? keyboardJumpAlternatives : null;
                if (id != 0) foreach (XInputJumpBinding binding in xinputJumpBindings)
                    if (binding.UserIndex == id - 1) { alternatives = binding.Alternatives; break; }
                if (alternatives == null) return;
                bool held = false;
                foreach (int[] chord in alternatives)
                {
                    bool match = chord.Length != 0;
                    foreach (int part in chord)
                        if (!observedPressedControls.Contains(((long)id << 32) | (uint)part)) { match = false; break; }
                    if (match) { held = true; break; }
                }
                // This event-only helper assumes continuous healthy sampling;
                // injected PollSource tests exercise actual read gaps/failures.
                physical[id].Timestamp = timestamp;
                Accept(physical[id], held, true, timestamp);
            }
        }

        public static bool IsXInputBindingDown(GamePadState state, XInputJumpBinding binding)
        {
            if (binding == null) return false;
            foreach (int[] chord in binding.Alternatives)
            {
                bool held = chord.Length > 0;
                foreach (int button in chord) if (!state.IsButtonDown((Buttons)button)) { held = false; break; }
                if (held) return true;
            }
            return false;
        }
        private static int[][] ToAlternatives(int[] buttons)
        {
            if (buttons == null) return new int[0][];
            int[][] result = new int[buttons.Length][];
            for (int i = 0; i < buttons.Length; i++) result[i] = new[] { buttons[i] };
            return result;
        }
        private static int[][] Clone(int[][] alternatives)
        {
            if (alternatives == null) return new int[0][];
            int[][] result = new int[alternatives.Length][];
            for (int i = 0; i < alternatives.Length; i++) result[i] = alternatives[i] == null ? new int[0] : (int[])alternatives[i].Clone();
            return result;
        }
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")] private static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")] private static extern uint TimeEndPeriod(uint ms);
    }
}
