using System;
using System.Linq;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed partial class GimmickPage
    {
        private Color[] palette = new Color[0];
        private int paletteSelected;
        private bool PaletteOpen { get { return !browsing && title == "COLLISION COLOUR PALETTE"; } }
        private void Colours()
        {
            palette = snapshot.Where(r => r.Entry.Colour.HasValue).Select(r => r.Entry.Colour.Value).Distinct().OrderBy(c => c.PackedValue).ToArray();
            paletteSelected = Math.Max(0, Math.Min(palette.Length - 1, paletteSelected));
            Show("COLLISION COLOUR PALETTE", new UiListItem[0]);
        }
        private void ChooseColour(int index)
        { paletteSelected = index; Query.Colour = GimmickSearch.Hex(palette[index]); SaveQuery(); }
        private void PaletteUpdate(UiInput input)
        {
            if (input.Secondary) { EditColour(); return; }
            if (input.Confirm && palette.Length > 0) ChooseColour(paletteSelected);
            int step = input.Left ? -1 : input.Right ? 1 : input.Up ? -10 : input.Down ? 10 : 0;
            paletteSelected = Math.Max(0, Math.Min(palette.Length - 1, paletteSelected + step));
        }
        private void EditColour()
        { EditText("EXACT COLOUR", Query.Colour, value => { Color? colour; if (!GimmickSearch.Colour(value, out colour)) throw new InvalidOperationException("Use #RRGGBB or R,G,B (0-255)."); Query.Colour = value; Colours(); }); }
        private void PaletteDraw()
        {
            Button(new Rectangle(28, 54, 313, 25), "HEX / RGB: " + (Query.Colour ?? "Any"), EditColour);
            Button(new Rectangle(348, 54, 100, 25), "Clear", () => { Query.Colour = null; SaveQuery(); });
            int first = paletteSelected / 50 * 50;
            UiPointer.ScrollRegion(new Rectangle(28, 95, 420, 157), step => paletteSelected = Math.Max(0, Math.Min(palette.Length - 1, paletteSelected - step * 10)));
            for (int i = first; i < Math.Min(first + 50, palette.Length); i++) {
                int index = i; Color colour = palette[i];
                var bounds = new Rectangle(28 + (i - first) % 10 * 42, 95 + (i - first) / 10 * 31, 36, 25);
                UiTheme.Panel(bounds, colour, i == paletteSelected ? UiTheme.Gold : UiTheme.Border);
                if (Query.Colour == GimmickSearch.Hex(colour)) UiTheme.TextLine("*", new Vector2(bounds.X + 13, bounds.Y + 6), colour.R + colour.G + colour.B > 390 ? Color.Black : Color.White, true);
                UiPointer.Region(bounds, null, () => ChooseColour(index));
            }
            string selection = palette.Length == 0 ? "No indexed colours." : GimmickSearch.Hex(palette[paletteSelected]) + "  RGB " + GimmickBlocks.RGB(palette[paletteSelected]);
            UiTheme.TextLine(selection, new Vector2(28, 258), UiTheme.Text, true);
            UiTheme.TextLine("Palette " + (palette.Length == 0 ? 0 : first / 50 + 1) + " / " + Math.Max(1, (palette.Length + 49) / 50) + "  /  " + palette.Length + " colours", new Vector2(28, 279), UiTheme.Muted, true);
            Button(new Rectangle(28, 307, 94, 27), "Previous", () => paletteSelected = Math.Max(0, paletteSelected - 50));
            Button(new Rectangle(130, 307, 94, 27), "Next", () => paletteSelected = Math.Min(Math.Max(0, palette.Length - 1), paletteSelected + 50));
            Button(new Rectangle(354, 307, 94, 27), "Done", Back);
        }
    }
}
