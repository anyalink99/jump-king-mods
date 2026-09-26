using System;

namespace JKRuntime.Simulation
{
    // Pure, on-demand port of PlayerStats.timeSpan + WindManager.CurrentVelocityRaw.
    // It does not decide whether the body receives wind; that separate native
    // behaviour also needs its activation latch, NoWind, Snow and screen entry.
    public static class NativeWind
    {
        public const string AuditedGameSha256 = "476f2033b8b614ec97b04311799b2b78239397a2fe8018c77bb45a1f946ffc88";
        public static float Velocity(long elapsedTicks, double targetElapsedSeconds, float legacySeconds,
            bool enabled, float intensity, bool? directionLeft)
        {
            if (elapsedTicks < 0 || targetElapsedSeconds <= 0 || double.IsNaN(targetElapsedSeconds)
                || double.IsInfinity(targetElapsedSeconds) || float.IsNaN(legacySeconds) || float.IsInfinity(legacySeconds)
                || float.IsNaN(intensity) || float.IsInfinity(intensity)) throw new ArgumentException("Invalid wind clock/configuration");
            if (!enabled) return 0;
            double time = TimeSpan.FromSeconds(elapsedTicks * targetElapsedSeconds + legacySeconds).TotalSeconds;
            float phase = (float)time * 0.48124886f;
            float wave = (float)Math.Sin(phase);
            wave = (float)Math.Cos(phase) > 0 ? wave * 2 + 1 : wave * 2 - 1;
            wave = Math.Max(-1, Math.Min(1, wave));
            if (intensity != 0)
            {
                if (directionLeft.HasValue) return directionLeft.Value ? -0.0125f * intensity : 0.0125f * intensity;
                return wave * (0.0125f * intensity);
            }
            return wave * 0.1f;
        }
    }
}
