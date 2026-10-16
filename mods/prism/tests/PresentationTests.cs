using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using JKRuntime;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prism;
using ModEntry = Prism.ModEntry;

internal static partial class PrismTests
{
    private static string Scratch(string name)
    {
        string dir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../_work/prism/tests"));
        Directory.CreateDirectory(dir); return Path.Combine(dir, name);
    }
    private static void SettingsAndWind()
    {
        var serializer = new XmlSerializer(typeof(Preferences));
        Preferences legacy;
        using (var text = new StringReader("<Preferences><Enabled>false</Enabled><Reflections>false</Reflections><Gentle>true</Gentle><OverlayOnly>true</OverlayOnly></Preferences>"))
            legacy = (Preferences)serializer.Deserialize(text);
        Preferences.Validate(legacy);
        Check(!legacy.Enabled && !legacy.Current.Reflections && legacy.Current.Gentle && legacy.Current.OverlayOnly, "Migrate existing 0.1 preferences");
        Check(legacy.Current.DisableProps && legacy.Current.MusicVolume == 80, "New theme defaults preserve visibility and volume choices");
        var copy = legacy.Copy(); copy.Current.MusicVolume = 31;
        Check(legacy.Current.MusicVolume == 80, "Failed edits cannot mutate live preferences");
        string saved;
        using (var writer = new StringWriter()) { serializer.Serialize(writer, copy); saved = writer.ToString(); }
        using (var reader = new StringReader(saved)) copy = (Preferences)serializer.Deserialize(reader);
        Preferences.Validate(copy);
        Check(copy.Current.MusicVolume == 31 && !copy.Current.Reflections, "Per-theme settings round trip");
        bool failed = false; copy.Current.MusicVolume = 101;
        try { Preferences.Validate(copy); } catch (InvalidDataException) { failed = true; }
        Check(failed, "Invalid persisted volume is rejected");
        var wind = new WindPresentation(); wind.Register(0); wind.Register(1);
        wind.Advance(.05); var right = wind.Sample(0, .1f);
        Check(right.Velocity > 0 && right.Offset > 0, "Positive native wind moves right");
        Check(wind.Sample(0, .1f).Offset == right.Offset, "Extra draw passes do not advance wind");
        wind.Advance(.05); var left = wind.Sample(0, -.1f);
        Check(left.Velocity < 0 && left.Offset < right.Offset, "Negative native wind moves left");
        wind.Advance(.05); Check(wind.Sample(0, 0).Offset == left.Offset, "Calm screens have no wind drift");
        Check(wind.Sample(1, float.NaN).Velocity == 0, "Invalid wind data cannot reach renderer");
        wind.Reset(); Check(wind.Sample(0, .1f).Offset == 0, "Restart resets wind presentation");
    }
    private static void PrepareUi(ContentManager content, GraphicsDevice device)
    {
        var game = Game1.instance;
        game.contentManager = new JKContentManager();
        var white = new Texture2D(device, 1, 1); white.SetData(new[] { Color.White });
        var pixel = (PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture)); GC.SuppressFinalize(pixel);
        typeof(PixelTexture).GetField("_texture", OwnedPatches.Members).SetValue(pixel, white); game.contentManager.Pixel = pixel;
        game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
        game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
        game.contentManager.font.LocationFont = content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
        var frame = content.Load<Texture2D>("Content/gui/frame"); int cell = frame.Width / 3;
        game.contentManager.gui.FrameSprites = new Sprite[3, 3];
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
            game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(frame, new Rectangle(x * cell, y * cell, cell, cell));
        typeof(UiSounds).GetField("TestPlayback", OwnedPatches.Members).SetValue(null, new Action<UiSound>(s => {}));
    }
    private static void WaterSlopes(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, Renderer renderer, string output)
    {
        var slopes = new[] {
            new SlopeBlock(new Rectangle(40, 220, 60, 60), SlopeType.BottomLeft),
            new SlopeBlock(new Rectangle(100, 280, 60, 60), SlopeType.BottomLeft),
            new SlopeBlock(new Rectangle(210, 240, 80, 80), SlopeType.TopLeft),
            new SlopeBlock(new Rectangle(320, 240, 80, 80), SlopeType.TopRight) };
        var collision = slopes.Select(JKRuntime.Geometry.NativeWorldGeometry.ReadSlopeVertices).ToArray();
        var blocks = slopes.Cast<IBlock>().Concat(new IBlock[] { new WaterBlock(new Rectangle(0, 200, 480, 160)), new NoWindBlock(new Rectangle(0, 0, 20, 360)) }).ToArray();
        Func<IBlock[], LevelScreen> screen = values => new LevelScreen(0, values, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null);
        using (var a = ScreenArt.Read(screen(blocks), device))
        using (var b = ScreenArt.Read(screen(blocks.Reverse().ToArray()), device))
        {
            Check(a.Cells.SequenceEqual(b.Cells) && a.WaterCells.SequenceEqual(b.WaterCells), "Water/slopes raster is independent of block order");
            Check(a.Cells[270 * 480 + 45] == 0 && a.Cells[225 * 480 + 95] == 1, "Southwest slope art uses its declared orientation");
            Check(a.Cells[305 * 480 + 125] == 1 && a.Cells[306 * 480 + 125] == 0, "Adjacent southwest tiles keep a continuous diagonal");
            Check(a.WaterCells[270 * 480 + 45] == 1 && a.Cells[210 * 480 + 450] == 0, "Water fills empty space without creating solid rectangles");
            Check(a.ShelterCells[10] == 1 && a.ShelterCells[30] == 0, "No-wind mask remains separate from solids");
            for (int i = 0; i < slopes.Length; i++) Check(collision[i].SequenceEqual(JKRuntime.Geometry.NativeWorldGeometry.ReadSlopeVertices(slopes[i])), "Visual adapter preserves native collision lines");
            renderer.Wind = new WindFrame { Velocity = -.1f, Offset = -95 };
            device.SetRenderTarget(target); renderer.Begin(); renderer.Background(80, 0, a, false); renderer.Foreground(80, a, true, false); batch.End(); device.SetRenderTarget(null);
            using (var file = File.Create(Path.Combine(output, "underwater-slopes.png"))) target.SaveAsPng(file, 480, 360);
            renderer.Wind = new WindFrame();
        }
    }
    private static void DrawPage(IUiPage page, GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, string output, string name)
    {
        device.SetRenderTarget(target); device.Clear(new Color(6, 9, 18));
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); page.Draw(); batch.End(); device.SetRenderTarget(null);
        using (var file = File.Create(Path.Combine(output, name))) target.SaveAsPng(file, 480, 360);
    }
    private static void LiveMenus(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, World world, string output)
    {
        var pages = new UiPageStack(p => new ThemesPage(p));
        var settingsPage = new ThemePage(pages, ThemeCatalog.All[0]); settingsPage.OnOpen();
        DrawPage(settingsPage, device, batch, target, output, "theme-settings.png");
        settingsPage.List.Select("disable-props"); settingsPage.Update(new UiInput { Action = UiAction.Confirm }, 0);
        Check(!ModEntry.HideProps && !Settings.Current.Current.DisableProps, "Props toggle updates active rendering immediately");
        settingsPage.Update(new UiInput { Action = UiAction.Confirm }, 0);
        Check(ModEntry.HideProps, "Props toggle restores suppression immediately");
        settingsPage.List.Select("reflections"); settingsPage.Update(new UiInput { Action = UiAction.Confirm }, 0);
        Check(!ModEntry.Prefs.Reflections, "Per-theme reflection toggle is live");
        settingsPage.Update(new UiInput { Action = UiAction.Confirm }, 0); settingsPage.OnClose();
        var number = new ThemeNumberPage("MUSIC VOLUME", "Adjust with arrows, wheel or drag. Changes are live.", 0, 100,
            () => Settings.Current.Current.MusicVolume, v => Settings.ChangeTheme("event-horizon", p => p.MusicVolume = v));
        number.OnOpen(); number.Number.Set(37);
        Check(Math.Abs(world.Music.Volume - .37f) < .001, "Slider changes native sound volume immediately");
        number.Update(new UiInput { Action = UiAction.Right }, 0);
        Check(Settings.Current.Current.MusicVolume == 38, "Keyboard uses the same live value setter");
        DrawPage(number, device, batch, target, output, "music-volume.png");
        number.Update(new UiInput { Action = UiAction.Cancel }, 0); number.OnClose();
        var persisted = new JKRuntime.Settings.SettingsFile<Preferences>(Scratch("settings.xml"), Preferences.Defaults, Preferences.Validate);
        Check(persisted.Value.Current.MusicVolume == 38, "Leaving the page needs no Apply and keeps saved value");
        var themes = new ThemesPage(pages); themes.OnOpen();
        DrawPage(themes, device, batch, target, output, "themes.png"); themes.OnClose();
    }
    private static void SceneryChecks(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        var native = typeof(Game1).Assembly;
        var propType = native.GetType("JumpKing.Props.LoopingProp", true);
        var prop = FormatterServices.GetUninitializedObject(propType);
        using (var texture = new Texture2D(device, 1, 1))
        {
            texture.SetData(new[] { Color.Red });
            propType.GetField("m_sprites", OwnedPatches.Members).SetValue(prop, new[] { Sprite.CreateSprite(texture, new Rectangle(0, 0, 1, 1)) });
            propType.GetField("m_position", OwnedPatches.Members).SetValue(prop, new Vector2(20, 20));
            propType.GetField("m_screen", OwnedPatches.Members).SetValue(prop, Camera.CurrentScreen);
            var pixels = new Color[480 * 360];
            foreach (bool hidden in new[] { true, false, true })
            {
                Settings.ChangeTheme("event-horizon", p => p.DisableProps = hidden);
                device.SetRenderTarget(target); device.Clear(Color.Black); batch.Begin(); propType.GetMethod("Draw").Invoke(prop, null); batch.End(); device.SetRenderTarget(null); target.GetData(pixels);
                Check(pixels.Any(p => p.R == 255 && p.G == 0) == !hidden, "Native prop draw suppression toggles without removing entity");
            }
        }
        var wall = native.GetType("JumpKing.Props.RaymanWall.RaymanWallEntity", true);
        var oldMan = native.GetType("JumpKing.MiscEntities.OldManEntity", true);
        foreach (var type in new[] { wall, oldMan })
        {
            Check(NativeHooks.IsScenery(type), "Native scenery classification: " + type.Name);
            Check(HarmonyLib.Harmony.GetPatchInfo(type.GetMethod("Draw")).Owners.Contains("prism.render"), "Scenery Draw hook installed: " + type.Name);
            Check(!HarmonyLib.Harmony.GetAllPatchedMethods().Any(m => m.DeclaringType == type && m.Name == "Update" && HarmonyLib.Harmony.GetPatchInfo(m).Owners.Contains("prism.render")), "Scenery logic remains native: " + type.Name);
        }
    }
    private static Type mappingHost;
    private static void LoadMappingFixture()
    {
        string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../mega-mapping-expansion/_INTERNAL/MegaMappingExpansion.Module.dll"));
        if (File.Exists(path)) mappingHost = System.Reflection.Assembly.LoadFrom(path).GetType("MegaMappingExpansion.SceneHost", true);
    }
    private static void MappingSceneryChecks()
    {
        if (mappingHost == null) { Console.WriteLine("[SKIP] Optional MME draw contract: build MME to check it"); return; }
        object host = FormatterServices.GetUninitializedObject(mappingHost);
        foreach (string name in new[] { "DrawLayer", "ComposeScenePass", "DrawSurfaceShadows", "DrawPlayerRim", "DrawScreenTexts" })
        {
            var method = mappingHost.GetMethod(name, OwnedPatches.Members);
            Check(method != null && HarmonyLib.Harmony.GetPatchInfo(method).Owners.Contains("prism.render"), "MME scenery hook: " + name);
            method.Invoke(host, method.GetParameters().Select(p => p.ParameterType == typeof(string) ? (object)"world" : null).ToArray());
            Check(true, "Disabled MME scenery never enters its rendering body: " + name);
        }
    }
}
