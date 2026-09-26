using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using BehaviorTree;
using JKRuntime.UI;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private sealed class LibraryDevice : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; set; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
        private sealed class LibraryFactory
        {
            public IList Drawables { get; private set; }
            public LibraryFactory() { Drawables = new ArrayList(); }
            public void AddDrawable(object value) { Drawables.Add(value); }
        }
        private static void GimmickGraphics(string gameDirectory)
        {
            var instance = typeof(Game1).GetField("_instance", Flags); var previousGame = instance.GetValue(null);
            var previousBatch = Game1.spriteBatch; var previousPins = Settings.Current.GimmickPins;
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gimmick-ui"); Directory.CreateDirectory(output);
            try
            {
                using (var window = new System.Windows.Forms.Form { ShowInTaskbar = false })
                using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
                    new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360 }))
                using (var target = new RenderTarget2D(device, 480, 360))
                using (var white = new Texture2D(device, 1, 1))
                using (var batch = new SpriteBatch(device))
                {
                    var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new LibraryDevice { GraphicsDevice = device });
                    var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1)); instance.SetValue(null, game);
                    typeof(Game).GetField("_services", Flags).SetValue(game, services);
                    foreach (var field in typeof(Game).GetFields(Flags).Where(f => f.FieldType == typeof(IGraphicsDeviceService))) field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
                    game.contentManager = new JKContentManager(); Game1.spriteBatch = batch;
                    var pixel = (PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture)); GC.SuppressFinalize(pixel);
                    white.SetData(new[] { Color.White }); typeof(PixelTexture).GetField("_texture", Flags).SetValue(pixel, white); game.contentManager.Pixel = pixel;
                    using (var content = new ContentManager(services, gameDirectory))
                    {
                        game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                        game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
                        game.contentManager.font.LocationFont = content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
                        var texture = content.Load<Texture2D>("Content/gui/frame"); int cell = texture.Width / 3;
                        game.contentManager.gui.FrameSprites = new Sprite[3, 3];
                        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(texture, new Rectangle(x * cell, y * cell, cell, cell));
                        game.contentManager.gui.CheckBoxTrue = game.contentManager.gui.CheckBoxFalse = Sprite.CreateSprite(white, new Rectangle(0, 0, 1, 1));
                        var factory = new LibraryFactory(); var format = new GuiFormat { anchor_bounds = new Rectangle(0, 0, 480, 360), anchor = new Vector2(.5f, .5f) };
                        Gimmicks.Initialize(); Settings.Current.GimmickPins = null;
                        var menu = new MenuSelector(format);
                        menu.AddChild(new WarpJumpOption()); menu.AddChild(new NoWalkOffOption()); menu.AddChild(new AirDashOption());
                        menu.AddChild(new GimmickLibraryButton(factory, format, true)); factory.AddDrawable(menu); menu.Initialize(false);
                        GimmickMenu.Refresh(factory);
                        Require(menu.Children.Count(c => c is GimmickPin) == 3, "Default library pins replace the original three native rows");
                        Settings.Current.GimmickPins = new[] { "mega.air-dash", "missing:provider" }; GimmickMenu.Refresh(factory);
                        Require(menu.Children.Count(c => c is GimmickPin) == 2 && menu.Children.Count(c => c is GimmickLibraryButton) == 1, "Pins refresh without duplicates and retain unavailable providers");
                        Settings.Current.GimmickPins = new string[0]; GimmickMenu.Refresh(factory);
                        Require(menu.Children.Length == 1, "Unpinning everything leaves the library accessible");
                        Settings.Current.GimmickPins = null;
                        var page = new GimmickPage(factory, format, true);
                        Action<string, object[], string> capture = (method, args, name) => {
                            typeof(GimmickPage).GetMethod(method, Flags).Invoke(page, args);
                            device.SetRenderTarget(target); device.Clear(Color.Black); batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                            page.Draw(); batch.End(); device.SetRenderTarget(null);
                            using (var file = File.Create(Path.Combine(output, name + ".png"))) target.SaveAsPng(file, 480, 360);
                        };
                        capture("Home", null, "library");
                        var settingPreview = new GimmickEntry { Id = "graphics:mod-setting", Label = "Provider option", Owner = "Fixture provider", Kind = "Setting", Read = () => true, Write = value => { throw new InvalidOperationException("Drawing settings must not write"); } };
                        try {
                            Gimmicks.Add(settingPreview);
                            capture("RefreshCatalogue", null, "mod-setting-catalogue");
                            capture("Details", new object[] { settingPreview.Id }, "mod-setting");
                            capture("Categories", null, "mod-setting-category");
                        } finally { Gimmicks.Entries.Remove(settingPreview.Id); }
                        capture("RefreshCatalogue", null, "library-refreshed");
                        capture("ActiveGimmicks", null, "active-reset");
                        var savedRules = Settings.Current.GimmickRules; bool savedDash = Settings.Current.AirDash;
                        try {
                            Settings.Current.AirDash = true;
                            Settings.Current.GimmickRules = new[] { new GimmickRule { Id = "graphics:unavailable", Name = "Upper-region wind", Enabled = true, FirstScreen = 31, LastScreen = 40 } };
                            typeof(GimmickPage).GetMethod("RefreshCatalogue", Flags).Invoke(page, null);
                            capture("ActiveGimmicks", null, "active-reset-populated");
                        } finally {
                            Settings.Current.GimmickRules = savedRules; Settings.Current.AirDash = savedDash;
                            typeof(GimmickPage).GetMethod("RefreshCatalogue", Flags).Invoke(page, null);
                        }
                        capture("Categories", null, "categories");
                        capture("SearchResults", null, "search");
                        capture("Filters", null, "filters");
                        capture("Colours", null, "palette");
                        capture("Details", new object[] { GimmickWind.Id }, "wind");
                        capture("WindStrength", new object[] { GimmickWind.Id }, "wind-strength");
                        capture("PreviewGeometry", new object[] { new[] { GimmickSpace.Fill(new JumpKing.Level.IBlock[] {
                            new JumpKing.Level.BoxBlock(new Rectangle(0, 320, 480, 40)), new JumpKing.Level.BoxBlock(new Rectangle(90, 170, 170, 24)),
                            new JumpKing.Level.SlopeBlock(new Rectangle(270, 256, 64, 64), JumpKing.Level.SlopeType.TopLeft)
                        }, 0, new JumpKing.Level.WaterBlock(Rectangle.Empty)) } }, "preview");
                        capture("Details", new object[] { Gimmicks.Entries.Values.First(e => e.Template is UnknownProvider.Material).Id }, "material");
                        var native = GimmickMaps.Read(Path.Combine(gameDirectory, "Content"), "native");
                        Require(native.Error == null && native.Regions.Length > 0, "Installed native atlas and regions index successfully: " + native.Error);
                        var previousMaps = GimmickMaps.Maps;
                        try { GimmickMaps.Maps = new[] { native }; capture("Maps", null, "maps"); }
                        finally { GimmickMaps.Maps = previousMaps; }
                        capture("Regions", new object[] { native }, "regions");
                        capture("Information", new object[] { Gimmicks.Entries.Values.First(e => e.Template is UnknownProvider.Material) }, "details");
                    }
                }
            }
            finally { instance.SetValue(null, previousGame); Game1.spriteBatch = previousBatch; Settings.Current.GimmickPins = previousPins; }
            Console.WriteLine("[OK] Gimmick native menu pin insertion/removal and native browser/filter/palette/wind/preview UI captures: " + output);
        }
    }
}
