using System;
using System.Collections.Generic;

namespace JKRuntime.Gameplay
{
    public static partial class MapMechanics
    {
        private sealed class Watcher { internal Func<int> Read; internal Action Apply; internal int Value; internal bool Active = true; }
        private static readonly List<Watcher> watchers = new List<Watcher>();
        private static bool updating;
        /// <summary>Reconcile controller changes at a safe entity boundary, never from Draw or a body callback</summary>
        public static IDisposable Watch(string owner, Func<int> state, Action apply)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (state == null || apply == null || updating) throw new InvalidOperationException("Invalid transition observer");
            var value = new Watcher { Read = state, Apply = apply, Value = state() }; watchers.Add(value);
            return RuntimeResources.Track(owner, "map-mechanic-transition", new ActionLease(delegate { value.Active = false; if (!updating) watchers.Remove(value); }));
        }
        internal static void BeforePlayer()
        {
            if (watchers.Count == 0) return;
            if (updating) throw new InvalidOperationException("Reentrant mechanic transition");
            updating = true;
            try { foreach (var watcher in watchers) if (watcher.Active) { int value = watcher.Read(); if (value != watcher.Value) { watcher.Apply(); watcher.Value = value; } } }
            finally { updating = false; watchers.RemoveAll(w => !w.Active); }
        }
        /// <summary>Import a parsed legacy screen rule into this attempt, with the same conflict checks as common XML</summary>
        public static void ImportScreen(string id, int screen, MapMechanicMode mode, string source)
        {
            RuntimeApi.Kernel.CheckThread();
            if (screen < 0 || !Enum.IsDefined(typeof(MapMechanicMode), mode)) throw new ArgumentException("Invalid imported screen rule");
            if (!providers.ContainsKey(id)) throw new InvalidOperationException("Register provider before importing rules");
            Dictionary<int, Rule> table;
            if (!screens.TryGetValue(id, out table)) screens.Add(id, table = new Dictionary<int, Rule>());
            var rule = new Rule { Id = id, Screen = screen, Mode = mode, Source = source }; Rule old;
            if (table.TryGetValue(screen, out old)) Merge(old, rule); else table.Add(screen, rule);
            Generation++;
        }
    }
}
