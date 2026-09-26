using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Windows.Forms;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

internal static class UiGraphicsTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private sealed class DeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }
    private sealed class ConnectedPad : IPad
    {
        private readonly IPad native;
        internal ConnectedPad(IPad value) { native = value; }
        public string ButtonToString(int value) { return native.ButtonToString(value); }
        public PadBinding GetDefaultBind() { return native.GetDefaultBind(); }
        public int[] GetPressedButtons() { return new int[0]; }
        public bool IsConnected() { return true; }
        public string GetSaveIdentifier() { return "ui-fixture"; }
        public string GetPrintName() { return "UI fixture"; }
    }
    private sealed class Factory
    {
        public IList Drawables { get; private set; }
        public Factory() { Drawables = new ArrayList(); }
        public void AddDrawable(object value) { Drawables.Add(value); }
    }
    private sealed class PointerButton : IBTSimpleMenuItem
    {
        internal int Clicks;
        protected override BTresult MyRun(TickData data)
        { if (ControllerManager.instance.MenuController.GetPadState().confirm) Clicks++; return BTresult.Failure; }
        public override Point GetSize() { return new Point(110, 18); }
        public override void Draw(int x, int y, bool selected) { UiTheme.TextLine("POINTER TEST", new Vector2(x, y), UiTheme.Text, true); }
    }
    private static object New(Type type, params object[] args)
    { return Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, args, null); }
    private static void Set(object value, string name, object data)
    { value.GetType().GetField(name, Flags).SetValue(value, data); }
    private static object Get(object value, string name)
    { return value.GetType().GetField(name, Flags).GetValue(value); }
    private static void Call(object value, string name)
    { value.GetType().GetMethod(name, Flags).Invoke(value, null); }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private static Action sceneDraw;
    private sealed class RunningProbe : IBTnode
    {
        internal int Runs;
        protected override BTresult MyRun(TickData data) { Runs++; return BTresult.Running; }
    }
    private static void InterruptedMenus(Game1 game, ControllerManager manager)
    {
        var input = typeof(MenuController).GetField("_menu_state", Flags);
        foreach (string label in new[] { "Settings", "Next" })
        {
            MenuSelector menu = label == "Next" ? new MenuSelectorClosePopup(new GuiFormat()) : new MenuSelector(new GuiFormat());
            var child = new RunningProbe(); menu.AddChild(new TextButton(label, child)); menu.Initialize(false);
            input.SetValue(manager.MenuController, new PadState()); menu.Run(new TickData(.016f, 1));
            input.SetValue(manager.MenuController, new PadState { confirm = true }); menu.Run(new TickData(.016f, 2));
            Check(child.Runs == 1, label + " opens on explicit confirmation");
            menu.SetResult(BTresult.Failure); input.SetValue(manager.MenuController, new PadState()); menu.Run(new TickData(.016f, 3));
            Check(menu.Children[0].last_result != BTresult.Running, label + " clears interrupted decorator state on exit");
            for (int tick = 4; tick < 9; tick++) menu.Run(new TickData(.016f, tick));
            Check(child.Runs == 1, label + " cannot reopen itself after its parent was interrupted");
            input.SetValue(manager.MenuController, new PadState { confirm = true }); menu.Run(new TickData(.016f, 9));
            Check(child.Runs == 2, label + " reopens only after another explicit confirmation");
            input.SetValue(manager.MenuController, new PadState()); menu.Run(new TickData(.016f, 10));
            Check(child.Runs == 3, label + " keeps updating its genuinely active child");
        }
        var nested = new MenuSelector(new GuiFormat());
        var deepest = new RunningProbe();
        nested.AddChild(new TextButton("Settings", deepest)); nested.Initialize(false);
        var outer = new MenuSelectorClosePopup(new GuiFormat());
        outer.AddChild(new TextButton("Next", nested)); outer.Initialize(false);
        input.SetValue(manager.MenuController, new PadState()); outer.Run(new TickData(.016f, 1));
        typeof(UiPointer).GetMethod("Queue", Flags).Invoke(null, new object[] { outer, UiAction.Confirm });
        outer.Run(new TickData(.016f, 2));
        for (int tick = 3; tick < 100; tick++) outer.Run(new TickData(.016f, tick));
        Check(deepest.Runs == 0 && !manager.MenuController.GetPadState().confirm,
            "One mouse confirmation opens one menu layer and never leaks into nested Settings");
        input.SetValue(manager.MenuController, new PadState { confirm = true }); outer.Run(new TickData(.016f, 100));
        Check(deepest.Runs == 1, "Nested Settings still accepts a fresh keyboard confirmation");
        input.SetValue(manager.MenuController, new PadState());
    }
    private sealed class TimedEntity : EntityComponent.Entity
    {
        protected override void Update(float delta) { SpendTime(); }
    }
    private sealed class TimedComponent : EntityComponent.Component
    {
        protected override void Update(float delta) { SpendTime(); }
        protected override void LateUpdate(float delta) { SpendTime(); }
    }
    private static void SpendTime()
    {
        long until = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency / 333;
        while (System.Diagnostics.Stopwatch.GetTimestamp() < until) { }
    }
    private static void TraceDispatch()
    {
        var trace = typeof(UIApi).Assembly.GetType("JKRuntime.StartupTrace", true);
        if (!(bool)trace.GetProperty("Enabled", Flags).GetValue(null, null)) return;
        trace.GetMethod("BeginAttempt", Flags).Invoke(null, null);
        var entity = new TimedEntity(); entity.AddComponents(new TimedComponent());
        entity.UpdateComponents(1f / 60f);
        var entries = (Array)trace.GetField("entries", Flags).GetValue(null);
        int count = (int)trace.GetField("count", Flags).GetValue(null);
        var names = new List<string>();
        for (int i = 0; i < count; i++)
        {
            object entry = entries.GetValue(i);
            var subject = (Type)Get(entry, "Subject");
            if (subject != null) names.Add(subject.Name + "." + Get(entry, "Name"));
        }
        Check(names.Contains("TimedEntity.UpdateComponents") && names.Contains("TimedComponent.LowUpdate")
            && names.Contains("TimedComponent.LowLateUpdate"), "Actual native dispatch records slow entities and both component stages");
        // Measure the hot diagnostic path separately from installation/JIT and
        // intentional slow callbacks. This is a fixture cost, not live game FPS.
        var empty = new EntityComponent.Entity();
        for (int i = 0; i < 1000; i++) empty.UpdateComponents(1f / 60f);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100000; i++) empty.UpdateComponents(1f / 60f);
        elapsed.Stop();
        Console.WriteLine("PERF trace dispatch: " + (elapsed.Elapsed.TotalMilliseconds * 1000 / 100000).ToString("F3") + " us/entity (empty native body plus hooks)");
        entity.Destroy(); empty.Destroy();
        trace.GetMethod("ResetForTest", Flags).Invoke(null, null);
    }
    private static bool DrawScene() { if (sceneDraw != null) sceneDraw(); return false; }
    private static void VerifyMenuAudio(ContentManager content, Game1 game)
    {
        typeof(Microsoft.Xna.Framework.Audio.SoundEffect).GetMethod("InitializeSoundEffect", Flags).Invoke(null, null);
        Check(typeof(Microsoft.Xna.Framework.Audio.SoundEffect).GetProperty("MasterVoice", Flags).GetValue(null, null) != null, "Native XAudio device is initialized for menu playback");
        var prefsType = typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefs");
        var managerType = typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime");
        var instance = managerType.BaseType.GetField("instance", Flags); object previous = instance.GetValue(null);
        var manager = FormatterServices.GetUninitializedObject(managerType);
        var prefs = Activator.CreateInstance(prefsType);
        prefsType.GetField("master", Flags).SetValue(prefs, .25f);
        prefsType.GetField("sfx_on", Flags).SetValue(prefs, false);
        managerType.BaseType.GetField("m_settings", Flags).SetValue(manager, prefs); instance.SetValue(null, manager);
        var menu = game.contentManager.audio.menu;
        var playback = typeof(UiSounds).GetField("TestPlayback", Flags); object observer = playback.GetValue(null);
        var sounds = new List<JumpKing.XnaWrappers.JKSound>();
        try
        {
            playback.SetValue(null, null);
            foreach (string name in new[] { "selectA", "selectC", "menu_fail" })
                sounds.Add(new JumpKing.XnaWrappers.JKSound(content.Load<Microsoft.Xna.Framework.Audio.SoundEffect>("Content/audio/gui_sfx/" + name), JumpKing.XnaWrappers.SoundType.SFX));
            // Keep these muted fixture voices alive across first-use JIT/device
            // work; production menu sounds retain their normal one-shot setting.
            foreach (var sound in sounds) sound.IsLooped = true;
            menu.CursorMove = sounds[0]; menu.Select = sounds[1]; menu.MenuFail = sounds[2];
            Check(ReferenceEquals(Game1.instance, game) && playback.GetValue(null) == null, "Native audio fixture uses the live game and production playback");
            sounds[0].Play();
            Check(sounds[0].State == JumpKing.XnaWrappers.JKSoundState.Playing, "Native JKSound direct playback is available");
            sounds[0].Stop();
            Check(System.Threading.SpinWait.SpinUntil(() => sounds.All(s => s.State == JumpKing.XnaWrappers.JKSoundState.Stopped), 250), "Audio fixture is idle before testing silent events");
            menu.OnMoveCursor(); menu.OnBack();
            Check(sounds.All(s => s.State == JumpKing.XnaWrappers.JKSoundState.Stopped), "Installed game's own navigation and back methods are silent");
            menu.OnSelect();
            Check(sounds[0].State == JumpKing.XnaWrappers.JKSoundState.Playing, "Installed game's accepted menu action uses selectA");
            sounds[0].Stop();
            Check(System.Threading.SpinWait.SpinUntil(() => sounds[0].State == JumpKing.XnaWrappers.JKSoundState.Stopped, 250), "Native reference cue stopped before service comparison");
            foreach (UiSound cue in Enum.GetValues(typeof(UiSound)))
            {
                int expected = cue == UiSound.Error ? 2 : 0;
                UiSounds.Play(cue);
                if (cue == UiSound.Move || cue == UiSound.Back)
                {
                    Check(sounds.All(s => s.State == JumpKing.XnaWrappers.JKSoundState.Stopped), "Native navigation/back policy is silent for " + cue);
                    continue;
                }
                Check(System.Threading.SpinWait.SpinUntil(() => sounds[expected].State == JumpKing.XnaWrappers.JKSoundState.Playing, 250), "Native audio asset plays for " + cue);
                foreach (var sound in sounds) sound.Stop();
                Check(System.Threading.SpinWait.SpinUntil(() => sounds.All(s => s.State == JumpKing.XnaWrappers.JKSoundState.Stopped), 250), "Native menu audio stops cleanly");
            }
            var voices = sounds.Select(s => (Microsoft.Xna.Framework.Audio.SoundEffectInstance)typeof(JumpKing.XnaWrappers.JKSound).GetField("m_instance", Flags).GetValue(s)).ToArray();
            Check(voices.All(v => v.Volume == 0), "UI feedback respects native SFX mute");
            InterruptedMenus(game, ControllerManager.instance);
            foreach (var sound in sounds) sound.Stop();
            Check(System.Threading.SpinWait.SpinUntil(() => sounds.All(s => s.State == JumpKing.XnaWrappers.JKSoundState.Stopped), 250),
                "Lifecycle fixture voices stop before unmuted volume assertions");
            prefsType.GetField("sfx_on", Flags).SetValue(prefs, true);
            JumpKing.XnaWrappers.JKSound.OnSoundSettingsChange((JumpKing.PlayerPreferences.SoundPrefs)prefs);
            Check(voices.All(v => Math.Abs(v.Volume - .25f) < .001f), "UI feedback respects native master volume");
        }
        finally
        {
            foreach (var sound in sounds) sound.Dispose();
            menu.CursorMove = menu.Select = menu.MenuFail = null;
            playback.SetValue(null, observer); instance.SetValue(null, previous);
        }
    }
    private static Action WarmDrawCaller(Game1 game, Assembly engine)
    {
        // Warm an optimized caller before installing UI hooks, as the title/loading
        // frames do in the real game. Tiny Game1 wrappers may already be inlined.
        game.m_game = (JumpGame)FormatterServices.GetUninitializedObject(typeof(JumpGame));
        var harmony = engine.GetType("HarmonyLib.Harmony");
        var metadata = engine.GetType("HarmonyLib.HarmonyMethod");
        var owner = Activator.CreateInstance(harmony, new object[] { "fixture.pointer.scene" });
        var prefix = Activator.CreateInstance(metadata, new object[] { typeof(UiGraphicsTests).GetMethod("DrawScene", Flags) });
        // Substitute only the scene body, after production BeginDraw prefixes.
        metadata.GetField("priority").SetValue(prefix, 0);
        harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(owner,
            new object[] { typeof(JumpGame).GetMethod("Draw", Flags), prefix, null, null, null });
        var caller = new DynamicMethod("WarmGameDraw", typeof(void), new[] { typeof(Game1) }, typeof(UiGraphicsTests), true);
        var il = caller.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, typeof(Game1).GetMethod("MyDraw", Flags)); il.Emit(OpCodes.Ret);
        var invoke = (Action<Game1>)caller.CreateDelegate(typeof(Action<Game1>));
        invoke(game);
        return () => invoke(game);
    }

    private static int Main(string[] args)
    {
        string gameDir = args[0], repo = args[1], output = args[2];
        Directory.CreateDirectory(output); Directory.SetCurrentDirectory(output);
        using (var window = new Form { ShowInTaskbar = false })
        using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360 }))
        using (var target = new RenderTarget2D(device, 480, 360))
        using (var white = new Texture2D(device, 1, 1))
        {
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            typeof(Game1).GetField("_instance", Flags).SetValue(null, game);
            typeof(Game).GetField("_services", Flags).SetValue(game, services);
            foreach (var field in typeof(Game).GetFields(Flags).Where(x => x.FieldType == typeof(IGraphicsDeviceService))) field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
            game.contentManager = new JKContentManager();
            var pixel = (PixelTexture)FormatterServices.GetUninitializedObject(typeof(PixelTexture)); GC.SuppressFinalize(pixel);
            white.SetData(new[] { Color.White }); typeof(PixelTexture).GetField("_texture", Flags).SetValue(pixel, white); game.contentManager.Pixel = pixel;
            using (var content = new ContentManager(services, gameDir))
            {
                game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
                game.contentManager.font.LocationFont = content.Load<SpriteFont>("Content/font/sf_pixolde_bold");
                var frame = content.Load<Texture2D>("Content/gui/frame"); int cell = frame.Width / 3;
                game.contentManager.gui.FrameSprites = new Sprite[3, 3];
                for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) game.contentManager.gui.FrameSprites[x, y] = Sprite.CreateSprite(frame, new Rectangle(x * cell, y * cell, cell, cell));
                game.contentManager.gui.CheckBoxTrue = game.contentManager.gui.CheckBoxFalse = Sprite.CreateSprite(white, new Rectangle(0, 0, 1, 1));
                var colors = new Color[frame.Width * frame.Height]; frame.GetData(colors);
                Check(colors.Where(c => c.A == 255 && c.R > 0).GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key == UiTheme.Border,
                    "Shared border equals the native GuiFrame asset color");
                var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance = manager;
                typeof(ControllerManager).GetField("_menu_controller", Flags).SetValue(manager, new MenuController(manager));
                game.contentManager.gui.Cursor = content.Load<Texture2D>("Content/gui/cursor");
                var soundCues = new List<UiSound>();
                typeof(UiSounds).GetField("TestPlayback", Flags).SetValue(null, new Action<UiSound>(soundCues.Add));
                var standaloneHost = UIApi.CreateCompactGrid(new Factory(), new[] {
                    new UiCompactGridItemDefinition("First", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Second", new MenuSelector(new GuiFormat())) });
                var standaloneGrid = standaloneHost.GetType().GetProperty("Grid", Flags).GetValue(standaloneHost, null);
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(standaloneGrid, BTresult.Running);
                var selectGrid = standaloneGrid.GetType().GetMethod("SelectItem", Flags);
                selectGrid.Invoke(standaloneGrid, new object[] { 1 }); selectGrid.Invoke(standaloneGrid, new object[] { 1 });
                Check(soundCues.Count == 0, "Runtime grid navigation is silent without Harmony");
                soundCues.Clear();
                var soundMenu = new MenuSelector(new GuiFormat());
                soundMenu.AddChild(new PointerButton()); soundMenu.AddChild(new PointerButton()); soundMenu.Initialize(false);
                var selectionSetter = typeof(MenuSelector).GetProperty("Index", Flags).GetSetMethod(true);
                var selectCaller = new DynamicMethod("WarmSelection", typeof(void), new[] { typeof(MenuSelector), typeof(int) }, typeof(UiGraphicsTests), true);
                var selectIl = selectCaller.GetILGenerator(); selectIl.Emit(OpCodes.Ldarg_0); selectIl.Emit(OpCodes.Ldarg_1); selectIl.Emit(OpCodes.Call, selectionSetter); selectIl.Emit(OpCodes.Ret);
                var selectNative = (Action<MenuSelector, int>)selectCaller.CreateDelegate(typeof(Action<MenuSelector, int>));
                for (int warm = 0; warm < 100; warm++) selectNative(soundMenu, warm % 2);
                var engine = Assembly.LoadFrom(args.Length > 3 ? args[3] : Path.Combine(gameDir, "Content/JKMods/0Harmony.dll"));
                var nativeDraw = WarmDrawCaller(game, engine);
                var hooks = typeof(UIApi).Assembly.GetType("JKRuntime.UI.PointerHooks", true);
                var startup = typeof(JKRuntime.UI.ModEntry).GetMethod("InitializeMenuInput");
                Check(startup != null && startup.GetCustomAttributes(typeof(JumpKing.Mods.BeforeLevelLoadAttribute), false).Length == 1,
                    "Mouse initialization is registered for title-menu startup");
                startup.Invoke(null, null);
                Check((bool)hooks.GetField("installed", Flags).GetValue(null) && JKRuntime.RuntimeApi.State == "idle",
                    "Pointer hooks install before a level or player exists");
                selectGrid.Invoke(standaloneGrid, new object[] { 0 }); selectGrid.Invoke(standaloneGrid, new object[] { 0 });
                Check(soundCues.Count == 0, "Runtime grid navigation stays silent with Harmony");
                soundCues.Clear();
                TraceDispatch();
                if (args.Length > 4 && args[4] == "audio") VerifyMenuAudio(content, game);
                var pads = new List<PadInstance>(); typeof(ControllerManager).GetField("m_pads", Flags).SetValue(manager, pads);
                var keyboard = (IPad)New(typeof(Game1).Assembly.GetType("JumpKing.Controller.KeyboardPad"));
                pads.Add(new PadInstance(keyboard));
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(soundMenu, BTresult.Running);
                selectNative(soundMenu, 0); selectNative(soundMenu, 0);
                Check(soundCues.Count == 0, "Warmed native selection and repeated hover retain their original silence");
                soundCues.Clear();
                typeof(MenuController).GetField("_menu_state", Flags).SetValue(manager.MenuController, new PadState { cancel = true });
                soundMenu.Run(new TickData(.016f, 1));
                Check(soundCues.Count == 0, "Native menu cancellation retains its original silence");
                soundCues.Clear();
                string originalTitle = "A very long Workshop title with a shared prefix and its complete ending";
                var titleFormat = new GuiFormat { anchor_bounds = new Rectangle(0, 0, 130, 200) };
                var fittedTitle = TextInfo.CreateOneFittedInfo(titleFormat, originalTitle, Color.White, 1);
                Check(fittedTitle.Text != originalTitle, "Native Workshop fixture really truncates its title");
                var recoverTitle = typeof(UIApi).Assembly.GetType("JKRuntime.UI.NativeMenuText").GetMethod("Original", Flags);
                Check((string)recoverTitle.Invoke(null, new object[] { fittedTitle }) == originalTitle, "Grid recovers the original name before native truncation");
                var completedIcon = Sprite.CreateSprite(content.Load<Texture2D>("Content/gui/completed"));
                var completedTitle = new IconTextInfo(fittedTitle.Text, completedIcon);
                Check((string)recoverTitle.Invoke(null, new object[] { completedTitle }) == originalTitle, "Completed-level icon wrappers retain the original name");
                string otherTitle = originalTitle + " - another item";
                var otherFitted = TextInfo.CreateOneFittedInfo(titleFormat, otherTitle, Color.White, 1);
                Check(otherFitted.Text == fittedTitle.Text && (string)recoverTitle.Invoke(null, new object[] { otherFitted }) == otherTitle
                    && (string)recoverTitle.Invoke(null, new object[] { fittedTitle }) == originalTitle, "Identical truncated labels never mix up different Workshop names");
                var nativeCatalog = new MenuSelector(new GuiFormat());
                string originalAuthor = "Made by An Author With A Long Workshop Name";
                var fittedAuthor = TextInfo.CreateOneFittedInfo(titleFormat, originalAuthor, Color.Gray, game.contentManager.font.LocationFont, 1);
                var detailedRow = new DoubleTextInfoButton(completedTitle, fittedAuthor, new PointerButton());
                detailedRow.Texts = new[] { completedTitle, fittedAuthor, new TextInfo("Local mod", Color.Gray) };
                nativeCatalog.AddChild(detailedRow);
                var collectCards = typeof(UIApi).Assembly.GetType("JKRuntime.UI.WorkshopGridIntegration").GetMethods(Flags).Single(m => m.Name == "CollectCards" && m.GetParameters().Length == 1);
                var recoveredCards = (IList)collectCards.Invoke(null, new object[] { nativeCatalog });
                Check((string)Get(recoveredCards[0], "Label") == originalTitle && fittedTitle.Text != originalTitle,
                    "Workshop adapter passes the full title into the grid without changing the original native row");
                Check((string)Get(recoveredCards[0], "Subtitle") == originalAuthor + "\nLocal mod" && fittedAuthor.Text != originalAuthor,
                    "Workshop adapter preserves full authors and additional metadata without changing native rows");
                Check(Get(recoveredCards[0], "Icon") != null, "Completed Workshop items retain their native icon");
                var conditionalTitle = new ConditionalIconTextInfo("Conditional completion", completedIcon, Color.White, Color.Gray, false);
                var conditionalCatalog = new MenuSelector(new GuiFormat());
                conditionalCatalog.AddChild(new DoubleTextInfoButton(conditionalTitle, new TextInfo("Made by Example author", Color.Gray), new PointerButton()));
                var conditionalCards = (IList)collectCards.Invoke(null, new object[] { conditionalCatalog });
                var showIcon = conditionalCards[0].GetType().GetProperty("ShowIcon", Flags);
                Check(!(bool)showIcon.GetValue(conditionalCards[0], null), "Conditional native icon stays hidden when unavailable");
                conditionalTitle.Condition = true;
                Check((bool)showIcon.GetValue(conditionalCards[0], null), "Conditional native icon follows its source condition without rebuilding the grid");
                Func<Action, string, Color[]> capture = (draw, name) => {
                    // Each isolated capture owns its SpriteBatch and GPU buffers.
                    using (var batch = new SpriteBatch(device))
                    {
                        Game1.spriteBatch = batch; device.SetRenderTarget(target); device.Clear(Color.Black);
                        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                        draw(); batch.End(); device.SetRenderTarget(null);
                        var data = new Color[480 * 360]; target.GetData(data);
                        if (name != null)
                            using (var bitmap = new System.Drawing.Bitmap(480, 360))
                            {
                                for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++)
                                {
                                    Color c = data[y * 480 + x];
                                    bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
                                }
                                bitmap.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                            }
                        return data;
                    }
                };
                InventoryGraphicsTests.Run(content, capture, Check);
                var uiExample = Assembly.LoadFrom(Path.Combine(repo, "build/jk-runtime/_INTERNAL/UiModExample.Module.dll"));
                var examplePage = uiExample.GetType("ExampleSettingsPage", true);
                var exampleStack = new UiPageStack(pages => (IUiPage)Activator.CreateInstance(
                    examplePage, Flags, null, new object[] { pages }, null));
                exampleStack.OnOpen();
                try
                {
                    var rootPixels = capture(exampleStack.Draw, "sdk-scoped-page");
                    exampleStack.Push(new UiTextEntryPage("Name", "Example", value => { }));
                    var childPixels = capture(exampleStack.Draw, "sdk-child-text-page");
                    Check(exampleStack.Depth == 2 && UIApi.IsTextInputActive && !rootPixels.SequenceEqual(childPixels),
                        "Canonical SDK page renders its owned child with native fonts and text capture");
                }
                finally { exampleStack.OnClose(); }
                Check(!UIApi.IsTextInputActive, "Closing canonical SDK stack releases child text capture");
                foreach (var font in new[] { game.contentManager.font.MenuFont, game.contentManager.font.MenuFontSmall, game.contentManager.font.LocationFont })
                {
                    var exact = capture(() => UiTheme.DrawText(font, "WORKSHOP / Replays 123", new Vector2(20, 30), Color.White), null);
                    var fractional = capture(() => UiTheme.DrawText(font, "WORKSHOP / Replays 123", new Vector2(20.5f, 30.5f), Color.White), null);
                    Check(exact.SequenceEqual(fractional), "Fractional text origins match integer pixels for " + font.LineSpacing + " px font");
                }
                var metadataGrid = New(typeof(UIApi).Assembly.GetType("JKRuntime.UI.SquareGridSelector"), recoveredCards);
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(metadataGrid, BTresult.Running);
                capture(() => Call(metadataGrid, "Draw"), "workshop-native-metadata");
                var factory = new Factory();
                var pointerMenu = new MenuSelector(new GuiFormat { anchor_bounds = new Rectangle(100, 100, 240, 100), all_padding = 10 });
                var pointerButton = new PointerButton(); pointerMenu.AddChild(pointerButton); pointerMenu.Initialize(false); pointerMenu.Run(new TickData(.016f, 1));
                sceneDraw = pointerMenu.Draw;
                capture(nativeDraw, "native-mouse-menu");
                var hit = new Point(pointerMenu.GetBounds().X + 15, pointerMenu.GetBounds().Y + 12);
                var poll = typeof(UiPointer).GetMethod("Poll", Flags);
                poll.Invoke(null, new object[] { hit, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { hit, true, true, true, false, 0, false });
                pointerMenu.Run(new TickData(.016f, 2)); Check(UiPointer.Visible && pointerButton.Clicks == 0, "Warmed native draw loop enables cursor without clicking through");
                poll.Invoke(null, new object[] { hit, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { hit, true, true, true, false, 0, false });
                pointerMenu.Run(new TickData(.016f, 3));
                Check(pointerButton.Clicks == 1 && !manager.MenuController.GetPadState().confirm, "Native BT menu receives one mouse action and consumes it");
                typeof(UiPointer).GetMethod("Reset", Flags).Invoke(null, null);
                string[] longNames = { "Mega Gameplay Expansion: movement and abilities", "Mega Mapping Expansion: world objects and effects", "Subframe Charge - precision and optimizations", "WorkshopCollectionWithAnUnbrokenLongName" };
                var smallFont = game.contentManager.font.MenuFontSmall;
                var splitLabel = typeof(UIApi).Assembly.GetType("JKRuntime.UI.SquareGridSelector").GetMethod("SplitLabel", Flags);
                foreach (string title in longNames)
                {
                    var lines = (string[])splitLabel.Invoke(null, new object[] { title, 121, smallFont });
                    Check(lines.Length <= 4 && lines.All(line => smallFont.MeasureString(line).X <= 121), "Complete grid title fits: " + title);
                    Check(string.Concat(lines).Replace(" ", "") == title.Replace(" ", ""), "Wrapping preserves every title character");
                }
                string nativeCapacity = "W";
                while (game.contentManager.font.MenuFont.MeasureString(nativeCapacity + "W").X <= 280) nativeCapacity += "W";
                int nativeWidth = (int)game.contentManager.font.MenuFont.MeasureString(nativeCapacity).X;
                var capacityLines = (string[])splitLabel.Invoke(null, new object[] { nativeCapacity, 121, smallFont });
                Check(nativeWidth <= 280 && capacityLines.Length <= 4, "Cell retains a full native Workshop row of wide glyphs");
                var host = UIApi.CreateCompactGrid(factory, new[] {
                    new UiCompactGridItemDefinition(longNames[0], new MenuSelector(new GuiFormat()), "Made by Example author"),
                    new UiCompactGridItemDefinition(longNames[1], new MenuSelector(new GuiFormat()), "Made by Another creator"),
                    new UiCompactGridItemDefinition(longNames[2], new MenuSelector(new GuiFormat()), "Local mod"),
                    new UiCompactGridItemDefinition(longNames[3], new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Golden Boots", new MenuSelector(new GuiFormat()), "Made by Example author"),
                    new UiCompactGridItemDefinition("Smooth Camera", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Ball King", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Casual Jumping", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("More Items", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Replays", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Wardrobe+", new MenuSelector(new GuiFormat())),
                    new UiCompactGridItemDefinition("Example Level", new MenuSelector(new GuiFormat()), "Made by Example author"),
                    new UiCompactGridItemDefinition("Last Page", new MenuSelector(new GuiFormat()), "Made by Example author") });
                var grid = host.GetType().GetProperty("Grid", Flags).GetValue(host, null);
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(grid, BTresult.Running);
                var selectedPixels = capture(() => Call(grid, "Draw"), "workshop-grid");
                Rectangle titleBounds = ((MenuSelector)grid).GetBounds();
                var firstCard = ((IList)Get(grid, "allItems"))[0];
                string firstLine = ((string[])Get(firstCard, "TitleLines"))[0];
                var nativeCursorFormat = new GuiFormat { padding = new GuiSpacing { left = 16, top = 3 } };
                var nativeCursorPixels = capture(() => nativeCursorFormat.DrawMenuItemsWithCursor(
                    new IMenuItem[] { new TextButton(firstLine, null, smallFont) },
                    new Rectangle(titleBounds.X + 14, titleBounds.Y + 12, 142, 76), 0), null);
                Check(Enumerable.Range(titleBounds.Y + 15, 11).All(y => Enumerable.Range(titleBounds.X + 14, 142)
                    .All(x => selectedPixels[y * 480 + x] == nativeCursorPixels[y * 480 + x])),
                    "Grid cursor and first text line exactly match native padding and selected offset");
                var measuredLayout = Get(grid, "layout");
                int secondRowY = titleBounds.Y + 12 + ((int[])Get(measuredLayout, "Tops"))[3];
                int dividerX = titleBounds.X + 14 + 142 + 2;
                Check(selectedPixels[(secondRowY - 4) * 480 + titleBounds.X + 30] == UiTheme.Border
                    && selectedPixels[(secondRowY - 4) * 480 + dividerX] == Color.Black,
                    "Horizontal dividers are separate segments with gaps between columns");
                var nextCard = ((IList)Get(grid, "allItems"))[1];
                int pairHeight = Math.Max((int)Get(firstCard, "ContentHeight"), (int)Get(nextCard, "ContentHeight"));
                Check(Enumerable.Range(titleBounds.Y + 15, pairHeight - 6).All(y => selectedPixels[y * 480 + dividerX] == UiTheme.Border)
                    && selectedPixels[(titleBounds.Y + 12 + pairHeight) * 480 + dividerX] == Color.Black,
                    "Vertical divider follows the adjacent content height and stops before the row gap");
                selectNative((MenuSelector)grid, 1);
                var unselectedPixels = capture(() => Call(grid, "Draw"), null);
                Check(Enumerable.Range(titleBounds.Y + 15, 44).All(y => Enumerable.Range(titleBounds.X + 30, 121)
                    .All(x => unselectedPixels[y * 480 + x] == selectedPixels[y * 480 + x + 5])),
                    "Selected grid text moves exactly five pixels like native GuiFormat, without reflow");
                selectNative((MenuSelector)grid, 0);
                capture(() => { Call(grid, "Draw"); typeof(UiPointer).GetMethod("DrawCursor", Flags).Invoke(null, new object[] { new Point(121, 98) }); }, "workshop-mouse");
                soundCues.Clear();
                var drawGrid = (Action)Delegate.CreateDelegate(typeof(Action), grid, grid.GetType().GetMethod("Draw"));
                using (var batch = new SpriteBatch(device))
                {
                    Game1.spriteBatch = batch; device.SetRenderTarget(target);
                    var beginUi = (Action)Delegate.CreateDelegate(typeof(Action), typeof(UiPointer).GetMethod("BeginDraw", Flags));
                    var endUi = (Action)Delegate.CreateDelegate(typeof(Action), typeof(UiPointer).GetMethod("EndDraw", Flags));
                    Action drawFrame = () => { beginUi(); batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp); drawGrid(); endUi(); batch.End(); };
                    for (int warm = 0; warm < 100; warm++) drawFrame();
                    int collections = GC.CollectionCount(0);
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    for (int sample = 0; sample < 1000; sample++) drawFrame();
                    timer.Stop(); device.SetRenderTarget(null);
                    Console.WriteLine("PERF Workshop grid: " + (timer.Elapsed.TotalMilliseconds / 1000).ToString("F4") + " ms/draw, Gen0=" + (GC.CollectionCount(0) - collections));
                }
                Check(soundCues.Count == 0, "Rendering the grid never emits audio");
                var manyDefinitions = Enumerable.Range(0, 60).Select(i => new UiCompactGridItemDefinition("Workshop item " + i,
                    new MenuSelector(new GuiFormat()), i % 4 == 0 ? "Made by A Longer Example Author" : "Made by Author")).ToArray();
                var pagingHost = UIApi.CreateCompactGrid(factory, manyDefinitions);
                grid = pagingHost.GetType().GetProperty("Grid", Flags).GetValue(pagingHost, null);
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(grid, BTresult.Running);
                drawGrid = (Action)Delegate.CreateDelegate(typeof(Action), grid, grid.GetType().GetMethod("Draw"));
                selectNative((MenuSelector)grid, 59);
                capture(drawGrid, "workshop-last-page");
                var gridLayout = Get(grid, "layout");
                var gridPages = (Array)Get(gridLayout, "Pages");
                Check(gridPages.Length > 1, "Large Workshop catalog exercises adaptive pagination");
                int lastPageFirst = (int)Get(gridPages.GetValue(gridPages.Length - 1), "First");
                Check(((MenuSelector)grid).GetBounds().Bottom <= 360, "Paginated native grid stays within the game viewport");
                sceneDraw = drawGrid;
                capture(nativeDraw, null);
                Rectangle gridBounds = ((MenuSelector)grid).GetBounds();
                var lastPagePoint = new Point(gridBounds.X + 30, gridBounds.Y + 20);
                poll.Invoke(null, new object[] { lastPagePoint, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { lastPagePoint, true, true, true, false, 0, false });
                poll.Invoke(null, new object[] { lastPagePoint, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { lastPagePoint, true, true, true, false, 0, false });
                Check((int)typeof(MenuSelector).GetProperty("Index", Flags).GetValue(grid, null) == lastPageFirst,
                    "Cached pointer callbacks select the correct item on later pages");
                var delivered = (UiInput)typeof(UiPointer).GetMethod("Read", Flags).Invoke(null, new object[] { grid, new UiInput() });
                Check(delivered.Action == UiAction.Confirm && soundCues.Count == 0,
                    "Grid pointer click queues activation without adding navigation audio");
                typeof(UiPointer).GetMethod("Reset", Flags).Invoke(null, null);
                var overflowFormat = new GuiFormat { anchor_bounds = new Rectangle(0, 125, 480, 230), anchor = new Vector2(.5f, 0), all_padding = 16, element_margin = 8 };
                var runtimeMenu = new MenuSelector(overflowFormat);
                foreach (string label in new[] { "Runtime diagnostics", "Settings", "Pinned settings", "Controls+", "ModsDebugActions", "Back" })
                    runtimeMenu.AddChild(new TextButton(label, new RunningProbe()));
                runtimeMenu.Initialize(false); typeof(IBTnode).GetField("m_last_result", Flags).SetValue(runtimeMenu, BTresult.Running);
                sceneDraw = runtimeMenu.Draw;
                capture(nativeDraw, "runtime-settings-bounded");
                Check(runtimeMenu.GetBounds().Top >= 12 && runtimeMenu.GetBounds().Bottom <= 348, "Runtime settings are repositioned entirely inside the viewport");
                var workshopMenu = new MenuSelector(overflowFormat);
                foreach (string label in new[] { "Browse Workshop", "Levels", "Collections", "Skins", "Mods", "Wardrobe+", "Back" })
                    workshopMenu.AddChild(new TextButton(label, new RunningProbe()));
                workshopMenu.Initialize(false); typeof(IBTnode).GetField("m_last_result", Flags).SetValue(workshopMenu, BTresult.Running);
                sceneDraw = workshopMenu.Draw;
                capture(nativeDraw, "workshop-root-bounded");
                Check(workshopMenu.GetBounds().Top >= 12 && workshopMenu.GetBounds().Bottom <= 348, "Workshop with Wardrobe fits inside the viewport");
                var tallMenu = new MenuSelectorClosePopup(overflowFormat);
                for (int i = 0; i < 24; i++) tallMenu.AddChild(new TextButton("Setting " + (i + 1), new RunningProbe()));
                tallMenu.Initialize(false); typeof(IBTnode).GetField("m_last_result", Flags).SetValue(tallMenu, BTresult.Running);
                sceneDraw = tallMenu.Draw;
                var viewportType = typeof(UIApi).Assembly.GetType("JKRuntime.UI.NativeMenuViewport");
                for (int i = 0; i < 24; i++)
                {
                    selectNative(tallMenu, i); capture(nativeDraw, i == 23 ? "native-settings-scrolled" : null);
                    object[] layoutArgs = { tallMenu, null }; viewportType.GetMethod("TryGet", Flags).Invoke(null, layoutArgs);
                    Check((int)Get(layoutArgs[1], "First") <= i && (int)Get(layoutArgs[1], "Last") > i
                        && tallMenu.GetBounds().Bottom <= 348, "Overflow menu reveals selected row " + i + " inside its frame");
                }
                object[] scrollLayoutArgs = { tallMenu, null }; viewportType.GetMethod("TryGet", Flags).Invoke(null, scrollLayoutArgs);
                int firstVisible = (int)Get(scrollLayoutArgs[1], "First");
                Point scrolledHit = new Point(tallMenu.GetBounds().X + 20, tallMenu.GetBounds().Y + 20);
                poll.Invoke(null, new object[] { scrolledHit, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { scrolledHit, true, true, true, false, 0, false });
                poll.Invoke(null, new object[] { scrolledHit, true, true, false, false, 0, false });
                scrolledHit.X++;
                poll.Invoke(null, new object[] { scrolledHit, true, true, false, false, 0, false });
                Check((int)typeof(MenuSelector).GetProperty("Index", Flags).GetValue(tallMenu, null) == firstVisible,
                    "Native popup pointer regions follow the scrolled visible rows");
                typeof(UiPointer).GetMethod("Reset", Flags).Invoke(null, null);
                for (int i = 0; i < 7; i++) UIApi.RegisterDebugAction(new UiDebugActionDefinition("fixture." + i, "More Items", "Test action " + (i + 1), delegate { }));
                var debug = New(typeof(UIApi).Assembly.GetType("JKRuntime.UI.DebugActionsPageNode"));
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(debug, BTresult.Running); Set(debug, "index", 6);
                var debugPixels = capture(() => Call(debug, "Draw"), "debug-full-list");
                Check(Enumerable.Range(285, 5).All(y => Enumerable.Range(74, 332).All(x => debugPixels[y * 480 + x] == Color.Black)),
                    "Full debug list leaves room above its footer");
                Check(Enumerable.Range(311, 6).All(y => Enumerable.Range(74, 332).All(x => debugPixels[y * 480 + x] == Color.Black)),
                    "Debug footer leaves room above the bottom frame decoration");
                for (int i = 7; i < 24; i++) UIApi.RegisterDebugAction(new UiDebugActionDefinition("fixture." + i, "More Items", "Test action " + (i + 1), delegate { }));
                var scrollingDebug = New(typeof(UIApi).Assembly.GetType("JKRuntime.UI.DebugActionsPageNode"));
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(scrollingDebug, BTresult.Running);
                sceneDraw = () => Call(scrollingDebug, "Draw");
                capture(nativeDraw, null);
                poll.Invoke(null, new object[] { new Point(90, 260), true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { new Point(90, 260), true, true, true, false, 0, false });
                poll.Invoke(null, new object[] { new Point(90, 260), true, true, false, false, 0, false });
                for (int move = 0; move < 5; move++)
                {
                    poll.Invoke(null, new object[] { new Point(91 + move, 260), true, true, false, false, 0, false });
                    capture(nativeDraw, null);
                    Check((int)Get(scrollingDebug, "index") == 6, "Repeated bottom-row hovering does not scroll the real list");
                }
                poll.Invoke(null, new object[] { new Point(96, 260), true, true, false, false, -120, false });
                capture(nativeDraw, null);
                Check((int)Get(scrollingDebug, "index") == 7, "Wheel scrolls the real list by one row");
                for (int move = 0; move < 3; move++)
                {
                    poll.Invoke(null, new object[] { new Point(91 + move, 86), true, true, false, false, -120, false });
                    capture(nativeDraw, null);
                    Check((int)Get(scrollingDebug, "index") == 1, "Top-row hovering preserves the wheel scroll position");
                }
                typeof(UiPointer).GetMethod("Reset", Flags).Invoke(null, null);
                var replays = Assembly.LoadFrom(Path.Combine(repo, "build/replays/_INTERNAL/Replays.Module.dll"));
                var library = (IUiPage)New(replays.GetType("Replays.ReplayLibraryPage"));
                capture(library.Draw, "replays-library");
                var dataReplay = New(replays.GetType("Replays.ReplayData"));
                var header = New(replays.GetType("Replays.ReplayHeader")); Set(header, "WorldName", "New Babe+"); Set(dataReplay, "Header", header);
                var frameList = (IList)New(typeof(List<>).MakeGenericType(replays.GetType("Replays.ReplayFrame")));
                for (int i = 0; i < 6000; i++) frameList.Add(New(replays.GetType("Replays.ReplayFrame")));
                Set(dataReplay, "Frames", frameList);
                var viewer = New(replays.GetType("Replays.ReplayViewerPage"), dataReplay);
                var keyboardViewer = capture(() => { Call(viewer, "DrawHeader"); Call(viewer, "DrawControls"); }, "replays-viewer-keyboard");
                var items = Assembly.LoadFrom(Path.Combine(repo, "build/more-items/_INTERNAL/MoreItems.Module.dll"));
                var hammerDefinition = items.GetType("MoreItems.HammerDefinition", true);
                hammerDefinition.GetMethod("RegisterModule", Flags).Invoke(null, null);
                hammerDefinition.GetMethod("Register", Flags).Invoke(null, null);
                foreach (var fixture in UIApi.GetDebugActions().Where(a => a.Id.StartsWith("fixture.")).ToArray()) UIApi.UnregisterDebugAction(fixture.Id);
                var hammerValue = UIApi.GetDebugActions().Single(a => a.Id == "more-items.hammer-strength");
                hammerValue.Execute();
                var hammerDebug = New(typeof(UIApi).Assembly.GetType("JKRuntime.UI.DebugActionsPageNode"));
                typeof(IBTnode).GetField("m_last_result", Flags).SetValue(hammerDebug, BTresult.Running);
                Set(hammerDebug, "index", UIApi.GetDebugActions().IndexOf(hammerValue));
                sceneDraw = () => Call(hammerDebug, "Draw");
                capture(nativeDraw, "hammer-debug-default");
                var hammerRight = new Point(395, 119);
                poll.Invoke(null, new object[] { hammerRight, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { hammerRight, true, true, true, false, 0, false });
                poll.Invoke(null, new object[] { hammerRight, true, true, false, false, 0, false });
                poll.Invoke(null, new object[] { hammerRight, true, true, true, false, 0, false });
                UiAction hammerAction = ((UiInput)typeof(UiPointer).GetMethod("Read", Flags).Invoke(null, new object[] { hammerDebug, new UiInput() })).Action;
                Check(hammerAction == UiAction.Right, "Hammer debug arrow has its own mouse hit target");
                hammerDebug.GetType().GetMethod("InvokeAction", Flags).Invoke(null, new object[] { hammerValue, hammerAction });
                Check(hammerValue.GetLabel() == "Hammer: 105%", "Production Hammer debug control updates the live percentage");
                capture(nativeDraw, "hammer-debug-adjusted");
                hammerValue.Execute();
                var hammerOffer = UIApi.GetMerchantOffers().Single(o => o.Id == "more-items.hammer");
                var inventoryType = items.GetType("MoreItems.ItemInventory", true);
                inventoryType.GetField("loaded", Flags).SetValue(null, true);
                inventoryType.GetField("available", Flags).SetValue(null, true);
                UIApi.RegisterCurrency(new UiCurrencyDefinition(hammerOffer.CurrencyId, "Silver Coin", "Silver Coins", 10,
                    () => 3, amount => amount <= 3, amount => { }, rect => UiTheme.TextLine("S", rect.Location.ToVector2(), UiTheme.Text, true)));
                var hammerShop = (IUiPage)New(typeof(UIApi).Assembly.GetType("JKRuntime.UI.MerchantPage"), "Merchant", new List<MerchantOfferDefinition> { hammerOffer }, null);
                capture(hammerShop.Draw, "hammer-merchant");
                var option = New(items.GetType("MoreItems.RewindRenderingOption")); Call(option, "GetSize");
                var visibility = New(items.GetType("JumpKingJetpack.JetpackVisibilityOption")); Call(visibility, "GetSize");
                var trail = New(items.GetType("JumpKingJetpack.JetpackTrailOption")); Call(trail, "GetSize");
                var volume = New(items.GetType("JumpKingJetpack.JetpackVolumeOption"));
                capture(() => { new UiFrame(new Rectangle(24, 20, 432, 320)).Draw();
                    option.GetType().GetMethod("Draw").Invoke(option, new object[] { 50, 80, true });
                    visibility.GetType().GetMethod("Draw").Invoke(visibility, new object[] { 50, 120, true });
                    trail.GetType().GetMethod("Draw").Invoke(trail, new object[] { 50, 160, true });
                    volume.GetType().GetMethod("IconDraw", Flags).Invoke(volume, new object[] { .5f, 50, 204, 0 });
                    UiTheme.CommandBar(UiTheme.FooterRow(new Rectangle(24, 20, 432, 320)), UiInputHints.Command(UiAction.Confirm, "Select"), UiInputHints.Command(UiAction.Cancel, "Back")); }, "more-items-settings");
                var shop = (IUiPage)New(typeof(UIApi).Assembly.GetType("JKRuntime.UI.MerchantPage"), "Merchant", null, null);
                capture(shop.Draw, "merchant-keyboard");
                var xbox = (IPad)New(typeof(Game1).Assembly.GetType("JumpKing.Controller.XboxPad"), PlayerIndex.One);
                pads.Clear(); pads.Add(new PadInstance(new ConnectedPad(xbox)));
                var controllerViewer = capture(() => { Call(viewer, "DrawHeader"); Call(viewer, "DrawControls"); }, "replays-viewer-controller");
                Check(keyboardViewer.Take(480 * 60).SequenceEqual(controllerViewer.Take(480 * 60)), "Repeated header rendering retains every glyph across device switches");
                capture(shop.Draw, "merchant-controller");
                pads.Clear(); pads.Add(new PadInstance(keyboard)); pads[0].GetBind().confirm = new[] { 13 };
                capture(library.Draw, "replays-library-rebound");
                Check(UiInputHints.Key(UiAction.Confirm) == "ENTER", "Consumer hints resolve current bindings after device changes");
            }
        }
        Console.WriteLine("[OK] UI graphics: " + output); return 0;
    }
}
