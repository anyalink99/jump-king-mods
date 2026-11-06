using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;
using JKRuntime.State;
using JKRuntime.UI;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions;
using Microsoft.Xna.Framework.Input;

[assembly: AssemblyTitle("God")]
[assembly: AssemblyVersion("0.3.5.0")]
[assembly: AssemblyFileVersion("0.3.5.0")]

namespace God
{
    [RuntimeModule("god", "God")]
    public static class Entry
    {
        internal static World World;
        internal static Editor Editor;
        internal static string Status = "";
        internal static bool Active, Open, Draining;
        private static bool initialized, previousMouse, wasHeld, marked;
        private static IDisposable cursor, prompt;
        private static JumpKing.Player.PlayerEntity player;
        private static readonly Marker marker = new Marker();
        private static readonly Type Pause = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        private static bool Captured { get { return Open || Draining; } }

        [BeforeLevelLoad] public static void Initialize()
        {
            if (initialized) return;
            Storage.Initialize(Path.Combine(Path.GetDirectoryName(typeof(Game1).Assembly.Location), "Content", "God"));
            UIApi.RegisterBinding(new UiBindingDefinition("god.editor", "God", "Toggle God mode", Storage.Chords, Storage.Bind,
                delegate { Storage.Bind(new[] { new UiChord((int)Keys.F8) }); }));
            initialized = true;
        }
        [MainMenuItemSetting] public static TextButton Binds(object factory, GuiFormat format)
        { Initialize(); return new TextButton("Binds", UIApi.CreateMenuPage(factory, new UiBindingsPage("God binds", "Toggle the platform editor. Default: F8.", "god.editor"))); }
        [PauseMenuItemSetting] public static TextButton PauseBinds(object factory, GuiFormat format) { return Binds(factory, format); }
        [MainMenuItemSetting] public static ForegroundOption MainForeground(object factory, GuiFormat format) { Initialize(); return new ForegroundOption(); }
        [PauseMenuItemSetting] public static ForegroundOption PauseForeground(object factory, GuiFormat format) { return MainForeground(factory, format); }
        internal static void SetTransferForeground(bool value)
        {
            Storage.SetTransferForeground(value);
            if (World != null) World.RefreshArtwork();
        }
        [OnWorldReady] public static void Prepare(RuntimeScope scope)
        {
            Initialize();
            scope.Defer(delegate { End(); Editor = null; World = null; });
            using (RuntimeApi.MeasureStartup("god.world")) { World = scope.Own(new World()); World.RestoreSaved(); }
            Editor = scope.Own(new Editor());
            var patches = scope.Own(new OwnedPatches("god.editor"));
            KingDrag.Prepare(patches);
            patches.Add(typeof(ControllerManager).GetMethod("Update"), postfix: Hook("Update"), priority: 0);
            patches.Add(typeof(Game1).GetMethod("DrawRenderTarget", OwnedPatches.Members), postfix: Hook("Draw"), priority: 0);
            patches.Add(Pause.GetMethod("PauseUpdate", OwnedPatches.Members), prefix: Hook("PauseUpdate"), priority: 800);
            patches.Add(Pause.GetMethod("Draw", OwnedPatches.Members), prefix: Hook("PauseDraw"), priority: 800);
        }
        [OnLevelStart] public static void Start(ModuleContext context)
        {
            Active = true; player = GameLoop.m_player; wasHeld = true;
            context.Track(new Cleanup(End));
            context.Track(GameState.Snapshots.Register(new TerrainState()));
            context.Track(JumpEvents.Subscribe(result => { if (Open && result.Provider == "jk-runtime.debug-jump") Mark(); }));
            context.Track(RuntimeApi.Mechanics.Register("god", "god.terrain", new Version(0, 1), MechanicEffects.Collision,
                () => new MechanicState(true, World != null, World != null && World.Changed, MechanicSource.Controller, "God platform edits")));
            if (World.Changed) Mark();
        }
        [OnLevelEnd] public static void End()
        {
            KingDrag.Release();
            ReleasePrompt();
            if (Editor != null) Editor.Cancel();
            if (Captured) { Open = Draining = false; Game1.instance.IsMouseVisible = previousMouse; }
            Release();
            if (marked && player != null) RunModifiers.Remove(player.m_body, marker);
            marked = false; player = null; Active = false;
        }
        private static MethodInfo Hook(string name) { return typeof(Entry).GetMethod(name, OwnedPatches.Members); }
        private static bool PauseUpdate() { return !Captured; }
        private static bool PauseDraw() { return !Open; }
        private static void Draw()
        {
            if (!Active || !Open) return;
            try { Editor.Draw(); }
            catch (Exception error) { Fail(error); }
        }
        private static bool BindingHeld(KeyboardState keyboard)
        {
            foreach (var chord in Storage.Chords())
                if (!chord.IsEmpty && chord.Buttons.All(b => b < 256 && keyboard.IsKeyDown((Keys)b))) return true;
            var manager = ControllerManager.instance;
            if (manager == null) return false;
            foreach (var pad in manager.GetConnectedPads())
            {
                if (!pad.IsValid || !pad.IsConnected || pad.GetPad().GetSaveIdentifier() == "pc_keyboard_jump_king") continue;
                var buttons = pad.GetPad().GetPressedButtons();
                if (Storage.Chords().Any(c => !c.IsEmpty && c.Buttons.All(buttons.Contains))) return true;
            }
            return false;
        }
        private static bool AllReleased(KeyboardState keyboard, MouseState mouse)
        {
            return !BindingHeld(keyboard) && keyboard.IsKeyUp(Keys.Escape) && mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released && mouse.MiddleButton == ButtonState.Released;
        }
        private static void Update()
        {
            if (!Active) return;
            try
            {
                var keyboard = Keyboard.GetState(); var mouse = Mouse.GetState(); bool held = BindingHeld(keyboard);
                if (!Game1.instance.IsActive) { Editor.Cancel(); Editor.Seed(keyboard, mouse); KingDrag.SeedWheel(mouse); wasHeld = true; return; }
                bool toggle = held && !wasHeld; wasHeld = held;
                if (Open && (UIApi.IsOpen || NativePause.IsPaused)) Close();
                else if (toggle && Open) Close();
                else if (toggle && !Draining && !UIApi.IsOpen && !NativePause.IsPaused) Begin(keyboard, mouse);
                else if (Open) Editor.Update(keyboard, mouse);
                else Editor.Seed(keyboard, mouse);
                if (Draining && AllReleased(keyboard, mouse))
                {
                    Draining = false; Release(); Game1.instance.IsMouseVisible = previousMouse;
                }
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void Begin(KeyboardState keyboard, MouseState mouse)
        {
            Editor.Begin(Camera.CurrentScreen); previousMouse = Game1.instance.IsMouseVisible; Open = true;
            try
            {
                cursor = UiPointer.AcquireWindowCursor(); Editor.Seed(keyboard, mouse); KingDrag.SeedWheel(mouse);
                if (GameLoop.m_player != null) prompt = DebugJumpPrompt.Acquire(GameLoop.m_player, () => Open && Editor.PromptPointerAvailable);
            }
            catch { Close(); throw; }
        }
        internal static void Close() { if (!Open) return; Editor.Cancel(); ReleasePrompt(); Open = false; Draining = true; }
        private static void ReleasePrompt() { if (prompt != null) { prompt.Dispose(); prompt = null; } }
        private static void Release()
        {
            if (cursor != null) { cursor.Dispose(); cursor = null; }
        }
        private static void Fail(Exception error)
        { Status = error.GetBaseException().Message; Console.WriteLine("[God] " + error); if (Open) Close(); }
        internal static void Changed(ScreenEdit screen)
        {
            Touch(); Save(screen);
        }
        internal static void Touch() { World.Generation++; Mark(); }
        internal static void Save(ScreenEdit screen)
        {
            try { World.Save(screen); Status = ""; }
            catch (Exception error) { Status = "Edits are live, but saving failed: " + error.Message; Console.WriteLine("[God] " + Status); }
        }
        internal static void Mark() { if (!marked && player != null) { RunModifiers.Register(player.m_body, marker); marked = true; } }
        private sealed class Marker : IBodyCompBehaviour { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private sealed class Cleanup : IDisposable
        {
            private Action action; internal Cleanup(Action value) { action = value; }
            public void Dispose() { var value = action; action = null; if (value != null) value(); }
        }
        private sealed class TerrainState : IStateParticipant
        {
            public string Id { get { return "god.terrain"; } }
            public int Version { get { return 1; } }
            public object Capture() { return World.Generation; }
            public void Validate(object value)
            { if (!(value is long) || (long)value != World.Generation) throw new InvalidOperationException("God terrain changed since this snapshot."); }
            public void Restore(object value) { Validate(value); }
        }
    }
    public sealed class ForegroundOption : ITextToggle
    {
        public ForegroundOption() : base(Storage.Preferences.TransferForeground) { }
        protected override string GetName() { return "Transfer foreground"; }
        protected override void OnToggle() { Entry.SetTransferForeground(!Storage.Preferences.TransferForeground); }
    }
}
