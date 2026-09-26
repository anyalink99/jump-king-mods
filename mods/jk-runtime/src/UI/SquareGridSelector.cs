using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal struct GridSubtitle
    {
        internal readonly string Text;

        private GridSubtitle(string text)
        {
            Text = text;
        }

        internal static GridSubtitle ForStatus(string value)
        {
            return ForText(value);
        }

        internal static GridSubtitle ForText(string value)
        {
            return new GridSubtitle((value ?? string.Empty).Trim());
        }
    }

    internal sealed class GridMenuItem : IBTSimpleMenuItem
    {
        internal readonly string Label;
        internal readonly string Subtitle;
        internal readonly Sprite Icon;
        internal readonly Color IconColor;
        private readonly ConditionalIconTextInfo conditionalIcon;
        private static readonly FieldInfo NativeIcon = typeof(IconTextInfo).GetField("m_icon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo NativeIconColor = typeof(IconTextInfo).GetField("m_color_icon", BindingFlags.Instance | BindingFlags.NonPublic);
        internal SpriteFont LayoutFont;
        internal string[] TitleLines, SubtitleLines;
        internal int ContentHeight;
        private readonly IBTnode child;
        private readonly Func<bool> isVisible;

        internal GridMenuItem(string label, IBTnode node)
            : this(label, node, new GridSubtitle())
        {
        }

        internal bool ShowIcon { get { return Icon != null && (conditionalIcon == null || conditionalIcon.Condition); } }

        internal GridMenuItem(string label, IBTnode node, GridSubtitle subtitle)
            : this(label, node, subtitle, null)
        {
        }

        internal GridMenuItem(
            string label,
            IBTnode node,
            GridSubtitle subtitle,
            Func<bool> visibility)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Mod" : label;
            Subtitle = subtitle.Text ?? string.Empty;
            child = node;
            isVisible = visibility ?? delegate { return true; };
        }

        internal bool IsVisible { get { return isVisible(); } }

        internal GridMenuItem(string label, TextButton button)
            : this(label, button == null ? null : button.Child)
        {
        }

        internal GridMenuItem(
            string label,
            DoubleTextInfoButton button,
            GridSubtitle subtitle)
            : this(label, button == null ? null : button.Child, subtitle)
        {
            var title = button == null || button.Texts == null || button.Texts.Length == 0 ? null : button.Texts[0] as IconTextInfo;
            if (title != null && NativeIcon != null && NativeIconColor != null)
            {
                Icon = (Sprite)NativeIcon.GetValue(title);
                IconColor = (Color)NativeIconColor.GetValue(title);
                if (IconColor == Color.Transparent) IconColor = Color.White;
                conditionalIcon = title as ConditionalIconTextInfo;
            }
        }

        protected override BTresult MyRun(TickData data)
        {
            return child == null ? BTresult.Failure : child.Run(data);
        }

        public override void ResetResult()
        {
            base.ResetResult();
            if (child != null) child.ResetResult();
        }

        public override void Draw(int x, int y, bool selected)
        {
        }

        public override Point GetSize()
        {
            return new Point(SquareGridSelector.CellWidth, SquareGridSelector.CellHeight);
        }
    }

    internal sealed class SquareGridSelector : MenuSelector
    {
        internal const int Columns = 3;
        internal const int CellWidth = 142;
        internal const int CellHeight = 76;
        private const int NativeTextInset = 16;
        private const int SelectionShift = 5;
        internal const int TextWidth = CellWidth - NativeTextInset - SelectionShift;
        internal const int LineStep = 11;
        private const int HorizontalGap = 4;
        private const int VerticalGap = 8;
        private const int ContentHeight = 300;
        private readonly List<GridMenuItem> allItems;
        private readonly List<IMenuItem> visibleItems = new List<IMenuItem>();
        private Action[] hoverActions;
        private readonly Action confirmAction;
        private readonly Action<int> scrollAction;
        private string[] pageLabels;
        private CompactGridLayout layout;
        private SpriteFont layoutFont;
        private GuiFrame frame;
        private Rectangle frameBounds;
        private readonly WorkshopGridExitGate exitGate =
            new WorkshopGridExitGate();
        private readonly CompactGridFocusState focus =
            new CompactGridFocusState();

        internal SquareGridSelector(IEnumerable<GridMenuItem> items)
            : base(new GuiFormat())
        {
            allItems = new List<GridMenuItem>(items);
            confirmAction = () => UiPointer.Queue(this, UiAction.Confirm);
            scrollAction = delta => SelectItem(Math.Max(0, Math.Min(m_menu_items.Length - 1, Index - delta * Columns)));
            foreach (GridMenuItem item in allItems) AddChild(item);
            RefreshVisibleItems();
        }

        internal int VisibleItemCount { get { return m_menu_items.Length; } }

        protected override BTresult MyRun(TickData data)
        {
            RefreshVisibleItems();
            if (_return_result != BTresult.NULL) return _return_result;
            if (m_menu_items.Length == 0) return BTresult.Failure;
            PadInstance main = ControllerManager.instance.GetMain();
            bool anyButtonPressed = main.GetPad().GetPressedButtons().Length > 0;
            if (!focus.CanReadGridInput(anyButtonPressed))
            {
                UiInputRouter.Consume();
                return BTresult.Running;
            }

            if (focus.ChildActive)
            {
                RunActiveChild(data);
                return BTresult.Running;
            }

            PadState input = ControllerManager.instance.MenuController.GetPadState();
            UiAction action = UiPointer.Read(this, UiInputRouter.Read(input, false)).Action;
            input = UiInputRouter.ToPadState(UiInputRouter.FromAction(action));
            bool backHeld = input.cancel || input.pause;
            WorkshopGridExitResult exitResult = exitGate.Poll(backHeld);
            if (exitResult == WorkshopGridExitResult.Waiting)
            {
                UiInputRouter.Consume();
                return BTresult.Running;
            }
            if (exitResult == WorkshopGridExitResult.Exit)
            {
                UiInputRouter.Consume();
                return BTresult.Failure;
            }
            if (action != UiAction.None) UiInputRouter.Consume();
            if (action == UiAction.Cancel)
            {
                UiSounds.Play(UiSound.Back);
                exitGate.RequestExit();
                return BTresult.Running;
            }

            int next = Index;
            if (action == UiAction.Left)
                next = GridNavigation.MoveHorizontal(next, m_menu_items.Length, -1, Columns);
            else if (action == UiAction.Right)
                next = GridNavigation.MoveHorizontal(next, m_menu_items.Length, 1, Columns);
            else if (action == UiAction.Up)
                next = GridNavigation.MoveVertical(next, m_menu_items.Length, -1, Columns);
            else if (action == UiAction.Down)
                next = GridNavigation.MoveVertical(next, m_menu_items.Length, 1, Columns);
            if (next != Index)
            {
                SelectItem(next);
            }
            else if (action == UiAction.Confirm)
            {
                focus.OpenChild();
                if (RunActiveChild(data) != BTresult.Failure) UiSounds.Play(UiSound.Confirm);
            }
            return BTresult.Running;
        }

        protected override void OnNewRun()
        {
            ResetGrid();
        }

        protected override void ResumeRun()
        {
            ResetGrid();
        }

        public override void Draw()
        {
            if (last_result != BTresult.Running || m_menu_items.Length == 0) return;
            EnsureLayout();
            int page = layout.PageOf[Index];
            CompactGridLayout.Page current = layout.Pages[page];
            int columns = Math.Min(Columns, m_menu_items.Length);
            int width = 28 + columns * CellWidth + (columns - 1) * HorizontalGap;
            int height = 24 + current.Height + (layout.Pages.Length > 1 ? 14 : 0);
            frameBounds = new Rectangle((480 - width) / 2, (360 - height) / 2, width, height);
            m_bounds = frameBounds; frame.SetBounds(frameBounds);
            UiPointer.BeginSurface(this, !focus.ChildActive);
            frame.Draw();
            int first = current.First;
            int count = current.Last - first;
            for (int visible = 0; visible < count; visible++)
            {
                int itemIndex = first + visible;
                int column = visible % Columns;
                Rectangle cell = new Rectangle(
                    frameBounds.X + 14 + column * (CellWidth + HorizontalGap),
                    frameBounds.Y + 12 + layout.Tops[itemIndex],
                    CellWidth,
                    layout.Heights[itemIndex]);
                bool selected = itemIndex == Index;
                UiPointer.Region(cell, hoverActions[itemIndex], confirmAction);
                GridMenuItem item = (GridMenuItem)m_menu_items[itemIndex];
                DrawLabel(item, cell, selected);
                if (visible >= Columns)
                    Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,
                        new Rectangle(cell.X + 6, cell.Y - VerticalGap / 2, CellWidth - 12, 1), UiTheme.Border);
                if (column < Columns - 1 && itemIndex + 1 < current.Last)
                {
                    var neighbour = (GridMenuItem)m_menu_items[itemIndex + 1];
                    int separatorHeight = Math.Max(item.ContentHeight, neighbour.ContentHeight) - 6;
                    Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,
                        new Rectangle(cell.Right + HorizontalGap / 2, cell.Y + 3, 1, separatorHeight), UiTheme.Border);
                }
            }
            UiPointer.ScrollRegion(frameBounds, scrollAction);
            if (layout.Pages.Length > 1)
            {
                string pageText = pageLabels[page];
                SpriteFont font = Game1.instance.contentManager.font.LocationFont;
                float pageWidth = font.MeasureString(pageText).X;
                UiTheme.DrawText(
                    font,
                    pageText,
                    new Vector2(frameBounds.Right - pageWidth - 12, frameBounds.Bottom - 17),
                    UiTheme.Muted);
            }
        }

        private void EnsureLayout()
        {
            SpriteFont font = Game1.instance.contentManager.font.MenuFontSmall;
            if (layout != null && ReferenceEquals(layoutFont, font)) return;
            var heights = new int[m_menu_items.Length];
            for (int i = 0; i < heights.Length; i++)
            {
                var item = (GridMenuItem)m_menu_items[i]; PrepareLabel(item, font);
                heights[i] = 3 + Math.Max(0, item.TitleLines.Length - 1) * LineStep + font.LineSpacing;
                if (item.SubtitleLines.Length > 0) heights[i] += 3 + item.SubtitleLines.Length * LineStep;
                if (item.Icon != null) heights[i] = Math.Max(heights[i], item.Icon.source.Height + 6);
                item.ContentHeight = heights[i];
            }
            layout = new CompactGridLayout(heights, Columns, ContentHeight, VerticalGap);
            layoutFont = font;
            pageLabels = new string[layout.Pages.Length];
            for (int i = 0; i < pageLabels.Length; i++) pageLabels[i] = (i + 1) + "/" + pageLabels.Length;
        }

        private static void PrepareLabel(GridMenuItem item, SpriteFont font)
        {
            if (!ReferenceEquals(item.LayoutFont, font))
            {
                item.TitleLines = SplitLabel(item.Label, TextWidth, font);
                LimitLines(ref item.TitleLines, item.Subtitle.Length == 0 && item.Icon == null ? 6 : 4, TextWidth);
                int subtitleWidth = TextWidth - (item.Icon == null ? 0 : item.Icon.source.Width + 3);
                item.SubtitleLines = item.Subtitle.Length == 0 ? new string[0] : SplitLabel(item.Subtitle, subtitleWidth, font);
                LimitLines(ref item.SubtitleLines, 2, subtitleWidth);
                item.LayoutFont = font;
            }
        }

        private static void DrawLabel(GridMenuItem item, Rectangle cell, bool selected)
        {
            SpriteFont font = Game1.instance.contentManager.font.MenuFontSmall;
            int x = cell.X + NativeTextInset + (selected ? SelectionShift : 0), y = cell.Y + 3;
            for (int i = 0; i < item.TitleLines.Length; i++)
            {
                UiTheme.DrawText(font, item.TitleLines[i], new Vector2(x, y), UiTheme.Text); y += LineStep;
            }
            y += 3;
            for (int i = 0; i < item.SubtitleLines.Length; i++)
            { UiTheme.DrawText(font, item.SubtitleLines[i], new Vector2(x, y), UiTheme.Muted); y += LineStep; }
            if (item.ShowIcon)
                Game1.spriteBatch.Draw(item.Icon.texture, new Vector2(cell.Right - item.Icon.source.Width, cell.Bottom - item.Icon.source.Height - 3), item.Icon.source, item.IconColor);
            if (selected)
            {
                var cursor = Game1.instance.contentManager.gui.Cursor;
                Game1.spriteBatch.Draw(cursor, new Vector2(cell.X, cell.Y + 3), Color.White);
            }
        }

        internal static string[] SplitLabel(string label, int width, SpriteFont font)
        { return UiTheme.WrapLines(string.IsNullOrWhiteSpace(label) ? "Mod" : label, width, value => (int)Math.Ceiling(font.MeasureString(value).X)); }
        private void SelectItem(int next)
        {
            int previous = Index;
            Index = next;
            if (Index != previous) UiSounds.Play(UiSound.Move);
        }
        private static void LimitLines(ref string[] lines, int capacity, int width)
        {
            if (lines.Length <= capacity) return;
            Array.Resize(ref lines, capacity);
            lines[capacity - 1] = UiTheme.FitText(lines[capacity - 1] + "...", width, true);
        }
        private void ResetGrid()
        {
            VanillaMenuAdapter.ResetRunningRows(m_menu_items);
            _return_result = BTresult.NULL;
            Index = 0;
            m_last_child_result = BTresult.NULL;
            exitGate.Reset();
            focus.Reset();
            UiInputRouter.Consume();
        }

        private BTresult RunActiveChild(TickData data)
        {
            m_last_child_result = (m_menu_items[Index] as IBTnode).Run(data);
            BTresult result = m_last_child_result;
            if (result == BTresult.Running) return result;
            focus.CloseChild();
            m_last_child_result = BTresult.NULL;
            exitGate.ChildClosed();
            UiInputRouter.Consume();
            return result;
        }

        private void RefreshVisibleItems()
        {
            List<IMenuItem> visible = visibleItems;
            visible.Clear();
            foreach (GridMenuItem item in allItems)
                if (item.IsVisible) visible.Add(item);

            bool unchanged = m_menu_items != null
                && m_menu_items.Length == visible.Count;
            if (unchanged)
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    if (!object.ReferenceEquals(m_menu_items[i], visible[i]))
                    {
                        unchanged = false;
                        break;
                    }
                }
            }
            if (unchanged) return;

            IMenuItem selected = m_menu_items != null
                && Index >= 0
                && Index < m_menu_items.Length
                    ? m_menu_items[Index]
                    : null;
            m_menu_items = visible.ToArray();
            // Rebuild callbacks only with the visible sequence, so paging and
            // filtering retain correct indices without per-frame closures.
            hoverActions = new Action[m_menu_items.Length];
            for (int i = 0; i < hoverActions.Length; i++)
            {
                int itemIndex = i;
                hoverActions[i] = () => SelectItem(itemIndex);
            }
            layout = null;
            if (m_menu_items.Length > 0)
            {
                int selectedIndex = selected == null
                    ? -1
                    : visible.IndexOf(selected);
                Index = selectedIndex >= 0
                    ? selectedIndex
                    : Math.Max(0, Math.Min(Index, m_menu_items.Length - 1));
            }
            if (frame == null) frame = new GuiFrame(Rectangle.Empty);
        }
    }

    internal sealed class CompactGridFocusState
    {
        private bool entering;

        internal bool ChildActive { get; private set; }

        internal CompactGridFocusState()
        {
            Reset();
        }

        internal bool CanReadGridInput(bool anyButtonPressed)
        {
            if (!entering) return true;
            if (anyButtonPressed) return false;
            entering = false;
            return false;
        }

        internal void OpenChild()
        {
            ChildActive = true;
        }

        internal void CloseChild()
        {
            ChildActive = false;
        }

        internal void Reset()
        {
            entering = true;
            ChildActive = false;
        }
    }

    internal sealed class CompactGridDrawableHost : IBTnode, JumpKing.Util.IDrawable
    {
        private readonly SquareGridSelector grid;
        private readonly IList<JumpKing.Util.IDrawable> pages;

        internal CompactGridDrawableHost(
            SquareGridSelector parent,
            IList<JumpKing.Util.IDrawable> childPages)
        {
            grid = parent;
            pages = childPages ?? new JumpKing.Util.IDrawable[0];
        }

        internal SquareGridSelector Grid { get { return grid; } }
        internal IList<JumpKing.Util.IDrawable> Pages { get { return pages; } }

        protected override BTresult MyRun(TickData data)
        {
            return grid.Run(data);
        }

        public void Draw()
        {
            grid.Draw();
            foreach (JumpKing.Util.IDrawable page in pages) page.Draw();
        }
    }

    internal enum WorkshopGridExitResult
    {
        None,
        Waiting,
        Exit
    }

    internal sealed class WorkshopGridExitGate
    {
        private bool waitingForRelease;
        private bool exitAfterRelease;

        internal void RequestExit()
        {
            waitingForRelease = true;
            exitAfterRelease = true;
        }

        internal void ChildClosed()
        {
            waitingForRelease = true;
            exitAfterRelease = false;
        }

        internal WorkshopGridExitResult Poll(bool backHeld)
        {
            if (!waitingForRelease) return WorkshopGridExitResult.None;
            if (backHeld) return WorkshopGridExitResult.Waiting;
            waitingForRelease = false;
            if (!exitAfterRelease) return WorkshopGridExitResult.None;
            exitAfterRelease = false;
            return WorkshopGridExitResult.Exit;
        }

        internal void Reset()
        {
            waitingForRelease = false;
            exitAfterRelease = false;
        }
    }

    internal static class GridNavigation
    {
        internal static int MoveHorizontal(int index, int count, int direction, int columns)
        {
            if (count <= 1) return 0;
            int rowStart = index - index % columns;
            int rowCount = Math.Min(columns, count - rowStart);
            int column = index - rowStart;
            column = (column + direction + rowCount) % rowCount;
            return rowStart + column;
        }

        internal static int MoveVertical(int index, int count, int direction, int columns)
        {
            if (count <= 1) return 0;
            int rows = (count + columns - 1) / columns;
            int column = index % columns;
            int row = index / columns;
            for (int step = 0; step < rows; step++)
            {
                row = (row + direction + rows) % rows;
                int candidate = row * columns + column;
                if (candidate < count) return candidate;
            }
            return index;
        }
    }
}
