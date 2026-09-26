using System;
using System.IO;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace HammerKing
{
    internal struct HammerImpacts { internal float Wood, Stone, Snow; }

    internal sealed class HammerImpactGate
    {
        private int quiet = 3, cooldown;
        internal void Suppress() { quiet = 0; cooldown = 6; }
        internal float Step(float speed)
        {
            if (cooldown > 0) cooldown--;
            if (speed < 0.1f) { quiet = Math.Min(3, quiet + 1); return 0f; }
            bool play = quiet >= 3 && cooldown == 0 && speed >= 1.2f;
            quiet = 0;
            if (!play) return 0f;
            cooldown = 6;
            return MathHelper.Clamp(speed / 10f, 0.25f, 1f);
        }
    }

    internal sealed class HammerSound : IDisposable
    {
        internal const string WoodResource = "HammerKing.hammer-wood-8bit.wav";
        internal const string StoneResource = "HammerKing.hammer-stone-8bit.wav";
        private readonly HammerImpactGate woodGate = new HammerImpactGate(), stoneGate = new HammerImpactGate();
        private readonly JKSound wood, stone;
        private bool disposed;
        private readonly JKRuntime.RuntimeScope resources = new JKRuntime.RuntimeScope();
        internal HammerSound()
        {
            wood = resources.Own(Load(WoodResource));
            try { stone = resources.Own(Load(StoneResource)); } catch { resources.Dispose(); throw; }
        }
        private static JKSound Load(string name)
        {
            using (Stream stream = typeof(HammerSound).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Missing hammer sound: " + name);
                var effect = SoundEffect.FromStream(stream);
                try { return new JKSound(effect, SoundType.SFX); } catch { effect.Dispose(); throw; }
            }
        }
        internal void Update(HammerImpacts impacts)
        {
            if (disposed) return;
            Play(wood, woodGate.Step(impacts.Wood));
            float strength = stoneGate.Step(Math.Max(impacts.Stone, impacts.Snow));
            if (strength > 0 && impacts.Snow >= impacts.Stone)
                PlaySnow();
            else Play(stone, strength);
        }
        internal static void PlaySnow()
        {
            // Borrow the current level's native one-shot. Never adjust, stop or
            // dispose it: it is also used by the king's own landing pipeline.
            var game = JumpKing.Game1.instance;
            if (game != null && game.contentManager != null && game.contentManager.audio != null
                && game.contentManager.audio.player != null && game.contentManager.audio.player.SnowLand != null)
                game.contentManager.audio.player.SnowLand.PlayOneShot();
        }
        private static void Play(JKSound sound, float strength)
        {
            if (strength <= 0f) return;
            sound.Stop(); sound.Volume = strength * 0.7f; sound.Play();
        }
        internal void Stop()
        {
            woodGate.Suppress(); stoneGate.Suppress();
            if (disposed) return;
            try { wood.Stop(); } finally { stone.Stop(); }
        }
        public void Dispose()
        {
            disposed = true;
            resources.Dispose();
        }
    }
}
