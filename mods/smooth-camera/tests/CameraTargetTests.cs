using System;
using System.IO;
using System.Runtime.Serialization;
using JKRuntime;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SmoothCamera;
using MapPolicy = SmoothCamera.MapPolicy;

internal static partial class CameraTests
{
    private static void CameraTargetTests()
    {
        var oldPlayer = GameLoop.m_player;
        var oldScreens = Renderer.Screens.GetValue(null);
        var oldScreen = RenderContext.NativeScreen.GetValue(null);
        var countField = typeof(LevelManager).GetField("_total_screens", Flags);
        var oldCount = countField.GetValue(null);
        var oldSettings = Settings.Current;
        var oldRules = MapPolicy.Current;
        var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        typeof(BodyComp).GetField("m_width", Flags).SetValue(body, 18);
        typeof(BodyComp).GetField("m_height", Flags).SetValue(body, 26);
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        player.m_body = body; body.Position = new Vector2(231, 305);
        try
        {
            GameLoop.m_player = player;
            Renderer.Screens.SetValue(null, new LevelScreen[24]); countField.SetValue(null, 24);
            RenderContext.NativeScreen.SetValue(null, 0);
            Settings.Current = new CameraSettings { Smooth = false };
            // Restless Knight's two smooth ranges, with native screens between them
            MapPolicy.Current = MapRules.Parse(new StringReader("<SmoothCamera><Screens from='1' to='8' mode='smooth'/><Screens from='11' to='24' mode='smooth'/></SmoothCamera>"), 24, true, true);
            Renderer.Start();
            using (var scope = new RuntimeScope())
            {
                var camera = CameraTargets.Acquire("test.viewer", scope);
                camera.Publish(new CameraTarget(new Vector2(240, 318), Vector2.Zero, 0, false));
                TargetFrame();
                Near(Renderer.View.Y, 0, "Replay starts on screen one");
                for (int i = 1; i <= 400; i++)
                {
                    float y = 318 - i * 4;
                    int screen = Math.Max(0, (int)Math.Ceiling(-y / 360));
                    camera.Publish(new CameraTarget(new Vector2(240, y), new Vector2(0, -240), screen, false));
                    TargetFrame();
                }
                Check(Renderer.View.Y > 1000 && Renderer.Decision.Active, "Camera follows replay across smooth screens while physical player stays at spawn");
                Near(body.Position.Y, 305, "Camera override never moves the live body");
                Check((int)RenderContext.NativeScreen.GetValue(null) == 0, "Presentation targeting doesn't assign native camera state");
                camera.Publish(new CameraTarget(new Vector2(240, -1282), Vector2.Zero, 4, true));
                TargetFrame(); float frozen = Renderer.View.Y;
                for (int i = 0; i < 12; i++) TargetFrame();
                Near(Renderer.View.Y, frozen, "Replay pause freezes camera spring even without native pause");
                camera.Publish(new CameraTarget(new Vector2(240, -1322), Vector2.Zero, 4, true), true);
                TargetFrame();
                Near(Renderer.View.Y, 1502, "Short paused seek reframes immediately without a distance heuristic");
                camera.Publish(new CameraTarget(new Vector2(199, -2522), Vector2.Zero, 8, false));
                TargetFrame();
                Check(!Renderer.Decision.Active, "Recorded screen nine exits smooth mode despite live screen one");
                camera.Publish(new CameraTarget(new Vector2(240, -3315), Vector2.Zero, 10, false));
                TargetFrame();
                Check(Renderer.Decision.Active && Renderer.View.Y >= 3600, "Recorded screen eleven restores the correct smooth range");
                MapPolicy.Current = MapRules.Parse(new StringReader("<SmoothCamera><Zone id='patch' screen='2' x='50' y='40' width='150' height='100' mode='smooth'/></SmoothCamera>"), 24, true, true);
                camera.Publish(new CameraTarget(new Vector2(80, -300), Vector2.Zero, 1, true), true);
                TargetFrame();
                Check(Renderer.Decision.Active, "Zone policy uses the replay's local position");
                camera.Publish(new CameraTarget(new Vector2(300, -300), Vector2.Zero, 1, true), true);
                TargetFrame();
                Check(!Renderer.Decision.Active, "Leaving a replay zone doesn't consult the live hitbox");
            }
            MapPolicy.Current = MapRules.Parse(null, 24, false, false); Settings.Current.Smooth = true;
            TargetFrame();
            Check(!Renderer.TargetOverridden, "Closing playback restores live tracking");
            Near(Renderer.View.Y, 0, "Live spawn framing returns after playback closes");
            using (var next = new RuntimeScope())
            {
                CameraTargets.Acquire("test.next", next).Publish(new CameraTarget(new Vector2(240, -2000), Vector2.Zero, 6, true));
                TargetFrame(); Near(Renderer.View.Y, 2180, "A new paused session can't inherit the old camera spring");
            }
        }
        finally
        {
            Renderer.Stop(); PresentationClock.Release();
            GameLoop.m_player = oldPlayer; Renderer.Screens.SetValue(null, oldScreens);
            RenderContext.NativeScreen.SetValue(null, oldScreen); countField.SetValue(null, oldCount);
            Settings.Current = oldSettings; MapPolicy.Current = oldRules;
        }
        Console.WriteLine("[OK] Camera targets: playback tracking, paused seek, map ranges/zones and live restoration");
    }

    private static void TargetFrame()
    {
        typeof(Renderer).GetField("drawDelta", Flags).SetValue(null, 1f / 60);
        Renderer.BeginFrame();
    }
}
