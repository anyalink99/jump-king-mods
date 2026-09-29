using System;
using System.Collections;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void CustomWindParity(Assembly assembly)
        {
            var api = assembly.GetType("CustomWindSwitch.CustomWindSwitchApi", true);
            var singleton = api.GetField("_instance", NativeWorld.Flags); var previous = singleton.GetValue(null);
            var profileType = assembly.GetType("CustomWindSwitch.WindParams", true);
            var gameField = typeof(Game1).GetField("_instance", NativeWorld.Flags); object oldGame = gameField.GetValue(null);
            var managerType = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var statsType = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.PlayerStats", true);
            var managerField = managerType.GetField("instance", NativeWorld.Flags); object oldManager = managerField.GetValue(null);
            var allTime = managerType.GetField("m_all_time_stats", NativeWorld.Flags);
            var raw = typeof(WindManager).GetProperty("CurrentVelocityRaw", NativeWorld.Flags).GetGetMethod(true);
            try
            {
                var instance = Activator.CreateInstance(api); singleton.SetValue(null, instance);
                var dict = (IDictionary)api.GetField("_freqs", NativeWorld.Flags).GetValue(instance);
                var game = (Game1)Empty(typeof(Game1)); GC.SuppressFinalize(game); gameField.SetValue(null, game);
                var manager = Empty(managerType); managerField.SetValue(null, manager);
                managerType.GetField("m_snapshot", NativeWorld.Flags).SetValue(manager, Activator.CreateInstance(statsType));
                int count = 0;
                foreach (float[] config in new[] { new[] { .35f, 1.2f, .4f, 1.8f }, new[] { 3.25f, 2.5f, 2f, .25f } })
                {
                    var value = Activator.CreateInstance(profileType);
                    string[] names = { "LeftWindTime", "RightWindTime", "LeftWindIntensity", "RightWindIntensity" };
                    for (int i = 0; i < 4; i++) profileType.GetField(names[i]).SetValue(value, config[i]);
                    dict[1] = value;
                    var captured = CustomWindProfile.Capture(assembly, 3);
                    foreach (long clock in new long[] { 166667, 170000 })
                    foreach (float intensity in new[] { 0f, 1f, 8f })
                    foreach (bool? direction in new bool?[] { null, false, true })
                    {
                        typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime", NativeWorld.Flags).SetValue(game, TimeSpan.FromTicks(clock));
                        var screen = new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), true, new TeleportLink[0], intensity, direction);
                        typeof(LevelManager).GetField("m_screens", NativeWorld.Flags).SetValue(null, new[] { screen, screen, screen });
                        typeof(LevelManager).GetField("_total_screens", NativeWorld.Flags).SetValue(null, 3);
                        typeof(Camera).GetField("_current_screen", NativeWorld.Flags).SetValue(null, 1);
                        for (int tick = 0; tick < 12000; tick += 7)
                        {
                            var stats = Activator.CreateInstance(statsType); statsType.GetField("_ticks", NativeWorld.Flags).SetValue(stats, tick);
                            allTime.SetValue(manager, stats);
                            float native = (float)raw.Invoke(null, null), model = captured[1].Velocity(tick, game.TargetElapsedTime.TotalSeconds, screen);
                            Check(native == model, "Custom Wind Switch mismatch tick=" + tick + " native=" + native + " model=" + model); count++;
                        }
                    }
                    WorldParity(null, captured);
                    dict.Clear(); Check(captured[1].Enabled, "Captured wind profile aliases the live dictionary");
                }
                var invalid = Activator.CreateInstance(profileType); dict[1] = invalid;
                bool rejected = false; try { CustomWindProfile.Capture(assembly, 3); } catch (NotSupportedException) { rejected = true; }
                Check(rejected, "Zero wind cycle accepted");
                Console.WriteLine("[OK] Custom Wind Switch: " + count + " exact waveform samples + 16000 world ticks; custom phases/intensities, fixed directions and snapshot isolation");
            }
            finally { singleton.SetValue(null, previous); gameField.SetValue(null, oldGame); managerField.SetValue(null, oldManager); }
        }
    }
}
