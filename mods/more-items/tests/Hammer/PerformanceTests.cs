using System;
using System.Collections.Generic;
using System.Diagnostics;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private sealed class CountedWorld : IHammerWorld
        {
            internal readonly NativeWorld Native = new NativeWorld();
            internal int Queries;
            public bool Solid(Vector2 center, Vector2 half) { Queries++; return Native.Solid(center, half); }
            public bool Snow(Vector2 center, Vector2 half) { return Native.Snow(center, half); }
            public float Friction(Vector2 center) { return Native.Friction(center); }
        }
        private static void Performance()
        {
            var blocks = new List<IBlock>();
            blocks.Add(new BoxBlock(new Rectangle(0, 300, 480, 60)));
            for (int y = 70; y < 220; y += 8)
            for (int x = 70; x < 350; x += 8) blocks.Add(new BoxBlock(new Rectangle(x, y, 3, 3)));
            Scene(blocks.ToArray());
            foreach (bool close in new[] { false, true })
            {
                var world = new CountedWorld(); var root = new Vector2(199, 287);
                long worst = 0, total = 0; int queries = 0, narrow = 0;
                for (int i = -3; i < 30; i++)
                {
                    long before = Stopwatch.GetTimestamp();
                    world.Native.Prepare(root); world.Queries = 0;
                    var physics = Pose(root, close ? new Vector2(1, 0) : new Vector2(20, 10));
                    physics.Step(root, close ? new Vector2(-12, 0) : Vector2.Zero, close ? new Vector2(-48, 0) : new Vector2(8, 8), world);
                    long elapsed = Stopwatch.GetTimestamp() - before;
                    if (i < 0) continue;
                    queries = Math.Max(queries, world.Queries); narrow = Math.Max(narrow, world.Native.NarrowQueries);
                    worst = Math.Max(worst, elapsed); total += elapsed;
                }
                Check(queries < 25000 && narrow < 2000, "Dense scene and near-hinge input keep collision work bounded");
                Console.WriteLine("[PERF] " + (close ? "Near hinge" : "Loaded floor") + ": mean=" + (total * 1000.0 / Stopwatch.Frequency / 30).ToString("F3")
                    + " ms, max=" + (worst * 1000.0 / Stopwatch.Frequency).ToString("F3") + " ms, box queries=" + queries + ", native narrow queries=" + narrow);
            }
        }

        private static void GeometryCache()
        {
            var world = new NativeWorld(); var center = new Vector2(32, 200);
            Scene(new BoxBlock(new Rectangle(16, 190, 64, 32)));
            world.Prepare(center);
            Check(world.Solid(center, Vector2.One), "Indexed query sees a block spanning grid cells");
            int calls = world.NarrowQueries;
            for (int i = 0; i < 100; i++) Check(world.Solid(center, Vector2.One), "Repeated box query preserves collision");
            Check(world.NarrowQueries == calls, "Repeated boxes reuse their result within the current geometry snapshot");
            Scene(new BoxBlock(new Rectangle(80, 190, 64, 32)));
            world.Prepare(center);
            Check(!world.Solid(center, Vector2.One) && world.Solid(new Vector2(90, 200), Vector2.One),
                "Preparing moved/replaced geometry invalidates both cached hits and grid buckets");
            foreach (int x in new[] { -33, -17, -1, 0, 15, 16, 31 })
            {
                Scene(new BoxBlock(new Rectangle(x, 210, 1, 1)));
                world.Prepare(center);
                Check(world.Solid(new Vector2(x + .5f, 210.5f), new Vector2(.2f)), "One-pixel geometry survives positive and negative grid boundaries");
                Check(!world.Solid(new Vector2(x + 3f, 210.5f), new Vector2(.2f)), "Grid does not turn empty neighbouring space solid");
            }
        }
    }
}
