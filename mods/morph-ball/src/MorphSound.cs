using System;
using System.IO;
using System.Reflection;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace MorphBallMod
{
    internal sealed class MorphSound : IDisposable
    {
        private const string TransformResource =
            "MorphBallMod.ball-transform-8bit.wav";
        private const string UntransformResource =
            "MorphBallMod.ball-untransform-8bit.wav";

        private readonly JKSound transform;
        private readonly JKSound untransform;
        private bool disposed;
        private readonly JKRuntime.RuntimeScope resources = new JKRuntime.RuntimeScope();

        internal MorphSound()
        {
            transform = resources.Own(Load(TransformResource));
            try { untransform = resources.Own(Load(UntransformResource)); }
            catch { resources.Dispose(); throw; }
        }
        internal void Stop() { if (!disposed) { transform.Stop(); untransform.Stop(); } }

        internal void Play(bool toBall)
        {
            if (disposed)
            {
                return;
            }
            transform.Stop();
            untransform.Stop();
            (toBall ? transform : untransform).Play();
        }

        public void Dispose() { disposed = true; resources.Dispose(); }

        private static JKSound Load(string resourceName)
        {
            Assembly assembly = typeof(MorphSound).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(
                resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Ball King sound resource is unavailable");
                }
                var effect = SoundEffect.FromStream(stream);
                try { return new JKSound(effect, SoundType.SFX); } catch { effect.Dispose(); throw; }
            }
        }
    }
}
