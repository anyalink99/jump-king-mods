using System;

namespace SubframeCharge
{
    public enum SubstepMode { HalfStep, QuarterStep, EighthStep, Millisecond }

    internal static class Substep
    {
        internal const double MinimumMilliseconds = 0.1;
        internal const double MaximumMilliseconds = 17;
        internal static bool Valid(SubstepMode mode) { return mode >= SubstepMode.HalfStep && mode <= SubstepMode.Millisecond; }
        internal static double Milliseconds(SubstepMode mode)
        {
            switch (mode)
            {
                case SubstepMode.HalfStep: return 8.5;
                case SubstepMode.QuarterStep: return 4.25;
                case SubstepMode.EighthStep: return 2.125;
                case SubstepMode.Millisecond: return 1;
                default: throw new ArgumentOutOfRangeException("mode");
            }
        }
        internal static string Label(SubstepMode mode)
        {
            switch (mode)
            {
                case SubstepMode.HalfStep: return "Half-step";
                case SubstepMode.QuarterStep: return "Quarter-step";
                case SubstepMode.EighthStep: return "Eighth-step";
                case SubstepMode.Millisecond: return "1 ms";
                default: throw new ArgumentOutOfRangeException("mode");
            }
        }
    }
}
