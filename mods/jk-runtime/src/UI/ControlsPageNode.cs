using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal sealed class ControlsPageNode : IBTnode, JumpKing.Util.IDrawable
    {
        private IList<UiBindingDefinition> bindings;
        private readonly UiFrame frame = new UiFrame(new Rectangle(24, 20, 432, 320));
        private int index;
        private readonly UiListViewport viewport = new UiListViewport();
        private int slot;
        private bool capturing;
        private bool waitingForRelease;
        private bool releasingCapture;
        private bool entering = true;
        private ChordCapture chordCapture;

        internal ControlsPageNode()
        {
            bindings = UIApi.GetBindings();
        }

        protected override void OnNewRun()
        {
            RefreshBindings();
        }

        protected override void ResumeRun()
        {
            RefreshBindings();
        }

        internal static ControlsPageNode Create(object factory, GuiFormat format)
        {
            ControlsPageNode page = new ControlsPageNode();
            VanillaMenuAdapter.AddDrawable(factory, page);
            return page;
        }

        protected override BTresult MyRun(TickData data)
        {
            int count = bindings.Count;
            if (count == 0) return BTresult.Failure;
            PadInstance main = ControllerManager.instance.GetMain();
            if (entering)
            {
                UiInputRouter.Consume();
                if (main.GetPad().GetPressedButtons().Length == 0) entering = false;
                return BTresult.Running;
            }
            if (capturing || releasingCapture)
            {
                int[] pressed = main.GetPad().GetPressedButtons();
                UiInputRouter.Consume();
                if (releasingCapture)
                {
                    if (pressed.Length == 0) releasingCapture = false;
                    return BTresult.Running;
                }
                if (waitingForRelease)
                {
                    if (pressed.Length == 0) waitingForRelease = false;
                    return BTresult.Running;
                }
                int[] captured;
                if (chordCapture.Update(pressed, out captured))
                {
                    SetSlot(bindings[index], slot, new UiChord(captured));
                    capturing = false;
                    releasingCapture = true;
                    chordCapture = null;
                    UiSounds.Play(UiSound.Confirm);
                }
                return BTresult.Running;
            }

            PadState state = ControllerManager.instance.MenuController.GetPadState();
            UiAction action = UiPointer.Read(this, UiInputRouter.Read(state, true)).Action;
            if (action != UiAction.None) UiInputRouter.Consume();
            if (action == UiAction.Cancel)
            {
                entering = true;
                UiSounds.Play(UiSound.Back);
                return BTresult.Success;
            }
            HandleAction(action);
            return BTresult.Running;
        }
        internal void HandleAction(UiAction action)
        {
            int count = bindings.Count;
            if (count == 0) return;
            if (action == UiAction.Up) { UiSounds.Select(ref index, (index + count - 1) % count); viewport.FollowSelection(index, count, BindingColumns.ListRows); }
            else if (action == UiAction.Down) { UiSounds.Select(ref index, (index + 1) % count); viewport.FollowSelection(index, count, BindingColumns.ListRows); }
            else if (action == UiAction.Left) UiSounds.Select(ref slot, BindingColumns.Step(slot, -1, bindings[index]));
            else if (action == UiAction.Right) UiSounds.Select(ref slot, BindingColumns.Step(slot, 1, bindings[index]));
            else if (slot == 2 && bindings[index].Mode != null)
            {
                try
                {
                    if (action == UiAction.Confirm) bindings[index].Mode.Cycle();
                    else if (action == UiAction.Reset) bindings[index].Mode.Reset();
                    else return;
                    UiSounds.Play(UiSound.Change);
                }
                catch (Exception error) { UiSounds.Play(UiSound.Error); Console.WriteLine("[JK Runtime UI] Binding mode: " + error.GetBaseException().Message); }
            }
            else if (action == UiAction.Confirm)
            {
                capturing = true;
                waitingForRelease = true;
                chordCapture = new ChordCapture(bindings[index].SupportsChords ? 2 : 1);
                UiSounds.Play(UiSound.Confirm);
            }
            else if (action == UiAction.Reset)
            {
                bindings[index].Reset();
                UiSounds.Play(UiSound.Confirm);
            }
            slot = BindingColumns.Clamp(slot, bindings[index]);
        }

        public void Draw()
        {
            if (last_result != BTresult.Running) return;
            UiPointer.BeginSurface(this, !capturing && !releasingCapture && !entering);
            frame.Draw();
            bool modes = true;
            UiTheme.TextLine("CONTROLS+", new Vector2(40, 34), UiTheme.Text, false);
            BindingColumns.Headers(modes);
            const int visibleRows = BindingColumns.ListRows;
            int count = bindings.Count;
            int first = viewport.FirstVisible(count, visibleRows, index);
            int last = Math.Min(count, first + visibleRows);
            int y = 62;
            PadInstance main = ControllerManager.instance.GetMain();
            for (int i = first; i < last; i++)
            {
                Rectangle row = new Rectangle(36, y - 2, 404, 28);
                int hoverIndex = i;
                UiPointer.Region(new Rectangle(row.X, row.Y, BindingColumns.X(2, modes) - row.X - 4, row.Height), () => Hover(hoverIndex, slot), null);
                UiPointer.ActionRegion(new Rectangle(BindingColumns.X(0, modes), row.Y, BindingColumns.Width(modes), row.Height), UiAction.Confirm, () => Hover(hoverIndex, 0));
                UiPointer.ActionRegion(new Rectangle(BindingColumns.X(1, modes), row.Y, BindingColumns.Width(modes), row.Height), UiAction.Confirm, () => Hover(hoverIndex, 1));
                if (i == index) UiTheme.Panel(row, new Color(29, 36, 38), UiTheme.Gold);
                UiBindingDefinition binding = bindings[i];
                UiTheme.TextLine(UiTheme.FitText(binding.Group.ToUpperInvariant(), BindingColumns.LabelWidth(modes) - 12, true), new Vector2(44, y + 1), UiTheme.Gold, true);
                UiTheme.TextLine(UiTheme.FitText(binding.Label, BindingColumns.LabelWidth(modes) - 12, true), new Vector2(44, y + 12), UiTheme.Text, true);
                UiChord[] values = binding.GetChords() ?? new UiChord[0];
                DrawBind(main, values, 0, BindingColumns.X(0, modes), y + 4, BindingColumns.Width(modes), i == index && slot == 0);
                DrawBind(main, values, 1, BindingColumns.X(1, modes), y + 4, BindingColumns.Width(modes), i == index && slot == 1);
                if (binding != null)
                {
                    var modeBounds = new Rectangle(BindingColumns.X(2, true), y + 1, BindingColumns.Width(true), 23);
                    BindingColumns.DrawMode(binding, modeBounds, i == index && slot == 2);
                    if (BindingColumns.Editable(binding)) UiPointer.ActionRegion(modeBounds, UiAction.Confirm, () => Hover(hoverIndex, 2));
                }
                y += 30;
            }
            UiPointer.ScrollRegion(new Rectangle(36, 60, 410, visibleRows * 30), delta => Hover(viewport.Scroll(-delta, bindings.Count, visibleRows, index), slot));
            UiTheme.ScrollBar(new Rectangle(444, 60, 3, visibleRows * 30 - 2), count, first, visibleRows);
            if (!capturing && !releasingCapture && count > 0 && bindings[index].Mode != null)
                BindingColumns.Message(frame.Bounds, bindings[index].Mode.Reason, UiTheme.Muted);
            if (capturing || releasingCapture)
                UiTheme.TextLine(capturing ? "PRESS, HOLD, RELEASE" : "RELEASE BUTTON", new Vector2(40, UiTheme.FooterRow(frame.Bounds).Y + 2), UiTheme.Cyan, true);
            else
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
                    UiInputHints.Command(UiAction.Confirm, slot == 2 ? "CHANGE" : "REBIND"),
                    UiInputHints.Command(UiAction.Reset, "DEFAULT"),
                    UiInputHints.Command(UiAction.Cancel, "BACK"));
        }

        private static void DrawBind(PadInstance main, UiChord[] values, int slot, int x, int y, int maxWidth, bool selected)
        {
            string text = slot < values.Length ? FormatChord(main, values[slot]) : "-";
            UiTheme.Keycap(UiTheme.FitText(text, maxWidth - 8, true), new Rectangle(x, y - 3, maxWidth, 23), selected);
        }

        private static void SetSlot(UiBindingDefinition definition, int slot, UiChord chord)
        { BindingSlots.Set(definition, slot, chord); }

        private static string FormatChord(PadInstance main, UiChord chord)
        {
            string result = string.Empty;
            foreach (int button in chord == null ? new int[0] : chord.Buttons)
            {
                string label = UiTheme.NormalizeKey(main.GetPad().ButtonToString(button));
                result = result.Length == 0 ? label : result + "+" + label;
            }
            return result.Length == 0 ? "-" : result;
        }

        private void Hover(int nextIndex, int nextSlot)
        {
            if (index == nextIndex && slot == nextSlot) return;
            index = nextIndex; slot = BindingColumns.Clamp(nextSlot, bindings[index]); UiSounds.Play(UiSound.Move);
        }

        private void RefreshBindings()
        {
            bindings = UIApi.GetBindings();
            index = Math.Max(0, Math.Min(index, bindings.Count - 1));
            slot = bindings.Count == 0 ? 0 : BindingColumns.First(bindings[index]);
            capturing = false;
            waitingForRelease = false;
            releasingCapture = false;
            chordCapture = null;
            entering = true;
            ControllerManager.instance.MenuController.ConsumePadPresses();
        }
    }
}
