using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace Replays
{
    // Runtime owns optional provider discovery; playback actors keep independent clocks.
    internal sealed class ReplayCosmetics : IDisposable
    {
        private JKRuntime.Presentation.ICosmeticActor actor;
        private readonly bool ghost;
        private int last = -1, stateStart;
        private string lastState = "";
        private readonly Dictionary<int, string[]> events;
        private readonly ReplayData replay;
        private bool failed;
        private long appearanceRevision=-1;
        internal ReplayCosmetics(ReplayData value, bool isGhost)
        {
            replay = value; ghost = isGhost;
            events = (value.CosmeticEvents ?? new List<ReplayCosmeticEvent>()).GroupBy(e => e.Frame).ToDictionary(g => g.Key, g => g.Select(e => e.Data).ToArray());
        }
        internal static IDisposable PlaybackScope() {return JKRuntime.Presentation.CosmeticPlayback.Playback();}
        internal static long Sequence {get{return JKRuntime.Presentation.CosmeticPlayback.Sequence;}}
        internal static void Capture(ReplayData replay,int frame,ref long sequence)
        {
            var values=JKRuntime.Presentation.CosmeticPlayback.ReadEvents(sequence);sequence=Sequence;
            foreach(var data in values)if(replay.CosmeticEvents.Count<1000000)replay.CosmeticEvents.Add(new ReplayCosmeticEvent {Frame=frame,Data=data});
        }
        internal void Present(int index, bool playing = true, bool cut = false)
        {
            if (failed || !JKRuntime.Presentation.CosmeticPlayback.Available || replay.Frames.Count == 0) return;
            try
            {
                long revision=JKRuntime.Presentation.PlayerAppearance.Revision;
                if (!cut && index == last&&appearanceRevision==revision) { if (actor != null) actor.Pause(!playing); return; }
                bool seek = cut || appearanceRevision!=revision || actor == null || index < last || index > last + 8;
                if (seek)
                {
                    if (actor != null) ((IDisposable)actor).Dispose(); actor = JKRuntime.Presentation.CosmeticPlayback.Create(ghost);
                    appearanceRevision=revision;
                    last = Math.Max(-1, index - 1801); lastState = "";
                    // find the actual clip start even when particle warmup is bounded to 30 seconds
                    stateStart = last + 1; string startState = State(replay.Frames[stateStart].Pose);
                    while (stateStart > 0 && State(replay.Frames[stateStart - 1].Pose) == startState) stateStart--;
                }
                if (actor == null) return;
                actor.Pause(!playing);
                for (int frame = last + 1; frame <= index; frame++)
                {
                    var value = replay.Frames[frame]; string state = State(value.Pose);
                    if (lastState.Length > 0 && state != lastState) stateStart = frame;
                    lastState = state;
                    var velocity = frame == 0 ? Vector2.Zero : value.Position - replay.Frames[frame - 1].Position;
                    string[] payload; if (!events.TryGetValue(frame, out payload)) payload = new string[0];
                    actor.Advance(frame, (frame - stateStart) / 60d, value.DrawAnchor, velocity, state,
                        (value.Flags & ReplayFrameFlags.FacingLeft) != 0, ReplayAppearanceTrack.AtFrame(replay, frame), payload, ghost || seek || !playing);
                }
                last = index;
            }
            catch (Exception error) { failed = true; Dispose(); Console.WriteLine("[Replays] Cosmetic playback disabled: " + error.GetBaseException().Message); }
        }
        internal IDisposable BeginDraw(Color tint) { return actor == null ? null : actor.BeginDraw(tint); }
        internal static string State(ReplayPose pose)
        {
            switch (pose) { case ReplayPose.Charge: return "charge"; case ReplayPose.JumpUp: return "rise"; case ReplayPose.JumpFall: case ReplayPose.Bounce: return "fall";
                case ReplayPose.Splat: return "splat"; case ReplayPose.LookUp: return "lookUp"; case ReplayPose.WalkOne: case ReplayPose.WalkSmear: case ReplayPose.WalkTwo: return "walk";
                case ReplayPose.StretchOne: case ReplayPose.StretchSmear: case ReplayPose.StretchTwo: return "recover"; default: return "idle"; }
        }
        public void Dispose() { if (actor != null) ((IDisposable)actor).Dispose(); actor = null; }
    }
}
