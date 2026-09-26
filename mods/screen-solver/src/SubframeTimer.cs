using System;

namespace ScreenSolver
{
    internal static class SubframeTimer
    {
        // Same float conversion and subtract/add sequence as SFC 0.15's
        // ChargeQuantizer -> TimerBeforeNativeRelease -> native JumpState.
        internal static float BeforeRelease(double seconds, float multiplier, double increment)
        {
            double maximum = Math.Max(1, Math.Ceiling(36 / (double)multiplier) - 1);
            int frames = (int)Math.Min(maximum, Math.Max(1, Math.Floor(seconds / .017 + .5)));
            float timer = (float)Math.Min(.6, (frames + 1.0) * multiplier / 60);
            return (float)Math.Max(0, timer - increment);
        }
    }
}
