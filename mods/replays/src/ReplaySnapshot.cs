using System;
using System.Collections.Generic;

namespace Replays
{
    internal static class ReplaySnapshot
    {
        internal static ReplayData Create(ReplayData source)
        {
            if (source == null || source.Header == null || source.Frames == null)
                return null;
            ReplayHeader header = source.Header;
            var snapshot = new ReplayData
            {
                Header = new ReplayHeader
                {
                    Id = Guid.NewGuid().ToString("N"),
                    WorldKey = header.WorldKey,
                    WorldName = header.WorldName,
                    WorldAuthor = header.WorldAuthor,
                    WorldRevision = header.WorldRevision,
                    GameVersion = header.GameVersion,
                    ReplaysVersion = header.ReplaysVersion,
                    CreatedUtc = DateTime.UtcNow,
                    TotalScreens = header.TotalScreens,
                    InitialGameTicks = header.InitialGameTicks,
                    InitialGameTime = header.InitialGameTime
                },
                AppearanceEvents = CloneAppearance(source.AppearanceEvents),
                Frames = new List<ReplayFrame>(source.Frames)
            };
            VerificationBridge.Associate(snapshot, source.Header.Id);
            return snapshot;
        }

        private static List<ReplayAppearanceEvent> CloneAppearance(
            IList<ReplayAppearanceEvent> source)
        {
            List<ReplayAppearanceEvent> result =
                new List<ReplayAppearanceEvent>();
            foreach (ReplayAppearanceEvent appearance in source
                ?? new ReplayAppearanceEvent[0])
            {
                if (appearance == null) continue;
                result.Add(new ReplayAppearanceEvent
                {
                    Frame = appearance.Frame,
                    Items = appearance.Items == null
                        ? new int[0]
                        : (int[])appearance.Items.Clone()
                });
            }
            return result;
        }
    }
}
