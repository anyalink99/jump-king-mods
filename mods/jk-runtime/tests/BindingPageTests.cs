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
            Capture();
            Require(UIApi.Supports("binding-pages-v1"), "The reusable page has a capability contract");
            Console.WriteLine("[OK] Focused bindings: lazy exact-ID selection, shared slots, clear/default, save refusal, device/focus cancellation and chord capture");
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
