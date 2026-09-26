using System;

namespace SmoothCamera
{
    internal sealed class CameraMotion
    {
        internal const int Width = 480, Height = 360;
        private bool initialized;
        private float previousPlayerY;
        private float target, velocity, maximum, playerY;
        private bool frozen;
        private float response = 12f;
        internal float Translation { get; private set; }
        internal int PixelTranslation { get { return (int)Math.Round(Translation); } }

        internal void Reset() { initialized = false; velocity = 0; }
        internal void Rebase(float translation)
        { if (initialized) { Translation += translation; target += translation; previousPlayerY -= translation; playerY -= translation; } }

        internal void Step(float centerY, int screenCount, float delta, bool paused)
        { Observe(centerY, screenCount, paused); Advance(delta); }

        internal void Observe(float centerY, int screenCount, bool paused, float verticalSpeed = 0)
        {
            if (screenCount <= 0 || float.IsNaN(centerY) || float.IsInfinity(centerY)) return;
            maximum = (screenCount - 1) * (float)Height;
            playerY = centerY; frozen = paused;
            // At native terminal fall speed a fixed slow spring trails beyond
            // the visibility guard and gets snapped back every simulation tick.
            // Keep its moving-target lag below the spare framing margin instead.
            if (!float.IsNaN(verticalSpeed) && !float.IsInfinity(verticalSpeed))
                response = Math.Max(12f, Math.Min(60f, Math.Abs(verticalSpeed) / 24f));
            float desired = Height * 0.5f - centerY;
            if (!initialized || Math.Abs(centerY - previousPlayerY) > Height * 0.75f)
            {
                Translation = target = Clamp(desired, 0, maximum);
                velocity = 0;
                initialized = true;
            }
            else if (!paused)
            {
                // Retain the last framing through a jump's apex and small landing
                // corrections. Downward travel must leave the larger lower band
                // before the camera reverses; no delayed recentering while idle.
                if (desired > target + 12f) target = desired - 12f;
                else if (desired < target - 64f) target = desired + 64f;
            }
            target = Clamp(target, 0, maximum);
            Translation = Clamp(Translation, 0, maximum);
            previousPlayerY = centerY;
        }

        internal void Advance(float delta)
        {
            if (!initialized || frozen || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float dt = Math.Min(delta, .1f);
            // Exact critically damped spring: continuous velocity, no overshoot
            // for a stopped target, and identical results at different draw rates.
            float omega = response;
            float displacement = Translation - target;
            float impulse = (velocity + omega * displacement) * dt;
            float decay = (float)Math.Exp(-omega * dt);
            float next = target + (displacement + impulse) * decay;
            velocity = (velocity - omega * impulse) * decay;
            // A sudden landing can lower the response while velocity remains
            // high. Finish at the retained target instead of overshooting it
            // and introducing a small reverse motion afterward.
            if ((displacement < 0 && next > target) || (displacement > 0 && next < target))
            { next = target; velocity = 0; }
            Translation = Clamp(Clamp(next, 48 - playerY, Height - 48 - playerY), 0, maximum);
            if (Translation != next) velocity = 0;
            if (Math.Abs(Translation - target) < .0001f && Math.Abs(velocity) < .001f)
            { Translation = target; velocity = 0; }
        }

        internal static float Clamp(float value, float minimum, float maximum)
        { return Math.Max(minimum, Math.Min(maximum, value)); }

        internal static int[] VisibleScreens(int translation, int screenCount)
        {
            if (screenCount <= 0) return new int[0];
            translation = Math.Max(0, Math.Min((screenCount - 1) * Height, translation));
            int lower = translation / Height;
            return translation % Height == 0 || lower + 1 >= screenCount
                ? new[] { lower } : new[] { lower, lower + 1 };
        }
    }
}
