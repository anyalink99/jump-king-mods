using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static class MeasurementDisplayTests
{
    private static readonly List<string> lines = new List<string>();
    private static bool Skip() { return false; }
    private static bool Text(string p_text, Vector2 p_position)
    {
        if (p_position != new Vector2(12, 26)) throw new Exception("Standalone label moved from the native HUD position");
        lines.Add(p_text);
        return false;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static int Main(string[] args)
    {
        try
        {
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            GC.SuppressFinalize(game);
            AccessTools.Field(typeof(Game1), "_instance").SetValue(null, game);
            game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
            game.contentManager.font = new JKContentManager.Font();
            AccessTools.Property(typeof(SettingsStore), "Current").SetValue(null, new SubframeChargeSettings(), null);
            AccessTools.Field(typeof(SettingsStore), "loaded").SetValue(null, true);
            Type pauseType = MenuCadence.Pause;
            Type achievementType = typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var pause = FormatterServices.GetUninitializedObject(pauseType);
            AccessTools.Field(pauseType, "instance").SetValue(null, pause);
            AccessTools.Field(achievementType, "instance").SetValue(null, FormatterServices.GetUninitializedObject(achievementType));
            var loop = (GameLoop)FormatterServices.GetUninitializedObject(typeof(GameLoop));
            AccessTools.Field(typeof(GameLoop), "m_pause_manager").SetValue(loop, pause);
            var draw = typeof(GameLoop).GetMethod("Draw");
            var fixture = new Harmony("subframe-charge.measurement-fixture");
            fixture.Patch(AccessTools.Method(pauseType, "Draw"), prefix: new HarmonyMethod(AccessTools.Method(typeof(MeasurementDisplayTests), "Skip")));
            fixture.Patch(AccessTools.Method(typeof(GameLoop), "DrawIngameOverlayItems"), prefix: new HarmonyMethod(AccessTools.Method(typeof(MeasurementDisplayTests), "Skip")));
            fixture.Patch(AccessTools.Method(typeof(TextHelper), "DrawString"), prefix: new HarmonyMethod(AccessTools.Method(typeof(MeasurementDisplayTests), "Text")));
            try
            {
                using (var world = new JKRuntime.RuntimeScope())
                {
                    JumpPercentIntegration.PrepareStandaloneDisplay(world);
                    draw.Invoke(loop, null);
                    Check(lines.Count == 0, "Preparation published a live HUD");
                    using (JumpPercentIntegration.ActivateDisplay())
                    {
                        JumpPercentIntegration.RecordLaunch(null, .12345, false);
                        draw.Invoke(loop, null);
                        Check(lines.Count == 1 && lines[0] == "SFC: 123.45 ms", "Native overlay did not draw the measured hold without Jump%");
                        SettingsStore.Current.ShowMeasurement = false;
                        draw.Invoke(loop, null);
                        Check(lines.Count == 1, "Show SFC off still draws");
                        SettingsStore.Current.ShowMeasurement = true;
                        AccessTools.Field(pauseType, "_paused").SetValue(pause, true);
                        draw.Invoke(loop, null);
                        Check(lines.Count == 1, "Native pause must hide SFC");
                        AccessTools.Field(pauseType, "_paused").SetValue(pause, false);
                        JumpPercentIntegration.RecordBuffered();
                        draw.Invoke(loop, null);
                        Check(lines.Count == 2 && lines[1] == "SFC: Buffered", "Standalone label lost the normal buffered status");
                    }
                    draw.Invoke(loop, null);
                    Check(lines.Count == 2, "Attempt teardown left a visible HUD");
                    using (JumpPercentIntegration.ActivateDisplay())
                    {
                        if (args.Length != 0)
                        {
                            var jumpAssembly = Assembly.LoadFrom(args[0]);
                            var entry = jumpAssembly.GetType("JumpKingLastJumpValue.JumpKingLastJumpValue", true);
                            var preferences = entry.GetProperty("Preferences");
                            var settings = Activator.CreateInstance(preferences.PropertyType);
                            preferences.SetValue(null, settings, null);
                            var enabled = settings.GetType().GetProperty("IsEnabled");
                            JumpPercentIntegration.PrepareDisplayHook();
                            Check((bool)AccessTools.Field(typeof(JumpPercentIntegration), "displayPatched").GetValue(null), "Installed Jump% display hook failed");
                            draw.Invoke(loop, null);
                            Check(lines.Count == 2, "Late Jump% discovery left a duplicate standalone label");
                            enabled.SetValue(settings, false, null);
                            draw.Invoke(loop, null);
                            Check(lines.Count == 3 && lines[2] == "SFC: Buffered", "Installed but disabled Jump% hides SFC");
                            enabled.SetValue(settings, true, null);
                            draw.Invoke(loop, null);
                            Check(lines.Count == 3, "Re-enabling Jump% left a standalone duplicate");
                            lines.RemoveAt(2);
                        }
                    }
                }
                using (JumpPercentIntegration.ActivateDisplay())
                {
                    draw.Invoke(loop, null);
                    Check(lines.Count == 2, "World teardown left a drawing hook");
                }
            }
            finally { fixture.UnpatchAll(fixture.Id); }
            Console.WriteLine("[OK] Native standalone SFC overlay: measurement, visibility, pause, scoped lifetime and late Jump% handoff");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
