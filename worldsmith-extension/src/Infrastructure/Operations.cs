using System;

namespace WorldsmithExtension
{
    // one job at a time if it changes anything, read-only news and update checks can run alongside
    internal sealed class OperationCoordinator
    {
        readonly object gate = new object();
        string current;
        internal bool Busy { get { lock (gate) return current != null; } }
        internal IDisposable Enter(string name)
        {
            lock (gate)
            {
                if (current != null) throw new InvalidOperationException("Wait for " + current + " to finish before starting " + name + ".");
                current = name;
                return new Lease(this);
            }
        }
        sealed class Lease : IDisposable
        {
            OperationCoordinator owner;
            internal Lease(OperationCoordinator value) { owner = value; }
            public void Dispose()
            {
                var value = System.Threading.Interlocked.Exchange(ref owner, null);
                if (value != null) lock (value.gate) value.current = null;
            }
        }
    }

    internal static class Operations
    {
        internal static readonly OperationCoordinator Current = new OperationCoordinator();
    }
}
