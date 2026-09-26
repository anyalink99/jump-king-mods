using System;
using System.Collections.Generic;

namespace JKRuntime.Gameplay
{
    public enum JumpEvidence { Unavailable, PhysicalHold, BufferedNative, BufferedHold }
    public enum JumpOutcome { UpwardImpulse, NoUpwardImpulse }
    public sealed class JumpResult
    {
        public string Provider { get; private set; }
        public JumpEvidence Evidence { get; private set; }
        public double? HoldMilliseconds { get; private set; }
        public int? CorrectedFrames { get; private set; }
        public int? PredictedFrames { get; private set; }
        /// <summary>Exact corrected hold frames, before surface scaling and the native release increment. Null when unavailable.</summary>
        public float? CorrectedFrameCount { get; private set; }
        /// <summary>Exact predicted hold frames, before surface scaling and the native release increment. Null when unavailable.</summary>
        public float? PredictedFrameCount { get; private set; }
        public float? CorrectedTimer { get; private set; }
        public bool ObservationOnly { get; private set; }
        public bool Automatic { get; private set; }
        public float VelocityBefore { get; private set; }
        public float VelocityAfter { get; private set; }
        public JumpOutcome Outcome { get { return VelocityAfter < 0 && VelocityAfter < VelocityBefore ? JumpOutcome.UpwardImpulse : JumpOutcome.NoUpwardImpulse; } }
        public JumpResult(string provider, JumpEvidence evidence, double? milliseconds, int? frames, int? predicted,
            float? timer, bool observationOnly, bool automatic, float before, float after)
            : this(provider, evidence, milliseconds, frames, predicted, timer, observationOnly, automatic, before, after, frames, predicted) { }

        /// <summary>Creates a result retaining fractional frame counts. Legacy integer counters must be null for fractional values.</summary>
        public JumpResult(string provider, JumpEvidence evidence, double? milliseconds, int? frames, int? predicted,
            float? timer, bool observationOnly, bool automatic, float before, float after,
            float? correctedFrameCount, float? predictedFrameCount)
        {
            Provider = ModuleDefinition.ValidId(provider); Evidence = evidence;
            if (milliseconds.HasValue && (milliseconds < 0 || double.IsNaN(milliseconds.Value) || double.IsInfinity(milliseconds.Value))) throw new ArgumentOutOfRangeException("milliseconds");
            if (evidence == JumpEvidence.Unavailable && milliseconds.HasValue) throw new ArgumentException("Unknown evidence cannot carry a measured duration");
            ValidateFrames(correctedFrameCount, frames, "correctedFrameCount");
            ValidateFrames(predictedFrameCount, predicted, "predictedFrameCount");
            HoldMilliseconds = milliseconds; CorrectedFrames = frames; PredictedFrames = predicted; CorrectedTimer = timer;
            CorrectedFrameCount = correctedFrameCount; PredictedFrameCount = predictedFrameCount;
            ObservationOnly = observationOnly; Automatic = automatic; VelocityBefore = before; VelocityAfter = after;
        }
        private static void ValidateFrames(float? exact, int? whole, string name)
        {
            if (exact.HasValue && (exact < 0 || float.IsNaN(exact.Value) || float.IsInfinity(exact.Value)))
                throw new ArgumentOutOfRangeException(name);
            if (whole.HasValue && (!exact.HasValue || exact.Value != whole.Value))
                throw new ArgumentException("Integer and exact frame counts disagree", name);
        }
    }
    public static class JumpEvents
    {
        private static readonly List<Action<JumpResult>> listeners = new List<Action<JumpResult>>();
        internal static bool Requested { get { return listeners.Count != 0; } }
        public static IDisposable Subscribe(Action<JumpResult> listener)
        {
            RuntimeApi.Kernel.CheckThread();
            if (listener == null) throw new ArgumentNullException("listener");
            listeners.Add(listener);
            return new ActionLease(delegate {
                RuntimeApi.Kernel.CheckThread();
                listeners.Remove(listener);
            });
        }
        public static void Publish(JumpResult result)
        {
            RuntimeApi.Kernel.CheckThread();
            if (result == null) throw new ArgumentNullException("result");
            NativeGameplayObserver.Enrich(result);
            foreach (var listener in listeners.ToArray())
                try { listener(result); } catch (Exception error) { Console.WriteLine("[JK Runtime] Jump observer failed: " + error); }
        }
    }
}
