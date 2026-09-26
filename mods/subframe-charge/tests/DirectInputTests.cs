using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.Controller;
using SubframeCharge;

internal static class DirectInputTests
{
    private sealed class FakeDevice : IDirectInputDevice
    {
        internal int[] Buttons = new int[0];
        internal bool Fail;
        internal bool Disposed;
        public int[] ReadButtons() { if (Fail) throw new InvalidOperationException("test disconnect"); return Buttons; }
        public void Dispose() { Disposed = true; }
    }

    internal static void Run(string gameDirectory)
    {
        Guid id = new Guid("184ff590-b295-11f0-8002-444553540000");
        DirectInputJumpBinding binding = new DirectInputJumpBinding(id, new[] { new[] { 2, 3 }, new[] { 10000 } });
        Require(!binding.IsDown(new[] { 2 }), "partial chord");
        Require(binding.IsDown(new[] { 2, 3 }), "physical chord");
        Require(binding.IsDown(new[] { 10000 }), "POV alternative");

        using (HighRateInputSampler sampler = new HighRateInputSampler(true))
        {
            sampler.Start();
            sampler.Configure(new[] { new[] { 32 } }, new[] { new XInputJumpBinding(0, new[] { 4096 }) },
                new[] { binding }, IntPtr.Zero, true);
            long epoch = sampler.ConfigurationGeneration;
            FakeDevice device = new FakeDevice { Buttons = new[] { 2, 3 } };
            int opens = 0;
            using (DirectInputPoller poller = new DirectInputPoller((guid, window) => { opens++; return device; }, sampler.ObserveDirectInput, true))
            {
                poller.Configure(new[] { binding }, IntPtr.Zero, epoch);
                long now = Stopwatch.GetTimestamp();
                long ms = Math.Max(1, Stopwatch.Frequency / 1000);
                poller.PollOnce(now);
                Require(!sampler.CanMeasureDirectInput(id), "initial held device must wait for neutral");
                Empty(sampler, "no synthetic acquisition press");
                device.Buttons = new int[0];
                poller.PollOnce(now + ms);
                Require(sampler.CanMeasureDirectInput(id), "neutral enables DI sampling");
                Empty(sampler, "neutral acquisition is not release");
                device.Buttons = new[] { 2, 3 };
                poller.PollOnce(now + 2 * ms);
                Edge(sampler, true, now + 2 * ms);
                sampler.ObserveKey(32, true, now + 3 * ms);
                device.Buttons = new int[0];
                poller.PollOnce(now + 4 * ms);
                Empty(sampler, "keyboard keeps combined jump down after DI release");
                sampler.ObserveXInputButton(0, 4096, true, now + 5 * ms);
                sampler.ObserveKey(32, false, now + 6 * ms);
                Empty(sampler, "Xbox keeps combined jump down after keyboard release");
                sampler.ObserveXInputButton(0, 4096, false, now + 7 * ms);
                Edge(sampler, false, now + 7 * ms);

                device.Buttons = new[] { 10000 };
                poller.PollOnce(now + 8 * ms);
                Edge(sampler, true, now + 8 * ms);
                device.Fail = true;
                poller.PollOnce(now + 9 * ms);
                JumpInputTransition invalid;
                Require(sampler.TryDequeue(out invalid) && !invalid.Reliable, "driver failure invalidates timing, not a fake release");
                Empty(sampler, "no release synthesized by disconnect");
                Require(!sampler.CanMeasureDirectInput(id) && device.Disposed, "disconnect disposes own handle and loses support");
                sampler.ObserveKey(32, true, now + 10 * ms);
                Edge(sampler, true, now + 10 * ms);
                sampler.ObserveKey(32, false, now + 11 * ms);
                Edge(sampler, false, now + 11 * ms);
                poller.PollOnce(now + 20 * ms);
                Require(opens == 1, "failed opens/read retry throttled");
                device = new FakeDevice { Buttons = new[] { 10000 } };
                now += Stopwatch.Frequency;
                poller.PollOnce(now);
                Require(opens == 2 && !sampler.CanMeasureDirectInput(id), "reconnect held is not measured");
                Empty(sampler, "no reconnect press");
                device.Buttons = new int[0];
                poller.PollOnce(now + ms);
                device.Buttons = new[] { 10000 };
                poller.PollOnce(now + 2 * ms);
                Edge(sampler, true, now + 2 * ms);
                // A blocked driver cannot silently join observations over a gap.
                poller.PollOnce(now + Stopwatch.Frequency / 10);
                Require(sampler.TryDequeue(out invalid) && !invalid.Reliable, "long observation gap invalidates charge");
                sampler.Configure(new[] { new[] { 32 } }, new XInputJumpBinding[0], new DirectInputJumpBinding[0], IntPtr.Zero, true);
                sampler.ObserveDirectInput(epoch, id, true, true, now);
                Empty(sampler, "old generation callback ignored after rebind");
                poller.Configure(new DirectInputJumpBinding[0], IntPtr.Zero, sampler.ConfigurationGeneration);
                poller.PollOnce(now + Stopwatch.Frequency);
                Require(device.Disposed, "remove binding closes worker-owned connection");
            }
        }
        NativeDecoderContract(gameDirectory);
        Console.WriteLine("[OK] DirectInput: chords, OR aggregation, timestamps, loss/retry/rebind, native decoder contract");
    }

    private static void NativeDecoderContract(string gameDirectory)
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Assembly slimDX = Assembly.Load("SlimDX");
        Type stateType = slimDX.GetType("SlimDX.DirectInput.JoystickState", true);
        Type nativeSlim = typeof(IPad).Assembly.GetType("JumpKing.Controller.Slim.SlimPad", true);
        Type nativeState = typeof(IPad).Assembly.GetType("JumpKing.Controller.Slim.SlimPadState", true);
        object slim = FormatterServices.GetUninitializedObject(nativeSlim);
        object unopenedJoystick = FormatterServices.GetUninitializedObject(slimDX.GetType("SlimDX.DirectInput.Joystick", true));
        Require(DirectInputDevice.CreateUpdate(unopenedJoystick, slim) != null,
            "checked GetCurrentState/ref result and native decoder delegate compile against installed SlimDX");
        nativeSlim.GetField("_connected", Fields).SetValue(slim, true);
        IPad pad = (IPad)Activator.CreateInstance(typeof(IPad).Assembly.GetType("JumpKing.Controller.LegacyPad", true), new[] { slim });
        ConstructorInfo convert = nativeState.GetConstructor(Fields, null, new[] { stateType }, null);
        object state = Activator.CreateInstance(stateType);
        stateType.GetField("pressedButtons", Fields).SetValue(state, new bool[] { false, true });
        stateType.GetField("povs", Fields).SetValue(state, new[] { -1 });
        int reads = 0;
        DirectInputDevice backend = (DirectInputDevice)FormatterServices.GetUninitializedObject(typeof(DirectInputDevice));
        typeof(DirectInputDevice).GetField("decoder", Fields).SetValue(backend, pad);
        typeof(DirectInputDevice).GetField("poll", Fields).SetValue(backend, (Func<bool>)(() => true));
        typeof(DirectInputDevice).GetField("update", Fields).SetValue(backend, (Action)(() =>
        {
            reads++;
            nativeSlim.GetField("m_current_state", Fields).SetValue(slim, convert.Invoke(new[] { state }));
        }));
        Require(Array.IndexOf(backend.ReadButtons(), 2) >= 0, "Button 2 uses native 1-based numbering");
        stateType.GetField("pressedButtons", Fields).SetValue(state, new bool[] { false, false });
        Require(Array.IndexOf(backend.ReadButtons(), 2) < 0 && reads == 2, "every backend read refreshes its own cached decoder");
        string[] axes = { "x", "y", "z", "rx", "ry", "rz" };
        for (int axis = 0; axis < axes.Length; axis++)
        {
            foreach (int value in new[] { -100, -99, -26, -25, 0, 25, 26, 99, 100 })
            {
                stateType.GetField(axes[axis], Fields).SetValue(state, value);
                int code = 1000 + axis * 2 + (value < 0 ? 1 : 0);
                // Installed SlimPadState divides INT axis values by 100.
                Require((Array.IndexOf(backend.ReadButtons(), code) >= 0) == (Math.Abs(value / 100) > 0.25), "exact native axis quantization");
            }
            stateType.GetField(axes[axis], Fields).SetValue(state, 0);
        }
        foreach (int angle in new[] { -1, 0, 4500, 9000, 13500, 18000, 22500, 27000, 31500 })
        {
            stateType.GetField("povs", Fields).SetValue(state, new[] { -1, angle });
            int[] buttons = backend.ReadButtons();
            bool[] expected = { angle >= 0 && (angle > 27000 || angle < 9000), angle > 9000 && angle < 27000,
                angle > 18000, angle > 0 && angle < 18000 };
            for (int direction = 0; direction < 4; direction++)
                Require((Array.IndexOf(buttons, 10004 + direction) >= 0) == expected[direction], "native POV numbering/diagonals");
        }
        // Compare the allocation-free bound-control path to the installed
        // native decoder, not to a duplicate expected-value formula.
        List<int[]> alternatives = new List<int[]>();
        for (int code = 1; code <= 8; code++) alternatives.Add(new[] { code });
        for (int code = 1000; code < 1012; code++) alternatives.Add(new[] { code });
        for (int code = 10000; code < 10012; code++) alternatives.Add(new[] { code });
        alternatives.Add(new[] { 2, 1000, 10004 });
        alternatives.Add(new[] { 999, 1000 });
        List<Func<bool>> matchers = new List<Func<bool>>();
        foreach (int[] chord in alternatives)
            matchers.Add(DirectInputDevice.CreateMatcher(state,
                new DirectInputJumpBinding(Guid.Empty, new[] { chord })));
        Random random = new Random(1729);
        for (int sample = 0; sample < 2000; sample++)
        {
            bool[] keys = new bool[8];
            for (int i = 0; i < keys.Length; i++) keys[i] = random.Next(2) == 0;
            stateType.GetField("pressedButtons", Fields).SetValue(state, keys);
            stateType.GetField("povs", Fields).SetValue(state, new[] { random.Next(-1, 36001), random.Next(-1, 36001) });
            foreach (string axis in axes) stateType.GetField(axis, Fields).SetValue(state, random.Next(-150, 151));
            int[] nativeButtons = backend.ReadButtons();
            for (int i = 0; i < alternatives.Count; i++)
            {
                bool expected = true;
                foreach (int code in alternatives[i]) expected &= Array.IndexOf(nativeButtons, code) >= 0;
                Require(matchers[i]() == expected, "fast bound matcher differs from installed LegacyPad");
            }
        }
        Stopwatch cost = Stopwatch.StartNew();
        int collections = GC.CollectionCount(0);
        for (int i = 0; i < 100000; i++) matchers[0]();
        cost.Stop();
        Console.WriteLine("[OK] DI fast matcher: 2000 native-state comparisons; 100000 evaluations="
            + cost.Elapsed.TotalMilliseconds.ToString("F2") + " ms, process Gen0 delta=" + (GC.CollectionCount(0) - collections)
            + " (synthetic decoder benchmark, not hardware polling)");
    }

    private static void Edge(HighRateInputSampler sampler, bool down, long timestamp)
    {
        JumpInputTransition edge;
        Require(sampler.TryDequeue(out edge) && edge.Reliable && edge.IsDown == down && edge.Timestamp == timestamp, "expected exact aggregated edge");
    }
    private static void Empty(HighRateInputSampler sampler, string reason)
    { JumpInputTransition edge; Require(!sampler.TryDequeue(out edge), reason); }
    private static void Require(bool condition, string reason) { if (!condition) throw new Exception("DirectInput: " + reason); }
}
