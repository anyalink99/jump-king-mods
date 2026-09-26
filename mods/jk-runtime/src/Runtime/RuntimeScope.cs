using System;
using System.Collections.Generic;

namespace JKRuntime
{
    // Used identically for process registrations, level objects, UI pages and
    // temporary operations. Disposing a parent ends children in reverse order.
    /// <summary>Reverse-order resource scope. Failed releases remain retryable; adding resources after teardown starts is rejected. Not thread-safe.</summary>
    public sealed class RuntimeScope : IDisposable
    {
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private bool disposed, closing, releasing;
        /// <summary>Own a resource immediately after acquisition. Returns the same instance; ownership does not change its API.</summary>
        public T Own<T>(T lease) where T : IDisposable
        {
            if (closing) throw new ObjectDisposedException("RuntimeScope");
            if (lease == null) throw new ArgumentNullException("lease");
            leases.Add(lease); return lease;
        }
        /// <summary>Run a cleanup action during reverse-order disposal. A throwing action remains pending for a retry.</summary>
        public IDisposable Defer(Action release) { return Own(new ActionLease(release)); }
        /// <summary>Create a nested scope whose lifetime cannot exceed this scope.</summary>
        public RuntimeScope Child() { return Own(new RuntimeScope()); }
        public void Dispose()
        {
            if (disposed) return;
            if (releasing) throw new InvalidOperationException("Reentrant scope disposal");
            closing = releasing = true;
            var errors = new List<Exception>();
            try
            {
                for (int i = leases.Count - 1; i >= 0; i--)
                    try { leases[i].Dispose(); leases.RemoveAt(i); } catch (Exception error) { errors.Add(error); }
            }
            finally { releasing = false; }
            if (errors.Count != 0) throw new AggregateException("Runtime scope cleanup incomplete", errors);
            disposed = true;
        }
    }
}
