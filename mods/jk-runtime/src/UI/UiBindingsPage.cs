using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    /// <summary>A focused primary/secondary binding editor for explicit registered IDs
    /// construction performs no discovery or preference reads. use with CreateMenuPage or Open
    /// GetChords, SetChords and Reset remain owned by the registered provider, as in Controls+.</summary>
    public sealed class UiBindingsPage : IUiPage, IUiPageInputPolicy
    {
        private readonly string title, description;
        private readonly string[] ids;
        private readonly UiFrame frame = new UiFrame(new Rectangle(24, 20, 432, 320));
        private readonly BindingCaptureSession capture = new BindingCaptureSession();
        private readonly UiListViewport viewport = new UiListViewport();
        private const int ListRows = BindingColumns.ListRows;
        private int index, slot;
        private string status;
        private UiBindingDefinition capturedDefinition;
        public bool WantsClose { get; private set; }
        public bool HandlesCancel { get { return true; } }
        /// <summary>Show actions as Controls+-style rows instead of a single-action editor.</summary>
        public bool ShowList { get; set; }

        /// <summary>Show only these stable binding IDs, in order. missing IDs stay unavailable;
        /// an empty selection is never expanded to every mod. Description may be null</summary>
        public UiBindingsPage(string title, string description, params string[] bindingIds)
        {
            if (bindingIds == null) throw new ArgumentNullException("bindingIds");
            if (bindingIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Binding IDs must not be empty", "bindingIds");
            this.title = string.IsNullOrWhiteSpace(title) ? "BINDS" : title;
            this.description = description;
            ids = bindingIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public void OnOpen() { WantsClose = false; index = 0; slot = BindingColumns.First(Selected()); status = null; capture.Cancel(); capturedDefinition = null; viewport.Reset(); }
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
            slot = BindingColumns.Clamp(slot, definition);
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
            if (input.Action == UiAction.Left) UiSounds.Select(ref slot, BindingColumns.Step(slot, -1, definition));
            else if (input.Action == UiAction.Right) UiSounds.Select(ref slot, BindingColumns.Step(slot, 1, definition));
            else if (input.Action == UiAction.Up) Move(-1);
            else if (input.Action == UiAction.Down) Move(1);
            else if (definition != null && slot == 2)
            {
                if (input.Action == UiAction.Confirm) Edit(definition.Mode.Cycle, "Mode saved");
                else if (input.Action == UiAction.Reset) Edit(definition.Mode.Reset, "Default mode restored");
            }
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
            else UiSounds.Select(ref slot, BindingColumns.Step(slot, direction, Selected()));
            slot = BindingColumns.Clamp(slot, Selected());
            if (ShowList) viewport.FollowSelection(index, ids.Length, ListRows);
        }
        public void Draw()
        {
            UiPointer.BeginSurface(this, !capture.Active);
            if (ShowList) { DrawList(); return; }
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
            var modeBounds = new Rectangle(40, 152, 120, 28);
            UiTheme.TextLine("MODE", new Vector2(40, 134), UiTheme.Muted, true);
            BindingColumns.DrawMode(definition, modeBounds, slot == 2);
            if (main != null && BindingColumns.Editable(definition)) UiPointer.ActionRegion(modeBounds, UiAction.Confirm, () => UiSounds.Select(ref slot, 2));
            for (int i = 0; i < 2; i++)
            {
                int chosen = i, x = 176 + i * 134;
                UiTheme.TextLine(i == 0 ? "PRIMARY" : "SECONDARY", new Vector2(x, 134), UiTheme.Muted, true);
                string text = main != null && i < chords.Length && chords[i] != null
                    ? string.Join("+", chords[i].Buttons.Select(b => UiTheme.NormalizeKey(main.GetPad().ButtonToString(b)))) : "-";
                var bounds = new Rectangle(x, 152, 128, 28);
                UiTheme.Keycap(UiTheme.FitText(text, 112, true), bounds, slot == i);
                if (main != null && definition != null) UiPointer.ActionRegion(bounds, UiAction.Confirm, () => UiSounds.Select(ref slot, chosen));
            }
            UiTheme.WrappedText(description ?? "", new Rectangle(40, 198, 400, 36), UiTheme.Muted);
            UiTheme.TextLine(definition != null && definition.Mode != null && definition.Mode.Reason != null ? UiTheme.FitText(definition.Mode.Reason, 400, true)
                : definition == null || definition.Mode == null ? "Input semantics unknown; mode locked." : "Choose Mode, Primary or Secondary.", new Vector2(40, 240), UiTheme.Muted, true);
            UiTheme.TextLine(capture.Active ? "PRESS, HOLD, RELEASE" : status ?? (definition == null ? "Binding provider unavailable" : "Choose a slot to rebind"), new Vector2(40, 264), UiTheme.Cyan, true);
            if (!capture.Active)
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds), BindingColumns.Commands(slot));
        }

        private void DrawList()
        {
            frame.Draw();
            var main = Main(); var bindings = UIApi.GetBindings();
            bool modes = true;
            UiTheme.TextLine(UiTheme.FitText(title.ToUpperInvariant(), BindingColumns.LabelWidth(modes), false), new Vector2(40, 34), UiTheme.Text, false);
            BindingColumns.Headers(modes);
            int first = viewport.FirstVisible(ids.Length, ListRows, index), last = Math.Min(ids.Length, first + ListRows);
            for (int i = first; i < last; i++)
            {
                int rowIndex = i, y = 62 + (i - first) * 30;
                var definition = bindings.FirstOrDefault(b => string.Equals(b.Id, ids[i], StringComparison.OrdinalIgnoreCase));
                var row = new Rectangle(36, y - 2, 404, 28);
                if (i == index) UiTheme.Panel(row, new Color(29, 36, 38), UiTheme.Gold);
                UiTheme.TextLine(UiTheme.FitText(definition == null ? ids[i] : definition.Label, BindingColumns.LabelWidth(modes), true),
                    new Vector2(44, y + 7), definition == null ? UiTheme.Disabled : UiTheme.Text, true);
                UiPointer.Region(new Rectangle(row.X, row.Y, BindingColumns.X(2, modes) - row.X - 4, row.Height), () => SelectRow(rowIndex, slot), null);
                UiChord[] chords = new UiChord[0];
                if (definition != null && main != null)
                    try { chords = definition.GetChords() ?? chords; } catch { status = "Binding unavailable on this device"; }
                for (int s = 0; s < 2; s++)
                {
                    int selectedSlot = s;
                    string text = main != null && s < chords.Length && chords[s] != null
                        ? string.Join("+", chords[s].Buttons.Select(b => UiTheme.NormalizeKey(main.GetPad().ButtonToString(b)))) : "-";
                    var key = new Rectangle(BindingColumns.X(s, modes), y + 1, BindingColumns.Width(modes), 23);
                    UiTheme.Keycap(UiTheme.FitText(text, key.Width - 8, true), key, i == index && slot == s);
                    if (main != null && definition != null) UiPointer.ActionRegion(key, UiAction.Confirm, () => SelectRow(rowIndex, selectedSlot));
                }
                if (definition != null)
                {
                    var modeBounds = new Rectangle(BindingColumns.X(2, true), y + 1, BindingColumns.Width(true), 23);
                    BindingColumns.DrawMode(definition, modeBounds, i == index && slot == 2);
                    if (main != null && BindingColumns.Editable(definition)) UiPointer.ActionRegion(modeBounds, UiAction.Confirm, () => SelectRow(rowIndex, 2));
                }
            }
            UiTheme.ScrollBar(new Rectangle(444, 60, 3, ListRows * 30 - 2), ids.Length, first, ListRows);
            UiPointer.ScrollRegion(new Rectangle(36, 60, 410, ListRows * 30), delta => SelectRow(viewport.Scroll(-delta, ids.Length, ListRows, index), slot));
            string message = capture.Active ? "PRESS, HOLD, RELEASE" : status ?? (ids.Length == 0 ? "No bindings available" : main == null ? "No input device" : "");
            var selected = Selected();
            if (message.Length == 0 && selected != null && selected.Mode != null) message = selected.Mode.Reason ?? "";
            BindingColumns.Message(frame.Bounds, message, UiTheme.Cyan);
            if (!capture.Active) UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds), BindingColumns.Commands(slot));
        }
        private void SelectRow(int row, int selectedSlot)
        {
            UiSounds.Select(ref index, row); UiSounds.Select(ref slot, BindingColumns.Clamp(selectedSlot, Selected())); status = null;
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
