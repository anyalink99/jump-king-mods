using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmoothCamera;

internal static partial class CameraTests
{
    private static FieldInfo legacyCamera, legacyAtlas, legacyScreen, legacyAnchor;
    private static int backdropPresents;
    private static void LegacyBackdropClear(GraphicsDevice __instance, ref Color __0)
    {
        if (__0 != Color.Black) return;
        var targets = __instance.GetRenderTargets();
        if (targets.Length != 1) return;
        var compositor = legacyCamera.GetValue(null);
        if (ReferenceEquals(targets[0].RenderTarget, legacyAtlas.GetValue(compositor))
            || ReferenceEquals(targets[0].RenderTarget, legacyScreen.GetValue(compositor))) __0 = Color.Transparent;
    }
    private static void LegacyBackdropPresent(Rectangle destination)
    {
        backdropPresents++;
        Game1.spriteBatch.Begin();
        Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, destination, Color.Teal);
        Game1.spriteBatch.End();
    }
    private static void LegacyBackdropPixels(GraphicsDevice device, RenderTarget2D target, Texture2D transparent, string output)
    {
        // match the reflection and hook contract already shipped in Total Eclipse
        legacyCamera = typeof(Renderer).GetField("compositor", Flags);
        legacyAtlas = typeof(ScreenCompositor).GetField("atlas", Flags);
        legacyScreen = typeof(ScreenCompositor).GetField("screenTarget", Flags);
        legacyAnchor = typeof(ScreenCompositor).GetField("cachedAnchor", Flags);
        Check(legacyCamera != null && legacyAtlas != null && legacyScreen != null && legacyAnchor != null,
            "Shipped video backdrop can attach to its camera contract");
        var compositor = (ScreenCompositor)legacyCamera.GetValue(null);
        var hooks = new Harmony("camera-tests.legacy-backdrop");
        try
        {
            hooks.Patch(typeof(GraphicsDevice).GetMethod("Clear", new[] { typeof(Color) }),
                prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("LegacyBackdropClear", Flags)));
            hooks.Patch(typeof(ScreenCompositor).GetMethod("Present", Flags),
                prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("LegacyBackdropPresent", Flags)));
            device.SetRenderTarget(target); device.Clear(Color.Black); device.SetRenderTarget(null);
            Check(Read(target)[0] == Color.Black, "Backdrop adapter leaves unrelated render targets opaque");
            var images = new[] { new ScreenImage(0, Vector2.Zero), new ScreenImage(1, new Vector2(480, 360)) };
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            compositor.DrawPlan(images, new Vector2(-240, 0), delegate {
                Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(0, 170, 480, 10), Color.White);
            }, true, 1, false, 17);
            Game1.instance.EndBatch();
            Check((int)legacyAnchor.GetValue(compositor) == 17, "Backdrop sees the current native coordinate chart");
            backdropPresents = 0;
            compositor.Present(new Rectangle(0, 0, 480, 360), transparent, 0, -240);
            device.SetRenderTarget(null);
            var pixels = Read(target);
            Console.WriteLine("[BACKDROP] presents=" + backdropPresents + " pixels=" + pixels[100 * 480 + 100] + " / " + pixels[100 * 480 + 400]);
            Check(pixels[100 * 480 + 100] == Color.Teal && pixels[100 * 480 + 400] == Color.Teal,
                "Video backdrop remains visible beneath both screen captures");
            Check(pixels[175 * 480 + 100] == Color.White && pixels[175 * 480 + 400] == Color.White,
                "Opaque world art still covers the video backdrop");
            Save(target, Path.Combine(output, "legacy-video-backdrop.png"));
            device.SetRenderTarget(target); device.Clear(Color.Black);
            compositor.PresentPreview(new Rectangle(0, 0, 480, 360), transparent, 0, -240);
            device.SetRenderTarget(null);
            Check(backdropPresents == 1 && Read(target)[100 * 480 + 100] == Color.Black,
                "Settings preview does not invoke the active map's video hook");
            Check(legacyAtlas.GetValue(compositor) == null && legacyScreen.GetValue(compositor) == null,
                "Borrowed capture targets are released after drawing");
        }
        finally { device.SetRenderTarget(null); hooks.UnpatchAll(hooks.Id); compositor.Dispose(); }
    }
}
