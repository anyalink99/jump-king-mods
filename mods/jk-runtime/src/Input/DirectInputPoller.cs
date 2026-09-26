using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace JKRuntime.Input
{
    public sealed class DirectInputJumpBinding
    {
        public readonly Guid DeviceId;
        public readonly int[][] Alternatives;
        public DirectInputJumpBinding(Guid id, int[][] alternatives)
        {
            DeviceId = id;
            Alternatives = new int[alternatives.Length][];
            for (int i = 0; i < alternatives.Length; i++) Alternatives[i] = (int[])alternatives[i].Clone();
        }
        public bool IsDown(int[] buttons)
        {
            foreach (int[] chord in Alternatives)
            {
                bool held = chord.Length != 0;
                foreach (int button in chord) held &= Array.IndexOf(buttons, button) >= 0;
                if (held) return true;
            }
            return false;
        }
    }

    // Driver calls are isolated from both the game thread AND keyboard/XInput
    // polling. A blocked/removed legacy device must not stall those paths.
    public sealed class DirectInputPoller : IDisposable
    {
        private sealed class Device
        {
            public DirectInputJumpBinding Binding;
            public IDirectInputDevice Handle;
            public bool Ready;
            public bool Down;
            public long RetryAt;
            public string Error;
            public long LastRead;
        }
        private readonly object sync = new object();
        private readonly Func<Guid, IntPtr, IDirectInputDevice> open;
        private readonly Action<long, Guid, bool, bool, long> publish;
        private readonly bool manual;
        private readonly List<Device> devices = new List<Device>(); // worker only
        private DirectInputJumpBinding[] requested = new DirectInputJumpBinding[0];
        private IntPtr window;
        private IntPtr workerWindow;
        private long generation;
        private long workerGeneration = -1;
        private Thread thread;
        private volatile bool running = true;
        private readonly bool isolated;
        private readonly List<DirectInputPoller> children = new List<DirectInputPoller>();

        public DirectInputPoller(Func<Guid, IntPtr, IDirectInputDevice> factory,
            Action<long, Guid, bool, bool, long> callback, bool manualPolling)
            : this(factory, callback, manualPolling, false) { }
        private DirectInputPoller(Func<Guid, IntPtr, IDirectInputDevice> factory,
            Action<long, Guid, bool, bool, long> callback, bool manualPolling, bool ownDevice)
        { open = factory; publish = callback; manual = manualPolling; isolated = ownDevice; }

        public void Configure(DirectInputJumpBinding[] bindings, IntPtr handle, long version)
        {
            if (!manual && !isolated)
            {
                // One worker/owned handle per GUID. Never wait for a legacy
                // driver during reconfiguration; old callbacks carry old epochs.
                lock (sync)
                {
                    foreach (DirectInputPoller child in children) child.Dispose();
                    children.Clear();
                    if (!running) return;
                    foreach (DirectInputJumpBinding binding in bindings)
                    {
                        DirectInputPoller child = new DirectInputPoller(open, publish, false, true);
                        children.Add(child);
                        child.Configure(new[] { binding }, handle, version);
                    }
                }
                return;
            }
            lock (sync)
            {
                requested = bindings;
                window = handle;
                generation = version;
                if (!manual && thread == null && bindings.Length > 0)
                {
                    thread = new Thread(Run) { IsBackground = true, Name = "Subframe Charge DirectInput" };
                    thread.Start();
                }
            }
        }

        private void Run()
        {
            try
            {
                while (running) { PollOnce(Stopwatch.GetTimestamp()); Thread.Sleep(1); }
            }
            catch (Exception error)
            {
                InputLog.Write("directinput worker stopped error=" + error.GetType().Name);
                foreach (Device device in devices) publish(workerGeneration, device.Binding.DeviceId, false, false, Stopwatch.GetTimestamp());
            }
            finally { CloseDevices(); }
        }

        public void PollOnce(long now)
        {
            DirectInputJumpBinding[] changed = null;
            lock (sync)
            {
                if (workerGeneration != generation)
                {
                    workerGeneration = generation;
                    workerWindow = window;
                    changed = requested;
                }
            }
            if (changed != null)
            {
                CloseDevices();
                foreach (DirectInputJumpBinding binding in changed) devices.Add(new Device { Binding = binding });
            }
            foreach (Device device in devices)
            {
                if (now < device.RetryAt) continue;
                try
                {
                    bool opened = device.Handle == null;
                    if (opened) device.Handle = open(device.Binding.DeviceId, workerWindow);
                    IDirectInputChargeDevice bound = device.Handle as IDirectInputChargeDevice;
                    bool down = bound != null ? bound.ReadJump(device.Binding)
                        : device.Binding.IsDown(device.Handle.ReadButtons());
                    long timestamp = manual ? now : Stopwatch.GetTimestamp();
                    if (device.Ready && timestamp - device.LastRead > Stopwatch.Frequency / 20)
                        throw new TimeoutException("DirectInput observation gap exceeded 50 ms");
                    device.LastRead = timestamp;
                    // A held control after (re)open has no trustworthy down edge.
                    // Wait for neutral; never invent a press on acquisition.
                    bool ready = device.Ready || !down;
                    if (opened) InputLog.Write("directinput opened id=" + device.Binding.DeviceId
                        + " waitingForNeutral=" + down + " ownConnection=True");
                    if (ready != device.Ready || (ready && down != device.Down))
                    {
                        InputLog.Write("directinput sample id=" + device.Binding.DeviceId
                            + " ready=" + ready + " down=" + down + " generation=" + workerGeneration);
                    }
                    publish(workerGeneration, device.Binding.DeviceId, down, ready, timestamp);
                    device.Ready = ready;
                    device.Down = down;
                    device.Error = null;
                }
                catch (Exception error)
                {
                    Exception actual = error.GetBaseException();
                    string reason = actual.GetType().Name + ": " + actual.Message.Replace('\r', ' ').Replace('\n', ' ');
                    if (reason != device.Error)
                        InputLog.Write("directinput unavailable id=" + device.Binding.DeviceId + " error=" + reason);
                    device.Error = reason;
                    publish(workerGeneration, device.Binding.DeviceId, false, false, manual ? now : Stopwatch.GetTimestamp());
                    device.Ready = device.Down = false;
                    Close(device);
                    device.RetryAt = now + Stopwatch.Frequency / 2;
                }
            }
        }

        private static void Close(Device device)
        {
            if (device.Handle != null)
            {
                try { device.Handle.Dispose(); } catch { }
                device.Handle = null;
            }
        }
        private void CloseDevices() { foreach (Device device in devices) Close(device); devices.Clear(); }
        public void Dispose()
        {
            running = false;
            if (manual) CloseDevices();
            lock (sync)
            {
                foreach (DirectInputPoller child in children) child.Dispose();
                children.Clear();
            }
            // Worker owns eventual cleanup. No Join: a stuck driver's lifetime
            // must not determine game-thread latency, including shutdown.
        }
    }
}
