using System;
using System.IO;
using System.Linq;
using JKRuntime.Simulation;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static int Seven(BinaryReader r)
        { int n = 0, shift = 0; byte b; do { b = r.ReadByte(); n |= (b & 127) << shift; shift += 7; } while ((b & 128) != 0 && shift < 35); return n; }
        private static void InstalledMapRoute()
        {
            LevelTexture texture;
            using (var r = new BinaryReader(File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "level.xnb"))))
            {
                Check(new string(r.ReadChars(4)) == "XNBw" && r.ReadByte() == 5 && r.ReadByte() == 0, "Installed level texture encoding changed");
                Check(r.ReadInt32() == r.BaseStream.Length, "XNB length");
                int readers = Seven(r); for (int i = 0; i < readers; i++) { r.ReadString(); r.ReadInt32(); }
                Check(Seven(r) == 0 && Seven(r) == 1 && r.ReadInt32() == 0, "XNB texture reader");
                int width = r.ReadInt32(), height = r.ReadInt32();
                Check(r.ReadInt32() == 1 && r.ReadInt32() == width * height * 4, "XNB color level");
                var colors = new Color[width * height];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                texture = (LevelTexture)Activator.CreateInstance(typeof(LevelTexture), NativeWorld.Flags, null, new object[] { colors, width, height }, null);
            }
            // Decode the installed collision pixels through the game's own block
            // factory. This is not hand-drawn test geometry or screenshot tracing.
            var load = typeof(LevelManager).GetMethod("LoadBlocksInterval", NativeWorld.Flags);
            var screens = new LevelScreen[3];
            for (int i = 0; i < screens.Length; i++)
            {
                object[] args = { texture, null, i, false, null, 0f, null };
                var blocks = (IBlock[])load.Invoke(null, args);
                screens[i] = new LevelScreen(i, blocks, new LevelScreen.Graphics(), (bool)args[3], (TeleportLink[])args[4], (float)args[5], (bool?)args[6]);
            }
            foreach (bool sfc in new[] { false, true })
            {
                var registry = new SimulationRegistry();
                var seed = new SimulationSeed(new SimulationPose { Position = new Vector2(240, 290), Width = 18, Height = 26 },
                    0, 1.0 / 60, NativeWind.AuditedGameSha256, new byte[0], new[] { NativePlayer.Requirement });
                using (registry.Register(new NativePlayer(screens, new NativeMemory { Subframe = sfc }, false).Provider()))
                using (var session = registry.Open(seed))
                {
                    var start = session.Initial;
                    for (int i = 0; i < 180 && !start.Pose.StableLanding; i++) start = session.Step(start, new SimulationInput()).State;
                    Check(start.Pose.StableLanding && start.Pose.Screen == 0, "Installed starting floor");
                    var grounded = new SimulationSeed(start.Pose, start.Tick, seed.TickSeconds, seed.Fingerprint, new byte[0], new[] { NativePlayer.Requirement });
                    var routes = new SimulationRegistry();
                    using (routes.Register(new NativePlayer(screens, NativeMemory.Decode(start.Read(NativePlayer.Id)), false).Provider()))
                    using (var job = new SearchJob(routes.Open(grounded), NativeActions.Expand, new[] { new SearchTarget(0, 1, ExitDirection.Up) }, nodeLimit: 2000, tickLimit: 350000, equivalentState: NativeActions.StaticIdentity))
                    {
                        for (int n = 0; n < 10000 && job.Status == SearchStatus.Running; n++) job.Advance(50, 5000);
                        Check(job.Status == SearchStatus.SimulatedRoute, "Installed screen 1 -> 2 route: " + job.Detail);
                        Console.WriteLine("[OK] Installed main-game screen 1 -> 2, SFC=" + sfc + ": " + string.Join("; ", job.Instructions));
                    }
                }
            }
        }
    }
}
