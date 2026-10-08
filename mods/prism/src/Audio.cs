using System;
using System.IO;
using System.Reflection;
using JumpKing;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;
using SharpDX.XAudio2;

namespace Prism
{
    internal sealed class Audio : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(JKSound).GetField("m_instance", JKRuntime.OwnedPatches.Members);
        private static readonly FieldInfo Voice = typeof(SoundEffectInstance).GetField("_voice", JKRuntime.OwnedPatches.Members);
        private static readonly Type Manager = typeof(Game1).Assembly.GetType("JumpKing.MusicManager", true);
        private static readonly FieldInfo NativeMusic = Manager.GetField("m_music", JKRuntime.OwnedPatches.Members);
        internal static MethodInfo NativePlay { get { return Manager.GetMethod("Play", JKRuntime.OwnedPatches.Members); } }
        private JKSound music;
        private SourceVoice voice;
        private readonly double duration;
        private readonly int rate;
        private readonly byte[] wave;
        private bool disposed;
        private IJKSound previous;
        private bool restorePlaying, started, ownsMusic;
        internal Audio(string path)
        {
            Validate();
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Expected WAV music");
                reader.ReadInt32(); if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Expected WAVE");
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string id = new string(reader.ReadChars(4)); int size = reader.ReadInt32(); long next = reader.BaseStream.Position + size + (size & 1);
                    if (size < 0 || next > reader.BaseStream.Length) throw new InvalidDataException("Truncated WAV");
                    if (id == "fmt ") { if (reader.ReadInt16() != 1) throw new InvalidDataException("Expected PCM"); reader.ReadInt16(); rate = reader.ReadInt32(); }
                    reader.BaseStream.Position = next;
                }
            }
            if (rate < 8000 || rate > 192000) throw new InvalidDataException("Invalid sample rate");
            wave = File.ReadAllBytes(path);
            SoundEffect effect; using (var stream = new MemoryStream(wave, false)) effect = SoundEffect.FromStream(stream);
            try { music = new JKSound(effect, SoundType.Music); }
            catch { effect.Dispose(); throw; }
            try
            {
                duration = music.Duration.TotalSeconds; music.IsLooped = true; music.Volume = .8f;
                voice = (SourceVoice)Voice.GetValue(Instance.GetValue(music));
                if (voice == null || duration <= 0) throw new InvalidOperationException("Native sample clock is unavailable");
            }
            catch { music.Dispose(); throw; }
        }
        internal static void Validate()
        {
            if (Instance == null || Voice == null || Voice.FieldType != typeof(SourceVoice) || NativeMusic == null || NativePlay == null)
                throw new NotSupportedException("Prism: unsupported native audio contract");
        }
        internal double Time { get { return started ? (voice.State.SamplesPlayed / (double)rate) % duration : 0; } }
        internal float Volume { get { return music.Volume; } set { music.Volume = value; } }
        internal void Restart()
        {
            if (ownsMusic) throw new InvalidOperationException("Release the previous attempt before preparing its music");
            if (!started) return;
            // New native voice, prepared before player handoff. Stop/Play can reuse a
            // flushing XAudio buffer and carry the previous sample cursor into the run.
            SoundEffect effect; using (var stream = new MemoryStream(wave, false)) effect = SoundEffect.FromStream(stream);
            JKSound next;
            try { next = new JKSound(effect, SoundType.Music); } catch { effect.Dispose(); throw; }
            try { next.IsLooped = true; next.Volume = music.Volume; }
            catch { next.Dispose(); throw; }
            music.Dispose(); music = next;
            voice = (SourceVoice)Voice.GetValue(Instance.GetValue(music)); started = false;
        }
        internal void Activate(bool paused)
        {
            if (!ownsMusic)
            {
                previous = NativeMusic.GetValue(null) as IJKSound;
                restorePlaying = previous != null && previous.State == JKSoundState.Playing;
                if (restorePlaying) previous.Pause();
                ownsMusic = true;
            }
            Pause(paused);
        }
        internal void Pause(bool paused)
        {
            if (!ownsMusic) return;
            if (paused) { if (music.State == JKSoundState.Playing) music.Pause(); }
            else if (!started) { music.Play(); started = true; }
            else if (music.State == JKSoundState.Paused) music.Resume();
        }
        internal void Requested(IJKSound sound) { previous = sound; restorePlaying = true; }
        internal void Deactivate()
        {
            if (!ownsMusic) return;
            if (music.State == JKSoundState.Playing) music.Pause();
            ownsMusic = false;
            if (restorePlaying && previous != null)
            {
                NativeMusic.SetValue(null, previous);
                if (previous.State == JKSoundState.Paused) previous.Resume(); else previous.Play();
            }
            previous = null; restorePlaying = false;
        }
        public void Dispose() { if (disposed) return; Deactivate(); music.Dispose(); disposed = true; }
    }
}
