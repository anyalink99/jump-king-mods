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
        internal Vector2? AnchorOffset;
        internal int Screen;
        internal ReplayFrameFlags Flags;
        internal ReplayPose Pose;

        internal Vector2 DrawAnchor
        {
            get
            {
                return Position + (AnchorOffset ?? new Vector2(9f, 26f)) + VisualOffset;
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
        internal long TickDuration = ReplayTiming.LegacyTickDuration;
        internal long? CompletionTime;
    }

    internal sealed class ReplayData
    {
        internal ReplayHeader Header;
        internal List<ReplayAppearanceEvent> AppearanceEvents;
        internal List<ReplayFrame> Frames;
        internal List<ReplayCosmeticEvent> CosmeticEvents = new List<ReplayCosmeticEvent>();
        // null means the old format had no audio track; an empty track is silence
        internal List<ReplaySoundEvent> SoundEvents;
    }

    internal sealed class ReplayCosmeticEvent
    {
        internal int Frame;
        internal string Data;
    }

    internal sealed class ReplaySummary
    {
        internal ReplayHeader Header;
        internal string FilePath;

        internal TimeSpan Duration
        {
            get
            {
                return Header == null ? TimeSpan.Zero : ReplayTiming.Duration(Header, Header.FrameCount);
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
