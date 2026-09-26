using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using JKRuntime.State;
using JumpKing;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Gameplay
{
    // Component slots observe boundaries; no controller, body behaviour or Harmony patch is replaced.
    internal sealed class NativeGameplayObserver : IDisposable
    {
        private sealed class Boundary : Component
        {
            internal Action Step, Late;
            protected override void Update(float delta) { if (Step != null) Step(); }
            protected override void LateUpdate(float delta) { if (Late != null) Late(); }
        }
        private static NativeGameplayObserver current;
        private readonly PlayerEntity player;
        private readonly Boundary before, after;
        private readonly IDisposable pause;
        private bool sampled, supported, charging, callback, paused, pauseKnown;
        private int screen;
        private float beforeJump;
        private JumpResult enrichment;
        private Vector2 callbackVelocity;
        private readonly List<JumpResult> nativeJumps = new List<JumpResult>();
        private static readonly FieldInfo Context = typeof(BodyComp).GetField("m_behaviourContext", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Components = typeof(Entity).GetField("m_components", BindingFlags.Instance | BindingFlags.NonPublic);
        private NativeGameplayObserver(PlayerEntity value)
        {
            player = value;
            before = new Boundary { Step = Begin };
            after = new Boundary { Step = AfterBody, Late = End };
            var components = Components == null ? null : Components.GetValue(player) as List<Component>;
            if (components == null || Context == null || !components.Contains(player.m_body)) throw new NotSupportedException("Native gameplay observation contract unavailable");
            // Resolve the pause contract before touching a live component list.
            NativePause.ValidateContract();
            pause = NativePause.Subscribe("jk-runtime.events", ObservePause);
            try
            {
            player.AddComponents(before, after);
            components.Remove(before); components.Remove(after);
            int index = components.IndexOf(player.m_body);
            components.Insert(index, before); components.Insert(index + 2, after);
            PlayerEntity.OnJumpCall += OnJump;
            Input.ActionInputs.BeginLevel();
            }
            catch { components.Remove(before); components.Remove(after); pause.Dispose(); throw; }
        }
        internal static IDisposable Install(PlayerEntity player)
        {
            if (current != null) throw new InvalidOperationException("Native gameplay observer already installed");
            return current = new NativeGameplayObserver(player);
        }
        internal static void Enrich(JumpResult result)
        { if (current != null && current.sampled) current.enrichment = result; }
        private void Begin()
        {
            Input.ActionInputs.BeginTick();
            sampled = GameplayEvents.Requested || JumpEvents.Requested;
            if (!sampled) { charging = false; return; }
            GameplayEvents.BeginTick(GameClock.ReadAttempt());
            supported = player.m_body.IsOnGround; screen = Camera.CurrentScreen;
            beforeJump = player.m_body.Velocity.Y;
            callback = false; enrichment = null;
            nativeJumps.Clear();
        }
        private void AfterBody()
        {
            if (!sampled) return;
            var body = player.m_body;
            if (!callback) beforeJump = body.Velocity.Y;
            if (body.Enabled)
            {
                if (supported != body.IsOnGround) Emit(body.IsOnGround ? GameplayEventKind.Landed : GameplayEventKind.SupportLost);
                var context = (BehaviourContext)Context.GetValue(body);
                if (context.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag)) Emit(GameplayEventKind.Teleported);
            }
        }
        private void OnJump()
        {
            if (!sampled) return;
            float previous = callback ? callbackVelocity.Y : beforeJump;
            callback = true; callbackVelocity = player.m_body.Velocity;
            nativeJumps.Add(new JumpResult(GameFeatures.IsMorphed ? "player.form" : "native.jump", JumpEvidence.Unavailable,
                null, null, null, null, true, false, previous, callbackVelocity.Y));
        }
        private void End()
        {
            if (!sampled) return;
            bool nowCharging = !JumpSlot.ChargePolicySuspended && !GameFeatures.IsMorphed && GameFeatures.Movement != MovementMode.VariableJump &&
                RuntimeHost.Contract.ReadChargeTimer(RuntimeHost.Contract.GetJumpState(player)) > 0;
            if (charging != nowCharging) Emit(nowCharging ? GameplayEventKind.ChargeStarted : GameplayEventKind.ChargeEnded);
            charging = nowCharging;
            if (callback || enrichment != null)
            {
                // Preserve multiple real callbacks. SFC enriches its final native
                // callback, or reports a native charge with no upward impulse.
                if (enrichment != null) { if (nativeJumps.Count > 0) nativeJumps[nativeJumps.Count - 1] = enrichment; else nativeJumps.Add(enrichment); }
                var alreadyDelivered = enrichment;
                foreach (var result in nativeJumps)
                {
                    GameplayEvents.Emit(GameplayEventKind.Jump, result.Provider, player.m_body.Position, player.m_body.Velocity, Camera.CurrentScreen, result);
                    // SFC already delivered its legacy result at the overlay hook.
                    if (!ReferenceEquals(result, alreadyDelivered)) JumpEvents.Publish(result);
                }
            }
            if (screen != Camera.CurrentScreen) Emit(GameplayEventKind.ScreenChanged);
            sampled = false;
        }
        private void ObservePause(bool value, long timestamp)
        {
            if (!GameplayEvents.Requested) return;
            if (!pauseKnown || value != paused) { paused = value; pauseKnown = true;
                GameplayEvents.Emit(GameplayEventKind.PauseChanged, "native.pause", player.m_body.Position, player.m_body.Velocity, Camera.CurrentScreen, null, paused); }
        }
        private void Emit(GameplayEventKind kind)
        { GameplayEvents.Emit(kind, "native.player", player.m_body.Position, player.m_body.Velocity, Camera.CurrentScreen); }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread();
            if (current != this) return;
            PlayerEntity.OnJumpCall -= OnJump;
            before.Step = after.Step = after.Late = null; before.Enabled = after.Enabled = false;
            if (pause != null) pause.Dispose();
            current = null;
        }
    }
}
