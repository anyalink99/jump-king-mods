using System;

namespace Replays
{
    internal sealed class ReplaySession : IDisposable
    {
        private readonly ReplayData replay;
        private readonly ReplayPlayerPresentation presentation =
            new ReplayPlayerPresentation();
        private readonly ReplayVictoryIsolation victory =
            new ReplayVictoryIsolation();
        private JKRuntime.State.GameClock.Lease clock;
        private bool active;
        private JKRuntime.RuntimeScope scope;

        internal ReplaySession(ReplayData value)
        {
            if (value == null) throw new ArgumentNullException("value");
            replay = value;
        }

        internal void Begin()
        {
            if (active) return;
            scope = new JKRuntime.RuntimeScope();
            try
            {
                var snapshot = JKRuntime.State.GameState.Snapshots.Capture();
                scope.Defer(delegate { JKRuntime.State.GameState.Snapshots.Restore(snapshot); });
                clock = scope.Own(JKRuntime.State.GameClock.Override(replay.Header.InitialGameTicks, replay.Header.InitialGameTime));
                scope.Own(victory);
                victory.Begin();
                scope.Own(presentation);
                presentation.Prepare();
                active = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void Present(int frameIndex)
        {
            if (!active || replay.Frames.Count == 0) return;
            int index = Math.Max(0, Math.Min(replay.Frames.Count - 1, frameIndex));
            clock.SetFrame(index);
            victory.Maintain();
            presentation.Present(
                replay.Frames[index],
                ReplayAppearanceTrack.AtFrame(replay, index));
        }

        public void Dispose()
        {
            if (scope != null) { scope.Dispose(); scope = null; }
            clock = null;
            active = false;
        }
    }
}
