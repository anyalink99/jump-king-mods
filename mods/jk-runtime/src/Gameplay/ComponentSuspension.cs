using System;
using System.Collections.Generic;
using EntityComponent;

namespace JKRuntime.Gameplay
{
    /// <summary>Cooperating owners suspend components without prematurely releasing each other.</summary>
    public static class ComponentSuspension
    {
        private sealed class Entry { internal Component Component; internal bool RestoreEnabled, Transitioning; internal int Holders; }
        private static readonly List<Entry> entries = new List<Entry>();
        public static IDisposable Acquire(string owner, Component component, bool? restoredEnabled = null)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (component == null) throw new ArgumentNullException("component");
            Entry entry = entries.Find(e => ReferenceEquals(e.Component, component));
            bool added = entry == null;
            if (added) entry = new Entry { Component = component, RestoreEnabled = restoredEnabled ?? component.Enabled };
            if (entry.Transitioning) throw new InvalidOperationException("Reentrant component suspension transition");
            if (added) entries.Add(entry);
            entry.Transitioning = true;
            try { component.Enabled = false; }
            catch { if (added) entries.Remove(entry); throw; }
            finally { entry.Transitioning = false; }
            entry.Holders++;
            return RuntimeResources.Track(owner, "component-suspension", new ActionLease(delegate {
                RuntimeApi.Kernel.CheckThread();
                if (entry.Transitioning) throw new InvalidOperationException("Reentrant component suspension release");
                if (entry.Holders == 1)
                {
                    entry.Transitioning = true;
                    try { entry.Component.Enabled = entry.RestoreEnabled; entries.Remove(entry); }
                    finally { entry.Transitioning = false; }
                }
                entry.Holders--;
            }));
        }
        public static bool IsSuspended(Component component)
        { RuntimeApi.Kernel.CheckThread(); return entries.Exists(e => ReferenceEquals(e.Component, component)); }
    }
}
