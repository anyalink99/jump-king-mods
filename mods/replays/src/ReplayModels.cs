using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Replays
{
    [Flags]
    internal enum ReplayFrameFlags : byte
    {
        None = 0,
        FacingLeft = 1
    }

    internal enum ReplayPose : byte
    {
        Idle,
        WalkOne,
        WalkSmear,
        WalkTwo,
        Charge,
        JumpUp,
        JumpFall,
        Bounce,
        Splat,
        LookUp,
        StretchOne,
        StretchSmear,
        StretchTwo
    }

    internal struct ReplayFrame
    {
        internal Vector2 Position;
        internal Vector2 VisualOffset;
        internal int Screen;
        internal ReplayFrameFlags Flags;
        internal ReplayPose Pose;

        internal Vector2 DrawAnchor
        {
            get
            {
                return Position + new Vector2(9f, 26f) + VisualOffset;
            }
        }
    }

    internal sealed class ReplayAppearanceEvent
    {
        internal int Frame;
        internal int[] Items;
    }

    internal sealed class ReplayHeader
    {
        internal string Id;
        internal string WorldKey;
        internal string WorldName;
        internal string WorldAuthor;
        internal string WorldRevision;
        internal string GameVersion;
        internal string ReplaysVersion;
        internal DateTime CreatedUtc;
        internal int TotalScreens;
        internal int InitialGameTicks;
        internal float InitialGameTime;
        internal int FrameCount;
    }

    internal sealed class ReplayData
    {
        internal ReplayHeader Header;
        internal List<ReplayAppearanceEvent> AppearanceEvents;
        internal List<ReplayFrame> Frames;
    }

    internal sealed class ReplaySummary
    {
        internal ReplayHeader Header;
        internal string FilePath;

        internal TimeSpan Duration
        {
            get
            {
                return TimeSpan.FromSeconds(
                    Header == null ? 0d : Header.FrameCount / 60d);
            }
        }
    }

    internal sealed class ReplayWorld
    {
        internal string Key;
        internal string Name;
        internal string Author;
        internal string Revision;
        internal int TotalScreens;
    }
}
