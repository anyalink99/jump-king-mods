using System;
using System.IO;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace MegaGameplayExpansion
{
    internal sealed class AirDashSound : IDisposable
    {
        internal const string Resource = "MegaGameplayExpansion.air-dash-8bit.wav";
        private readonly byte[] wave;
        private JKSound sound;
        private bool disposed, playingRequested;

        internal AirDashSound()
        {
            using (var stream = typeof(AirDashSound).Assembly.GetManifestResourceStream(Resource))
            {
                if (stream == null) throw new InvalidOperationException("Air Dash sound resource is unavailable");
                using (var copy = new MemoryStream()) { stream.CopyTo(copy); wave = copy.ToArray(); }
            }
            sound = CreateSound();
        }
        private JKSound CreateSound()
        {
            using (var stream = new MemoryStream(wave, false))
                return new JKSound(SoundEffect.FromStream(stream), SoundType.SFX);
        }
        internal void Play()
        {
            if (disposed) return;
            // This MonoGame version can still report Playing while XAudio is
            // flushing Stop(), making an immediate Play() silently do nothing.
            // Replace a busy voice without waiting on the game thread.
            if (sound.State != JKSoundState.Stopped) { sound.Dispose(); sound = CreateSound(); }
            sound.Play(); playingRequested = true;
        }
        internal void Stop()
        {
            if (disposed || !playingRequested) return;
            playingRequested = false; sound.Stop();
        }
        internal void SetPaused(bool paused)
        {
            if (disposed || !playingRequested) return;
            if (paused && sound.State == JKSoundState.Playing) sound.Pause();
            else if (!paused && sound.State == JKSoundState.Paused) sound.Resume();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; sound.Stop(); sound.Dispose();
        }
    }
}
