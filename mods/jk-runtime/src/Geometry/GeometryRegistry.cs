using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    public delegate bool GeometryProvider(IBlock block, out Vector2[] worldVertices);

    /// <summary>Explicit actor/operation profiles. No profile can replace global native collision.</summary>
    public sealed class GeometryRegistry
    {
        private sealed class Entry
        {
            internal string Owner, Profile;
            internal int Version;
            internal GeometryProvider Provider;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private bool querying;
        public long Generation { get; private set; }

        public IDisposable Register(string owner, string profile, int version, GeometryProvider provider)
        {
            CheckMutation();
            owner = ModuleDefinition.ValidId(owner);
            profile = ModuleDefinition.ValidId(profile);
            if (version < 1 || provider == null) throw new ArgumentException("Version and provider required");
            foreach (var entry in entries)
                if (entry.Owner == owner && entry.Profile == profile && entry.Version == version)
                    throw new InvalidOperationException("Geometry provider already registered: " + owner + "/" + profile);
            var value = new Entry { Owner = owner, Profile = profile, Version = version, Provider = provider };
            entries.Add(value);
            Generation++;
            return RuntimeResources.Track(owner, "geometry:" + profile, new ActionLease(delegate { CheckMutation(); if (entries.Remove(value)) Generation++; }));
        }

        public bool TryGetPolygon(string profile, int version, IBlock block, out Vector2[] vertices)
        {
            RuntimeApi.Kernel.CheckThread();
            ModuleDefinition.ValidId(profile);
            if (block == null || version < 1) throw new ArgumentException("Block and profile version required");
            if (querying) throw new InvalidOperationException("Recursive geometry query");
            querying = true;
            vertices = null;
            string owner = null;
            try
            {
                foreach (Entry entry in entries)
                {
                    if (entry.Profile != profile || entry.Version != version) continue;
                    Vector2[] candidate;
                    if (!entry.Provider(block, out candidate)) continue;
                    if (owner != null) throw new InvalidOperationException("Conflicting geometry: " + owner + " and " + entry.Owner);
                    Validate(candidate);
                    vertices = (Vector2[])candidate.Clone();
                    owner = entry.Owner;
                }
                return owner != null;
            }
            finally { querying = false; }
        }

        public void Invalidate()
        {
            CheckMutation();
            Generation++;
        }

        private void CheckMutation()
        {
            RuntimeApi.Kernel.CheckThread();
            if (querying) throw new InvalidOperationException("Geometry registration changed during query");
        }

        private static void Validate(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 3 || vertices.Length > 4096)
                throw new InvalidOperationException("Geometry provider returned an invalid polygon size");
            foreach (Vector2 point in vertices)
                if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y))
                    throw new InvalidOperationException("Geometry provider returned non-finite vertices");
        }
    }
}
