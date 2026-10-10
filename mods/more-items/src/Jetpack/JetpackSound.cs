using System;
using System.IO;
using System.Reflection;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace JumpKingJetpack
{
    internal sealed class JetpackSound : IDisposable
    {
        private const string ResourceName =
            "JumpKingJetpack.jetpack-loop-8bit.wav";

        private readonly JKSound sound;
        private readonly JetpackSoundEnvelope envelope =
            new JetpackSoundEnvelope();
        private float volume;
        private bool disposed;

        internal JetpackSound()
        {
            Assembly assembly = typeof(JetpackSound).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(
                ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Jetpack sound resource is unavailable");
                }
                sound = new JKSound(
                    SoundEffect.FromStream(stream),
                    SoundType.SFX);
            }
            sound.IsLooped = true;
        }

        internal void SetVolume(float value)
        {
            volume = Math.Max(0f, Math.Min(1f, value));
            sound.Volume = volume * envelope.Level;
        }

        internal void Update(bool active)
        {
            if (disposed)
            {
                return;
            }

            bool wasPlaying = envelope.Playing;
            envelope.Update(active);
            sound.Volume = volume * envelope.Level;
            if (!wasPlaying && envelope.Playing)
            {
                sound.Play();
            }
            else if (wasPlaying && !envelope.Playing)
            {
                sound.Stop();
            }
        }

        internal void Stop() { if (!disposed) { sound.Stop(); envelope.Reset(); } }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            sound.Stop();
            sound.Dispose();
            disposed = true;
        }
    }
}
