using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JKRuntime;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prism;

internal static partial class PrismTests
{
    private static int checks;
    private static bool video;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static Beatmap Fixture()
    {
        return Beatmap.Parse(new[] { "[Difficulty]", "SliderMultiplier:1.4", "[TimingPoints]", "0,500,4,2,1,60,1,0", "1000,-50,4,2,1,60,0,0", "[HitObjects]",
            "0,0,500,1,0", "512,384,1000,2,0,L|0:384,2,140", "100,100,2000,8,0,3000" });
    }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            video = Array.IndexOf(args, "--video") >= 0;
            GC.KeepAlive(typeof(HarmonyLib.Harmony)); Audio.Validate();
            JKRuntime.Geometry.NativeWorldGeometry.ValidateContract();
            SettingsAndWind();
            Beatmap b = Fixture();
            Check(b.LastNote(.499) == -1 && b.LastNote(.5) == 0 && b.LastNote(100) == 2, "Binary search object boundaries");
            Check(Math.Abs(b.Notes[1].End - 1.5) < .001, "Slider repeats and inherited velocity");
            Check(Math.Abs(b.Notes[1].Distance - 1) < .001 && b.Notes[1].Direction.X > .7, "Object spacing and direction");
            Check(b.Pulse(.5) > b.Pulse(.9), "Transient decay follows chart time");
            Check(b.Notes[2].End == 3, "Spinner duration");
            bool failed = false; try { Beatmap.Parse(new[] { "[TimingPoints]", "0,NaN", "[HitObjects]", "1,1,0,1,0" }); } catch { failed = true; }
            Check(failed, "Reject non-finite chart data");
            var tri = new[] { new Vector2(0, 8), new Vector2(8, 0), new Vector2(8, 8) };
            Check(ScreenArt.Inside(tri, 7.5f, 7.5f) && !ScreenArt.Inside(tri, .5f, .5f), "Slope shape keeps its empty half");
            using (var art = new ScreenArt())
            {
                art.Raster(new Rectangle(-5, -5, 13, 13), tri, 1);
                Check(art.Cells[7 * 480 + 7] == 1 && art.Cells[0] == 0, "Raster clips off-screen and slope pixels");
            }
            string imported = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../LOCAL_THEME/event-horizon/score.osu"));
            if (File.Exists(imported)) { var real = Beatmap.Load(imported); Check(real.Notes.Length > 100, "Imported .357 Magnum chart parses"); Console.WriteLine("[INFO] Imported hit objects: " + real.Notes.Length); }
            if (args.Length > 0 && args[0] == "--graphics") Graphics(args[1], File.Exists(imported) ? Beatmap.Load(imported) : b);
            Console.WriteLine("[OK] Prism: " + checks + " checks"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private sealed class Service : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add {} remove {} }
        public event EventHandler<EventArgs> DeviceDisposing { add {} remove {} }
        public event EventHandler<EventArgs> DeviceReset { add {} remove {} }
        public event EventHandler<EventArgs> DeviceResetting { add {} remove {} }
    }
    private static void Graphics(string gameDir, Beatmap chart)
    {
        string output = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../PREVIEW")); Directory.CreateDirectory(output);
        using (var window = new Form { ShowInTaskbar = false })
        using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360 }))
        using (var batch = new SpriteBatch(device))
        using (var target = new RenderTarget2D(device, 480, 360))
        using (var renderer = new Renderer(device, batch, chart))
        {
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new Service { GraphicsDevice = device });
            using (var content = new ContentManager(services, gameDir))
            {
                var texture = content.Load<Texture2D>("Content/level");
                var source = LevelTexture.FromTexture(texture);
                PrepareNativeHost(device, batch, services);
                PrepareUi(content, device);
                WaterSlopes(device, batch, target, renderer, output);
                var load = typeof(LevelManager).GetMethod("LoadBlocksInterval", OwnedPatches.Members);
                var king = content.Load<Texture2D>("Content/king/base");
                using (var kingFile = File.Create(Path.Combine(output, "king-atlas.png"))) king.SaveAsPng(kingFile, king.Width, king.Height);
                foreach (int screen in new[] { 0, 1, 4, 12, 23 })
                {
                    object[] arguments = { source, null, screen, false, null, 0f, null };
                    var blocks = (IBlock[])load.Invoke(null, arguments);
                    var native = new LevelScreen(screen, blocks, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
                    using (var art = ScreenArt.Read(native, device))
                    {
                        Check(art.Supported, "Native screen supports replacement: " + screen);
                        Check(art.Cells.Count(x => x != 0) > 0, "Actual base map geometry loaded: " + screen);
                        foreach (var strip in art.Mirrors) for (int x = strip.X; x < strip.X + strip.Width; x++)
                            Check(art.Cells[strip.Y * 480 + x] != 0, "Reflection stays in platform");
                        double time = screen == 0 ? 15 : screen == 1 ? 33 : screen == 4 ? 80 : screen == 12 ? 110 : 135;
                        device.SetRenderTarget(target); device.Clear(Color.Black); renderer.Begin();
                        renderer.Background(time, screen, art, false);
                        // A bright actor marker makes clipping/copy regressions visible.
                        batch.Draw(king, new Vector2(224, screen == 0 ? 296 : 272), new Rectangle(0, 0, 32, 32), Color.White);
                        renderer.Foreground(time, art, true, false);
                        batch.End(); device.SetRenderTarget(null);
                        var pixels = new Color[480 * 360]; target.GetData(pixels);
                        Check(pixels[0].A == 255 && pixels.Any(p => p.R > 100 || p.G > 100), "World survives reflection target switch");
                        using (var file = File.Create(Path.Combine(output, "screen-" + (screen + 1) + ".png"))) target.SaveAsPng(file, 480, 360);
                        if (video && screen == 0)
                        {
                            string frames = Path.Combine(output, "frames"); Directory.CreateDirectory(frames);
                            for (int frame = 0; frame < 480; frame++)
                            {
                                double t = 79 + frame / 60.0;
                                device.SetRenderTarget(target); renderer.Begin(); renderer.Background(t, screen, art, false);
                                batch.Draw(king, new Vector2(224, 296), new Rectangle(0, 0, 32, 32), Color.White);
                                renderer.Foreground(t, art, true, false); batch.End(); device.SetRenderTarget(null);
                                using (var file = File.Create(Path.Combine(frames, frame.ToString("D4") + ".png"))) target.SaveAsPng(file, 480, 360);
                            }
                        }
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 120; i++)
                        { device.SetRenderTarget(target); renderer.Begin(); renderer.Background(time + i / 60.0, screen, art, false); renderer.Foreground(time, art, true, false); batch.End(); device.SetRenderTarget(null); }
                        Console.WriteLine("[GPU] Screen {0}: {1:F2} ms/frame, {2} reflection strips", screen + 1, watch.Elapsed.TotalMilliseconds / 120, art.Mirrors.Length);
                    }
                }
                NativeLifecycle(device, batch, target, source, output);
            }
        }
    }
    private static void PrepareNativeHost(GraphicsDevice device, SpriteBatch batch, GameServiceContainer services)
    {
        var game = (JumpKing.Game1)FormatterServices.GetUninitializedObject(typeof(JumpKing.Game1));
        typeof(JumpKing.Game1).GetField("_instance", OwnedPatches.Members).SetValue(null, game);
        typeof(Game).GetField("_services", OwnedPatches.Members).SetValue(game, services);
        foreach (var field in typeof(Game).GetFields(OwnedPatches.Members).Where(x => x.FieldType == typeof(IGraphicsDeviceService)))
            field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
        JumpKing.Game1.spriteBatch = batch;
        typeof(Microsoft.Xna.Framework.Audio.SoundEffect).GetMethod("InitializeSoundEffect", OwnedPatches.Members).Invoke(null, null);
        var soundType = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime", true);
        var sound = Activator.CreateInstance(soundType, true);
        soundType.BaseType.GetField("instance", OwnedPatches.Members).SetValue(null, sound);
        // Keep the hardware clock running silently during automated audio checks.
        soundType.BaseType.GetMethod("SetStartSettings", OwnedPatches.Members).Invoke(sound, new object[] { new JumpKing.PlayerPreferences.SoundPrefs { music_on = true, master = 0 } });
    }
    private sealed class ForeignBlock : BoxBlock { internal ForeignBlock() : base(new Rectangle(20, 20, 20, 20)) {} }
    private sealed class SoundMarker : JumpKing.XnaWrappers.IJKSound
    {
        internal SoundMarker() { Stop(); }
        public bool IsLooped { get; set; }
        public float Volume { get; set; }
        public TimeSpan Duration { get { return TimeSpan.FromSeconds(10); } }
        public JumpKing.XnaWrappers.JKSoundState State { get; private set; }
        public void Play() { State = JumpKing.XnaWrappers.JKSoundState.Playing; }
        public void Pause() { State = JumpKing.XnaWrappers.JKSoundState.Paused; }
        public void Resume() { Play(); }
        public void Stop() { State = JumpKing.XnaWrappers.JKSoundState.Stopped; }
    }
    private static void NativeLifecycle(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, LevelTexture source, string output)
    {
        string theme = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../LOCAL_THEME/event-horizon"));
        if (!File.Exists(Path.Combine(theme, "music.wav"))) return;
        string statePath = Scratch("settings.xml");
        if (File.Exists(statePath)) File.Delete(statePath);
        var settings = new JKRuntime.Settings.SettingsFile<Preferences>(statePath, () => { var p = Preferences.Defaults(); p.Enabled = false; return p; }, Preferences.Validate);
        Settings.File = settings;
        object[] args = { source, null, 0, false, null, 0f, null };
        var blocks = (IBlock[])typeof(LevelManager).GetMethod("LoadBlocksInterval", OwnedPatches.Members).Invoke(null, args);
        using (var original = new Texture2D(device, 480, 360))
        using (var unknown = ScreenArt.Read(new LevelScreen(0, new IBlock[] { new ForeignBlock() }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null), device))
        {
            Check(!unknown.Supported, "Unknown subclasses preserve original artwork");
            original.SetData(Enumerable.Repeat(Color.DarkGreen, 480 * 360).ToArray());
            var native = new LevelScreen(0, blocks, new LevelScreen.Graphics { background = original }, false, new TeleportLink[0], 0, null);
            typeof(LevelManager).GetField("m_screens", OwnedPatches.Members).SetValue(null, new[] { native });
            using (var scope = new RuntimeScope())
            {
                ModEntry.Prepare(scope);
                Check(typeof(ModEntry).GetField("world", OwnedPatches.Members).GetValue(null) == null, "Disabled preparation decodes no audio or geometry");
                Check(!HarmonyLib.Harmony.GetAllPatchedMethods().Any(m => HarmonyLib.Harmony.GetPatchInfo(m).Owners.Contains("prism.render")), "Initially disabled theme installs no render hooks");
                var world = scope.Own(new World(theme));
                LoadMappingFixture();
                ModEntry.InstallHooks(scope);
                typeof(ModEntry).GetField("world", OwnedPatches.Members).SetValue(null, world);
                settings.Value.Enabled = true;
                var kernel = typeof(RuntimeApi).GetField("Kernel", OwnedPatches.Members).GetValue(null);
                scope.Own(RuntimeApi.Register(new ModuleDefinition("prism", new Version(0, 1), ModEntry.Start)));
                scope.Defer(delegate { kernel.GetType().GetMethod("Deactivate", OwnedPatches.Members).Invoke(kernel, null); });
                Check((bool)kernel.GetType().GetMethod("Activate", OwnedPatches.Members).Invoke(kernel, null), "Native Runtime activation: " + string.Join(";", RuntimeApi.GetErrors()));
                System.Threading.Thread.Sleep(90);
                double playing = world.Music.Time;
                Check(playing > .025, "Animation reads the native audio sample cursor");
                world.Music.Pause(true); double paused = world.Music.Time; System.Threading.Thread.Sleep(55);
                Check(Math.Abs(world.Music.Time - paused) < .02, "Pause freezes sample clock");
                world.Music.Pause(false); System.Threading.Thread.Sleep(55);
                Check(world.Music.Time > paused + .02, "Resume continues sample clock");
                LiveMenus(device, batch, target, world, output);
                SceneryChecks(device, batch, target);
                MappingSceneryChecks();
                var marker = new SoundMarker();
                Audio.NativePlay.Invoke(null, new object[] { marker });
                Check(marker.State != JumpKing.XnaWrappers.JKSoundState.Playing, "Map music changes are suppressed while theme owns music");
                device.SetRenderTarget(target); device.Clear(Color.Black); world.Renderer.Begin(); native.Draw(); native.DrawForeground(); batch.End(); device.SetRenderTarget(null);
                var pixels = new Color[480 * 360]; target.GetData(pixels);
                Check(pixels[100] != Color.DarkGreen, "Actual native Draw hooks replace map layers");
                settings.Value.Enabled = false; world.Music.Deactivate();
                Check(marker.State == JumpKing.XnaWrappers.JKSoundState.Playing, "Latest map music resumes on disable");
                device.SetRenderTarget(target); world.Renderer.Begin(); native.Draw(); native.DrawForeground(); batch.End(); device.SetRenderTarget(null); target.GetData(pixels);
                Check(pixels[100] == Color.DarkGreen, "Disabled hooks immediately restore original map pixels");
                kernel.GetType().GetMethod("Deactivate", OwnedPatches.Members).Invoke(kernel, null);
                Check(!ModEntry.Rendering, "Attempt release removes active presentation");
                settings.Value.Enabled = true;
                using (var attempt = new RuntimeScope()) ModEntry.Attempt(attempt);
                Check(world.Music.Time == 0, "Restart prepares a fresh audio cursor before activation");
                world.Music.Activate(true);
                System.Threading.Thread.Sleep(35);
                Check(world.Music.Time == 0, "Paused restart does not start audio during preparation");
                world.Music.Deactivate();
                Check((bool)kernel.GetType().GetMethod("Activate", OwnedPatches.Members).Invoke(kernel, null), "Same-world restart reuses prepared world");
                System.Threading.Thread.Sleep(65);
                Check(world.Music.Time > .015 && world.Music.Time < .3, "Restart plays from the beginning");
                kernel.GetType().GetMethod("Deactivate", OwnedPatches.Members).Invoke(kernel, null);
            }
            Check(ModEntry.Current == null, "World teardown clears prepared state");
            Check(!HarmonyLib.Harmony.GetAllPatchedMethods().Any(m => HarmonyLib.Harmony.GetPatchInfo(m).Owners.Contains("prism.render")), "World teardown removes only owned hooks");
        }
    }
}
