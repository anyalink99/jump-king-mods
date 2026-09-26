using JKRuntime.Input;
using System;

namespace SubframeCharge
{
    internal struct ChargeResult
    {
        internal readonly float ExactFrames;
        // Jump%'s legacy public counter is integer-only. ExactFrames is authoritative.
        internal int Frames { get { return (int)Math.Min(int.MaxValue - 1.0, Math.Floor(ExactFrames + 0.5)); } }
        internal int? WholeFrames { get { return ExactFrames == Math.Floor(ExactFrames) && ExactFrames < int.MaxValue ? (int?)ExactFrames : null; } }
        internal readonly float TimerSeconds;
        internal float Strength { get { return Math.Min(1f, TimerSeconds / 0.6f); } }
        internal ChargeResult(float frames, float timerSeconds)
        { ExactFrames = frames; TimerSeconds = timerSeconds; }
    }

    internal static class ChargeQuantizer
    {
        // Game1 sets TargetElapsedTime through .NET Framework FromSeconds:
        // 1/60 rounds to 17 ms. Physics still advances by 1/60 second.
        internal const double HoldStepSeconds = 0.017;
        internal const int StockStepsPerSecond = 60;
        internal const int MinimumStep = 2;
        internal const int MaximumStep = 36;

        internal static int Quantize(
            double heldSeconds,
            float chargeMultiplier)
        {
            return QuantizeRelease(heldSeconds, chargeMultiplier).Frames + 1;
        }

        internal static ChargeResult QuantizeRelease(
            double heldSeconds,
            float chargeMultiplier)
        { return QuantizeRelease(heldSeconds, chargeMultiplier, false); }

        internal static ChargeResult QuantizeRelease(
            double heldSeconds, float chargeMultiplier, bool quarterSteps)
        {
            if (double.IsNaN(heldSeconds)
                || double.IsInfinity(heldSeconds)
                || heldSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException("heldSeconds");
            }
            if (float.IsNaN(chargeMultiplier)
                || float.IsInfinity(chargeMultiplier)
                || chargeMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException("chargeMultiplier");
            }

            // Round input frames FIRST. Water advances each native timer tick
            // by 0.5/60, including release; rounding the scaled power instead
            // removes every other underwater jump strength.
            double subdivisions = quarterSteps ? 4 : 1;
            double maxFrames = Math.Max(1, Math.Ceiling((MaximumStep / (double)chargeMultiplier - 1) * subdivisions) / subdivisions);
            double frames = Math.Min(int.MaxValue - 1.0, Math.Min(maxFrames,
                Math.Max(1, Math.Floor(heldSeconds / HoldStepSeconds * subdivisions + 0.5) / subdivisions)));
            float timer = (float)Math.Min(0.6,
                (frames + 1.0) * chargeMultiplier / StockStepsPerSecond);
            return new ChargeResult((float)frames, timer);
        }

        internal static float TimerBeforeNativeRelease(ChargeResult charge, double nextIncrement)
        {
            if (double.IsNaN(nextIncrement) || double.IsInfinity(nextIncrement) || nextIncrement < 0)
                throw new ArgumentOutOfRangeException("nextIncrement");
            return (float)Math.Max(0, charge.TimerSeconds - nextIncrement);
        }

        internal static float StrengthForStep(int step)
        {
            ValidateStep(step);
            return (float)step / MaximumStep;
        }

        internal static float TimerBeforeNativeRelease(
            int finalTimerStep,
            double nextIncrement)
        {
            ValidateStep(finalTimerStep);
            if (double.IsNaN(nextIncrement)
                || double.IsInfinity(nextIncrement)
                || nextIncrement < 0.0)
            {
                throw new ArgumentOutOfRangeException("nextIncrement");
            }

            double targetSeconds =
                (double)finalTimerStep / StockStepsPerSecond;
            return (float)Math.Max(0.0, targetSeconds - nextIncrement);
        }

        private static void ValidateStep(int step)
        {
            if (step < MinimumStep || step > MaximumStep)
            {
                throw new ArgumentOutOfRangeException("step");
            }
        }
    }
}
