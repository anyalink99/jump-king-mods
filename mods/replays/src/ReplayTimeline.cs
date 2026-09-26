using System;

namespace Replays
{
    internal sealed class ReplayTimeline
    {
        private readonly int count;
        private float accumulator;

        internal int Index { get; private set; }
        internal bool Playing { get; private set; }

        internal ReplayTimeline(int frameCount)
        {
            count = Math.Max(0, frameCount);
            Playing = count > 1;
        }

        internal void Toggle()
        {
            if (count > 1) Playing = !Playing;
        }

        internal void Seek(int frames)
        {
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
            accumulator += Math.Max(0f, delta) * 60f;
            int advance = (int)accumulator;
            if (advance <= 0) return;
            accumulator -= advance;
            Index = Math.Min(count - 1, Index + advance);
            if (Index >= count - 1) Playing = false;
        }
    }
}
