using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SubframeCharge
{
    // Eligibility comes from the native tree visiting JumpState after physics,
    // not from yesterday's body flags or the age of an input event.
    internal sealed class ChargeTimeline
    {
        internal long FrameTimestamp { get; private set; }
        internal long FrameNumber { get; private set; }
        internal long EligibleSince { get; private set; }
        internal bool InFrame { get; private set; }
        private bool visited;
        private bool previousVisited;
        private bool observedFrame;
        private struct PauseSpan { internal long Start, End; }
        private readonly List<PauseSpan> pauses = new List<PauseSpan>();
        private long pauseStart;
        private long historyFloor;

        // Called by a component on PauseManager, which keeps updating even
        // while player entities are suspended. A slow player frame is NOT pause.
        internal void ObservePause(bool paused, long timestamp)
        {
            if (paused && pauseStart == 0) pauseStart = timestamp;
            if (!paused && pauseStart != 0)
            {
                pauses.Add(new PauseSpan { Start = pauseStart, End = timestamp });
                pauseStart = 0;
                if (pauses.Count > 256)
                {
                    historyFloor = pauses[0].End;
                    pauses.RemoveAt(0);
                }
            }
        }

        internal bool HasHistory(long start) { return start >= historyFloor; }

        internal double ActiveSeconds(long start, long end)
        {
            long ticks = Math.Max(0, end - start);
            foreach (PauseSpan span in pauses)
                ticks -= Math.Max(0, Math.Min(end, span.End) - Math.Max(start, span.Start));
            if (pauseStart != 0) ticks -= Math.Max(0, end - Math.Max(start, pauseStart));
            return (double)Math.Max(0, ticks) / Stopwatch.Frequency;
        }

        internal void BeginFrame(long timestamp)
        {
            if (InFrame) throw new InvalidOperationException("SFC frame observer did not finish its previous update");
            if (timestamp <= 0 || timestamp < FrameTimestamp)
                throw new ArgumentOutOfRangeException("timestamp");
            FrameTimestamp = timestamp;
            FrameNumber++;
            visited = false;
            InFrame = true;
        }

        internal void VisitNative()
        {
            if (!InFrame) throw new InvalidOperationException("SFC native charge ran outside its frame observers");
            if (!visited && observedFrame && !previousVisited)
                EligibleSince = FrameTimestamp;
            visited = true;
        }

        internal void EndFrame()
        {
            if (!InFrame) throw new InvalidOperationException("SFC frame observer has no active update");
            previousVisited = visited;
            observedFrame = true;
            InFrame = false;
        }

        internal long OriginFor(long physicalPress)
        {
            if (!InFrame || !visited) throw new InvalidOperationException("SFC charge eligibility was not observed");
            long origin = Math.Max(physicalPress, EligibleSince);
            foreach (PauseSpan span in pauses)
                if (origin >= span.Start && origin < span.End) origin = span.End;
            return origin;
        }

        internal void ResetEligibility()
        {
            EligibleSince = 0;
            observedFrame = false;
            previousVisited = false;
        }
    }
}
