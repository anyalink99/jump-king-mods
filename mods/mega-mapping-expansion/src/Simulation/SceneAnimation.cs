using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal static class SceneAnimation
    {
        internal static float Phase(float age, float duration, string loop)
        {
            if (age <= 0f) return 0f;
            float raw = age / Math.Max(0.0001f, duration);
            if (string.Equals(loop, "once", StringComparison.OrdinalIgnoreCase)) return MathHelper.Clamp(raw, 0f, 1f);
            if (string.Equals(loop, "pingpong", StringComparison.OrdinalIgnoreCase))
            {
                float cycle = raw - (float)Math.Floor(raw / 2f) * 2f;
                return cycle <= 1f ? cycle : 2f - cycle;
            }
            return raw - (float)Math.Floor(raw);
        }

        internal static Vector2 OrbitPosition(float phase, float amplitudeX, float amplitudeY)
        {
            double angle = Math.PI * 2.0 * phase;
            return new Vector2((float)Math.Cos(angle) * amplitudeX, (float)Math.Sin(angle) * amplitudeY);
        }

        internal static Vector2 PathPosition(Vector2[] path, float phase, bool closed)
        {
            int segments = closed ? path.Length : path.Length - 1;
            float scaled = MathHelper.Clamp(phase, 0f, 0.999999f) * segments;
            int index = Math.Min(segments - 1, (int)Math.Floor(scaled));
            Vector2 next = index + 1 < path.Length ? path[index + 1] : path[0];
            return Vector2.Lerp(path[index], next, scaled - index);
        }

        internal static Vector2 SplinePathPosition(Vector2[] path, float phase, bool closed)
        {
            if (path == null || path.Length < 3) return PathPosition(path, phase, closed);
            int segments = closed ? path.Length : path.Length - 1;
            float normalized = closed ? PositiveModulo(phase, 1f) : MathHelper.Clamp(phase, 0f, 0.999999f);
            float scaled = normalized * segments;
            int index = Math.Min(segments - 1, (int)Math.Floor(scaled));
            float amount = scaled - index;
            int p1 = index;
            int p2 = closed ? (index + 1) % path.Length : Math.Min(path.Length - 1, index + 1);
            int p0 = closed ? (index - 1 + path.Length) % path.Length : Math.Max(0, index - 1);
            int p3 = closed ? (index + 2) % path.Length : Math.Min(path.Length - 1, index + 2);
            return Vector2.CatmullRom(path[p0], path[p1], path[p2], path[p3], amount);
        }

        internal static Vector2 SplinePathTangent(Vector2[] path, float phase, bool closed)
        {
            const float epsilon = 0.0005f;
            float before = closed ? PositiveModulo(phase - epsilon, 1f) : Math.Max(0f, phase - epsilon);
            float after = closed ? PositiveModulo(phase + epsilon, 1f) : Math.Min(0.999999f, phase + epsilon);
            return SplinePathPosition(path, after, closed) - SplinePathPosition(path, before, closed);
        }

        internal static Vector2 PathTangent(Vector2[] path, float phase, bool closed)
        {
            const float epsilon = 0.0005f;
            float before = closed ? PositiveModulo(phase - epsilon, 1f) : Math.Max(0f, phase - epsilon);
            float after = closed ? PositiveModulo(phase + epsilon, 1f) : Math.Min(0.999999f, phase + epsilon);
            return PathPosition(path, after, closed) - PathPosition(path, before, closed);
        }

        internal static float TrackValue(TrackData track, float phase, string id)
        {
            TrackKey[] keys = SceneValidation.ParseTrack(track.Keys, id + "." + track.Property);
            return Interpolate(keys, track.Easing, phase);
        }

        internal static float Interpolate(TrackKey[] keys, string easing, float phase)
        {
            float value = keys[keys.Length - 1].Value;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (phase > keys[i + 1].Time) continue;
                float amount = (phase - keys[i].Time) / Math.Max(0.00001f, keys[i + 1].Time - keys[i].Time);
                amount = MathHelper.Clamp(amount, 0f, 1f);
                if (string.Equals(easing, "smooth", StringComparison.OrdinalIgnoreCase)) amount = amount * amount * (3f - 2f * amount);
                else if (string.Equals(easing, "sine", StringComparison.OrdinalIgnoreCase)) amount = 0.5f - 0.5f * (float)Math.Cos(amount * Math.PI);
                value = MathHelper.Lerp(keys[i].Value, keys[i + 1].Value, amount); break;
            }
            return value;
        }

        private static float PositiveModulo(float value, float modulo) { float result = value % modulo; return result < 0f ? result + modulo : result; }
    }
}
