using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.XnaWrappers;

namespace Replays
{
    // these byte values are saved in .jkr files
    internal enum ReplaySound : byte
    {
        Jump, Land, Bump, Splat, IceJump, IceLand, SnowJump, SnowLand,
        SnowSplat, IronLand, IronSplat, WaterJump, WaterLand, WaterBump, WaterSplat, SandLand
    }

    internal sealed class ReplaySoundEvent
    {
        internal int Frame;
        internal ReplaySound Sound;
    }

    internal static class ReplayAudio
    {
        internal static event Action<ReplaySound> Played;

        internal static void Prepare(JKRuntime.RuntimeScope scope)
        {
            using (JKRuntime.RuntimeApi.MeasureStartup("replays.audio-hooks"))
            {
                var patches = scope.Own(new JKRuntime.OwnedPatches("replays.audio"));
                patches.Add(typeof(OneShotSound).GetMethod("PlayOneShot"),
                    postfix: typeof(ReplayAudio).GetMethod("Observe", JKRuntime.OwnedPatches.Members));
            }
        }

        private static void Observe(OneShotSound __instance)
        {
            var handler = Played;
            if (handler == null || ReplayRuntime.ViewerActive) return;
            for (int i = 0; i <= (int)ReplaySound.SandLand; i++)
                if (ReferenceEquals(__instance, Resolve((ReplaySound)i)))
                {
                    handler((ReplaySound)i);
                    return;
                }
        }

        internal static OneShotSound Resolve(ReplaySound sound)
        {
            var game = Game1.instance;
            if (game == null || game.contentManager == null || game.contentManager.audio == null) return null;
            var player = game.contentManager.audio.player;
            if (player == null) return null;
            switch (sound)
            {
                case ReplaySound.Jump: return player.Jump;
                case ReplaySound.Land: return player.Land;
                case ReplaySound.Bump: return player.Bump;
                case ReplaySound.Splat: return player.Splat;
                case ReplaySound.IceJump: return player.IceJump;
                case ReplaySound.IceLand: return player.IceLand;
                case ReplaySound.SnowJump: return player.SnowJump;
                case ReplaySound.SnowLand: return player.SnowLand;
                case ReplaySound.SnowSplat: return player.SnowSplat;
                case ReplaySound.IronLand: return player.IronLand;
                case ReplaySound.IronSplat: return player.IronSplat;
                case ReplaySound.WaterJump: return player.WaterJump;
                case ReplaySound.WaterLand: return player.WaterLand;
                case ReplaySound.WaterBump: return player.WaterBump;
                case ReplaySound.WaterSplat: return player.WaterSplat;
                case ReplaySound.SandLand: return player.SandLand;
                default: return null;
            }
        }

        internal static ReplaySound? Infer(ReplayPose previous, ReplayPose current)
        {
            if (current == previous) return null;
            // a short jump under a ceiling can already be falling in its first sample
            if (previous == ReplayPose.Charge && (current == ReplayPose.JumpUp || current == ReplayPose.JumpFall))
                return ReplaySound.Jump;
            if (current == ReplayPose.Bounce) return ReplaySound.Bump;
            if (current == ReplayPose.Splat) return ReplaySound.Splat;
            bool air = previous == ReplayPose.JumpUp || previous == ReplayPose.JumpFall || previous == ReplayPose.Bounce;
            bool ground = current == ReplayPose.Idle || current == ReplayPose.Charge
                || current == ReplayPose.WalkOne || current == ReplayPose.WalkSmear || current == ReplayPose.WalkTwo;
            return air && ground ? (ReplaySound?)ReplaySound.Land : null;
        }
    }

    internal interface IReplayAudioOutput
    {
        void Play(ReplaySound sound);
        void Pause(bool paused);
        void Stop();
    }

    internal sealed class NativeReplayAudio : IReplayAudioOutput
    {
        private readonly HashSet<OneShotSound> voices = new HashSet<OneShotSound>();
        public void Play(ReplaySound id)
        {
            var sound = ReplayAudio.Resolve(id);
            if (sound == null) return;
            voices.Add(sound);
            sound.PlayOneShot();
        }
        public void Pause(bool paused)
        {
            foreach (var voice in voices) { if (paused) voice.Pause(); else voice.Resume(); }
        }
        public void Stop()
        {
            foreach (var voice in voices) voice.Stop();
            // borrowed map assets belong to the game, never dispose them here
            voices.Clear();
        }
    }

    internal sealed class ReplayAudioPlayback : IDisposable
    {
        private readonly ReplayData replay;
        private readonly IReplayAudioOutput output;
        private int last = -1, next;
        private bool paused;
        private bool finished;

        internal ReplayAudioPlayback(ReplayData value, IReplayAudioOutput sink)
        {
            replay = value;
            output = sink;
        }

        internal void Present(int index, bool playing, bool cut)
        {
            bool seek = cut || last < 0 || index < last || index > last + 8;
            // let the final landing ring out when the timeline stops naturally
            finished = !seek && !playing && index == replay.Frames.Count - 1
                && (finished || (!paused && index > last));
            bool audible = playing || finished;
            if (seek)
            {
                output.Stop();
                // binary search keeps scrubbing cheap even in long recordings
                int low = 0, high = replay.SoundEvents == null ? 0 : replay.SoundEvents.Count;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    if (replay.SoundEvents[middle].Frame <= index) low = middle + 1;
                    else high = middle;
                }
                next = low;
            }
            if (paused == audible) { paused = !audible; output.Pause(paused); }
            if (!seek && audible)
            {
                if (replay.SoundEvents != null)
                {
                    while (next < replay.SoundEvents.Count && replay.SoundEvents[next].Frame <= index)
                    {
                        var sound = replay.SoundEvents[next++];
                        if (sound.Frame > last) output.Play(sound.Sound);
                    }
                }
                else
                {
                    for (int frame = last + 1; frame <= index; frame++)
                    {
                        var sound = ReplayAudio.Infer(replay.Frames[frame - 1].Pose, replay.Frames[frame].Pose);
                        if (sound.HasValue) output.Play(sound.Value);
                    }
                }
            }
            last = index;
        }

        public void Dispose() { output.Stop(); }
    }
}
