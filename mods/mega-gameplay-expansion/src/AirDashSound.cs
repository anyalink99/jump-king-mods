using System;
using JKRuntime.Audio;

namespace MegaGameplayExpansion
{
    internal sealed class AirDashSound : IDisposable
    {
        internal const string Resource = "MegaGameplayExpansion.air-dash-8bit.wav";
        private readonly PreparedSound sound = PreparedSound.FromResource(typeof(AirDashSound).Assembly, Resource);
        internal void Play() { if (!sound.IsDisposed) sound.Play(); }
        internal void Stop() { sound.Stop(); }
        internal void SetPaused(bool paused) { if (!sound.IsDisposed) sound.SetPaused(paused); }
        internal IDisposable BindToAttempt() { return sound.BindToAttempt("mega-gameplay-expansion"); }
        public void Dispose() { sound.Dispose(); }
    }
}
