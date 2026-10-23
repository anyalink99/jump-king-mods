using System;

namespace Replays
{
    internal sealed class ReplayTimeline
    {
        private readonly int count;
        private float accumulator;
        private readonly float cadence;

        internal int Index { get; private set; }
        internal bool Playing { get; private set; }
        internal long SeekRevision { get; private set; }

        internal ReplayTimeline(int frameCount, float recordedCadence = 1f)
        {
            count = Math.Max(0, frameCount);
            cadence = recordedCadence;
            Playing = count > 1;
        }

        internal void Toggle()
        {
            if (count > 1) Playing = !Playing;
        }

        internal void Seek(int frames)
        {
            SeekRevision++;
            if (count == 0)
            {
                Index = 0;
                return;
            }
            Index = Math.Max(0, Math.Min(count - 1, Index + frames));
            accumulator = 0f;
            if (Index >= count - 1) Playing = false;
        }

        internal void Update(float delta)
        {
            if (!Playing || count < 2) return;
            // native updates supply 1/60 even though their IGT step is 17 ms
            accumulator += Math.Max(0f, delta) * 60f * cadence;
            int advance = (int)accumulator;
            if (advance <= 0) return;
            accumulator -= advance;
            Index = Math.Min(count - 1, Index + advance);
            if (Index >= count - 1) Playing = false;
        }
    }
}
