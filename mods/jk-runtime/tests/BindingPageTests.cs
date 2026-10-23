using System;
using System.Linq;
using JKRuntime.UI;

namespace JKRuntime
{
    internal static class BindingPageTests
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Main()
        {
            int reads = 0, writes = 0, foreignWrites = 0;
            UiChord[] saved = { new UiChord(70) };
            var binding = new UiBindingDefinition("test.binding-page", "Test", "Focus", () => { reads++; return saved; }, v => { writes++; saved = v; }, () => saved = new[] { new UiChord(70) });
            var foreign = new UiBindingDefinition("test.foreign", "Other", "Other", () => new[] { new UiChord(10) }, v => foreignWrites++, () => foreignWrites++);
            UIApi.RegisterBinding(binding); UIApi.RegisterBinding(foreign);
            try {
                string[] ids = { binding.Id };
                var page = new UiBindingsPage("Binds", "Test", ids); ids[0] = foreign.Id;
                Require(reads == 0 && writes == 0, "Construction must not invoke provider callbacks");
                page.OnOpen();
                Require(ReferenceEquals(page.Selected(), binding) && reads == 0, "Explicit binding IDs are copied and resolved without reading preferences");
                Require(new UiBindingsPage("Binds", null, new string[0]).Selected() == null, "An empty filter never exposes all bindings");
                UIApi.UnregisterBinding(binding.Id);
                Require(page.Selected() == null, "A missing provider never falls back to another mod");
                UIApi.RegisterBinding(binding);
                BindingSlots.Set(binding, 1, new UiChord(17, 38));
                Require(saved.Length == 2 && saved[0].Buttons.SequenceEqual(new[] { 70 }), "Editing secondary preserves primary");
                BindingSlots.Set(binding, 0, new UiChord(38, 17));
                Require(saved.Length == 1, "Equivalent chords deduplicate independent of button order");
                BindingSlots.Set(binding, 0, null);
                Require(saved.Length == 0 && foreignWrites == 0, "Clear changes only the selected registered binding");
                binding.Reset(); Require(saved[0].Buttons[0] == 70, "Reset uses the owning provider's defaults");
                var broken = new UiBindingDefinition("test.broken", "Test", "Broken", () => saved, v => { throw new InvalidOperationException("save refused"); }, null);
                bool refused = false;
                try { BindingSlots.Set(broken, 0, new UiChord(99)); } catch (InvalidOperationException) { refused = true; }
                Require(refused && saved[0].Buttons[0] == 70, "Preparing a slot edit never mutates the provider's array before persistence");
                page.OnClose();
            } finally { UIApi.UnregisterBinding(binding.Id); UIApi.UnregisterBinding(foreign.Id); }
            Modes();
            Capture();
            Require(UIApi.Supports("binding-pages-v1"), "The reusable page has a capability contract");
            Console.WriteLine("[OK] Focused bindings: lazy exact-ID selection, shared slots, clear/default, save refusal, device/focus cancellation and chord capture");
        }
        static void Modes()
        {
            int reads = 0, writes = 0, keyWrites = 0;
            var value = UiBindingMode.Hold;
            var option = new UiBindingModeOption(() => { reads++; return value; }, mode => { writes++; value = mode; }, UiBindingMode.Hold, UiBindingMode.Hold, UiBindingMode.Press, UiBindingMode.Both);
            Require(reads == 0 && writes == 0 && option.Default == UiBindingMode.Hold, "Mode construction is lazy and defaults to Hold");
            option.Cycle(); Require(value == UiBindingMode.Press, "Hold cycles to Press");
            option.Cycle(); Require(value == UiBindingMode.Both, "Press cycles to Both");
            option.Cycle(); Require(value == UiBindingMode.Hold && writes == 3, "Both cycles to Hold with one write per command");
            option.Value = UiBindingMode.Press; option.Reset();
            Require(value == UiBindingMode.Hold, "Default resets the mode");
            option.FallbackReason = () => "Active input mechanic";
            option.Cycle();
            Require(option.CanChange && value == UiBindingMode.Press && option.Reason == "Active input mechanic",
                "Gameplay fallback explains itself without locking the saved mode");
            option.Reset(); option.FallbackReason = null;
            bool invalid = false;
            try { option.Value = (UiBindingMode)99; } catch (ArgumentOutOfRangeException) { invalid = true; }
            Require(invalid && value == UiBindingMode.Hold, "Invalid enum cannot reach persistence");
            var custom = new UiBindingModeOption(() => value, mode => value = mode, UiBindingMode.Both, UiBindingMode.Hold, UiBindingMode.Press, UiBindingMode.Both);
            custom.Reset(); Require(value == UiBindingMode.Both, "Provider may choose its own default");
            var broken = new UiBindingModeOption(() => value, mode => { throw new InvalidOperationException("save refused"); });
            value = UiBindingMode.Hold; bool refused = false; try { broken.Cycle(); } catch (InvalidOperationException) { refused = true; }
            Require(refused && value == UiBindingMode.Hold, "Failed save leaves provider state intact");
            var withMode = new UiBindingDefinition("test.mode", "Test", "Mode", () => new UiChord[0], v => keyWrites++, () => keyWrites++) { Mode = option };
            var plain = new UiBindingDefinition("test.plain", "Test", "Plain", () => new UiChord[0], v => keyWrites++, () => keyWrites++);
            UIApi.RegisterBinding(withMode); UIApi.RegisterBinding(plain);
            try
            {
                var controls = new ControlsPageNode();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(ControlsPageNode).GetField("bindings", flags).SetValue(controls, new[] { withMode, plain });
                controls.HandleAction(UiAction.Left);
                controls.HandleAction(UiAction.Reset);
                Require(value == UiBindingMode.Hold && keyWrites == 0, "Controls+ Default on Mode leaves keys untouched");
                controls.HandleAction(UiAction.Confirm);
                Require(value == UiBindingMode.Press && !(bool)typeof(ControlsPageNode).GetField("capturing", flags).GetValue(controls), "Controls+ mode click cycles without starting key capture");
                controls.HandleAction(UiAction.Secondary);
                Require(value == UiBindingMode.Press && keyWrites == 0, "Clear doesn't erase a mode");
                controls.HandleAction(UiAction.Down); controls.HandleAction(UiAction.Confirm);
                Require((bool)typeof(ControlsPageNode).GetField("capturing", flags).GetValue(controls), "Moving to a plain binding clamps to a key slot");
                var page = new UiBindingsPage("Binds", null, withMode.Id, plain.Id) { ShowList = true }; page.OnOpen();
                typeof(UiBindingsPage).GetField("slot", flags).SetValue(page, 2);
                typeof(UiBindingsPage).GetMethod("Move", flags).Invoke(page, new object[] { 1 });
                Require(ReferenceEquals(page.Selected(), plain) && (int)typeof(UiBindingsPage).GetField("slot", flags).GetValue(page) == 0,
                    "Focused list also skips absent mode cells");
                page.OnClose();
            }
            finally { UIApi.UnregisterBinding(withMode.Id); UIApi.UnregisterBinding(plain.Id); }
            Require(UIApi.Supports("binding-modes-v1"), "Optional modes expose a capability contract");
        }
        static void Capture()
        {
            object first = new object(), second = new object(); int[] result;
            var capture = new BindingCaptureSession(); capture.Begin(first, "keyboard", 2);
            Require(!capture.Update(first, "keyboard", true, new[] { 13 }, out result), "Opening Confirm cannot be captured");
            capture.Update(first, "keyboard", true, new int[0], out result);
            capture.Update(first, "keyboard", true, new[] { 17 }, out result);
            capture.Update(first, "keyboard", true, new[] { 17, 70, 71 }, out result);
            capture.Update(first, "keyboard", true, new int[0], out result);
            capture.Update(first, "keyboard", true, new int[0], out result);
            Require(capture.Update(first, "keyboard", true, new int[0], out result) && result.SequenceEqual(new[] { 17, 70 }) && !capture.Active, "Chord commits once after three release polls, with at most two buttons");
            Require(!capture.Update(first, "keyboard", true, new int[0], out result), "Completed capture cannot replay a write");
            foreach (int reason in new[] { 0, 1, 2 }) {
                capture.Begin(first, "keyboard", 2);
                capture.Update(first, "keyboard", true, new int[0], out result);
                capture.Update(first, "keyboard", true, new[] { 70 }, out result);
                Require(!capture.Update(reason == 0 ? second : first, reason == 1 ? "controller" : "keyboard", reason != 2, new int[0], out result)
                    && !capture.Active && result == null, "Device replacement, profile change and focus loss discard partial input");
            }
            capture.Begin(first, "controller", 1);
            capture.Update(first, "controller", true, new int[0], out result);
            capture.Update(first, "controller", true, new[] { 2, 3 }, out result);
            capture.Update(first, "controller", true, new int[0], out result);
            capture.Update(first, "controller", true, new int[0], out result);
            Require(capture.Update(first, "controller", true, new int[0], out result) && result.SequenceEqual(new[] { 2 }), "Single-button providers do not receive unsupported chords");
            capture.Begin(first, "controller", 2); capture.Cancel();
            Require(!capture.Active, "Closing the page cancels capture");
        }
    }
}
