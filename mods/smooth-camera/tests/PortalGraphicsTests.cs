using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.GameManager;
using JumpKing.BodyCompBehaviours;
using JumpKing.MiscEntities.OldMan;
using JumpKing.MiscEntities.Merchant;
using JumpKing.Util.DrawBT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmoothCamera;

internal static partial class CameraTests
{
    private static bool SkipNpcText() { return false; }
    private static void NativeNpcPixels(GraphicsDevice device, RenderTarget2D target, LevelScreen[] screens, Texture2D white, string output)
    {
        var textIsolation = new Harmony("smooth-camera.npc-fixture");
        var actors = new ISpriteEntity[2];
        for (int i = 0; i < 2; i++)
        {
            Type type = typeof(Game1).Assembly.GetType(i == 0 ? "JumpKing.MiscEntities.OldManEntity" : "JumpKing.MiscEntities.Merchant.MerchantEntity", true);
            actors[i] = (ISpriteEntity)FormatterServices.GetUninitializedObject(type);
            var settings = new OldManSettings { home_screen = 2 };
            type.GetField("m_settings", Flags).SetValue(actors[i], i == 0 ? (object)settings : new MerchantSettings { settings = settings });
            actors[i].Position = new Vector2(70 + i * 30, -40);
            actors[i].SetSprite(Sprite.CreateSprite(white, new Rectangle(0, 0, 10, 10)));
            foreach (var method in type.GetMethods(Flags))
                if (method.Name == "DrawText") textIsolation.Patch(method, prefix: new HarmonyMethod(typeof(CameraTests), "SkipNpcText"));
        }
        try
        {
            // Real native Draw callers are JIT-compiled before installing camera
            // hooks, as happens when enabling the mod during an existing run.
            Hooks.Uninstall(); RenderContext.NativeScreen.SetValue(null, 0);
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            foreach (var actor in actors) actor.Draw();
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            Hooks.Install();
            using (var compositor = new ScreenCompositor())
            {
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(180, 2, index => { screens[index].Draw(); foreach (var actor in actors) actor.Draw(); screens[index].DrawForeground(); });
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var pixels = Read(target);
                foreach (var actor in actors)
                {
                    Check(pixels[145 * 480 + (int)actor.Position.X + 4] == Color.White, "Actual prewarmed native " + actor.GetType().Name + " appears on adjacent screen");
                    Near(actor.Position.Y, -40, "NPC position is not changed by neighbor rendering");
                }
                Check(Camera.CurrentScreenIndex1 == 1, "Native screen remains physical after adjacent NPC draw");
                Save(target, Path.Combine(output, "native-adjacent-npcs.png"));
            }
        }
        finally { textIsolation.UnpatchAll("smooth-camera.npc-fixture"); }
    }

    private static void PortalPixels(GraphicsDevice device, RenderTarget2D target, Texture2D white, string output)
    {
        using (var blue = Solid(device, Color.DarkBlue))
        using (var red = Solid(device, Color.DarkRed))
        using (var green = Solid(device, Color.DarkGreen))
        using (var purple = Solid(device, Color.Purple))
        using (var compositor = new ScreenCompositor())
        using (var full = new RenderTarget2D(device, 1920, 1440))
        {
            var screens = new[] { MakeScreen(0, blue, null, null), MakeScreen(1, red, null, null), MakeScreen(2, green, null, null), MakeScreen(3, purple, null, null) };
            foreach (float x in new[] { -180f, 180f })
            {
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(180, 4, index => screens[index].Draw(), false, -1, x, 2, 2, 0);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var pixels = Read(target);
                for (int y = 0; y < 360; y++) for (int column = 0; column < 480; column++)
                {
                    bool destination = x > 0 ? column < 180 : column >= 300;
                    Color expected = destination ? (y < 180 ? Color.Purple : Color.DarkGreen) : (y < 180 ? Color.DarkRed : Color.DarkBlue);
                    if (pixels[y * 480 + column] != expected) throw new Exception("Portal seam/vertical neighbor pixel mismatch " + x + ":" + column + "," + y);
                }
                Check(true, "Both axes compose portal destination and its vertical neighbor without gaps");
                Save(target, Path.Combine(output, x < 0 ? "portal-right.png" : "portal-left.png"));
            }
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            compositor.Draw(180, 4, index => screens[index].Draw(), true, 100, -180, 2, 2, 0);
            Game1.spriteBatch.Draw(white, new Rectangle(8, 8, 8, 8), Color.Magenta);
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            int captures = compositor.SceneRenders;
            compositor.Present(new Rectangle(0, 0, 480, 360), target, 180, -180); // Existing backbuffer is valid.
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            compositor.Draw(180, 4, index => { throw new Exception("Unexpected scene refresh"); }, true, 100, -180.25f, 2, 2, 0);
            Game1.spriteBatch.Draw(white, new Rectangle(8, 8, 8, 8), Color.Magenta);
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            Check(compositor.SceneRenders == captures, "Horizontal subframes reuse captured world");
            device.SetRenderTarget(full); device.Clear(Color.Black);
            compositor.Present(new Rectangle(0, 0, 1920, 1440), target, 180, -180.25f);
            device.SetRenderTarget(null);
            var enlarged = Read(full);
            Check(enlarged[1000 * 1920 + 1198] == Color.DarkBlue && enlarged[1000 * 1920 + 1199] == Color.DarkGreen, "Quarter-pixel horizontal movement resolves to one output pixel at 4x");
            Check(enlarged[36 * 1920 + 36] == Color.Magenta, "Horizontal presentation keeps HUD fixed");
            NativePortalCrossing(device, target, screens, white, output);
            PortalWallPixels(device, target, screens, output);
        }
        Check(!RenderContext.Active && Camera.CurrentScreen == 0, "Portal rendering restores native camera scope");
    }

    private static void NativePortalCrossing(GraphicsDevice device, RenderTarget2D target, LevelScreen[] screens, Texture2D white, string output)
    {
        object previousScreens = Renderer.Screens.GetValue(null);
        object previousCount = typeof(LevelManager).GetField("_total_screens", Flags).GetValue(null);
        PlayerEntity previousPlayer = GameLoop.m_player;
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        player.m_body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        typeof(BodyComp).GetField("m_width", Flags).SetValue(player.m_body, 18);
        typeof(BodyComp).GetField("m_height", Flags).SetValue(player.m_body, 26);
        typeof(PlayerEntity).GetField("m_sprite", Flags).SetValue(player, Sprite.CreateCenteredSprite(white, new Rectangle(0, 0, 10, 10)));
        typeof(LevelScreen).GetField("m_teleport", Flags).SetValue(screens[0], new[] { new TeleportLink(3) });
        try
        {
            GameLoop.m_player = player; Renderer.Screens.SetValue(null, screens);
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 4);
            using (var compositor = new ScreenCompositor())
            foreach (bool right in new[] { true, false })
            {
                RenderContext.NativeScreen.SetValue(null, 0);
                player.m_body.Position = new Vector2(right ? 472 : -10, 100);
                float horizontal = right ? -180 : 180;
                Action<int> draw = index => { screens[index].Draw(); Renderer.EntityDraw(player); };
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(0, 4, draw, false, -1, horizontal, 2, 2, 0);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var before = Read(target);
                new HandlePlayerTeleportBehaviour().ExecuteBehaviour(new BehaviourContext(player.m_body));
                Check(Camera.CurrentScreen == 2, "Actual native body teleport reaches destination");
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(720, 4, draw, false, -1, horizontal + (right ? 480 : -480), right ? 0 : -1, right ? -1 : 0, 2);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var after = Read(target);
                for (int i = 0; i < before.Length; i++) if (before[i] != after[i]) throw new Exception("Native teleport visual discontinuity, right=" + right + " pixel=" + i);
                Check(true, "Every world/player pixel is continuous through native " + (right ? "right" : "left") + " teleport");
                Save(target, Path.Combine(output, right ? "native-portal-crossing-right.png" : "native-portal-crossing-left.png"));
            }
        }
        finally
        {
            Renderer.Screens.SetValue(null, previousScreens); GameLoop.m_player = previousPlayer;
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, previousCount); RenderContext.NativeScreen.SetValue(null, 0);
        }
    }
}
