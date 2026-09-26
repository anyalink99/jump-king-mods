using System;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.State;

namespace RuntimeExamples
{
    [RuntimeModule("example.state", "State Example")]
    public static class StateExample
    {
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            // A fresh state owner for each activation; no static previous-player data.
            var state = new ExampleState();
            context.Track(GameState.Snapshots.Register(state));
        }
    }

    public sealed class ExampleState : IStateParticipant
    {
        private int[] charges = { 1, 2 };
        public string Id { get { return "example.state.charges"; } }
        public int Version { get { return 1; } }
        public int ReadCharge(int index) { return charges[index]; }
        public void SetCharge(int index, int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException("value");
            charges[index] = value;
        }
        public object Capture() { return new Payload((int[])charges.Clone()); }
        public void Validate(object snapshot)
        {
            var payload = snapshot as Payload;
            if (payload == null || payload.Values.Length != charges.Length)
                throw new ArgumentException("Incompatible charge snapshot");
            foreach (int charge in payload.Values)
                if (charge < 0) throw new ArgumentException("Negative charge count");
        }
        public void Restore(object snapshot)
        {
            Validate(snapshot);
            // Do not share mutable payload storage with live state after restore.
            charges = (int[])((Payload)snapshot).Values.Clone();
        }
        private sealed class Payload
        {
            internal readonly int[] Values;
            internal Payload(int[] values) { Values = values; }
        }
    }
}
