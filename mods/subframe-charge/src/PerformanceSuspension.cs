using System;
namespace SubframeCharge
{
    // a temporary owner must never rewrite the player's saved preference
    internal static class PerformanceSuspension
    {
        private static int owners;
        internal static bool Active { get { return owners != 0; } }
        internal static IDisposable Acquire() { owners++; return new Lease(); }
        private sealed class Lease : IDisposable
        {
            private bool disposed;
            public void Dispose() { if (disposed) return; disposed=true; owners--; }
        }
    }
}
