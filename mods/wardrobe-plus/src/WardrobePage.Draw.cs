using System;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace WardrobePlus
{
    internal sealed partial class WardrobePage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(12, 12, 456, 336));
        public void Draw()
        {
            UiPointer.BeginSurface(this);
            frame.Draw();
            if (textEntry != null) { textEntry.Draw(); return; }
            Line(fitting ? "POSITION FITTING" : page.Title, 25, 26, 422, false, UiTheme.Gold);
            if (fitting) DrawFit(); else DrawRows();
            preview.Draw(draft, Controller.Data.Animate, tryOnItem, checkerboard, fitting);
            Line("WORN OUTFIT", 285, 48, 167, true, UiTheme.Cyan);
            string hint = page.Description;
            if (!fitting && page.Rows.Count > 0 && page.Rows[page.Index].CurrentHint.Length > 0) hint = page.Rows[page.Index].CurrentHint;
            if (fitting) hint = "Changes are immediate. Back keeps them. Ctrl+Z undoes a movement; Ctrl+Y restores it.";
            string problem = preview.Error;
            if (preview.Prepared != null && !fitting)
            {
                foreach (var resolved in preview.Prepared.Resolved.Values)
                    if (resolved.Warning.Length > 0) { problem = resolved.Warning + ". Showing: " + resolved.Label; break; }
            }
            if (!Hooks.Installed) problem = Hooks.Error;
            if (Controller.Store.ReadOnly) problem = Controller.Store.Warning;
            UiTheme.WrappedText(problem.Length > 0 ? problem : hint, new Rectangle(25, 243, 427, 28), problem.Length > 0 ? new Color(255, 165, 115) : UiTheme.Muted);
            Line(Controller.LastError.Length > 0 ? Controller.Status : Controller.Pending || Controller.Unsaved ? "Saving..." : Controller.Status, 25, 272, 427, true, UiTheme.Cyan);
            DrawCommands();
        }
        private void DrawRows()
        {
            UiPointer.ScrollRegion(new Rectangle(22, 62, 249, 178), delta => SelectRow(page.Viewport.Scroll(-delta, page.Rows.Count, VisibleRows, page.Index)));
            int visible = VisibleRows, height = page.Main ? 32 : 20;
            int first = page.Viewport.FirstVisible(page.Rows.Count, visible, page.Index);
            for (int i = first; i < Math.Min(page.Rows.Count, first + visible); i++)
            {
                int y = 65 + (i - first) * height;
                int rowIndex = i;
                UiPointer.ActionRegion(new Rectangle(22, y - 3, 249, height), UiAction.Confirm, () => SelectRow(rowIndex));
                bool selected = i == page.Index;
                if (selected) UiTheme.Panel(new Rectangle(22, y - 3, 249, height), new Color(31, 48, 48), new Color(64, 90, 88));
                Line((selected ? "> " : "  ") + page.Rows[i].CurrentLabel, 27, y, 239, true, selected ? UiTheme.Cyan : UiTheme.Text);
                if (page.Main && page.Rows[i].Summary != null)
                    Line(page.Rows[i].Summary(), 39, y + 13, 223, true, UiTheme.Muted);
            }
            Line(page.Rows.Count == 0 ? "Empty" : (page.Index + 1) + " / " + page.Rows.Count, 25, 225, 245, true, UiTheme.Muted);
        }
        private void DrawFit()
        {
            var fit = CurrentFit();
            Line("X: " + fit.X + " px", 35, 92, 220, false, UiTheme.Text);
            Line("Y: " + fit.Y + " px", 35, 121, 220, false, UiTheme.Text);
            DrawFitButton("-", 186, 87, UiAction.Left); DrawFitButton("+", 219, 87, UiAction.Right);
            DrawFitButton("-", 186, 116, UiAction.Up); DrawFitButton("+", 219, 116, UiAction.Down);
            Line(allPoses ? "ALL POSES" : "THIS POSE", 35, 158, 220, true, UiTheme.Cyan);
            UiTheme.WrappedText(allPoses ? "Move this item for all poses. Use More options to adjust a single pose." : "Only the selected pose will change.", new Rectangle(35, 186, 223, 50), UiTheme.Muted);
        }
        private void DrawFitButton(string label, int x, int y, UiAction action)
        {
            var bounds = new Rectangle(x, y, 25, 20);
            bool hovered = UiPointer.Visible && bounds.Contains(UiPointer.Position);
            UiTheme.Panel(bounds, UiTheme.PanelFill, hovered ? UiTheme.Cyan : UiTheme.Border);
            Line(label, x + 9, y + 2, 14, true, hovered ? UiTheme.Cyan : UiTheme.Gold);
            UiPointer.ActionRegion(bounds, action);
        }
        private void DrawCommands()
        {
            bool naming = false;
            string secondary = naming ? "Erase" : fitting ? "Flip" : "Favorite";
            var navigation = new UiCommand(UiInputHints.Key(UiAction.Up) + "/" + UiInputHints.Key(UiAction.Down), fitting || naming ? "Move" : "Navigate");
            if (fitting || naming)
            {
                var horizontal = new UiCommand(UiInputHints.Key(UiAction.Left) + "/" + UiInputHints.Key(UiAction.Right), "Move");
                if (fitting) UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds, 1), navigation, horizontal, UiInputHints.Command(UiAction.Reset, "Reset"));
                else UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds, 1), navigation, horizontal);
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds), UiInputHints.Command(UiAction.Secondary, secondary),
                    UiInputHints.Command(UiAction.Confirm, fitting ? "Done" : "Select"),
                    UiInputHints.Command(UiAction.Cancel, "Back"));
            }
            else if (page.Main)
            {
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds, 1), navigation,
                    UiInputHints.Command(UiAction.Left, "Outfits"), UiInputHints.Command(UiAction.Right, "More"));
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds), UiInputHints.Command(UiAction.Confirm, "Choose"),
                    UiInputHints.Command(UiAction.Secondary, "Save outfit"), UiInputHints.Command(UiAction.Cancel, "Back"));
            }
            else
            {
                if (page.Rows.Count > 0 && page.Rows[page.Index].SourceId.Length > 0)
                    UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds, 1), navigation, UiInputHints.Command(UiAction.Secondary, secondary));
                else UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds, 1), navigation);
                UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
                    UiInputHints.Command(UiAction.Confirm, "Select"), UiInputHints.Command(UiAction.Cancel, "Back"));
            }
        }
        private void Line(string text, int x, int y, int width, bool small, Color color)
        { textRenderer.DrawFitted(text ?? "", width, new Vector2(x,y), color, small); }
    }
}
