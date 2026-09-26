using System;

namespace MegaMappingExpansion
{
    /// <summary>
    /// Deterministic presentation height-field. Jump King's WaterBlock remains
    /// authoritative for movement, gravity and charge physics.
    /// </summary>
    internal sealed class WaterSurfaceSimulation
    {
        private readonly float[] height;
        private readonly float[] velocity;
        private readonly float[] acceleration;
        private float stiffness;
        private float propagation;
        private float damping;

        internal WaterSurfaceSimulation(int segments, float tension, float spread, float damp)
        {
            int count = Math.Max(12, Math.Min(segments, 256));
            height = new float[count]; velocity = new float[count]; acceleration = new float[count];
            Configure(tension, spread, damp);
        }

        internal int SegmentCount { get { return height.Length; } }
        internal void Configure(float tension, float spread, float damp)
        { stiffness = Math.Max(0.1f, tension); propagation = Math.Max(0f, spread); damping = Math.Max(0f, damp); }

        internal void Impulse(float normalizedX, float force, float radius)
        {
            float center = Clamp01(normalizedX) * (height.Length - 1);
            float radiusSegments = Math.Max(1f, radius * height.Length);
            int first = Math.Max(0, (int)Math.Floor(center - radiusSegments));
            int last = Math.Min(height.Length - 1, (int)Math.Ceiling(center + radiusSegments));
            for (int i = first; i <= last; i++)
            {
                float distance = Math.Abs(i - center) / radiusSegments;
                if (distance >= 1f) continue;
                float envelope = 0.5f + 0.5f * (float)Math.Cos(distance * Math.PI);
                velocity[i] += force * envelope;
            }
        }

        internal void Step(float delta)
        {
            float remaining = Math.Max(0f, Math.Min(delta, 0.1f));
            while (remaining > 0f)
            {
                float step = Math.Min(remaining, 1f / 120f);
                for (int i = 0; i < height.Length; i++)
                {
                    float left = height[i == 0 ? i : i - 1];
                    float right = height[i == height.Length - 1 ? i : i + 1];
                    acceleration[i] = -stiffness * height[i] + propagation * (left + right - 2f * height[i]);
                }
                float drag = (float)Math.Exp(-damping * step);
                for (int i = 0; i < height.Length; i++)
                { velocity[i] = (velocity[i] + acceleration[i] * step) * drag; height[i] += velocity[i] * step; }
                remaining -= step;
            }
        }

        internal float Sample(float normalizedX)
        {
            float sample = Clamp01(normalizedX) * (height.Length - 1);
            int low = Math.Min(height.Length - 1, (int)Math.Floor(sample));
            int high = Math.Min(height.Length - 1, low + 1);
            return height[low] + (height[high] - height[low]) * (sample - low);
        }

        internal float Energy()
        { float value = 0f; for (int i = 0; i < height.Length; i++) value += Math.Abs(height[i]) + Math.Abs(velocity[i]); return value; }
        private static float Clamp01(float value) { return value < 0f ? 0f : value > 1f ? 1f : value; }
    }

    internal sealed class WaterSurfaceState
    {
        internal readonly WaterSurfaceSimulation Surface;
        internal readonly System.Collections.Generic.List<SplashParticle> Splashes = new System.Collections.Generic.List<SplashParticle>();
        internal bool ContactKnown;
        internal bool WasInside;
        internal float WakeClock;
        internal WaterSurfaceState(WaterData water)
        { Surface = new WaterSurfaceSimulation(water.WaveSegments, water.SurfaceTension, water.WaveSpread, water.WaveDamping); }
    }

    internal sealed class SplashParticle
    {
        internal float X, Y, VelocityX, VelocityY, Life, Lifetime, Size;
    }
}
