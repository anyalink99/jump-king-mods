using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JKRuntime.State;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    public enum GameplayEventKind { ChargeStarted, ChargeEnded, Jump, Landed, SupportLost, Teleported, ScreenChanged, PauseChanged, RestoreStarted, RestoreCompleted, RestoreFailed }
    public sealed class GameplayEvent
    {
        public long Sequence { get; internal set; }
        public long Tick { get; internal set; }
        public long Timestamp { get; internal set; }
        public AttemptStamp Attempt { get; internal set; }
        public long RestoreEpoch { get; internal set; }
        public GameplayEventKind Kind { get; internal set; }
        public string Source { get; internal set; }
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public int Screen { get; internal set; }
        public bool Paused { get; internal set; }
        public JumpResult Jump { get; internal set; }
    }
    /// <summary>Ordered game-thread observations. Subscribers never own or cancel the observed action.</summary>
    public static class GameplayEvents
    {
        private sealed class Listener { internal string Owner; internal Action<GameplayEvent> Callback; internal bool Active = true; }
        private static readonly List<Listener> listeners = new List<Listener>();
        private static long sequence, tick;
        private static AttemptStamp attempt;
        private static bool delivering;
        internal static bool Requested { get { return listeners.Any(l => l.Active); } }
        /// <summary>Receive ordered observations on the game thread. A throwing listener is disabled; dispose the lease at level teardown.</summary>
        public static IDisposable Subscribe(string owner, Action<GameplayEvent> callback)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (callback == null) throw new ArgumentNullException("callback");
            var listener = new Listener { Owner = owner, Callback = callback }; listeners.Add(listener);
            return RuntimeResources.Track(owner, "gameplay-events", new ActionLease(delegate {
                RuntimeApi.Kernel.CheckThread(); listener.Active = false; listeners.Remove(listener);
            }));
        }
        internal static void BeginTick(AttemptStamp value) { tick++; attempt = value; }
        internal static void Emit(GameplayEventKind kind, string source, Vector2 position, Vector2 velocity, int screen, JumpResult jump = null, bool paused = false)
        {
            RuntimeApi.Kernel.CheckThread();
            if (!Requested) return;
            if (delivering) throw new InvalidOperationException("Gameplay observers cannot recursively publish observations");
            var value = new GameplayEvent { Sequence = ++sequence, Tick = tick, Timestamp = Stopwatch.GetTimestamp(), Attempt = attempt,
                RestoreEpoch = GameState.Snapshots.RestoreEpoch, Kind = kind, Source = source, Position = position, Velocity = velocity,
                Screen = screen, Jump = jump, Paused = paused };
            RuntimeJournal.Record(source, kind.ToString(), "tick=" + tick + " screen=" + screen);
            delivering = true;
            try { foreach (var listener in listeners.ToArray()) if (listener.Active)
                if (!RuntimeJournal.Observe(listener.Owner, "gameplay-event:" + kind, () => listener.Callback(value))) listener.Active = false; }
            finally { delivering = false; }
        }
        /// <summary>Declare a real relocation at its commit point; never infer teleports from velocity thresholds.</summary>
        public static void NotifyTeleport(string owner, Vector2 position, Vector2 velocity, int screen)
        { ModuleDefinition.ValidId(owner); Emit(GameplayEventKind.Teleported, owner, position, velocity, screen); }
    }
}
