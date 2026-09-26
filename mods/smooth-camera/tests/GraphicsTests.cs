using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using BehaviorTree;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.JKMemory;
using JumpKing.Level;
using JumpKing.Level.Data;
using JumpKing.Player;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SmoothCamera;

internal static partial class CameraTests
{
    private sealed class DeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }
    private sealed class KingMarker : Entity
    {
        internal int Draws, Updates;
        internal Vector2 Position = new Vector2(230, -8);
        public override void Draw()
        {
            Draws++;
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,
                Camera.TransformRect(new Rectangle((int)Position.X, (int)Position.Y, 20, 16)), Color.White);
        }
        protected override void Update(float delta) { Updates++; }
    }
    private sealed class HudMarker : Entity, IForeground
    {
        internal int Draws;
        public void ForegroundDraw()
        { Draws++; Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(8, 8, 18, 8), Color.Magenta); }
    }
    private sealed class ScreenMarker : Entity
    {
        internal int Screen;
        public override void Draw()
        {
            if (Camera.CurrentScreen != Screen) return;
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,
                Camera.TransformRect(new Rectangle(60, 180 - Screen * 360, 10, 10)), Color.Lime);
        }
    }

    private static bool SkipGameLoopDraw() { return false; }
    private static bool FixtureGameRect(Game1 __instance)
    { typeof(Game1).GetField("_game_rect", Flags).SetValue(__instance, new Rectangle(0, 0, 480, 360)); return false; }
    private static bool FakePauseDraw()
    {
        Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(300, 20, 30, 20), Color.Cyan);
        return false;
    }

    private static void GraphicsTests(string gameDir)
    {
        string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "graphics"); Directory.CreateDirectory(output);
        using (var window = new Form { ShowInTaskbar = false })
        using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360, IsFullScreen = false }))
        using (var batch = new SpriteBatch(device))
        using (var target = new RenderTarget2D(device, 480, 360))
        using (var white = new Texture2D(device, 1, 1))
        using (var lower = Solid(device, Color.DarkBlue))
        using (var upper = Solid(device, Color.DarkRed))
        using (var transparent = Solid(device, Color.Transparent))
        using (var foreground = Stripe(device))
        {
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            typeof(Game1).GetField("_instance", Flags).SetValue(null, game);
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
            typeof(Game).GetField("_services", Flags).SetValue(game, services);
            foreach (var field in typeof(Game).GetFields(Flags).Where(x => x.FieldType == typeof(IGraphicsDeviceService)))
                field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
            game.contentManager = new JKContentManager(); Game1.spriteBatch = batch;
            white.SetData(new[] { Color.White });
            var pixel = (PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture)); GC.SuppressFinalize(pixel);
            typeof(PixelTexture).GetField("_texture", Flags).SetValue(pixel, white); game.contentManager.Pixel = pixel;
            var screens = new[] { MakeScreen(0, lower, transparent, foreground), MakeScreen(1, upper, transparent, foreground) };
            Renderer.Screens.SetValue(null, screens);
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 2);
            RenderContext.NativeScreen.SetValue(null, 0); Camera.Offset = Vector2.Zero;

            using (var compositor = new ScreenCompositor())
            {
                foreach (int shift in new[] { 0, 1, 179, 180, 181, 359, 360 })
                {
                    device.SetRenderTarget(target); device.Clear(Color.Yellow); game.StartBatch();
                    compositor.Draw(shift, 2, delegate(int index)
                    {
                        screens[index].Draw();
                        batch.Draw(white, Camera.TransformRect(new Rectangle(230, -8, 20, 16)), Color.White);
                        screens[index].DrawForeground();
                    });
                    batch.Draw(white, new Rectangle(8, 8, 18, 8), Color.Magenta);
                    game.EndBatch(); device.SetRenderTarget(null);
                    var pixels = Read(target);
                    for (int y = 0; y < 360; y++)
                        Check(pixels[y * 480 + 10] == (y >= 8 && y < 16 ? Color.Magenta : (y < shift ? Color.DarkRed : Color.DarkBlue)), "Native screen layers have no seam gaps at " + shift + ", " + y);
                    if (shift == 180)
                    {
                        for (int y = 172; y < 188; y++)
                        {
                            Check(pixels[y * 480 + 235] == Color.White, "World sprite crosses the screen seam without clipping");
                            Check(pixels[y * 480 + 245] == Color.Gold, "Native foreground occludes sprite on both sides");
                        }
                        Save(target, Path.Combine(output, "native-layers.png"));
                    }
                    Check(Camera.CurrentScreen == 0 && !RenderContext.Active, "GPU pass leaves logical screen intact");
                }
                // A thrown native draw must restore the render scope, target and batch.
                device.SetRenderTarget(target); game.StartBatch();
                var originalViewport = device.Viewport; var originalScissor = device.ScissorRectangle;
                bool threw = false;
                try { compositor.Draw(180, 2, delegate { throw new InvalidOperationException("fixture draw"); }); }
                catch (InvalidOperationException) { threw = true; }
                Check(threw && !RenderContext.Active, "GPU exception releases render scope");
                Check(ReferenceEquals(device.GetRenderTargets()[0].RenderTarget, target), "GPU exception restores target");
                Check(device.Viewport.Equals(originalViewport) && device.ScissorRectangle == originalScissor, "GPU exception restores viewport/scissor");
                batch.Draw(white, new Rectangle(0, 0, 480, 360), Color.Green); game.EndBatch(); device.SetRenderTarget(null);
                Check(Read(target)[100] == Color.Green, "Sprite batch remains usable after exception");
                compositor.Dispose();
                device.SetRenderTarget(target); game.StartBatch();
                compositor.Draw(0, 2, delegate(int index) { screens[index].Draw(); });
                game.EndBatch(); device.SetRenderTarget(null);
                Check(Read(target)[100] == Color.DarkBlue, "Disposed targets can be rebuilt for a new level/device");
            }

            PresentationPixels(device, target, screens, white, output);
            PortalPixels(device, target, white, output);
            NativeNpcPixels(device, target, screens, white, output);
            NativeDrawFixture(device, target, screens, output, gameDir);
            WeatherAndScrolling(device, services, target, gameDir, output);
            RealArtPreview(device, services, target, gameDir, output);
            FocusMenuPreview(device, services, target, gameDir, output);
        }
        Game1.spriteBatch = null;
        GameLoop.m_player = null;
        typeof(Game1).GetField("_instance", Flags).SetValue(null, null);
        Console.WriteLine("[OK] Native MonoGame captures: " + output);
    }

    private static void FocusMenuPreview(GraphicsDevice device, GameServiceContainer services, RenderTarget2D target, string gameDir, string output)
    {
        var previous = JumpKing.Controller.ControllerManager.instance;
        using (var content = new ContentManager(services, gameDir))
        try
        {
            var game = Game1.instance;
            game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
            game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
            game.contentManager.font.LocationFont = content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
            var frame = content.Load<Texture2D>("Content/gui/frame"); int cell = frame.Width / 3;
            game.contentManager.gui.FrameSprites = new Sprite[3, 3];
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
                game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(frame, new Rectangle(x * cell, y * cell, cell, cell));
            var manager = (JumpKing.Controller.ControllerManager)FormatterServices.GetUninitializedObject(typeof(JumpKing.Controller.ControllerManager));
            JumpKing.Controller.ControllerManager.instance = manager;
            var keyboard = (JumpKing.Controller.IPad)Activator.CreateInstance(typeof(Game1).Assembly.GetType("JumpKing.Controller.KeyboardPad"), true);
            typeof(JumpKing.Controller.ControllerManager).GetField("m_pads", Flags).SetValue(manager,
                new List<JumpKing.Controller.PadInstance> { new JumpKing.Controller.PadInstance(keyboard) });
            var definition = new JKRuntime.UI.UiBindingDefinition("fixture", "Smooth Camera", "Focus Camera",
                () => new[] { new JKRuntime.UI.UiChord(70) }, values => { }, () => { });
            JKRuntime.UI.UIApi.RegisterBinding(definition);
            var page = new JKRuntime.UI.UiBindingsPage("Bind Focus", "Tap to toggle. Hold for a temporary view.", definition.Id); page.OnOpen();
            device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch();
            page.Draw(); game.EndBatch(); device.SetRenderTarget(null);
            Save(target, Path.Combine(output, "bind-focus.png"));
            Check(Read(target).Any(c => c == JKRuntime.UI.UiTheme.Gold), "Bind Focus draws its binding label with installed fonts and frame");
            page.OnClose(); JKRuntime.UI.UIApi.UnregisterBinding(definition.Id);
        }
        finally { JumpKing.Controller.ControllerManager.instance = previous; }
    }

    private static void PresentationPixels(GraphicsDevice device, RenderTarget2D overlay, LevelScreen[] screens, Texture2D white, string output)
    {
        using (var compositor = new ScreenCompositor())
        using (var full = new RenderTarget2D(device, 1960, 1480))
        {
            var destination = new Rectangle(20, 20, 1920, 1440);
            Action<int> scene = delegate(int index) { screens[index].Draw(); };
            Action<float, int> capture = delegate(float shift, int revision)
            {
                device.SetRenderTarget(overlay); Game1.instance.StartBatch();
                compositor.Draw(shift, 2, scene, true, revision);
                Game1.spriteBatch.Draw(white, new Rectangle(8, 8, 18, 8), Color.Magenta);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
            };
            Action<float> present = delegate(float shift)
            {
                device.SetRenderTarget(full); device.Clear(Color.Yellow);
                var oldScissor = device.ScissorRectangle;
                compositor.Present(destination, overlay, shift);
                Check(device.ScissorRectangle == oldScissor, "Final composition restores clipping state");
                device.SetRenderTarget(null);
            };
            capture(180, 100); present(180);
            var original = Read(full);
            Check(Read(overlay)[100 * 480 + 10] == Color.Transparent, "Native target contains stationary UI over transparent world");
            for (int i = 1; i <= 10; i++) capture(180 + i * .025f, 100);
            Check(compositor.SceneRenders == 1, "Extra camera frames reuse the native scene between physics updates");
            present(180.25f); var shifted = Read(full);
            int seam = 20 + 180 * 4;
            Check(original[(seam - 1) * full.Width + 200] == Color.DarkRed && original[seam * full.Width + 200] == Color.DarkBlue, "High resolution native seam matches camera translation");
            Check(shifted[seam * full.Width + 200] == Color.DarkRed && shifted[(seam + 1) * full.Width + 200] == Color.DarkBlue, "Quarter logical pixel moves world by one output pixel");
            for (int y = 0; y < full.Height; y++)
            {
                Check(shifted[y * full.Width + 5] == Color.Yellow, "World is clipped to the native game rectangle");
                if (y >= 52 && y < 84) Check(shifted[y * full.Width + 60] == Color.Magenta && original[y * full.Width + 60] == Color.Magenta, "HUD stays fixed during fractional camera movement");
            }
            Save(full, Path.Combine(output, "high-refresh-presentation.png"));
            capture(180.25f, 101); Check(compositor.SceneRenders == 2, "New simulation update refreshes native entities and effects");
            capture(0, 101); capture(.25f, 101);
            Check(compositor.SceneRenders == 4, "Screen boundary refreshes coverage even between simulation updates");
        }
    }

    private static void NativeDrawFixture(GraphicsDevice device, RenderTarget2D target, LevelScreen[] screens, string output, string gameDir)
    {
        var jump = (JumpGame)FormatterServices.GetUninitializedObject(typeof(JumpGame));
        foreach (var field in typeof(JumpGame).GetFields(Flags).Where(x => typeof(IBTnode).IsAssignableFrom(x.FieldType)))
        {
            var node = (IBTnode)FormatterServices.GetUninitializedObject(field.FieldType);
            typeof(IBTnode).GetField("m_last_result", Flags).SetValue(node, BTresult.Failure);
            field.SetValue(jump, node);
        }
        var loop = (GameLoop)typeof(JumpGame).GetField("m_game_loop", Flags).GetValue(jump);
        typeof(IBTnode).GetField("m_last_result", Flags).SetValue(loop, BTresult.Running);
        var manager = new EntityManager();
        typeof(JumpGame).GetField("m_entity_manager", Flags).SetValue(jump, manager);
        var king = new KingMarker(); var hud = new HudMarker();
        new ScreenMarker { Screen = 0 }; new ScreenMarker { Screen = 1 };
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        player.m_body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        typeof(BodyComp).GetField("m_width", Flags).SetValue(player.m_body, 20);
        typeof(BodyComp).GetField("m_height", Flags).SetValue(player.m_body, 16);
        player.m_body.Position = king.Position; GameLoop.m_player = player;
        var fixture = new Harmony("smooth-camera.fixture");
        fixture.Patch(typeof(GameLoop).GetMethod("Draw"), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("SkipGameLoopDraw", Flags)));
        Type pauseType = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        var pause = (Entity)FormatterServices.GetUninitializedObject(pauseType); manager.AddObject(pause);
        fixture.Patch(pauseType.GetMethod("Draw"), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("FakePauseDraw", Flags)));
        try
        {
            Renderer.Start();
            RenderFrame(device, target, jump);
            var pixels = Read(target);
            Check(pixels[100 * 480 + 10] == Color.DarkRed && pixels[250 * 480 + 10] == Color.DarkBlue, "Actual JumpGame.Draw transpiler composes both native screens");
            Check(king.Draws == 2 && king.Updates == 0, "Visible world draws never replay entity updates");
            Check(hud.Draws == 1 && pixels[10 * 480 + 10] == Color.Magenta, "Actual late foreground overlay draws once at fixed coordinates");
            Check(pixels[25 * 480 + 310] == Color.Cyan && pixels[205 * 480 + 310] != Color.Cyan, "Native PauseManager UI is excluded from scrolling screen passes");
            Check(pixels[365 % 360 * 480 + 65] == Color.Lime, "Adjacent-screen culled entity appears in correct world position");
            Save(target, Path.Combine(output, "native-draw-boundary.png"));
            // Exercise the final native blit hook, including the deferred world
            // and stationary UI path used by high refresh presentation.
            fixture.Patch(typeof(Game1).GetMethod("CalculateGameRect", Flags), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("FixtureGameRect", Flags)));
            typeof(Game1).GetField("m_render_target", Flags).SetValue(Game1.instance, target);
            using (var final = new RenderTarget2D(device, 480, 360))
            {
                int worldBefore = king.Draws;
                for (int frame = 0; frame < 3; frame++)
                {
                    Renderer.BeforeGameDraw();
                    device.SetRenderTarget(target); Game1.instance.StartBatch(); jump.Draw(); Game1.instance.EndBatch();
                    device.SetRenderTarget(final); device.Clear(Color.Black);
                    typeof(Game1).GetMethod("DrawRenderTarget", Flags).Invoke(Game1.instance, null);
                    device.SetRenderTarget(null);
                    var composed = Read(final);
                    Check(composed[100 * 480 + 10] == Color.DarkRed && composed[250 * 480 + 10] == Color.DarkBlue, "Native final blit presents deferred world");
                    Check(composed[10 * 480 + 10] == Color.Magenta && composed[25 * 480 + 310] == Color.Cyan, "Native final blit retains stationary overlays");
                }
                Check(king.Draws == worldBefore && king.Updates == 0, "Real draw hooks reuse cached world on extra presentation frames");
            }
            var gestures = CameraControls.Gestures;
            gestures.Update(false, false, false, 0, true);
            gestures.Update(false, false, true, 1, true); gestures.Update(false, false, false, 1.1, true);
            for (int frame = 0; frame < 240; frame++)
            {
                typeof(Renderer).GetField("drawDelta", Flags).SetValue(null, 1f / 120);
                RenderFrame(device, target, jump);
            }
            Near(Renderer.View.Y, 0, "Focus settles on actual native screen through real renderer");
            var focusedPixels = Read(target);
            Check(focusedPixels[100 * 480 + 10] == Color.DarkBlue && focusedPixels[10 * 480 + 10] == Color.Magenta,
                "Focused native screen and stationary HUD render together");
            Save(target, Path.Combine(output, "focus-camera.png"));
            gestures.Reset();
            Settings.Current.Smooth = false;
            RenderFrame(device, target, jump);
            Check(Read(target)[100 * 480 + 10] == Color.DarkBlue, "Disabled setting restores original native screen rendering");
            Settings.Current.Smooth = true;
            var conflict = new Harmony("mega-mapping-expansion.render");
            conflict.Patch(typeof(LevelScreen).GetMethod("DrawForeground"), postfix: new HarmonyMethod(typeof(CameraTests).GetMethod("ConflictMarker", Flags)));
            Renderer.InvalidateCompatibility();
            try { RenderFrame(device, target, jump); Check(Renderer.Blocked && Read(target)[100 * 480 + 10] == Color.DarkBlue, "Known compositor conflict falls back to native screens"); }
            finally { conflict.UnpatchAll("mega-mapping-expansion.render"); Renderer.InvalidateCompatibility(); }
            RenderFrame(device, target, jump); Check(!Renderer.Blocked, "Camera resumes when compositor conflict is removed");
            if (mappingImplementation != null) MappingToggleFixture(device, target, jump, output, gameDir);
            Renderer.Stop(); RenderFrame(device, target, jump);
            Check(Read(target)[100 * 480 + 10] == Color.DarkBlue, "Level end restores original rendering");
        }
        finally { fixture.UnpatchAll("smooth-camera.fixture"); typeof(EntityManager).GetField("_instance", Flags).SetValue(null, null); }
    }
    private static void ConflictMarker() { }
    private static void RenderFrame(GraphicsDevice device, RenderTarget2D target, JumpGame jump)
    { Renderer.AfterUpdate(); device.SetRenderTarget(target); device.Clear(Color.Black); Game1.instance.StartBatch(); jump.Draw(); Game1.instance.EndBatch(); device.SetRenderTarget(null); }

    private static void RealArtPreview(GraphicsDevice device, GameServiceContainer services, RenderTarget2D target, string gameDir, string output)
    {
        using (var content = new ContentManager(services, gameDir))
        using (var compositor = new ScreenCompositor())
        {
            var art = new LevelScreen[2];
            for (int index = 0; index < 2; index++)
            {
                string number = (index + 1).ToString();
                art[index] = MakeScreen(index, LoadOptional(content, gameDir, "Content/screens/background/bg" + number),
                    LoadOptional(content, gameDir, "Content/screens/midground/" + number),
                    LoadOptional(content, gameDir, "Content/screens/foreground/fg" + number));
            }
            device.SetRenderTarget(target); Game1.instance.StartBatch();
            compositor.Draw(180, 2, delegate(int index) { art[index].Draw(); art[index].DrawForeground(); });
            Game1.instance.EndBatch(); device.SetRenderTarget(null);
            Save(target, Path.Combine(output, "base-game-seam.png"));
        }
    }

    private static void WeatherAndScrolling(GraphicsDevice device, GameServiceContainer services, RenderTarget2D target, string gameDir, string output)
    {
        using (var content = new ContentManager(services, gameDir))
        using (var compositor = new ScreenCompositor())
        using (var back = Solid(device, Color.DarkSlateGray))
        using (var clear = Solid(device, Color.Transparent))
        using (var band = new Texture2D(device, 480, 16))
        using (var rain = new Texture2D(device, 480, 360))
        {
            var game = Game1.instance;
            var bandPixels = new Color[480 * 16];
            for (int i = 0; i < bandPixels.Length; i++) bandPixels[i] = i % 19 < 9 ? Color.Orange : Color.Transparent;
            band.SetData(bandPixels);
            var rainPixels = new Color[480 * 360];
            for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                if ((x + y * 3) % 37 < 2) rainPixels[y * 480 + x] = Color.Cyan;
            rain.SetData(rainPixels);
            game.contentManager.ScrollingBackgrounds = new Dictionary<string, Texture2D> { { "fixture", band } };
            game.contentManager.particles.WeatherSprites["fixture"] = new[] { rain };
            typeof(JKContentManager).GetField("m_weather_masks", Flags).SetValue(game.contentManager, new Dictionary<string, Texture2D>());
            game.contentManager.shaders.Mask = new MaskShader(content.Load<Effect>("Content/shaders/Mask"));
            var weather = (WeatherInstance)Activator.CreateInstance(typeof(WeatherInstance), Flags, null,
                new object[] { new WeatherManager.Weather { name = "fixture", fps = 10, foreground = false, screens = new[] { 1, 2 } } }, null);
            var screens = new LevelScreen[2];
            for (int i = 0; i < 2; i++)
            {
                var scroll = new ScrollingBackground(new ScrollingBGdata { scroll_speed = 15, layers = new[] {
                    new ScrollingBGdata.Layer { texture = "fixture", direction = LayerDirection.Horizontal, position = 100 + i * 30, scroll_multiplier = 1, layer_mode = LayerMode.Background },
                    new ScrollingBGdata.Layer { texture = "fixture", direction = LayerDirection.Horizontal, position = 280 - i * 20, scroll_multiplier = -1, layer_mode = LayerMode.Foreground }
                }});
                screens[i] = new LevelScreen(i, new IBlock[0], new LevelScreen.Graphics { backbackground = back, background = clear, scrolling_background = scroll, weather = weather }, false, new TeleportLink[0], 0, null);
                screens[i].Update(0.25f);
            }
            var native = new Color[2][];
            for (int i = 0; i < 2; i++)
            {
                device.SetRenderTarget(target); device.Clear(Color.Black); game.StartBatch();
                using (new RenderContext(i)) { screens[i].Draw(); screens[i].DrawForeground(); }
                game.EndBatch(); device.SetRenderTarget(null); native[i] = Read(target);
            }
            Check(native[0].Distinct().Count() >= 3, "Native weather/scroll fixture has visible layered content");
            const int shift = 157;
            device.SetRenderTarget(target); game.StartBatch();
            compositor.Draw(shift, 2, delegate(int index) { screens[index].Draw(); screens[index].DrawForeground(); });
            game.EndBatch(); device.SetRenderTarget(null);
            var actual = Read(target);
            for (int y = 0; y < 360; y++)
            {
                int screen = y < shift ? 1 : 0, localY = y - shift + screen * 360;
                for (int x = 0; x < 480; x++)
                    if (actual[y * 480 + x] != native[screen][localY * 480 + x])
                        throw new Exception("Native weather/scroll mismatch at " + x + ", " + y);
            }
            Check(true, "Every composed weather/scroll pixel matches the corresponding native view");
            Save(target, Path.Combine(output, "weather-and-scrolling.png"));
        }
    }
    private static Texture2D LoadOptional(ContentManager content, string root, string asset)
    { return File.Exists(Path.Combine(root, asset + ".xnb")) ? content.Load<Texture2D>(asset) : null; }
    private static LevelScreen MakeScreen(int index, Texture2D back, Texture2D middle, Texture2D front)
    { return new LevelScreen(index, new IBlock[0], new LevelScreen.Graphics { backbackground = back, background = middle, foreground = front }, false, new TeleportLink[0], 0, null); }
    private static Texture2D Solid(GraphicsDevice device, Color color)
    { var texture = new Texture2D(device, 480, 360); texture.SetData(Enumerable.Repeat(color, 480 * 360).ToArray()); return texture; }
    private static Texture2D Stripe(GraphicsDevice device)
    {
        var texture = new Texture2D(device, 480, 360); var colors = new Color[480 * 360];
        for (int y = 0; y < 360; y++) for (int x = 240; x < 250; x++) colors[y * 480 + x] = Color.Gold;
        texture.SetData(colors); return texture;
    }
    private static Color[] Read(RenderTarget2D target) { var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels; }
    private static void Save(RenderTarget2D target, string path)
    {
        // The installed MonoGame PNG writer truncates some large captures. Encode
        // the read-back pixels through GDI+ and reopen the artifact for validation.
        var pixels = Read(target);
        var argb = new int[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) argb[i] = pixels[i].A << 24 | pixels[i].R << 16 | pixels[i].G << 8 | pixels[i].B;
        using (var bitmap = new System.Drawing.Bitmap(target.Width, target.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            var bits = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, target.Width, target.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try { System.Runtime.InteropServices.Marshal.Copy(argb, 0, bits.Scan0, argb.Length); }
            finally { bitmap.UnlockBits(bits); }
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        using (var decoded = new System.Drawing.Bitmap(path)) Check(decoded.Width == target.Width && decoded.Height == target.Height, "Capture decodes successfully");
    }
}
