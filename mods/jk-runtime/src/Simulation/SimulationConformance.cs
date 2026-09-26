using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JumpKing.Player;

namespace JKRuntime.Simulation
{
    /// <summary>A first-divergence report. A passing pose comparison alone is not coverage of hidden world state.</summary>
    public sealed class ConformanceResult
    {
        public bool Matches { get; internal set; }
        public int Tick { get; internal set; }
        public SimulationInput Input { get; internal set; }
        public string[] Differences { get; internal set; }
        public string Evidence { get; internal set; }
    }
    /// <summary>Explicit test harness; never subscribes to live gameplay or advances it without a caller-provided step.</summary>
    public static class SimulationConformance
    {
        public static ConformanceResult Compare(string evidence, IEnumerable<SimulationInput> inputs,
            Func<SortedDictionary<string, string>> reference, Func<SortedDictionary<string, string>> candidate,
            Action<SimulationInput> stepReference, Action<SimulationInput> stepCandidate, int maximumTicks = 7200)
        {
            if (string.IsNullOrWhiteSpace(evidence) || inputs == null || reference == null || candidate == null || stepReference == null || stepCandidate == null
                || maximumTicks < 1 || maximumTicks > 1000000) throw new ArgumentException("Explicit conformance evidence, callbacks and bounded inputs required");
            var result = CompareFields(evidence, 0, new SimulationInput(), reference(), candidate());
            if (!result.Matches) return result;
            int tick = 0;
            foreach (var input in inputs)
            {
                if (++tick > maximumTicks) throw new InvalidOperationException("Conformance tick budget exceeded");
                stepReference(input); stepCandidate(input);
                result = CompareFields(evidence, tick, input, reference(), candidate());
                if (!result.Matches) return result;
            }
            return result;
        }
        public static ConformanceResult CompareFields(string evidence, int tick, SimulationInput input,
            IDictionary<string, string> reference, IDictionary<string, string> candidate)
        {
            if (reference == null || candidate == null || reference.Count + candidate.Count > 8192) throw new ArgumentException("Invalid comparison fields");
            var differences = new List<string>();
            foreach (string key in reference.Keys.Concat(candidate.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            {
                string a, b; bool hasA = reference.TryGetValue(key, out a), hasB = candidate.TryGetValue(key, out b);
                if (hasA != hasB || a != b) differences.Add(key + ": expected=" + (hasA ? a : "<missing>") + "; actual=" + (hasB ? b : "<missing>"));
            }
            return new ConformanceResult { Matches = differences.Count == 0, Tick = tick, Input = input, Differences = differences.ToArray(), Evidence = evidence };
        }
        /// <summary>Native ballistic body fields; callers must add any controller, block and world state relevant to their claim.</summary>
        public static SortedDictionary<string, string> ReadBody(BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread();
            if (body == null) throw new ArgumentNullException("body");
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            fields["position.x"] = body.Position.X.ToString("R", CultureInfo.InvariantCulture);
            fields["position.y"] = body.Position.Y.ToString("R", CultureInfo.InvariantCulture);
            fields["velocity.x"] = body.Velocity.X.ToString("R", CultureInfo.InvariantCulture);
            fields["velocity.y"] = body.Velocity.Y.ToString("R", CultureInfo.InvariantCulture);
            fields["lastVelocity.x"] = body.LastVelocity.X.ToString("R", CultureInfo.InvariantCulture);
            fields["lastVelocity.y"] = body.LastVelocity.Y.ToString("R", CultureInfo.InvariantCulture);
            fields["grounded"] = body.IsOnGround.ToString();
            fields["screen"] = body.LastScreen.ToString(CultureInfo.InvariantCulture);
            fields["hitbox"] = body.GetHitbox().ToString();
            return fields;
        }
    }
}
