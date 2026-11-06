using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using God;
using JKRuntime;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;

internal static class GodTests
{
    private static int checks;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, message); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(args[1]); Geometry(); Walls(); BlockTypes(); History(); Persistence(args[1]); Materials(args[1]); Decor(args[1]); NativeContracts();
            if (args.Contains("--graphics")) Graphics(args[0], args[1]);
            Console.WriteLine("[OK] God: " + checks + " behavioral assertions"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Geometry()
    {
        var cells = new int[Terrain.Count];
        Terrain.Paint(cells, new Rectangle(16, 16, 24, 8), 1); Terrain.Paint(cells, new Rectangle(40, 16, 24, 8), 1);
        Check(Terrain.Regions(cells).Count == 1 && Terrain.Rectangles(cells).Single() == new Rectangle(16, 16, 48, 8), "touching rectangles merge without a collision seam");
        Check(((Material.Feature(cells, 39, 20) >> 2) & 3) == 3, "the former join has no right edge");
        Terrain.Paint(cells, new Rectangle(32, 16, 8, 8), 0);
        Check(Terrain.Regions(cells).Count == 2, "the rectangular eraser splits a platform");
        Terrain.Paint(cells, new Rectangle(0, 0, 1, 1), 1); Terrain.Paint(cells, new Rectangle(1, 1, 1, 1), 1);
        Check(Terrain.Regions(cells).Count == 4, "corner-only contact doesn't join regions");
        var random = new Random(84017);
        for (int iteration = 0; iteration < 12; iteration++)
        {
            cells = new int[Terrain.Count];
            for (int n = 0; n < 40; n++) Terrain.Paint(cells, new Rectangle(random.Next(-10, 480), random.Next(-10, 360), random.Next(1, 100), random.Next(1, 60)), random.Next(0, 3));
            var roundtrip = new int[Terrain.Count]; foreach (var r in Terrain.Rectangles(cells)) Terrain.Paint(roundtrip, r, 1);
            Check(cells.Select(i => i != 0).SequenceEqual(roundtrip.Select(i => i != 0)), "rectangle compilation preserves every occupied pixel");
        }
        cells = new int[Terrain.Count]; Terrain.Paint(cells, new Rectangle(32, 32, 16, 8), 1);
        var region = Terrain.Regions(cells).Single(); var moved = Terrain.Move(cells, region, new Point(8, -8), false, Rectangle.Empty);
        Check(!Terrain.Solid(moved, 32, 32) && Terrain.Solid(moved, 40, 24), "move removes the old surface and places the new one");
        Check(Terrain.Move(cells, region, new Point(-80, 0), false, Rectangle.Empty).All(i => i == 0), "a move entirely outside the screen leaves no in-bounds cells");
        Check(Terrain.Move(cells, region, new Point(-80, 0), false, Rectangle.Empty, true).SequenceEqual(cells), "an offscreen copy leaves its donor intact");
        foreach (var offset in new[] { new Point(-40, 0), new Point(440, 0), new Point(0, -36), new Point(0, 324), new Point(440, 324), new Point(-40, -36) })
        {
            var expected = new int[Terrain.Count];
            var destination = region.Bounds; destination.Offset(offset.X, offset.Y); destination = Rectangle.Intersect(Terrain.Screen, destination);
            Terrain.Paint(expected, destination, 1);
            var clipped = Terrain.Move(cells, region, offset, false, Rectangle.Empty);
            Check(clipped.SequenceEqual(expected), "moves clip at every edge and corner: " + offset);
            var history = new EditHistory(); history.Push(cells);
            Check(history.Undo(clipped).SequenceEqual(cells), "undo restores the complete unclipped platform: " + offset);
            var copied = Terrain.Move(cells, region, offset, false, Rectangle.Empty, true);
            Terrain.Paint(expected, region.Bounds, 1);
            Check(copied.SequenceEqual(expected), "clipped copies preserve their donor: " + offset);
        }
        var typed = (int[])cells.Clone(); Terrain.Paint(typed, new Rectangle(40, 32, 8, 8), 2);
        var resizeOutside = Terrain.Move(typed, region, Point.Zero, true, new Rectangle(-16, 352, 32, 16));
        Check(resizeOutside.Count(i => i == 2) == 128 && resizeOutside.Count(i => i == 1) == 0,
            "resizing crops the requested shape without rescaling its surviving material halves");
        Reject(() => Terrain.Move(cells, region, Point.Zero, true, new Rectangle(0, 0, 0, 8)), "empty requested resize remains invalid");
        var resized = Terrain.Move(cells, region, Point.Zero, true, new Rectangle(32, 32, 8, 8));
        Check(Terrain.Rectangles(resized).Single() == new Rectangle(32, 32, 8, 8), "minimum grid block is a complete 8x8 platform");
        Check(Terrain.DragRect(new Point(40, 40), new Point(32, 24), 8) == new Rectangle(32, 24, 16, 24), "reverse dragging has inclusive grid cells");
    }
    private static void History()
    {
        var history = new EditHistory(); var a = new int[Terrain.Count]; var b = (int[])a.Clone(); b[4] = 1;
        history.Push(a); a[1] = 5; var undone = history.Undo(b);
        Check(undone[1] == 0 && undone[4] == 0, "history owns detached snapshots");
        Check(history.Redo(undone)[4] == 1, "redo restores edited terrain");
        history.Undo(b); history.Push(undone);
        Check(ReferenceEquals(history.Redo(b), b), "a new edit discards the redo branch");
    }
    private static void Walls()
    {
        var cells = new int[Terrain.Count];
        Terrain.Paint(cells, new Rectangle(0, 0, 32, 360), 1);
        Terrain.Paint(cells, new Rectangle(16, 120, 16, 80), 0);
        Terrain.Paint(cells, new Rectangle(32, 64, 48, 8), 1);
        Terrain.Paint(cells, new Rectangle(464, 0, 16, 360), 1);
        var regions = Terrain.Regions(cells);
        Check(regions.Count(r => r.Wall) == 2, "both full-height screen edges form independent walls");
        Check(regions.First(r => r.Wall).Bounds == new Rectangle(0, 0, 16, 360), "a shallow cavity leaves only the full-height core as wall");
        Check(regions.Count(r => !r.Wall) == 2, "geometry above and below a cavity becomes separate platforms");
        var materials = new List<Material>();
        foreach (var region in regions) { int id = materials.Count + 1; foreach (int i in region.Pixels) cells[i] = id; materials.Add(new Material(id, region, cells)); }
        int choice = Material.ChooseConnected(materials, new Rectangle(16, 240, 8, 8), cells);
        Check(!materials[choice - 1].Wall, "Auto never chooses a touching boundary wall");
        Check(Terrain.Regions(cells, id => 1).Count == 4, "selection keeps the wall split after material labeling");
        var mixed = new int[Terrain.Count]; Terrain.Paint(mixed, new Rectangle(40, 40, 16, 8), 1); Terrain.Paint(mixed, new Rectangle(56, 40, 16, 8), 2);
        Check(Terrain.Regions(mixed).Count == 2 && Terrain.Regions(mixed, id => 1).Count == 1, "different types split; separate samples of one type can merge");
        var roundtrip = new int[Terrain.Count]; foreach (var r in Terrain.Rectangles(mixed)) Terrain.Paint(roundtrip, r, mixed[r.Y * 480 + r.X]);
        Check(roundtrip.SequenceEqual(mixed), "compiled colliders preserve material boundaries");
    }
    private sealed class CustomBlock : BoxBlock
    {
        internal readonly int[] Values;
        internal readonly float Strength;
        internal CustomBlock(Rectangle r, float strength) : base(r) { Strength = strength; Values = new[] { 3, 7 }; }
    }
    private static void BlockTypes()
    {
        var bounds = new Rectangle(16, 24, 8, 8); var destination = new Rectangle(100, -300, 24, 16);
        foreach (IBlock source in new IBlock[] { new BoxBlock(bounds), new IceBlock(bounds), new SnowBlock(bounds), new SandBlock(bounds), new WaterBlock(bounds), new NoWindBlock(bounds), new QuarkBlock(bounds), new SlopeBlock(bounds, SlopeType.TopLeft), new CustomBlock(bounds, .7f) })
        {
            var template = new BlockTemplate(source); var copy = template.Copy(destination);
            Check(copy.GetType() == source.GetType() && copy.GetRect() == destination && source.GetRect() == bounds, "copies keep type and relocate without modifying the donor: " + source.GetType().Name);
        }
        var custom = new CustomBlock(bounds, .7f); var customCopy = (CustomBlock)new BlockTemplate(custom).Copy(destination);
        customCopy.Values[0] = 99;
        Check(custom.Values[0] == 3 && customCopy.Strength == custom.Strength, "mod block mutable data is detached and parameters survive");
        Check(new BlockTemplate(custom).Key != new BlockTemplate(new CustomBlock(bounds, .9f)).Key, "different parameters split samples of the same mod block class");
    }
    private static void Decor(string output)
    {
        var baseline = new int[Terrain.Count]; Terrain.Paint(baseline, new Rectangle(32, 80, 16, 8), 1);
        var materials = new List<Material> { new Material(1, Terrain.Regions(baseline).Single(), baseline) };
        var source = new Color[Terrain.Count];
        for (int x = 32; x < 48; x++) { source[79 * 480 + x] = Color.Lime; source[76 * 480 + x] = Color.Gold; source[50 * 480 + x] = Color.Red; }
        var next = (int[])baseline.Clone(); Terrain.Paint(next, new Rectangle(112, 160, 16, 8), 1);
        for (int y = 130; y < 160; y++) for (int x = 112; x < 128; x++) source[y * 480 + x] = Color.Blue;
        var art = PlatformArt.WithDecor(source, baseline, next, materials, 26, true);
        Check(art[159 * 480 + 113] == Color.Lime && art[156 * 480 + 113] == Color.Gold, "foreground above the donor follows a new platform");
        Check(art[158 * 480 + 113].A == 0, "transparent gaps replace destination foreground instead of stacking it");
        Check(art[130 * 480 + 113] == Color.Blue, "decor sampling stops at the king's height");
        Terrain.Paint(next, new Rectangle(32, 80, 16, 8), 0);
        art = PlatformArt.WithDecor(source, baseline, next, materials, 26, true);
        Check(art[79 * 480 + 33].A == 0 && art[50 * 480 + 33] == Color.Red, "erasing clears owned decor and leaves higher scenery alone");
        Check(PlatformArt.WithDecor(source, baseline, baseline, materials, 26, true).SequenceEqual(source), "untouched foreground stays exact");
        SavePixels(art, Path.Combine(output, "decor.png"));
    }
    private static void Persistence(string output)
    {
        string directory = Path.Combine(output, "storage"); Directory.CreateDirectory(directory);
        Storage.Initialize(directory); Storage.Bind(new[] { new JKRuntime.UI.UiChord((int)Keys.F8, (int)Keys.LeftControl) }); Storage.Initialize(directory);
        Check(!new Preferences().TransferForeground && !Storage.Preferences.TransferForeground, "foreground transfer defaults off, including existing settings without the field");
        var option = new ForegroundOption(); typeof(ForegroundOption).GetMethod("OnToggle", Flags).Invoke(option, null); Storage.Initialize(directory);
        Check(Storage.Preferences.TransferForeground, "the native foreground option persists its enabled state");
        typeof(ForegroundOption).GetMethod("OnToggle", Flags).Invoke(option, null); Storage.Initialize(directory);
        Check(!Storage.Preferences.TransferForeground && Storage.Chords().Single().Buttons.Length == 2, "turning foreground off preserves existing bindings");
        Check(Storage.Chords().Single().Buttons.Length == 2, "rebound chords survive reload");
        var cells = new int[Terrain.Count]; Terrain.Paint(cells, new Rectangle(100, 200, 80, 16), 2);
        Check(Storage.Decode(Storage.Encode(cells), 2).SequenceEqual(cells), "terrain RLE survives gzip and XML storage");
        Reject(() => Storage.Decode(Storage.Encode(cells), 1), "unknown saved material IDs are rejected");
        Reject(() => Storage.Decode("not base64", 3), "damaged terrain doesn't reach the live screen");
        string path = Path.Combine(directory, "atomic.xml");
        Storage.Write(path, new SavedWorld { Version = 7 }); Storage.Write(path, new SavedWorld { Version = 8 });
        Check(Storage.Read<SavedWorld>(path).Version == 8 && Storage.Read<SavedWorld>(path + ".bak").Version == 7, "atomic saves retain the preceding complete file");
        File.WriteAllText(path, "<broken");
        Check(Storage.Read<SavedWorld>(path).Version == 7, "a damaged primary can recover its previous complete XML");
        File.WriteAllText(path, "<!DOCTYPE test [<!ENTITY probe SYSTEM 'file:///does-not-exist'>]><SavedWorld>&probe;</SavedWorld>");
        Check(Storage.Read<SavedWorld>(path).Version == 7, "DTD payloads are rejected before parsing entity contents");
    }
    private static Color[] Donor(out int[] baseline, out List<Material> materials)
    {
        baseline = new int[Terrain.Count]; Terrain.Paint(baseline, new Rectangle(16, 16, 48, 24), 1);
        var source = new Color[Terrain.Count];
        for (int y = 16; y < 40; y++) for (int x = 16; x < 64; x++)
        {
            int feature = Material.Feature(baseline, x, y);
            bool top = (feature & 3) == 0, right = ((feature >> 2) & 3) == 0, bottom = ((feature >> 4) & 3) == 0, left = ((feature >> 6) & 3) == 0;
            source[y * Terrain.Width + x] = top ? Color.Lime : bottom ? Color.Blue : left ? Color.Red : right ? Color.Yellow : new Color(75 + x % 8 * 3, 50 + y % 8 * 3, 30);
        }
        materials = new List<Material> { new Material(1, Terrain.Regions(baseline).Single(), baseline) }; return source;
    }
    private static void Materials(string output)
    {
        int[] baseline; List<Material> materials; var source = Donor(out baseline, out materials);
        var cells = (int[])baseline.Clone(); Terrain.Paint(cells, new Rectangle(100, 40, 8, 8), 1);
        var art = PlatformArt.Compose(source, baseline, cells, materials);
        Check(art[40 * 480 + 103] == Color.Lime && art[47 * 480 + 103] == Color.Blue, "an 8x8 platform inherits top and bottom caps");
        Check(art[44 * 480 + 100] == Color.Red && art[44 * 480 + 107] == Color.Yellow, "an 8x8 platform inherits both side caps");
        Terrain.Paint(cells, new Rectangle(108, 40, 40, 8), 1); art = PlatformArt.Compose(source, baseline, cells, materials);
        Check(art[44 * 480 + 107] != Color.Yellow && art[44 * 480 + 108] != Color.Red, "joining rectangles removes their internal textured edges");
        Terrain.Paint(cells, new Rectangle(116, 48, 8, 24), 1);
        Terrain.Paint(cells, new Rectangle(180, 40, 56, 40), 1); Terrain.Paint(cells, new Rectangle(196, 48, 24, 24), 0);
        art = PlatformArt.Compose(source, baseline, cells, materials);
        Check(art[48 * 480 + 200] == Color.Transparent && art[47 * 480 + 200] == Color.Blue, "a cutout has an empty interior and an exposed lower rim");
        Check(PlatformArt.Compose(source, baseline, baseline, materials).SequenceEqual(source), "unchanged source artwork stays byte-for-byte intact");
        Check(art.SequenceEqual(PlatformArt.Compose(source, baseline, cells, materials)), "material sampling is deterministic across rebuilds");
        SavePixels(art, Path.Combine(output, "contours.png"));
        Terrain.Paint(cells, new Rectangle(16, 16, 48, 24), 0); art = PlatformArt.Compose(source, baseline, cells, materials);
        Check(art[20 * 480 + 20] == Color.Transparent, "removing an authored platform clears its pixels");
        SavePixels(art, Path.Combine(output, "erase.png"));
    }
    private static void SavePixels(Color[] pixels, string path, int width = 480, int height = 360)
    {
        using (var bitmap = new System.Drawing.Bitmap(width, height))
        {
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            { var c = pixels[y * width + x]; bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)); }
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    private static void NativeContracts()
    {
        ScreenEdit.ValidateContracts();
        foreach (string name in new[] { "PauseUpdate", "Draw", "SetPause" })
            Check(typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager").GetMethod(name, Flags) != null, "installed pause hook exists: " + name);
        Check(typeof(JumpKing.Controller.ControllerManager).GetMethod("Update").GetParameters().Length == 0, "editor update follows the installed native controller poll");
        Check(typeof(Game1).GetMethod("DrawRenderTarget", Flags) != null, "window overlay hook exists");
        IBlock box = new BoxBlock(new Rectangle(4, 8, 12, 16)); Rectangle overlap;
        Check(box.Intersects(new Rectangle(4, 8, 1, 1), out overlap) == BlockCollisionType.Collision_Blocking, "generated platforms use the native blocking contract");
    }
    private sealed class DeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }
    private static void Graphics(string gameDirectory, string output)
    {
        using (var form = new Form { ShowInTaskbar = false })
        using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, new PresentationParameters { DeviceWindowHandle = form.Handle, BackBufferWidth = 1280, BackBufferHeight = 720 }))
        using (var source = new Texture2D(device, 480, 360))
        {
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1)); typeof(Game1).GetField("_instance", Flags).SetValue(null, game);
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
            typeof(Game).GetField("_services", Flags).SetValue(game, services);
            foreach (var field in typeof(Game).GetFields(Flags).Where(f => f.FieldType == typeof(IGraphicsDeviceService))) field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
            typeof(Game1).GetField("_game_rect", Flags).SetValue(game, new Rectangle(160, 0, 960, 720));
            game.contentManager = new JKContentManager();
            int[] mask; List<Material> unused; source.SetData(Donor(out mask, out unused));
            IBlock solid = new BoxBlock(new Rectangle(16, 16, 48, 24)); IBlock water = new WaterBlock(new Rectangle(320, 280, 32, 32));
            var screen = new LevelScreen(0, new[] { solid, water }, new LevelScreen.Graphics { background = source }, false, new TeleportLink[0], 0, null);
            ScreenEdit.Screens.SetValue(null, new[] { screen });
            using (var edit = new ScreenEdit(screen))
            {
                var regions = edit.Regions;
                Check(ReferenceEquals(regions, edit.Regions), "visiting an unchanged screen reuses its selection regions");
                Check(typeof(ScreenEdit).GetField("midground", Flags).GetValue(edit) == null,
                    "visiting a screen doesn't read its GPU artwork");
                Check(edit.Materials.All(m => !(bool)typeof(Material).GetField("samplesReady", Flags).GetValue(m)),
                    "visiting a screen doesn't build unused texture donor samples");
                var bad = (int[])edit.Cells.Clone(); bad[0] = -1;
                Reject(() => edit.Apply(bad, false), "invalid edits fail before replacing the cached screen selection");
                Check(ReferenceEquals(regions, edit.Regions), "a rejected edit keeps the cached regions");
                edit.Apply(edit.Cells, false);
                Check(ReferenceEquals(regions, edit.Regions), "a no-op edit keeps the cached regions");
                var next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(100, 80, 48, 8), 1); edit.Apply(next, false);
                Check(!ReferenceEquals(regions, edit.Regions) && edit.Regions.Any(r => r.Bounds == new Rectangle(100, 80, 48, 8)),
                    "a committed edit rebuilds selection for the new platform");
                regions = edit.Regions;
                Check(((IBlock[])ScreenEdit.Blocks.GetValue(screen)).Contains(water), "native non-solid water survives terrain commits");
                Check(((IBlock[])ScreenEdit.Blocks.GetValue(screen)).Length == 3, "new connected solids compile to two blocks beside the native water");
                var graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen); var pixels = new Color[Terrain.Count]; graphics.background.GetData(pixels);
                Check(pixels[80 * 480 + 110] == Color.Lime, "committed GPU artwork contains the sampled top edge");
                edit.Apply(edit.Baseline, false);
                Check(!ReferenceEquals(regions, edit.Regions) && edit.Regions.All(r => r.Bounds != new Rectangle(100, 80, 48, 8)),
                    "reset invalidates selection without retaining the removed platform");
                Check(ReferenceEquals(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).background, source), "reset restores the original texture object");
                var foreign = new[] { solid }; ScreenEdit.Blocks.SetValue(screen, foreign);
                Reject(() => edit.Apply(next, false), "foreign geometry replacement can't be overwritten");
                Check(ReferenceEquals(ScreenEdit.Blocks.GetValue(screen), foreign), "a failed edit leaves foreign geometry untouched");
            }
            Check(ReferenceEquals(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).background, source), "teardown preserves the source texture");
            TypedWorld(); SlopeWorld(); SlopeGroups(); DiagonalSlopes(); ResetWorld(output); LayerUpdates(device);
            using (var content = new ContentManager(services, gameDirectory))
            {
                ProfileScreenVisits(content, output);
                ProfileEditing(content, device, output);
                NativeLayers(content, output);
                var native = content.Load<Texture2D>("Content/screens/midground/1");
                var texture = new Color[Terrain.Count]; native.GetData(texture);
                // an ordinary rectangular crop from the installed first screen
                var cells = new int[Terrain.Count]; Terrain.Paint(cells, new Rectangle(184, 40, 112, 48), 1);
                var materials = new List<Material> { new Material(1, Terrain.Regions(cells).Single(), cells) };
                var next = (int[])cells.Clone(); Terrain.Paint(next, new Rectangle(184, 112, 8, 8), 1); Terrain.Paint(next, new Rectangle(208, 112, 112, 16), 1); Terrain.Paint(next, new Rectangle(248, 128, 16, 32), 1);
                SavePixels(PlatformArt.Compose(texture, cells, next, materials), Path.Combine(output, "native-material.png"));
                Check(native.Width == 480 && native.Height == 360, "the installed native material fixture uses the supported screen dimensions");
                game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
                game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                var frame = content.Load<Texture2D>("Content/gui/frame"); int frameCell = frame.Width / 3;
                game.contentManager.gui.FrameSprites = new Sprite[3, 3];
                for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
                    game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(frame, new Rectangle(x * frameCell, y * frameCell, frameCell, frameCell));
                game.contentManager.root = Path.Combine(gameDirectory, "Content");
                var nativeScreen = new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(184, 40, 112, 48)) }, new LevelScreen.Graphics { background = native }, false, new TeleportLink[0], 0, null);
                ScreenEdit.Screens.SetValue(null, new[] { nativeScreen });
                Storage.Initialize(Path.Combine(output, "live-storage")); Storage.Preferences.Grid = 8;
                var pauseType = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager");
                pauseType.GetField("instance", Flags).SetValue(null, FormatterServices.GetUninitializedObject(pauseType));
                var fixture = new HarmonyLib.Harmony("god.fixture.window");
                var visible = typeof(Game).GetProperty("IsMouseVisible");
                fixture.Patch(visible.GetGetMethod(), prefix: new HarmonyLib.HarmonyMethod(typeof(GodTests).GetMethod("CursorVisible", Flags)));
                fixture.Patch(visible.GetSetMethod(), prefix: new HarmonyLib.HarmonyMethod(typeof(GodTests).GetMethod("SetCursorVisible", Flags)));
                Entry.World = new World(); Entry.Editor = new Editor();
                try
                {
                    Entry.Begin(new KeyboardState(), Pointer(104, 160, false));
                    Check(!JKRuntime.Gameplay.NativePause.IsPaused, "entering God leaves the world running");
                    var inputCapture = typeof(RuntimeApi).Assembly.GetType("JKRuntime.UI.TextInputCapture").GetProperty("Active", Flags);
                    Check(!(bool)inputCapture.GetValue(null, null), "God doesn't capture keyboard gameplay input");
                    Entry.Editor.Update(new KeyboardState(), Pointer(104, 160, true));
                    var live = Entry.World.Get(0);
                    Check(Terrain.Solid(live.Cells, 104, 160), "a held draw gesture installs collision before mouse release");
                    Entry.Editor.Update(new KeyboardState(), Pointer(200, 176, true));
                    Check(Terrain.Solid(live.Cells, 200, 176) && !JKRuntime.Gameplay.NativePause.IsPaused, "stretching a live rectangle updates geometry without pausing");
                    Entry.Editor.Update(new KeyboardState(), Pointer(200, 176, false));
                    int[] completed = (int[])live.Cells.Clone();
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl, Keys.Z), Pointer(200, 176, false));
                    Check(live.Cells.SequenceEqual(live.Baseline), "one undo removes the entire live gesture");
                    Entry.Editor.Update(new KeyboardState(), Pointer(200, 176, false));
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl, Keys.Y), Pointer(200, 176, false));
                    Check(live.Cells.SequenceEqual(completed), "one redo restores the whole live gesture");
                    Entry.Editor.Update(new KeyboardState(), Pointer(320, 160, true));
                    Check(Terrain.Solid(live.Cells, 320, 160), "a second live stroke reaches collision immediately");
                    Entry.Editor.Update(new KeyboardState(), new MouseState(160 + 320 * 2, 160 * 2, 0, Microsoft.Xna.Framework.Input.ButtonState.Pressed, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Pressed, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released));
                    Check(live.Cells.SequenceEqual(completed), "right-click cancels and rolls back an unfinished live stroke");
                    Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, false));
                    Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, true));
                    Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, false));
                    Check(live.Cells.SequenceEqual(completed), "the visible toolbar consumes clicks in its empty space");
                    KingControls(live, completed);
                    Entry.Editor.Update(new KeyboardState(Keys.D1), Pointer(104, 168, false));
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl), Pointer(104, 168, true));
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl), Pointer(152, 208, true));
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl), Pointer(152, 208, false));
                    Check(Terrain.Solid(live.Cells, 104, 168) && Terrain.Solid(live.Cells, 152, 208), "Ctrl + pointer drag copies a platform and leaves its source");
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl, Keys.Z), Pointer(152, 208, false));
                    Check(live.Cells.SequenceEqual(completed), "one undo removes the complete pointer copy");
                    Entry.Status = "";
                    Entry.Editor.Update(new KeyboardState(), Pointer(104, 168, true));
                    Entry.Editor.Update(new KeyboardState(), Pointer(472, 168, true));
                    Check(string.IsNullOrEmpty(Entry.Status) && Terrain.Solid(live.Cells, 479, 168) && !Terrain.Solid(live.Cells, 104, 168),
                        "dragging into the right edge clips the native platform without an error");
                    Entry.Editor.Update(new KeyboardState(), Pointer(304, 168, true));
                    Check(Terrain.Solid(live.Cells, 400, 168), "dragging back from the edge restores cells cropped during the same gesture");
                    Entry.Editor.Update(new KeyboardState(), Pointer(472, 168, false));
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl, Keys.Z), Pointer(472, 168, false));
                    Check(live.Cells.SequenceEqual(completed), "one pointer undo restores the full platform after an edge crop");
                    Entry.Editor.Update(new KeyboardState(Keys.D2), Pointer(12, 16, false));
                    Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, true));
                    Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, false));
                    Entry.Editor.Update(new KeyboardState(), Pointer(320, 232, true));
                    Entry.Editor.Update(new KeyboardState(), Pointer(360, 240, true));
                    Entry.Editor.Update(new KeyboardState(), Pointer(360, 240, false));
                    var drawnSlopes = ((IBlock[])ScreenEdit.Blocks.GetValue(nativeScreen)).OfType<SlopeBlock>().ToArray();
                    Check(drawnSlopes.Length == 6 && drawnSlopes.All(b => b.GetRect().Width == 8 && b.GetRect().Height == 8),
                        "the Shape control and an uneven mouse drag install square native slope cells");
                    Check(live.VisualCells.Count(i => i != 0) == completed.Count(i => i != 0) + 48 * 49 / 2,
                        "the mouse gesture draws one complete 45-degree triangle");
                    Entry.Editor.Update(new KeyboardState(Keys.LeftControl, Keys.Z), Pointer(360, 240, false));
                    Check(live.Cells.SequenceEqual(completed), "one undo removes both the diagonal and its foundation");
                    for (int n = 0; n < 4; n++)
                    {
                        Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, true));
                        Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, false));
                    }
                    using (var target = new RenderTarget2D(device, 1280, 720))
                    using (var draw = new SpriteBatch(device))
                    {
                        device.SetRenderTarget(target); device.Clear(Color.Black);
                        draw.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                        var graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(nativeScreen);
                        draw.Draw(graphics.background, new Rectangle(160, 0, 960, 720), Color.White); draw.End();
                        var reference = new Color[1280 * 720]; target.GetData(reference);
                        Entry.Editor.Draw();
                        var pixels = new Color[1280 * 720]; target.GetData(pixels);
                        SavePixels(pixels, Path.Combine(output, "editor-live.png"), 1280, 720);
                        var icons = new Color[100 * 20];
                        var iconTypes = new[] { SlopeType.None, SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight };
                        for (int icon = 0; icon < iconTypes.Length; icon++)
                        {
                            if (icon != 0)
                            {
                                Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, true));
                                Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, false));
                            }
                            device.Clear(Color.Black); Entry.Editor.Draw(); target.GetData(pixels);
                            var expected = new JKRuntime.Geometry.VisualShape(JKRuntime.Geometry.VisualGeometry.DeclaredSlope(new Rectangle(0, 0, 14, 14), iconTypes[icon]));
                            bool correct = true;
                            for (int y = 0; y < 14; y++) for (int x = 0; x < 14; x++)
                            {
                                var c = pixels[(31 + y) * 1280 + 606 + x]; icons[(y + 3) * 100 + icon * 20 + x + 3] = c;
                                if ((c.R > 180) != expected.Contains(new Vector2(x + .5f, y + .5f))) correct = false;
                            }
                            Check(correct, "shape icon matches the actual filled orientation: " + iconTypes[icon]);
                        }
                        SavePixels(icons, Path.Combine(output, "shape-icons.png"), 100, 20);
                        Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, true));
                        Entry.Editor.Update(new KeyboardState(), Pointer(205, 19, false));
                        device.Clear(Color.Black); draw.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone); draw.Draw(graphics.background, new Rectangle(160, 0, 960, 720), Color.White); draw.End();
                        Entry.Editor.Draw(); target.GetData(pixels);
                        Check(pixels.Any(p => p.R > 210 && p.G > 160 && p.B < 100), "window overlay renders native tool controls");
                        Check(pixels.Skip(600 * 1280).SequenceEqual(reference.Skip(600 * 1280)), "the editor leaves the bottom of the viewport untouched");
                        Entry.Editor.Update(new KeyboardState(Keys.Tab), Pointer(12, 16, false));
                        device.Clear(Color.Black); draw.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone); draw.Draw(graphics.background, new Rectangle(160, 0, 960, 720), Color.White); draw.End();
                        Entry.Editor.Draw(); target.GetData(pixels);
                        Check(pixels.SequenceEqual(reference), "Tab removes the whole toolbar without leaving a handle or status bar");
                        SavePixels(pixels, Path.Combine(output, "editor-hidden.png"), 1280, 720);
                        Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, true));
                        Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, false));
                        Check(Terrain.Solid(live.Cells, 12, 16), "the hidden toolbar no longer intercepts drawing over its former area");
                        Entry.Editor.Update(new KeyboardState(Keys.LeftControl, Keys.Z), Pointer(12, 16, false));
                        Check(live.Cells.SequenceEqual(completed), "undo remains active while the toolbar is hidden");
                        Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, false));
                        Entry.Editor.Update(new KeyboardState(Keys.Tab), Pointer(12, 16, false));
                        device.Clear(Color.Black); draw.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone); draw.Draw(graphics.background, new Rectangle(160, 0, 960, 720), Color.White); draw.End();
                        Entry.Editor.Draw(); target.GetData(pixels);
                        Check(!pixels.SequenceEqual(reference), "a second Tab restores the native toolbar");
                        device.SetRenderTarget(null);
                    }
                }
                finally { Entry.End(); Entry.Editor.Dispose(); Entry.Editor = null; Entry.World.Dispose(); Entry.World = null; fixture.UnpatchAll("god.fixture.window"); }
                using (var restored = new World())
                {
                    restored.RestoreSaved();
                    Check(Terrain.Solid(restored.Get(0).Cells, 104, 160), "completed live strokes restore on the same source map");
                    Check(!Terrain.Solid(restored.Get(0).Cells, 320, 160), "cancelled strokes never enter the saved map");
                }
                LinkedScreenEditing(output);
            }
        }
    }
    private static MouseState Pointer(int x, int y, bool down)
    { return new MouseState(160 + x * 2, y * 2, 0, down ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released); }
    private static bool CursorVisible(ref bool __result) { __result = false; return false; }
    private static bool SetCursorVisible() { return false; }
    private static void LinkedScreenEditing(string output)
    {
        var camera = typeof(Camera).GetField("_current_screen", Flags);
        var total = typeof(LevelManager).GetField("_total_screens", Flags); var oldTotal = total.GetValue(null);
        var oldScreen = camera.GetValue(null); var oldScreens = ScreenEdit.Screens.GetValue(null);
        var screens = new LevelScreen[6];
        for (int i = 0; i < screens.Length; i++)
        {
            var links = new[] { new TeleportLink(), new TeleportLink() };
            JKRuntime.World.NativeSideLinks.SetPair(links, i == 2 ? 2 : i == 5 ? 5 : 0, i == 1 ? 3 : i == 4 ? 6 : 0);
            screens[i] = new LevelScreen(i, new IBlock[] {
                new BoxBlock(new Rectangle(0, -i * 360, 480, 8)), new BoxBlock(new Rectangle(0, 352 - i * 360, 480, 8)),
                new BoxBlock(new Rectangle(96, 120 - i * 360, 32, 8)) }, new LevelScreen.Graphics(), false, links, 0, null);
        }
        ScreenEdit.Screens.SetValue(null, screens); total.SetValue(null, screens.Length); JKRuntime.Geometry.MapTopology.Invalidate();
        Storage.Initialize(Path.Combine(output, "linked-screen-storage"));
        Entry.World = new World(); Entry.Editor = new Editor();
        try
        {
            foreach (bool presented in new[] { false, true })
            using (presented ? FrameComposition.RegisterPresenter(() => true, point =>
                Camera.TransformVector2(JKRuntime.Geometry.MapTopology.ChartPosition(new Vector2(240, 180 - Camera.CurrentScreen * 360), point)) + new Vector2(12, -8)) : null)
            foreach (int index in new[] { 0, 1, 2, 4, 5 })
            {
                camera.SetValue(null, index);
                int dx = presented ? 12 : 0, dy = presented ? -8 : 0;
                Entry.Editor.Begin(index); Entry.Editor.Seed(new KeyboardState(), Pointer(104 + dx, 124 + dy, false));
                Entry.Editor.Update(new KeyboardState(Keys.D1), Pointer(104 + dx, 124 + dy, false));
                var point = (Point)typeof(Editor).GetMethod("Pointer", Flags).Invoke(Entry.Editor, null);
                var hover = (Region)typeof(Editor).GetMethod("Pick", Flags).Invoke(Entry.Editor, new object[] { point });
                Check(hover != null && hover.Bounds == new Rectangle(96, 120, 32, 8), "linked-screen hover stays on the visible platform: " + index + "/" + presented);
                var edit = Entry.World.Get(index);
                Entry.Editor.Update(new KeyboardState(Keys.D2), Pointer(200 + dx, 200 + dy, false));
                Entry.Editor.Update(new KeyboardState(), Pointer(200 + dx, 200 + dy, true));
                Entry.Editor.Update(new KeyboardState(), Pointer(200 + dx, 200 + dy, false));
                Check(Terrain.Solid(edit.Cells, 200, 200), "linked-screen drawing reaches the visible screen: " + index + "/" + presented);
                Entry.Editor.Update(new KeyboardState(Keys.D3), Pointer(200 + dx, 200 + dy, false));
                Entry.Editor.Update(new KeyboardState(), Pointer(200 + dx, 200 + dy, true));
                Entry.Editor.Update(new KeyboardState(), Pointer(200 + dx, 200 + dy, false));
                Check(!Terrain.Solid(edit.Cells, 200, 200), "linked-screen erasing reaches the visible screen: " + index + "/" + presented);
            }
        }
        finally
        {
            Entry.Editor.Dispose(); Entry.Editor = null; Entry.World.Dispose(); Entry.World = null;
            camera.SetValue(null, oldScreen); total.SetValue(null, oldTotal); ScreenEdit.Screens.SetValue(null, oldScreens); JKRuntime.Geometry.MapTopology.Invalidate();
        }
    }
    private static void LayerUpdates(GraphicsDevice device)
    {
        using (var mid = new Texture2D(device, 480, 360))
        using (var front = new Texture2D(device, 480, 360))
        using (var animated = new Texture2D(device, 480, 360))
        {
            int[] mask; List<Material> unused; var image = Donor(out mask, out unused); mid.SetData(image);
            var fg = new Color[Terrain.Count];
            for (int x = 16; x < 64; x++) { fg[10 * 480 + x] = Color.Lime; fg[15 * 480 + x] = Color.Gold; }
            front.SetData(fg); var phase = (Color[])fg.Clone(); phase[10 * 480 + 20] = Color.Red; animated.SetData(phase);
            var screen = new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(16, 16, 48, 24)) }, new LevelScreen.Graphics { background = mid, foreground = front }, false, new TeleportLink[0], 0, null);
            ScreenEdit.Screens.SetValue(null, new[] { screen });
            Storage.Preferences.TransferForeground = false;
            using (var edit = new ScreenEdit(screen))
            {
                var next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(100, 80, 48, 24), 1); edit.Apply(next, false);
                var graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen);
                Check(ReferenceEquals(graphics.foreground, front), "default edits leave the original foreground texture untouched");
                graphics.foreground = animated; ScreenEdit.Graphics.SetValue(screen, graphics);
                next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(160, 80, 16, 24), 1); edit.Apply(next, false);
                Check(ReferenceEquals(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).foreground, animated), "an animated foreground owner can replace its texture while transfer is disabled");
                Storage.Preferences.TransferForeground = true; edit.Apply(edit.Cells, false);
                var pixels = new Color[Terrain.Count];
                for (int n = 0; n < 12; n++)
                {
                    next = (int[])edit.Baseline.Clone(); Terrain.Paint(next, new Rectangle(100 + n * 8, 80, 48, 24), 1);
                    if (n % 3 == 0) Terrain.Paint(next, new Rectangle(16, 16, 8, 24), 0);
                    edit.Apply(next, false); graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen);
                    graphics.background.GetData(pixels);
                    Check(pixels.SequenceEqual(PlatformArt.Compose(image, edit.VisualBaseline, edit.VisualCells, edit.Materials, true)), "regional midground equals a full rebuild across alternating GPU buffers");
                    graphics.foreground.GetData(pixels);
                    Check(pixels.SequenceEqual(PlatformArt.WithDecor(phase, edit.VisualBaseline, edit.VisualCells, edit.Materials, 26, true)), "regional foreground equals the original replacement and erasure rules");
                }
                var midLayer = (LayerSurface)typeof(ScreenEdit).GetField("midSurface", Flags).GetValue(edit);
                var fgLayer = (LayerSurface)typeof(ScreenEdit).GetField("frontSurface", Flags).GetValue(edit);
                Check(midLayer.CreatedTextures == 2 && fgLayer.CreatedTextures == 2, "continuous editing reuses two textures per owned layer");
                Check(midLayer.UploadedPixels < Terrain.Count * 6 && fgLayer.UploadedPixels < Terrain.Count * 6, "small movements upload dirty rectangles instead of whole screens");
                var blocks = ScreenEdit.Blocks.GetValue(screen); long generation = RuntimeApi.Geometry.Generation;
                Storage.Preferences.TransferForeground = false; edit.Apply(edit.Cells, false);
                Check(ReferenceEquals(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).foreground, animated)
                    && ReferenceEquals(ScreenEdit.Blocks.GetValue(screen), blocks) && RuntimeApi.Geometry.Generation == generation,
                    "disabling foreground restores the live owner without rebuilding collision or invalidating geometry");
                using (var invalid = new Texture2D(device, 8, 8))
                {
                    graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen); var liveMid = graphics.background;
                    graphics.foreground = invalid; ScreenEdit.Graphics.SetValue(screen, graphics);
                    Storage.Preferences.TransferForeground = true;
                    var failed = (int[])edit.Cells.Clone(); Terrain.Paint(failed, new Rectangle(400, 280, 32, 24), 1);
                    Reject(() => edit.Apply(failed, false), "a foreground read failure aborts a staged texture update");
                    graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen);
                    Check(ReferenceEquals(graphics.background, liveMid) && ReferenceEquals(ScreenEdit.Blocks.GetValue(screen), blocks) && edit.Cells.SequenceEqual(next), "a staged failure leaves live art and collision intact");
                    graphics.foreground = animated; ScreenEdit.Graphics.SetValue(screen, graphics);
                    var retry = (int[])edit.Cells.Clone(); Terrain.Paint(retry, new Rectangle(300, 160, 24, 24), 1);
                    edit.Apply(retry, false); graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen); graphics.background.GetData(pixels);
                    Check(pixels.SequenceEqual(PlatformArt.Compose(image, edit.VisualBaseline, edit.VisualCells, edit.Materials, true)), "retrying a different edit repairs pixels staged by the failed operation");
                    Storage.Preferences.TransferForeground = false; edit.Apply(edit.Cells, false);
                }
                phase[10 * 480 + 20] = Color.Blue; animated.SetData(phase);
                Storage.Preferences.TransferForeground = true; edit.Apply(edit.Cells, false);
                graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen); graphics.foreground.GetData(pixels);
                Check(pixels.SequenceEqual(PlatformArt.WithDecor(phase, edit.VisualBaseline, edit.VisualCells, edit.Materials, 26, true)), "reenabling samples the current animation frame rather than an old snapshot");
                edit.Apply(edit.Baseline, false);
                Check(ReferenceEquals(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).background, mid), "reset restores the source texture with pooled buffers still allocated");
                edit.Apply(next, false); graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen); graphics.background.GetData(pixels);
                Check(pixels.SequenceEqual(PlatformArt.Compose(image, edit.VisualBaseline, edit.VisualCells, edit.Materials, true)), "an edit after reset cannot reveal stale pixels from an old buffer");
            }
            Check(ReferenceEquals(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).foreground, animated) && !animated.IsDisposed, "teardown returns the borrowed animation texture without disposing it");
            Storage.Preferences.TransferForeground = false;
        }
    }
    private static void ProfileScreenVisits(ContentManager content, string output)
    {
        var level = JumpKing.Level.Sampler.LevelTexture.FromTexture(content.Load<Texture2D>("Content/level"));
        var loader = typeof(LevelManager).GetMethod("LoadBlocksInterval", Flags);
        var lines = new List<string>();
        foreach (int index in new[] { 2, 5 })
        {
            var mid = content.Load<Texture2D>("Content/screens/midground/" + (index + 1));
            var front = content.Load<Texture2D>("Content/screens/foreground/fg" + (index + 1));
            object[] args = { level, null, index, false, null, 0f, null };
            var screen = new LevelScreen(index, (IBlock[])loader.Invoke(null, args), new LevelScreen.Graphics { background = mid, foreground = front }, false, new TeleportLink[0], 0, null);
            var screens = new LevelScreen[index + 1]; screens[index] = screen; ScreenEdit.Screens.SetValue(null, screens);
            double cold = 0, revisit = 0, firstEdit = 0;
            for (int n = 0; n < 9; n++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                using (var edit = new ScreenEdit(screen))
                {
                    var regions = edit.Regions;
                    if (n != 0) cold += watch.Elapsed.TotalMilliseconds;
                    watch.Restart();
                    for (int visit = 0; visit < 10; visit++) regions = edit.Regions;
                    if (n != 0) revisit += watch.Elapsed.TotalMilliseconds / 10;
                    if (n == 0)
                    {
                        Check(edit.Signature == LegacySignature(edit, mid, front), "deferred artwork keeps existing saved-screen fingerprints compatible");
                        Check(ReferenceEquals(regions, edit.Regions), "preparing artwork doesn't rebuild selection geometry");
                    }
                    var platform = regions.First(r => !r.Wall && r.Slope == SlopeType.None);
                    var next = SlopePlatforms.Move(edit, edit.Cells, platform, new Point(8, -8), false, Rectangle.Empty, false);
                    watch.Restart(); edit.Apply(next, false);
                    if (n != 0) firstEdit += watch.Elapsed.TotalMilliseconds;
                }
            }
            lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "screen {0}: first visit={1:F3} ms, revisit={2:F3} ms, first edit={3:F3} ms", index + 1, cold / 8, revisit / 8, firstEdit / 8));
        }
        File.WriteAllLines(Path.Combine(output, "screen-visits-performance.txt"), lines);
        foreach (string line in lines) Console.WriteLine("[PERF] " + line);
    }
    private static string LegacySignature(ScreenEdit edit, Texture2D mid, Texture2D front)
    {
        using (var bytes = new MemoryStream()) using (var writer = new BinaryWriter(bytes))
        {
            writer.Write("typed-walls-decor-v2");
            foreach (int id in edit.Baseline) writer.Write(id);
            // v0.3.3 hashed base donors and the blank, before adding synthetic slopes
            foreach (var material in edit.Materials.Take(edit.Baseline.Max() + 1)) writer.Write(material.Template.Key ?? "");
            var pixels = new Color[Terrain.Count];
            mid.GetData(pixels); foreach (var color in pixels) writer.Write(color.PackedValue);
            front.GetData(pixels); foreach (var color in pixels) writer.Write(color.PackedValue);
            foreach (var block in (IBlock[])ScreenEdit.Blocks.GetValue(edit.Screen))
            {
                writer.Write(block.GetType().AssemblyQualifiedName); var r = block.GetRect();
                writer.Write(r.X); writer.Write(r.Y); writer.Write(r.Width); writer.Write(r.Height);
            }
            writer.Flush(); return Storage.Hash(bytes.ToArray());
        }
    }
    private static void ProfileEditing(ContentManager content, GraphicsDevice device, string output)
    {
        var mid = content.Load<Texture2D>("Content/screens/midground/3");
        var front = content.Load<Texture2D>("Content/screens/foreground/fg3");
        var level = JumpKing.Level.Sampler.LevelTexture.FromTexture(content.Load<Texture2D>("Content/level"));
        var loader = typeof(LevelManager).GetMethod("LoadBlocksInterval", Flags);
        object[] args = { level, null, 2, false, null, 0f, null };
        var screen = new LevelScreen(2, (IBlock[])loader.Invoke(null, args), new LevelScreen.Graphics { background = mid, foreground = front }, false, new TeleportLink[0], 0, null);
        var screens = new LevelScreen[3]; screens[2] = screen; ScreenEdit.Screens.SetValue(null, screens);
        using (var edit = new ScreenEdit(screen))
        using (var target = new RenderTarget2D(device, 480, 360))
        using (var batch = new SpriteBatch(device))
        {
            var region = SlopePlatforms.Regions(edit, edit.Cells, edit.VisualCells).Where(r => !r.Wall && r.Slope == SlopeType.None).OrderByDescending(r => r.Pixels.Count).First();
            var midPixels = new Color[Terrain.Count]; mid.GetData(midPixels); var fgPixels = new Color[Terrain.Count]; front.GetData(fgPixels);
            var reports = new List<string>();
            foreach (bool transfer in new[] { false, true })
            {
            Storage.Preferences.TransferForeground = transfer;
            var times = new List<double>(); double compose = 0, decor = 0, mask = 0, gesture = 0;
            for (int n = 0; n < 20; n++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var next = SlopePlatforms.Move(edit, edit.Baseline, region, new Point((n % 8 - 4) * 8, -40), false, Rectangle.Empty, false);
                gesture += watch.Elapsed.TotalMilliseconds; watch.Restart();
                var visible = edit.Visualize(next); mask += watch.Elapsed.TotalMilliseconds; watch.Restart();
                PlatformArt.Compose(midPixels, edit.VisualBaseline, visible, edit.Materials, true); compose += watch.Elapsed.TotalMilliseconds; watch.Restart();
                PlatformArt.WithDecor(fgPixels, edit.VisualBaseline, visible, edit.Materials, 26, true); decor += watch.Elapsed.TotalMilliseconds;
                // keep the previous edited texture in a real GPU draw before the next update
                device.SetRenderTarget(target); batch.Begin();
                var graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen);
                batch.Draw(graphics.background, Vector2.Zero, Color.White);
                batch.Draw(graphics.foreground, new Vector2(n % 4, 0), Color.White); batch.End(); device.SetRenderTarget(null);
                watch.Restart(); edit.Apply(next, false); if (n >= 4) times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "screen 3, moving native platform with an animated foreground draw; transfer={6}\napply mean={0:F3} ms p95={1:F3} ms\ngesture={2:F3} visual-mask={3:F3} full-midground={4:F3} full-foreground={5:F3} ms\n",
                times.Average(), times[(int)(times.Count * .95)], gesture / 20, mask / 20, compose / 20, decor / 20, transfer);
            reports.Add(report); Console.WriteLine("[PERF] " + report.Replace('\n', ' '));
            }
            Storage.Preferences.TransferForeground = false;
            File.WriteAllText(Path.Combine(output, "editing-performance.txt"), string.Join("\n", reports));
        }
    }
    private static void NativeLayers(ContentManager content, string output)
    {
        var mid = content.Load<Texture2D>("Content/screens/midground/6");
        var front = content.Load<Texture2D>("Content/screens/foreground/fg6");
        var background = content.Load<Texture2D>("Content/screens/background/bg6");
        var backdrop = new Color[Terrain.Count]; background.GetData(backdrop);
        Check(backdrop.All(c => c.A == 255), "screen six has a fully opaque native background beneath its platforms");
        var texture = JumpKing.Level.Sampler.LevelTexture.FromTexture(content.Load<Texture2D>("Content/level"));
        var loader = typeof(LevelManager).GetMethod("LoadBlocksInterval", Flags);
        object[] args = { texture, null, 5, false, null, 0f, null };
        var blocks = (IBlock[])loader.Invoke(null, args);
        var screen = new LevelScreen(5, blocks, new LevelScreen.Graphics { background = mid, foreground = front, backbackground = background }, false, new TeleportLink[0], 0, null);
        var screens = new LevelScreen[6]; screens[5] = screen; ScreenEdit.Screens.SetValue(null, screens);
        using (var edit = new ScreenEdit(screen))
        {
            var regions = SlopePlatforms.Regions(edit, edit.Cells, edit.VisualCells);
            File.WriteAllText(Path.Combine(output, "native-slope-groups.txt"), "Screen 6: " + blocks.OfType<SlopeBlock>().Count() + " native slopes; " + regions.Count(r => r.Slope != SlopeType.None) + " ramp groups");
            var body = regions.Where(r => !r.Wall && r.Pixels.All(i => edit.Materials[edit.Cells[i] - 1].Template.Source is BoxBlock)).OrderByDescending(r => r.Pixels.Count).First();
            var next = (int[])edit.Cells.Clone(); foreach (int i in body.Pixels) next[i] = 0; edit.Apply(next, false);
            var graphics = (LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen);
            Check(ReferenceEquals(graphics.backbackground, background), "erasing a real authored platform leaves Background untouched");
            var pixels = new Color[Terrain.Count]; graphics.background.GetData(pixels);
            Check(body.Pixels.All(i => pixels[i].A == 0), "erasing clears the platform's midground pixels instead of painting black");
            for (int i = 0; i < pixels.Length; i++) if (pixels[i].A == 0) pixels[i] = backdrop[i];
            SavePixels(pixels, Path.Combine(output, "screen-six-erased.png"));
        }
    }
    private static void TypedWorld()
    {
        var blocks = new IBlock[] { new BoxBlock(new Rectangle(0, 0, 16, 360)), new SnowBlock(new Rectangle(16, 80, 16, 8)), new IceBlock(new Rectangle(32, 80, 16, 8)), new CustomBlock(new Rectangle(64, 80, 4, 4), .7f) };
        var screen = new LevelScreen(0, blocks, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
        ScreenEdit.Screens.SetValue(null, new[] { screen });
        using (var edit = new ScreenEdit(screen))
        {
            Check(edit.CellSize == 4 && edit.Untextured, "a texture-free map derives reduced collision cells from loaded geometry");
            var regions = Terrain.Regions(edit.Cells, edit.Kind);
            Check(regions.Count == 4 && regions.Count(r => r.Wall) == 1, "bare maps separate touching snow, ice and walls");
            int snow = edit.Cells[80 * 480 + 16], custom = edit.Cells[80 * 480 + 64];
            var next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(120, 160, 4, 4), snow); Terrain.Paint(next, new Rectangle(160, 160, 4, 4), custom);
            edit.Apply(next, false);
            var installed = (IBlock[])ScreenEdit.Blocks.GetValue(screen);
            Check(installed.Any(b => b is SnowBlock && b.GetRect() == new Rectangle(120, 160, 4, 4)), "a one-cell snow copy stays snow without artwork");
            Check(installed.Any(b => b is CustomBlock && b.GetRect() == new Rectangle(160, 160, 4, 4)), "live terrain compilation retains a foreign block's class");
            Check(installed.Contains(blocks[0]) && installed.Contains(blocks[2]), "unmodified wall and ice instances remain owned by the game");
            Check(((LevelScreen.Graphics)ScreenEdit.Graphics.GetValue(screen)).background == null, "bare maps retain the native automatic debug drawing path after editing");
            var encoded = Storage.Encode(next); edit.Apply(edit.Baseline, false); edit.Apply(Storage.Decode(encoded, edit.Materials.Count), false);
            Check(((IBlock[])ScreenEdit.Blocks.GetValue(screen)).Any(b => b is SnowBlock && b.GetRect().X == 120), "saved typed masks reconstruct their sampled block behavior");
        }
        var empty = new LevelScreen(0, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
        ScreenEdit.Screens.SetValue(null, new[] { empty });
        using (var edit = new ScreenEdit(empty))
        {
            int donor = Material.Choose(edit.Materials, new Rectangle(80, 80, 8, 8));
            var next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(80, 80, 8, 8), donor); edit.Apply(next, false);
            Check(((IBlock[])ScreenEdit.Blocks.GetValue(empty)).Single().GetType() == typeof(BoxBlock), "an empty untextured screen can create its first solid cell");
        }
    }
    private static void SlopeWorld()
    {
        foreach (SlopeType type in new[] { SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
        {
            var block = new SlopeBlock(new Rectangle(16, 16, 8, 8), type);
            var screen = new LevelScreen(0, new IBlock[] { block }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
            ScreenEdit.Screens.SetValue(null, new[] { screen });
            using (var edit = new ScreenEdit(screen))
            {
                Check(edit.VisualCells.Count(i => i != 0) == 36, "debug slope has a filled triangle with inclusive diagonal: " + type);
                int id = edit.Cells[16 * 480 + 16];
                var next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(80, 80, 16, 16), id); edit.Apply(next, false);
                var moved = ((IBlock[])ScreenEdit.Blocks.GetValue(screen)).OfType<SlopeBlock>().Single(b => b.GetRect().X == 80);
                Check(moved.GetSlopeType() == type && edit.VisualCells.Count(i => i != 0) == 36 + 136, "resized slopes stay triangular native records: " + type);
                if (type == SlopeType.BottomLeft)
                    Check(edit.VisualCells[80 * 480 + 95] == id && edit.VisualCells[95 * 480 + 80] == 0, "southwest slope fills the upper-right half");
                Check(block.GetRect() == new Rectangle(16, 16, 8, 8), "slope donor isn't moved by a copy");
            }
        }
    }
    private static void ResetWorld(string output)
    {
        string previous = Storage.Root; Storage.Initialize(Path.Combine(output, "reset-storage"));
        var screens = new[] {
            new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(16, 80, 8, 8)) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
            new LevelScreen(1, new IBlock[] { new BoxBlock(new Rectangle(16, -280, 8, 8)) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) };
        ScreenEdit.Screens.SetValue(null, screens);
        try
        {
            using (var world = new World())
            {
                foreach (int index in new[] { 0, 1 })
                {
                    var edit = world.Get(index); var next = (int[])edit.Cells.Clone(); Terrain.Paint(next, new Rectangle(80, 80, 8, 8), 1);
                    edit.Apply(next, false); world.Save(edit);
                }
                var a = (int[])world.Get(0).Cells.Clone(); var b = (int[])world.Get(1).Cells.Clone();
                world.ResetAll(); Check(!world.Changed, "Reset clears edits on every prepared screen");
                Check(world.HistoryReset(false) && world.Get(0).Cells.SequenceEqual(a) && world.Get(1).Cells.SequenceEqual(b), "one undo restores all reset screens");
                Check(world.HistoryReset(true) && !world.Changed, "one redo clears all reset screens again");
                world.HistoryReset(false);
                var path = (string)typeof(World).GetField("path", Flags).GetValue(world);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    Reject(world.ResetAll, "a failed reset save rolls back live geometry");
                Check(world.Get(0).Cells.SequenceEqual(a) && world.Get(1).Cells.SequenceEqual(b), "save failure leaves both edited screens intact");
                var installed = ScreenEdit.Blocks.GetValue(screens[1]); var foreign = new IBlock[0]; ScreenEdit.Blocks.SetValue(screens[1], foreign);
                Reject(world.ResetAll, "a foreign change prevents a partial map reset");
                Check(world.Get(0).Cells.SequenceEqual(a) && ReferenceEquals(ScreenEdit.Blocks.GetValue(screens[1]), foreign), "reset rollback preserves earlier edits and the foreign block array");
                ScreenEdit.Blocks.SetValue(screens[1], installed);
                world.ResetAll();
            }
            using (var restored = new World()) { restored.RestoreSaved(); Check(!restored.Changed, "a successful global reset stays cleared on reload"); }
        }
        finally { Storage.Initialize(previous); }
    }
    private static void SlopeGroups()
    {
        foreach (var type in new[] { SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
        {
            var bounds = new Rectangle(32, 32, 16, 16);
            var shape = new JKRuntime.Geometry.VisualShape(JKRuntime.Geometry.VisualGeometry.DeclaredSlope(bounds, type));
            var blocks = new List<IBlock>(); Rectangle foundation = Rectangle.Empty;
            for (int y = 32; y < 48; y += 8) for (int x = 32; x < 48; x += 8)
            {
                int filled = 0;
                foreach (int dy in new[] { 0, 7 }) foreach (int dx in new[] { 0, 7 }) if (shape.Contains(new Vector2(x + dx + .5f, y + dy + .5f))) filled++;
                var r = new Rectangle(x, y, 8, 8);
                if (filled == 4) { blocks.Add(new BoxBlock(r)); foundation = r; }
                else if (filled != 0) blocks.Add(new SlopeBlock(r, type));
            }
            var adjacent = new Rectangle(foundation.X == 32 ? 24 : 48, foundation.Y, 8, 8); blocks.Add(new BoxBlock(adjacent));
            var screen = new LevelScreen(0, blocks.ToArray(), new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
            ScreenEdit.Screens.SetValue(null, new[] { screen });
            using (var edit = new ScreenEdit(screen))
            {
                var regions = edit.Regions;
                var ramp = regions.Single(r => r.Slope == type);
                Check(ramp.Bounds == bounds && ramp.Pixels.Count == 192 && regions.Single(r => r.Slope == SlopeType.None).Bounds == adjacent,
                    "a diagonal chain owns only its triangular foundation, not the attached platform: " + type);
                var copy = Terrain.Move(edit.Cells, ramp, new Point(64, 64), false, Rectangle.Empty, true); edit.Apply(copy, false);
                Check(!ReferenceEquals(regions, edit.Regions) && edit.Regions.Count(r => r.Slope == type) == 2,
                    "cached selection includes both slope foundations after copying: " + type);
                Check(edit.VisualCells.Count(i => i != 0) == 136 * 2 + 64 && Terrain.Solid(edit.Cells, foundation.X, foundation.Y), "Ctrl-copy keeps the donor and copies the whole ramp: " + type);
                foreach (var direction in new[] { SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
                {
                    int id = SlopePlatforms.Choose(edit, new Rectangle(160, 160, 16, 16), 0, direction, copy);
                    var material = edit.Materials[id - 1];
                    Check(SlopePlatforms.Type(material) == direction && !material.Untextured, "all orientations choose slope art, including mirrored donors: " + direction);
                    var paint = (int[])copy.Clone(); Terrain.Paint(paint, new Rectangle(160, 160, 16, 16), id); edit.Apply(paint, false);
                    Check(edit.VisualCells.Count(i => i != 0) == 136 * 3 + 64, "directional drawing keeps a triangular mask: " + direction);
                }
            }
        }
    }
    private static void DiagonalSlopes()
    {
        var legacy = new LevelScreen(0, new IBlock[] { new SlopeBlock(new Rectangle(32, 32, 16, 8), SlopeType.TopRight) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
        ScreenEdit.Screens.SetValue(null, new[] { legacy });
        using (var edit = new ScreenEdit(legacy))
        {
            var region = SlopePlatforms.Regions(edit, edit.Cells, edit.VisualCells).Single();
            var moved = SlopePlatforms.Move(edit, edit.Cells, region, new Point(64, 64), false, Rectangle.Empty, false); edit.Apply(moved, false);
            Check(edit.VisualCells.Count(i => i != 0) == 136, "moving an old stretched slope normalizes it instead of failing the square constraint");
        }
        foreach (var end in new[] { new Point(120, 104), new Point(104, 120), new Point(72, 96), new Point(104, 72) })
        {
            var r = SlopePlatforms.DrawBounds(new Point(104, 104), end, 8);
            Check(r.Width == r.Height && r.Width % 8 == 0, "uneven pointer drags still make square slope bounds");
        }
        foreach (var type in new[] { SlopeType.TopLeft, SlopeType.TopRight, SlopeType.BottomLeft, SlopeType.BottomRight })
        {
            var screen = new LevelScreen(0, new IBlock[] { new SlopeBlock(new Rectangle(32, 32, 32, 32), type) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
            ScreenEdit.Screens.SetValue(null, new[] { screen });
            using (var edit = new ScreenEdit(screen))
            {
                var region = SlopePlatforms.Regions(edit, edit.Cells, edit.VisualCells).Single(r => r.Slope == type);
                foreach (var offset in new[] { new Point(-48, 0), new Point(432, 0), new Point(0, -48), new Point(0, 312), new Point(-48, -48), new Point(432, 312) })
                {
                    var next = SlopePlatforms.Move(edit, edit.Baseline, region, offset, false, Rectangle.Empty, false); edit.Apply(next, false);
                    var expectedBounds = region.Bounds; expectedBounds.Offset(offset.X, offset.Y);
                    var expected = new JKRuntime.Geometry.VisualShape(JKRuntime.Geometry.VisualGeometry.DeclaredSlope(expectedBounds, type));
                    bool matches = true;
                    for (int y = 0; y < 360 && matches; y++) for (int x = 0; x < 480; x++)
                        if ((edit.VisualCells[y * 480 + x] != 0) != expected.Contains(new Vector2(x + .5f, y + .5f))) { matches = false; break; }
                    Check(matches, "cropping keeps the original diagonal rather than tilting it: " + type + " " + offset);
                    foreach (var slope in ((IBlock[])ScreenEdit.Blocks.GetValue(screen)).OfType<SlopeBlock>())
                    {
                        var p = JKRuntime.Geometry.VisualGeometry.ReadSlope(slope);
                        Check(p.Zip(p.Skip(1).Concat(p.Take(1)), (a, b) => b - a).All(d => d.X == 0 || d.Y == 0 || Math.Abs(d.X) == Math.Abs(d.Y)), "every installed incline stays at 45 degrees");
                    }
                    var decoded = Storage.Decode(Storage.Encode(next), edit.Materials.Count);
                    edit.Apply(edit.Baseline, false); edit.Apply(decoded, false);
                    Check(edit.Cells.SequenceEqual(next), "cropped diagonal tiles roundtrip through saved terrain");
                }
                var resized = SlopePlatforms.ResizeBounds(region, new Point(16, -8), 8);
                Check(resized.Width == 48 && resized.Height == 48, "asymmetric resize input changes both slope dimensions together");
                var large = SlopePlatforms.Move(edit, edit.Baseline, region, Point.Zero, true, resized, false); edit.Apply(large, false);
                Check(edit.VisualCells.Count(i => i != 0) == 48 * 49 / 2, "resized slope includes its full triangular foundation");
                Check(((IBlock[])ScreenEdit.Blocks.GetValue(screen)).OfType<SlopeBlock>().All(b => b.GetRect().Width == b.GetRect().Height), "resized collision never contains a stretched slope block");
                var drawn = new int[Terrain.Count]; int donor = SlopePlatforms.Choose(edit, resized, 0, type, drawn);
                SlopePlatforms.Paint(edit, drawn, resized, donor); edit.Apply(drawn, false);
                Check(edit.VisualCells.Count(i => i != 0) == 48 * 49 / 2, "directional drawing uses the same diagonal geometry as resize");
                Reject(() => SlopePlatforms.Paint(edit, drawn, new Rectangle(0, 0, 16, 24), donor), "non-square slope construction is rejected below the UI");
                var cut = (int[])edit.Baseline.Clone(); var erased = new Rectangle(32, 32, 16, 32);
                SlopePlatforms.Erase(edit, cut, erased); edit.Apply(cut, false);
                var original = new JKRuntime.Geometry.VisualShape(JKRuntime.Geometry.VisualGeometry.DeclaredSlope(region.Bounds, type));
                bool preserved = true;
                for (int y = 32; y < 64; y++) for (int x = 32; x < 64; x++)
                    if ((edit.VisualCells[y * 480 + x] != 0) != (!erased.Contains(x, y) && original.Contains(new Vector2(x + .5f, y + .5f)))) preserved = false;
                Check(preserved, "erasing part of an old large slope crops its original diagonal");
                var adjacent = new int[Terrain.Count];
                SlopePlatforms.Paint(edit, adjacent, new Rectangle(128, 128, 8, 8), donor);
                SlopePlatforms.Paint(edit, adjacent, new Rectangle(136, 128, 8, 8), donor); edit.Apply(adjacent, false);
                var installedSlopes = ((IBlock[])ScreenEdit.Blocks.GetValue(screen)).OfType<SlopeBlock>().ToArray();
                Check(installedSlopes.Length == 2 && installedSlopes.All(b => b.GetRect().Width == 8 && b.GetRect().Height == 8)
                    && edit.VisualCells.Count(i => i != 0) == 72, "touching slope cells stay separate diagonal colliders");
            }
        }
    }
    private static void KingControls(ScreenEdit edit, int[] terrain)
    {
        var player = (JumpKing.Player.PlayerEntity)FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.PlayerEntity));
        typeof(EntityComponent.Entity).GetField("m_components", Flags).SetValue(player, new List<EntityComponent.Component>());
        var body = new JumpKing.Player.BodyComp(new Vector2(80, 200), 18, 26); player.m_body = body; player.AddComponents(body);
        var beforePlayer = JumpKing.GameManager.GameLoop.m_player;
        var screens = ScreenEdit.Screens.GetValue(null);
        var total = typeof(LevelManager).GetField("_total_screens", Flags); var oldTotal = total.GetValue(null); total.SetValue(null, 3);
        using (var patches = new OwnedPatches("god.fixture.king"))
        {
            KingDrag.Prepare(patches); JumpKing.GameManager.GameLoop.m_player = player;
            try
            {
                Entry.Editor.Update(new KeyboardState(), Pointer(84, 207, false));
                Entry.Editor.Update(new KeyboardState(), Pointer(84, 207, true));
                Check(KingDrag.Active, "a direct left grab selects the king before the draw tool");
                KingDrag.Move(Pointer(84, 207, true));
                Check(body.Position == new Vector2(80, 200), "the native teleport keeps the original grab offset: " + body.Position);
                KingDrag.Move(Pointer(124, 187, true));
                Check(body.Position == new Vector2(120, 180) && body.Velocity == Vector2.Zero, "dragging moves the native body and clears velocity");
                Check(edit.Cells.SequenceEqual(terrain), "grabbing the king doesn't paint terrain underneath");
                Entry.Editor.Update(new KeyboardState(), Pointer(124, 187, false));
                Check(!KingDrag.Active, "releasing left mouse releases the king");
                ScreenEdit.Screens.SetValue(null, new[] { edit.Screen, edit.Screen, edit.Screen });
                var wheel = new MouseState(400, 400, 120, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released);
                KingDrag.SeedWheel(Pointer(124, 187, false)); KingDrag.Wheel(wheel, true);
                Check(Camera.CurrentScreen == 1 && body.Position.Y == -180, "God wheel calls native screen teleport and keeps local height");
                wheel = new MouseState(400, 400, 240, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released);
                KingDrag.Wheel(wheel, false); KingDrag.Wheel(wheel, true);
                Check(Camera.CurrentScreen == 1, "scrolling over the toolbar cannot accumulate a later screen jump");
                Entry.Editor.Cancel();
            }
            finally
            {
                KingDrag.Release(); JumpKing.GameManager.GameLoop.m_player = beforePlayer; ScreenEdit.Screens.SetValue(null, screens);
                total.SetValue(null, oldTotal);
                typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                Entry.Editor.Seed(new KeyboardState(), Pointer(12, 16, false));
                Entry.Editor.Update(new KeyboardState(), Pointer(12, 16, false));
            }
        }
    }
}
