using System;
using JKRuntime.Simulation;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void NativeRoute(bool subframe = false)
        {
            var screens = new[] {
                new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(0, 320, 480, 40)),
                    new BoxBlock(new Rectangle(170, 220, 140, 8)), new BoxBlock(new Rectangle(0, 120, 140, 8)),
                    new BoxBlock(new Rectangle(300, 120, 180, 8)), new BoxBlock(new Rectangle(150, 20, 180, 8)) },
                    new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
                new LevelScreen(1, new IBlock[] { new BoxBlock(new Rectangle(0, -80, 150, 8)), new BoxBlock(new Rectangle(330, -80, 150, 8)) },
                    new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) };
            var seed = new SimulationSeed(new SimulationPose { Position = new Vector2(240, 294), Width = 18, Height = 26, Grounded = true },
                0, 1.0 / 60, NativeWind.AuditedGameSha256, new byte[0], new[] { NativePlayer.Requirement });
            var registry = new SimulationRegistry();
            using (registry.Register(new NativePlayer(screens, new NativeMemory { WasGrounded = true, Subframe = subframe }, false).Provider()))
            using (var search = new PrioritySearch(new NativeWorld(screens, 0).ExitTargets(),
                t => new SearchJob(registry.Open(seed), NativeActions.Expand, t, tickLimit: 350000, equivalentState: NativeActions.StaticIdentity)))
            {
                for (int i = 0; i < 10000 && search.Status == SearchStatus.Running; i++) search.Advance(50, 5000);
                Check(search.Status == SearchStatus.SimulatedRoute, "Native multi-jump route failed: " + search.Detail + "; ticks=" + search.SimulatedTicks);
                Check(search.Instructions.Length >= 3 && search.Route[search.Route.Length - 1].Pose.Screen == 1, "Native multi-jump fixture was bypassed");
                Console.WriteLine("[OK] Native multi-jump search + double replay: " + search.Instructions.Length + " actions, " + search.SimulatedTicks + " simulation ticks");
            }
        }
    }
}
