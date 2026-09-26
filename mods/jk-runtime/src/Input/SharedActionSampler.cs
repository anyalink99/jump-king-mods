using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JKRuntime.Input
{
    public interface IHighRateInput : IDisposable
    {
        bool Enabled { get; }
        bool RequestedEnabled { get; }
        bool Available { get; }
        void Start();
        void Configure(int[][] keys, XInputJumpBinding[] xbox, DirectInputJumpBinding[] legacy, IntPtr window, bool enabled);
        bool CanMeasureDirectInput(Guid id);
        bool CanMeasurePhysical(int source);
        bool TryDequeue(out JumpInputTransition transition);
        void Reset();
        void LogHealth(long timestamp);
    }
    /// <summary>One sampler per action, independent bounded subscriber queues, one binding authority.</summary>
    public sealed class SharedActionSampler : IHighRateInput
    {
        private sealed class Stream
        {
            internal HighRateInputSampler Sampler;
            internal SharedActionSampler Authority;
            internal readonly List<SharedActionSampler> Clients = new List<SharedActionSampler>();
        }
        private static readonly Dictionary<string, Stream> streams = new Dictionary<string, Stream>(StringComparer.Ordinal);
        private readonly string action;
        private Stream stream;
        private IDisposable registration;
        private readonly Queue<JumpInputTransition> queue = new Queue<JumpInputTransition>();
        private SharedActionSampler(string id, Stream value) { action = id; stream = value; }
        /// <summary>Join an action stream without historical edges. One binding authority may configure it; Start is explicit and every client owns its queue.</summary>
        public static SharedActionSampler Acquire(string owner, string actionId, bool bindingAuthority = false)
        { return Acquire(owner, actionId, bindingAuthority, () => new HighRateInputSampler()); }
        internal static SharedActionSampler Acquire(string owner, string actionId, bool bindingAuthority, Func<HighRateInputSampler> factory)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner); ModuleDefinition.ValidId(actionId);
            Stream value;
            if (!streams.TryGetValue(actionId, out value)) { value = new Stream { Sampler = factory() }; streams.Add(actionId, value); }
            if (bindingAuthority && value.Authority != null) throw new InvalidOperationException("Physical action bindings already owned: " + actionId);
            Pump(value); // New clients cannot consume somebody else's old edges.
            var result = new SharedActionSampler(actionId, value); value.Clients.Add(result);
            if (bindingAuthority) value.Authority = result;
            result.registration = RuntimeResources.Track(owner, "physical-action:" + actionId, new ActionLease(result.Release));
            return result;
        }
        private void Check() { RuntimeApi.Kernel.CheckThread(); if (stream == null) throw new ObjectDisposedException("SharedActionSampler"); }
        public bool Enabled { get { Check(); return stream.Sampler.Enabled; } }
        public bool RequestedEnabled { get { Check(); return stream.Sampler.RequestedEnabled; } }
        public bool Available { get { Check(); return stream.Sampler.Available; } }
        public void Start() { Check(); stream.Sampler.Start(); }
        public void Configure(int[][] keys, XInputJumpBinding[] xbox, DirectInputJumpBinding[] legacy, IntPtr window, bool enabled)
        {
            Check(); if (stream.Authority != this) throw new InvalidOperationException("Only the action's binding authority can reconfigure it");
            stream.Sampler.Configure(keys, xbox, legacy, window, enabled);
            foreach (var client in stream.Clients) { client.queue.Clear(); client.queue.Enqueue(new JumpInputTransition(Stopwatch.GetTimestamp())); }
            // The authority itself already handles its explicit rebind/reset.
            queue.Clear();
        }
        public bool CanMeasureDirectInput(Guid id) { Check(); return stream.Sampler.CanMeasureDirectInput(id); }
        public bool CanMeasurePhysical(int source) { Check(); return stream.Sampler.CanMeasurePhysical(source); }
        private static void Pump(Stream value)
        {
            JumpInputTransition edge;
            while (value.Sampler.TryDequeue(out edge)) foreach (var client in value.Clients)
            {
                if (client.queue.Count >= HighRateInputSampler.MaximumQueuedEdges)
                { client.queue.Clear(); client.queue.Enqueue(new JumpInputTransition(edge.Timestamp)); }
                else client.queue.Enqueue(edge);
            }
        }
        public bool TryDequeue(out JumpInputTransition edge)
        { Check(); Pump(stream); if (queue.Count == 0) { edge = default(JumpInputTransition); return false; } edge = queue.Dequeue(); return true; }
        /// <summary>Discard this client's pending observations without consuming any other client's queue.</summary>
        public void Reset() { Check(); Pump(stream); queue.Clear(); }
        public void LogHealth(long timestamp) { Check(); stream.Sampler.LogHealth(timestamp); }
        public void Dispose()
        { RuntimeApi.Kernel.CheckThread(); if (registration != null) registration.Dispose(); }
        private void Release()
        {
            RuntimeApi.Kernel.CheckThread(); if (stream == null) return;
            var value = stream; value.Clients.Remove(this); stream = null; queue.Clear();
            if (value.Authority == this)
            {
                value.Authority = null;
                value.Sampler.Configure(new int[0][], new XInputJumpBinding[0], new DirectInputJumpBinding[0], IntPtr.Zero, false);
                foreach (var client in value.Clients) { client.queue.Clear(); client.queue.Enqueue(new JumpInputTransition(Stopwatch.GetTimestamp())); }
            }
            if (value.Clients.Count == 0) { value.Sampler.Dispose(); streams.Remove(action); }
        }
        public static int ActiveStreams { get { RuntimeApi.Kernel.CheckThread(); return streams.Count; } }
    }
}
