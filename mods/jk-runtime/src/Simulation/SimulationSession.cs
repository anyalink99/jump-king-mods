using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace JKRuntime.Simulation
{
    public sealed class SimulationSnapshot
    {
        internal readonly SimulationSession Owner;
        internal readonly Dictionary<string, byte[]> Data;
        public SimulationPose Pose { get; private set; }
        public long Tick { get; private set; }
        // Full byte identity: no lossy position buckets or hash collisions.
        public string Key { get; private set; }
        internal SimulationSnapshot(SimulationSession owner, SimulationPose pose, long tick, Dictionary<string, byte[]> data)
        {
            Owner = owner; Pose = pose; Tick = tick; Data = data;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(tick); writer.Write(pose.Position.X); writer.Write(pose.Position.Y);
                writer.Write(pose.Velocity.X); writer.Write(pose.Velocity.Y); writer.Write(pose.Screen);
                writer.Write(pose.Width); writer.Write(pose.Height); writer.Write(pose.Grounded); writer.Write(pose.StableLanding);
                foreach (var pair in data.OrderBy(p => p.Key, StringComparer.Ordinal))
                { writer.Write(pair.Key); writer.Write(pair.Value.Length); writer.Write(pair.Value); }
                Key = Convert.ToBase64String(stream.ToArray());
            }
        }
        public byte[] Read(string providerId) { return SimulationSession.Copy(Data[providerId]); }
    }

    public sealed class SimulationStepResult
    {
        private readonly SimulationEvent[] events;
        public SimulationSnapshot State { get; private set; }
        public SimulationEvent[] Events { get { return (SimulationEvent[])events.Clone(); } }
        internal SimulationStepResult(SimulationSnapshot state, List<SimulationEvent> output)
        { State = state; events = output.ToArray(); }
    }

    public sealed class SimulationSession : IDisposable
    {
        public const int MaxStateBytes = 1024 * 1024;
        private SimulationSeed seed;
        private sealed class Stage
        { internal SimulationProvider Provider; internal SimulationPhase Phase; }
        private Stage[] stages;
        private Action<SimulationSession> release;
        private bool disposed, advancing;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        public SimulationSnapshot Initial { get; private set; }
        internal SimulationSession(SimulationSeed value, SimulationProvider[] order, Action<SimulationSession> onRelease)
        {
            seed = value; release = onRelease;
            stages = Enum.GetValues(typeof(SimulationPhase)).Cast<SimulationPhase>()
                .SelectMany(phase => order.Where(p => p.Phases.Contains(phase)).Select(p => new Stage { Provider = p, Phase = phase })).ToArray();
            var data = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var provider in order) data.Add(provider.Id, Copy(provider.Capture(seed)));
            CheckSize(data); Initial = new SimulationSnapshot(this, seed.Pose, seed.Tick, data);
        }
        /// <summary>Advance one copied branch. Cancellation leaves the parent unchanged; provider failure closes the session.</summary>
        public SimulationStepResult Step(SimulationSnapshot source, SimulationInput input, CancellationToken cancellation = default(CancellationToken))
        {
            Check(); if (advancing) throw new InvalidOperationException("Recursive simulation step");
            if (source == null || source.Owner != this) throw new ArgumentException("Snapshot belongs to another session");
            cancellation.ThrowIfCancellationRequested(); advancing = true;
            try
            {
                var data = source.Data.ToDictionary(p => p.Key, p => Copy(p.Value), StringComparer.Ordinal);
                var events = new List<SimulationEvent>(); var pose = source.Pose;
                var temporary = new Dictionary<string, object>(StringComparer.Ordinal);
                long tick = checked(source.Tick + 1);
                foreach (var stage in stages)
                {
                    var provider = stage.Provider;
                    cancellation.ThrowIfCancellationRequested();
                    var frame = new SimulationTick(provider.Id, stage.Phase, input, seed, tick, pose, data, events, temporary);
                    try { provider.Advance(frame); }
                    finally { pose = frame.Close(); }
                    Check(); pose.Validate(); CheckSize(data);
                }
                cancellation.ThrowIfCancellationRequested();
                return new SimulationStepResult(new SimulationSnapshot(this, pose, tick, data), events);
            }
            catch (OperationCanceledException) { throw; }
            catch { Dispose(); throw; }
            finally { advancing = false; }
        }
        internal static byte[] Copy(byte[] value)
        {
            if (value == null || value.Length > MaxStateBytes) throw new InvalidOperationException("Invalid simulation state size");
            return (byte[])value.Clone();
        }
        private static void CheckSize(Dictionary<string, byte[]> states)
        { if (states.Values.Sum(v => (long)v.Length) > MaxStateBytes) throw new InvalidOperationException("Simulation state budget exceeded"); }
        private void Check()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Simulation session is owner-thread only");
            if (disposed) throw new ObjectDisposedException("SimulationSession");
        }
        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Simulation session is owner-thread only");
            if (disposed) return; disposed = true; stages = new Stage[0]; Initial = null; seed = null;
            var callback = release; release = null; if (callback != null) callback(this);
        }
    }
}
