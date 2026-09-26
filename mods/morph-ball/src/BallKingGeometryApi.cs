using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MorphBallMod
{
    public delegate bool BallKingGeometryProvider(
        IBlock block,
        out Vector2[] vertices);

    public static class BallKingGeometryApi
    {
        // A contour query, not a replacement for the ball's native block callbacks.
        public const string RuntimeProfile = "ball-king.contour";
        public const int RuntimeProfileVersion = 1;

        private static readonly object Sync = new object();
        private static readonly List<BallKingGeometryProvider> Providers =
            new List<BallKingGeometryProvider>();

        public static IDisposable Register(
            BallKingGeometryProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            lock (Sync)
            {
                Providers.Add(provider);
            }
            return new Registration(provider);
        }

        internal static bool TryGetPolygon(
            IBlock block,
            out List<Vector2> polygon)
        {
            BallKingGeometryProvider[] snapshot;
            lock (Sync)
            {
                snapshot = Providers.ToArray();
            }
            for (int index = snapshot.Length - 1; index >= 0; index--)
            {
                Vector2[] vertices;
                if (!snapshot[index](block, out vertices))
                {
                    continue;
                }
                Validate(vertices);
                polygon = new List<Vector2>(vertices);
                return true;
            }
            polygon = null;
            return false;
        }

        private static void Validate(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 3)
            {
                throw new InvalidOperationException(
                    "A Ball King geometry provider returned fewer than "
                        + "three vertices");
            }
            for (int index = 0; index < vertices.Length; index++)
            {
                if (!SurfaceMotion.IsFinite(vertices[index]))
                {
                    throw new InvalidOperationException(
                        "A Ball King geometry provider returned a non-finite "
                            + "vertex");
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private BallKingGeometryProvider provider;

            internal Registration(BallKingGeometryProvider value)
            {
                provider = value;
            }

            public void Dispose()
            {
                lock (Sync)
                {
                    if (provider != null)
                    {
                        Providers.Remove(provider);
                        provider = null;
                    }
                }
            }
        }
    }
}
