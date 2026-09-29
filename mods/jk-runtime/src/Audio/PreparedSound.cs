using System;
using System.IO;
using System.Reflection;
using JKRuntime.Gameplay;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace JKRuntime.Audio
{
    /// <summary>A reusable, single-voice sound with native volume preferences. Prepare during loading, own it in a world scope.</summary>
    public sealed class PreparedSound : IDisposable
    {
        private readonly Func<JKSound> create;
        private JKSound sound;
        private bool disposed, requested, paused, pendingStart;
        private RuntimeScope binding;
        private PreparedSound(Func<JKSound> factory) { create = factory; sound = create(); }
        public bool IsDisposed { get { return disposed; } }
        public JKSoundState State { get { return disposed ? JKSoundState.Stopped : sound.State; } }
        public float Volume
        {
            get { Check(); return sound.Volume; }
            set { Check(); if (float.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException("value"); sound.Volume = value; }
        }
        public bool IsLooped { get { Check(); return sound.IsLooped; } set { Check(); sound.IsLooped = value; } }
        public static PreparedSound FromResource(Assembly assembly, string name, SoundType type = SoundType.SFX)
        {
            RuntimeApi.Kernel.CheckThread();
            if (assembly == null) throw new ArgumentNullException("assembly");
            using (var stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Embedded sound is unavailable: " + name);
                return FromStream(stream, type);
            }
        }
        /// <summary>Copies the remaining bytes and decodes now. The caller still owns the stream.</summary>
        public static PreparedSound FromStream(Stream stream, SoundType type = SoundType.SFX)
        {
            RuntimeApi.Kernel.CheckThread();
            if (stream == null) throw new ArgumentNullException("stream");
            if (!Enum.IsDefined(typeof(SoundType), type)) throw new ArgumentOutOfRangeException("type");
            byte[] wave;
            using (var copy = new MemoryStream()) { stream.CopyTo(copy); wave = copy.ToArray(); }
            return new PreparedSound(delegate
            {
                using (var input = new MemoryStream(wave, false))
                {
                    var effect = SoundEffect.FromStream(input);
                    try { return new JKSound(effect, type); }
                    catch { effect.Dispose(); throw; }
                }
            });
        }
        private void Check() { RuntimeApi.Kernel.CheckThread(); if (disposed) throw new ObjectDisposedException("PreparedSound"); }
        /// <summary>Restart this voice. A still-flushing XAudio voice is replaced from memory; playback never waits or reads a file.</summary>
        public void Play()
        {
            Check();
            if (sound.State != JKSoundState.Stopped)
            {
                // Stop can return before XAudio has flushed. Don't ask that voice to restart.
                var next = create();
                try { next.Volume = sound.Volume; next.IsLooped = sound.IsLooped; }
                catch { next.Dispose(); throw; }
                sound.Dispose(); sound = next;
            }
            requested = true;
            pendingStart = paused;
            // Don't start and immediately pause: that can leak an attack while a menu is open.
            if (!paused) sound.Play();
        }
        public void Stop()
        {
            RuntimeApi.Kernel.CheckThread(); if (disposed) return;
            requested = pendingStart = false; sound.Stop();
        }
        public void SetPaused(bool value)
        {
            Check(); paused = value;
            if (!requested) return;
            if (!paused && pendingStart) { sound.Play(); pendingStart = false; return; }
            if (paused && sound.State == JKSoundState.Playing) sound.Pause();
            else if (!paused && sound.State == JKSoundState.Paused) sound.Resume();
            else if (!paused && sound.State == JKSoundState.Stopped) { requested = false; }
        }
        /// <summary>Opt into native pause and stop-on-restore for one attempt. Dispose the binding at attempt end; the prepared sound survives.</summary>
        public IDisposable BindToAttempt(string owner)
        {
            Check(); ModuleDefinition.ValidId(owner);
            if (binding != null) throw new InvalidOperationException("Sound already belongs to an attempt");
            var scope = new RuntimeScope(); binding = scope;
            try
            {
                scope.Defer(delegate { Stop(); paused = false; binding = null; });
                scope.Own(NativePause.Subscribe(owner, (value, timestamp) => SetPaused(value)));
                scope.Own(GameplayEvents.Subscribe(owner, value => { if (value.Kind == GameplayEventKind.RestoreStarted) Stop(); }));
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread(); if (disposed) return;
            if (binding != null) binding.Dispose();
            sound.Dispose(); disposed = true; requested = false;
        }
    }
}
