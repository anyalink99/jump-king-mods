using System;

namespace MorphBallMod
{
    internal struct MorphJumpProfile
    {
        internal readonly float HeightScale;
        internal readonly int TakeoffFrames;
        internal readonly int SustainedFrames;

        internal MorphJumpProfile(
            float heightScale,
            int takeoffFrames,
            int sustainedFrames)
        {
            HeightScale = heightScale;
            TakeoffFrames = takeoffFrames;
            SustainedFrames = sustainedFrames;
        }
    }

    internal static class MorphJumpPhysics
    {
        internal const int TakeoffFrameCount = 6;
        internal const int SustainedFrames = 36;
        internal const float EarlyReleaseMultiplier = 2.5f;

        private static readonly float[] TakeoffFractions =
        {
            2f / 7f,
            3.25f / 7f,
            4.4f / 7f,
            5.4f / 7f,
            6.3f / 7f,
            1f,
        };

        internal static MorphJumpProfile CreateProfile(float heightScale)
        {
            if (heightScale <= 0f || heightScale > 1f)
            {
                throw new ArgumentOutOfRangeException("heightScale");
            }
            float durationScale = heightScale;
            return new MorphJumpProfile(
                heightScale,
                Math.Max(1, (int)Math.Round(
                    TakeoffFrameCount * durationScale)),
                Math.Max(1, (int)Math.Round(
                    SustainedFrames * durationScale)));
        }

        internal static float TakeoffSpeed(
            int frame,
            float fullSpeed,
            float gravity)
        {
            return TakeoffSpeed(
                frame,
                fullSpeed,
                gravity,
                CreateProfile(1f));
        }

        internal static float TakeoffSpeed(
            int frame,
            float fullSpeed,
            float gravity,
            MorphJumpProfile profile)
        {
            if (frame < 1 || frame > profile.TakeoffFrames)
            {
                throw new ArgumentOutOfRangeException("frame");
            }
            float fullRise = BallisticRise(fullSpeed, gravity)
                * profile.HeightScale;
            float peakSpeed = fullRise * 7f / 153f;
            float position = profile.TakeoffFrames == 1
                ? TakeoffFrameCount - 1f
                : (frame - 1f) * (TakeoffFrameCount - 1f)
                    / (profile.TakeoffFrames - 1f);
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(TakeoffFrameCount - 1, lower + 1);
            float blend = position - lower;
            float fraction = TakeoffFractions[lower]
                + (TakeoffFractions[upper] - TakeoffFractions[lower])
                    * blend;
            return peakSpeed * fraction;
        }

        internal static float HeldAcceleration(
            float fullSpeed,
            float gravity)
        {
            return HeldAcceleration(
                fullSpeed,
                gravity,
                CreateProfile(1f));
        }

        internal static float HeldAcceleration(
            float fullSpeed,
            float gravity,
            MorphJumpProfile profile)
        {
            float fullRise = BallisticRise(fullSpeed, gravity)
                * profile.HeightScale;
            float peakSpeed = TakeoffSpeed(
                profile.TakeoffFrames,
                fullSpeed,
                gravity,
                profile);
            float takeoffRise = 0f;
            for (int frame = 1; frame <= profile.TakeoffFrames; frame++)
            {
                takeoffRise += TakeoffSpeed(
                    frame,
                    fullSpeed,
                    gravity,
                    profile);
            }
            float sustainedRise = fullRise - takeoffRise;
            float triangle = profile.SustainedFrames
                * (profile.SustainedFrames + 1f) / 2f;
            float heldGravity =
                (profile.SustainedFrames * peakSpeed - sustainedRise)
                    / triangle;
            return gravity - heldGravity;
        }

        internal static float EarlyReleaseDeceleration(float gravity)
        {
            return gravity * (EarlyReleaseMultiplier - 1f);
        }

        private static float BallisticRise(float speed, float gravity)
        {
            float rise = 0f;
            while (speed > 0f)
            {
                rise += speed;
                speed -= gravity;
            }
            return rise;
        }
    }
}
