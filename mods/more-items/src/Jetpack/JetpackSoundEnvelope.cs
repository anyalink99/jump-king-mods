using System;

namespace JumpKingJetpack
{
    internal sealed class JetpackSoundEnvelope
    {
        internal const int FadeUpdates = 6;

        internal float Level { get; private set; }
        internal bool Playing { get; private set; }

        internal void Reset() { Level = 0f; Playing = false; }

        internal void Update(bool active)
        {
            float step = 1f / FadeUpdates;
            if (active)
            {
                Playing = true;
                Level = Math.Min(1f, Level + step);
                return;
            }

            Level = Math.Max(0f, Level - step);
            if (Level == 0f)
            {
                Playing = false;
            }
        }
    }
}
