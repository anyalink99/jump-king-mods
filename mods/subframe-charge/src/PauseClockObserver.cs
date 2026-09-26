using System;
using System.Diagnostics;
using JKRuntime.Gameplay;

namespace SubframeCharge
{
    // One level subscription. Menu toggles only swap the timeline reference;
    // they never change a component list while the native manager enumerates it.
    internal static class PauseClockObserver
    {
        private static IDisposable subscription;
        private static ChargeTimeline timeline;
        internal static void InstallForLevel()
        {
            if (subscription == null)
                subscription = NativePause.Subscribe("subframe-charge", delegate(bool paused, long timestamp) {
                    if (timeline != null) timeline.ObservePause(paused, timestamp);
                });
        }
        internal static void Attach(ChargeTimeline clock)
        {
            timeline = clock;
            clock.ObservePause(NativePause.IsPaused, Stopwatch.GetTimestamp());
        }
        internal static void Detach(ChargeTimeline clock)
        { if (ReferenceEquals(timeline, clock)) timeline = null; }
        internal static void Clear()
        { timeline = null; if (subscription != null) { subscription.Dispose(); subscription = null; } }
    }
}
