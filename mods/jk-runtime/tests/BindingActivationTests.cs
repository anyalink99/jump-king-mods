using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Input;
using JKRuntime.UI;
using JumpKing.Controller;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class BindingActivationTests
    {
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        internal sealed class Preferences { public Dictionary<int, int[]> KeyBindings { get; set; } }
        private enum DeclaredBinding { [UiBindingMode(UiBindingMode.Press)] Action }
        private static Preferences preferences;
        private static bool Held { get; set; }
        private static bool Other { get; set; }
        private static void Producer(ControllerManager controller)
        {
            var p = preferences;
            Held = controller.GetMain().GetPad().GetPressedButtons().Any(((IEnumerable<int>)p.KeyBindings[0]).Contains);
        }
        private static void Arithmetic(ref float result) { if (Held) result += 2; }
        private static void OneShotProducer(ControllerManager controller)
        {
            var p = preferences;
            Other = controller.GetMain().GetPad().GetPressedButtons().Any(((IEnumerable<int>)p.KeyBindings[1]).Contains);
        }
        private static int saves;
        private static void NotReversible() { if (Other) saves++; }
        private struct State { internal bool Action; }
        private static State current, previous;
        private static State Capture(IPad pad)
        {
            int[] down = pad.GetPressedButtons();
            return new State { Action = Match(down, preferences.KeyBindings[2]) };
        }
        private static bool Match(int[] down, int[] bind) { return down.Any(bind.Contains); }
        private static State Edge() { return new State { Action = !previous.Action && current.Action }; }
        private static void ConsumeEdge() { if (Edge().Action) saves++; }
        private sealed class Pad : IPad
        {
            internal bool Connected = true;
            public int[] GetPressedButtons() { return new int[0]; }
            public bool IsConnected() { return Connected; }
            public string GetSaveIdentifier() { return "mode-test"; }
            public string GetPrintName() { return "Controller"; }
            public string ButtonToString(int button) { return button.ToString(); }
            public PadBinding GetDefaultBind() { return new PadBinding { jump = new[] { 32 } }; }
        }
        private static bool available = true;
        private static readonly Dictionary<JumpKing.MiscEntities.WorldItems.Items, bool> worn = new Dictionary<JumpKing.MiscEntities.WorldItems.Items, bool>();
        private static bool ReadEquipment(JumpKing.MiscEntities.WorldItems.Items __0, ref bool __result)
        { worn.TryGetValue(__0, out __result); return false; }
        private static bool WriteEquipment(JumpKing.MiscEntities.WorldItems.Items __0, bool __1)
        { worn[__0] = __1; return false; }
        private static void EquipmentFeedback()
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var skin = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.Player.Skins.SkinManager");
            using (var hooks = new OwnedPatches("test.binding-equipment")) {
                hooks.Add(skin.GetMethod("IsWearingSkin", flags), prefix: typeof(BindingActivationTests).GetMethod("ReadEquipment", flags));
                hooks.Add(skin.GetMethod("SetSkinEnabled", flags), prefix: typeof(BindingActivationTests).GetMethod("WriteEquipment", flags));
                int sounds = 0;
                UiSounds.TestPlayback = sound => { if (sound == UiSound.Equipment) sounds++; };
                try {
                    foreach (string item in new[] { "Boots", "Ring" }) {
                        var getter = typeof(BindingEquipment).GetMethod(item, flags);
                        var setter = typeof(BindingEquipment).GetMethod("Set" + item, flags);
                        Func<bool> read = () => (bool)getter.Invoke(null, null);
                        Action<bool> write = value => setter.Invoke(null, new object[] { value });
                        var hold = new TemporaryToggle(); int before = sounds;
                        hold.Update(false, true, read, write); hold.Update(true, true, read, write);
                        for (int i = 0; i < 20; i++) hold.Update(true, true, read, write);
                        Check(sounds == before + 1, "Equipment hold sounds once when equipped, never on held frames");
                        hold.Update(false, true, read, write); write(false);
                        Check(sounds == before + 2, "Release sounds once; unchanged equipment is silent");
                    }
                } finally { UiSounds.TestPlayback = null; worn.Clear(); }
            }
        }
        private sealed class ForeignSurface : BoxBlock { internal ForeignSurface() : base(new Rectangle(0, 0, 10, 10)) { } }
        private static bool ScopedControl(JumpKing.Player.BodyComp body)
        { if (body.IsOnBlock<ForeignSurface>()) return false; return true; }
        private static bool UnscopedControl(JumpKing.Player.BodyComp body)
        { if (body.IsOnBlock<ForeignSurface>()) return false; return Other; }
        private static bool StatefulControl(JumpKing.Player.BodyComp body)
        { if (body.IsOnBlock<ForeignSurface>()) return false; NotReversible(); return true; }
        private static bool Available(out bool __result) { __result = available; return false; }
        private static void Publication(string harmonyPath)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var engine = Assembly.LoadFrom(harmonyPath); var harmony = engine.GetType("HarmonyLib.Harmony"); var metadata = engine.GetType("HarmonyLib.HarmonyMethod");
            var owner = Activator.CreateInstance(harmony, new object[] { "fixture.mode-availability" });
            harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(owner, new[] {
                (object)typeof(BindingActivation).GetProperty("Available", flags).GetGetMethod(true),
                Activator.CreateInstance(metadata, new object[] { typeof(BindingActivationTests).GetMethod("Available", flags) }), null, null, null });
            Check(BindingActivation.Install(), "Native mode hooks install against the loaded engine");
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            var device = new Pad(); var pad = new PadInstance(device); ControllerManager.instance = manager;
            typeof(ControllerManager).GetField("m_pads", flags).SetValue(manager, new List<PadInstance> { pad });
            typeof(ControllerManager).GetField("_current_main", flags).SetValue(manager, pad);
            var menu = new MenuController(manager); typeof(ControllerManager).GetField("_menu_controller", flags).SetValue(manager, menu);
            SettingsStore.SetBindingMode("jump-king.jump", UiBindingMode.Press);
            var before = typeof(BindingActivation).GetMethod("BeforePoll", flags);
            var currentField = typeof(PadInstance).GetField("current_state", flags); var previousField = typeof(PadInstance).GetField("last_state", flags);
            Action<bool> tick = down => {
                before.Invoke(null, null);
                previousField.SetValue(pad, currentField.GetValue(pad)); currentField.SetValue(pad, new PadState { jump = down, left = down, right = down });
                menu.Update(); BindingActivation.AfterPoll(); BindingActivation.AfterBody();
            };
            tick(false); tick(true);
            Check(pad.GetState().jump && menu.GetPadState().jump, "First press reaches both gameplay and the physical menu frame");
            tick(false);
            Check(pad.GetState().jump && !pad.GetPressed().jump && !menu.GetPadState().jump, "Release retains logical hold without repeating a menu command");
            tick(true);
            Check(!pad.GetState().jump && menu.GetPadState().jump, "Second physical press reaches menus while releasing gameplay hold");
            tick(false); tick(true); available = false; tick(false); available = true; tick(true);
            Check(!pad.GetState().jump, "Focus loss clears publication and returning held needs release");
            tick(false); tick(true); device.Connected = false; tick(false); device.Connected = true; tick(true);
            Check(!pad.GetState().jump, "Disconnected devices cannot retain a gameplay latch");
            SettingsStore.SetBindingMode("jump-king.jump", UiBindingMode.Hold); tick(false); tick(true); tick(false);
            Check(!pad.GetState().jump, "Restoring Hold resumes physical release");
            Check(((PadState)previousField.GetValue(pad)).jump, "Default mode preserves the native release history without republishing");
            BindingActivation.Reset();
        }

        private static void Main(string[] args)
        {
            Publication(args[0]);
            EquipmentFeedback();
            var register = typeof(BaseBindings).GetMethod("RegisterBase", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var button in new[] { JKpadButtons.Up, JKpadButtons.Down }) {
                register.Invoke(null, new object[] { button, "Menu navigation" });
                var mode = UIApi.GetBindings().Single(b => b.Id == "jump-king." + button.ToString().ToLowerInvariant()).Mode;
                Check(mode.Value == UiBindingMode.Press && !mode.CanChange, "Menu steps are press actions; key repeat isn't a held gameplay action");
            }
            var declared = BindingModeDiscovery.Discover("fixture.declared", typeof(BindingActivationTests).Assembly, null, DeclaredBinding.Action);
            Check(declared != null && declared.Value == UiBindingMode.Press && !declared.CanChange,
                "Provider enum metadata survives automatic binding discovery without input inference");
            var latch = new HoldPressLatch();
            Check(!latch.Update(true, true, UiBindingMode.Press), "Entry with held input cannot toggle");
            latch.Update(false, true, UiBindingMode.Press);
            Check(latch.Update(true, true, UiBindingMode.Press), "Press enables a persistent hold");
            for (int i = 0; i < 50; i++) Check(latch.Update(true, true, UiBindingMode.Press), "Repeated frame reads never repeat a toggle");
            Check(latch.Update(false, true, UiBindingMode.Press), "Physical release preserves logical hold");
            Check(!latch.Update(true, true, UiBindingMode.Press), "Next press releases logical hold");
            latch.Update(false, true, UiBindingMode.Press); latch.Update(true, true, UiBindingMode.Press);
            Check(!latch.Update(false, false, UiBindingMode.Press), "Pause, focus loss and device loss clear the latch");
            Check(!latch.Update(true, true, UiBindingMode.Press), "Returning while held cannot reactivate");
            latch.Update(false, true, UiBindingMode.Hold);
            Check(latch.Update(true, true, UiBindingMode.Hold) && !latch.Update(false, true, UiBindingMode.Hold), "Hold follows press and release");

            bool equipped = false; int writes = 0;
            var hold = new TemporaryToggle(); Func<bool> read = () => equipped; Action<bool> write = value => { equipped = value; writes++; };
            hold.Update(false, true, read, write); hold.Update(true, true, read, write);
            hold.Update(true, true, read, write);
            Check(equipped && writes == 1, "Hold equips exactly once");
            hold.Update(false, true, read, write); Check(!equipped && writes == 2, "Release restores the prior state");
            equipped = true; hold.Update(true, true, read, write); hold.Update(false, false, read, write);
            Check(equipped && writes == 2, "Already enabled state survives hold and focus loss");
            equipped = false; hold.Update(false, true, read, write); hold.Update(true, true, read, write);
            equipped = false; hold.Update(true, true, read, write); hold.Update(false, true, read, write);
            Check(!equipped && writes == 3, "An independent state edit takes ownership away from the hold");

            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type[] blocks;
            Check(BindingContextIl.TryGuard(typeof(BindingActivationTests).GetMethod("ScopedControl", flags), out blocks)
                && blocks.SequenceEqual(new[] { typeof(ForeignSurface) }), "Input replacement is scoped to its actual block predicate");
            Check(!BindingContextIl.TryGuard(typeof(BindingActivationTests).GetMethod("UnscopedControl", flags), out blocks)
                && !BindingContextIl.TryGuard(typeof(BindingActivationTests).GetMethod("StatefulControl", flags), out blocks),
                "Unconditional replacement or opaque inactive effects aren't certified");
            var property = typeof(Preferences).GetProperty("KeyBindings");
            var assembly = typeof(BindingActivationTests).Assembly;
            var field = BindingModeDiscovery.Analyze(assembly, property, 0, new[] { typeof(BindingActivationTests).GetMethod("Producer", flags) });
            Check(field != null && field.FieldType == typeof(bool), "Unnamed held flag with arithmetic-only consumers is recognized structurally");
            Check(BindingModeDiscovery.Analyze(assembly, property, 1, new[] { typeof(BindingActivationTests).GetMethod("OneShotProducer", flags) }) == null,
                "A held predicate with a state-changing consumer isn't treated as reversible");
            Check(BindingModeDiscovery.Analyze(assembly, property, 0, new MethodInfo[0]) == null, "An unregistered producer has no polling-order proof");
            Check(BindingModeDiscovery.ReadsOnlyEdges(assembly, property, 2), "A previous/current edge pipeline reports fixed Press");
            Check(!BindingModeDiscovery.ReadsOnlyEdges(assembly, property, 99), "Unknown keys remain unknown");
            var option = new UiBindingModeOption(() => UiBindingMode.Hold, value => { });
            Check(option.AvailableModes.SequenceEqual(new[] { UiBindingMode.Hold, UiBindingMode.Press }), "Both requires explicit provider support");
            bool refused = false; try { option.Value = UiBindingMode.Both; } catch (ArgumentOutOfRangeException) { refused = true; }
            Check(refused && !new UiBindingModeOption(UiBindingMode.Press, "One-shot").CanChange, "Unsupported modes cannot be saved");
            Console.WriteLine("[OK] Binding modes: latch edges, focus/re-arm, reversible holds, foreign IL evidence, one-shot refusal and explicit Both support");
        }
    }
}
