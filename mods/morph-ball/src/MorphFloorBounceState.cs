namespace MorphBallMod
{
    internal sealed class MorphFloorBounceState
    {
        private int remaining;
        private bool extendedSequence;

        internal int Remaining { get { return remaining; } }
        internal bool IsContinuation
        {
            get { return extendedSequence && remaining == 1; }
        }

        internal void BeginFall(bool regularBounceEnabled)
        {
            remaining = regularBounceEnabled ? 1 : 0;
            extendedSequence = false;
        }

        internal bool PrepareImpact(int bounceCount)
        {
            if (bounceCount >= 2)
            {
                remaining = bounceCount;
                extendedSequence = true;
            }
            else if (bounceCount == 1 && remaining == 0)
            {
                remaining = 1;
            }
            return bounceCount > 0
                || (extendedSequence && remaining > 0);
        }

        internal void Consume()
        {
            if (remaining > 0)
            {
                remaining--;
            }
            if (remaining == 0)
            {
                extendedSequence = false;
            }
        }

        internal void Cancel()
        {
            remaining = 0;
            extendedSequence = false;
        }
    }
}
