using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace JKRuntime
{
    public sealed class ModuleStatus
    {
        public string Id { get; internal set; }
        public string Version { get; internal set; }
        public string State { get; internal set; }
        public string Detail { get; internal set; }
        public string Origin { get; internal set; }
        public string[] Requires { get; internal set; }
        public string[] Provides { get; internal set; }
        public string[] Before { get; internal set; }
        public string[] After { get; internal set; }
        public string[] ExclusiveResources { get; internal set; }
    }

    internal sealed class CapabilitySlot
    {
        internal CapabilityDefinition Definition;
        internal string Owner;
        internal object Service;
    }

    /// <summary>Game-thread level lifetime, declared capability access and reverse-order ownership. Do not retain after teardown.</summary>
    public sealed class ModuleContext
    {
        private readonly RuntimeKernel kernel;
        private readonly ModuleDefinition definition;
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private bool alive = true;
        internal bool Installing = true;
        public string ModuleId { get { return definition.Id; } }

        internal ModuleContext(RuntimeKernel host, ModuleDefinition module)
        { kernel = host; definition = module; }

        /// <summary>Own a successful registration or mutation immediately. Partial module activation releases all tracked resources.</summary>
        public T Track<T>(T lease) where T : IDisposable
        {
            Check();
            if (lease == null) throw new ArgumentNullException("lease");
            // Track immediately after each mutation, before another operation
            // can throw. Reverse disposal also covers a partially failed Install.
            leases.Add(RuntimeResources.Track(definition.Id, lease.GetType().FullName, lease));
            return lease;
        }
        /// <summary>Publish a declared capability during installation. Unpublished requirements prevent dependent activation.</summary>
        public void Publish(string id, object service)
        {
            Check();
            if (!Installing) throw new InvalidOperationException("Publish capabilities during Install only.");
            kernel.Publish(definition.Id, id, service);
        }
        public bool TryGetCapability(string id, out object service)
        {
            Check();
            foreach (CapabilityRequirement requirement in definition.Requires)
                if (requirement.Id == id) return kernel.TryGetCapability(id, requirement.Major, requirement.MinimumMinor, out service);
            throw new InvalidOperationException("Declare the capability requirement before resolving it: " + id);
        }
        /// <summary>Resolve a declared capability with version and managed-type checks. Throws if unavailable.</summary>
        public T Require<T>(string id) where T : class
        {
            object value;
            if (!TryGetCapability(id, out value) || !(value is T))
                throw new InvalidOperationException("Unavailable or incompatible service: " + id);
            return (T)value;
        }
        private void Check()
        {
            kernel.CheckThread();
            if (!alive) throw new ObjectDisposedException("ModuleContext", "This level has ended.");
        }
        internal void Release(List<string> errors)
        {
            alive = false;
            for (int i = leases.Count - 1; i >= 0; i--)
                try { leases[i].Dispose(); }
                catch (Exception error) { errors.Add(definition.Id + " undo: " + error); }
            leases.Clear();
        }
    }

    internal sealed class ActionLease : IDisposable
    {
        private Action dispose;
        internal ActionLease(Action action) { dispose = action; }
        public void Dispose()
        {
            // Keep the callback if it refuses an unsafe, reentrant disposal.
            if (dispose == null) return;
            dispose(); dispose = null;
        }
    }

    // Per-module transactions within one level graph. Built-in UI remains
    // available to diagnose rejected feature modules.
    internal sealed class RuntimeKernel
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly SortedDictionary<string, ModuleDefinition> modules = new SortedDictionary<string, ModuleDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CapabilitySlot> builtins = new Dictionary<string, CapabilitySlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, CapabilitySlot> capabilities = new Dictionary<string, CapabilitySlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, ModuleStatus> states = new Dictionary<string, ModuleStatus>(StringComparer.Ordinal);
        private readonly List<ModuleContext> installed = new List<ModuleContext>();
        private readonly HashSet<string> starting = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> errors = new List<string>();
        private string[] order = new string[0];
        private bool busy;
        private bool sealedLevel;
        private bool cleanupFailed;
        internal string State { get; private set; }
        internal RuntimeKernel() { State = "idle"; }

        internal void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread)
                throw new InvalidOperationException("JK Runtime APIs must be called on the game thread.");
        }
        private void CheckMutable()
        {
            CheckThread();
            if (busy || sealedLevel) throw new InvalidOperationException("Module registrations are frozen until level teardown.");
        }
        internal IDisposable Register(ModuleDefinition module)
        {
            CheckMutable();
            if (module == null) throw new ArgumentNullException("module");
            if (module.Id == "jk.runtime" || module.Id.StartsWith("jk.runtime.", StringComparison.Ordinal))
                throw new InvalidOperationException("The jk.runtime module ID prefix is reserved.");
            if (modules.ContainsKey(module.Id)) throw new InvalidOperationException("Duplicate module: " + module.Id);
            modules.Add(module.Id, module);
            module.Registered = true;
            states[module.Id] = Status(module, "registered", "");
            return new ActionLease(delegate { CheckMutable(); modules.Remove(module.Id); states.Remove(module.Id); });
        }
        internal void AddBuiltin(CapabilityDefinition definition, object service)
        {
            CheckMutable();
            if (service == null) throw new ArgumentNullException("service");
            builtins.Add(definition.Id, new CapabilitySlot { Definition = definition, Owner = "jk.runtime", Service = service });
        }
        internal bool TryGetCapability(string id, int major, int minimumMinor, out object service)
        {
            CheckThread();
            service = null;
            CapabilitySlot slot;
            if (!capabilities.TryGetValue(id ?? "", out slot) || slot.Service == null
                || slot.Definition.Major != major || slot.Definition.Minor < minimumMinor) return false;
            service = slot.Service; return true;
        }
        internal void Publish(string owner, string id, object service)
        {
            CapabilitySlot slot;
            if (!capabilities.TryGetValue(id ?? "", out slot) || slot.Owner != owner)
                throw new InvalidOperationException("Undeclared capability: " + id);
            if (service == null || slot.Service != null) throw new InvalidOperationException("Publish a non-null service exactly once: " + id);
            slot.Service = service;
        }
        internal ModuleStatus[] GetModules()
        {
            CheckThread();
            var result = new List<ModuleStatus>();
            foreach (string id in modules.Keys)
            {
                ModuleStatus s = states[id];
                result.Add(Status(modules[id], s.State, s.Detail));
            }
            return result.ToArray();
        }
        internal string[] Errors { get { return errors.ToArray(); } }
        internal string[] Order { get { return (string[])order.Clone(); } }

        internal void Prepare(Action<string[]> prepare)
        {
            CheckMutable(); busy = true; errors.Clear();
            try { var plan = Resolve(); capabilities.Clear(); prepare(plan); }
            finally { capabilities.Clear(); busy = false; }
        }
        internal void ValidateMapPolicy()
        { CheckThread(); if (!MapPolicy.Empty) MapPolicy.Validate(modules.Values, MapPolicy.HasForeignRules ? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name) : Enumerable.Empty<string>()); }

        internal bool Activate()
        {
            CheckMutable();
            busy = true; sealedLevel = true; errors.Clear(); order = new string[0];
            try
            {
                order = Resolve();
                State = "installing";
                foreach (string id in order)
                {
                    ModuleDefinition definition = modules[id];
                    bool unavailable = false;
                    foreach (var required in definition.Requires)
                    {
                        object service;
                        if (!required.Optional && !TryGetCapability(required.Id, required.Major, required.MinimumMinor, out service))
                        { Reject(id, "Required provider failed: " + required.Id); unavailable = true; break; }
                    }
                    if (unavailable) continue;
                    var context = new ModuleContext(this, definition);
                    installed.Add(context);
                    try
                    {
                        states[id] = Status(definition, "installing", "");
                        using (StartupTrace.Measure("module.install:" + id)) definition.Install(context);
                        foreach (CapabilityDefinition provided in definition.Provides)
                            if (capabilities[provided.Id].Service == null)
                                throw new InvalidOperationException(id + " did not publish " + provided.Id);
                        context.Installing = false;
                        starting.Add(id);
                        states[id] = Status(definition, "starting", "");
                        if (definition.Start != null)
                            using (StartupTrace.Measure("module.start:" + id)) definition.Start(context);
                        states[id] = Status(definition, "active", "");
                    }
                    catch (Exception error)
                    {
                        Reject(id, error.ToString());
                        int count = errors.Count;
                        if (starting.Contains(id) && definition.Stop != null)
                            try { definition.Stop(context); } catch (Exception cleanup) { errors.Add(id + " stop: " + cleanup); }
                        context.Release(errors);
                        foreach (var provided in definition.Provides) capabilities[provided.Id].Service = null;
                        installed.Remove(context); starting.Remove(id);
                        if (errors.Count != count)
                        {
                            cleanupFailed = true;
                            Teardown();
                            State = "cleanup-failed";
                            return false;
                        }
                    }
                }
                bool rejected = states.Values.Any(s => s.State == "rejected");
                State = !rejected ? "active" : installed.Count == 0 ? "rejected" : "degraded";
                return !rejected;
            }
            catch (Exception error)
            {
                errors.Add("kernel: " + error); Teardown();
                State = cleanupFailed ? "cleanup-failed" : "rejected";
                return false;
            }
            finally { busy = false; }
        }

        private void Reject(string id, string reason)
        {
            if (states[id].State == "rejected") return;
            states[id] = Status(modules[id], "rejected", reason);
            errors.Add(id + ": " + reason);
        }
        internal void Deactivate()
        {
            CheckThread();
            if (busy) throw new InvalidOperationException("Reentrant runtime lifecycle.");
            if (!sealedLevel) return;
            busy = true;
            try
            {
                Teardown();
                foreach (ModuleDefinition module in modules.Values)
                    states[module.Id] = Status(module, cleanupFailed ? "cleanup-failed" : "registered",
                        cleanupFailed ? "Cleanup failed; restart the game before reactivation." : "Level ended; awaiting next activation.");
                // If an undo failed, do not promise restoration or install a
                // second set of modules over possibly unrecovered state.
                State = cleanupFailed ? "cleanup-failed" : "idle";
                sealedLevel = cleanupFailed;
                order = new string[0];
            }
            finally { busy = false; }
        }
        private void Teardown()
        {
            int errorCount = errors.Count;
            for (int i = installed.Count - 1; i >= 0; i--)
            {
                ModuleContext context = installed[i];
                ModuleDefinition definition = modules[context.ModuleId];
                if (starting.Contains(definition.Id) && definition.Stop != null)
                    try { definition.Stop(context); }
                    catch (Exception error) { errors.Add(definition.Id + " stop: " + error); }
                context.Release(errors);
                foreach (CapabilityDefinition provided in definition.Provides)
                    capabilities[provided.Id].Service = null;
            }
            installed.Clear(); starting.Clear(); capabilities.Clear();
            if (errors.Count != errorCount) cleanupFailed = true;
        }

        private string[] Resolve()
        {
            ValidateMapPolicy();
            capabilities.Clear();
            foreach (var pair in builtins)
                capabilities.Add(pair.Key, new CapabilitySlot { Definition = pair.Value.Definition, Owner = pair.Value.Owner, Service = pair.Value.Service });
            var claims = new Dictionary<string, string>(StringComparer.Ordinal);
            var edges = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (ModuleDefinition module in modules.Values)
            {
                states[module.Id] = Status(module, "registered", "");
                edges.Add(module.Id, new SortedSet<string>(StringComparer.Ordinal));
                if (MapPolicy.Suspends(module.Id)) states[module.Id] = Status(module, "map-disabled", "Suspended by map policy");
            }
            foreach (ModuleDefinition module in modules.Values)
            {
                if (MapPolicy.Suspends(module.Id)) continue;
                foreach (CapabilityDefinition capability in module.Provides)
                {
                    CapabilitySlot previous;
                    if (capabilities.TryGetValue(capability.Id, out previous))
                    {
                        Reject(module.Id, "Conflicting capability: " + capability.Id);
                        if (previous.Owner != "jk.runtime") Reject(previous.Owner, "Conflicting capability: " + capability.Id);
                    }
                    else capabilities.Add(capability.Id, new CapabilitySlot { Definition = capability, Owner = module.Id });
                }
                foreach (string resource in module.ExclusiveResources)
                {
                    string previous;
                    if (claims.TryGetValue(resource, out previous))
                    { Reject(module.Id, "Exclusive resource: " + resource); Reject(previous, "Exclusive resource: " + resource); }
                    else claims.Add(resource, module.Id);
                }
            }
            foreach (ModuleDefinition module in modules.Values)
            {
                if (MapPolicy.Suspends(module.Id)) continue;
                foreach (string before in module.Before)
                    if (!modules.ContainsKey(before)) { if (!module.OptionalOrderingTargets) Reject(module.Id, "Missing ordering target: " + before); }
                    else edges[module.Id].Add(before);
                foreach (string after in module.After)
                    if (!modules.ContainsKey(after)) { if (!module.OptionalOrderingTargets) Reject(module.Id, "Missing ordering target: " + after); }
                    else edges[after].Add(module.Id);
                foreach (CapabilityRequirement requirement in module.Requires)
                {
                    CapabilitySlot provider;
                    if (!capabilities.TryGetValue(requirement.Id, out provider) || !requirement.Accepts(provider.Definition))
                    {
                        string reason = "needs " + requirement.Id + " " + requirement.Major + "." + requirement.MinimumMinor + "+ (same major)";
                        if (!requirement.Optional) Reject(module.Id, reason);
                        else errors.Add("optional: " + module.Id + " " + reason);
                        continue;
                    }
                    if (provider.Owner != "jk.runtime") edges[provider.Owner].Add(module.Id);
                }
            }
            bool changed;
            do
            {
                changed = false;
                foreach (var module in modules.Values)
                {
                    if (states[module.Id].State == "rejected" || states[module.Id].State == "map-disabled") continue;
                    foreach (var required in module.Requires.Where(r => !r.Optional))
                    {
                        CapabilitySlot provider;
                        if (capabilities.TryGetValue(required.Id, out provider) && provider.Owner != "jk.runtime" && states[provider.Owner].State == "rejected")
                        { Reject(module.Id, "Rejected dependency: " + provider.Owner); changed = true; break; }
                    }
                }
            } while (changed);
            var incoming = modules.Keys.Where(id => states[id].State != "rejected" && states[id].State != "map-disabled").ToDictionary(id => id, id => 0);
            foreach (var from in incoming.Keys.ToArray())
                foreach (var to in edges[from]) if (incoming.ContainsKey(to)) incoming[to]++;
            var ready = new SortedSet<string>(incoming.Where(p => p.Value == 0).Select(p => p.Key), StringComparer.Ordinal);
            var sorted = new List<string>();
            while (ready.Count != 0)
            {
                string id = ready.Min; ready.Remove(id); sorted.Add(id);
                foreach (string next in edges[id]) if (incoming.ContainsKey(next) && --incoming[next] == 0) ready.Add(next);
            }
            foreach (var id in incoming.Keys) if (incoming[id] != 0) Reject(id, "Dependency/order cycle or blocked by cycle");
            MapPolicy.RequireAvailable(sorted);
            return sorted.ToArray();
        }
        private static ModuleStatus Status(ModuleDefinition module, string state, string detail)
        {
            var required = new List<string>();
            foreach (var item in module.Requires) required.Add(item.Id + ":" + item.Major + ":" + item.MinimumMinor + (item.Optional ? " (optional)" : ""));
            var provided = new List<string>();
            foreach (var item in module.Provides) provided.Add(item.Id + ":" + item.Major + ":" + item.Minor);
            return new ModuleStatus { Id = module.Id, Version = module.Version.ToString(), State = state, Detail = detail,
                Origin = module.Origin,
                Requires = required.ToArray(), Provides = provided.ToArray(),
                Before = new List<string>(module.Before).ToArray(), After = new List<string>(module.After).ToArray(),
                ExclusiveResources = new List<string>(module.ExclusiveResources).ToArray() };
        }
    }
}
