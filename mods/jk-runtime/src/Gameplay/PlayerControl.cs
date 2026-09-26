using System;
using System.Collections.Generic;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    /// <summary>Exclusive movement operations on a particular body. Acquisition never preempts another owner or changes gameplay; callers choose an explicit denied/suspended outcome.</summary>
    public static class PlayerControl
    {
        private static readonly Dictionary<BodyComp, Lease> active = new Dictionary<BodyComp, Lease>();
        public static string Owner(BodyComp body)
        {
            RuntimeApi.Kernel.CheckThread();
            Lease lease; return body != null && active.TryGetValue(body, out lease) ? lease.Owner : null;
        }
        /// <summary>Memory-only query for additive participants and an operation's own continuation.</summary>
        public static bool Available(BodyComp body, string owner)
        { string current = Owner(body); return current == null || current == owner; }
        public static bool TryAcquire(BodyComp body, string owner, out Lease lease)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (body == null) throw new ArgumentNullException("body");
            lease = null;
            if (active.ContainsKey(body)) return false;
            lease = new Lease(body, owner); active.Add(body, lease); return true;
        }
        public static Lease Acquire(BodyComp body, string owner)
        {
            Lease lease;
            if (!TryAcquire(body, owner, out lease)) throw new InvalidOperationException("Player control is owned by " + Owner(body) + "; release that operation before enabling " + owner);
            return lease;
        }
        public sealed class Lease : IDisposable
        {
            private BodyComp body;
            public string Owner { get; private set; }
            internal Lease(BodyComp value, string owner) { body = value; Owner = owner; }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread(); if (body == null) return;
                Lease current;
                if (!active.TryGetValue(body, out current) || !ReferenceEquals(current, this)) throw new InvalidOperationException("Player control ownership changed externally");
                active.Remove(body); body = null;
            }
        }
    }
}
