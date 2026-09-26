using System;

namespace MorphBallMod
{
    internal sealed class MorphTransition
    {
        internal const int TransitionFrames = 8;
        private const float Step = 1f / TransitionFrames;

        internal float Amount { get; private set; }
        internal bool TargetMorphed { get; private set; }
        internal bool UsesBallHitbox { get { return Amount >= 0.5f; } }

        internal void Toggle(bool canUnfold)
        {
            if (TargetMorphed)
            {
                if (canUnfold)
                {
                    TargetMorphed = false;
                }
            }
            else
            {
                TargetMorphed = true;
            }
        }

        internal void Update()
        {
            float target = TargetMorphed ? 1f : 0f;
            if (Amount < target)
            {
                Amount = Math.Min(target, Amount + Step);
            }
            else if (Amount > target)
            {
                Amount = Math.Max(target, Amount - Step);
            }
        }

        internal void ForceUnmorphed()
        {
            Amount = 0f;
            TargetMorphed = false;
        }

        internal void KeepMorphed()
        {
            Amount = 1f;
            TargetMorphed = true;
        }
    }
}
