using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    public enum PlayerUpdatePhase { BeforeInput, AfterInput }

    /// <summary>Callbacks around the native input slot, without adding or moving player components.</summary>
    public static class PlayerUpdates
    {
        private static readonly FieldInfo Components = typeof(Entity).GetField("m_components", OwnedPatches.Members);
        private static readonly FieldInfo Brain = typeof(PlayerEntity).GetField("m_bt", OwnedPatches.Members);
        private static readonly MethodInfo LowUpdate = typeof(Component).GetMethod("LowUpdate", OwnedPatches.Members);
        private static readonly Action<Component, float> RunNative = (Action<Component, float>)Delegate.CreateDelegate(typeof(Action<Component, float>), LowUpdate);
        private static readonly Dictionary<PlayerEntity, Actor> actors = new Dictionary<PlayerEntity, Actor>();
        private static OwnedPatches hooks;
        private static int preparations, dispatching;
        internal sealed class Actor
        {
            internal PlayerEntity Player;
            internal InputComponent Input;
            internal bool Dispatching;
            internal Component Body, Brain;
            internal readonly List<Registration> Entries = new List<Registration>();
            internal Registration[] Snapshot = new Registration[0];
        }
        private static void Install()
        {
            if (hooks != null) return;
            if (Components == null || Brain == null) throw new NotSupportedException("Native player update contract unavailable");
            var pending = new OwnedPatches("jk-runtime.player-updates");
            try
            {
                pending.ReplaceCalls(typeof(Entity).GetMethod("UpdateComponents"), LowUpdate,
                    typeof(PlayerUpdates).GetMethod("UpdateComponent", OwnedPatches.Members), 1);
                pending.Add(typeof(Entity).GetMethod("Destroy"), postfix: typeof(PlayerUpdates).GetMethod("Destroyed", OwnedPatches.Members));
                hooks = pending;
            }
            catch { pending.Dispose(); throw; }
        }
        public static void Prepare(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();
            if (scope == null) throw new ArgumentNullException("scope");
            Install(); preparations++;
            scope.Defer(delegate { preparations--; ReleaseHooks(); });
        }
        private static void ReleaseHooks()
        {
            if (preparations == 0 && actors.Count == 0 && dispatching == 0 && hooks != null)
            { hooks.Dispose(); hooks = null; }
        }
        /// <summary>Register at activation. Callbacks run even if input/body are suspended, but only when the native player update runs. Order is ascending, then owner ID.</summary>
        public static Registration Register(PlayerEntity player, string owner, PlayerUpdatePhase phase, Action<float> callback, int order = 0)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if (player == null || callback == null) throw new ArgumentNullException(player == null ? "player" : "callback");
            if (phase != PlayerUpdatePhase.BeforeInput && phase != PlayerUpdatePhase.AfterInput) throw new ArgumentOutOfRangeException("phase");
            if (!player.IsAlive) throw new InvalidOperationException("Player was destroyed");
            if (Components == null || Brain == null) throw new NotSupportedException("Native player update contract unavailable");
            Actor actor;
            bool added = !actors.TryGetValue(player, out actor);
            if (added) actor = new Actor { Player = player, Body = player.m_body, Input = player.GetComponent<InputComponent>(), Brain = Brain.GetValue(player) as Component };
            Validate(actor);
            foreach (var entry in actor.Entries) if (entry.Owner == owner && entry.Phase == phase) throw new InvalidOperationException("Player phase already registered: " + owner);
            Install();
            var result = new Registration(actor, owner, phase, callback, order);
            actor.Entries.Add(result);
            actor.Entries.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Owner, b.Owner));
            actor.Snapshot = actor.Entries.ToArray();
            if (added) actors.Add(player, actor);
            return result;
        }
        private static void Validate(Actor actor)
        {
            var list = Components.GetValue(actor.Player) as List<Component>;
            int body = list == null || actor.Body == null ? -1 : list.IndexOf(actor.Body);
            int input = list == null || actor.Input == null ? -1 : list.IndexOf(actor.Input);
            int brain = list == null || actor.Brain == null ? -1 : list.IndexOf(actor.Brain);
            if (body < 0 || input <= body || brain <= input || !ReferenceEquals(actor.Player.m_body, actor.Body)
                || !ReferenceEquals(Brain.GetValue(actor.Player), actor.Brain))
                throw new InvalidOperationException("Player phases require native body/input/tree order");
        }
        private static void UpdateComponent(Component component, float delta)
        {
            Actor actor;
            var player = component.gameObject as PlayerEntity;
            if (player == null || !actors.TryGetValue(player, out actor)) { RunNative(component, delta); return; }
            // Check at the body slot too, so a reordered tree can't run before we refuse it.
            if (actor.Dispatching) throw new InvalidOperationException("Reentrant player phase update");
            if (ReferenceEquals(component, actor.Body) || ReferenceEquals(component, actor.Input) || ReferenceEquals(component, actor.Brain)) Validate(actor);
            if (!ReferenceEquals(component, actor.Input)) { RunNative(component, delta); return; }
            dispatching++; actor.Dispatching = true;
            try
            {
                Dispatch(actor, PlayerUpdatePhase.BeforeInput, delta);
                if (!player.IsAlive) return;
                RunNative(component, delta);
                if (player.IsAlive) Dispatch(actor, PlayerUpdatePhase.AfterInput, delta);
            }
            finally { actor.Dispatching = false; dispatching--; ReleaseHooks(); }
        }
        private static void Dispatch(Actor actor, PlayerUpdatePhase phase, float delta)
        {
            var snapshot = actor.Snapshot;
            foreach (var entry in snapshot)
            {
                if (!actor.Player.IsAlive) return;
                if (entry.Disposed || entry.Phase != phase) continue;
                // Gameplay failures must not silently let physics continue with a half-applied operation.
                try { entry.Callback(delta); entry.LastError = null; }
                catch (Exception error) { entry.LastError = error.GetBaseException().Message; throw; }
            }
        }
        private static void Destroyed(Entity __instance)
        {
            var player = __instance as PlayerEntity;
            Actor actor;
            if (player == null || !actors.TryGetValue(player, out actor)) return;
            foreach (var entry in actor.Snapshot)
            {
                entry.Dispose();
            }
        }
        public sealed class Registration : IDisposable
        {
            private readonly Actor actor;
            internal readonly Action<float> Callback;
            internal readonly int Order;
            internal bool Disposed;
            public string Owner { get; private set; }
            public PlayerUpdatePhase Phase { get; private set; }
            public string LastError { get; internal set; }
            internal Registration(Actor value, string owner, PlayerUpdatePhase phase, Action<float> callback, int order)
            { actor = value; Owner = owner; Phase = phase; Callback = callback; Order = order; }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread(); if (Disposed) return;
                Disposed = true; actor.Entries.Remove(this); actor.Snapshot = actor.Entries.ToArray();
                if (actor.Entries.Count == 0) actors.Remove(actor.Player);
                ReleaseHooks();
            }
        }
    }
}
