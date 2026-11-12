using System;

namespace Replays
{
    internal static class ReplayTiming
    {
        // .NET Framework rounds the native 1/60 TargetElapsedTime to 17 ms
        internal const long LegacyTickDuration = 170000;

        internal static TimeSpan Duration(ReplayHeader header, int frames)
        {
            return TimeSpan.FromTicks(header.TickDuration * (long)frames);
        }

        internal static double InitialSeconds(ReplayHeader header)
        {
            return header.InitialGameTime + header.InitialGameTicks * (header.TickDuration / (double)TimeSpan.TicksPerSecond);
        }

        internal static int SeekFrames(ReplayHeader header, int seconds)
        {
            return (int)Math.Round(seconds * (double)TimeSpan.TicksPerSecond / header.TickDuration);
        }

        internal static string Format(TimeSpan value, bool precise = false)
        {
            string format = value.TotalHours >= 1 ? "h\\:mm\\:ss" : "m\\:ss";
            return value.ToString(format + (precise ? "\\.fff" : ""));
        }
    }
}
