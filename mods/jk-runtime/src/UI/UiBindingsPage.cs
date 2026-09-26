using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    /// <summary>A focused primary/secondary binding editor for explicit registered IDs.
    /// Construction performs no discovery or preference reads. Use with CreateMenuPage or Open.
    /// GetChords, SetChords and Reset remain owned by the registered provider, as in Controls+.</summary>
    public sealed class UiBindingsPage : IUiPage, IUiPageInputPolicy
    {
        private readonly string title, description;
        private readonly string[] ids;
        private readonly UiFrame frame = new UiFrame(new Rectangle(24, 20, 432, 320));
        private readonly BindingCaptureSession capture = new BindingCaptureSession();
        private int index, slot;
        private string status;
        private UiBindingDefinition capturedDefinition;
        public bool WantsClose { get; private set; }
        public bool HandlesCancel { get { return true; } }

        /// <summary>Show only these stable binding IDs, in order. Missing IDs stay unavailable;
        /// an empty selection is never expanded to every mod. Description may be null.</summary>
        public UiBindingsPage(string title, string description, params string[] bindingIds)
        {
            if (bindingIds == null) throw new ArgumentNullException("bindingIds");
            if (bindingIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Binding IDs must not be empty", "bindingIds");
            this.title = string.IsNullOrWhiteSpace(title) ? "BINDS" : title;
            this.description = description;
            ids = bindingIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public void OnOpen() { WantsClose = false; index = slot = 0; status = null; capture.Cancel(); capturedDefinition = null; }
        public void OnClose() { capture.Cancel(); capturedDefinition = null; UiPointer.CancelCapture(this); }
        internal UiBindingDefinition Selected()
        {
            return ids.Length == 0 ? null : UIApi.GetBindings().FirstOrDefault(b => string.Equals(b.Id, ids[index], StringComparison.OrdinalIgnoreCase));
        }
        private static PadInstance Main()
        {
            if (ControllerManager.instance == null) return null;
            var main = ControllerManager.instance.GetMain();
            return main != null && main.IsValid && main.IsConnected ? main : null;
        }
        public void Update(UiInput input, float delta)
        {
            var main = Main();
            var definition = Selected();
            bool available = main != null && Game1.instance != null && Game1.instance.IsActive;
            if (capture.Active)
            {
                if (ControllerManager.instance != null) ControllerManager.instance.MenuController.ConsumePadPresses();
                int[] buttons;
                if (capture.Update(main, main == null ? null : main.GetPad().GetSaveIdentifier(), available && ReferenceEquals(definition, capturedDefinition),
                    main == null ? null : main.GetPad().GetPressedButtons(), out buttons))
                    Edit(() => BindingSlots.Set(definition, slot, new UiChord(buttons)), "Saved");
                else if (!capture.Active) { status = "Capture cancelled"; UiSounds.Play(UiSound.Back); }
                return;
            }
            if (input.Action == UiAction.Cancel) { WantsClose = true; UiSounds.Play(UiSound.Back); return; }
            if (!available) return;
            if (input.Action == UiAction.Left) UiSounds.Select(ref slot, 0);
            else if (input.Action == UiAction.Right) UiSounds.Select(ref slot, 1);
            else if (input.Action == UiAction.Up) Move(-1);
            else if (input.Action == UiAction.Down) Move(1);
            else if (definition != null && input.Action == UiAction.Confirm)
            {
                capturedDefinition = definition; status = null;
                capture.Begin(main, main.GetPad().GetSaveIdentifier(), definition.SupportsChords ? 2 : 1);
                UiSounds.Play(UiSound.Confirm);
            }
            else if (definition != null && input.Action == UiAction.Secondary) Edit(() => BindingSlots.Set(definition, slot, null), "Cleared");
            else if (definition != null && input.Action == UiAction.Reset) Edit(definition.Reset, "Defaults restored");
        }
        private void Edit(Action action, string success)
        {
            try { action(); status = success; UiSounds.Play(UiSound.Change); }
            catch (Exception error) { UiSounds.Play(UiSound.Error); status = "Could not save binding"; Console.WriteLine("[JK Runtime UI] Binding edit: " + error.GetBaseException().Message); }
        }
        private void Move(int direction)
        {
            if (ids.Length > 1) { UiSounds.Select(ref index, (index + direction + ids.Length) % ids.Length); status = null; }
            else UiSounds.Select(ref slot, direction < 0 ? 0 : 1);
        }
        public void Draw()
        {
            UiPointer.BeginSurface(this, !capture.Active);
            frame.Draw();
            UiTheme.TextLine(UiTheme.FitText(title.ToUpperInvariant(), 400, false), new Vector2(40, 36), UiTheme.Text, false);
            var main = Main(); var definition = Selected();
            UiTheme.TextLine(UiTheme.FitText(main == null ? "No input device" : main.GetPad().GetPrintName(), 400, true), new Vector2(40, 68), UiTheme.Muted, true);
            UiTheme.TextLine(UiTheme.FitText(definition == null ? (ids.Length == 0 ? "No bindings selected" : "Unavailable: " + ids[index]) : definition.Label, ids.Length > 1 ? 290 : 400, false), new Vector2(40, 98), UiTheme.Gold, false);
            if (ids.Length > 1)
            {
                UiTheme.Keycap("<", new Rectangle(338, 93, 30, 24), false);
                UiTheme.Keycap(">", new Rectangle(410, 93, 30, 24), false);
                UiTheme.TextLine((index + 1) + "/" + ids.Length, new Vector2(374, 100), UiTheme.Muted, true);
                UiPointer.ActionRegion(new Rectangle(338, 93, 30, 24), UiAction.Up);
                UiPointer.ActionRegion(new Rectangle(410, 93, 30, 24), UiAction.Down);
                UiPointer.ScrollRegion(new Rectangle(36, 90, 408, 28), delta => Move(delta > 0 ? -1 : 1));
            }
            UiChord[] chords = new UiChord[0];
            if (definition != null && main != null)
                try { chords = definition.GetChords() ?? chords; }
                catch { status = "Binding unavailable on this device"; }
            for (int i = 0; i < 2; i++)
            {
                int chosen = i, x = 40 + i * 206;
                UiTheme.TextLine(i == 0 ? "PRIMARY" : "SECONDARY", new Vector2(x, 134), UiTheme.Muted, true);
                string text = main != null && i < chords.Length && chords[i] != null
                    ? string.Join("+", chords[i].Buttons.Select(b => UiTheme.NormalizeKey(main.GetPad().ButtonToString(b)))) : "-";
                var bounds = new Rectangle(x, 152, 192, 28);
                UiTheme.Keycap(UiTheme.FitText(text, 176, true), bounds, slot == i);
                if (main != null && definition != null) UiPointer.ActionRegion(bounds, UiAction.Confirm, () => UiSounds.Select(ref slot, chosen));
            }
            UiTheme.WrappedText(description ?? "", new Rectangle(40, 198, 400, 36), UiTheme.Muted);
            UiTheme.TextLine(definition != null && !definition.SupportsChords ? "Assign one button per slot." : "Assign one key or a two-button chord.", new Vector2(40, 240), UiTheme.Muted, true);
            UiTheme.TextLine(capture.Active ? "PRESS, HOLD, RELEASE" : status ?? (definition == null ? "Binding provider unavailable" : "Choose a slot to rebind"), new Vector2(40, 264), UiTheme.Cyan, true);
            if (!capture.Active)
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
                    UiInputHints.Command(UiAction.Confirm, "REBIND"), UiInputHints.Command(UiAction.Secondary, "CLEAR"),
                    UiInputHints.Command(UiAction.Reset, "DEFAULT"), UiInputHints.Command(UiAction.Cancel, "BACK"));
        }
    }

    internal static class BindingSlots
    {
        internal static void Set(UiBindingDefinition definition, int slot, UiChord chord)
        {
            if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException("slot");
            var values = new List<UiChord>(definition.GetChords() ?? new UiChord[0]);
            while (values.Count <= slot) values.Add(null);
            values[slot] = chord;
            var unique = new List<UiChord>();
            foreach (var value in values)
                if (value != null && !value.IsEmpty && !unique.Any(x => x.Buttons.OrderBy(b => b).SequenceEqual(value.Buttons.OrderBy(b => b)))) unique.Add(value);
            definition.SetChords(unique.ToArray());
        }
    }

    internal sealed class BindingCaptureSession
    {
        private object device;
        private string deviceId;
        private bool waiting;
        private ChordCapture capture;
        internal bool Active { get { return capture != null; } }
        internal void Begin(object current, string id, int maximum)
        { Cancel(); device = current; deviceId = id; capture = new ChordCapture(maximum); waiting = true; }
        internal void Cancel() { capture = null; device = null; deviceId = null; waiting = false; }
        internal bool Update(object current, string id, bool available, int[] buttons, out int[] result)
        {
            result = null;
            if (!Active) return false;
            if (!available || !ReferenceEquals(current, device) || id != deviceId) { Cancel(); return false; }
            if (waiting) { if (buttons == null || buttons.Length == 0) waiting = false; return false; }
            if (!capture.Update(buttons, out result)) return false;
            Cancel(); return true;
        }
    }
}
