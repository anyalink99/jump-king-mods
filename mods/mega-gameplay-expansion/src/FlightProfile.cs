using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace MegaGameplayExpansion
{
    // Explicit diagnostic sessions only. Markers time the original stages on
    // the private forecast body without modifying their order or behaviour.
    internal sealed class FlightProfile
    {
        private sealed class Sample
        {
            internal string Name;
            internal long Total, Maximum;
            internal int Calls;
        }
        private sealed class Marker : IBodyCompBehaviour
        {
            internal FlightProfile Owner;
            internal int Index;
            public bool ExecuteBehaviour(BehaviourContext context)
            { Owner.Begin(Index); return true; }
        }
        private readonly List<Sample> samples = new List<Sample>();
        private readonly Dictionary<string, int> exceptions = new Dictionary<string, int>();
        private int current = -1;
        private long started;
        private bool reported;
        private static int remaining;
        private static bool listening;
        [ThreadStatic] private static FlightProfile active;
        [ThreadStatic] private static bool recordingException;
        private sealed class Session : IDisposable
        { public void Dispose() { StopSession(); } }
        internal static IDisposable StartSession()
        {
            StopSession(); remaining = 12;
            AppDomain.CurrentDomain.FirstChanceException += OnException;
            listening = true;
            return new Session();
        }
        internal static void StopSession()
        {
            remaining = 0; active = null;
            if (listening) AppDomain.CurrentDomain.FirstChanceException -= OnException;
            listening = false;
        }
        private static void OnException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args)
        {
            var profile = active;
            if (profile == null || recordingException) return;
            recordingException = true;
            try
            {
            string key = (profile.current < 0 ? "outside-stage" : profile.samples[profile.current].Name)
                + ":" + args.Exception.GetType().FullName;
            int count;
            if (profile.exceptions.TryGetValue(key, out count)) profile.exceptions[key] = count + 1;
            else if (profile.exceptions.Count < 16) profile.exceptions.Add(key, 1);
            }
            finally { recordingException = false; }
        }
        internal static FlightProfile Create()
        { if (remaining <= 0) return null; remaining--; return new FlightProfile(); }
        internal void Enter() { active = this; }
        internal void Leave() { End(); if (active == this) active = null; }
        internal int Add(string name) { samples.Add(new Sample { Name = name }); return samples.Count - 1; }
        internal void Begin(int index) { End(); current = index; started = Stopwatch.GetTimestamp(); }
        internal void End()
        {
            if (current < 0) return;
            long elapsed = Stopwatch.GetTimestamp() - started;
            var sample = samples[current]; sample.Total += elapsed; sample.Calls++;
            sample.Maximum = Math.Max(sample.Maximum, elapsed); current = -1;
        }
        internal void Attach(BodyComp body)
        {
            var stages = NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours");
            for (var node = stages.First; node != null; node = node.Next)
                stages.AddBefore(node, new Marker { Owner = this, Index = Add(node.Value.GetType().FullName) });
        }
        internal string Summary(int ticks, int slices, double work, string failure)
        {
            var text = new StringBuilder("Warp profile v1: ticks=").Append(ticks).Append(" slices=").Append(slices)
                .Append(" workMs=").Append(work.ToString("F3", CultureInfo.InvariantCulture))
                .Append(" status=").Append(failure ?? "landed");
            foreach (var sample in samples)
                if (sample.Calls != 0) text.Append("\n  ").Append(sample.Name).Append(" calls=").Append(sample.Calls)
                    .Append(" totalMs=").Append(Milliseconds(sample.Total)).Append(" maxMs=").Append(Milliseconds(sample.Maximum));
            foreach (var pair in exceptions) text.Append("\n  firstChance ").Append(pair.Key).Append(" count=").Append(pair.Value);
            return text.ToString();
        }
        private static string Milliseconds(long ticks)
        { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture); }
        internal void Report(int ticks, int slices, double work, string failure)
        {
            if (reported) return; reported = true;
            WarpDiagnostics.Write(Summary(ticks, slices, work, failure));
        }
    }
}
