using System;
using JKRuntime;
using JKRuntime.Audio;

namespace MorphBallMod
{
    internal sealed class MorphSound : IDisposable
    {
        private readonly PreparedSound transform, untransform;
        private bool disposed;
        private readonly RuntimeScope resources = new RuntimeScope();
        internal MorphSound()
        {
            try
            {
                transform = resources.Own(PreparedSound.FromResource(typeof(MorphSound).Assembly, "MorphBallMod.ball-transform-8bit.wav"));
                untransform = resources.Own(PreparedSound.FromResource(typeof(MorphSound).Assembly, "MorphBallMod.ball-untransform-8bit.wav"));
            }
            catch { resources.Dispose(); throw; }
        }
        internal void Stop() { if (!disposed) { transform.Stop(); untransform.Stop(); } }
        internal void Play(bool toBall)
        {
            if (disposed) return;
            Stop(); (toBall ? transform : untransform).Play();
        }
        internal IDisposable BindToAttempt()
        {
            var scope = new RuntimeScope();
            try { scope.Own(transform.BindToAttempt("morph-ball")); scope.Own(untransform.BindToAttempt("morph-ball")); return scope; }
            catch { scope.Dispose(); throw; }
        }
        public void Dispose() { resources.Dispose(); disposed = true; }
    }
}
