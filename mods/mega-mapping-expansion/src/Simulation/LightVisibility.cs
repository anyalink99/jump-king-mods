using System;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Pure geometric source visibility shared by the runtime and regression tests.
    internal static class LightVisibility
    {
        private static readonly Vector2[] SourceOffsets = {
            Vector2.Zero, new Vector2(-2f, 0), new Vector2(2f, 0),
            new Vector2(0, -2f), new Vector2(0, 2f)
        };

        internal static float SourceVisibility(Vector2 source, Rectangle blocker)
        {
            if (blocker.Width <= 0 || blocker.Height <= 0) return 1f;
            int visible = 0;
            foreach (Vector2 offset in SourceOffsets)
                if (!blocker.Contains(source + offset)) visible++;
            return visible / 5f;
        }

        internal static float Attenuation(float distance, float radius, float exponent)
        {
            float value = Math.Max(0f, 1f - distance / radius);
            return (float)Math.Pow(value * value * (3f - 2f * value), exponent);
        }
        internal static bool Blocked(Vector2 source, Vector2 receiver, Rectangle blocker)
        {
            if (blocker.Width <= 0 || blocker.Height <= 0) return false;
            if (blocker.Contains(source)) return true;
            // The receiving player's own pixels stay illuminated. The shadow starts behind it.
            if (blocker.Contains(receiver)) return false;
            Vector2 delta = receiver - source;
            float enter = 0f, leave = 1f;
            return Slab(source.X, delta.X, blocker.Left, blocker.Right, ref enter, ref leave)
                && Slab(source.Y, delta.Y, blocker.Top, blocker.Bottom, ref enter, ref leave)
                && enter < 1f && leave > 0f;
        }

        private static bool Slab(float start, float delta, float minimum, float maximum, ref float enter, ref float leave)
        {
            if (Math.Abs(delta) < 0.00001f) return start >= minimum && start <= maximum;
            float a = (minimum - start) / delta, b = (maximum - start) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Math.Max(enter, a); leave = Math.Min(leave, b);
            return enter <= leave;
        }

        internal static float Sample(Vector2 source, Vector2 receiver, Rectangle blocker, float opacity)
        {
            if (blocker.Width <= 0 || blocker.Height <= 0) return 1f;
            if (blocker.Contains(receiver)) return SourceVisibility(source, blocker);
            Rectangle conservative = blocker; conservative.Inflate(2, 2);
            // Every soft-source ray stays within 2 pixels of the centre ray.
            // Reject receivers outside the conservative cone before the five tests.
            if (!conservative.Contains(source) && !conservative.Contains(receiver)
                && !Blocked(source, receiver, conservative)) return 1f;
            float visible = 0f;
            foreach (Vector2 offset in SourceOffsets)
            {
                Vector2 sample = source + offset;
                // Covered emitter samples cannot illuminate anything, including the
                // King's surface. Shadow opacity only softens rays from exposed samples.
                if (blocker.Contains(sample)) continue;
                visible += Blocked(sample, receiver, blocker) ? 1f - opacity : 1f;
            }
            return visible / 5f;
        }
    }
}
