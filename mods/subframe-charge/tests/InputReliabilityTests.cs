using JKRuntime.Input;
using System;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SubframeCharge;

internal static class InputReliabilityTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("Input reliability: " + message); }
    private static GamePadState Pad(bool down)
    { return new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, down ? new[] { Buttons.A } : new Buttons[0]); }

    internal static void Run()
    {
        NativeXInputContract();
        SourceHealth();
        BackendParity();
        BlockedXInput();
        BlockedDirectInput();
        QueueBound();
        Console.WriteLine("[OK] Input reliability: native XInput conversion, independent health, blocked REAL workers, rebind/dispose, bounded queue");
    }

    private static void BackendParity()
    {
        foreach (int source in new[] { 0, 1, 2, 3 })
            foreach (double milliseconds in new[] { 1.0, 17.0, 28.0, 68.0, 575.81, 1207.0 })
                using (HighRateInputSampler sampler = new HighRateInputSampler(false, key => 0, slot => Pad(false)))
                {
                    Guid id = Guid.NewGuid();
                    sampler.Configure(new[] { new[] { source == 3 ? MouseButtons.Left : 32 } }, new[] { new XInputJumpBinding(0, new[] { (int)Buttons.A }) },
                        new[] { new DirectInputJumpBinding(id, new[] { new[] { 2 } }) }, IntPtr.Zero, true);
                    long epoch = sampler.ConfigurationGeneration;
                    Action<bool, long> observe = (held, stamp) => {
                        if (source == 3) sampler.ObserveKey(MouseButtons.Left, held, stamp);
                        else if (source == 2) sampler.ObserveDirectInput(epoch, id, held, true, stamp);
                        else sampler.ObservePhysical(epoch, source, held, true, stamp);
                    };
                    long start = Stopwatch.GetTimestamp();
                    observe(false, start - 1); observe(true, start);
                    for (int ms = 1; ms < milliseconds; ms++) observe(true, start + Stopwatch.Frequency * ms / 1000);
                    long end = start + (long)(milliseconds * Stopwatch.Frequency / 1000);
                    observe(false, end);
                    JumpInputTransition down, up;
                    Check(sampler.TryDequeue(out down) && down.Reliable && down.IsDown && down.Timestamp == start,
                        "backend press parity source=" + source);
                    Check(sampler.TryDequeue(out up) && up.Reliable && !up.IsDown && up.Timestamp == end,
                        "backend release parity source=" + source);
                    foreach (float scale in new[] { 1f, .5f })
                        Check(ChargeQuantizer.QuantizeRelease((up.Timestamp - down.Timestamp) / (double)Stopwatch.Frequency, scale).Frames
                            == ChargeQuantizer.QuantizeRelease(milliseconds / 1000, scale).Frames, "backend quantizer parity");
                }
    }

    private static void NativeXInputContract()
    {
        // Own connection construction/delegate compilation against the game's
        // SharpDX library, without assuming any controller is attached.
        Check(NativeXInputReader.Create(0) != null, "own XInput reader contract");
        Assembly dx = typeof(GamePad).GetField("_controllers", BindingFlags.NonPublic | BindingFlags.Static)
            .FieldType.GetElementType().Assembly;
        Type rawType = dx.GetType("SharpDX.XInput.Gamepad", true);
        Func<object, GamePadState> decode = NativeXInputReader.CreateDecoder(rawType);
        object raw = Activator.CreateInstance(rawType);
        foreach (byte value in new byte[] { 0, 29, 30, 31, 254, 255 })
        {
            rawType.GetField("LeftTrigger").SetValue(raw, value);
            rawType.GetField("RightTrigger").SetValue(raw, value);
            GamePadState state = decode(raw);
            Check(state.IsConnected && state.IsButtonDown(Buttons.LeftTrigger) == (value >= 30), "native trigger threshold");
            Check(state.IsButtonDown(Buttons.RightTrigger) == (value >= 30), "native right trigger threshold");
        }
        foreach (string field in new[] { "LeftThumbX", "LeftThumbY", "RightThumbX", "RightThumbY" })
            foreach (short value in new short[] { short.MinValue, -8689, -7849, -1, 0, 1, 7849, 8689, short.MaxValue })
            {
                rawType.GetField(field).SetValue(raw, value);
                GamePadState actual = decode(raw);
                Vector2 left = new Vector2((short)rawType.GetField("LeftThumbX").GetValue(raw), (short)rawType.GetField("LeftThumbY").GetValue(raw)) / 32767f;
                Vector2 right = new Vector2((short)rawType.GetField("RightThumbX").GetValue(raw), (short)rawType.GetField("RightThumbY").GetValue(raw)) / 32767f;
                ConstructorInfo constructor = typeof(GamePadThumbSticks).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(Vector2), typeof(Vector2), typeof(GamePadDeadZone), typeof(GamePadDeadZone) }, null);
                GamePadThumbSticks expected = (GamePadThumbSticks)constructor.Invoke(new object[] { left, right, GamePadDeadZone.IndependentAxes, GamePadDeadZone.IndependentAxes });
                Check(actual.ThumbSticks.Equals(expected), "native thumb-stick conversion " + field);
            }
    }

    private static void SourceHealth()
    {
        Guid id = Guid.NewGuid();
        using (HighRateInputSampler s = new HighRateInputSampler(false, key => 0, slot => Pad(false)))
        {
            s.Configure(new[] { new[] { 32 } }, new[] { new XInputJumpBinding(0, new[] { (int)Buttons.A }) },
                new[] { new DirectInputJumpBinding(id, new[] { new[] { 2 } }) }, IntPtr.Zero, true);
            long generation = s.ConfigurationGeneration;
            long t = Stopwatch.GetTimestamp();
            s.ObservePhysical(generation, 0, false, true, t);
            s.ObserveDirectInput(generation, id, false, true, t);
            s.ObservePhysical(generation, 0, true, true, t + 1);
            JumpInputTransition edge;
            Check(s.TryDequeue(out edge) && edge.Reliable && edge.IsDown, "keyboard down");
            s.ObserveDirectInput(generation, id, false, false, t + 2);
            Check(!s.TryDequeue(out edge), "idle DI fault invalidated keyboard charge");
            s.ObservePhysical(generation, 0, false, true, t + 3);
            Check(s.TryDequeue(out edge) && edge.Reliable && !edge.IsDown, "keyboard release after idle DI fault");
            s.ObservePhysical(generation, 1, false, true, t + 4);
            s.ObservePhysical(generation, 1, true, true, t + 5);
            Check(s.TryDequeue(out edge) && edge.IsDown, "Xbox down");
            s.ObservePhysical(generation, 1, false, false, t + 6);
            Check(s.TryDequeue(out edge) && !edge.Reliable, "Xbox disconnect must invalidate, not synthesize release");
            s.ObservePhysical(generation, 1, true, true, t + 7);
            Check(!s.TryDequeue(out edge), "held reconnect must not invent press");
            s.ObservePhysical(generation, 1, false, true, t + 8);
            s.ObservePhysical(generation, 1, true, true, t + 9);
            Check(s.TryDequeue(out edge) && edge.Reliable && edge.IsDown, "neutral re-arm");
            s.ObservePhysical(generation, 1, false, true, t + HighRateInputSampler.MaximumObservationGap + 10);
            Check(s.TryDequeue(out edge) && !edge.Reliable, "Xbox observation gap must invalidate");
            Check(!s.TryDequeue(out edge), "gap produced fake release");
        }
    }

    private static void BlockedXInput()
    {
        using (ManualResetEvent entered = new ManualResetEvent(false))
        using (ManualResetEvent release = new ManualResetEvent(false))
        {
            short key = 0;
            HighRateInputSampler s = new HighRateInputSampler(false, unused => key, slot => {
                entered.Set(); release.WaitOne(); return Pad(false);
            });
            s.Configure(new[] { 32 }, new[] { new XInputJumpBinding(0, new[] { (int)Buttons.A }) }, true);
            s.PollSource(0); // neutral arms keyboard
            Thread blocked = new Thread(() => s.PollSource(1)) { IsBackground = true };
            blocked.Start();
            try
            {
                Check(entered.WaitOne(2000), "blocked driver did not enter");
                Stopwatch time = Stopwatch.StartNew();
                key = unchecked((short)0x8000);
                s.PollSource(0);
                JumpInputTransition edge;
                Check(s.TryDequeue(out edge) && edge.Reliable && edge.IsDown, "blocked Xbox stalled keyboard");
                s.Configure(new[] { 32 }, true);
                s.Dispose();
                Check(time.ElapsedMilliseconds < 500, "driver lock blocked drain/rebind/dispose");
                int duplicateReads = 0;
                using (HighRateInputSampler replacement = new HighRateInputSampler(false, unused => 0,
                    slot => { duplicateReads++; return Pad(false); }))
                {
                    replacement.Configure(new int[0], new[] { new XInputJumpBinding(0, new[] { (int)Buttons.A }) }, true);
                    replacement.PollSource(1);
                    Check(duplicateReads == 0, "reinstall spawned a second read on an already stuck source");
                }
            }
            finally { release.Set(); Check(blocked.Join(2000), "released worker did not complete"); s.Dispose(); }
        }
    }

    private sealed class Device : IDirectInputDevice
    {
        internal ManualResetEvent Enter, Release;
        internal volatile bool Down, Disposed;
        public int[] ReadButtons()
        {
            if (Enter != null) { Enter.Set(); Release.WaitOne(); }
            return Down ? new[] { 2 } : new int[0];
        }
        public void Dispose() { Disposed = true; }
    }
    private static void BlockedDirectInput()
    {
        using (ManualResetEvent enter = new ManualResetEvent(false))
        using (ManualResetEvent release = new ManualResetEvent(false))
        using (ManualResetEvent neutral = new ManualResetEvent(false))
        using (ManualResetEvent down = new ManualResetEvent(false))
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            Device stuck = new Device { Enter = enter, Release = release }, healthy = new Device();
            DirectInputPoller poller = new DirectInputPoller((id, window) => id == a ? stuck : healthy,
                (epoch, id, held, ready, stamp) => { if (id == b && ready) { if (held) down.Set(); else neutral.Set(); } }, false);
            poller.Configure(new[] { new DirectInputJumpBinding(a, new[] { new[] { 2 } }),
                new DirectInputJumpBinding(b, new[] { new[] { 2 } }) }, IntPtr.Zero, 1);
            try
            {
                Check(enter.WaitOne(2000) && neutral.WaitOne(2000), "one DI driver blocked the other");
                healthy.Down = true;
                Check(down.WaitOne(2000), "independent DI edge missing");
                Stopwatch time = Stopwatch.StartNew();
                poller.Dispose();
                Check(time.ElapsedMilliseconds < 500, "DI dispose waited for stuck driver");
            }
            finally
            {
                poller.Dispose(); release.Set();
                Check(SpinWait.SpinUntil(() => stuck.Disposed && healthy.Disposed, 2000), "owned DI handles not eventually disposed");
            }
        }
    }
    private static void QueueBound()
    {
        using (HighRateInputSampler s = new HighRateInputSampler(true))
        {
            s.Configure(new[] { 32 }, true);
            for (int i = 0; i < 10000; i++) s.ObserveKey(32, i % 2 == 0, i + 1);
            int count = 0; bool invalid = false; JumpInputTransition edge;
            while (s.TryDequeue(out edge)) { count++; invalid |= !edge.Reliable; }
            Check(count <= HighRateInputSampler.MaximumQueuedEdges && invalid, "overflow kept false precision or unbounded backlog");
        }
    }
}
