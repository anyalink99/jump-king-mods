using System;

namespace JKRuntime
{
    /// <summary>Opt-in startup timing on the game thread. Disabled measurements allocate no objects and write no files.</summary>
    public struct StartupMeasurement : IDisposable
    {
        private StartupTrace.Span span;
        internal StartupMeasurement(string stage) { span = StartupTrace.Measure(stage); }
        /// <summary>Complete the measurement. Use with a using statement; do not retain across attempts or copy active measurements.</summary>
        public void Dispose() { span.Dispose(); }
    }
}
