using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    /// <summary>Native pause state and timestamped observations, including ticks while the player is paused.</summary>
    public static class NativePause
    {
        private static readonly Type Type = typeof(PlayerEntity).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        private static readonly FieldInfo Instance = Type.GetField("instance");
        private static readonly PropertyInfo Paused = Type.GetProperty("IsPaused");
        private sealed class Listener { internal string Owner; internal Action<bool, long> Callback; }
        private static readonly List<Listener> listeners = new List<Listener>();
        private sealed class Pump : Component { protected override void Update(float delta) { Observe(); } }
        private static Entity pumpOwner;
        private static Pump pump;
        internal static Entity Manager { get { ValidateContract(); return Instance.GetValue(null) as Entity; } }
        internal static bool Requested { get { return listeners.Count != 0; } }
        public static void ValidateContract()
        { if (Instance == null || Paused == null || Paused.PropertyType != typeof(bool)) throw new NotSupportedException("Native pause contract unavailable"); }
        public static bool IsPaused
        { get { RuntimeApi.Kernel.CheckThread(); var manager = Manager; return manager != null && (bool)Paused.GetValue(manager, null); } }
        public static IDisposable Subscribe(string owner, Action<bool, long> callback)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (callback == null) throw new ArgumentNullException("callback");
            var entry = new Listener { Owner = owner, Callback = callback };
            Entity manager = Manager;
            if (manager != null)
            {
                callback(IsPaused, Stopwatch.GetTimestamp());
                if (!ReferenceEquals(manager, pumpOwner))
                { if (pump != null) pump.Enabled = false; pumpOwner = manager; pump = new Pump(); manager.AddComponents(pump); }
                pump.Enabled = true;
            }
            listeners.Add(entry);
            return RuntimeResources.Track(owner, "native-pause", new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); listeners.Remove(entry); if (listeners.Count == 0 && pump != null) pump.Enabled = false; }));
        }
        internal static void Observe()
        {
            if (!Requested) return;
            bool value = IsPaused; long now = Stopwatch.GetTimestamp();
            foreach (var listener in listeners.ToArray())
                if (!RuntimeJournal.Observe(listener.Owner, "native-pause", () => listener.Callback(value, now))) listeners.Remove(listener);
            if (listeners.Count == 0 && pump != null) pump.Enabled = false;
        }
    }
}
