using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace JKRuntime.Simulation
{
    // No game-loop subscription, worker, timer or automatic capture. A registry
    // is inert until a consumer explicitly opens and advances a session.
    public sealed class SimulationRegistry
    {
        private readonly Dictionary<string, SimulationProvider> providers = new Dictionary<string, SimulationProvider>(StringComparer.Ordinal);
        private readonly HashSet<SimulationSession> sessions = new HashSet<SimulationSession>();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private bool opening;
        public int ActiveSessions { get { CheckThread(); return sessions.Count; } }
        /// <summary>Register versioned simulation coverage without calling provider code. Registry changes invalidate open sessions.</summary>
        public IDisposable Register(SimulationProvider provider)
        {
            CheckThread(); if (provider == null) throw new ArgumentNullException("provider");
            if (providers.ContainsKey(provider.Id)) throw new InvalidOperationException("Duplicate simulation provider: " + provider.Id);
            Invalidate(); providers.Add(provider.Id, provider);
            return new Registration(this, provider.Id);
        }
        public string[] CheckCoverage(IEnumerable<SimulationRequirement> requirements)
        { CheckThread(); List<string> errors; Resolve(requirements, out errors); return errors.ToArray(); }
        /// <summary>Resolve exact coverage and dependencies, then capture isolated state. Fails before capture on unsupported coverage.</summary>
        public SimulationSession Open(SimulationSeed seed)
        {
            CheckThread(); if (seed == null) throw new ArgumentNullException("seed");
            List<string> errors; var order = Resolve(seed.Requirements, out errors);
            if (errors.Count != 0) throw new NotSupportedException(string.Join("; ", errors));
            if (opening) throw new InvalidOperationException("Recursive simulation capture");
            opening = true;
            try
            {
                var session = new SimulationSession(seed, order, delegate(SimulationSession value) { sessions.Remove(value); });
                sessions.Add(session); return session;
            }
            finally { opening = false; }
        }
        private SimulationProvider[] Resolve(IEnumerable<SimulationRequirement> requirements, out List<string> errors)
        {
            errors = new List<string>(); var selected = new Dictionary<string, SimulationProvider>(StringComparer.Ordinal);
            foreach (var requirement in requirements ?? new SimulationRequirement[0])
            {
                if (requirement == null) { errors.Add("Null simulation requirement"); continue; }
                var owners = providers.Values.Where(p => p.Claims.Any(c => c.Key == requirement.Key)).ToArray();
                if (owners.Length != 1) errors.Add((owners.Length == 0 ? "Unsupported: " : "Conflicting providers: ") + requirement.Key);
                else selected[owners[0].Id] = owners[0];
            }
            var pending = new Queue<SimulationProvider>(selected.Values);
            while (pending.Count != 0)
            {
                foreach (string dependency in pending.Dequeue().After)
                {
                    SimulationProvider provider;
                    if (!providers.TryGetValue(dependency, out provider)) { errors.Add("Missing simulation dependency: " + dependency); continue; }
                    if (!selected.ContainsKey(dependency)) { selected.Add(dependency, provider); pending.Enqueue(provider); }
                }
            }
            foreach (var claim in selected.Values.SelectMany(p => p.Claims.Select(c => c.Id)).GroupBy(c => c))
                if (claim.Count() > 1) errors.Add("Overlapping simulation claims: " + claim.Key);
            var result = new List<SimulationProvider>();
            while (selected.Count != 0)
            {
                var ready = selected.Values.Where(p => p.After.All(id => result.Any(r => r.Id == id))).OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
                if (ready == null) { errors.Add("Simulation dependency cycle or unresolved order"); break; }
                result.Add(ready); selected.Remove(ready.Id);
            }
            if (result.Count == 0 && errors.Count == 0) errors.Add("No simulation providers selected");
            return result.ToArray();
        }
        public void Invalidate()
        {
            CheckThread(); if (opening) throw new InvalidOperationException("Provider registry changed during capture");
            foreach (var session in sessions.ToArray()) session.Dispose();
        }
        private void CheckThread()
        { if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Simulation registry is owner-thread only"); }
        private sealed class Registration : IDisposable
        {
            private SimulationRegistry registry; private readonly string id;
            internal Registration(SimulationRegistry value, string key) { registry = value; id = key; }
            public void Dispose()
            {
                if (registry == null) return;
                registry.CheckThread(); registry.Invalidate(); registry.providers.Remove(id); registry = null;
            }
        }
    }
}
