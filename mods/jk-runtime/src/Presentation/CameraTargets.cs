using System;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    /// <summary>A world-space camera subject. Screen is zero-based; velocity is pixels per second.</summary>
    public struct CameraTarget
    {
        public Vector2 Center { get; private set; }
        public Vector2 Velocity { get; private set; }
        public int Screen { get; private set; }
        public bool Paused { get; private set; }
        public long Revision { get; internal set; }
        public long Discontinuity { get; internal set; }

        public CameraTarget(Vector2 center, Vector2 velocity, int screen, bool paused) : this()
        { Center = center; Velocity = velocity; Screen = screen; Paused = paused; }

        internal void Validate()
        {
            if (!Finite(Center.X) || !Finite(Center.Y) || !Finite(Velocity.X) || !Finite(Velocity.Y))
                throw new ArgumentException("Camera target coordinates and velocity must be finite");
            if (Screen < 0) throw new ArgumentOutOfRangeException("Screen");
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    /// <summary>Exclusive, scoped presentation target. Does not move bodies or change the native camera.</summary>
    public static class CameraTargets
    {
        private static Lease active;
        private static long revision, discontinuity;

        /// <summary>Acquire one owner for a session. A competing owner is rejected; the scope releases ownership.</summary>
        public static Lease Acquire(string owner, RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();
            owner = ModuleDefinition.ValidId(owner);
            if (scope == null) throw new ArgumentNullException("scope");
            if (active != null) throw new InvalidOperationException("Camera target already owned by " + active.Owner);
            var lease = new Lease(owner);
            try { scope.Own(lease); }
            catch { lease.Dispose(); throw; }
            active = lease;
            return lease;
        }

        /// <summary>Read a value snapshot. No target means the consumer should follow its normal live subject.</summary>
        public static bool TryRead(out CameraTarget target)
        {
            RuntimeApi.Kernel.CheckThread();
            target = active == null ? default(CameraTarget) : active.Target;
            return active != null && active.HasTarget;
        }

        public sealed class Lease : IDisposable
        {
            public string Owner { get; private set; }
            internal CameraTarget Target;
            internal bool HasTarget;
            private IDisposable registration;
            private bool disposed;

            internal Lease(string owner)
            {
                Owner = owner;
                registration = RuntimeResources.Track(owner, "camera-target", new ActionLease(delegate {
                    disposed = true;
                    if (ReferenceEquals(active, this)) active = null;
                }));
            }

            /// <summary>Publish before drawing the frame. Mark seeks and other cuts even while paused.</summary>
            public void Publish(CameraTarget target, bool cut = false)
            {
                RuntimeApi.Kernel.CheckThread();
                if (disposed) throw new ObjectDisposedException("CameraTargets.Lease");
                target.Validate();
                target.Revision = ++revision;
                target.Discontinuity = !HasTarget || cut ? ++discontinuity : Target.Discontinuity;
                Target = target;
                HasTarget = true;
            }

            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (registration == null) return;
                registration.Dispose(); registration = null;
            }
        }
    }
}
