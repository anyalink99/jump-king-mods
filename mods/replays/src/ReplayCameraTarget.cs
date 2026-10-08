using JKRuntime;
using JKRuntime.Presentation;
using Microsoft.Xna.Framework;

namespace Replays
{
    internal sealed class ReplayCameraTarget
    {
        private readonly ReplayData replay;
        private readonly CameraTargets.Lease lease;
        private int previousIndex = -1;

        internal ReplayCameraTarget(ReplayData data, RuntimeScope scope)
        { replay = data; lease = CameraTargets.Acquire("replays", scope); }

        internal void Present(int index, bool playing, bool cut)
        {
            var frame = replay.Frames[index];
            // formats 2-4 have no camera-center track, retain the viewer's native center
            var center = frame.Position + new Vector2(9, 13);
            var velocity = Vector2.Zero;
            cut |= previousIndex < 0 || index < previousIndex;
            if (!cut && index > 0)
            {
                var distance = frame.Position - replay.Frames[index - 1].Position;
                // don't turn a teleport into camera anticipation
                if (distance.LengthSquared() <= 270 * 270)
                    velocity = distance * 60f;
            }
            lease.Publish(new CameraTarget(center, velocity, frame.Screen - 1, !playing), cut);
            previousIndex = index;
        }
    }
}
