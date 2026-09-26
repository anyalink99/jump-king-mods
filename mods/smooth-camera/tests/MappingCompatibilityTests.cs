using System;
using System.IO;
using System.Reflection;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void MappingSceneFixture(Assembly mapping, Type entry, GraphicsDevice device, RenderTarget2D target, JumpGame jump, string output)
    {
        string root = Path.Combine(output, "mapping-scene-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "props", "mega-mapping-expansion"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "scene.xml"),
            "<MegaMapping version='1'><Options advancedLighting='true' ambientIntensity='0.5'/>" +
            "<VectorAssets><Asset id='marker' width='12' height='12' pixelSnap='true'><Shape type='rect' width='12' height='12' fill='#FF00FF'/></Asset></VectorAssets>" +
            "<Nodes><Node id='lower' asset='marker' screen='1' x='100' y='100'/><Node id='upper' asset='marker' screen='2' x='100' y='300'/></Nodes>" +
            "<Waters><Water id='mirror' screen='1' x='80' y='120' width='40' height='40' compositeReflection='true' sceneReflectionOpacity='1' reflectionScaleY='1' ripple='0'/></Waters></MegaMapping>");
        var loaded = mapping.GetType("MegaMappingExpansion.LoadedScene").GetMethod("Load", Flags).Invoke(null, new object[] { root, 2 });
        var type = mapping.GetType("MegaMappingExpansion.SceneHost");
        var host = (IDisposable)Activator.CreateInstance(type, Flags, null, new object[] { loaded, root }, null);
        var state = mapping.GetType("MegaMappingExpansion.MappingState");
        var data = type.GetProperty("SceneData", Flags).GetValue(host, null);
        var options = type.GetProperty("Options", Flags).GetValue(host, null);
        using (host)
        using ((IDisposable)entry.GetMethod("RegisterCameraComposition", Flags).Invoke(null, null))
        {
            type.GetMethod("Activate", Flags).Invoke(host, null); state.GetMethod("Commit", Flags).Invoke(null, new[] { data });
            RenderFrame(device, target, jump);
            var pixels = Read(target);
            Check(!Renderer.Blocked && pixels[100 * 480 + 10].R > 55 && pixels[100 * 480 + 10].R < 85
                && pixels[250 * 480 + 10].B > 55 && pixels[250 * 480 + 10].B < 85, "Mapping lighting processes both screen targets before smooth composition");
            Check(pixels[120 * 480 + 100].R > 100 && pixels[280 * 480 + 100].R > 100,
                "Mapping world props remain visible on both sides of the moving seam");
            Check(pixels[316 * 480 + 100].R > 30, "Mapping water reflects the world marker from its own logical screen");
            Check(pixels[10 * 480 + 10] == Color.Magenta && pixels[25 * 480 + 310] == Color.Cyan,
                "Scene lighting and reflections exclude stationary HUD and pause UI");
            Check(!JKRuntime.FrameComposition.InScreenPass && Camera.CurrentScreen == 0, "Composition restores borrowed screen context");
            Save(target, Path.Combine(output, "mapping-camera-scene.png"));
            Hooks.Uninstall(); Hooks.Install(); Renderer.InvalidateCompatibility();
            RenderFrame(device, target, jump);
            var reversePixels = Read(target);
            Check(reversePixels[120 * 480 + 100] == pixels[120 * 480 + 100]
                && reversePixels[316 * 480 + 100] == pixels[316 * 480 + 100],
                "Mapping-first and camera-first hooks produce the same lit props and water reflections");
            using (var outer = new JKRuntime.FrameComposition.ScreenPass(target))
            {
                using (var inner = new JKRuntime.FrameComposition.ScreenPass(target))
                {
                    bool rejected = false; try { outer.Dispose(); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && JKRuntime.FrameComposition.InScreenPass, "Nested passes reject out-of-order release even for the same target");
                }
                try { using (var inner = new JKRuntime.FrameComposition.ScreenPass(target)) throw new InvalidOperationException("screen fixture fault"); }
                catch (InvalidOperationException) { }
                Check(JKRuntime.FrameComposition.InScreenPass, "Throwing nested pass restores its outer scope");
            }
            Check(!JKRuntime.FrameComposition.InScreenPass, "All borrowed screen contexts release after faults");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 120; i++) RenderFrame(device, target, jump);
            var measured = Read(target); timer.Stop();
            Console.WriteLine("[COST] Mapping + Smooth Camera, two screens/light/reflection/HUD: " + (timer.Elapsed.TotalMilliseconds / 120).ToString("F3") + " ms/frame including GPU readback fence");
            var mappingSettings = mapping.GetType("MegaMappingExpansion.MappingSettings");
            var mappingStore = mappingSettings.GetField("store", Flags).GetValue(null);
            var setEnabled = mappingStore.GetType().GetMethod("Set", Flags);
            setEnabled.Invoke(mappingStore, new object[] { false });
            timer.Restart();
            for (int i = 0; i < 120; i++) RenderFrame(device, target, jump);
            measured = Read(target); timer.Stop();
            Console.WriteLine("[COST] Smooth Camera only, same two screens/HUD: " + (timer.Elapsed.TotalMilliseconds / 120).ToString("F3") + " ms/frame including GPU readback fence");
            setEnabled.Invoke(mappingStore, new object[] { true });
            Settings.Current.Smooth = false;
            timer.Restart();
            for (int i = 0; i < 120; i++) RenderFrame(device, target, jump);
            measured = Read(target); timer.Stop();
            Console.WriteLine("[COST] Mapping only, native screen/light/reflection/HUD: " + (timer.Elapsed.TotalMilliseconds / 120).ToString("F3") + " ms/frame including GPU readback fence");
            Settings.Current.Smooth = true;
            options.GetType().GetProperty("Mirror").SetValue(options, "both", null);
            options.GetType().GetProperty("Tint").SetValue(options, "#80FF80", null);
            options.GetType().GetProperty("TintOpacity").SetValue(options, 1f, null);
            state.GetMethod("Commit", Flags).Invoke(null, new[] { data });
            using (var final = new RenderTarget2D(device, 480, 360))
            {
                type.GetField("previewAllowed", Flags).SetValue(host, true);
                type.GetField("captureRequested", Flags).SetValue(host, true);
                Renderer.BeforeGameDraw(); device.SetRenderTarget(target); Game1.instance.StartBatch(); jump.Draw(); Game1.instance.EndBatch();
                device.SetRenderTarget(final); device.Clear(Color.Yellow);
                typeof(Game1).GetMethod("DrawRenderTarget", Flags).Invoke(Game1.instance, null); device.SetRenderTarget(null);
                var presented = Read(final); Color hud = presented[349 * 480 + 469];
                Check(hud.R >= 126 && hud.R <= 129 && hud.G == 0 && hud.B >= 126 && hud.B <= 129,
                    "One final presentation applies Mapping mirror and tint to the assembled world and HUD exactly once");
                Save(final, Path.Combine(output, "mapping-camera-mirror-tint.png"));
                string[] captures = Directory.GetFiles(Path.Combine(directory, "preview"), "frame-*.png");
                Check(captures.Length == 1 && !JKRuntime.FrameComposition.CaptureRequested,
                    "Explicit full preview captures the assembled camera exactly once");
                using (var capture = new System.Drawing.Bitmap(captures[0]))
                {
                    var hudPixel = capture.GetPixel(10, 10);
                    Check(capture.Width == 480 && capture.Height == 360 && hudPixel.R == 255 && hudPixel.B == 255,
                        "Full preview retains logical resolution and stationary UI before final tint/mirror");
                }
            }
        }
        state.GetMethod("Reset", Flags).Invoke(null, null);
    }

    private static void MappingToggleFixture(GraphicsDevice device, RenderTarget2D target, JumpGame jump, string output, string gameDir)
    {
        // The previous fixture ends during the animated return from Focus.
        // Test compositor toggles from settled automatic framing.
        CameraControls.Gestures.Reset(); Renderer.View.Reset();
        var mapping = Assembly.LoadFrom(mappingImplementation);
        var hooks = mapping.GetType("MegaMappingExpansion.NativeHooks", true);
        var settings = mapping.GetType("MegaMappingExpansion.MappingSettings", true);
        var storeType = mapping.GetType("MegaMappingExpansion.MappingSettingsStore", true);
        var storeField = settings.GetField("store", Flags);
        object oldStore = storeField.GetValue(null);
        string path = Path.Combine(output, "mapping-toggle-" + Guid.NewGuid().ToString("N") + ".xml");
        var store = Activator.CreateInstance(storeType, Flags, null, new object[] { path }, null);
        storeField.SetValue(null, store);
        var entry = mapping.GetType("MegaMappingExpansion.ModEntry", true);
        var toggle = (JKRuntime.UI.SettingToggle)entry.GetMethod("MainMenuEnabled").Invoke(null, new object[] { null, new JumpKing.PauseMenu.GuiFormat() });
        var change = typeof(JKRuntime.UI.SettingToggle).GetMethod("OnToggle", Flags);
        hooks.GetMethod("Install", Flags).Invoke(null, null);
        Renderer.InvalidateCompatibility();
        try
        {
            RenderFrame(device, target, jump);
            Check(!Renderer.Blocked && Read(target)[100 * 480 + 10] == Color.DarkRed, "Enabled Mapping without an active scene preserves smooth camera");
            toggle.OverrideToggle(false); change.Invoke(toggle, null);
            RenderFrame(device, target, jump);
            Check(!Renderer.Blocked && Read(target)[100 * 480 + 10] == Color.DarkRed, "Actual Mapping checkbox immediately releases Smooth Camera while hooks remain installed");
            Check(HarmonyLib.Harmony.GetPatchInfo(typeof(LevelScreen).GetMethod("DrawForeground")).Owners.Contains("mega-mapping-expansion.render"), "Mapping save/lifecycle hooks are retained while disabled");
            var reloaded = Activator.CreateInstance(storeType, Flags, null, new object[] { path }, null);
            Check(!(bool)storeType.GetProperty("Enabled", Flags).GetValue(reloaded, null), "Mapping checkbox persists off");
            using (var content = new ContentManager(Game1.instance.Services, gameDir))
            {
                Game1.instance.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                var sheet = content.Load<Texture2D>("Content/gui/checkbox");
                var uncheckedSprite = Sprite.CreateSprite(sheet, new Rectangle(0, 0, sheet.Width / 2, sheet.Height));
                var checkedSprite = Sprite.CreateSprite(sheet, new Rectangle(sheet.Width / 2, 0, sheet.Width / 2, sheet.Height));
                uncheckedSprite.center = checkedSprite.center = new Vector2(0, .5f);
                Game1.instance.contentManager.gui.CheckBoxFalse = uncheckedSprite;
                Game1.instance.contentManager.gui.CheckBoxTrue = checkedSprite;
                var cameraToggle = new CameraOption();
                var horizontalToggle = ModEntry.MainHorizontal(null, new JumpKing.PauseMenu.GuiFormat());
                cameraToggle.GetSize(); horizontalToggle.GetSize(); toggle.GetSize();
                device.SetRenderTarget(target); device.Clear(new Color(15, 18, 24)); Game1.instance.StartBatch();
                cameraToggle.Draw(24, 110, false); horizontalToggle.Draw(24, 145, false); toggle.Draw(24, 180, false);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                Save(target, Path.Combine(output, "native-checkboxes.png"));
                Check(cameraToggle.toggle && !toggle.toggle, "Both settings render native checkboxes with their current values");
                Check(!horizontalToggle.toggle, "Horizontal setting is a separate unchecked native checkbox");
            }
            toggle.OverrideToggle(true); change.Invoke(toggle, null);
            RenderFrame(device, target, jump);
            Check(!Renderer.Blocked, "Re-enabling scene-free Mapping keeps camera cooperation");
            MappingSceneFixture(mapping, entry, device, target, jump, output);
        }
        finally
        {
            hooks.GetMethod("Uninstall", Flags).Invoke(null, null);
            Renderer.InvalidateCompatibility();
            storeField.SetValue(null, oldStore);
        }
    }
}
