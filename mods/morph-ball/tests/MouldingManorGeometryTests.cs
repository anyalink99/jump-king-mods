using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MorphBallMod;
using JumpKing.Level;
using Microsoft.Xna.Framework;

internal static partial class MorphBallTests
{
    private static void TestMouldingManorGeometry(string levelTexturePath)
    {
        Stopwatch performanceBudget = Stopwatch.StartNew();
        LevelTextureData texture = ReadLevelTexture(levelTexturePath);
        const int firstScreen = 109;
        const int lastScreen = 116;
        const int screenWidth = 60;
        const int screenHeight = 45;
        const int cellSize = 8;
        int rows = (lastScreen - firstScreen + 1) * screenHeight;
        bool[,] solid = new bool[screenWidth, rows];
        bool[,] slope = new bool[screenWidth, rows];
        for (int screen = firstScreen; screen <= lastScreen; screen++)
        {
            int worldScreenRow = lastScreen - screen;
            for (int y = 0; y < screenHeight; y++)
            {
                for (int x = 0; x < screenWidth; x++)
                {
                    byte red;
                    byte green;
                    byte blue;
                    byte alpha;
                    GetAtlasPixel(
                        texture,
                        screen,
                        x,
                        y,
                        out red,
                        out green,
                        out blue,
                        out alpha);
                    int worldY = worldScreenRow * screenHeight + y;
                    bool isBox = alpha != 0
                        && red == 0 && green == 0 && blue == 0;
                    bool isSlope = alpha != 0
                        && red == 255 && green == 0 && blue == 0;
                    solid[x, worldY] = isBox || isSlope;
                    slope[x, worldY] = isSlope;
                }
            }
        }

        IBlock[,] blocks = new IBlock[screenWidth, rows];
        int slopeCount = 0;
        int southWestCount = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < screenWidth; x++)
            {
                if (!solid[x, y])
                {
                    continue;
                }
                Rectangle rectangle = new Rectangle(
                    x * cellSize,
                    y * cellSize,
                    cellSize,
                    cellSize);
                if (!slope[x, y])
                {
                    blocks[x, y] = new BoxBlock(rectangle);
                    continue;
                }
                SlopeType type = InferSlopeType(solid, x, y);
                blocks[x, y] = type == SlopeType.None
                    ? (IBlock)new BoxBlock(rectangle)
                    : new SlopeBlock(rectangle, type);
                if (type != SlopeType.None)
                {
                    slopeCount++;
                }
                if (type == SlopeType.BottomLeft)
                {
                    southWestCount++;
                }
            }
        }

        int neighborhoods = 0;
        int traversedSegments = 0;
        int traversedSouthWestSegments = 0;
        int rebuiltPaths = 0;
        int impactContacts = 0;
        int orientedContacts = 0;
        int topSurfacePaths = 0;
        int topSurfaceReleases = 0;
        for (int centerY = 0; centerY < rows; centerY++)
        {
            for (int centerX = 0; centerX < screenWidth; centerX++)
            {
                if (blocks[centerX, centerY] == null)
                {
                    continue;
                }
                List<IBlock> neighborhood = new List<IBlock>();
                for (int y = Math.Max(0, centerY - 3);
                    y <= Math.Min(rows - 1, centerY + 3);
                    y++)
                {
                    for (int x = Math.Max(0, centerX - 3);
                        x <= Math.Min(screenWidth - 1, centerX + 3);
                        x++)
                    {
                        if (blocks[x, y] != null)
                        {
                            neighborhood.Add(blocks[x, y]);
                        }
                    }
                }
                List<MorphContourSegment> exterior =
                    MorphContourGeometry.BuildExterior(
                        neighborhood,
                        18,
                        18);
                neighborhoods++;
                foreach (MorphContourSegment segment in exterior)
                {
                    AssertTrue(
                        IsFinite(segment.Start)
                            && IsFinite(segment.End)
                            && IsFinite(segment.Normal),
                        "Moulding Manor exterior is finite");
                    float length = (segment.End - segment.Start).Length();
                    if (length <= 0.01f)
                    {
                        continue;
                    }
                    Vector2 midpoint = (segment.Start + segment.End) * 0.5f;
                    float crossingDistance = length * 0.5f + 0.25f;
                    Vector2 advanced;
                    MorphContourSegment advancedSegment;
                    AssertTrue(
                        MorphContourGeometry.TryAdvance(
                            exterior,
                            segment,
                            midpoint,
                            crossingDistance,
                            out advanced,
                            out advancedSegment),
                        "Moulding Manor contour crosses its forward vertex");
                    AssertTrue(
                        MorphContourGeometry.TryAdvance(
                            exterior,
                            segment,
                            midpoint,
                            -crossingDistance,
                            out advanced,
                            out advancedSegment),
                        "Moulding Manor contour crosses its reverse vertex");
                    traversedSegments++;
                    Vector2 impactPoint = midpoint
                        + segment.Normal * 1.5f;
                    MorphContourSegment selectedContact;
                    float selectedFraction;
                    AssertTrue(
                        MorphContourGeometry.TrySweepContact(
                            exterior,
                            impactPoint,
                            -segment.Normal * 5f,
                            out selectedContact,
                            out selectedFraction),
                        "Moulding Manor impact acquires an exterior surface");
                    AssertTrue(
                        Vector2.Dot(
                            selectedContact.Normal,
                            segment.Normal) > 0.99f,
                        "Moulding Manor impact keeps the approached surface");
                    impactContacts++;
                    AssertTrue(
                        MorphContourGeometry.TrySelectOrientedContact(
                            exterior,
                            impactPoint,
                            segment.Normal,
                            6f,
                            out selectedContact),
                        "Moulding Manor native support resolves to its exact contour");
                    AssertTrue(
                        Vector2.Dot(
                            selectedContact.Normal,
                            segment.Normal) > 0.999f,
                        "Moulding Manor native support preserves its slope normal");
                    orientedContacts++;
                    if (segment.Normal.Y < -0.05f)
                    {
                        float untraversed;
                        bool topAdvanced = MorphContourGeometry.TryAdvance(
                            exterior,
                            segment,
                            midpoint,
                            crossingDistance,
                            true,
                            out advanced,
                            out advancedSegment,
                            out untraversed);
                        AssertTrue(
                            IsFinite(advanced),
                            "Moulding Manor top-only result is finite");
                        if (topAdvanced)
                        {
                            AssertTrue(
                                advancedSegment.Normal.Y < -0.05f,
                                "Moulding Manor top-only path stays upper-facing");
                            AssertNear(
                                untraversed,
                                0f,
                                "Moulding Manor top-only path consumes motion");
                            topSurfacePaths++;
                        }
                        else
                        {
                            AssertTrue(
                                untraversed > 0f,
                                "Moulding Manor top-only release preserves motion");
                            topSurfaceReleases++;
                        }
                    }
                    SlopeBlock sourceSlope = segment.Block as SlopeBlock;
                    if (sourceSlope != null
                        && sourceSlope.GetSlopeType() == SlopeType.BottomLeft)
                    {
                        traversedSouthWestSegments++;
                    }
                }

                MorphContourSegment representative;
                if (TryFindRepresentative(
                    exterior,
                    blocks[centerX, centerY],
                    out representative))
                {
                    AssertTrue(
                        SimulateRebuiltPath(
                            blocks,
                            representative,
                            1),
                        "Moulding Manor runtime contour advances forward at "
                            + centerX + "," + centerY);
                    AssertTrue(
                        SimulateRebuiltPath(
                            blocks,
                            representative,
                            -1),
                        "Moulding Manor runtime contour advances backward at "
                            + centerX + "," + centerY);
                    rebuiltPaths += 2;
                }
            }
        }
        AssertTrue(slopeCount > 0, "Moulding Manor slopes were extracted");
        AssertTrue(
            southWestCount > 0,
            "Moulding Manor south-west slopes were extracted");
        AssertTrue(
            traversedSouthWestSegments > 0,
            "Moulding Manor south-west exterior was traversed");
        AssertTrue(
            topSurfacePaths > 0 && topSurfaceReleases > 0,
            "Moulding Manor includes rolling paths and native releases");
        performanceBudget.Stop();
        AssertTrue(
            performanceBudget.Elapsed < TimeSpan.FromSeconds(60d),
            "Moulding Manor contour regression stays inside 60 seconds");
        Console.WriteLine(
            "[OK] Moulding Manor: " + neighborhoods
                + " neighborhoods, " + slopeCount
                + " slopes, " + traversedSegments
                + " exterior segments, " + rebuiltPaths
                + " rebuilt paths, " + impactContacts
                + " swept contacts, " + orientedContacts
                + " native support contacts, " + topSurfacePaths
                + " rolling transitions, " + topSurfaceReleases
                + " native releases, "
                + performanceBudget.ElapsedMilliseconds
                + "ms");
    }

    private static bool TryFindRepresentative(
        List<MorphContourSegment> exterior,
        IBlock block,
        out MorphContourSegment representative)
    {
        representative = default(MorphContourSegment);
        float bestLength = 0f;
        SlopeBlock slope = block as SlopeBlock;
        Vector2 preferredNormal = slope == null
            ? Vector2.Zero
            : Vector2.Normalize(slope.GetNormal());
        foreach (MorphContourSegment segment in exterior)
        {
            if (!ReferenceEquals(segment.Block, block))
            {
                continue;
            }
            float length = (segment.End - segment.Start).Length();
            float preference = slope != null
                && Vector2.Dot(segment.Normal, preferredNormal) > 0.999f
                    ? 1000f
                    : 0f;
            if (length + preference <= bestLength)
            {
                continue;
            }
            bestLength = length + preference;
            representative = segment;
        }
        return bestLength > 0f;
    }

    private static bool SimulateRebuiltPath(
        IBlock[,] blocks,
        MorphContourSegment initial,
        int direction)
    {
        MorphContourSegment current = initial;
        Vector2 position = (initial.Start + initial.End) * 0.5f;
        for (int step = 0; step < 20; step++)
        {
            List<IBlock> local = new List<IBlock>();
            int minX = Math.Max(
                0,
                (int)Math.Floor((position.X - 8f) / 8f));
            int maxX = Math.Min(
                blocks.GetLength(0) - 1,
                (int)Math.Floor((position.X + 26f) / 8f));
            int minY = Math.Max(
                0,
                (int)Math.Floor((position.Y - 8f) / 8f));
            int maxY = Math.Min(
                blocks.GetLength(1) - 1,
                (int)Math.Floor((position.Y + 26f) / 8f));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (blocks[x, y] != null)
                    {
                        local.Add(blocks[x, y]);
                    }
                }
            }
            if (current.Block != null && !local.Contains(current.Block))
            {
                local.Add(current.Block);
            }
            List<MorphContourSegment> exterior =
                MorphContourGeometry.BuildExterior(local, 18, 18);
            MorphContourSegment refreshed;
            if (!MorphContourGeometry.TryFindSegment(
                exterior,
                current,
                position,
                out refreshed))
            {
                return false;
            }
            Vector2 next;
            if (!MorphContourGeometry.TryAdvance(
                exterior,
                refreshed,
                position,
                direction * 0.5f,
                out next,
                out current)
                || !IsFinite(next))
            {
                return false;
            }
            if ((next - position).Length() < 0.01f)
            {
                return false;
            }
            position = next;
        }
        return true;
    }

    private static SlopeType InferSlopeType(
        bool[,] solid,
        int x,
        int y)
    {
        bool right = x + 1 < solid.GetLength(0) && solid[x + 1, y];
        bool left = x > 0 && solid[x - 1, y];
        bool below = y + 1 < solid.GetLength(1) && solid[x, y + 1];
        bool above = y > 0 && solid[x, y - 1];
        if (right && below) return SlopeType.TopLeft;
        if (left && below) return SlopeType.TopRight;
        if (right && above) return SlopeType.BottomLeft;
        if (left && above) return SlopeType.BottomRight;
        return SlopeType.None;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.X)
            && !float.IsNaN(value.Y)
            && !float.IsInfinity(value.X)
            && !float.IsInfinity(value.Y);
    }

    private sealed class LevelTextureData
    {
        internal int Width;
        internal int Height;
        internal byte[] Pixels;
    }

    private static LevelTextureData ReadLevelTexture(string path)
    {
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            if (new string(reader.ReadChars(4)) != "XNBw")
            {
                throw new InvalidDataException("level.xnb is not a Windows XNB");
            }
            byte version = reader.ReadByte();
            byte flags = reader.ReadByte();
            reader.ReadInt32();
            if (version != 5 || (flags & 0x80) != 0)
            {
                throw new InvalidDataException("unsupported level.xnb encoding");
            }
            int readers = Read7Bit(reader);
            for (int index = 0; index < readers; index++)
            {
                int length = Read7Bit(reader);
                reader.ReadBytes(length);
                reader.ReadInt32();
            }
            int sharedResources = Read7Bit(reader);
            if (sharedResources != 0)
            {
                throw new InvalidDataException("unsupported level.xnb resources");
            }
            int primaryReader = Read7Bit(reader);
            if (primaryReader <= 0 || primaryReader > readers)
            {
                throw new InvalidDataException("invalid level.xnb reader");
            }
            int format = reader.ReadInt32();
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            int mipCount = reader.ReadInt32();
            int byteCount = reader.ReadInt32();
            if (format != 0 || mipCount < 1 || byteCount != width * height * 4)
            {
                throw new InvalidDataException("unsupported level texture format");
            }
            return new LevelTextureData
            {
                Width = width,
                Height = height,
                Pixels = reader.ReadBytes(byteCount)
            };
        }
    }

    private static int Read7Bit(BinaryReader reader)
    {
        int result = 0;
        int shift = 0;
        while (shift < 35)
        {
            byte value = reader.ReadByte();
            result |= (value & 0x7f) << shift;
            if ((value & 0x80) == 0)
            {
                return result;
            }
            shift += 7;
        }
        throw new InvalidDataException("invalid XNB integer");
    }

    private static void GetAtlasPixel(
        LevelTextureData texture,
        int screen,
        int x,
        int y,
        out byte red,
        out byte green,
        out byte blue,
        out byte alpha)
    {
        int internalScreen = screen - 1;
        int atlasX = (internalScreen / 13) * 60 + x;
        int atlasY = (internalScreen % 13) * 45 + y;
        int offset = (atlasY * texture.Width + atlasX) * 4;
        red = texture.Pixels[offset];
        green = texture.Pixels[offset + 1];
        blue = texture.Pixels[offset + 2];
        alpha = texture.Pixels[offset + 3];
    }

    private static MorphContourSegment FindContourSegment(
        List<MorphContourSegment> segments,
        Vector2 normal)
    {
        Vector2 unit = Vector2.Normalize(normal);
        foreach (MorphContourSegment segment in segments)
        {
            if (Vector2.Dot(segment.Normal, unit) > 0.999f)
            {
                return segment;
            }
        }
        failures++;
        Console.Error.WriteLine("missing contour segment " + normal);
        return default(MorphContourSegment);
    }

}
