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
        private readonly ReplayCosmetics cosmetics;
        private ReplayCameraTarget camera;
        private ReplayAudioPlayback audio;
        private ReplayWorldPlayback world;

        internal ReplaySession(ReplayData value)
        {
            if (value == null) throw new ArgumentNullException("value");
            replay = value;
            cosmetics = new ReplayCosmetics(value, false);
            presentation.Cosmetics = cosmetics;
        }

        internal void Begin()
        {
            if (active) return;
            scope = new JKRuntime.RuntimeScope();
            try
            {
                camera = new ReplayCameraTarget(replay, scope);
                var cosmeticScope=ReplayCosmetics.PlaybackScope();if(cosmeticScope!=null)scope.Own(cosmeticScope);
                var snapshot = JKRuntime.State.GameState.Snapshots.Capture();
                scope.Defer(delegate { JKRuntime.State.GameState.Snapshots.Restore(snapshot); });
                clock = scope.Own(JKRuntime.State.GameClock.Override(replay.Header.InitialGameTicks, replay.Header.InitialGameTime));
                scope.Own(victory);
                victory.Begin();
                scope.Own(presentation);
                presentation.Prepare();
                audio = scope.Own(new ReplayAudioPlayback(replay, new NativeReplayAudio()));
                world=scope.Own(new ReplayWorldPlayback(replay));
                world.Begin();
                active = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void Present(int frameIndex, bool playing = true, bool cut = false)
        {
            if (!active || replay.Frames.Count == 0) return;
            int index = Math.Max(0, Math.Min(replay.Frames.Count - 1, frameIndex));
            clock.SetFrame(index);
            world.Present(index);
            victory.Maintain();
            cosmetics.Present(index, playing, cut);
            audio.Present(index, playing, cut);
            presentation.Present(
                replay.Frames[index],
                ReplayAppearanceTrack.AtFrame(replay, index));
            camera.Present(index, playing, cut);
        }

        public void Dispose()
        {
            if (scope != null) { scope.Dispose(); scope = null; }
            clock = null;
            camera = null;
            audio = null;
            world = null;
            active = false;
            cosmetics.Dispose();
        }
    }
}
