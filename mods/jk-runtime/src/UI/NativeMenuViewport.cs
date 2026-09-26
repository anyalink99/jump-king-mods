using System;
using System.Runtime.CompilerServices;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    // Native CalculateBounds caps width only. Preserve its items and input tree,
    // but keep extended menus on screen and reveal the selected row when scrolling.
    internal static class NativeMenuViewport
    {
        internal sealed class Layout
        {
            internal IMenuItem[] Items;
            internal Point[] Sizes;
            internal Rectangle Bounds;
            internal int First, Last, ContentY;
            internal GuiFormat Format;
            internal bool Scrolls;
        }
        private static readonly ConditionalWeakTable<MenuSelector, Layout> Layouts = new ConditionalWeakTable<MenuSelector, Layout>();
        private static readonly Rectangle Safe = new Rectangle(12, 12, 456, 336);

        internal static bool TryGet(MenuSelector menu, out Layout layout) { return Layouts.TryGetValue(menu, out layout); }

        internal static bool Draw(MenuSelector menu, IMenuItem[] items, int selected)
        {
            Layout layout;
            Rectangle native = menu.GetBounds();
            bool known = Layouts.TryGetValue(menu, out layout);
            if (!known && Safe.Contains(native)) return false;
            if (!known) { layout = new Layout(); Layouts.Add(menu, layout); }
            bool changed = !ReferenceEquals(layout.Items, items) || layout.Bounds != native;
            if (layout.Sizes == null || layout.Sizes.Length != items.Length) { layout.Sizes = new Point[items.Length]; changed = true; }
            for (int i = 0; i < items.Length; i++)
            {
                Point size = items[i].GetSize();
                if (size != layout.Sizes[i]) { layout.Sizes[i] = size; changed = true; }
            }
            if (changed)
            {
                layout.Items = items; layout.Format = VanillaMenuAdapter.GetFormat(menu);
                Rectangle natural = layout.Format.CalculateBounds(items);
                layout.Scrolls = natural.Height > Safe.Height;
                int height = Math.Min(Safe.Height, natural.Height), width = Math.Min(Safe.Width, natural.Width);
                layout.Bounds = new Rectangle(Math.Max(Safe.Left, Math.Min(Safe.Right - width, natural.X)),
                    Math.Max(Safe.Top, Math.Min(Safe.Bottom - height, natural.Y)), width, height);
                VanillaMenuAdapter.SetRenderedBounds(menu, layout.Bounds);
            }
            int contentHeight = layout.Bounds.Height - layout.Format.padding.height - (layout.Scrolls ? 14 : 0);
            layout.First = Math.Min(Math.Max(0, layout.First), selected);
            layout.Last = End(layout, layout.First, contentHeight);
            while (selected >= layout.Last && layout.First < selected)
            { layout.First++; layout.Last = End(layout, layout.First, contentHeight); }
            // Fill unused space when the list shrinks or selection wraps upward.
            while (layout.First > 0 && End(layout, layout.First - 1, contentHeight) >= layout.Last)
                layout.First--;
            layout.Last = End(layout, layout.First, contentHeight);
            layout.ContentY = layout.Bounds.Y + layout.Format.padding.top;
            menu.DrawBgOnly();
            int y = layout.ContentY;
            for (int i = layout.First; i < layout.Last; i++)
            {
                bool active = i == selected;
                items[i].Draw(layout.Bounds.X + layout.Format.padding.left + (active ? 5 : 0), y, active);
                if (active && !(items[i] is UnSelectable))
                    Game1.spriteBatch.Draw(Game1.instance.contentManager.gui.Cursor, new Vector2(layout.Bounds.X, y), Color.White);
                y += layout.Sizes[i].Y + layout.Format.element_margin;
            }
            if (layout.Scrolls)
                UiTheme.TextLine((selected + 1) + "/" + items.Length, new Vector2(layout.Bounds.Right - 42, layout.Bounds.Bottom - 18), UiTheme.Muted, true);
            return true;
        }

        private static int End(Layout layout, int first, int height)
        {
            int used = 0, last = first;
            while (last < layout.Items.Length)
            {
                int next = layout.Sizes[last].Y + (last == first ? 0 : layout.Format.element_margin);
                if (last > first && used + next > height) break;
                used += next; last++;
            }
            return last;
        }
    }
}
