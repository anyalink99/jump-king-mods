using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    public enum PlayerUpdatePhase { BeforeInput, AfterInput }
    /// <summary>Mark a player component whose update and late update must follow the input/tree cadence.</summary>
    public interface IPlayerControlObserver { }

    /// <summary>Callbacks around the native input slot, without adding or moving player components.</summary>
    public static class PlayerUpdates
    {
        private static readonly FieldInfo Components = typeof(Entity).GetField("m_components", OwnedPatches.Members);
        private static readonly FieldInfo Brain = typeof(PlayerEntity).GetField("m_bt", OwnedPatches.Members);
        private static readonly MethodInfo LowUpdate = typeof(Component).GetMethod("LowUpdate", OwnedPatches.Members);
        private static readonly Action<Component, float> RunNative = (Action<Component, float>)Delegate.CreateDelegate(typeof(Action<Component, float>), LowUpdate);
        private static readonly MethodInfo LowLateUpdate = typeof(Component).GetMethod("LowLateUpdate", OwnedPatches.Members);
        private static readonly Action<Component,float> RunLate = (Action<Component,float>)Delegate.CreateDelegate(typeof(Action<Component,float>),LowLateUpdate);
        private static readonly Dictionary<PlayerEntity, Actor> actors = new Dictionary<PlayerEntity, Actor>();
        private static OwnedPatches hooks;
        private static int preparations, dispatching;
        internal sealed class Actor
        {
            internal PlayerEntity Player;
            internal InputComponent Input;
            internal bool Dispatching;
            internal Component Body, Brain;
            internal SubstepLease Substeps;
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
                pending.ReplaceCalls(typeof(Entity).GetMethod("UpdateComponents"), LowLateUpdate,
                    typeof(PlayerUpdates).GetMethod("LateComponent", OwnedPatches.Members), 1);
                pending.Add(typeof(Entity).GetMethod("UpdateComponents"), prefix: typeof(PlayerUpdates).GetMethod("BeginNative", OwnedPatches.Members));
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
            bool substep=actor.Substeps!=null && actor.Substeps.OwnsNativeSlot(component);
            if (!ReferenceEquals(component, actor.Input)) { if(!substep) RunNative(component, delta); return; }
            dispatching++; actor.Dispatching = true;
            try
            {
                Dispatch(actor, PlayerUpdatePhase.BeforeInput, delta);
                if (!player.IsAlive) return;
                if(!substep) RunNative(component, delta);
                if (player.IsAlive) Dispatch(actor, PlayerUpdatePhase.AfterInput, delta);
            }
            finally { actor.Dispatching = false; dispatching--; ReleaseHooks(); }
        }
        private static void LateComponent(Component component,float delta)
        {
            Actor actor; var player=component.gameObject as PlayerEntity;
            if(player==null || !actors.TryGetValue(player,out actor) || actor.Substeps==null || !actor.Substeps.OwnsNativeSlot(component)) RunLate(component,delta);
        }
        private static void BeginNative(Entity __instance)
        {
            Actor actor; var player=__instance as PlayerEntity;
            if(player!=null && actors.TryGetValue(player,out actor) && actor.Substeps!=null) actor.Substeps.CaptureNativeSlots();
        }
        /// <summary>Own the player's body/input/tree and selected observers at a separate cadence. Entity updates, saves and registered phase callbacks retain native cadence. The caller owns pacing and pause checks.</summary>
        public static SubstepLease RegisterSubsteps(PlayerEntity player,string owner,Func<Component,bool> include)
        { return RegisterSubsteps(player,owner,include,null); }
        /// <summary>Keep alternate input/tree controllers at native cadence while advancing the native body in smaller steps.</summary>
        public static SubstepLease RegisterSubsteps(PlayerEntity player,string owner,Func<Component,bool> include,Func<bool> nativeControls)
        {
            RuntimeApi.Kernel.CheckThread(); ModuleDefinition.ValidId(owner);
            if(player==null || include==null) throw new ArgumentNullException(player==null ? "player" : "include");
            if(!player.IsAlive) throw new InvalidOperationException("Player was destroyed");
            Actor actor;
            if(!actors.TryGetValue(player,out actor)) actor=new Actor { Player=player,Body=player.m_body,Input=player.GetComponent<InputComponent>(),Brain=Brain.GetValue(player) as Component };
            Validate(actor);
            if(actor.Substeps!=null) throw new InvalidOperationException("Player simulation already has an owner");
            Install(); var lease=new SubstepLease(actor,include,nativeControls); actor.Substeps=lease; actors[player]=actor; return lease;
        }
        public sealed class SubstepLease : IDisposable
        {
            private Actor actor;
            private readonly Func<Component,bool> include;
            private readonly Func<bool> nativeControls;
            private readonly List<Component> selected=new List<Component>();
            private readonly HashSet<Component> nativeSlots=new HashSet<Component>();
            private bool advancing, releasePending;
            internal SubstepLease(Actor value,Func<Component,bool> selector,Func<bool> native)
            { actor=value; include=selector; nativeControls=native; }
            private bool Owns(Component value,bool native,bool nativePass)
            {
                if(actor==null) return false;
                if(ReferenceEquals(value,actor.Input) || ReferenceEquals(value,actor.Brain) || value is IPlayerControlObserver) return !native;
                // body events need every small move; alternate jumps also need the native pass
                if(NativeGameplayObserver.IsBoundary(value)) return !nativePass || !native;
                return ReferenceEquals(value,actor.Body) || include(value);
            }
            internal bool OwnsNativeSlot(Component value) { return nativeSlots.Contains(value); }
            internal void CaptureNativeSlots()
            {
                // a form change halfway through the frame mustn't split the tree's observers
                bool native=nativeControls!=null && nativeControls();
                nativeSlots.Clear();
                foreach(var component in (List<Component>)Components.GetValue(actor.Player)) if(Owns(component,native,true)) nativeSlots.Add(component);
            }
            /// <summary>Advance selected components once, in their native order, followed by their late updates. Doesn't run Entity.Update or registered native phase callbacks.</summary>
            public void Advance(float delta)
            {
                RuntimeApi.Kernel.CheckThread();
                if(actor==null) throw new ObjectDisposedException("PlayerUpdates.SubstepLease");
                if(advancing || delta<=0 || delta>1f/60 || float.IsNaN(delta)) throw new ArgumentOutOfRangeException("delta");
                Validate(actor); if(!actor.Player.IsAlive) return;
                advancing=true; dispatching++;
                try
                {
                    selected.Clear();
                    bool native=nativeControls!=null && nativeControls();
                    foreach(var component in (List<Component>)Components.GetValue(actor.Player)) if(Owns(component,native,false)) selected.Add(component);
                    foreach(var component in selected) { if(releasePending || !actor.Player.IsAlive) return; RunNative(component,delta); }
                    foreach(var component in selected) { if(releasePending || !actor.Player.IsAlive) return; RunLate(component,delta); }
                }
                finally { advancing=false; dispatching--; if(releasePending) Dispose(); ReleaseHooks(); }
            }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread(); if(actor==null)return;
                if(advancing) { releasePending=true; return; }
                var value=actor; actor=null; value.Substeps=null;
                if(value.Entries.Count==0) actors.Remove(value.Player); ReleaseHooks();
            }
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
            if(actor.Substeps!=null) actor.Substeps.Dispose();
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
                if (actor.Entries.Count == 0 && actor.Substeps==null) actors.Remove(actor.Player);
                ReleaseHooks();
            }
        }
    }
}
