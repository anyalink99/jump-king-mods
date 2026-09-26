using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace Replays
{
    internal static class ReplayCodec
    {
        private const int FormatVersion = 2;
        internal const int MaximumFrames = 60 * 60 * 60 * 24;
        private const int MaximumEquippedItems = 5;
        private const int MaximumStringBytes = 4096;
        private const int MaximumScreens = 100000;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("JKRP");

        internal static void Write(string path, ReplayData replay)
        {
            if (replay == null || replay.Header == null)
                throw new ArgumentNullException("replay");
            if (replay.Frames == null)
                throw new ArgumentException("Replay frames are required", "replay");
            if (replay.AppearanceEvents == null
                || replay.AppearanceEvents.Count == 0)
            {
                throw new ArgumentException(
                    "Replay appearance events are required",
                    "replay");
            }
            if (replay.Frames.Count > MaximumFrames)
                throw new InvalidDataException("Replay exceeds the 24-hour format limit");

            replay.Header.FrameCount = replay.Frames.Count;
            using (FileStream file = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (GZipStream gzip = new GZipStream(
                file,
                CompressionMode.Compress,
                false))
            using (BinaryWriter writer = new BinaryWriter(gzip, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                WriteHeader(writer, replay.Header);
                WriteAppearance(writer, replay);
                foreach (ReplayFrame frame in replay.Frames)
                {
                    ValidateFrame(frame, replay.Header.TotalScreens);
                    writer.Write(frame.Position.X);
                    writer.Write(frame.Position.Y);
                    writer.Write(frame.VisualOffset.X);
                    writer.Write(frame.VisualOffset.Y);
                    writer.Write(frame.Screen);
                    writer.Write((byte)frame.Flags);
                    writer.Write((byte)frame.Pose);
                }
            }
        }

        internal static ReplaySummary ReadSummary(string path)
        {
            using (BinaryReader reader = OpenReader(path))
            {
                ReadPreamble(reader);
                return new ReplaySummary
                {
                    Header = ReadHeader(reader),
                    FilePath = path
                };
            }
        }

        internal static ReplayData Read(string path)
        {
            using (BinaryReader reader = OpenReader(path))
            {
                ReadPreamble(reader);
                ReplayHeader header = ReadHeader(reader);
                List<ReplayAppearanceEvent> appearance =
                    ReadAppearance(reader, header.FrameCount);
                List<ReplayFrame> frames = new List<ReplayFrame>(header.FrameCount);
                for (int index = 0; index < header.FrameCount; index++)
                {
                    ReplayFrame frame = new ReplayFrame
                    {
                        Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                        VisualOffset = new Vector2(
                            reader.ReadSingle(),
                            reader.ReadSingle()),
                        Screen = reader.ReadInt32(),
                        Flags = (ReplayFrameFlags)reader.ReadByte(),
                        Pose = (ReplayPose)reader.ReadByte()
                    };
                    ValidateFrame(frame, header.TotalScreens);
                    frames.Add(frame);
                }
                if (reader.Read() != -1)
                    throw new InvalidDataException("Replay contains trailing data");
                return new ReplayData
                {
                    Header = header,
                    AppearanceEvents = appearance,
                    Frames = frames
                };
            }
        }

        private static BinaryReader OpenReader(string path)
        {
            FileStream file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            try
            {
                return new BinaryReader(
                    new GZipStream(file, CompressionMode.Decompress, false),
                    Encoding.UTF8);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        private static void ReadPreamble(BinaryReader reader)
        {
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length
                || Encoding.ASCII.GetString(magic) != "JKRP")
            {
                throw new InvalidDataException("Not a Jump King replay");
            }
            int version = reader.ReadInt32();
            if (version != FormatVersion)
                throw new InvalidDataException("Unsupported replay version: " + version);
        }

        private static void WriteHeader(BinaryWriter writer, ReplayHeader header)
        {
            ValidateHeader(header);
            WriteString(writer, header.Id);
            WriteString(writer, header.WorldKey);
            WriteString(writer, header.WorldName);
            WriteString(writer, header.WorldAuthor);
            WriteString(writer, header.WorldRevision);
            WriteString(writer, header.GameVersion);
            WriteString(writer, header.ReplaysVersion);
            writer.Write(header.CreatedUtc.ToUniversalTime().Ticks);
            writer.Write(header.TotalScreens);
            writer.Write(header.InitialGameTicks);
            writer.Write(header.InitialGameTime);
            writer.Write(header.FrameCount);
        }

        private static void WriteAppearance(
            BinaryWriter writer,
            ReplayData replay)
        {
            writer.Write(replay.AppearanceEvents.Count);
            int previousFrame = -1;
            foreach (ReplayAppearanceEvent appearance in replay.AppearanceEvents)
            {
                if (appearance == null
                    || appearance.Frame < 0
                    || appearance.Frame >= replay.Header.FrameCount
                    || appearance.Frame <= previousFrame)
                {
                    throw new InvalidDataException(
                        "Replay appearance events must be ordered and in range");
                }
                int[] items = appearance.Items ?? new int[0];
                ValidateItems(items);
                writer.Write(appearance.Frame);
                writer.Write((byte)items.Length);
                foreach (int item in items) writer.Write((byte)item);
                previousFrame = appearance.Frame;
            }
            if (replay.AppearanceEvents[0].Frame != 0)
                throw new InvalidDataException("Replay appearance must start at frame zero");
        }

        private static List<ReplayAppearanceEvent> ReadAppearance(
            BinaryReader reader,
            int frameCount)
        {
            int count = reader.ReadInt32();
            if (count <= 0 || count > frameCount)
                throw new InvalidDataException("Invalid replay appearance event count");
            List<ReplayAppearanceEvent> result =
                new List<ReplayAppearanceEvent>(count);
            int previousFrame = -1;
            for (int index = 0; index < count; index++)
            {
                int frame = reader.ReadInt32();
                int itemCount = reader.ReadByte();
                if (frame < 0 || frame >= frameCount || frame <= previousFrame)
                    throw new InvalidDataException("Invalid replay appearance frame");
                int[] items = new int[itemCount];
                for (int item = 0; item < itemCount; item++)
                    items[item] = reader.ReadByte();
                ValidateItems(items);
                result.Add(new ReplayAppearanceEvent
                {
                    Frame = frame,
                    Items = items
                });
                previousFrame = frame;
            }
            if (result[0].Frame != 0)
                throw new InvalidDataException("Replay appearance must start at frame zero");
            return result;
        }

        private static void ValidateItems(int[] items)
        {
            if (items.Length > MaximumEquippedItems)
                throw new InvalidDataException("Too many equipped replay items");
            for (int index = 0; index < items.Length; index++)
            {
                if (items[index] < 0 || items[index] >= (int)Items.NULL)
                    throw new InvalidDataException("Invalid equipped replay item");
                for (int previous = 0; previous < index; previous++)
                    if (items[previous] == items[index])
                        throw new InvalidDataException("Duplicate equipped replay item");
            }
        }

        private static void ValidateFrame(ReplayFrame frame, int totalScreens)
        {
            if (float.IsNaN(frame.Position.X)
                || float.IsInfinity(frame.Position.X)
                || float.IsNaN(frame.Position.Y)
                || float.IsInfinity(frame.Position.Y))
            {
                throw new InvalidDataException("Replay contains a non-finite position");
            }
            if (float.IsNaN(frame.VisualOffset.X)
                || float.IsInfinity(frame.VisualOffset.X)
                || float.IsNaN(frame.VisualOffset.Y)
                || float.IsInfinity(frame.VisualOffset.Y)
                || Math.Abs(frame.VisualOffset.X) > 128f
                || Math.Abs(frame.VisualOffset.Y) > 128f)
            {
                throw new InvalidDataException(
                    "Replay contains an invalid visual offset");
            }
            if (frame.Screen < 1 || frame.Screen > totalScreens)
                throw new InvalidDataException("Replay contains an invalid screen");
            if ((frame.Flags & ~ReplayFrameFlags.FacingLeft) != 0)
                throw new InvalidDataException("Replay contains invalid frame flags");
            if (!Enum.IsDefined(typeof(ReplayPose), frame.Pose))
                throw new InvalidDataException("Replay contains an invalid pose");
        }

        private static ReplayHeader ReadHeader(BinaryReader reader)
        {
            ReplayHeader header = new ReplayHeader
            {
                Id = ReadString(reader, "id"),
                WorldKey = ReadString(reader, "world key"),
                WorldName = ReadString(reader, "world name"),
                WorldAuthor = ReadString(reader, "world author"),
                WorldRevision = ReadString(reader, "world revision"),
                GameVersion = ReadString(reader, "game version"),
                ReplaysVersion = ReadString(reader, "Replays version"),
                CreatedUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                TotalScreens = reader.ReadInt32(),
                InitialGameTicks = reader.ReadInt32(),
                InitialGameTime = reader.ReadSingle(),
                FrameCount = reader.ReadInt32()
            };
            ValidateHeader(header);
            return header;
        }

        private static void ValidateHeader(ReplayHeader header)
        {
            RequireText(header.Id, "id");
            RequireText(header.WorldKey, "world key");
            RequireText(header.WorldName, "world name");
            RequireText(header.WorldRevision, "world revision");
            RequireText(header.GameVersion, "game version");
            RequireText(header.ReplaysVersion, "Replays version");
            if (header.FrameCount < 0 || header.FrameCount > MaximumFrames)
                throw new InvalidDataException("Invalid replay frame count");
            if (header.TotalScreens <= 0 || header.TotalScreens > MaximumScreens)
                throw new InvalidDataException("Invalid replay screen count");
            if (header.InitialGameTicks < 0)
                throw new InvalidDataException("Invalid replay game clock");
            if (float.IsNaN(header.InitialGameTime)
                || float.IsInfinity(header.InitialGameTime)
                || header.InitialGameTime < 0f)
            {
                throw new InvalidDataException(
                    "Invalid replay sub-frame game clock");
            }
            ValidateString(header.Id, "id");
            ValidateString(header.WorldKey, "world key");
            ValidateString(header.WorldName, "world name");
            ValidateString(header.WorldAuthor, "world author");
            ValidateString(header.WorldRevision, "world revision");
            ValidateString(header.GameVersion, "game version");
            ValidateString(header.ReplaysVersion, "Replays version");
        }

        private static void ValidateString(string value, string name)
        {
            if (value == null || Encoding.UTF8.GetByteCount(value) > MaximumStringBytes)
                throw new InvalidDataException("Invalid replay " + name);
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("Replay " + name + " is missing");
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            string text = value ?? string.Empty;
            ValidateString(text, "text");
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader, string name)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > MaximumStringBytes)
                throw new InvalidDataException("Invalid replay " + name);
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("Truncated replay " + name);
            string value = Encoding.UTF8.GetString(bytes);
            ValidateString(value, name);
            return value;
        }
    }
}
