using System;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using JKRuntime.UI;

namespace Replays
{
    internal sealed class ReplayViewerPage : IUiPage, IReplayViewer
    {
        private readonly ReplayData replay;
        private readonly ReplayTimeline timeline;
        private readonly ReplaySession session;
        private bool hudVisible = true;
        private bool closed;
        private long presentedSeek = -1;
        private static readonly Rectangle ControlsBounds = new Rectangle(8, 268, 464, 88);

        internal ReplayViewerPage(ReplayData value)
        {
            if (value == null) throw new ArgumentNullException("value");
            replay = value;
            timeline = new ReplayTimeline(replay.Frames.Count,
                Game1.instance.TargetElapsedTime.Ticks / (float)replay.Header.TickDuration);
            session = new ReplaySession(replay);
        }

        public bool IsOpen { get; private set; }
        public bool WantsClose { get { return false; } }

        public void Abort()
        {
            if (closed) return;
            closed = true;
            IsOpen = false;
            session.Dispose();
        }

        public void OnOpen()
        {
            IsOpen = true;
            session.Begin();
            PresentFrame();
            UpdateCamera();
        }

        public void OnClose()
        {
            if (closed) return;
            closed = true;
            IsOpen = false;
            session.Dispose();
            ReplayRuntime.ViewerClosed(this);
        }

        public void Update(UiInput input, float delta)
        {
            if (input.Secondary)
                hudVisible = !hudVisible;
            else if (input.Left)
                timeline.Seek(ReplayTiming.SeekFrames(replay.Header, -1));
            else if (input.Right)
                timeline.Seek(ReplayTiming.SeekFrames(replay.Header, 1));
            else if (input.Up)
                timeline.Seek(ReplayTiming.SeekFrames(replay.Header, -10));
            else if (input.Down)
                timeline.Seek(ReplayTiming.SeekFrames(replay.Header, 10));
            else if (input.Confirm)
                timeline.Toggle();
            timeline.Update(delta);
            PresentFrame();
            UpdateCamera();
        }

        public void Draw()
        {
            UiPointer.BeginSurface(this);
            UiPointer.ScrollRegion(new Rectangle(0, 0, 480, 360), delta => SeekAndPresent(ReplayTiming.SeekFrames(replay.Header, -delta)));
            if (replay.Frames.Count == 0) return;
            UpdateCamera();
            if (!hudVisible)
            {
                UiPointer.Region(new Rectangle(0, 0, 480, 360), null, () => hudVisible = true);
                return;
            }
            DrawHeader();
            DrawControls();
        }

        private void DrawHeader()
        {
            UiTheme.Panel(
                new Rectangle(12, 10, 456, replay.Header.CompletionTime.HasValue ? 60 : 42),
                new Color(7, 9, 11, 225),
                UiTheme.Border);
            UiTheme.TextLine(
                UiTheme.FitText(
                    replay.Header.WorldName.ToUpperInvariant(),
                    285,
                    true),
                new Vector2(23, 20),
                UiTheme.Text,
                true);
            string state = timeline.Playing ? "PLAYING" : "PAUSED";
            if (replay.Header.CompletionTime.HasValue)
                UiTheme.TextLine("COMPLETION IGT  " + ReplayTiming.Format(TimeSpan.FromTicks(replay.Header.CompletionTime.Value), true),
                    new Vector2(23, 43), UiTheme.Muted, true);
            UiTheme.TextLine(
                state,
                new Vector2(376, 20),
                timeline.Playing ? UiTheme.Cyan : UiTheme.Gold,
                true);
        }

        private void DrawControls()
        {
            UiTheme.Panel(
                ControlsBounds,
                new Color(7, 9, 11, 225),
                UiTheme.Border);
            DrawProgress();
            UiTheme.CommandBar(
                UiTheme.FooterRow(ControlsBounds, 1),
                new UiCommand(UiInputHints.Key(UiAction.Left) + "/" + UiInputHints.Key(UiAction.Right), "1 sec"),
                new UiCommand(UiInputHints.Key(UiAction.Up) + "/" + UiInputHints.Key(UiAction.Down), "10 sec"));
            UiTheme.CommandBar(UiTheme.FooterRow(ControlsBounds),
                UiInputHints.Command(UiAction.Confirm, timeline.Playing ? "Pause" : "Play"),
                UiInputHints.Command(UiAction.Secondary, "HUD"),
                UiInputHints.Command(UiAction.Cancel, "Close"));
        }

        private void UpdateCamera()
        {
            if (replay.Frames.Count == 0) return;
            ReplayFrame frame = replay.Frames[timeline.Index];
            // preserve the recorded screen's native hysteresis on falls and at jump apexes
            Camera.UpdateCamera(new Point(240, (1 - frame.Screen) * 360 + 180));
        }

        private void PresentFrame()
        {
            if (replay.Frames.Count == 0) return;
            session.Present(timeline.Index, timeline.Playing, presentedSeek != timeline.SeekRevision);
            presentedSeek = timeline.SeekRevision;
        }

        private void SeekAndPresent(int frames)
        {
            timeline.Seek(frames);
            PresentFrame();
            UpdateCamera();
        }

        private void DrawProgress()
        {
            int total = Math.Max(1, replay.Frames.Count - 1);
            float ratio = timeline.Index / (float)total;
            Rectangle track = new Rectangle(19, 282, 338, 5);
            UiPointer.Region(new Rectangle(track.X, track.Y - 5, track.Width, 15), null,
                () => SeekAndPresent((int)((UiPointer.Position.X - track.X) * (long)total / (track.Width - 1)) - timeline.Index));
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Game1.spriteBatch.Draw(pixel, track, UiTheme.Border);
            Game1.spriteBatch.Draw(
                pixel,
                new Rectangle(
                    track.X,
                    track.Y,
                    Math.Max(1, (int)(track.Width * ratio)),
                    track.Height),
                UiTheme.Cyan);
            UiTheme.TextLine(
                FormatFrames(timeline.Index)
                    + " / "
                    + FormatFrames(replay.Frames.Count),
                new Vector2(369, 276),
                UiTheme.Text,
                true);
        }

        private string FormatFrames(int frames)
        {
            return ReplayTiming.Format(ReplayTiming.Duration(replay.Header, frames));
        }
    }
}
