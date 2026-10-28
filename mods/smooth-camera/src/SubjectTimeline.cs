using System;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal sealed class SubjectTimeline
    {
        private SubjectSample previous, current;
        private bool initialized, pendingCut;
        private float age = 1;
        internal void Reset() { initialized = pendingCut = false; age = 1; }
        internal void Observe(SubjectSample value, LevelScreen[] screens, bool nativeTick = true)
        {
            // identical native ticks still consume the last movement; repeated draws don't
            if (!nativeTick && initialized && value.Center == current.Center && value.Velocity == current.Velocity
                && value.Screen == current.Screen && value.Paused == current.Paused && !value.Cut) return;
            previous = current;
            Vector2 shift = Vector2.Zero;
            bool seam = initialized && !value.Cut && JKRuntime.Geometry.MapTopology.TryCrossing(screens,
                current.Center - new Vector2(0, 13), value.Center - new Vector2(0, 13), out shift);
            if (seam) previous.Center += shift;
            bool cut = !initialized || value.Cut || Vector2.DistanceSquared(previous.Center, value.Center) > 96 * 96;
            current = value; if (cut || value.Paused) previous = value;
            initialized = true; pendingCut |= cut; age = cut ? 1 : 0;
        }
        internal SubjectSample Sample(float delta, bool smooth, float phase = float.NaN)
        {
            age = float.IsNaN(phase) ? Math.Min(1, age + Math.Max(0, delta) * 60) : CameraMotion.Clamp(phase, 0, 1);
            var sample = current;
            if (smooth && !current.Paused) sample.Center = Vector2.Lerp(previous.Center, current.Center, age);
            sample.NativeCenter = current.Center; sample.Presented = true;
            sample.Cut = pendingCut; pendingCut = false;
            return sample;
        }
    }
}
