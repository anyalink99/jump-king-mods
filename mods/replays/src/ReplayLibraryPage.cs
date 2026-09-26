using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using JKRuntime.UI;

namespace Replays
{
    internal sealed class ReplayLibraryPage : IUiPage, IUiPageInputPolicy
    {
        private enum PageMode
        {
            Library,
            Details,
            DeleteConfirmation
        }

        private const int VisibleRows = 8;
        private readonly Action continueFromMainMenu;
        private readonly UiFrame frame =
            new UiFrame(new Rectangle(31, 19, 418, 322));
        private IList<ReplaySummary> replays = new List<ReplaySummary>();
        private PageMode mode;
        private ReplaySummary selected;
        private int index;
        private readonly UiListViewport viewport = new UiListViewport();
        private int actionIndex;
        private bool close;
        private string status;
        private string watchId;
        private int observedRevision;

        internal ReplayLibraryPage()
            : this(null)
        {
        }

        internal ReplayLibraryPage(Action continueGame)
        {
            continueFromMainMenu = continueGame;
        }

        public bool WantsClose { get { return close; } }
        public bool HandlesCancel { get { return true; } }

        public void OnOpen()
        {
            close = false;
            mode = PageMode.Library;
            selected = null;
            index = 0;
            viewport.Reset();
            actionIndex = 0;
            status = string.Empty;
            watchId = null;
            ReplayRuntime.Repository.Refresh();
            Refresh();
        }

        public void OnClose()
        {
            ReplayRuntime.ApplyRecordingSetting();
            if (!string.IsNullOrWhiteSpace(watchId))
            {
                bool accepted = ReplayRuntime.RequestWatch(watchId);
                if (accepted && continueFromMainMenu != null)
                    continueFromMainMenu();
            }
        }

        public void Update(UiInput input, float delta)
        {
            if (mode == PageMode.Library && observedRevision != ReplayRuntime.Repository.Revision) Refresh();
            if (mode == PageMode.Library)
                UpdateLibrary(input);
            else if (mode == PageMode.Details)
                UpdateDetails(input);
            else UpdateDeleteConfirmation(input);
        }

        public void Draw()
        {
            UiPointer.BeginSurface(this);
            frame.Draw();
            UiTheme.TextLine(
                "REPLAYS",
                new Vector2(49, 35),
                UiTheme.Text,
                false);
            if (mode == PageMode.Library) DrawLibrary();
            else DrawDetails();
        }

        private void UpdateLibrary(UiInput input)
        {
            if (input.Action == UiAction.Secondary)
            {
                SelectSound();
                ReplayRuntime.Repository.RetryFailed();
                ReplayRuntime.Repository.Refresh(); Refresh();
                return;
            }
            int count = replays.Count;
            if (input.Up && count > 0)
            {
                SelectIndex(ref index, (index + count - 1) % count);
                viewport.FollowSelection(index, count, VisibleRows);
            }
            else if (input.Down && count > 0)
            {
                SelectIndex(ref index, (index + 1) % count);
                viewport.FollowSelection(index, count, VisibleRows);
            }
            else if (input.Confirm && count > 0)
            {
                selected = replays[index];
                actionIndex = 0;
                mode = PageMode.Details;
                status = string.Empty;
                SelectSound();
            }
            else if (input.Cancel)
            {
                UiSounds.Play(UiSound.Back);
                close = true;
            }
        }

        private void UpdateDetails(UiInput input)
        {
            if (input.Up)
            {
                actionIndex = (actionIndex + 2) % 3;
                MoveSound();
            }
            else if (input.Down)
            {
                actionIndex = (actionIndex + 1) % 3;
                MoveSound();
            }
            else if (input.Cancel)
            {
                UiSounds.Play(UiSound.Back);
                mode = PageMode.Library;
                status = string.Empty;
            }
            else if (input.Confirm)
            {
                if (actionIndex == 0) WatchSelected();
                else if (actionIndex == 1) ToggleGhost();
                else
                {
                    SelectSound();
                    mode = PageMode.DeleteConfirmation;
                    status = "DELETE THIS REPLAY?";
                }
            }
        }

        private void UpdateDeleteConfirmation(UiInput input)
        {
            if (input.Cancel)
            {
                UiSounds.Play(UiSound.Back);
                mode = PageMode.Details;
                status = string.Empty;
                return;
            }
            if (!input.Confirm || selected == null) return;
            string id = selected.Header.Id;
            if (ReplayRuntime.Repository.Delete(id))
            {
                ReplayRuntime.Deleted(id);
                SelectSound();
            }
            Refresh();
            mode = PageMode.Library;
            selected = null;
            index = Math.Min(index, Math.Max(0, replays.Count - 1));
            status = "REPLAY DELETED";
        }

        private void WatchSelected()
        {
            if (selected == null) return;
            if (!ReplayRuntime.CanWatch(selected.Header))
            {
                status = "LOAD THIS REPLAY'S WORLD FIRST";
                UiSounds.Play(UiSound.Error);
                return;
            }
            watchId = selected.Header.Id;
            SelectSound();
            close = true;
            status = "OPENING REPLAY";
        }

        private void ToggleGhost()
        {
            if (selected == null) return;
            string current = ReplaySettingsStore.Current.GhostReplayId;
            ReplaySettingsStore.SetGhost(string.Equals(
                current,
                selected.Header.Id,
                StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : selected.Header.Id);
            ReplayRuntime.RefreshGhost();
            SelectSound();
            status = string.IsNullOrWhiteSpace(
                ReplaySettingsStore.Current.GhostReplayId)
                    ? "GHOST DISABLED"
                    : "GHOST SELECTED";
        }

        private void DrawLibrary()
        {
            UiPointer.ScrollRegion(new Rectangle(48, 71, 384, 225), delta => SelectIndex(ref index, viewport.Scroll(-delta, replays.Count, VisibleRows, index)));
            if (replays.Count == 0)
            {
                UiTheme.TextLine(
                    "NO REPLAYS RECORDED",
                    new Vector2(67, 147),
                    UiTheme.Muted,
                    true);
            }
            else
            {
                int first = viewport.FirstVisible(replays.Count, VisibleRows, index);
                int last = Math.Min(replays.Count, first + VisibleRows);
                int y = 75;
                for (int replayIndex = first;
                    replayIndex < last;
                    replayIndex++)
                {
                    ReplaySummary replay = replays[replayIndex];
                    DrawRow(
                        replayIndex,
                        replay.Header.WorldName,
                        FormatDuration(replay.Duration),
                        IsGhost(replay) ? UiTheme.Cyan : UiTheme.Text,
                        index == replayIndex,
                        y);
                    y += 28;
                }
            }

            DrawStatus();
            UiTheme.CommandBar(
                UiTheme.FooterRow(frame.Bounds),
                UiInputHints.Command(UiAction.Confirm, "Select"),
                UiInputHints.Command(UiAction.Secondary, "Refresh / retry"),
                UiInputHints.Command(UiAction.Cancel, "Back"));
        }

        private void DrawDetails()
        {
            if (selected == null) return;
            UiTheme.TextLine(
                UiTheme.FitText(
                    selected.Header.WorldName.ToUpperInvariant(),
                    340,
                    true),
                new Vector2(51, 74),
                UiTheme.Gold,
                true);
            UiTheme.TextLine(
                UiTheme.FitText(selected.Header.WorldAuthor, 340, true),
                new Vector2(51, 94),
                UiTheme.Muted,
                true);
            UiTheme.TextLine(
                selected.Header.CreatedUtc.ToLocalTime()
                    .ToString("yyyy-MM-dd  HH:mm"),
                new Vector2(51, 121),
                UiTheme.Text,
                true);
            UiTheme.TextLine(
                FormatDuration(selected.Duration)
                    + "   "
                    + selected.Header.FrameCount
                    + " FRAMES",
                new Vector2(51, 141),
                UiTheme.Text,
                true);

            DrawAction(0, "WATCH", 181);
            DrawAction(
                1,
                IsGhost(selected) ? "DISABLE GHOST" : "USE AS GHOST",
                215);
            DrawAction(2, "DELETE", 249);
            DrawStatus();
            UiTheme.CommandBar(
                UiTheme.FooterRow(frame.Bounds),
                UiInputHints.Command(
                    UiAction.Confirm,
                    mode == PageMode.DeleteConfirmation
                        ? "CONFIRM DELETE"
                        : "SELECT"),
                UiInputHints.Command(UiAction.Cancel, "Back"));
        }

        private void DrawAction(int action, string label, int y)
        {
            Rectangle row = new Rectangle(48, y - 4, 384, 28);
            if (mode != PageMode.DeleteConfirmation)
                UiPointer.ActionRegion(row, UiAction.Confirm, () => SelectIndex(ref actionIndex, action));
            bool active = action == actionIndex;
            if (active)
                UiTheme.Panel(
                    row,
                    new Color(29, 36, 38),
                    mode == PageMode.DeleteConfirmation
                        ? UiTheme.Red
                        : UiTheme.Gold);
            UiTheme.TextLine(
                label,
                new Vector2(61, y + 2),
                action == 2 ? UiTheme.Red : active ? UiTheme.Gold : UiTheme.Text,
                true);
        }

        private void DrawRow(
            int rowIndex,
            string label,
            string value,
            Color color,
            bool selectedRow,
            int y)
        {
            Rectangle row = new Rectangle(48, y - 4, 384, 25);
            UiPointer.ActionRegion(row, UiAction.Confirm, () => SelectIndex(ref index, rowIndex));
            if (selectedRow)
                UiTheme.Panel(
                    row,
                    new Color(29, 36, 38),
                    UiTheme.Gold);
            UiTheme.TextLine(
                UiTheme.FitText(label.ToUpperInvariant(), 272, true),
                new Vector2(60, y + 1),
                selectedRow ? UiTheme.Gold : color,
                true);
            UiTheme.TextLine(
                value,
                new Vector2(356, y + 1),
                color,
                true);
        }

        private void DrawStatus()
        {
            string message = status;
            if (string.IsNullOrWhiteSpace(message)) message = ReplayRuntime.RecordingStatus;
            if (string.IsNullOrWhiteSpace(message)) message = ReplayRuntime.Repository.SaveStatus;
            if (string.IsNullOrWhiteSpace(message)) return;
            UiTheme.TextLine(
                UiTheme.FitText(message, 370, true),
                new Vector2(54, 284),
                mode == PageMode.DeleteConfirmation
                    ? UiTheme.Red
                    : UiTheme.Cyan,
                true);
        }

        private void Refresh()
        {
            ReplaySettingsStore.EnsureLoaded();
            replays = ReplayRuntime.Repository.List(out observedRevision);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1d
                ? duration.ToString("h\\:mm\\:ss")
                : duration.ToString("m\\:ss");
        }

        private static bool IsGhost(ReplaySummary replay)
        {
            return replay != null
                && string.Equals(
                    replay.Header.Id,
                    ReplaySettingsStore.Current.GhostReplayId,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void SelectIndex(ref int current, int next)
        { if (current == next) return; current = next; MoveSound(); }

        private static void MoveSound()
        {
            UiSounds.Play(UiSound.Move);
        }

        private static void SelectSound()
        {
            UiSounds.Play(UiSound.Confirm);
        }
    }
}
