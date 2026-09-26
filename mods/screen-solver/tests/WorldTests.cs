using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.Simulation;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void WorldParity(bool[] vertical = null, CustomWindProfile[] profiles = null)
        {
            var gameField = typeof(Game1).GetField("_instance", NativeWorld.Flags);
            var statsType = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.PlayerStats", true);
            var managerType = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var managerField = managerType.GetField("instance", NativeWorld.Flags);
            object oldGame = gameField.GetValue(null), oldManager = managerField.GetValue(null);
            try
            {
                var game = (Game1)Empty(typeof(Game1)); GC.SuppressFinalize(game); gameField.SetValue(null, game);
                var manager = Empty(managerType); managerField.SetValue(null, manager);
                managerType.GetField("m_snapshot", NativeWorld.Flags).SetValue(manager, Activator.CreateInstance(statsType));
                int ticks = 0, teleports = 0, verticalUp = 0, verticalDown = 0, horizontal = 0;
                foreach (long clock in new long[] { 166667, 170000 })
                foreach (int startTick in vertical == null ? new[] { 587 } : new[] { 0, 587 })
                foreach (bool snow in vertical == null ? new[] { false } : new[] { false, true })
                foreach (bool wind in new[] { false, true })
                foreach (bool portal in new[] { false, true })
                foreach (int direction in new[] { -1, 1 })
                foreach (SlopeType slope in new[] { SlopeType.None, SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
                {
                    typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime", NativeWorld.Flags).SetValue(game, TimeSpan.FromTicks(clock));
                    var screens = Enumerable.Range(0, 3).Select(i => {
                        var rect = new Rectangle(0, 320 - 360 * i, 480, 40);
                        var blocks = new List<IBlock> { snow ? (IBlock)new SnowBlock(rect) : new BoxBlock(rect) };
                        if (slope != SlopeType.None) blocks.Add(new SlopeBlock(new Rectangle(200, 260 - 360 * i, 90, 60), slope));
                        if (wind) blocks.Add(new NoWindBlock(new Rectangle(40, 180 - 360 * i, 60, 140)));
                        return new LevelScreen(i, blocks.ToArray(), new LevelScreen.Graphics(), wind,
                            portal ? new[] { new TeleportLink(1), new TeleportLink(3) } : new TeleportLink[0], 8, null);
                    }).ToArray();
                    typeof(LevelManager).GetField("m_screens", NativeWorld.Flags).SetValue(null, screens);
                    typeof(LevelManager).GetField("_total_screens", NativeWorld.Flags).SetValue(null, 3);
                    typeof(Camera).GetField("_current_screen", NativeWorld.Flags).SetValue(null, 1);
                    var body = new BodyComp(new Vector2(portal ? (direction < 0 ? -12 : 474) : 140, -166), 18, 26)
                        { Velocity = new Vector2(direction * 3.5f, -8.742858f) };
                    NativePlayer.Set(body, "_is_on_ground", true);
                    var behaviours = NativePlayer.Field<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours");
                    foreach (var b in behaviours.ToArray())
                        if (b.GetType().Name == "WaterParticleSpawningBehaviour" || b.GetType().Name == "PlayBumpSFXBehaviour") behaviours.Remove(b);
                    var copiedMarkers = vertical == null ? null : (bool[])vertical.Clone();
                    var model = new NativePlayer(screens, NativePlayer.Capture(body), false, game.TargetElapsedTime.TotalSeconds, copiedMarkers, profiles) { ControlsEnabled = false };
                    // The provider must own its snapshot, not the caller's array.
                    if (copiedMarkers != null) Array.Clear(copiedMarkers, 0, copiedMarkers.Length);
                    var registry = new SimulationRegistry();
                    using (registry.Register(model.Provider()))
                    using (var session = registry.Open(new SimulationSeed(new SimulationPose { Position = body.Position, Velocity = body.Velocity,
                        Grounded = true, Width = 18, Height = 26, Screen = 1 }, startTick, 1.0 / 60, NativeWind.AuditedGameSha256, new byte[0], new[] { NativePlayer.Requirement })))
                    {
                        var state = session.Initial;
                        for (int t = 0; t < 100; t++)
                        {
                            var stats = Activator.CreateInstance(statsType); statsType.GetField("_ticks", NativeWorld.Flags).SetValue(stats, startTick + 1 + t);
                            managerType.GetField("m_all_time_stats", NativeWorld.Flags).SetValue(manager, stats);
                            typeof(BodyComp).GetMethod("UpdateInternal", NativeWorld.Flags).Invoke(body, new object[] { 1f / 60 });
                            Camera.UpdateCameraWithVelocity(body.GetHitbox().Center, body.Velocity);
                            var result = session.Step(state, new SimulationInput()); state = result.State;
                            teleports += result.Events.Count(e => e.Kind.StartsWith("teleport"));
                            verticalUp += result.Events.Count(e => e.Kind == "wind-vertical" && e.Value < 0);
                            verticalDown += result.Events.Count(e => e.Kind == "wind-vertical" && e.Value > 0);
                            horizontal += result.Events.Count(e => e.Kind == "wind" && e.Value != 0);
                            Check(body.Position == state.Pose.Position && body.Velocity == state.Pose.Velocity && Camera.CurrentScreen == state.Pose.Screen,
                                "World parity wind=" + wind + " vertical=" + (vertical != null) + " snow=" + snow + " portal=" + portal + " slope=" + slope + " t=" + t + " native=" + body.Position + "/" + body.Velocity +
                                " model=" + state.Pose.Position + "/" + state.Pose.Velocity + " camera=" + Camera.CurrentScreen + "/" + state.Pose.Screen);
                            ticks++;
                        }
                    }
                }
                Check(teleports > 0, "Teleport fixture never teleported");
                if (vertical != null && vertical.Any(v => v)) Check(verticalUp > 0 && verticalDown > 0 && horizontal > 0, "Vertical Wind fixture missed an axis/sign");
                Console.WriteLine("[OK] " + (vertical == null ? "Native" : "Patched Vertical Wind") + " world: " + ticks + " exact ticks, " + teleports + " teleports; wind clocks/latch, NoWind, slopes, camera; vertical up/down=" + verticalUp + "/" + verticalDown);
            }
            finally { gameField.SetValue(null, oldGame); managerField.SetValue(null, oldManager); }
        }
    }
}
