using System;

namespace JKRuntime
{
    /// <summary>Optional game-thread performance substage. No allocation or clock read when diagnostic mode is off.</summary>
    public struct PerformanceMeasurement : IDisposable
    {
        private readonly string name;
        private readonly long start;
        private readonly int generation;
        internal PerformanceMeasurement(string stage)
        { name = stage; generation = PerformanceDiagnostics.Generation; start = PerformanceDiagnostics.BeginMeasure(); }
        /// <summary>Complete once with using. Do not copy active scopes. A capture transition discards the unfinished scope.</summary>
        public void Dispose() { PerformanceDiagnostics.EndMeasure(name, start, generation); }
    }
}
