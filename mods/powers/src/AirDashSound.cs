using System;
using JKRuntime.Audio;

namespace Powers
{
    internal sealed class AirDashSound : IDisposable
    {
        internal const string Resource = "Powers.air-dash-8bit.wav";
        private readonly PreparedSound sound = PreparedSound.FromResource(typeof(AirDashSound).Assembly, Resource);
        internal void Play() { if (!sound.IsDisposed) sound.Play(); }
        internal void Stop() { sound.Stop(); }
        internal void SetPaused(bool paused) { if (!sound.IsDisposed) sound.SetPaused(paused); }
        internal IDisposable BindToAttempt() { return sound.BindToAttempt("powers"); }
        public void Dispose() { sound.Dispose(); }
    }
}
