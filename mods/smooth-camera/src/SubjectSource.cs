using System;
using System.Reflection;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    // optional presentation adapters never update the body or advance another clock
    internal static class SubjectSource
    {
        private static Func<BodyComp, Vector2> external;
        private static long nextScan;
        internal static Vector2 Read(BodyComp body)
        {
            if (!ModEntry.ExternalInterpolation) return body.Position;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now >= nextScan)
            {
                external = null; nextScan = now + System.Diagnostics.Stopwatch.Frequency;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("SubframeCharge.PlayerPresentation", false);
                    var method = type == null ? null : type.GetMethod("Position", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(BodyComp) }, null);
                    if (method != null && method.ReturnType == typeof(Vector2))
                    { external = (Func<BodyComp, Vector2>)Delegate.CreateDelegate(typeof(Func<BodyComp, Vector2>), method); break; }
                }
            }
            if (external != null) try { return external(body); } catch { external = null; }
            return body.Position;
        }
    }
}
