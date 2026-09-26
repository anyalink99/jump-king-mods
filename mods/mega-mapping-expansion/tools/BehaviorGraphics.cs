using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using JumpKing;
using EntityComponent;
using JumpKing.Util.DrawBT;
using JKRuntime.UI;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal static class BehaviorGraphics
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private sealed class DeviceService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; set; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
        private static object Field(object value, string name) { return value.GetType().GetField(name, All).GetValue(value); }
        private sealed class LateSignal : EntityComponent.Component
        {
            internal Action Signal;
            protected override void LateUpdate(float delta) { Signal(); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        private static bool SkipNativeFrame() { return false; }
        private static bool resultPress;
        // Isolate completion-save IO and physical input; run the native statistics
        // behavior tree and the installed narrative adapters unchanged.
        private static bool StatsFixture(BehaviorTree.BTmanager ___m_BT, ref string[] ___m_lines)
        { ___m_BT.Reset(); ___m_lines = new[] { "NATIVE STATISTICS" }; return false; }
        private static bool ResultInput(ref BehaviorTree.BTresult __result)
        { __result = resultPress ? BehaviorTree.BTresult.Success : BehaviorTree.BTresult.Failure; return false; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void DrawNativeStats(JumpKing.GameManager.StatsScreen stats) { stats.Draw(); }
        private static int Main(string[] args)
        {
            try { Run(args[0], args[1], args[2]); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Run(string gameDir, string sceneRoot, string output)
        {
            Directory.CreateDirectory(output); Directory.SetCurrentDirectory(output);
            using (var window = new Form { ShowInTaskbar = false })
            using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
                new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360 }))
            using (var target = new RenderTarget2D(device, 480, 360))
            using (var white = new Texture2D(device, 1, 1))
            using (var batch = new SpriteBatch(device))
            {
                var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
                var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
                typeof(Game1).GetField("_instance", All).SetValue(null, game);
                typeof(Game).GetField("_services", All).SetValue(game, services);
                foreach (var field in typeof(Game).GetFields(All).Where(f => f.FieldType == typeof(IGraphicsDeviceService))) field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
                game.contentManager = new JKContentManager(); Game1.spriteBatch = batch;
                var pixel = (PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture)); GC.SuppressFinalize(pixel);
                white.SetData(new[] { Color.White }); typeof(PixelTexture).GetField("_texture", All).SetValue(pixel, white); game.contentManager.Pixel = pixel;
                using (var content = new ContentManager(services, gameDir))
                {
                    game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                    game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
                    game.contentManager.font.StyleFont = game.contentManager.font.MenuFont;
                    game.contentManager.font.LocationFont = content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
                    var frame = content.Load<Texture2D>("Content/gui/frame"); int cell = frame.Width / 3;
                    game.contentManager.gui.FrameSprites = new Sprite[3, 3];
                    for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(frame, new Rectangle(x * cell, y * cell, cell, cell));
                    var manager = (JumpKing.Controller.ControllerManager)FormatterServices.GetUninitializedObject(typeof(JumpKing.Controller.ControllerManager));
                    JumpKing.Controller.ControllerManager.instance = manager;
                    typeof(JumpKing.Controller.ControllerManager).GetField("_menu_controller", All).SetValue(manager, new JumpKing.Controller.MenuController(manager));
                    var pads = new System.Collections.Generic.List<JumpKing.Controller.PadInstance>();
                    typeof(JumpKing.Controller.ControllerManager).GetField("m_pads", All).SetValue(manager, pads);
                    var keyboard = (JumpKing.Controller.IPad)Activator.CreateInstance(typeof(Game1).Assembly.GetType("JumpKing.Controller.KeyboardPad"), true);
                    pads.Add(new JumpKing.Controller.PadInstance(keyboard));
                    SceneFile data = SceneValidation.Load(sceneRoot, 2);
                    data.Flags = new[] { new FlagData { Id = "late" } };
                    data.Rules = new[] { new RuleData { Id = "late", Event = "late-component", SetFlag = "late", Value = "true" } };
                    var entities = new EntityComponent.EntityManager();
                    NarrativeGraphics(device, target, batch, white, entities, sceneRoot, output);
                    for (int attemptIndex = 0; attemptIndex < 3; attemptIndex++)
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        using (var attempt = new JKRuntime.RuntimeScope())
                        {
                            ModEntry.PrepareAttempt(attempt, sceneRoot, 2, true);
                            var ready = (SceneHost)typeof(ModEntry).GetField("preparedHost", All).GetValue(null);
                            Check(ready != null && SceneHost.Current == null && ready.EntityOwner == null,
                                "Preparation owns decoded GPU resources without publishing a scene or binding an entity manager");
                            Console.WriteLine("[COST] Mapping resource preparation: " + timer.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
                            if (attemptIndex == 1)
                            {
                                timer.Restart(); ready.Activate(); timer.Stop();
                                Check(SceneHost.Current == ready && object.ReferenceEquals(ready.EntityOwner, entities), "Activation attaches the prepared scene to the current entity manager");
                                Console.WriteLine("[COST] Mapping prepared activation: " + timer.Elapsed.TotalMilliseconds.ToString("F6") + " ms");
                            }
                        }
                        Check(SceneHost.Current == null && SceneHost.LiveInstances == 0, "Cancelled and activated attempts release all prepared scene resources");
                    }

                    using (var host = new SceneHost(new LoadedScene(data, null), sceneRoot))
                    {
                        host.Activate();
                        var actor = new SceneActor { Present = true, Screen = 1, Bounds = new Rectangle(210, 215, 20, 30) };
                        host.behaviors.Tick(0, true, true, actor); host.ResolveBehaviorTransforms();
                        Check(host.SceneData.Lights.Length == 1 && host.SceneData.Nodes.First(p => p.Id == "panel").Asset == "gold", "packaged lantern recipe enters region and swaps asset");
                        LightData lamp = host.SceneData.Lights[0];
                        Check(lamp.X == 220 && lamp.Y == 212 && lamp.IgnoreAttachmentActor, "player socket offset and self-shadow exclusion resolve in same tick");
                        var prepare = typeof(SceneHost).GetMethod("PrepareLightFields", All);
                        Action refresh = () => prepare.Invoke(host, new object[] { 480, 360, 240, 180, actor.Bounds });
                        refresh();
                        var fields = (IDictionary)Field(host, "lightFields"); object field = fields[lamp];
                        var buffer = (float[])Field(field, "Base"); int oldPeak = Array.IndexOf(buffer, buffer.Max());
                        var colors = (Vector3[])Field(host, "activeLightColors"); Check(colors[0].LengthSquared() > 0, "carried light remains illuminated inside player bounds");
                        lamp.Radius = 60; lamp.Color = "#91D8FF"; refresh();
                        Check((float)Field(field, "Radius") == 60 && ((Vector3[])Field(host, "activeLightColors"))[0].Z > .9f, "radius and color edits invalidate derived light values");
                        lamp.Enabled = false; refresh(); Check(((Vector3[])Field(host, "activeLightColors"))[0] == Vector3.Zero, "disabled light contributes zero radiance"); lamp.Enabled = true; refresh();
                        Capture(device, target, batch, host.DrawWorld, Path.Combine(output, "lantern-active.png"));
                        actor.Bounds = new Rectangle(350, 200, 20, 30);
                        host.behaviors.Tick(1d / 60, true, true, actor); host.ResolveBehaviorTransforms(); refresh();
                        Check(ReferenceEquals(buffer, Field(field, "Base")) && Array.IndexOf(buffer, buffer.Max()) != oldPeak, "moving source rebuilds attenuation in the same allocated buffer");
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 120; i++) { lamp.X += .25f; refresh(); }
                        clock.Stop(); Console.WriteLine("[PROFILE] One moving 240x180 light field: " + (clock.Elapsed.TotalMilliseconds / 120).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms/update; no field-buffer replacements");
                        object saved = host.CaptureBehavior();
                        for (int i = 0; i < 1800; i++) host.behaviors.Tick(1d / 60, true, true, actor);
                        host.ResolveBehaviorTransforms(); refresh();
                        Check(fields.Count == 0 && host.SceneData.Nodes.First(p => p.Id == "panel").Asset == "blue", "expiry releases field cache and restores authored asset");
                        Capture(device, target, batch, host.DrawWorld, Path.Combine(output, "lantern-expired.png"));
                        host.RestoreBehavior(saved); host.ResolveBehaviorTransforms(); refresh();
                        Check(host.SceneData.Lights.Length == 1, "host snapshot restores light membership before next render");
                        actor.Screen = 2; host.behaviors.Tick(0, true, true, actor); host.ResolveBehaviorTransforms();
                        Check(host.SceneData.Lights[0].Screen == 2, "attachment follows screen transition");
                        actor.Present = false; host.behaviors.Tick(0, true, true, actor); host.ResolveBehaviorTransforms();
                        Check(!host.SceneData.Lights[0].AttachmentVisible, "lost actor hides the attachment");
                        var inspector = new SceneInspector(); inspector.OnOpen();
                        Capture(device, target, batch, inspector.Draw, Path.Combine(output, "inspector.png")); inspector.OnClose();
                        var commands = new[] { new UiPageCommand(UiAction.Confirm, "Apply selected property", () => { }), new UiPageCommand(UiAction.Secondary, "Restore authored values", () => { }), new UiPageCommand(UiAction.Cancel, "Back", () => { }) };
                        var layout = new UiPageLayout(new Rectangle(12, 12, 456, 336), commands, 2);
                        Check(layout.Footers.All(r => r.Bottom < 342) && layout.Content.Bottom <= layout.Status.Top && layout.Status.Bottom <= layout.Footers[0].Top, "measured footer wraps without crossing content or native frame");
                        Capture(device, target, batch, () => { new UiFrame(new Rectangle(12, 12, 456, 336)).Draw(); layout.DrawCommands(); UiTheme.TextLine("WRAPPED COMMANDS", layout.Title.Location.ToVector2(), UiTheme.Gold, false); }, Path.Combine(output, "wrapped-commands.png"));
                        var signal = new LateSignal { Signal = () => host.behaviors.Emit("late-component") };
                        var entity = new EntityComponent.Entity(); entity.AddComponents(signal);
                        NativeHooks.Install();
                        try
                        {
                            entities.Update(1f / 60);
                            Check(host.behaviors.GetFlag("late") == "true", "real EntityManager hook drains events after component LateUpdate");
                            var settingsField = typeof(MappingSettings).GetField("store", All);
                            object previousSettings = settingsField.GetValue(null);
                            var settings = new MappingSettingsStore(Path.Combine(output, "toggle.xml"));
                            settingsField.SetValue(null, settings);
                            try
                            {
                                var effect = host.behaviors.Apply("fixture", new EffectDefinition { Id = "toggle-clock", Duration = 30 });
                                settings.Set(false);
                                entities.Update(.1f);
                                Check(effect.RemainingSeconds == 30 && ReferenceEquals(SceneHost.Current, host), "disabled Mapping freezes effects without destroying scene ownership");
                                Capture(device, target, batch, () => typeof(NativeHooks).GetMethod("AfterScreenDraw", All).Invoke(null, null), Path.Combine(output, "mapping-disabled.png"));
                                var offPixels = new Color[480 * 360]; target.GetData(offPixels);
                                Check(offPixels.All(c => c == new Color(15, 18, 24)), "disabled native world hook draws no Mapping scenery");
                                settings.Set(true); entities.Update(.1f);
                                Check(effect.RemainingSeconds < 30 && SceneHost.LiveInstances == 1, "re-enabling resumes the same scene without duplicate resources");
                                effect.Dispose();
                            }
                            finally { settingsField.SetValue(null, previousSettings); }
                            FieldManager(null); var foreign = new EntityComponent.EntityManager(); FieldManager(entities);
                            double remaining = host.behaviors.InspectEffects()[0].RemainingSeconds;
                            foreign.Update(.1f);
                            Check(host.behaviors.InspectEffects()[0].RemainingSeconds == remaining, "foreign/simulation manager cannot advance the live scene");
                            var pauseType = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
                            object pause = FormatterServices.GetUninitializedObject(pauseType);
                            pauseType.GetField("_paused", All).SetValue(pause, true); pauseType.GetField("instance", All).SetValue(null, pause);
                            var fixture = new HarmonyLib.Harmony("fixture.mapping.pause");
                            fixture.Patch(typeof(JumpGame).GetMethod("Update"), prefix: new HarmonyLib.HarmonyMethod(typeof(BehaviorGraphics).GetMethod("SkipNativeFrame", All)) { priority = 0 });
                            try
                            {
                                var gameClock = host.behaviors.Apply("fixture", new EffectDefinition { Id = "game-clock", Duration = 30 });
                                var uiClock = host.behaviors.Apply("fixture", new EffectDefinition { Id = "presentation-clock", Duration = 1, Clock = "presentation" });
                                host.behaviors.Apply("fixture", new EffectDefinition { Id = "paused-edit", Changes = new[] { new SceneChange { Target = "panel", Property = "x", Value = "100" } } });
                                var fake = (JumpGame)FormatterServices.GetUninitializedObject(typeof(JumpGame));
                                for (int i = 0; i < 60; i++) fake.Update(new GameTime());
                                Check(gameClock.RemainingSeconds == 30 && !uiClock.Active, "native paused frame advances presentation while freezing gameplay clock");
                                var panel = host.SceneData.Nodes.First(p => p.Id == "panel");
                                var pose = (PropPose)typeof(SceneHost).GetMethod("ResolvePropPose", All).Invoke(host, new object[] { panel });
                                Check(pose.Position.X == 100, "inspector edits refresh rendered poses while EntityManager is paused");
                            }
                            finally { fixture.UnpatchAll("fixture.mapping.pause"); pauseType.GetField("instance", All).SetValue(null, null); }
                        }
                        finally { NativeHooks.Uninstall(); entity.Destroy(); }
                    }
                    Check(SceneHost.LiveInstances == 0 && SceneHost.Current == null, "GPU scene teardown releases live ownership");
                    CheckInfrastructureFrames(device, target, batch, sceneRoot, output);
                }
            }
            Console.WriteLine("[OUTPUT] " + output);
        }
        private static void FieldManager(EntityComponent.EntityManager manager)
        { typeof(EntityComponent.EntityManager).GetField("_instance", All).SetValue(null, manager); }
        private static void CheckInfrastructureFrames(GraphicsDevice device, RenderTarget2D target, SpriteBatch batch, string root, string output)
        {
            var scene = new SceneFile(); scene.Options.AdvancedLighting = true;
            scene.Options.AmbientLight = "#FFFFFF"; scene.Options.AmbientIntensity = .5f;
            using (var host = new SceneHost(new LoadedScene(scene, null), root))
            {
                host.Activate();
                Capture(device, target, batch, () => host.ComposeScenePass(target), Path.Combine(output, "ambient-only.png"));
                var pixels = new Color[480 * 360]; target.GetData(pixels);
                Check(Math.Abs(pixels[240].R - 7) <= 1 && Math.Abs(pixels[240].B - 12) <= 1, "Uniform ambient modulation retains the intended pixel values");
                Check(Field(host, "compositeTarget") == null && Field(host, "lightTarget") == null && Field(host, "radial") == null,
                    "A plain ambient-only scene allocates no reflection copy, light map or radial texture");
            }
            scene.Lights = new[] { new LightData { Id = "lamp", X = 60, Y = 160, Radius = 220, OccludeKing = false } };
            scene.Anchors = new[] { new SceneAnchor { Id = "wall", Screen = 1, X = 100, Y = 100, Width = 12, Height = 120, BlocksLight = true } };
            using (var host = new SceneHost(new LoadedScene(scene, null), root))
            {
                host.Activate();
                var prepare = typeof(SceneHost).GetMethod("PrepareLightFields", All);
                Action update = () => prepare.Invoke(host, new object[] { 480, 360, 240, 180, Rectangle.Empty });
                update();
                object field = ((IDictionary)Field(host, "lightFields"))[host.SceneData.Lights[0]];
                float[] attenuated = (float[])Field(field, "Attenuated");
                Check(attenuated[80 * 240 + 90] < .001f && attenuated[80 * 240 + 40] > .1f,
                    "Authored wall removes irradiance behind it but preserves light before it");
                using (host.behaviors.Apply("glass", new EffectDefinition { Id = "transparent", Changes = new[] {
                    new SceneChange { Target = "anchor:wall", Property = "lightOpacity", Value = "0" } } }))
                {
                    host.ResolveBehaviorTransforms(); update();
                    Check(attenuated[80 * 240 + 90] > .01f, "Zero-opacity map blocker transmits light");
                }
                host.behaviors.Apply("door", new EffectDefinition { Id = "open", Changes = new[] {
                    new SceneChange { Target = "anchor:wall", Property = "blocksLight", Value = "false" } } });
                host.ResolveBehaviorTransforms(); update();
                Check(attenuated[80 * 240 + 90] > .01f, "Effect-controlled blocker invalidates cached lighting");
            }
            scene = new SceneFile();
            scene.VectorAssets = new[] { new VectorAssetData { Id = "red", Width = 20, Height = 20,
                Shapes = new[] { new VectorShapeData { Type = "rect", Width = 20, Height = 20, Fill = "#FF0000" } } } };
            scene.Nodes = new[] { new PropData { Id = "selected", Asset = "red", X = 40, Y = 40 },
                new PropData { Id = "excluded", Asset = "red", X = 100, Y = 40 } };
            scene.Waters = new[] { new WaterData { Id = "pool", Screen = 1, X = 0, Y = 150, Width = 160, Height = 100,
                CompositeReflection = true, ReflectionObjects = "selected" } };
            using (var host = new SceneHost(new LoadedScene(scene, null), root))
            {
                host.Activate();
                Capture(device, target, batch, () => { host.DrawWorld(); host.ComposeScenePass(target); }, Path.Combine(output, "selected-reflection.png"));
                var source = (RenderTarget2D)typeof(SceneHost).GetMethod("ReflectionFrame", All).Invoke(host, new object[] { 1, "selected" });
                var pixels = new Color[480 * 360]; source.GetData(pixels);
                Check(pixels[40 * 480 + 40].R > 200 && pixels[40 * 480 + 100].A == 0,
                    "Separate reflection source includes selected object and excludes its visible neighbor");
            }
        }
        private static void NarrativeGraphics(GraphicsDevice device, RenderTarget2D target, SpriteBatch batch, Texture2D white, EntityComponent.EntityManager entities, string root, string output)
        {
            var scene = new SceneFile {
                Flags = new[] { new FlagData { Id = "help", Value = "yes" }, new FlagData { Id = "count", Type = "integer", Value = "3" } },
                Strings = new[] { new MapString { Id = "intro", Value = "THE LAST LIGHT" }, new MapString { Id = "quote", Value = "A literal line.\nThe second line." }, new MapString { Id = "label", Value = "Count: {flag:count}" } },
                Texts = new[] { new SceneText { Id = "hud", String = "label", Space = "screen", X = 12, Y = 12, Font = "small" } },
                IntroPages = new[] { new IntroPage { String = "intro" } },
                ResultPages = new[] { new ResultPage { Title = "intro", Background = "#07143D", Rows = new[] { new ResultRow { String = "label" } } } },
                NativeActors = new[] { new NativeActorData { Id = "guide", Name = "fixture", TextVisible = false, OffsetX = 7, Tint = "#FF0000", Quotes = new[] { new ActorQuote { String = "quote", RequiresFlag = "help", EqualsValue = "yes" } } }, new NativeActorData { Id = "shop", Name = "shop", Kind = "merchant", TextVisible = false } }
            };
            Game1.instance.contentManager.oldMan.spawn_names = new[] { "fixture" };
            Game1.instance.contentManager.oldMan.merchant_names = new[] { "shop" };
            var actorType = typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.OldManEntity", true);
            var merchantType = typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.Merchant.MerchantEntity", true);
            var actor = (ISpriteEntity)FormatterServices.GetUninitializedObject(actorType);
            var merchant = (ISpriteEntity)FormatterServices.GetUninitializedObject(merchantType);
            foreach (var entity in new[] { actor, merchant }) {
                typeof(EntityComponent.Entity).GetField("m_components", All).SetValue(entity, new System.Collections.Generic.List<EntityComponent.Component>());
                entities.AddObject(entity); entity.SetSprite(Sprite.CreateSprite(white)); entity.Position = new Vector2(40, 40);
            }
            var settings = new JumpKing.MiscEntities.OldMan.OldManSettings { name = "fixture", home_screen = 1 };
            actorType.GetField("m_settings", All).SetValue(actor, settings);
            var merchantField = merchantType.GetField("m_settings", All); object merchantSettings = Activator.CreateInstance(merchantField.FieldType);
            merchantSettings.GetType().GetField("settings").SetValue(merchantSettings, new JumpKing.MiscEntities.OldMan.OldManSettings { name = "shop", home_screen = 1 }); merchantField.SetValue(merchant, merchantSettings);
            actor.AddComponents((EntityComponent.Component)Activator.CreateInstance(typeof(Game1).Assembly.GetType("EntityComponent.BlackBoardComp", true), true));
            using (var fixture = new JKRuntime.OwnedPatches("mapping.tests.results")) {
            fixture.Add(typeof(JumpKing.GameManager.StatsScreen).GetMethod("OnNewRun", All), typeof(BehaviorGraphics).GetMethod("StatsFixture", All));
            fixture.Add(typeof(JumpKing.Util.AnyInputNode).GetMethod("MyRun", All), typeof(BehaviorGraphics).GetMethod("ResultInput", All));
            var stats = new JumpKing.GameManager.StatsScreen();
            var resultTree = new BehaviorTree.BTmanager(stats);
            resultPress = false; resultTree.Run(1f / 60);
            // Match the game's already-compiled draw caller before map preparation.
            Capture(device, target, batch, () => DrawNativeStats(stats), Path.Combine(output, "narrative-native.png"));
            stats.ResetResult();
            try {
                using (var host = new SceneHost(new LoadedScene(scene, null), root)) {
                    NativeNarrative.Prepare(host); host.Activate();
                    Vector2 position; int screen; bool visible;
                    Check(NativeNarrative.Pose("guide", out position, out screen, out visible) && visible && position.X == 47 && screen == 1, "Native actor binding discovers derived sprite entities and visual sockets");
                    Check(NativeNarrative.Pose("shop", out position, out screen, out visible), "Merchant binding leaves native trade entity intact");
                    var fetch = new JumpKing.MiscEntities.OldMan.FetchQuote(actor);
                    Check(fetch.Run(new BehaviorTree.TickData(0, 0)) == BehaviorTree.BTresult.Success, "Actual native FetchQuote hook selects conditional dialogue");
                    new EntityComponent.BT.SetBBKeyNode<int>(actor, "BKKEY_INDEX", 0).Run(new BehaviorTree.TickData(0, 0));
                    var line = new JumpKing.MiscEntities.OldMan.TargetLine(actor);
                    Check(line.Run(new BehaviorTree.TickData(0, 0)) == BehaviorTree.BTresult.Success, "Native line node consumes custom literal text without a second localization lookup");
                    var intro = typeof(JumpKing.GameManager.IntroState).GetMethod("MakeLegendHasItText", All).Invoke(FormatterServices.GetUninitializedObject(typeof(JumpKing.GameManager.IntroState)), new object[] { false });
                    Check(intro is BehaviorTree.BTsequencor, "Actual native intro method receives map-authored page sequence");
                    Capture(device, target, batch, host.DrawScreenTexts, Path.Combine(output, "narrative-hud.png"));
                    var pixels = new Color[480 * 360]; target.GetData(pixels); Check(pixels.Any(p => p.R > 100), "Screen text renders with installed native font");
                    Capture(device, target, batch, () => NativeNarrative.DrawReflection("guide"), Path.Combine(output, "narrative-npc-reflection.png"));
                    target.GetData(pixels); Check(pixels[40 * 480 + 47].R > 200 && pixels[40 * 480 + 47].G < 10, "Selected native reflection applies actor tint and visual offset");
                    Sprite original = actor.sprite; Vector2 originalPosition = actor.Position;
                    object[] drawArgs = { actor, null }; typeof(NativeNarrative).GetMethod("BeforeActorDraw", All).Invoke(null, drawArgs);
                    Check(actor.Position.X == 47 && actor.sprite.GetColor().G == 0, "Native draw wrapper uses independently tinted sprite without moving gameplay state permanently");
                    typeof(NativeNarrative).GetMethod("AfterActorDraw", All).Invoke(null, new[] { drawArgs[1] });
                    Check(ReferenceEquals(original, actor.sprite) && actor.Position == originalPosition, "Native draw finalizer restores sprite and position");
                    host.behaviors.SetFlag("count", "9"); NativeNarrative.CaptureResults(host);
                }
                for (int tick = 0; tick < 40; tick++) resultTree.Run(1f / 60);
                Check((int)typeof(NativeNarrative).GetField("resultPage", All).GetValue(null) == -1, "Native statistics remain the first result page after scene disposal");
                resultPress = true;
                Check(resultTree.Run(1f / 60) == BehaviorTree.BTresult.Running && (int)typeof(NativeNarrative).GetField("resultPage", All).GetValue(null) == 0,
                    "Native confirm advances to authored results instead of leaving statistics");
                Capture(device, target, batch, () => DrawNativeStats(stats), Path.Combine(output, "narrative-results.png"));
                var displayed = new Color[480 * 360]; target.GetData(displayed);
                Check(displayed[0] == SceneValidation.ParseColor(scene.ResultPages[0].Background, "test"), "Already-compiled native draw caller shows authored results background");
                var pages = (Array)typeof(NativeNarrative).GetField("resultPages", All).GetValue(null);
                Check(((string[])Field(pages.GetValue(0), "Lines")).Contains("Count: 9"), "Result pages retain final counters after resource teardown");
                resultPress = false;
                bool waiting = true;
                for (int tick = 0; tick < 40; tick++) waiting &= resultTree.Run(1f / 60) == BehaviorTree.BTresult.Running;
                Check(waiting, "Authored page waits for a new press");
                resultPress = true;
                Check(resultTree.Run(1f / 60) == BehaviorTree.BTresult.Success, "Second native confirm completes authored results");
            }
            finally { NativeNarrative.Release(); entities.RemoveObject(actor); entities.RemoveObject(merchant); }
            }
        }
        private static void Capture(GraphicsDevice device, RenderTarget2D target, SpriteBatch batch, Action draw, string path)
        {
            device.SetRenderTarget(target); device.Clear(new Color(15, 18, 24));
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); draw(); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[480 * 360]; target.GetData(pixels);
            using (var bitmap = new System.Drawing.Bitmap(480, 360))
            { for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++) { Color c = pixels[y * 480 + x]; bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)); } bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
    }
}
