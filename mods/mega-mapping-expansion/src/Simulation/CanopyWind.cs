using System;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Continuous two-dimensional bending, shared by adjacent mesh vertices.
    // The five branch attachments correspond to crown groups, not raster rows.
    internal static class CanopyWind
    {
        internal sealed class Binding
        {
            internal Vector2 Point;
            internal float HeightSquared;
            internal readonly Vector2[] Arms = new Vector2[5];
            internal readonly float[] Weights = new float[5];
        }
        internal sealed class Pose
        {
            internal Vector2 Trunk;
            internal readonly float[] Sines = new float[5], CosineDelta = new float[5];
            internal void Update(float seconds, float strength, float period, float phase)
            {
                float clock = seconds * MathHelper.TwoPi / period;
                float gust = (float)(Math.Sin(clock + phase) * .72 + Math.Sin(clock * .43 + phase * .7) * .28);
                Trunk = new Vector2(strength * gust, strength * gust * gust * .045f);
                for (int branch = 0; branch < 5; branch++)
                {
                    float lag = phase + branch * 1.37f;
                    float angle = strength * .014f * (float)(Math.Sin(clock * (1.2 + branch * .09) + lag)
                        + .24 * Math.Sin(clock * 3.1 + lag * 2));
                    Sines[branch] = (float)Math.Sin(angle); CosineDelta[branch] = (float)Math.Cos(angle) - 1f;
                }
            }
        }
        internal static Binding Bind(Vector2 point, Vector2 root, float height)
        {
            float h = MathHelper.Clamp((root.Y - point.Y) / height, 0f, 1.4f);
            var binding = new Binding { Point = point, HeightSquared = h * h };
            float total = 0;
            for (int branch = 0; branch < 5; branch++)
            {
                float side = branch == 0 ? 0f : branch % 2 == 1 ? -1f : 1f;
                float attachment = branch == 0 ? .69f : branch < 3 ? .50f : .32f;
                Vector2 pivot = root + new Vector2(side * height * .06f, -height * attachment);
                Vector2 crown = root + new Vector2(side * height * .19f, -height * (branch == 0 ? .87f : branch < 3 ? .69f : .47f));
                Vector2 relative = point - crown;
                binding.Arms[branch] = point - pivot;
                binding.Weights[branch] = (float)Math.Exp(-(relative.X * relative.X / (height * height * .027f)
                    + relative.Y * relative.Y / (height * height * .014f)));
                total += binding.Weights[branch];
            }
            float normalize = Math.Min(1f, h * h * 4f) / Math.Max(1f, total);
            for (int branch = 0; branch < 5; branch++) binding.Weights[branch] *= normalize;
            return binding;
        }
        internal static Vector2 Evaluate(Binding binding, Pose pose)
        {
            Vector2 point = binding.Point + pose.Trunk * binding.HeightSquared;
            for (int i = 0; i < 5; i++)
            {
                float weight = binding.Weights[i];
                Vector2 arm = binding.Arms[i];
                point.X += (arm.X * pose.CosineDelta[i] - arm.Y * pose.Sines[i]) * weight;
                point.Y += (arm.X * pose.Sines[i] + arm.Y * pose.CosineDelta[i]) * weight;
            }
            return point;
        }

        // Reference implementation retained for equivalence and performance tests.
        internal static Vector2 Bend(Vector2 point, Vector2 root, float height,
            float seconds, float strength, float period, float phase)
        {
            float h = MathHelper.Clamp((root.Y - point.Y) / height, 0f, 1.4f);
            if (h == 0f || strength == 0f) return point;
            float clock = seconds * MathHelper.TwoPi / period;
            float gust = (float)(Math.Sin(clock + phase) * .72 + Math.Sin(clock * .43 + phase * .7) * .28);
            Vector2 result = point + new Vector2(strength * h * h * gust, strength * h * h * gust * gust * .045f);
            float total = 0f; Vector2 branchMotion = Vector2.Zero;
            for (int branch = 0; branch < 5; branch++)
            {
                float side = branch == 0 ? 0f : branch % 2 == 1 ? -1f : 1f;
                float attachment = branch == 0 ? .69f : branch < 3 ? .50f : .32f;
                Vector2 pivot = root + new Vector2(side * height * .06f, -height * attachment);
                Vector2 crown = root + new Vector2(side * height * .19f,
                    -height * (branch == 0 ? .87f : branch < 3 ? .69f : .47f));
                Vector2 relative = point - crown;
                float weight = (float)Math.Exp(-(relative.X * relative.X / (height * height * .027f)
                    + relative.Y * relative.Y / (height * height * .014f)));
                float lag = phase + branch * 1.37f;
                float angle = strength * .014f * (float)(Math.Sin(clock * (1.2 + branch * .09) + lag)
                    + .24 * Math.Sin(clock * 3.1 + lag * 2));
                Vector2 arm = point - pivot;
                float sine = (float)Math.Sin(angle), cosine = (float)Math.Cos(angle);
                branchMotion += new Vector2(arm.X * (cosine - 1f) - arm.Y * sine,
                    arm.X * sine + arm.Y * (cosine - 1f)) * weight;
                total += weight;
            }
            // Trunk and root remain stiff; crown tips have local lag and torsion.
            result += branchMotion / Math.Max(1f, total) * Math.Min(1f, h * h * 4f);
            return result;
        }
    }
}
