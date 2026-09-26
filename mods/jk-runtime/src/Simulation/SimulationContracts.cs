using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace JKRuntime.Simulation
{
    public enum SimulationPhase { BeforeBody, BeforeWind, BeforeX, AfterX, AfterY, AfterGravity, Controls, World }

    public struct SimulationInput
    {
        public readonly int Direction;
        public readonly bool Jump;
        public readonly uint ExtraButtons;
        public SimulationInput(int direction, bool jump, uint extraButtons = 0)
        {
            if (direction < -1 || direction > 1) throw new ArgumentOutOfRangeException("direction");
            Direction = direction; Jump = jump; ExtraButtons = extraButtons;
        }
    }

    // Presentation/search data only. Providers must keep every other relevant
    // physics, controller, world and RNG field in their serialized state.
    public struct SimulationPose
    {
        public Vector2 Position, Velocity;
        public int Screen, Width, Height;
        public bool Grounded, StableLanding;
        internal void Validate()
        {
            if (Width <= 0 || Height <= 0 || Screen < 0 || !Finite(Position.X) || !Finite(Position.Y)
                || !Finite(Velocity.X) || !Finite(Velocity.Y) || (StableLanding && !Grounded))
                throw new InvalidOperationException("Invalid simulation pose");
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    public sealed class SimulationEvent
    {
        public string Kind { get; private set; }
        public Vector2 Position { get; private set; }
        public float Value { get; private set; }
        public SimulationEvent(string kind, Vector2 position, float value = 0)
        {
            if (string.IsNullOrWhiteSpace(kind) || kind.Length > 256 || float.IsNaN(value) || float.IsInfinity(value)
                || float.IsNaN(position.X) || float.IsInfinity(position.X) || float.IsNaN(position.Y) || float.IsInfinity(position.Y))
                throw new ArgumentException("Invalid simulation event");
            Kind = kind; Position = position; Value = value;
        }
    }

    public sealed class SimulationRequirement
    {
        public string Id { get; private set; }
        public Version Version { get; private set; }
        public SimulationRequirement(string id, Version version)
        {
            if (string.IsNullOrWhiteSpace(id) || version == null) throw new ArgumentException("Mechanic id/version required");
            Id = id; Version = version;
        }
        internal string Key { get { return Id + "@" + Version; } }
    }

    public sealed class SimulationSeed
    {
        private readonly byte[] world;
        private readonly SimulationRequirement[] requirements;
        public SimulationPose Pose { get; private set; }
        public long Tick { get; private set; }
        public double TickSeconds { get; private set; }
        public string Fingerprint { get; private set; }
        public byte[] World { get { return (byte[])world.Clone(); } }
        public SimulationRequirement[] Requirements { get { return (SimulationRequirement[])requirements.Clone(); } }
        public SimulationSeed(SimulationPose pose, long tick, double tickSeconds, string fingerprint,
            byte[] worldData, IEnumerable<SimulationRequirement> required)
        {
            pose.Validate();
            if (tick < 0 || tickSeconds <= 0 || double.IsNaN(tickSeconds) || double.IsInfinity(tickSeconds)
                || string.IsNullOrWhiteSpace(fingerprint) || worldData == null || worldData.Length > SimulationSession.MaxStateBytes)
                throw new ArgumentException("Invalid simulation seed");
            Pose = pose; Tick = tick; TickSeconds = tickSeconds; Fingerprint = fingerprint;
            world = (byte[])worldData.Clone();
            requirements = (required ?? new SimulationRequirement[0]).ToArray();
            if (requirements.Any(r => r == null) || requirements.Select(r => r.Id).Distinct().Count() != requirements.Length)
                throw new ArgumentException("Duplicate/null simulation requirement");
        }
    }

    // Capture runs only on explicit Open. Advance is a trusted pure adapter,
    // not an arbitrary live mod callback. Registration does not grant coverage.
    public sealed class SimulationProvider
    {
        private readonly SimulationRequirement[] claims;
        private readonly string[] after;
        private readonly SimulationPhase[] phases;
        public string Id { get; private set; }
        public string Evidence { get; private set; }
        public SimulationRequirement[] Claims { get { return (SimulationRequirement[])claims.Clone(); } }
        public string[] After { get { return (string[])after.Clone(); } }
        public SimulationPhase[] Phases { get { return (SimulationPhase[])phases.Clone(); } }
        internal readonly Func<SimulationSeed, byte[]> Capture;
        internal readonly Action<SimulationTick> Advance;
        public SimulationProvider(string id, string evidence, IEnumerable<SimulationRequirement> coverage,
            IEnumerable<SimulationPhase> stages, Func<SimulationSeed, byte[]> capture,
            Action<SimulationTick> advance, IEnumerable<string> dependencies = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(evidence) || capture == null || advance == null)
                throw new ArgumentException("Provider identity, evidence and callbacks required");
            Id = id; Evidence = evidence; Capture = capture; Advance = advance;
            claims = (coverage ?? new SimulationRequirement[0]).ToArray();
            phases = (stages ?? new SimulationPhase[0]).Distinct().OrderBy(p => p).ToArray();
            after = (dependencies ?? new string[0]).Distinct().ToArray();
            if (claims.Length == 0 || claims.Any(c => c == null) || claims.Select(c => c.Id).Distinct().Count() != claims.Length
                || phases.Length == 0 || phases.Any(p => !Enum.IsDefined(typeof(SimulationPhase), p))
                || after.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Invalid provider coverage/order");
        }
    }

    public sealed class SimulationTick
    {
        private Dictionary<string, byte[]> states;
        private List<SimulationEvent> events;
        private Dictionary<string, object> scratch;
        private readonly string owner;
        private bool active = true;
        private SimulationPose pose;
        public SimulationPhase Phase { get; private set; }
        public SimulationInput Input { get; private set; }
        public SimulationSeed Seed { get; private set; }
        public long Tick { get; private set; }
        public SimulationPose Pose { get { Check(); return pose; } set { Check(); value.Validate(); pose = value; } }
        public byte[] State { get { return Read(owner); } set { Check(); states[owner] = SimulationSession.Copy(value); } }
        internal SimulationTick(string id, SimulationPhase phase, SimulationInput input, SimulationSeed seed,
            long tick, SimulationPose value, Dictionary<string, byte[]> data, List<SimulationEvent> output, Dictionary<string, object> temporary)
        { owner = id; Phase = phase; Input = input; Seed = seed; Tick = tick; pose = value; states = data; events = output; scratch = temporary; }
        // Fresh per Step; never part of a snapshot. Use only for newly-created
        // branch objects reconstructed from serialized state, never live objects.
        public void SetLocal(string name, object value) { Check(); scratch[owner + "/" + name] = value; }
        public T GetLocal<T>(string providerId, string name) { Check(); return (T)scratch[providerId + "/" + name]; }
        public byte[] Read(string providerId) { Check(); return SimulationSession.Copy(states[providerId]); }
        public void Emit(SimulationEvent value)
        {
            Check(); if (value == null) throw new ArgumentNullException("value");
            if (events.Count >= 256) throw new InvalidOperationException("Simulation event budget exceeded");
            events.Add(value);
        }
        internal SimulationPose Close() { active = false; states = null; events = null; scratch = null; Seed = null; return pose; }
        private void Check() { if (!active) throw new InvalidOperationException("Simulation tick callback has ended"); }
    }
}
