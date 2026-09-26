using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.Player;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework.Audio;

namespace HammerKing
{
    internal static partial class Tests
    {
        private sealed class AudioGraphicsService : Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService
        {
            public Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice { get { return null; } }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
        private static void Audio(bool native, string gameDir = null)
        {
            foreach (string name in new[] { HammerSound.WoodResource, HammerSound.StoneResource })
            using (var stream = typeof(HammerSound).Assembly.GetManifestResourceStream(name))
            using (var reader = new BinaryReader(stream))
            {
                Check(new string(reader.ReadChars(4)) == "RIFF", "Embedded WAV RIFF");
                reader.ReadInt32(); Check(new string(reader.ReadChars(4)) == "WAVE", "Embedded WAV type");
                bool format = false, data = false;
                while (stream.Position + 8 <= stream.Length)
                {
                    string chunk = new string(reader.ReadChars(4)); int size = reader.ReadInt32();
                    long end = stream.Position + size;
                    if (chunk == "fmt ")
                    {
                        Check(reader.ReadInt16() == 1 && reader.ReadInt16() == 1 && reader.ReadInt32() == 44100, "Mono native PCM format");
                        reader.ReadInt32(); reader.ReadInt16(); Check(reader.ReadInt16() == 16, "Native PCM16 container");
                        format = true;
                    }
                    if (chunk == "data")
                    {
                        Check(size / 88200f > .1f && size / 88200f < .3f, "Impact trimmed to a short one-shot");
                        int first = 0, last = 0, peak = 0, early = 0;
                        bool quantized = true;
                        for (int i = 0; i < size / 2; i++)
                        {
                            int sample = reader.ReadInt16(); if (i == 0) first = sample; last = sample;
                            peak = Math.Max(peak, Math.Abs(sample)); if (i < 882) early = Math.Max(early, Math.Abs(sample));
                            quantized &= sample % 256 == 0;
                        }
                        Check(first == 0 && last == 0, "Click-free endpoints");
                        Check(peak > 1000 && peak < 30000 && early > 1000, "Immediate audible attack with headroom");
                        Check(quantized, "Actual signed 8-bit sample quantization"); data = true;
                    }
                    stream.Position = end + (size & 1);
                }
                Check(format && data, "Complete embedded wave");
            }
            if (!native) { Console.WriteLine("[OK] Both embedded impacts: trim, attack, 8-bit quantization, headroom and clean endpoints"); return; }
            typeof(SoundEffect).GetMethod("InitializeSoundEffect", Flags).Invoke(null, null);
            Check(typeof(SoundEffect).GetProperty("MasterVoice", Flags).GetValue(null, null) != null, "Native XAudio device");
            var assembly = typeof(PlayerEntity).Assembly;
            var prefsType = assembly.GetType("JumpKing.PlayerPreferences.SoundPrefs", true);
            var managerType = assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime", true);
            var baseType = managerType.BaseType;
            var instance = baseType.GetField("instance", Flags); var previous = instance.GetValue(null);
            var prefs = Activator.CreateInstance(prefsType);
            prefsType.GetField("master", Flags).SetValue(prefs, .25f);
            prefsType.GetField("sfx_on", Flags).SetValue(prefs, false);
            var manager = FormatterServices.GetUninitializedObject(managerType);
            baseType.GetField("m_settings", Flags).SetValue(manager, prefs); instance.SetValue(null, manager);
            try
            {
                using (var audio = new HammerSound())
                {
                    var wood = (JKSound)typeof(HammerSound).GetField("wood", Flags).GetValue(audio);
                    var stone = (JKSound)typeof(HammerSound).GetField("stone", Flags).GetValue(audio);
                    Check(wood.Duration.TotalMilliseconds > 230 && wood.Duration.TotalMilliseconds < 240
                        && stone.Duration.TotalMilliseconds > 135 && stone.Duration.TotalMilliseconds < 145, "Native decoder reads both trimmed effects");
                    audio.Update(new HammerImpacts { Wood = 8, Stone = 8 });
                    foreach (var sound in new[] { wood, stone })
                    {
                        var voice = (SoundEffectInstance)typeof(JKSound).GetField("m_instance", Flags).GetValue(sound);
                        Check(sound.soundType == SoundType.SFX && voice.Volume == 0 && sound.State == JKSoundState.Playing, "Native SFX mute and simultaneous material voices");
                    }
                    audio.Stop();
                    Check(System.Threading.SpinWait.SpinUntil(() => wood.State == JKSoundState.Stopped && stone.State == JKSoundState.Stopped, 250), "Pause/restore stops both short tails");
                    prefsType.GetField("sfx_on", Flags).SetValue(prefs, true);
                    baseType.GetField("m_settings", Flags).SetValue(manager, prefs);
                    typeof(JKSound).GetMethod("OnSoundSettingsChange", Flags).Invoke(null, new[] { prefs });
                    foreach (var sound in new[] { wood, stone })
                    {
                        var voice = (SoundEffectInstance)typeof(JKSound).GetField("m_instance", Flags).GetValue(sound);
                        Check(Math.Abs(voice.Volume - .25f * .8f * .7f) < .001f, "Native master volume scales impact strength");
                    }
                    audio.Update(new HammerImpacts { Wood = 8, Stone = 8 });
                    Check(wood.State == JKSoundState.Stopped && stone.State == JKSoundState.Stopped, "Pause does not replay stale impacts");
                    for (int i = 0; i < 6; i++) audio.Update(new HammerImpacts());
                    audio.Update(new HammerImpacts { Stone = 10 });
                    Check(stone.State == JKSoundState.Playing && wood.State == JKSoundState.Stopped, "Independent material playback after separation");
                    audio.Dispose(); audio.Dispose(); audio.Stop(); audio.Update(new HammerImpacts { Wood = 10 });
                }
                SnowAudio(gameDir);
            }
            finally { instance.SetValue(null, previous); }
            Console.WriteLine("[OK] Native XAudio: both decoders, independent voices, SFX mute/master volume, contact gating, stop and disposal");
        }

        private static void SnowAudio(string gameDir)
        {
            var instance = typeof(Game1).GetField("_instance", Flags); var previous = instance.GetValue(null);
            var services = new Microsoft.Xna.Framework.GameServiceContainer();
            services.AddService(typeof(Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService), new AudioGraphicsService());
            using (var content = new Microsoft.Xna.Framework.Content.ContentManager(services, gameDir))
            {
                var pool = new OneShotSound(content.Load<SoundEffect>("Content/audio/jump_king/snow/king_land"), SoundType.SFX);
                pool.Volume = JKContentManager.Audio.Player.LAND_VOLUME;
                var voices = (List<JKSound>)typeof(OneShotSound).GetField("m_sound", Flags).GetValue(pool);
                var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
                game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
                game.contentManager.audio = new JKContentManager.Audio();
                game.contentManager.audio.player = new JKContentManager.Audio.Player();
                game.contentManager.audio.player.SnowLand = pool;
                instance.SetValue(null, game);
                try
                {
                    using (var audio = new HammerSound())
                    {
                        var stone = (JKSound)typeof(HammerSound).GetField("stone", Flags).GetValue(audio);
                        audio.Update(new HammerImpacts { Snow = 8 });
                        Check(voices.Count(s => s.State == JKSoundState.Playing) == 1 && stone.State == JKSoundState.Stopped,
                            "Snow plays the installed native SnowLand asset instead of stone");
                        for (int i = 0; i < 20; i++) audio.Update(new HammerImpacts { Snow = 8 });
                        Check(voices.Count(s => s.State == JKSoundState.Playing) == 1, "Snow contact pressure does not repeat native one-shots");
                        audio.Stop(); audio.Dispose();
                        Check(voices.Any(s => s.State == JKSoundState.Playing) && pool.Volume == JKContentManager.Audio.Player.LAND_VOLUME,
                            "Hammer cleanup preserves shared landing voices and native volume");
                        pool.PlayOneShot();
                        Check(voices.Count(s => s.State == JKSoundState.Playing) == 2, "King landing can play independently after hammer disposal");
                    }
                }
                finally { instance.SetValue(null, previous); foreach (var voice in voices) voice.Dispose(); }
            }
            Console.WriteLine("[OK] Installed SnowLand asset, shared pool lifetime, native volume and independent landing playback");
        }
    }
}
