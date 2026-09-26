using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JumpKing;
using JumpKing.JKMemory;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.Player.Skins;
using JumpKing.SaveThread;
using JumpKing.SaveThread.SaveComponents;
using JumpKing.Workshop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using JKRuntime.UI;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using LanguageJK;
using WardrobePlus;
using Collection = JumpKing.Workshop.Collection;

internal static partial class WardrobeTests
{
    private sealed class DeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }
    private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static void GraphicsTests(string gameDir, string compatibilityRepo)
    {
        string originalDirectory = Directory.GetCurrentDirectory();
        string fixture = Path.Combine(output, "game"); Directory.CreateDirectory(fixture);
        string king = Path.Combine(fixture, "Content", "king"); Directory.CreateDirectory(king);
        foreach (string file in Directory.GetFiles(Path.Combine(gameDir, "Content", "king"))) File.Copy(file, Path.Combine(king, Path.GetFileName(file)));
        Directory.SetCurrentDirectory(fixture);
        try
        {
            using (var window = new Form { ShowInTaskbar = false })
            using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter,
                Environment.GetEnvironmentVariable("WARDROBE_REACH") == "1" ? GraphicsProfile.Reach : GraphicsProfile.HiDef,
                new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360, IsFullScreen = false }))
            using (var batch = new SpriteBatch(device))
            using (var target = new RenderTarget2D(device, 480, 360))
            {
                var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
                var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
                typeof(Game1).GetField("_instance", Flags).SetValue(null, game);
                typeof(Game).GetField("_services", Flags).SetValue(game, services);
                foreach (var field in typeof(Game).GetFields(Flags).Where(x => x.FieldType == typeof(IGraphicsDeviceService)))
                    field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
                game.contentManager = new JKContentManager();
                Game1.spriteBatch = batch;
                var pixel = (PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture)); GC.SuppressFinalize(pixel);
                using (var white = new Texture2D(device, 1, 1))
                using (var content = new ContentManager(services, gameDir))
                {
                    white.SetData(new[] { Color.White }); typeof(PixelTexture).GetField("_texture", Flags).SetValue(pixel, white); game.contentManager.Pixel = pixel;
                    game.contentManager.gui.Explore = Sprite.CreateSprite(white, new Rectangle(0, 0, 1, 1));
                    var keyboard = (IPad)Activator.CreateInstance(typeof(Game1).Assembly.GetType("JumpKing.Controller.KeyboardPad"), true);
                    var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
                    ControllerManager.instance = manager;
                    typeof(ControllerManager).GetField("m_pads", Flags).SetValue(manager, new List<PadInstance> { new PadInstance(keyboard) });
                    game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                    game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
                    game.contentManager.font.LocationFont = content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
                    var frame = content.Load<Texture2D>("Content/gui/frame"); int cell = frame.Width / 3;
                    game.contentManager.gui.FrameSprites = new Sprite[3, 3];
                    for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(frame, new Rectangle(x * cell, y * cell, cell, cell));
                    InitializeReadCache();
                    var workshop = (WorkshopManager)FormatterServices.GetUninitializedObject(typeof(WorkshopManager));
                    workshop.reskins = new Collection<Reskin>(); workshop.collections = new Collection<Collection>();
                    typeof(WorkshopManager).GetField("_instance", Flags).SetValue(null, workshop);
                    string setDir = Path.Combine(fixture, "1001"), singleDir = Path.Combine(fixture, "1002");
                    Directory.CreateDirectory(setDir); Directory.CreateDirectory(singleDir);
                    File.Copy(Path.Combine(king, "base.xnb"), Path.Combine(setDir, "body.xnb"));
                    File.Copy(Path.Combine(king, "cap.xnb"), Path.Combine(setDir, "hat.xnb"));
                    File.Copy(Path.Combine(king, "crown.xnb"), Path.Combine(singleDir, "hat.xnb"));
                    File.WriteAllText(Path.Combine(setDir, "set_settings.xml"), "<SetSettings><enabled>true</enabled><Reskins><Reskin><skin>NULL</skin><name>body</name></Reskin><Reskin><skin>Cap</skin><name>hat</name></Reskin></Reskins></SetSettings>");
                    File.WriteAllText(Path.Combine(singleDir, "cosmetic_settings.xml"), "<ReskinSettings><skin>Cap</skin><name>hat</name><enabled>false</enabled></ReskinSettings>");
                    var set = new Collection(setDir); var single = new Reskin(singleDir);
                    workshop.collections.Add(set); workshop.reskins.Add(single);
                    string nativeXml = File.ReadAllText(Path.Combine(setDir, "set_settings.xml")) + File.ReadAllText(Path.Combine(singleDir, "cosmetic_settings.xml"));
                    Controller.Store = new Store(Path.Combine(fixture, "Content", "WardrobePlus"));
                    Controller.Data = new WardrobeData();
                    Controller.Catalog = Catalog.Discover();
                    typeof(Controller).GetField("initialized", Flags).SetValue(null, true);
                    Assembly.LoadFrom(Path.Combine(gameDir, "Content", "JKMods", "0Harmony.dll"));
                    Controller.Data.Enabled = false;
                    Controller.Ensure(); Check(Hooks.Installed, "Initialized controller retries missing Harmony hooks against the actual game assembly: " + Hooks.Error);
                    Controller.Data.Enabled = true;
                    Menus.Sync();
                    WorkshopMenuTests();
                    workshop.collections.Clear(); workshop.reskins.Clear();
                    Check(!Controller.Load(false) && !Controller.Data.ImportedNative, "First import waits for the asynchronous Workshop catalog");
                    workshop.collections.Add(set); workshop.reskins.Add(single);
                    WorkshopManager.InvokeOnItemParsed();
                    game.contentManager.LoadPlayerSprites();
                    Check(Controller.Active != null, "Native sprite loading invokes the wardrobe resolver");
                    Check(Controller.Data.ImportedNative && Controller.Data.Current.ParentId == Catalog.PackageId(set), "First completed catalog imports the native collection");
                    Check(Controller.Data.Current.Choice((int)Items.Cap).Mode == ChoiceMode.Inherit, "Native import keeps parent entries inherited so later collection changes can replace them");
                    Check(NativeAppearance.Worn().Contains((int)Items.Cap), "Native owned/enabled equipment is composed");
                    Sprite originalWrapper = game.contentManager.playerSprites.idle;
                    var outfit = Controller.Data.Current.Copy(); outfit.ParentId = Catalog.PackageId(set); outfit.ParentName = "Fixture collection";
                    outfit.Set(new AppearanceChoice { Item = (int)Items.Cap, Mode = ChoiceMode.Source, SourceId = Catalog.SourceId(single, (int)Items.Cap, false), Label = "Fixture crown" });
                    Controller.Apply(outfit); Controller.Pump();
                    Check(Controller.Active.Resolved[(int)Items.Cap].Id == Catalog.SourceId(single, (int)Items.Cap, false), "GPU application combines a collection body and standalone override");
                    Check(ReferenceEquals(originalWrapper, game.contentManager.playerSprites.idle), "Applying preserves native behavior-tree sprite references");
                    Check(!single.Info.enabled && set.Info.enabled, "Applying leaves native Workshop enabled flags unchanged");
                    Check(Controller.Store.Load().Current.ParentId == outfit.ParentId, "Applied mixed selection survives settings reload");
                    int revision = AppearanceEvents.Revision;
                    Controller.Store.ReadOnly = true; Controller.Apply(new Outfit()); Controller.Pump(); Controller.Store.ReadOnly = false;
                    Check(AppearanceEvents.Revision == revision && Controller.Data.Current.ParentId == outfit.ParentId, "Failed persistence leaves visible and saved selection unchanged");
                    Controller.LastError = ""; Controller.Status = "";
                    RenderPage(device, batch, target);
                    BakeComparison(device, batch, target, Controller.Active.Items[Items.Cap]);
                    ReloadAndMapTests(game, workshop, set, single, fixture, king);
                    PresetPageTests();
                    SimplifiedPageTests();
                    MenuLifetimeTests();
                    MaterialGraphicsTests(device, batch, target);
                    CosmicTests(gameDir, device, batch, target, compatibilityRepo);
                    MaterialReferences(gameDir);
                    MaterialScenePreview(gameDir, device, batch, target);
                    CrystalPerformanceTests(device, batch, target);
                    if (compatibilityRepo != null)
                    {
                        ConsumerTests(compatibilityRepo, game, device, batch, target);
                        ConsumerTests(compatibilityRepo, game, device, batch, target, MaterialKind.Cosmic);
                    }
                    var selector = new JumpKing.PauseMenu.BT.Actions.SwitchSkinOption(Items.Cap);
                    var canChange = selector.GetType().GetMethod("CanChange", Flags);
                    Check((bool)canChange.Invoke(selector, null), "Inventory selector remains available with an active collection");
                    selector.GetType().GetMethod("CurrentOptionName", Flags).Invoke(selector, null);
                    selector.CurrentOption = 2; Controller.Pump();
                    Check(Controller.Data.Current.Choice((int)Items.Cap).Mode == ChoiceMode.OriginalGame, "Native inventory arrows select from the full wardrobe choices");
                    Controller.Undo(); Controller.Pump();
                    var collectionToggle = new JumpKing.PauseMenu.BT.Actions.Possessions.ToggleCollection(set);
                    collectionToggle.GetType().GetMethod("OnToggle", Flags).Invoke(collectionToggle, null); Controller.Pump();
                    Check(Controller.Data.Current.ParentId == "" && Controller.Data.Current.Choice((int)Items.Cap).Mode == ChoiceMode.Source, "Deselecting a native collection keeps explicit item overrides");
                    collectionToggle.GetType().GetMethod("OnToggle", Flags).Invoke(collectionToggle, null); Controller.Pump();
                    Check(Controller.Data.Current.ParentId == Catalog.PackageId(set) && Controller.Data.Current.Choice((int)Items.Cap).Mode == ChoiceMode.Source, "Native collection selection follows Keep customizations");
                    var toggle = new JumpKing.PauseMenu.BT.Actions.Possessions.ToggleReskin(single);
                    toggle.GetType().GetMethod("OnToggle", Flags).Invoke(toggle, null); Controller.Pump();
                    Check(Controller.Data.Current.Choice((int)Items.Cap).Mode == ChoiceMode.Inherit && Controller.Active.Resolved[(int)Items.Cap].Id.Contains(":set:"), "Native reskin toggle returns to the collection");
                    Controller.Undo(); Controller.Pump();
                    Check(Controller.Data.Current.Choice((int)Items.Cap).Mode == ChoiceMode.Source, "Undo restores the prior explicit appearance");
                    Controller.Request(data => data.Enabled = false, true, "Disabled"); Controller.Pump();
                    Check(!Controller.Enabled && Controller.Active.Resolved[(int)Items.Cap].Id.Contains(":set:"), "Disabling restores the native collection selection");
                    string nativeName = (string)selector.GetType().GetMethod("CurrentOptionName", Flags).Invoke(selector, null);
                    Check(nativeName == "Default" && selector.OptionCount == workshop.GetSkinsFromItem(Items.Cap).Count + 1, "Disabling restores an already open native selector's indices and choices");
                    Controller.Request(data => data.Enabled = true, true, "Enabled"); Controller.Pump();
                    Check(Controller.Enabled && Controller.Active.Resolved[(int)Items.Cap].Id.Contains(":skin:"), "Re-enabling restores the mod profile");
                    Check(nativeXml == File.ReadAllText(Path.Combine(setDir, "set_settings.xml")) + File.ReadAllText(Path.Combine(singleDir, "cosmetic_settings.xml")), "Workshop configuration files remain byte-identical");
                    Check(!Directory.Exists("SavesPerma"), "Appearance operations never write native save files");
                    LiveEquipmentTests();
                }
                Controller.Release(); if (Controller.Active != null) Controller.Active.Dispose();
            }
        }
        finally { Directory.SetCurrentDirectory(originalDirectory); }
    }
    private static void InitializeReadCache()
    {
        Type saveLube = typeof(Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
        var cache = (Dictionary<string, object>)saveLube.GetField("loaded_objects", Flags).GetValue(null);
        var inventory = new Inventory().GetDefault(); inventory.items.Add(new InventoryItem { item = Items.Cap, count = 1 }); inventory.items.Add(new InventoryItem { item = Items.Shoes, count = 1 });
        cache["SavesPermainventory.inv"] = inventory;
        cache["SavesPermageneral_settings.set"] = new GeneralSettings().GetDefault();
    }
    private static void RenderPage(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target)
    {
        var page = new WardrobePage(); page.OnOpen(); page.Update(new UiInput(), 0.016f);
        var pointer = typeof(UiPointer);
        Action<string> capture = name => {
            device.SetRenderTarget(target); device.Clear(Color.Black); batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            pointer.GetMethod("BeginDraw", Flags).Invoke(null, null);
            page.Draw(); pointer.GetMethod("EndDraw", Flags).Invoke(null, null);
            batch.End(); device.SetRenderTarget(null); device.Present();
            var pixels = new Color[480 * 360]; target.GetData(pixels);
            Check(Enumerable.Range(333, 7).All(y => Enumerable.Range(28, 424).All(x =>
                pixels[y * 480 + x].R == 0 && pixels[y * 480 + x].G == 0 && pixels[y * 480 + x].B == 0)),
                "Footer leaves a clear gap above the frame in " + name);
            using (var bitmap = new System.Drawing.Bitmap(480, 360))
            {
                for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                {
                    Color color = pixels[y * 480 + x];
                    bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
                }
                bitmap.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        };
        capture("wardrobe");
        var poll = pointer.GetMethod("Poll", Flags);
        poll.Invoke(null, new object[] { new Point(42, 204), true, true, false, false, 0, false });
        poll.Invoke(null, new object[] { new Point(42, 204), true, true, true, false, 0, false });
        for (int pass = 0; pass < 3; pass++)
        {
            poll.Invoke(null, new object[] { new Point(43 + pass, 204), true, true, false, false, 0, false });
            page.Update(new UiInput(), .016f);
            Check((int)PageState(page).GetType().GetField("Index", Flags).GetValue(PageState(page)) == 4,
                "Hovering the bottom of the new two-line list never advances the viewport");
            capture("wardrobe-hover-" + pass);
        }
        poll.Invoke(null, new object[] { new Point(45, 204), true, true, false, false, -120, false });
        page.Update(new UiInput(), .016f);
        Check((int)PageState(page).GetType().GetField("Index", Flags).GetValue(PageState(page)) == 5,
            "Wheel scrolls the two-line main list by one item");
        capture("wardrobe-scrolled");
        pointer.GetMethod("Reset", Flags).Invoke(null, null);
        // Restore the starting selection for subsequent independent captures.
        typeof(WardrobePage).GetMethod("Root", Flags).Invoke(page, new object[] { false });
        page.Update(new UiInput(), .016f);
        var binding = ControllerManager.instance.GetMain().GetBind();
        binding.boots = new[] { 88 };
        Check(UiInputHints.Key(UiAction.Secondary) == "X", "Displayed command follows a live keyboard rebind");
        capture("wardrobe-rebound"); binding.boots = new[] { 80 };
        typeof(WardrobePage).GetMethod("Item", Flags).Invoke(page, new object[] { (int)Items.Cap });
        page.Update(new UiInput(), .016f); capture("item-appearance");
        SelectRow(page, row => RowLabel(row) == "More options"); capture("item-options");
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        typeof(WardrobePage).GetMethod("FitOptions", Flags).Invoke(page, new object[] { (int)Items.Cap });
        typeof(WardrobePage).GetMethod("BeginFit", Flags).Invoke(page, null);
        var prior = Controller.Data.Current.Copy();
        for (int i = 0; i < 6; i++) page.Update(new UiInput { Right = true, Action = UiAction.Right }, .016f);
        capture("fitting");
        Check(Controller.Data.Current.Fits.Any(f => f.Item == (int)Items.Cap && f.X == 6), "Arrow fitting updates the live outfit immediately");
        page.Update(new UiInput { Cancel = true, Action = UiAction.Cancel }, .016f);
        Check(Controller.Store.Load().Current.Fits.Any(f => f.Item == (int)Items.Cap && f.X == 6), "Back keeps and flushes the fitted position");
        Controller.Undo(); Controller.Pump(); page.Update(new UiInput(), .016f);
        Check(Controller.Data.Current.Fits.Count == prior.Fits.Count, "One undo reverses a repeated fitting gesture");
        Controller.Redo(); Controller.Pump(); page.Update(new UiInput(), .016f);
        Check(Controller.Data.Current.Fits.Any(f => f.Item == (int)Items.Cap && f.X == 6), "Redo restores the entire fitted gesture");
        Controller.Apply(prior); Controller.Pump(); page.Update(new UiInput(), .016f);
        typeof(WardrobePage).GetMethod("BeginFit", Flags).Invoke(page, null);
        page.Update(new UiInput(), .016f);
        Check(Controller.Data.Current.Fits.Count == prior.Fits.Count, "Entering fitting without moving does not create a pose override");
        page.Update(new UiInput { Right = true, Action = UiAction.Right }, .016f);
        page.Update(new UiInput { Confirm = true, Action = UiAction.Confirm }, .016f);
        Check(Controller.Data.Current.Fits.Count == prior.Fits.Count + 1 && !Controller.Unsaved, "Done keeps an already published fit and flushes persistence");
        typeof(WardrobePage).GetMethod("Name", Flags).Invoke(page, new object[] { "OUTFIT NAME", "Test outfit", (Action<string>)delegate { }, false });
        capture("name-entry");
        var manager = ControllerManager.instance;
        var pads = (List<PadInstance>)typeof(ControllerManager).GetField("m_pads", Flags).GetValue(manager);
        var keyboard = pads[0];
        var xbox = (IPad)Activator.CreateInstance(typeof(Game1).Assembly.GetType("JumpKing.Controller.XboxPad"), new object[] { PlayerIndex.One });
        pads.Clear(); pads.Add(new PadInstance(new ConnectedPad(xbox)));
        Check(UiInputHints.Key(UiAction.Confirm) == "A", "Physical hints follow the newly active controller");
        capture("name-entry-controller");
        typeof(WardrobePage).GetMethod("Name", Flags).Invoke(page, new object[] { "OUTFIT NAME", "Космический король", (Action<string>)delegate { }, false });
        capture("name-entry-cyrillic");
        pads.Clear(); pads.Add(keyboard); page.OnClose();
        Controller.Apply(prior); Controller.Pump();
    }
    private sealed class ConnectedPad : IPad
    {
        private readonly IPad native;
        public ConnectedPad(IPad value) { native = value; }
        public string ButtonToString(int button) { return native.ButtonToString(button); }
        public int[] GetPressedButtons() { return new int[0]; }
        public PadBinding GetDefaultBind() { return native.GetDefaultBind(); }
        public string GetSaveIdentifier() { return "fixture"; }
        public string GetPrintName() { return "Fixture controller"; }
        public bool IsConnected() { return true; }
    }
    private sealed class MenuFixture
    {
        public System.Collections.IList Drawables { get; private set; }
        public MenuFixture() { Drawables = new System.Collections.ArrayList(); }
        public void AddDrawable(object value) { Drawables.Add(value); }
    }
    private static void WorkshopMenuTests()
    {
        Controller.Data.MainMenu = true; Controller.Data.PauseMenu = true; Menus.Sync();
        Check(UIApi.GetMainMenuItems().Single(x => x.Id.StartsWith("wardrobe-plus.")).Placement == UiMainMenuPlacement.Workshop,
            "Legacy root menu preferences still place Wardrobe only in Workshop");
        Check(!UIApi.GetPauseMenuItems().Any(x => x.Id.StartsWith("wardrobe-plus.")), "Wardrobe has no standalone Pause entry");
        Check(typeof(WardrobePlus.ModEntry).GetMethod("MainMenu").IsDefined(typeof(JKRuntime.Modules.MainMenuItemSettingAttribute), false)
            && typeof(WardrobePlus.ModEntry).GetMethod("Pause").IsDefined(typeof(JKRuntime.Modules.PauseMenuItemSettingAttribute), false), "Both native Mods settings entries remain discoverable");
        var format = new GuiFormat { anchor_bounds = new Rectangle(0, 0, 480, 360), anchor = new Vector2(.5f, .5f), element_margin = 4 };
        var menu = new MenuSelector(format);
        var browse = new LinkButton(language.WORKSHOP_BROWSE, "https://steamcommunity.com/app/1061090/workshop/");
        var mods = new TextButton("Mods", new MenuSelector(format));
        var back = new TextButton("Back", new MenuSelectorBack(menu));
        menu.AddChild(browse); menu.AddChild(mods); menu.AddChild(back); menu.Initialize(false);
        var root = new MenuSelector(format);
        root.AddChild(new TextButton(language.GAMETITLESCREEN_EXTRAS, new MenuSelector(format))); root.Initialize(false);
        var factory = new MenuFixture(); factory.AddDrawable(root); factory.AddDrawable(menu);
        var integration = typeof(UIApi).Assembly.GetType("JKRuntime.UI.WorkshopMenuIntegration").GetMethod("Apply", Flags);
        integration.Invoke(null, new object[] { factory }); integration.Invoke(null, new object[] { factory });
        Check(menu.Children.OfType<TextButton>().Count(x => x.Text == "Wardrobe+") == 1, "Refreshing Workshop does not duplicate Wardrobe");
        Check(ReferenceEquals(menu.Children.Last(), back) && menu.Children.Contains(browse) && menu.Children.Contains(mods), "Workshop preserves Browse, Mods and the final Back entry");
        typeof(UIApi).Assembly.GetType("JKRuntime.UI.RootMainMenuIntegration").GetMethod("Apply", Flags).Invoke(null, new object[] { factory });
        Check(!root.Children.OfType<TextButton>().Any(x => x.Text == "Wardrobe+"), "Workshop registration does not leak into the root main menu");
        UIApi.UnregisterMainMenuItem("wardrobe-plus.workshop"); integration.Invoke(null, new object[] { factory });
        Check(menu.Children.Length == 3, "Unregister removes only the injected Workshop row");
        Menus.Sync(); Controller.Data.MainMenu = false; Controller.Data.PauseMenu = false;
    }
    private static void BakeComparison(GraphicsDevice device, SpriteBatch batch, RenderTarget2D target, KingSprites original)
    {
        var texture = NativeAppearance.Frames(original.regular)[0].texture;
        var moved = new KingSprites(texture); var outfit = new Outfit();
        outfit.SetFit(new FitAdjustment { BaseId = "body", SourceId = "hat", Item = 4, Group = 0, Frame = 0, X = 17, Y = -12 });
        var owned = new List<Texture2D>(); FitBaker.Apply(moved, outfit, "body", "hat", 4, owned);
        var sprite = NativeAppearance.Frames(original.regular)[0]; var fitted = NativeAppearance.Frames(moved.regular)[0];
        Func<Sprite, Vector2, SpriteEffects, Color[]> render = (image, position, effects) => {
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            image.Draw(position, effects); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[480 * 360]; target.GetData(pixels); return pixels;
        };
        Check(render(sprite, new Vector2(217, 188), SpriteEffects.None).SequenceEqual(render(fitted, new Vector2(200, 200), SpriteEffects.None)), "Baked fitting matches native pixel translation facing right");
        Check(render(sprite, new Vector2(183, 188), SpriteEffects.FlipHorizontally).SequenceEqual(render(fitted, new Vector2(200, 200), SpriteEffects.FlipHorizontally)), "Baked fitting mirrors correctly facing left");
        Check(ReferenceEquals(NativeAppearance.Frames(moved.regular)[1].texture, texture), "Per-pose baking retains untouched native frames");
        foreach (var resource in owned) resource.Dispose();
        Check(!texture.IsDisposed, "Fitting resource cleanup preserves game-owned textures");
        owned.Clear(); moved = new KingSprites(texture); outfit.Fits.Clear();
        outfit.SetFit(new FitAdjustment { BaseId = "body", SourceId = "hat", Item = 4, X = -32, Y = 32 });
        FitBaker.Apply(moved, outfit, "body", "hat", 4, owned);
        bool allMatch = true; int frames = 0;
        for (int group = 0; group < original.m_groups.Count; group++)
            foreach (var pair in NativeAppearance.Frames(original.m_groups[group]))
            {
                var changed = NativeAppearance.Frames(moved.m_groups[group])[pair.Key];
                allMatch &= render(pair.Value, new Vector2(208, 252), SpriteEffects.None).SequenceEqual(render(changed, new Vector2(240, 220), SpriteEffects.None));
                allMatch &= render(pair.Value, new Vector2(272, 252), SpriteEffects.FlipHorizontally).SequenceEqual(render(changed, new Vector2(240, 220), SpriteEffects.FlipHorizontally));
                frames++;
            }
        Check(allMatch && frames > 13, "All " + frames + " native movement/ending frames match GPU translation in both directions at the fit limits");
        foreach (var resource in owned) resource.Dispose();
    }
}
