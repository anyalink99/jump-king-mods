using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.Controller;
using JKRuntime.Input;

namespace JKRuntime.UI
{
    // physical input stays separate from the published gameplay latch
    internal sealed class HoldPressLatch
    {
        private bool armed, previous, latched;
        internal bool Update(bool down, bool available, UiBindingMode mode)
        {
            if (!available) { Reset(); return false; }
            if (!armed) { if (!down) armed = true; return false; }
            if (mode == UiBindingMode.Hold) { previous = down; return down; }
            if (down && !previous) latched = !latched;
            previous = down; return latched;
        }
        internal void Reset() { armed = previous = latched = false; }
    }

    internal static class BindingActivation
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Current = typeof(PadInstance).GetField("current_state", Flags);
        private static readonly FieldInfo Previous = typeof(PadInstance).GetField("last_state", Flags);
        private static readonly FieldInfo Overlay = typeof(PadInstance).GetField("_steam_overlay_active", Flags);
        private sealed class Frame
        {
            internal PadState Raw, RawPrevious, Logical;
            internal bool Published, Converted;
            internal int Epoch;
            internal readonly HoldPressLatch Left = new HoldPressLatch(), Right = new HoldPressLatch(), Jump = new HoldPressLatch();
            internal void Reset() { Left.Reset(); Right.Reset(); Jump.Reset(); Logical = new PadState(); Converted = false; }
        }
        internal sealed class ForeignHold
        {
            internal string Id;
            internal FieldInfo Field;
            internal Func<bool> Valid;
            internal bool Blocked;
            internal readonly HoldPressLatch Latch = new HoldPressLatch();
            internal bool Raw, Published;
        }
        private static readonly Dictionary<PadInstance, Frame> frames = new Dictionary<PadInstance, Frame>();
        private static readonly Dictionary<string, ForeignHold> foreign = new Dictionary<string, ForeignHold>();
        private static bool installed;
        private static object player, mainDevice;
        private static long restoreEpoch;
        internal static bool Available
        {
            get { return Game1.instance != null && Game1.instance.IsActive && JumpGame.instance != null && JumpGame.instance.IsPlaying()
                && JumpKing.GameManager.GameLoop.m_player != null && !UiInputRouter.IsGamePaused() && !UIApi.IsOpen
                && !TextInputCapture.Active && !InputContextLease.Active && !(bool)Overlay.GetValue(null); }
        }
        internal static UiBindingModeOption Option(string id, UiBindingMode initial)
        { return new UiBindingModeOption(() => SettingsStore.GetBindingMode(id, initial), value => SettingsStore.SetBindingMode(id, value), initial)
            { FallbackReason = () => SettingsStore.GetBindingMode(id, initial) == initial ? null : BindingConversionContext.Reason(id) }; }
        internal static UiBindingModeOption RegisterForeign(string id, FieldInfo field, Func<bool> valid)
        {
            if (!Install()) return new UiBindingModeOption(UiBindingMode.Hold, "Input conversion is unavailable");
            if (!foreign.ContainsKey(id)) foreign.Add(id, new ForeignHold { Id = id, Field = field, Valid = valid });
            return Option(id, UiBindingMode.Hold);
        }
        internal static bool JumpIsToggle
        { get { return installed && SettingsStore.GetBindingMode("jump-king.jump", UiBindingMode.Hold) == UiBindingMode.Press && BindingConversionContext.Reason("jump-king.jump") == null; } }

        internal static bool Install()
        {
            if (installed) return true;
            var engines = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            if (engines.Length != 1 || Current == null || Previous == null || Overlay == null) return false;
            var harmony = engines[0].GetType("HarmonyLib.Harmony"); var metadata = engines[0].GetType("HarmonyLib.HarmonyMethod");
            var owner = Activator.CreateInstance(harmony, new object[] { "jk-runtime.binding-modes" });
            var target = typeof(ControllerManager).GetMethod("Update");
            var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            var prefix = Activator.CreateInstance(metadata, new object[] { typeof(BindingActivation).GetMethod("BeforePoll", Flags) });
            var postfix = Activator.CreateInstance(metadata, new object[] { typeof(BindingActivation).GetMethod("AfterPoll", Flags) });
            metadata.GetField("priority").SetValue(prefix, 10000);
            metadata.GetField("priority").SetValue(postfix, -10000);
            try {
                patch.Invoke(owner, new[] { (object)target, prefix, postfix, null, null });
                var equipment = Activator.CreateInstance(metadata, new object[] { typeof(BindingEquipment).GetMethod("ObserveWrite", Flags) });
                patch.Invoke(owner, new[] { (object)BindingEquipment.WriteTarget, equipment, null, null, null });
                installed = true;
            }
            catch (Exception error) {
                harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(owner,
                    new object[] { target, typeof(BindingActivation).GetMethod("BeforePoll", Flags) });
                harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(owner,
                    new object[] { target, typeof(BindingActivation).GetMethod("AfterPoll", Flags) });
                harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(owner,
                    new object[] { BindingEquipment.WriteTarget, typeof(BindingEquipment).GetMethod("ObserveWrite", Flags) });
                Console.WriteLine("[JK Runtime UI] Binding modes unavailable: " + error.GetBaseException().Message);
            }
            return installed;
        }
        internal static void Reset()
        {
            foreach (var frame in frames.Values) frame.Reset();
            foreach (var value in foreign.Values) value.Latch.Reset();
            BindingEquipment.Release();
            ModSettingsCatalog.UpdateHeldToggles(false);
        }
        private static void BeforePoll()
        {
            // menu repeat and main-device selection must see physical edges
            foreach (var pair in frames) if (pair.Value.Published) {
                Current.SetValue(pair.Key, pair.Value.Raw); Previous.SetValue(pair.Key, pair.Value.RawPrevious); pair.Value.Published = false;
            }
            foreach (var value in foreign.Values) if (value.Published && !value.Blocked) {
                value.Field.SetValue(null, value.Raw); value.Published = false;
            }
        }
        internal static void AfterPoll()
        {
            bool available = Available;
            if (!ReferenceEquals(player, JumpKing.GameManager.GameLoop.m_player)) { Reset(); player = JumpKing.GameManager.GameLoop.m_player; }
            var pads = BindingSnapshot.Registered();
            var main = ControllerManager.instance == null ? null : ControllerManager.instance.GetMain();
            long restored = JKRuntime.State.GameState.Snapshots.RestoreEpoch;
            if (!ReferenceEquals(mainDevice, main) || restoreEpoch != restored) { Reset(); mainDevice = main; restoreEpoch = restored; }
            foreach (var pad in frames.Keys.Where(p => !pads.Contains(p)).ToArray()) frames.Remove(pad);
            bool boots = false, ring = false;
            foreach (var pad in pads)
            {
                Frame frame; if (!frames.TryGetValue(pad, out frame)) { frame = new Frame(); frames.Add(pad, frame); }
                frame.Raw = (PadState)Current.GetValue(pad); frame.RawPrevious = (PadState)Previous.GetValue(pad);
                bool active = available && pad.IsValid && pad.IsConnected && pad.GetBind() != null && pad.GetBind().Enabled;
                if (frame.Epoch != PhysicalBindings.Epoch) { frame.Reset(); frame.Epoch = PhysicalBindings.Epoch; }
                var held = frame.Raw; var pressed = pad.GetPressed();
                bool changed = Convert("jump-king.left", ref held.left, ref pressed.left, frame.Logical.left, frame.Left, active);
                changed |= Convert("jump-king.right", ref held.right, ref pressed.right, frame.Logical.right, frame.Right, active);
                changed |= Convert("jump-king.jump", ref held.jump, ref pressed.jump, frame.Logical.jump, frame.Jump, active);
                frame.Converted = changed;
                boots |= active && frame.Raw.boots; ring |= active && frame.Raw.snake;
                // equipment holds own state restoration, never replay toggle callbacks
                if (BindingEquipment.HoldBoots) { held.boots = pressed.boots = false; changed = true; }
                if (BindingEquipment.HoldRing) { held.snake = pressed.snake = false; changed = true; }
                // default modes don't rewrite a frame another mod may own
                if (active && changed) { NativeInputFrames.Publish(pad, held, pressed); frame.Published = true; }
                frame.Logical = held;
            }
            BindingEquipment.Update(boots, ring, available);
            ModSettingsCatalog.UpdateHeldToggles(available && BindingConversionContext.Reason("") == null);
            foreach (var value in foreign.Values)
            {
                value.Raw = (bool)value.Field.GetValue(null);
                if (value.Blocked) continue;
                bool valid = true;
                if (SettingsStore.GetBindingMode(value.Id, UiBindingMode.Hold) == UiBindingMode.Press)
                    try { valid = value.Valid(); } catch { valid = false; }
                if (!valid) {
                    value.Blocked = true; value.Latch.Reset();
                    var binding = UIApi.GetBindings().FirstOrDefault(b => b.Id == value.Id); if (binding != null) binding.Mode = null;
                    Console.WriteLine("[JK Runtime UI] Mode conversion disabled after input patch changes: " + value.Id);
                    continue;
                }
                var mode = SettingsStore.GetBindingMode(value.Id, UiBindingMode.Hold);
                if (mode == UiBindingMode.Hold) { value.Latch.Reset(); continue; }
                if (BindingConversionContext.Reason(value.Id) != null) { value.Latch.Reset(); continue; }
                bool converted = value.Latch.Update(value.Raw, available, mode);
                if (mode == UiBindingMode.Press) { value.Field.SetValue(null, converted); value.Published = true; }
            }
        }
        internal static void AfterBody()
        {
            if (!installed) return;
            bool available = Available;
            foreach (var pair in frames)
            {
                var pad = pair.Key; var frame = pair.Value;
                if (!frame.Converted) continue;
                bool active = available && pad.IsValid && pad.IsConnected && pad.GetBind() != null && pad.GetBind().Enabled;
                var held = (PadState)Current.GetValue(pad); var pressed = pad.GetPressed();
                // collisions/materials have just updated their actual predicates
                bool changed = Yield("jump-king.left", ref held.left, ref pressed.left, frame.Raw.left, frame.RawPrevious.left, frame.Left);
                changed |= Yield("jump-king.right", ref held.right, ref pressed.right, frame.Raw.right, frame.RawPrevious.right, frame.Right);
                changed |= Yield("jump-king.jump", ref held.jump, ref pressed.jump, frame.Raw.jump, frame.RawPrevious.jump, frame.Jump);
                if (active && changed) { NativeInputFrames.Publish(pad, held, pressed); frame.Published = true; }
                frame.Logical = held;
            }
        }
        private static bool Yield(string id, ref bool held, ref bool pressed, bool raw, bool previous, HoldPressLatch latch)
        {
            if (SettingsStore.GetBindingMode(id, UiBindingMode.Hold) != UiBindingMode.Press || BindingConversionContext.Reason(id) == null) return false;
            latch.Reset(); held = raw; pressed = raw && !previous; return true;
        }
        private static bool Convert(string id, ref bool held, ref bool pressed, bool previous, HoldPressLatch latch, bool active)
        {
            var mode = SettingsStore.GetBindingMode(id, UiBindingMode.Hold);
            if (mode == UiBindingMode.Hold) { latch.Reset(); return false; }
            if (BindingConversionContext.Reason(id) != null) { latch.Reset(); return false; }
            bool logical = latch.Update(held, active, mode);
            if (mode == UiBindingMode.Press) { held = logical; pressed = logical && !previous; }
            return mode == UiBindingMode.Press;
        }
    }
}
