using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    /// <summary>Recommended page base with identical modal/embedded cleanup, including partial opens.
    /// Acquire page resources with scope.Own/Defer inside OpenPage. All methods run on the game thread.</summary>
    public abstract class ScopedUiPage : IUiPage, IUiPageInputPolicy
    {
        private RuntimeScope scope;
        public bool WantsClose { get; protected set; }
        public virtual bool HandlesCancel { get { return true; } }
        public void OnOpen()
        {
            OnClose(); WantsClose = false; scope = new RuntimeScope();
            try { OpenPage(scope); }
            catch (Exception error)
            {
                try { OnClose(); }
                catch (Exception cleanup) { throw new AggregateException("Page open and cleanup failed", error, cleanup); }
                throw;
            }
        }
        public void OnClose() { UiPointer.CancelCapture(this); if (scope != null) { scope.Dispose(); scope = null; } }
        protected virtual void OpenPage(RuntimeScope resources) { }
        public abstract void Update(UiInput input, float delta);
        public abstract void Draw();
    }

    /// <summary>One command definition supplies routing, physical hints and pointer action.</summary>
    public sealed class UiPageCommand
    {
        public UiAction Action { get; private set; }
        public string Label { get; private set; }
        private readonly Action execute;
        private readonly Func<bool> enabled;
        public bool Enabled { get { return enabled(); } }
        public UiPageCommand(UiAction action, string label, Action callback, Func<bool> isEnabled = null)
        {
            if (action == UiAction.None) throw new ArgumentException("Command action is required", "action");
            if (callback == null) throw new ArgumentNullException("callback");
            Action = action; Label = label ?? ""; execute = callback; enabled = isEnabled ?? (() => true);
        }
        public bool Handle(UiInput input)
        {
            if (input.Action != Action) return false;
            if (!Enabled) return false;
            try { execute(); }
            catch { UiSounds.Play(UiSound.Error); throw; }
            UiSounds.Play(Action == UiAction.Cancel ? UiSound.Back : UiSound.Confirm); return true;
        }
        public UiCommand Hint() { return UiInputHints.Command(Action, Label); }
    }

    /// <summary>Measured safe page rectangles. Footer rows wrap instead of dropping commands.
    /// Recreate after a binding/command change so content and pointer bounds use the same layout.</summary>
    public sealed class UiPageLayout
    {
        public Rectangle Title { get; private set; }
        public Rectangle Content { get; private set; }
        public Rectangle Status { get; private set; }
        public Rectangle[] Footers { get; private set; }
        private readonly UiCommand[][] rows;
        public UiPageLayout(Rectangle frame, IEnumerable<UiPageCommand> commands, int statusLines = 1)
        {
            var hints = (commands ?? new UiPageCommand[0]).Where(c => c.Enabled).Select(c => c.Hint()).ToArray();
            var keyFont = JumpKing.Game1.instance.contentManager.font.LocationFont;
            var labelFont = JumpKing.Game1.instance.contentManager.font.MenuFontSmall;
            int[] widths = hints.Select(c => Math.Max(18, (int)Math.Ceiling(keyFont.MeasureString(UiTheme.NormalizeKey(c.Key)).X) + 10)
                + 6 + (int)Math.Ceiling(labelFont.MeasureString(c.Label.ToUpperInvariant()).X)).ToArray();
            int[][] groups = WrapCommands(frame.Width - 32, widths);
            rows = groups.Select(g => g.Select(i => hints[i]).ToArray()).ToArray();
            Initialize(frame, rows.Length, statusLines);
        }
        /// <summary>Layout-only overload for custom footer content and headless geometry checks.</summary>
        public UiPageLayout(Rectangle frame, int footerRows, int statusLines = 1)
        { rows = new UiCommand[0][]; Initialize(frame, footerRows, statusLines); }
        private void Initialize(Rectangle frame, int footerRows, int statusLines)
        {
            if (footerRows < 0 || statusLines < 0 || statusLines > 8) throw new ArgumentOutOfRangeException("footerRows/statusLines");
            Title = new Rectangle(frame.X + 16, frame.Y + 16, frame.Width - 32, 24);
            Footers = Enumerable.Range(0, footerRows).Select(i => UiTheme.FooterRow(frame, footerRows - 1 - i)).ToArray();
            int bottom = footerRows == 0 ? frame.Bottom - 16 : Footers[0].Top - 4;
            Status = new Rectangle(frame.X + 16, bottom - statusLines * 15, frame.Width - 32, statusLines * 15);
            Content = new Rectangle(frame.X + 16, Title.Bottom + 8, frame.Width - 32, Status.Top - Title.Bottom - 12);
            if (Content.Width < 32 || Content.Height < 20) throw new ArgumentException("Frame is too small for the requested page content and commands");
        }
        public void DrawCommands() { for (int i = 0; i < rows.Length; i++) UiTheme.CommandBar(Footers[i], rows[i]); }
        internal static int[][] WrapCommands(int width, int[] widths)
        {
            if (width < 32) throw new ArgumentOutOfRangeException("width");
            var rows = new List<int[]>(); var row = new List<int>(); int used = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                int next = Math.Min(width, Math.Max(18, widths[i]));
                if (row.Count > 0 && used + 16 + next > width) { rows.Add(row.ToArray()); row.Clear(); used = 0; }
                used += (row.Count == 0 ? 0 : 16) + next; row.Add(i);
            }
            if (row.Count > 0) rows.Add(row.ToArray());
            return rows.ToArray();
        }
    }

    public sealed class UiListItem
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Description { get; private set; }
        public bool Enabled { get; private set; }
        internal Action Execute;
        public UiListItem(string id, string label, Action activate = null, string description = "", bool enabled = true)
        { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable item ID is required"); Id = id; Label = label ?? id; Execute = activate; Description = description ?? ""; Enabled = enabled; }
    }

    /// <summary>Stable-key list with shared drawing/hit geometry. Hover never scrolls; navigation does.
    /// Supply fresh items on data changes, Update in the page update, and Draw inside its surface.</summary>
    public sealed class UiList
    {
        private UiListItem[] items = new UiListItem[0];
        private readonly UiListViewport viewport = new UiListViewport();
        private int selected, visibleRows = 1;
        public string SelectedId { get { return items.Length == 0 ? null : items[selected].Id; } }
        public string Description { get { return items.Length == 0 ? "" : items[selected].Description; } }
        public void SetItems(IEnumerable<UiListItem> values)
        {
            string previous = SelectedId;
            UiListItem[] next = (values ?? new UiListItem[0]).ToArray();
            if (next.Any(i => i == null) || next.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != next.Length)
                throw new ArgumentException("List items must have unique stable IDs");
            items = next; int restored = Array.FindIndex(items, i => i.Id == previous);
            selected = restored >= 0 ? restored : Math.Max(0, Math.Min(selected, items.Length - 1));
        }
        public void Select(string id)
        { int found = Array.FindIndex(items, i => i.Id == id); if (found < 0) throw new ArgumentException("Unknown list item: " + id); selected = found; viewport.FollowSelection(selected, items.Length, visibleRows); }
        public bool Update(UiInput input)
        {
            if (items.Length == 0) return false;
            if (input.Action == UiAction.Up || input.Action == UiAction.Down)
            { UiSounds.Select(ref selected, (selected + (input.Action == UiAction.Up ? items.Length - 1 : 1)) % items.Length); viewport.FollowSelection(selected, items.Length, visibleRows); return true; }
            if (input.Action == UiAction.Confirm) { Activate(SelectedId); return true; }
            return false;
        }
        private void Activate(string id)
        {
            UiListItem item = Array.Find(items, i => i.Id == id);
            if (item == null || !item.Enabled || item.Execute == null) return;
            try { item.Execute(); }
            catch { UiSounds.Play(UiSound.Error); throw; }
            UiSounds.Play(UiSound.Confirm);
        }
        public void Draw(Rectangle bounds, string emptyText = "No items", int rowHeight = 22)
        {
            if (rowHeight < 16 || bounds.Height < rowHeight || bounds.Width < 32) throw new ArgumentException("List needs at least one complete row and 32 pixels of width");
            visibleRows = Math.Max(1, bounds.Height / rowHeight);
            if (items.Length == 0) { UiTheme.TextLine(UiTheme.FitText(emptyText, bounds.Width, true), bounds.Location.ToVector2(), UiTheme.Muted, true); return; }
            int first = viewport.FirstVisible(items.Length, visibleRows, selected);
            bool scrolling = items.Length > visibleRows;
            UiPointer.ScrollRegion(bounds, rows => UiSounds.Select(ref selected, viewport.Scroll(-rows, items.Length, visibleRows, selected)));
            for (int i = first; i < Math.Min(items.Length, first + visibleRows); i++)
            {
                string id = items[i].Id; Rectangle row = new Rectangle(bounds.X, bounds.Y + (i - first) * rowHeight, bounds.Width - (scrolling ? 10 : 0), rowHeight - 2);
                if (i == selected) UiTheme.Panel(row, UiTheme.PanelFill, UiTheme.Border);
                UiTheme.TextLine(UiTheme.FitText(items[i].Label, row.Width - 12, true), new Vector2(row.X + 6, row.Y + 3), items[i].Enabled ? UiTheme.Text : UiTheme.Disabled, true);
                UiPointer.Region(row, () => { int found = Array.FindIndex(items, item => item.Id == id); if (found >= 0) UiSounds.Select(ref selected, found); }, () => Activate(id));
            }
            if (scrolling)
            {
                int height = visibleRows * rowHeight - 2;
                int thumb = Math.Max(12, height * visibleRows / items.Length);
                UiTheme.Panel(new Rectangle(bounds.Right - 4, bounds.Y, 3, height), UiTheme.PanelFill, UiTheme.Border);
                UiTheme.Panel(new Rectangle(bounds.Right - 4, bounds.Y + (height - thumb) * first / (items.Length - visibleRows), 3, thumb), UiTheme.Cyan, UiTheme.Cyan);
            }
        }
    }
}
