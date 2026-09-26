using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml.Serialization;
using EntityComponent;
using EntityComponent.BT;
using BehaviorTree;
using JumpKing.GameManager;
using JumpKing.GameManager.TitleScreen;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks, draws, polls;
    private static readonly List<GameTime> updates = new List<GameTime>();
    private static bool enableDuringUpdate, disableDuringUpdate;
    private static bool menuPlaying;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static bool RecordUpdate(GameTime gameTime)
    {
        updates.Add(new GameTime(gameTime.TotalGameTime, gameTime.ElapsedGameTime));
        if (disableDuringUpdate) { disableDuringUpdate = false; SettingsStore.Current.SetInputs(false); }
        if (enableDuringUpdate) { enableDuringUpdate = false; SettingsStore.Current.SetRefresh(true); }
        return false;
    }
    private static bool RecordDraw() { draws++; return false; }
    private static bool RecordPoll() { polls++; return false; }
    private static bool Skip() { return false; }
    private sealed class PlatformGame : Game { }
    private static object animation;
    private static MethodInfo advanceAnimation;
    private static bool NativeAnimationUpdate(GameTime gameTime)
    {
        updates.Add(new GameTime(gameTime.TotalGameTime, gameTime.ElapsedGameTime));
        advanceAnimation.Invoke(animation, new object[] { 1f / 60f });
        return false;
    }
    private static bool AllowPlatform(ref bool __result) { __result = true; return false; }
    private static void NativeStartupClockTest()
    {
        // Exercise the real DoUpdate body. Patching DoUpdate with a fixture BEFORE
        // production installation prevents the JIT inlining that this test targets.
        var fixture = new Harmony("sfc.cold-start.tests");
        var game = MakeGame();
        using (var platformGame = new PlatformGame())
        {
            var platform = AccessTools.Field(typeof(Game), "Platform");
            platform.SetValue(game, platform.GetValue(platformGame));
            var type = typeof(Game1).Assembly.GetType("JumpKing.Props.LoopingProp", true);
            animation = Activator.CreateInstance(type, Flags, null,
                new object[] { new Sprite[1000], .1f, Point.Zero, 1, false, false, null }, null);
            advanceAnimation = AccessTools.Method(type, "Update");
            fixture.Patch(AccessTools.Method(typeof(Game1), "Update"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "NativeAnimationUpdate")));
            fixture.Patch(AccessTools.Method(typeof(Game1), "Draw"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "RecordDraw")));
            fixture.Patch(AccessTools.Method(typeof(Game), "EndDraw"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "Skip")));
            fixture.Patch(AccessTools.Method(platform.GetValue(game).GetType(), "BeforeUpdate"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "AllowPlatform")));
            fixture.Patch(AccessTools.Method(typeof(FrameworkDispatcher), "Update"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "Skip")));
            fixture.Patch(AccessTools.Method(typeof(Game), "Tick"), transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(ResponsiveInput), "Poll"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "RecordPoll")));
            fixture.Patch(AccessTools.Method(typeof(MenuCadence), "Pump"), prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "Skip")));
            try
            {
                PerformanceFeatures.Install(); SettingsStore.Current.SetRefresh(true);
                updates.Clear(); draws = 0;
                var accumulator = AccessTools.Field(typeof(Game), "_accumulatedElapsedTime");
                for (int i=0; i<2400; i++) { accumulator.SetValue(game, FeatureClock.RenderInterval); game.Tick(); }
                Check(updates.Count == 2400L*41667/170000, "Production install preserves actual Game1.Update frequency without an instrumented DoUpdate");
                int index = (int)AccessTools.Property(type, "Index").GetValue(animation, null);
                object baseline = Activator.CreateInstance(type, Flags, null,
                    new object[] { new Sprite[1000], .1f, Point.Zero, 1, false, false, null }, null);
                for (int i=0; i<updates.Count; i++) advanceAnimation.Invoke(baseline, new object[] { 1f / 60f });
                Check(index == (int)AccessTools.Property(type, "Index").GetValue(baseline, null), "Native background prop animation retains original elapsed time");
            }
            finally { PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null); }
        }
    }
    private static bool ActiveProperty(ref bool __result) { __result = true; return false; }
    private static bool IsPlaying(ref bool __result) { __result = menuPlaying; return false; }
    private static bool GamePlaying(JumpGame game) { return menuPlaying; }
    private static IEnumerable<CodeInstruction> HeadlessMenu(IEnumerable<CodeInstruction> source)
    {
        foreach(var instruction in source)
        {
            if(instruction.Calls(AccessTools.Method(typeof(JumpGame),"IsPlaying")))
            { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(PerformanceTests),"GamePlaying"); }
            yield return instruction;
        }
    }
    private static bool Active(Game game) { return true; }
    private static void NoDeviceEvents(object instance) { }
    private static IEnumerable<CodeInstruction> HeadlessController(IEnumerable<CodeInstruction> source)
    {
        foreach(var instruction in source)
        {
            var method = instruction.operand as MethodInfo;
            if(method != null && (method.Name == "PollSlimEvents" || method.Name == "Update" && method.DeclaringType.Name == "SlimJoystickManager"))
            { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(PerformanceTests),"NoDeviceEvents"); }
            yield return instruction;
        }
    }
    private static IEnumerable<CodeInstruction> HeadlessTick(IEnumerable<CodeInstruction> source)
    {
        foreach (var instruction in source)
        {
            if (instruction.Calls(AccessTools.PropertyGetter(typeof(Game), "IsActive")))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(PerformanceTests), "Active"); }
            yield return instruction;
        }
    }
    private static void SettingsTests()
    {
        var legacy = (SubframeChargeSettings)new XmlSerializer(typeof(SubframeChargeSettings)).Deserialize(new StringReader("<SubframeChargeSettings><Enabled>false</Enabled><ShowMeasurement>false</ShowMeasurement></SubframeChargeSettings>"));
        Check(!legacy.Enabled && !legacy.ShowMeasurement && !legacy.Optimizations && !legacy.HighRefresh && !legacy.SubframeInputs, "Migration preserves old settings and does not silently enable new behavior");
        legacy.SetRefresh(true); Check(legacy.HighRefresh && legacy.SubframeInputs, "240 Hz enables required inputs");
        legacy.SetInputs(false); Check(!legacy.HighRefresh && !legacy.SubframeInputs, "Disabling inputs disables 240 Hz");
        legacy.HighRefresh = true; legacy.Normalize(); Check(legacy.SubframeInputs, "Load repairs inconsistent dependency");
        legacy.Optimizations = true; legacy.SetInputs(false); Check(legacy.Optimizations, "Optimizations independent");
        SettingsStore.EnsureLoaded();
        bool oldEnabled = SettingsStore.Current.Enabled;
        bool oldQuarter = SettingsStore.Current.QuarterStepCharge;
        try
        {
            SettingsStore.Current.QuarterStepCharge = true;
            var toggle = ModEntry.MainQuarterSteps(null, default(JumpKing.PauseMenu.GuiFormat));
            var canChange = typeof(JKRuntime.UI.SettingToggle).GetMethod("CanChange", BindingFlags.NonPublic | BindingFlags.Instance);
            SettingsStore.Current.Enabled = false;
            Check(!(bool)canChange.Invoke(toggle, null), "Native quarter setting is disabled with SFC off");
            SettingsStore.Current.Enabled = true;
            Check((bool)canChange.Invoke(toggle, null) && Options.QuarterSteps.Value, "Re-enabling SFC restores the saved quarter option without menu reconstruction");
        }
        finally { SettingsStore.Current.Enabled = oldEnabled; SettingsStore.Current.QuarterStepCharge = oldQuarter; }
    }
    private static void InputTests()
    {
        for (int bit = 1; bit <= 1024; bit <<= 1)
        {
            Check(ResponsiveInput.Bits(ResponsiveInput.State(bit)) == bit, "All eleven native actions round trip");
            var latch = new InputLatch(); latch.Observe(bit); latch.Observe(0); latch.Commit();
            Check(latch.NativeEdges == bit && latch.Held == 0, "A released subframe press survives until the native tick");
            Check(latch.NativeEdges == bit, "Reading does not consume another component's input");
            latch.Commit(); Check(latch.NativeEdges == 0, "Press is delivered exactly once");
            latch.Observe(bit); latch.Suppress(); latch.Observe(bit); latch.Commit();
            Check(latch.Held == 0 && latch.NativeEdges == 0, "Focus/menu held input requires release");
            latch.Observe(0); latch.Observe(bit); latch.Commit(); Check(latch.NativeEdges == bit, "Release rearms action");
        }
        var simultaneous = new InputLatch(); simultaneous.Observe(4|8|256); simultaneous.Observe(512); simultaneous.Commit();
        Check(simultaneous.NativeEdges == (4|8|256|512) && simultaneous.Held == 512, "Directions and equipment edges remain distinct from held state");
        bool key=false, second=false, focus=true; long now=Stopwatch.Frequency;
        var actions=new int[11][][];
        for(int i=0;i<11;i++) actions[i]=new int[0][];
        actions[2]=new[]{new[]{65}}; actions[8]=new[]{new[]{65,66}};
        using(var keyboard=new KeyboardActionEdges(actions,new IntPtr(1),k=>(short)((k==65?key:second)?-32768:0),()=>new IntPtr(focus?1:2),false))
        {
            keyboard.Sample(now); key=true; keyboard.Sample(now+=Stopwatch.Frequency/1000); key=false; keyboard.Sample(now+=Stopwatch.Frequency/1000);
            Check(keyboard.Take(now)==4 && keyboard.Take(now)==0,"Independent keyboard worker captures a completed 1 ms tap exactly once between render calls");
            key=true; keyboard.Sample(now+=Stopwatch.Frequency/1000); second=true; keyboard.Sample(now+=Stopwatch.Frequency/1000);
            Check(keyboard.Take(now)==(4|256),"Worker preserves Controls+ chords and separate native actions");
            keyboard.Publish(4|256,true,++now); Check(keyboard.Take(now)==0,"Native boundary observation cannot duplicate a worker press");
            keyboard.Publish(0,true,++now); keyboard.Publish(4,true,++now);
            Check(keyboard.Take(now)==4,"A press immediately before native physics does not wait for the worker's next sample");
            focus=false; keyboard.Sample(now+=Stopwatch.Frequency/1000); focus=true; keyboard.Sample(now+=Stopwatch.Frequency/1000);
            Check(keyboard.Take(now)==0,"Focus reacquisition while held cannot synthesize presses");
            key=second=false; keyboard.Sample(now+=Stopwatch.Frequency/1000); key=true; keyboard.Sample(now+=Stopwatch.Frequency/1000);
            Check(keyboard.Take(now+Stopwatch.Frequency)==0,"A long render stall cannot replay stale keyboard edges");
        }
        var keyboardLatch=new InputLatch{KeyboardStream=true}; keyboardLatch.Observe(256,false); keyboardLatch.Pending|=256; keyboardLatch.Commit();
        keyboardLatch.Observe(256,false); keyboardLatch.Commit(); Check(keyboardLatch.NativeEdges==0,"Native held snapshot does not duplicate worker-owned key edges");
        bool physical = false;
        var menuActions = new int[11][][];
        for(int i=0;i<11;i++) menuActions[i]=new int[0][];
        menuActions[6]=new[]{new[]{65}};
        var menuPad=new PadInstance(new Pad());
        using(var worker=new KeyboardActionEdges(menuActions,new IntPtr(1),k=>(short)(physical?-32768:0),()=>new IntPtr(1),false))
        {
            AccessTools.Field(typeof(SubframeKeyboard),"worker").SetValue(null,worker);
            AccessTools.Field(typeof(SubframeKeyboard),"owner").SetValue(null,menuPad);
            try
            {
                worker.Sample(Stopwatch.GetTimestamp()); physical=true; worker.Sample(Stopwatch.GetTimestamp());
                Check(SubframeKeyboard.Take(menuPad,0)==64,"A physical menu press survives a stale released MonoGame snapshot");
                for(int i=0;i<20;i++)
                {
                    worker.Sample(Stopwatch.GetTimestamp());
                    Check(SubframeKeyboard.Take(menuPad,0)==0,"Stale native snapshots cannot rearm a held menu key");
                }
                physical=false; worker.Sample(Stopwatch.GetTimestamp());
                Check(SubframeKeyboard.Take(menuPad,64)==0,"Stale held snapshot cannot fabricate a new press after physical release");
                physical=true;
                Check(SubframeKeyboard.Take(menuPad,0)==64,"A real second press is delivered without a debounce delay");
            }
            finally { SubframeKeyboard.Stop(); }
        }
    }
    private static Game1 MakeGame()
    {
        var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1)); GC.SuppressFinalize(game);
        AccessTools.Field(typeof(Game1), "_instance").SetValue(null, game);
        AccessTools.Field(typeof(Game), "_targetElapsedTime").SetValue(game, TimeSpan.FromTicks(170000));
        AccessTools.Field(typeof(Game), "_gameTime").SetValue(game, new GameTime());
        AccessTools.Field(typeof(Game), "_gameTimer").SetValue(game, new Stopwatch());
        AccessTools.Field(typeof(Game), "_maxElapsedTime").SetValue(game, TimeSpan.FromSeconds(.5));
        game.IsFixedTimeStep = true; return game;
    }
    private sealed class Pad : IPad
    {
        internal int[] Buttons = new int[0];
        internal string Identifier = "fixture";
        internal PadBinding Binding;
        public int[] GetPressedButtons() { return Buttons; }
        public string ButtonToString(int value) { return value.ToString(); }
        public string GetPrintName() { return "Fixture"; }
        public string GetSaveIdentifier() { return Identifier; }
        public bool IsConnected() { return true; }
        public PadBinding GetDefaultBind() { return Binding ?? new PadBinding { left=new[]{1}, jump=new[]{2}, boots=new[]{3}, snake=new[]{4}, confirm=new[]{5} }; }
    }
    private static void NativeInputTests()
    {
        var fixture=new Harmony("sfc.native-input.tests"); var game=MakeGame();
        fixture.Patch(AccessTools.PropertyGetter(typeof(Game),"IsActive"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"ActiveProperty")));
        fixture.Patch(AccessTools.Method(typeof(ResponsiveInput),"Poll"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
        fixture.Patch(AccessTools.Method(typeof(PadInstance),"GetPadState"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
        PerformanceFeatures.Install(); SettingsStore.Current.SetInputs(true); FeatureClock.Configure(game,true);
        var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
        var device=new Pad(); var pad=new PadInstance(device);
        AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>{pad});
            AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(manager,new MenuController(manager));
            fixture.Patch(AccessTools.Method(typeof(ControllerManager),"Update"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessController")));
        try
        {
            ResponsiveInput.Poll(); device.Buttons=new[]{1,3,4}; ResponsiveInput.Poll(); device.Buttons=new int[0]; ResponsiveInput.Poll();
            manager.Update(); Check(pad.GetPressed().left && pad.GetPressed().boots && pad.GetPressed().snake,"Actual native pad dispatch retains completed movement and equipment taps");
            Check(pad.GetState().left && pad.GetState().boots,"Completed actions publish a coherent native held/pressed snapshot");
            manager.Update(); Check(ResponsiveInput.Bits(pad.GetPressed())==0 && ResponsiveInput.Bits(pad.GetState())==0,"Actual native state returns to released without duplicate toggles");
            device.Buttons=new[]{2}; ResponsiveInput.Poll(); device.Buttons=new int[0]; ResponsiveInput.Poll(); manager.Update();
            Check(pad.GetState().jump,"Enabled without an attached charge policy retains a completed native Jump tap");
            fixture.Patch(AccessTools.Method(typeof(SubframeChargeInstaller),"OwnsCompletedTap"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"TapOwner")));
            manager.Update(); device.Buttons=new[]{2}; ResponsiveInput.Poll(); device.Buttons=new int[0]; ResponsiveInput.Poll(); manager.Update();
            Check(!pad.GetState().jump,"An active SFC replay owner does not also receive a fabricated held Jump");
            SettingsStore.Current.Enabled=false;
            device.Buttons=new[]{2}; ResponsiveInput.Poll(); device.Buttons=new int[0]; ResponsiveInput.Poll(); manager.Update();
            Check(pad.GetState().jump,"Independent Subframe Inputs retains jump taps when charge correction is disabled");
            manager.Update(); Check(!pad.GetState().jump,"Independent tap releases next native tick");
            device.Buttons=new[]{1}; ResponsiveInput.Poll(); ResponsiveInput.SuppressGameplay(); ResponsiveInput.Poll(); manager.Update();
            Check(!pad.GetState().left,"Actual held action cannot leak through resume gate");
        }
        finally { PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null; AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null); }
    }
    private static bool KeepKeyboardWorker(ref bool __result) { __result=false; return false; }
    private static bool TapOwner(ref bool __result) { __result=SettingsStore.Current.Enabled; return false; }
    private static void NativeKeyboardJumpTests()
    {
        // Physical input can lead MonoGame's message-based snapshot by a native
        // update. Exercise the real native jump eligibility, not just pad bits.
        foreach(bool refresh in new[]{false,true})
        {
            var fixture=new Harmony("sfc.keyboard-jump.tests"); var game=MakeGame();
            fixture.Patch(AccessTools.Method(typeof(ResponsiveInput),"Poll"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(PadInstance),"GetPadState"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(SubframeKeyboard),"Refresh"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"KeepKeyboardWorker")));
            fixture.Patch(AccessTools.Method(typeof(SubframeChargeInstaller),"OwnsCompletedTap"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"TapOwner")));
            PerformanceFeatures.Install(); SettingsStore.Current.Enabled=true;
            SettingsStore.Current.SetInputs(true); SettingsStore.Current.HighRefresh=refresh; FeatureClock.BeforeTick(game);
            var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
            var device=new Pad{Identifier="pc_keyboard_jump_king"}; var pad=new PadInstance(device);
            AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>{pad});
            AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(manager,new MenuController(manager));
            fixture.Patch(AccessTools.Method(typeof(ControllerManager),"Update"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessController")));
            var input=new InputComponent(); var update=AccessTools.Method(typeof(InputComponent),"Update");
            Action tick=()=>{manager.Update();update.Invoke(input,new object[]{1f/60});};
            bool physical=false;
            var actions=new int[11][][];
            for(int i=0;i<actions.Length;i++) actions[i]=new int[0][];
            actions[4]=actions[6]=new[]{new[]{65}};
            using(var worker=new KeyboardActionEdges(actions,new IntPtr(1),k=>(short)(physical?-32768:0),()=>new IntPtr(1),false))
            try
            {
                AccessTools.Field(typeof(SubframeKeyboard),"worker").SetValue(null,worker);
                AccessTools.Field(typeof(SubframeKeyboard),"owner").SetValue(null,pad);
                ResponsiveInput.Poll(); tick();
                physical=true; ResponsiveInput.Poll(); tick();
                Check(!input.TryConsumeJump(),"Native eligibility rejects an early edge without native held state");
                device.Buttons=new[]{2,5}; ResponsiveInput.Poll(); tick();
                Check(input.GetState().jump && input.TryConsumeJump(),"Delayed native held Jump must retain its native rising edge with 240 Hz="+refresh);
                Check(!pad.GetPressed().confirm,"Delayed native Confirm cannot repeat the worker's menu press");
                using (JKRuntime.Input.NativeInputFrames.BeginMenu(manager.MenuController, ResponsiveInput.MenuEdges())) {
                Check(!manager.MenuController.GetPadState().jump && !manager.MenuController.GetPadState().confirm,"Native gameplay fallback cannot replay an edge into the fast menu");
                }
                tick(); Check(!input.TryConsumeJump(),"Holding Jump cannot rearm a consumed native jump");
                physical=false; device.Buttons=new int[0]; ResponsiveInput.Poll(); tick();
                physical=true; device.Buttons=new[]{2}; ResponsiveInput.Poll(); tick();
                Check(input.TryConsumeJump(),"An aligned physical/native second press starts exactly one jump");
                tick(); Check(!input.TryConsumeJump(),"Second held press stays consumed");
                ResponsiveInput.SuppressGameplay(); ResponsiveInput.Poll(); tick();
                Check(!input.GetState().jump && !input.TryConsumeJump(),"Resume suppression still blocks a held jump");
            }
            finally
            {
                PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null;
                AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            }
        }
    }
    private static void InputTransitionTests()
    {
        var fixture=new Harmony("sfc.input-transition.tests"); var game=MakeGame();
        fixture.Patch(AccessTools.Method(typeof(ResponsiveInput),"Poll"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
        fixture.Patch(AccessTools.Method(typeof(PadInstance),"GetPadState"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
        fixture.Patch(AccessTools.Method(typeof(ControllerManager),"Update"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessController")));
        fixture.Patch(AccessTools.Method(typeof(SubframeKeyboard),"Refresh"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"KeepKeyboardWorker")));
        var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
        var device=new Pad{Identifier="pc_keyboard_jump_king",Binding=new PadBinding{
            up=new[]{1},down=new[]{2},left=new[]{3},right=new[]{4},jump=new[]{5},pause=new[]{6},confirm=new[]{7},cancel=new[]{8},boots=new[]{9},snake=new[]{10},restart=new[]{11}}};
        var pad=new PadInstance(device);
        AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>{pad});
        AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(manager,new MenuController(manager));
        int physical=0;
        var actions=Enumerable.Range(0,11).Select(i=>new[]{new[]{65+i}}).ToArray();
        try
        {
            // Warm the native caller/getters before production installation.
            // No fixture patches PadInstance.Update/GetPressed/GetState.
            for(int i=0;i<20;i++) manager.Update();
            PerformanceFeatures.Install(); SettingsStore.Current.Enabled=false;
            for(int cycle=0;cycle<12;cycle++)
            {
                SettingsStore.Current.SetInputs(true); SettingsStore.Current.HighRefresh=true; FeatureClock.BeforeTick(game);
                var worker=new KeyboardActionEdges(actions,new IntPtr(1),k=>(short)((physical&(1<<(k-65)))!=0?-32768:0),()=>new IntPtr(1),false);
                AccessTools.Field(typeof(SubframeKeyboard),"worker").SetValue(null,worker);
                AccessTools.Field(typeof(SubframeKeyboard),"owner").SetValue(null,pad);
                ResponsiveInput.Poll(); manager.Update();
                foreach(bool refresh in new[]{true,false,true,false})
                {
                    SettingsStore.Current.HighRefresh=refresh; FeatureClock.BeforeTick(game);
                    Check(MenuCadence.Fast,"240 Hz toggling never switches the menu input owner");
                    for(int action=0;action<11;action++)
                    {
                        int bit=1<<action;
                        physical=bit; ResponsiveInput.Poll();
                        Check(ResponsiveInput.Bits(ResponsiveInput.MenuEdges())==bit,"Every keyboard action reaches the menu stream after a refresh toggle");
                        physical=0; ResponsiveInput.Poll(); manager.Update();
                        Check(ResponsiveInput.Bits(pad.GetPressed())==bit && ResponsiveInput.Bits(pad.GetState())==bit,"Completed keyboard tap reaches the real warmed native controller exactly once");
                        manager.Update(); Check(ResponsiveInput.Bits(pad.GetPressed())==0 && ResponsiveInput.Bits(pad.GetState())==0,"Published tap releases on the next native frame");
                    }
                }
                SettingsStore.Current.SetInputs(false); FeatureClock.BeforeTick(game);
                device.Buttons=new[]{1,3,5,7,9}; manager.Update();
                Check(ResponsiveInput.Bits(pad.GetPressed())==(1|4|16|64|256),"Disabling Subframe Inputs immediately restores ordinary native input");
                device.Buttons=new int[0]; manager.Update();
                SettingsStore.Current.SetInputs(true); FeatureClock.BeforeTick(game);
                worker=new KeyboardActionEdges(actions,new IntPtr(1),k=>(short)((physical&(1<<(k-65)))!=0?-32768:0),()=>new IntPtr(1),false);
                AccessTools.Field(typeof(SubframeKeyboard),"worker").SetValue(null,worker);
                AccessTools.Field(typeof(SubframeKeyboard),"owner").SetValue(null,pad);
                ResponsiveInput.Poll(); manager.Update();
                physical=64; ResponsiveInput.Poll(); ResponsiveInput.MenuConsumed();
                SettingsStore.Current.HighRefresh=true; FeatureClock.BeforeTick(game); ResponsiveInput.Poll();
                Check(!ResponsiveInput.MenuEdges().confirm,"Changing refresh while Confirm is held cannot reactivate a menu item");
                SettingsStore.Current.SetInputs(false); FeatureClock.BeforeTick(game);
                manager.Update(); device.Buttons=new[]{7}; manager.Update();
                Check(!pad.GetPressed().confirm,"Early physical confirmation cannot bounce into native menus when Inputs is disabled");
                physical=0; manager.Update();
                Check(!pad.GetPressed().confirm,"Handoff waits for the delayed native release too");
                device.Buttons=new int[0]; manager.Update();
                device.Buttons=new[]{7}; manager.Update();
                Check(pad.GetPressed().confirm,"Fresh native confirmation works after complete release");
                device.Buttons=new int[0]; manager.Update();
            }
        }
        finally
        {
            PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null;
            AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
        }
    }
    private static void CameraCooperationTests(string path)
    {
        var camera=Assembly.LoadFrom(path);
        var hooks=camera.GetType("SmoothCamera.Hooks",true); var renderer=camera.GetType("SmoothCamera.Renderer",true);
        var settings=camera.GetType("SmoothCamera.Settings",true); var entry=camera.GetType("SmoothCamera.ModEntry",true);
        object data=settings.GetField("Current",Flags).GetValue(null);
        data.GetType().GetField("Smooth").SetValue(data,true); data.GetType().GetField("HighRefresh").SetValue(data,true);
        for(int order=0;order<2;order++)
        {
            var fixture=new Harmony("sfc.camera-clock.tests"); var game=MakeGame(); var accumulator=AccessTools.Field(typeof(Game),"_accumulatedElapsedTime");
            fixture.Patch(AccessTools.Method(typeof(Game),"Tick"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(Game),"DoUpdate"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"RecordUpdate")){priority=Priority.Last});
            fixture.Patch(AccessTools.Method(typeof(Game),"DoDraw"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"RecordDraw")){priority=Priority.Last});
            fixture.Patch(AccessTools.Method(typeof(ResponsiveInput),"Poll"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"RecordPoll")));
            fixture.Patch(AccessTools.Method(typeof(MenuCadence),"Pump"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"Skip")));
            try
            {
                if(order==0) PerformanceFeatures.Install();
                hooks.GetMethod("Install",Flags).Invoke(null,null); renderer.GetMethod("Start",Flags).Invoke(null,null);
                if(order==1) PerformanceFeatures.Install();
                entry.GetMethod("ConnectExternalClock").Invoke(null,null); PerformanceFeatures.ConnectCamera();
                SettingsStore.Current.SetRefresh(true); updates.Clear(); draws=0;
                for(int i=0;i<240;i++){accumulator.SetValue(game,FeatureClock.RenderInterval);game.Tick();}
                Check(updates.Count==240L*41667/170000 && draws==240,"Both patch orders preserve exactly one scheduler owner");
                SettingsStore.Current.SetInputs(false);
                for(int i=0;i<240;i++){accumulator.SetValue(game,FeatureClock.RenderInterval);game.Tick();}
                Check(!FeatureClock.Active && updates.Count==480L*41667/170000,"Smooth Camera takes over with no lost or duplicated physics time");
                SettingsStore.Current.SetRefresh(true);
                for(int i=0;i<240;i++){accumulator.SetValue(game,FeatureClock.RenderInterval);game.Tick();}
                Check(FeatureClock.Active && updates.Count==720L*41667/170000,"SFC takes ownership back without clock drift");
            }
            finally { PerformanceFeatures.Uninstall(); hooks.GetMethod("Uninstall",Flags).Invoke(null,null); renderer.GetMethod("Stop",Flags).Invoke(null,null); fixture.UnpatchAll(fixture.Id); AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null); }
        }
    }
    private sealed class MenuNode : IBTnode
    {
        internal int Calls; internal float Time;
        protected override BTresult MyRun(TickData data) { Calls++; Time+=data.delta_time; return BTresult.Running; }
    }
    private static void MenuTests()
    {
        var fixture=new Harmony("sfc.menu.tests"); var game=MakeGame();
        fixture.Patch(AccessTools.Method(typeof(JumpGame),"IsPlaying"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"IsPlaying")));
        fixture.Patch(AccessTools.Method(typeof(MenuCadence),"Pump"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessMenu")));
        fixture.Patch(AccessTools.Method(typeof(JKRuntime.UI.UiPointer),"Update"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"Skip")){priority=Priority.First});
        var jump=(JumpGame)FormatterServices.GetUninitializedObject(typeof(JumpGame));
        AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,jump);
        ControllerManager.instance=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
        AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(ControllerManager.instance,new List<PadInstance>());
        AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(ControllerManager.instance,new MenuController(ControllerManager.instance));
        SettingsStore.Current.SetRefresh(true); PerformanceFeatures.Install(); FeatureClock.BeforeTick(game);
        var pause=(Entity)FormatterServices.GetUninitializedObject(MenuCadence.Pause);
        AccessTools.Field(typeof(Entity),"m_components").SetValue(pause,new List<Component>());
        var node=new MenuNode(); pause.AddComponents(new BehaviorTreeComp(node));
        AccessTools.Field(MenuCadence.Pause,"instance").SetValue(null,pause); AccessTools.Field(MenuCadence.Pause,"_paused").SetValue(pause,true);
        GameLoop.m_player=EmptyPlayer(); menuPlaying=true;
        try
        {
            for(int i=0;i<240;i++) MenuCadence.Pump(1f/240);
            Check(node.Calls==240 && Math.Abs(node.Time-1)<.00001f,"Actual native pause tree runs at presentation rate with unchanged time progression");
            AccessTools.Method(MenuCadence.Pause,"PauseUpdate").Invoke(pause,new object[]{1f/60});
            Check(node.Calls==240,"Native tick cannot advance the fast pause tree twice");
            for(int mode=0;mode<6;mode++)
            {
                SettingsStore.Current.HighRefresh=mode%2==0; FeatureClock.BeforeTick(game);
                int before=node.Calls;
                for(int i=0;i<20;i++) MenuCadence.Pump(1f/240);
                AccessTools.Method(MenuCadence.Pause,"PauseUpdate").Invoke(pause,new object[]{1f/60});
                Check(node.Calls==before+20,"Refresh toggles retain exactly one pause-tree update owner");
            }
            menuPlaying=false;
            var title=(GameTitleScreen)FormatterServices.GetUninitializedObject(typeof(GameTitleScreen));
            AccessTools.Field(typeof(IBTnode),"m_last_result").SetValue(title,BTresult.Running);
            MenuCadence.TitleStart(title); var titleNode=new MenuNode(); var tree=new BTmanager(titleNode);
            AccessTools.Field(typeof(GameTitleScreen),"m_menu").SetValue(title,titleNode);
            AccessTools.Field(typeof(IBTnode),"m_last_result").SetValue(titleNode,BTresult.Running);
            MenuCadence.RunTitle(tree,1f/60);
            for(int i=0;i<240;i++) { MenuCadence.Pump(1f/240); if(i%4==0) MenuCadence.RunTitle(tree,1f/60); }
            Check(titleNode.Calls==241 && Math.Abs(titleNode.Time-(1+1f/60))<.00001f,"Native starts the title tree; presentation advances only its active menu");
            SettingsStore.Current.SetInputs(false); FeatureClock.BeforeTick(game); MenuCadence.RunTitle(tree,1f/60);
            Check(titleNode.Calls==241,"Disabling consumes the last pending menu result before resuming native updates");
            MenuCadence.RunTitle(tree,1f/60); Check(titleNode.Calls==242,"The following native tick resumes the running menu");
        }
        finally
        {
            PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null; GameLoop.m_player=null;
            AccessTools.Field(MenuCadence.Pause,"instance").SetValue(null,null); AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,null); AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
        }
    }
    private static void ClockTests()
    {
        var fixture = new Harmony("sfc.performance.tests");
        var game = MakeGame(); var accumulator = AccessTools.Field(typeof(Game), "_accumulatedElapsedTime");
        var clock = (GameTime)AccessTools.Field(typeof(Game), "_gameTime").GetValue(game);
        fixture.Patch(AccessTools.Method(typeof(Game), "Tick"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "HeadlessTick")));
        fixture.Patch(AccessTools.Method(typeof(Game), "DoUpdate"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "RecordUpdate")) { priority=Priority.Last });
        fixture.Patch(AccessTools.Method(typeof(Game), "DoDraw"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "RecordDraw")) { priority=Priority.Last });
        fixture.Patch(AccessTools.Method(typeof(ResponsiveInput), "Poll"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "RecordPoll")));
        fixture.Patch(AccessTools.Method(typeof(MenuCadence), "Pump"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests), "Skip")));
        try
        {
            PerformanceFeatures.Install(); SettingsStore.Current.SetRefresh(true);
            updates.Clear(); draws = polls = 0;
            for (int i=0;i<2400;i++) { accumulator.SetValue(game, FeatureClock.RenderInterval); game.Tick(); }
            Check(draws == 2400 && polls == 2400, "240 Hz presentation and input polling execute every scheduler interval");
            Check(updates.Count == 2400L*41667/170000, "Physics and foreign native update hooks retain exact original count");
            for (int i=0;i<updates.Count;i++) Check(updates[i].ElapsedGameTime.Ticks == 170000 && updates[i].TotalGameTime.Ticks == (i+1L)*170000, "Original native clock/delta without drift");
            Check(game.TargetElapsedTime.Ticks == 170000, "Public physics contract unchanged");
            SettingsStore.Current.HighRefresh = false;
            int initialDraws = draws, initialUpdates = updates.Count;
            for (int i=0;i<240;i++) { accumulator.SetValue(game, FeatureClock.RenderInterval); game.Tick(); }
            Check(draws-initialDraws == updates.Count-initialUpdates && polls == 2640, "Inputs-only retains native presentation rate while polling between ticks");
            long pending = clock.TotalGameTime.Ticks - updates.Last().TotalGameTime.Ticks;
            SettingsStore.Current.SetInputs(false); accumulator.SetValue(game, TimeSpan.FromTicks(170000-pending)); game.Tick();
            Check(!FeatureClock.Active && updates.Count == initialUpdates+(int)((240L*41667+(2400L*41667%170000))/170000)+1, "Disable returns partial time to native scheduler");
            long previousTime = updates.Last().TotalGameTime.Ticks;
            enableDuringUpdate = true; accumulator.SetValue(game, TimeSpan.FromTicks(170000)); game.Tick();
            Check(!FeatureClock.Active, "Enable inside native Update waits for next Tick boundary");
            accumulator.SetValue(game, TimeSpan.FromTicks(41667*10)); disableDuringUpdate=true; game.Tick();
            Check(FeatureClock.Active && updates.Last().TotalGameTime.Ticks == previousTime+3*170000, "Disable during catch-up preserves native time and atomic ownership");
            pending = clock.TotalGameTime.Ticks-updates.Last().TotalGameTime.Ticks;
            accumulator.SetValue(game, TimeSpan.FromTicks(170000-pending)); game.Tick();
            Check(!FeatureClock.Active && updates.Last().TotalGameTime.Ticks == previousTime+4*170000, "Catch-up remainder returned once");
        }
        finally { PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); AccessTools.Field(typeof(Game1), "_instance").SetValue(null,null); }
    }
    private static PlayerEntity EmptyPlayer() { var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity)); GC.SuppressFinalize(player); return player; }
    private static void PredictionTests()
    {
        PredictionCollision.Prepare();
        var history=new PositionHistory(); history.Observe(new Vector2(10,20),new Vector2(20,20),1,false); history.Observe(new Vector2(30,40),new Vector2(20,20),1,false);
        Check(history.At(.5f)==new Vector2(40,50),"King predicts ahead without adding an interpolation delay");
        Check(history.Current==new Vector2(30,40),"Rendering does not mutate physics position");
        history.Observe(new Vector2(40,50),new Vector2(20),2,false); Check(history.At(.7f)==new Vector2(40,50),"Screen transition snaps instead of crossing unrelated screens");
        history.Observe(new Vector2(400,50),new Vector2(20),2,false); Check(history.At(.7f)==new Vector2(400,50),"Teleport snaps");
        history.Observe(new Vector2(420,60),new Vector2(20),2,true); Check(history.At(.1f)==new Vector2(420,60),"Pause/focus loss snaps to actual saved position");
        history.Observe(new Vector2(420,60),Vector2.Zero,2,false); Check(history.At(.9f)==new Vector2(420,60),"Landing with zero velocity does not continue predicting a fall");
        history.Observe(new Vector2(420,60),new Vector2(float.NaN),2,false); Check(history.At(.9f)==new Vector2(420,60),"Invalid foreign velocities cannot poison rendering");
        history.Observe(new Vector2(420,60),new Vector2(-1.5f,.257f),2,false,true);
        Check(history.Velocity==Vector2.Zero,"Ground contact suppresses gravity drift and walking-into-wall bounce jitter");
        history.Observe(new Vector2(417,60),new Vector2(-3,.257f),2,false,true);
        Check(history.Velocity==new Vector2(-3,0),"Actual grounded movement remains predicted without a buffered frame");
        history.Observe(new Vector2(407,55),new Vector2(-20,-10),2,false);
        Check(history.At(.5f)==new Vector2(402,52.5f),"Material-modified displacement predicts water movement without doubling raw body velocity");
        history.Observe(new Vector2(403,55),new Vector2(-4,.257f),2,false,true);
        Check(history.At(.5f)==new Vector2(401,55),"Ice movement keeps subframe prediction with grounded gravity suppressed");
        history.Observe(new Vector2(403,55),new Vector2(3,-12),2,false,true,Vector2.Zero);
        Check(history.At(.5f)==new Vector2(404.5f,49),"Fresh native walk and takeoff remain visible before another physics integration");
        history.Observe(new Vector2(403,55),new Vector2(float.NaN),2,false,true,Vector2.Zero);
        Check(history.At(.5f)==new Vector2(403,55),"Fresh-input prediction cannot reintroduce invalid foreign velocity");
        history.Reset();history.Observe(Vector2.Zero,new Vector2(0,10),1,false);
        history.Observe(new Vector2(0,10),new Vector2(0,.75f),1,false,false,new Vector2(0,10));
        Check(history.At(.75f).Y==10.5625f,"First sand contact immediately uses outgoing sink speed instead of replaying a fast fall");
        history.Reset();history.Observe(Vector2.Zero,new Vector2(4,-8),1,false);
        history.Observe(new Vector2(2,-4),new Vector2(4,-7.5f),1,false,false,new Vector2(4,-8));
        Check(history.Velocity==new Vector2(2,-3.75f),"Outgoing acceleration retains the observed water multiplier");
        history.Reset();history.Observe(Vector2.Zero,new Vector2(0,.00001f),1,false);
        history.Observe(new Vector2(0,.001f),new Vector2(0,.257f),1,false,false,new Vector2(0,.00001f));
        Check(history.Velocity.Y<.3f,"Near-zero native speed cannot amplify position rounding into a visual lurch");
        CollisionPredictionTests();
        NativePredictionTests();
    }
    private sealed class UnauditedBlock : JumpKing.Level.IBlock
    {
        public Rectangle GetRect() { throw new Exception("Prediction must not call foreign geometry getters"); }
        public JumpKing.Level.BlockCollisionType Intersects(Rectangle box,out Rectangle overlap)
        { throw new Exception("Prediction must not execute custom collision code"); }
    }
    private static void CollisionPredictionTests()
    {
        var path=new PredictionPath();
        var floor=new JumpKing.Level.BoxBlock(new Rectangle(-100,20,400,1));
        var wall=new JumpKing.Level.BoxBlock(new Rectangle(20,-100,1,400));
        var ceiling=new JumpKing.Level.BoxBlock(new Rectangle(-100,-11,400,1));
        Action<Vector2,Vector2,JumpKing.Level.IBlock[]> test=(start,motion,blocks)=>{
            path.Begin(start,motion,10,10,false); foreach(var block in blocks)path.Add(block); path.Build();
            for(int i=0;i<=100;i++)
            {
                Vector2 predicted=path.At(i/100f); Rectangle box=new Rectangle((int)predicted.X,(int)predicted.Y,10,10),overlap;
                foreach(var block in blocks) Check(block.Intersects(box,out overlap)!=JumpKing.Level.BlockCollisionType.Collision_Blocking,"Every predicted fraction remains outside blocking native geometry");
            }
        };
        test(Vector2.Zero,new Vector2(0,64),new JumpKing.Level.IBlock[]{floor});
        Check(path.At(.9f).Y<=10.5f,"Fast fall cannot tunnel through a one-pixel floor");
        test(Vector2.Zero,new Vector2(64,0),new JumpKing.Level.IBlock[]{wall});
        test(Vector2.Zero,new Vector2(0,-64),new JumpKing.Level.IBlock[]{ceiling});
        test(Vector2.Zero,new Vector2(30,30),new JumpKing.Level.IBlock[]{wall,floor});
        test(Vector2.Zero,new Vector2(30,15),new JumpKing.Level.IBlock[]{wall});
        Check(path.At(.9f).Y>12 && path.At(.9f).X<=10.5f,"Prediction slides along a wall instead of freezing both axes");
        test(new Vector2(0,-30),new Vector2(20,50),new JumpKing.Level.IBlock[]{new JumpKing.Level.BoxBlock(new Rectangle(-100,-10,400,1))});
        foreach(JumpKing.Level.SlopeType slope in new[]{JumpKing.Level.SlopeType.TopLeft,JumpKing.Level.SlopeType.TopRight,JumpKing.Level.SlopeType.BottomLeft,JumpKing.Level.SlopeType.BottomRight})
            test(new Vector2(10,-15),new Vector2(8,50),new JumpKing.Level.IBlock[]{new JumpKing.Level.SlopeBlock(new Rectangle(0,10,40,40),slope)});
        path.Begin(Vector2.Zero,new Vector2(30,0),10,10,false);
        path.Add(new JumpKing.Level.WaterBlock(new Rectangle(10,0,100,100))); path.Build();
        Check(Math.Abs(path.At(.5f).X-15)<.001f,"Nonblocking water volume is not treated as a wall");
        path.Begin(Vector2.Zero,new Vector2(30,0),10,10,false); path.Add(new UnauditedBlock()); path.Build();
        Check(path.At(.9f).X>26,"Unknown nearby geometry preserves prediction without invoking foreign callbacks");
        path.Begin(Vector2.Zero,new Vector2(30,0),10,10,true); path.Build();
        Check(path.At(.5f)==new Vector2(15,0),"Open space still predicts ahead immediately");
        path.Begin(new Vector2(473,0),new Vector2(30,0),10,10,true); path.Build();
        Check(path.At(1).X==475,"Prediction respects native non-teleport screen caps");
        var collisionPatch=new Harmony("sfc.collision-ownership.tests");
        try
        {
            foreach(var type in new[]{typeof(JumpKing.Level.BoxBlock),typeof(JumpKing.Level.SlopeBlock)})
            {
                var map=type.GetInterfaceMap(typeof(JumpKing.Level.IBlock));
                var method=map.TargetMethods[Array.FindIndex(map.InterfaceMethods,m=>m.Name=="Intersects")];
                collisionPatch.Patch(method,prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"ForbiddenCollision")));
                path.Begin(Vector2.Zero,new Vector2(0,64),10,10,false);
                path.Add(type==typeof(JumpKing.Level.BoxBlock) ? floor : (JumpKing.Level.IBlock)new JumpKing.Level.SlopeBlock(new Rectangle(0,20,30,30),JumpKing.Level.SlopeType.TopLeft));
                path.Build(); Check(path.At(.5f).Y>0 && path.At(.5f).Y<32,"Foreign hooks do not disable native shape clipping or execute speculative gameplay callbacks");
                collisionPatch.UnpatchAll(collisionPatch.Id);
            }
        }
        finally { collisionPatch.UnpatchAll(collisionPatch.Id); }
        PreparedPredictionTests(path,floor);
        var benchFloor=new JumpKing.Level.BoxBlock(new Rectangle(-100,50,400,10));
        var stopwatch=Stopwatch.StartNew();
        for(int i=0;i<10000;i++)
        { path.Begin(Vector2.Zero,new Vector2(30,30),18,26,false); path.Add(wall); path.Add(benchFloor); path.Build(); path.At(.25f);path.At(.5f);path.At(.75f); }
        stopwatch.Stop(); Console.WriteLine("[PERF] Local collision path + 3 draws: "+(stopwatch.Elapsed.TotalMilliseconds/10000).ToString("F4")+" ms/native tick (synthetic scene)");
    }
    private static void PreparedPredictionTests(PredictionPath path,JumpKing.Level.IBlock floor)
    {
        var field=AccessTools.Field(typeof(JumpKing.Level.LevelManager),"m_screens");
        var previous=field.GetValue(null);
        Func<JumpKing.Level.IBlock[],JumpKing.Level.LevelScreen> screen=blocks=>new JumpKing.Level.LevelScreen(0,blocks,
            new JumpKing.Level.LevelScreen.Graphics(),false,new JumpKing.Level.TeleportLink[0],0,null);
        var box=new Rectangle(0,0,10,10);
        try
        {
            var screens=new[]{screen(new[]{floor}),screen(new JumpKing.Level.IBlock[0]),screen(new JumpKing.Level.IBlock[0])};
            field.SetValue(null,screens);
            path.Prepare(Vector2.Zero,new Vector2(0,64),box,1);
            Check(path.At(.9f).Y<=10.5f,"Production geometry discovery includes the neighboring screen");
            screens[0]=screen(new JumpKing.Level.IBlock[0]);
            path.Prepare(Vector2.Zero,new Vector2(0,64),box,1);
            Check(path.At(.5f).Y==32,"Next native tick reads changed live geometry instead of stale collision data");
            screens[1]=new JumpKing.Level.LevelScreen(1,new JumpKing.Level.IBlock[0],new JumpKing.Level.LevelScreen.Graphics(),false,
                new[]{new JumpKing.Level.TeleportLink(2)},0,null);
            path.Prepare(new Vector2(473,0),new Vector2(30,0),box,1);
            Check(path.At(.5f)==new Vector2(473,0),"Teleport boundary is left to the next native update");
            screens[1]=screen(Enumerable.Repeat(floor,257).ToArray());
            path.Prepare(Vector2.Zero,new Vector2(0,64),box,1);
            Check(path.At(.5f).Y>0 && path.At(.5f).Y<=10.5f,"Nearby geometry overflow retains clipping against collected blocks");
            var distant=new JumpKing.Level.BoxBlock(new Rectangle(1000,1000,10,10));
            screens[1]=screen(Enumerable.Repeat<JumpKing.Level.IBlock>(distant,8193).ToArray());
            path.Prepare(Vector2.Zero,new Vector2(0,64),box,1);
            Check(path.At(.5f).Y==32,"Scan budget does not disable presentation across a dense scene");
            screens[1]=screen(new JumpKing.Level.IBlock[]{new JumpKing.Level.SandBlock(new Rectangle(0,10,100,100))});
            path.Prepare(Vector2.Zero,new Vector2(0,64),box,1);
            Check(path.At(.5f).Y==32,"Special sand support follows observed movement without speculative material logic");
            field.SetValue(null,null);
            path.Prepare(Vector2.Zero,new Vector2(0,64),box,0);
            Check(path.At(.5f).Y==32,"Missing geometry keeps current motion without retaining old blocks");
            var slope=new JumpKing.Level.SlopeBlock(new Rectangle(0,10,40,40),JumpKing.Level.SlopeType.TopLeft);
            screens=new[]{screen(Enumerable.Repeat<JumpKing.Level.IBlock>(distant,1000).Concat(new[]{slope}).ToArray())};
            field.SetValue(null,screens);
            var watch=Stopwatch.StartNew();
            for(int i=0;i<10000;i++)
            { path.Prepare(new Vector2(10,-15),new Vector2(8,50),box,0); path.At(.25f);path.At(.5f);path.At(.75f); }
            watch.Stop(); Console.WriteLine("[PERF] Live screen scan (1001 blocks), slope path + 3 draws: "+(watch.Elapsed.TotalMilliseconds/10000).ToString("F4")+" ms/native tick (synthetic scene)");
            var hook=new Harmony("sfc.visual-endpoint-ownership.tests");
            try
            {
                hook.Patch(PredictionCollision.Slope,prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"ForbiddenCollision")));
                hook.Patch(AccessTools.Method(typeof(JumpKing.Level.SlopeBlock),"GetNormal"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"ForbiddenCollision")));
                path.Prepare(new Vector2(10,-15),new Vector2(8,50),box,0,new Vector2(8,50));
                Check(path.At(.5f).Y>-15,"Native visual endpoint uses stored geometry without foreign collision or normal callbacks");
            }
            finally { hook.UnpatchAll(hook.Id); }
            watch.Restart();
            for(int i=0;i<10000;i++)
            { path.Prepare(new Vector2(10,-15),new Vector2(8,50),box,0,new Vector2(8,50)); path.At(.25f);path.At(.5f);path.At(.75f); }
            watch.Stop(); Console.WriteLine("[PERF] Live screen scan (1001 blocks), native contact endpoint + 3 draws: "+(watch.Elapsed.TotalMilliseconds/10000).ToString("F4")+" ms/native tick (synthetic scene)");
        }
        finally { field.SetValue(null,previous); }
    }
    private static void ForbiddenCollision() { throw new Exception("Prediction called a foreign collision hook"); }
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.SetData("SubframeCharge.LogDirectory",AppDomain.CurrentDomain.BaseDirectory);
            AccessTools.Property(typeof(SettingsStore),"Current").SetValue(null,new SubframeChargeSettings(),null);
            AccessTools.Field(typeof(SettingsStore),"loaded").SetValue(null,true);
            if(args.Length==4 && args[0]=="--terrain") { TerrainTests(args[1],args[2],args[3]); Console.WriteLine("Terrain presentation checks: "+checks); return 0; }
            if (args.Length != 0 && args[0] == "--native-startup") { NativeStartupClockTest(); Console.WriteLine("Native startup animation check passed"); return 0; }
            if (args.Length != 0 && args[0] == "--input-startup") { InputTransitionTests(); Console.WriteLine("Warmed native input transition checks: "+checks); return 0; }
            if (args.Length != 0 && args[0] == "--buffered-jumps") { BufferedTreeTests(); Console.WriteLine("Buffered jump tree checks: "+checks); return 0; }
            SettingsTests(); InputTests(); ClockTests(); NativeInputTests(); NativeKeyboardJumpTests(); TextInputRoutingTests(); MenuTests(); ContinueTransitionTests(); StartupInputTests(); ReplayOwnershipTests(); PredictionTests();
            if(args.Length!=0 && args[0]!="--fast") CameraCooperationTests(args[0]);
            if (!args.Contains("--fast")) GraphicsTests();
            Console.WriteLine("[OK] Presentation, input, settings and native optimization checks: "+checks); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { PerformanceFeatures.Uninstall(); }
    }

    private static void StartupInputTests()
    {
        foreach(bool refresh in new[]{false,true})
        {
            var fixture=new Harmony("sfc.startup-input.tests"); var game=MakeGame();
            fixture.Patch(AccessTools.Method(typeof(ResponsiveInput),"Poll"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(PadInstance),"GetPadState"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessTick")));
            fixture.Patch(AccessTools.Method(typeof(ControllerManager),"Update"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessController")));
            fixture.Patch(AccessTools.Method(typeof(MenuCadence),"Pump"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessMenu")));
            fixture.Patch(AccessTools.Method(typeof(JKRuntime.UI.UiPointer),"Update"),prefix:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"Skip")){priority=Priority.First});
            var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
            var device=new Pad{Binding=new PadBinding{jump=new[]{32},confirm=new[]{32}}}; var pad=new PadInstance(device);
            AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>{pad});
            AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(manager,new MenuController(manager));
            var jump=(JumpGame)FormatterServices.GetUninitializedObject(typeof(JumpGame)); AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,jump);
            SettingsStore.Current.Enabled=true; SettingsStore.Current.SetInputs(true); SettingsStore.Current.HighRefresh=refresh;
            PerformanceFeatures.Install(); FeatureClock.BeforeTick(game); menuPlaying=false;
            var title=(GameTitleScreen)FormatterServices.GetUninitializedObject(typeof(GameTitleScreen));
            AccessTools.Field(typeof(IBTnode),"m_last_result").SetValue(title,BTresult.Running);
            AccessTools.Field(typeof(GameTitleScreen),"m_menu").SetValue(title,new MenuNode());
            MenuCadence.TitleStart(title); var intro=new MenuNode(); var tree=new BTmanager(intro);
            var pressType=typeof(GameTitleScreen).GetNestedType("PressStart",BindingFlags.NonPublic);
            var press=Activator.CreateInstance(pressType,true);
            try
            {
                MenuCadence.RunTitle(tree,1f/60); ResponsiveInput.Poll(); manager.Update();
                device.Buttons=new[]{32}; ResponsiveInput.Poll(); MenuCadence.Pump(1f/240);
                device.Buttons=new int[0]; ResponsiveInput.Poll();
                for(int i=0;i<3;i++) MenuCadence.Pump(1f/240);
                Check(intro.Calls==1 && Math.Abs(intro.Time-1f/60)<.00001f,"Intro retains one native-time owner at refresh="+refresh);
                manager.Update();
                Check(new WaitForPressStart().Run(new TickData(1f/60,1))==BTresult.Success,"Actual logo-skip node receives a between-tick Space tap");
                Check((BTresult)AccessTools.Method(pressType,"MyRun").Invoke(press,new object[]{new TickData(1f/60,1)})==BTresult.Success,"Actual PressStart node receives Jump while Enabled is on without a player");
                manager.Update();
                Check((BTresult)AccessTools.Method(pressType,"MyRun").Invoke(press,new object[]{new TickData(1f/60,2)})==BTresult.Running,"Startup tap is consumed once, not replayed into the next native tick");
                MenuCadence.RunTitle(tree,1f/60);
                Check(intro.Calls==2,"Native intro continues while fast-menu pump is inactive");
            }
            finally
            {
                PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null;
                AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,null); AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            }
        }
    }

    private sealed class ReplaySampler : JKRuntime.Input.IHighRateInput
    {
        internal bool Healthy=true;
        public bool Enabled { get { return Healthy; } }
        public bool RequestedEnabled { get { return true; } }
        public bool Available { get { return Healthy; } }
        public void Start() { }
        public void Configure(int[][] keys,JKRuntime.Input.XInputJumpBinding[] xbox,JKRuntime.Input.DirectInputJumpBinding[] legacy,IntPtr window,bool enabled) { }
        public bool CanMeasureDirectInput(Guid id) { return false; }
        public bool CanMeasurePhysical(int source) { return Healthy && source==0; }
        public bool TryDequeue(out JKRuntime.Input.JumpInputTransition value) { value=default(JKRuntime.Input.JumpInputTransition); return false; }
        public void Reset() { }
        public void LogHealth(long timestamp) { }
        public void Dispose() { }
    }
    private static void ReplayOwnershipTests()
    {
        var fixture=new Harmony("sfc.replay-ownership.tests");
        fixture.Patch(AccessTools.Method(typeof(SubframeChargeInstaller),"OwnsCompletedTap"),transpiler:new HarmonyMethod(AccessTools.Method(typeof(PerformanceTests),"HeadlessMenu")));
        AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,FormatterServices.GetUninitializedObject(typeof(JumpGame))); menuPlaying=true;
        var player=EmptyPlayer(); player.m_body=(BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp)); GameLoop.m_player=player;
        var state=(SubframeChargeState)FormatterServices.GetUninitializedObject(typeof(SubframeChargeState)); var sampler=new ReplaySampler();
        var pad=new PadInstance(new Pad{Identifier="pc_keyboard_jump_king"});
        AccessTools.Field(typeof(SubframeChargeState),"sampler").SetValue(state,sampler);
        AccessTools.Field(typeof(SubframeChargeInstaller),"replacementJumpState").SetValue(null,state);
        AccessTools.Field(typeof(SubframeChargeInstaller),"installedBody").SetValue(null,player.m_body);
        AccessTools.Field(typeof(SubframeChargeInstaller),"jumpBindings").SetValue(null,new JKRuntime.Gameplay.JumpNodeBindings());
        AccessTools.Field(typeof(PlayerEntity),"m_jump_state").SetValue(player,state);
        SettingsStore.Current.Enabled=true;
        try
        {
            Check(SubframeChargeInstaller.OwnsCompletedTap(pad),"Live correcting node with healthy device owns completed taps");
            menuPlaying=false; Check(!SubframeChargeInstaller.OwnsCompletedTap(pad),"A retained player from a previous attempt cannot claim title-screen Jump"); menuPlaying=true;
            using(JKRuntime.Gameplay.JumpSlot.SuspendChargePolicy("test.controller"))
                Check(!SubframeChargeInstaller.OwnsCompletedTap(pad),"Reserved alternate controller retains independent Jump input");
            Check(!SubframeChargeInstaller.OwnsCompletedTap(new PadInstance(new Pad())),"Unknown device is not claimed by the charge sampler");
            sampler.Healthy=false; Check(!SubframeChargeInstaller.OwnsCompletedTap(pad),"Unavailable sampler cannot swallow native taps"); sampler.Healthy=true;
            AccessTools.Field(typeof(SubframeChargeState),"observationOnly").SetValue(state,true);
            Check(!SubframeChargeInstaller.OwnsCompletedTap(pad),"Measurement-only observer never claims tap replay");
            AccessTools.Field(typeof(SubframeChargeState),"observationOnly").SetValue(state,false);
            AccessTools.Field(typeof(PlayerEntity),"m_jump_state").SetValue(player,null);
            Check(!SubframeChargeInstaller.OwnsCompletedTap(pad),"Externally replaced player jump reference revokes replay ownership");
        }
        finally
        {
            AccessTools.Field(typeof(SubframeChargeInstaller),"replacementJumpState").SetValue(null,null);
            AccessTools.Field(typeof(SubframeChargeInstaller),"installedBody").SetValue(null,null);
            AccessTools.Field(typeof(SubframeChargeInstaller),"jumpBindings").SetValue(null,null); GameLoop.m_player=null;
            fixture.UnpatchAll(fixture.Id); AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,null); menuPlaying=false;
        }
    }
}
