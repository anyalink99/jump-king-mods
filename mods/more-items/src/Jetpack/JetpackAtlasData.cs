using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace JumpKingJetpack
{
    internal static class JetpackAtlasData
    {
        internal const int Width = 832;
        internal const int Height = 384;

        private const string PoseResource =
            "JumpKingJetpack.jetpack-poses.txt";

        private static readonly Pose[] RegularPoses = LoadPoses();

        internal static char[] BuildBodyAtlas()
        {
            return BuildAtlas();
        }

        internal static int RegularPoseCount
        {
            get { return RegularPoses.Length; }
        }

        internal static string GetPoseName(int poseIndex)
        {
            if (poseIndex < 0 || poseIndex >= RegularPoses.Length)
            {
                throw new ArgumentOutOfRangeException("poseIndex");
            }
            return RegularPoses[poseIndex].Name;
        }

        internal static int CountPixelsInPose(int poseIndex, char[] atlas)
        {
            if (poseIndex < 0 || poseIndex >= RegularPoses.Length)
            {
                throw new ArgumentOutOfRangeException("poseIndex");
            }
            if (atlas == null || atlas.Length != Width * Height)
            {
                throw new ArgumentException("Invalid jetpack atlas", "atlas");
            }

            Pose pose = RegularPoses[poseIndex];
            int count = 0;
            for (int y = pose.CellY; y < pose.CellY + 48; y++)
            {
                for (int x = pose.CellX; x < pose.CellX + 48; x++)
                {
                    if (atlas[y * Width + x] != '.')
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        internal static bool TryGetNozzle(
            int sourceX,
            int sourceY,
            out int x,
            out int y,
            out bool rotated)
        {
            foreach (Pose pose in RegularPoses)
            {
                if (pose.CellX != sourceX || pose.CellY != sourceY)
                {
                    continue;
                }
                GetNozzle(pose, out x, out y, out rotated);
                return true;
            }
            x = 0;
            y = 0;
            rotated = false;
            return false;
        }

        internal static void GetNozzle(
            int poseIndex,
            out int x,
            out int y,
            out bool rotated)
        {
            if (poseIndex < 0 || poseIndex >= RegularPoses.Length)
            {
                throw new ArgumentOutOfRangeException("poseIndex");
            }
            GetNozzle(RegularPoses[poseIndex], out x, out y, out rotated);
        }

        private static void GetNozzle(
            Pose pose,
            out int x,
            out int y,
            out bool rotated)
        {
            rotated = pose.Rotated;
            if (rotated)
            {
                x = pose.X - 1;
                y = pose.Y + 6;
            }
            else
            {
                x = pose.X + 6;
                y = pose.Y + JetpackSpriteData.Height;
            }
        }

        private static char[] BuildAtlas()
        {
            string[] sprite = JetpackSpriteData.GetBody();
            char[] atlas = new char[Width * Height];
            for (int index = 0; index < atlas.Length; index++)
            {
                atlas[index] = '.';
            }

            foreach (Pose pose in RegularPoses)
            {
                for (int y = 0; y < JetpackSpriteData.Height; y++)
                {
                    for (int x = 0; x < JetpackSpriteData.Width; x++)
                    {
                        char value = sprite[y][x];
                        if (value == '.')
                        {
                            continue;
                        }

                        int transformedX = pose.Rotated
                            ? JetpackSpriteData.Height - 1 - y
                            : x;
                        int transformedY = pose.Rotated ? x : y;
                        int atlasX = pose.CellX + pose.X + transformedX;
                        int atlasY = pose.CellY + pose.Y + transformedY;
                        if (atlasX >= pose.CellX
                            && atlasX < pose.CellX + 48
                            && atlasY >= pose.CellY
                            && atlasY < pose.CellY + 48)
                        {
                            atlas[atlasY * Width + atlasX] = value;
                        }
                    }
                }
            }
            return atlas;
        }

        private static Pose[] LoadPoses()
        {
            Assembly assembly = typeof(JetpackAtlasData).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(PoseResource))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Jetpack pose data is unavailable");
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    List<Pose> poses = new List<Pose>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }
                        string[] values = line.Split(',');
                        if (values.Length != 6)
                        {
                            throw new InvalidDataException(
                                "Invalid jetpack pose: " + line);
                        }
                        poses.Add(new Pose(
                            values[0],
                            ParseInt(values[1]),
                            ParseInt(values[2]),
                            ParseInt(values[3]),
                            ParseInt(values[4]),
                            bool.Parse(values[5])));
                    }
                    if (poses.Count != 13)
                    {
                        throw new InvalidDataException(
                            "Jetpack pose data must contain 13 frames");
                    }
                    return poses.ToArray();
                }
            }
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private struct Pose
        {
            internal readonly string Name;
            internal readonly int CellX;
            internal readonly int CellY;
            internal readonly int X;
            internal readonly int Y;
            internal readonly bool Rotated;

            internal Pose(
                string name,
                int cellX,
                int cellY,
                int x,
                int y,
                bool rotated)
            {
                Name = name;
                CellX = cellX;
                CellY = cellY;
                X = x;
                Y = y;
                Rotated = rotated;
            }
        }
    }
}
