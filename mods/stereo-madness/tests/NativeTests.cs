using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.GameManager.MultiEnding.NormalEnding;
using JumpKing.JKMemory;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.XnaWrappers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StereoMadness;
using JKRuntime.Input;
using JumpKing.Controller;
using MegaMappingExpansion.Api;
using JKRuntime.Settings;

internal static class NativeTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private sealed class ResultsScene : IMappingScene
    {
        internal readonly Dictionary<string,string> Flags = new Dictionary<string,string>();
        public bool Available { get { return true; } }
        public long Generation { get { return 1; } }
        public void SetFlag(string id,string value) { Flags[id]=value; }
        public string GetFlag(string id) { return Flags[id]; }
        public void Emit(string id) { }
        public ISceneEffect Activate(string owner,string id) { throw new NotSupportedException(); }
        public ISceneEffect Apply(string owner,EffectDefinition value) { throw new NotSupportedException(); }
        public SceneObjectInfo[] InspectObjects() { return new SceneObjectInfo[0]; }
        public ScenePropertyInfo[] DescribeProperties(string id) { return new ScenePropertyInfo[0]; }
        public EffectInfo[] InspectEffects() { return new EffectInfo[0]; }
        public string[] InspectErrors() { return new string[0]; }
    }
    private static int checks;
    private sealed class RestoredChargeProbe : Component { }
    private static bool SkipPlayerSave() { return false; }
    private static bool InactiveFixture(ref bool __result) { __result=false; return false; }
    private static bool FixtureMouse(ref Microsoft.Xna.Framework.Input.MouseState __result) { __result=new Microsoft.Xna.Framework.Input.MouseState(); return false; }
    private static bool AdvanceCourse(StereoMadness.Controller __instance)
    { __instance.Advance(false); return false; }
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("[OK] " + label); }
    private sealed class DeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
    }
    [STAThread] public static int Main(string[] args)
    {
        try { Run(args); Console.WriteLine("[OK] " + checks + " native checks"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Run(string[] args)
    {
        Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"));
        Type smoothRenderer = args.Length > 2 ? Assembly.LoadFrom(args[2]).GetType("SmoothCamera.Renderer", true) : null;
        typeof(SoundEffect).GetMethod("InitializeSoundEffect", F).Invoke(null, null);
        var native = typeof(Game1).Assembly;
        var prefsType = native.GetType("JumpKing.PlayerPreferences.SoundPrefs");
        var managerType = native.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime");
        var settings = Activator.CreateInstance(prefsType);
        var manager = FormatterServices.GetUninitializedObject(managerType);
        managerType.BaseType.GetField("m_settings", F).SetValue(manager, settings);
        managerType.BaseType.GetField("instance", F).SetValue(null, manager);
        string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "graphics"); Directory.CreateDirectory(output);
        using (var window = new Form { ShowInTaskbar = false })
        using (var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters { DeviceWindowHandle = window.Handle, BackBufferWidth = 480, BackBufferHeight = 360 }))
        using (var batch = new SpriteBatch(device))
        using (var target = new RenderTarget2D(device, 480, 360))
        {
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1)); GC.SuppressFinalize(game);
            typeof(Game1).GetField("_instance", F).SetValue(null, game);
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new DeviceService { GraphicsDevice = device });
            typeof(Game).GetField("_services", F).SetValue(game, services);
            foreach (var field in typeof(Game).GetFields(F).Where(f => f.FieldType == typeof(IGraphicsDeviceService))) field.SetValue(game, services.GetService(typeof(IGraphicsDeviceService)));
            game.contentManager = new JKContentManager(); game.contentManager.root = args[1]; Game1.spriteBatch = batch;
            typeof(LevelManager).GetField("_total_screens", F).SetValue(null, 2);
            var screens = new[] {
                new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(0, 336, 480, 24)) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
                new LevelScreen(1, new IBlock[] { new BoxBlock(new Rectangle(0, -216, 480, 24)) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null)
            };
            typeof(LevelManager).GetField("m_screens", F).SetValue(null, screens);
            using (var content = new ContentManager(services, args[0]))
            using (var scope = new RuntimeScope())
            {
                game.contentManager.playerSprites._CurrentSprites = new LayeredKingSprites(content.Load<Texture2D>("Content/king/base"));
                game.contentManager.font.MenuFontSmall = content.Load<SpriteFont>("Content/font/sf_small");
                foreach (var field in game.contentManager.font.GetType().GetFields(F).Where(f => f.FieldType == typeof(SpriteFont)))
                    field.SetValue(game.contentManager.font, game.contentManager.font.MenuFontSmall);
                game.contentManager.font.MenuFont = content.Load<SpriteFont>("Content/font/sf_litter_lover2_bold");
                foreach (var field in game.contentManager.gui.GetType().GetFields(F).Where(f => f.FieldType == typeof(Sprite)))
                    field.SetValue(game.contentManager.gui, Sprite.CreateSprite(content.Load<Texture2D>("Content/gui/thumbs_up")));
                ModEntry.Prepare(scope); var world = ModEntry.World;
                typeof(Commands).GetMethod("Start", F).Invoke(null, null);
                scope.Defer(delegate { typeof(Commands).GetMethod("Stop", F).Invoke(null, null); });
                Check(world != null && world.Course.Objects.Length > 1900, "Full packaged course decoded on the native graphics thread");
                var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
                typeof(Entity).GetField("m_components", F).SetValue(player, new List<Component>());
                player.m_body = new BodyComp(new Vector2(231, 310), 18, 26);
                var tree = new BehaviorTreeComp(new BTselector());
                player.AddComponents(player.m_body, tree);
                Sprite previous = game.contentManager.playerSprites.idle; player.SetSprite(previous); GameLoop.m_player = player;
                for (int pass = 0; pass < 3; pass++) {
                    var results = new ResultsScene();
                    using (var controller = new StereoMadness.Controller(player, world, results)) {
                        ModEntry.Active = controller;
                        var statsType = native.GetType("JumpKing.MiscSystems.Achievements.AchievementManager");
                        var stats = FormatterServices.GetUninitializedObject(statsType);
                        statsType.GetField("instance", F).SetValue(null, stats);
                        PlayerEntity.OnJumpCall = (PlayerEntity.OnJump)Delegate.CreateDelegate(typeof(PlayerEntity.OnJump), stats, statsType.GetMethod("OnPlayerJump", F));
                        controller.Model.Reset();
                        var inputManager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
                        var pad = (PadInstance)FormatterServices.GetUninitializedObject(typeof(PadInstance));
                        typeof(ControllerManager).GetField("m_pads", F).SetValue(inputManager, new List<PadInstance>{pad});
                        ControllerManager.instance = inputManager;
                        NativeInputFrames.Publish(pad, new PadState{jump=true}, new PadState{jump=true});
                        typeof(ActionInputs).GetMethod("BeginTick", F).Invoke(null,null);
                        controller.Advance(ActionInputs.Read("native.jump").Down);
                        Check(controller.Model.Jumps == 1, "SFC-style completed native input pulse reaches the cube while charge is suspended");
                        NativeInputFrames.Publish(pad, new PadState(), new PadState());
                        typeof(ActionInputs).GetMethod("BeginTick", F).Invoke(null,null);
                        Check(!ActionInputs.Read("native.jump").Down, "Completed input pulse is consumed in one native tick");
                        for (int tick = 0; tick < 90; tick++) controller.Advance(true);
                        object totals = statsType.GetField("m_all_time_stats", F).GetValue(stats);
                        Check((int)totals.GetType().GetField("jumps").GetValue(totals) == controller.Model.Jumps && controller.Model.Jumps > 1,
                            "Real AchievementManager counts each held-button takeoff exactly once");
                        controller.Model.Ship = true; controller.Model.Y = 330;
                        int before = controller.Model.Jumps; for (int tick = 0; tick < 20; tick++) controller.Advance(true);
                        Check(controller.Model.Jumps == before, "Native ship thrust adds no jump events");
                        PlayerEntity.OnJumpCall = null;
                        var spike = world.Course.Objects.First(o => o.Kind == "hazard");
                        int deaths = 0; PlayerEntity.OnSplatCall = delegate { deaths++; };
                        PlayerEntity.OnSplatCall += (PlayerEntity.OnSplat)Delegate.CreateDelegate(typeof(PlayerEntity.OnSplat), stats, statsType.GetMethod("OnPlayerFall", F));
                        controller.Model.X = spike.X; controller.Model.Y = spike.Y; controller.Model.VY = 0;
                        controller.Advance(false); for (int tick = 0; tick < 15; tick++) controller.Advance(false);
                        Check(deaths == 1 && controller.Model.Dead, "A lethal contact emits one native fall, without repeated death events");
                        PlayerEntity.OnSplatCall = null; controller.Model.Reset();
                        Check(System.Threading.SpinWait.SpinUntil(() => world.Music.State == JKSoundState.Stopped, 250), "Death stops the previous music voice");
                        world.Music.Play();
                        Check(!player.m_body.Enabled && !tree.Enabled && PlayerControl.Owner(player.m_body) == "stereo-madness", "Controller owns and suspends the native body/tree");
                        using (var patches = new OwnedPatches("stereo.fixture")) {
                            ModEntry.PatchEnding(patches);
                            var babeType = native.GetType("JumpKing.GameManager.MultiEnding.NormalEnding.EndingBabe", true);
                            var babe = FormatterServices.GetUninitializedObject(babeType);
                            controller.Model.Y = 600;
                            controller.Advance(false);
                            // Without the guard this real Draw reaches its missing sprite.
                            babeType.GetMethod("Draw").Invoke(babe, null);
                            Check(!(bool)typeof(ModEntry).GetMethod("DrawEndingBabe", F).Invoke(null, null)
                                && !new NormalEnding().CheckWin(player),
                                "Native Babe and victory stay gated when course altitude crosses the ending screen");
                            patches.Add(typeof(LevelScreen).GetMethod("Draw"), prefix: typeof(ModEntry).GetMethod("DrawScene", F));
                            patches.Add(typeof(LevelScreen).GetMethod("DrawForeground"), prefix: typeof(ModEntry).GetMethod("DrawForeground", F));
                            if (smoothRenderer != null) {
                                patches.Add(smoothRenderer.GetMethod("BeginFrame", F), prefix: typeof(ModEntry).GetMethod("NativeViewport", F));
                                foreach (string name in new[] { "frameActive", "worldPending", "canPresent" }) smoothRenderer.GetField(name, F).SetValue(null, true);
                                smoothRenderer.GetMethod("BeginFrame", F).Invoke(null, null);
                                Check(!(bool)smoothRenderer.GetField("frameActive", F).GetValue(null) && !(bool)smoothRenderer.GetField("canPresent", F).GetValue(null),
                                    "Installed Smooth Camera compositor yields the viewport without changing settings");
                            }
                            var screen = (LevelScreen)FormatterServices.GetUninitializedObject(typeof(LevelScreen));
                            foreach (double x in new[] { 0d, 2600d, 15990d, 19000d, 25110d, 36000d, 45870d, 50500d }) {
                                controller.Model.X = x; controller.Model.Y = x >= 15990 && x < 25110 || x >= 45870 ? 330 : 90;
                                controller.Model.Ship = x >= 15990 && x < 25110 || x >= 45870;
                                controller.Model.CameraY = controller.Model.Ship ? 160 : 0;
                                controller.Model.Ceiling = controller.Model.Ship ? (double?)600 : null;
                                device.SetRenderTarget(target); device.Clear(Color.Magenta);
                                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                                screen.Draw(); player.Draw(); screen.DrawForeground(); batch.End(); device.SetRenderTarget(null);
                                if (pass == 0) using (var file = File.Create(Path.Combine(output, "course-" + x + ".png"))) target.SaveAsPng(file, 480, 360);
                            }
                        }
                        controller.Model.Reset(); controller.Advance(true);
                        var snapshot=JKRuntime.State.GameState.Snapshots.Capture();
                        Vector2 savedNative=player.m_body.Position;
                        double savedX=controller.Model.X, savedY=controller.Model.Y, savedVy=controller.Model.VY, savedTime=controller.Model.Time;
                        for(int tick=0;tick<5;tick++)controller.Advance(false);
                        Check(player.m_body.Position.X>savedNative.X,"Native world X advances with the course, independently of the camera");
                        try { JKRuntime.State.GameState.Snapshots.Restore(snapshot); }
                        catch(Exception error) { Console.WriteLine(error); throw; }
                        Check(player.m_body.Position==savedNative && controller.Model.X==savedX && controller.Model.Y==savedY && controller.Model.VY==savedVy && controller.Model.Time==savedTime,
                            "Runtime snapshot restores native position and exact airborne controller state");
                        var fly=world.Course.Objects.First(o=>o.Kind=="fly");
                        player.m_body.Position=new Vector2(156+(float)(fly.X+100)*.5f,239-(float)fly.Y*.5f);
                        player.m_body.Velocity=Vector2.Zero;
                        Check(controller.ReadNativePosition() && controller.Model.Ship && !controller.Model.Dead && Math.Abs(controller.Model.X-fly.X-100)<.01,
                            "Position-only save/load is accepted by the controller and restores ship mode");
                        Check(!controller.ReadNativePosition(),"Published native coordinates do not retrigger restores");
                        if(pass==0) VerifyInstalledSaveStates(args[0],controller,player,world);
                        controller.Model.Reset(); controller.Advance(true);
                        double authoritativeX=controller.Model.X, authoritativeTime=controller.Model.Time;
                        int authoritativeJumps=controller.Model.Jumps;
                        Vector2 authoritativeBody=player.m_body.Position;
                        var scheduler=typeof(PresentationScheduling);
                        var client=(PresentationScheduling.Client)Activator.CreateInstance(typeof(PresentationScheduling.Client), F, null,
                            new object[]{"subframe-charge",new Func<PresentationRequest>(()=>new PresentationRequest(true,true)),null,null},null);
                        typeof(PresentationScheduling.Client).GetProperty("Active").GetSetMethod(true).Invoke(client,new object[]{true});
                        typeof(PresentationScheduling.Client).GetProperty("HighRefresh").GetSetMethod(true).Invoke(client,new object[]{true});
                        scheduler.GetField("snapshot",F).SetValue(null,new[]{client});
                        scheduler.GetField("owner",F).SetValue(null,game);
                        typeof(Game).GetField("_targetElapsedTime",F).SetValue(game,TimeSpan.FromTicks(170000));
                        try {
                            double previousX=double.NegativeInfinity;
                            for(int frame=0;frame<4;frame++) {
                                // Runtime publishes pause observations every native tick,
                                // including unchanged false observations after player Update.
                                typeof(NativePause).GetMethod("Observe",F).Invoke(null,null);
                                scheduler.GetField("remainder",F).SetValue(null,42500L*frame);
                                Check(controller.ViewX>previousX,"Presentation advances at quarter tick " + frame);
                                previousX=controller.ViewX;
                                device.SetRenderTarget(target); device.Clear(Color.Black);
                                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp);
                                controller.DrawScene(); player.Draw(); controller.DrawHud(); batch.End(); device.SetRenderTarget(null);
                                Check(controller.Model.X==authoritativeX && controller.Model.Time==authoritativeTime && controller.Model.Jumps==authoritativeJumps && player.m_body.Position==authoritativeBody,
                                    "240 Hz drawing leaves simulation, native body and statistics unchanged");
                            }
                        } finally {
                            scheduler.GetField("snapshot",F).SetValue(null,new PresentationScheduling.Client[0]);
                            scheduler.GetField("owner",F).SetValue(null,null);
                            scheduler.GetField("remainder",F).SetValue(null,0L);
                        }
                        Check(controller.ViewX==controller.Model.X,"Disabling high refresh returns to the completed native tick");
                        Check(world.Music.State == JKSoundState.Playing, "Native music starts with the run");
                        typeof(StereoMadness.Controller).GetMethod("OnPause", F).Invoke(controller, new object[] { true, 0L });
                        Check(world.Music.State == JKSoundState.Paused, "Native pause pauses course audio");
                        double frozen = controller.Model.X;
                        typeof(StereoMadness.Controller).GetMethod("Update", F).Invoke(controller, new object[] { 1f / 60 });
                        Check(controller.Model.X == frozen, "Pause freezes simulation");
                        typeof(StereoMadness.Controller).GetMethod("OnPause", F).Invoke(controller, new object[] { false, 1L });
                        Check(world.Music.State == JKSoundState.Playing, "Resume resumes the same sound instance");
                        IDisposable restoredProbe = null;
                        int restorations = 0;
                        // SFC reinstalls trajectory/frame components when charge resumes.
                        using (var charge = JumpSlot.RegisterChargePolicy(
                            delegate { restorations++; restoredProbe = new ComponentAttachment(player, new RestoredChargeProbe()); },
                            delegate { if (restoredProbe != null) { restoredProbe.Dispose(); restoredProbe = null; } }))
                        using (var finishPatches = new OwnedPatches("stereo.finish.fixture")) {
                        ModEntry.PatchEnding(finishPatches);
                        finishPatches.Add(typeof(PlayerEntity).GetMethod("Update", F), prefix: typeof(NativeTests).GetMethod("SkipPlayerSave", F));
                        finishPatches.Add(typeof(StereoMadness.Controller).GetMethod("Update", F), prefix: typeof(NativeTests).GetMethod("AdvanceCourse", F));
                        controller.Model.Reset(); controller.Model.X = world.Course.EndX - 35;
                        player.UpdateComponents(1f / 60);
                        Check(controller.Model.Complete && !controller.Released && restorations == 0,
                            "Finish keeps component ownership until native component iteration completes");
                        controller.Finish();
                        typeof(Commands).GetMethod("Drain", F).Invoke(null, null);
                        Check(restorations == 1, "Deferred finish restores charge components exactly once outside enumeration");
                        Check(results.Flags["falls"] == "1" && int.Parse(results.Flags["jumps"]) > 1,
                            "MME results capture real native falls as deaths and native jump totals");
                        Check(player.m_body.Enabled && tree.Enabled && PlayerControl.Owner(player.m_body) == null, "Finish releases body, tree and ownership");
                        Check(Camera.CurrentScreen == 1 && player.m_body.Position.X == 340, "Finish enters the native ending screen");
                        var ending = new NormalEnding();
                        typeof(BodyComp).GetField("_is_on_ground", F).SetValue(player.m_body, false);
                        for (int tick = 0; tick < 4; tick++) typeof(BodyComp).GetMethod("Update", F).Invoke(player.m_body, new object[] { 1f / 60 });
                        Check(player.m_body.IsOnGround, "Native physics lands on the finish platform without forcing grounded state");
                        Check(ending.CheckWin(player), "Installed native NormalEnding accepts the handoff");
                        Check((bool)typeof(ModEntry).GetMethod("DrawEndingBabe", F).Invoke(null, null),
                            "Native Babe drawing resumes after the real course finish");
                        }
                    }
                    Check(System.Threading.SpinWait.SpinUntil(() => world.Music.State == JKSoundState.Stopped, 250) && ModEntry.Active == null, "Attempt teardown stops audio and clears active controller");
                    Check(((List<Component>)typeof(Entity).GetField("m_components", F).GetValue(player)).Count == 2, "Attempt teardown removes only its attached component");
                }
                foreach (string cancellation in new[] { "disposed", "replaced-player", "level-unload" }) {
                    var abandonedResults = new ResultsScene();
                    using (var abandoned = new StereoMadness.Controller(player, world, abandonedResults)) {
                        Vector2 position = player.m_body.Position;
                        abandoned.Finish();
                        if (cancellation == "disposed") abandoned.Dispose();
                        else if (cancellation == "replaced-player") GameLoop.m_player = null;
                        else typeof(Commands).GetMethod("Stop", F).Invoke(null, null);
                        typeof(Commands).GetMethod("Drain", F).Invoke(null, null);
                        Check(player.m_body.Position == position && abandonedResults.Flags.Count == 0,
                            "Queued finish cannot move a stale player or publish results after " + cancellation);
                        GameLoop.m_player = player;
                    }
                    typeof(Commands).GetMethod("Start", F).Invoke(null, null);
                    Check(System.Threading.SpinWait.SpinUntil(() => world.Music.State == JKSoundState.Stopped, 250), "Cancelled attempt stops audio");
                }
                string mappingPath=Path.Combine(Path.GetDirectoryName(args[1]), "..", "mega-mapping-expansion", "_INTERNAL", "SceneCacheCompiler.exe");
                var mapping=Assembly.LoadFrom(Path.GetFullPath(mappingPath));
                var endings=mapping.GetType("MegaMappingExpansion.Endings.NativeEndings",true);
                var mappingEntry=mapping.GetType("MegaMappingExpansion.ModEntry",true);
                using (var mappingScope = new RuntimeScope()) {
                    mappingEntry.GetMethods(F).Single(m => m.Name == "PrepareAttempt" && m.GetParameters().Length == 4)
                        .Invoke(null,new object[]{mappingScope,args[1],2,true});
                    Check(true,"MME prepares the actual course scene and native intro before activation");
                    var intro = (IBTnode)typeof(IntroState).GetMethod("MakeLegendHasItText",F).Invoke(new IntroState(),new object[]{false});
                    string text = string.Join(" ", IntroText(intro));
                    Check(text.Contains("Legend says the Babe waits at the top.") && text.Contains("This one read the map sideways.")
                        && text.Contains("She is always right.") && text.Contains("Keep going that way."),
                        "Native intro tree contains all authored Stereo Madness pages");
                }
                game.contentManager.audio=new JKContentManager.Audio();
                game.contentManager.audio.music=new JKContentManager.Audio.Music { Ending=world.Finish };
                try {
                    endings.GetMethod("Prepare",F).Invoke(null,new object[]{args[1]});
                    var ending=new NormalEnding();
                    var endingTree=(BTmanager)typeof(NormalEnding).GetMethod("MakeBT",F).Invoke(ending,null);
                    for(int tick=0;tick<420;tick++) endingTree.Run(1f/60);
                    Check(endingTree.LastResult==BTresult.Success,"Authored MME ending executes through native MakeBT and reaches completion instead of holding forever");
                } finally { endings.GetMethod("Release",F).Invoke(null,null); ModEntry.StopNativeMusic(); }
                Check(world.Background != null && !world.Background.IsDisposed, "World resources survive attempt restarts");
                scope.Dispose(); Check(ModEntry.World == null && world.Background.IsDisposed, "World exit clears and disposes prepared resources");
            }
        }
    }
    private static IEnumerable<string> IntroText(IBTnode node)
    {
        if (node is SpawnTextNode) yield return (string)typeof(SpawnTextNode).GetField("m_text",F).GetValue(node);
        var children=node.GetRelatedNodes();
        if (children!=null) foreach(var child in children) foreach(string text in IntroText(child)) yield return text;
    }
    private static void VerifyInstalledSaveStates(string gameDir, StereoMadness.Controller controller, PlayerEntity player, StereoMadness.Resources world)
    {
        string workshop=Path.GetFullPath(Path.Combine(gameDir,"..","..","workshop","content","1061090","3161216998","JumpKingSaveStates.dll"));
        var assembly=Assembly.LoadFrom(workshop);
        var entry=assembly.GetType("JumpKingSaveStates.JumpKingSaveStates",true);
        var padType=assembly.GetType("JumpKingSaveStates.Models.CustomPadInstance",true);
        var pad=FormatterServices.GetUninitializedObject(padType);
        var stateType=assembly.GetType("JumpKingSaveStates.Models.CustomPadState",true);
        var preferences=Activator.CreateInstance(assembly.GetType("JumpKingSaveStates.Preferences",true));
        entry.GetProperty("Preferences",F).SetValue(null,preferences,null);
        entry.GetProperty("PadInstance",F).SetValue(null,pad,null);
        var behaviour=(JumpKing.API.IBodyCompBehaviour)Activator.CreateInstance(assembly.GetType("JumpKingSaveStates.SavestateBehaviour",true),true);
        var list=(LinkedList<JumpKing.API.IBodyCompBehaviour>)typeof(BodyComp).GetField("m_behaviours",F).GetValue(player.m_body);
        Game1.instance.contentManager.audio.menu.MenuFail=world.Finish;
        Game1.instance.contentManager.audio.menu.Select=world.Finish;
        list.AddLast(behaviour);
        var focusField=typeof(StereoMadness.Controller).GetField("focused",F);
        object previousFocus=focusField.GetValue(controller);
        using(var inputFixture=new OwnedPatches("stereo.savestates.fixture")) {
        inputFixture.Add(typeof(Game).GetProperty("IsActive").GetGetMethod(),prefix:typeof(NativeTests).GetMethod("InactiveFixture",F));
        inputFixture.Add(typeof(Microsoft.Xna.Framework.Input.Mouse).GetMethod("GetState",Type.EmptyTypes),prefix:typeof(NativeTests).GetMethod("FixtureMouse",F));
        try {
            var save=Activator.CreateInstance(stateType); stateType.GetField("savePos").SetValue(save,true);
            padType.GetField("current_state",F).SetValue(pad,save);
            Vector2 position=player.m_body.Position;
            typeof(StereoMadness.Controller).GetMethod("Update",F).Invoke(controller,new object[]{1f/60});
            var values=(Array)preferences.GetType().GetProperty("Saves").GetValue(preferences,null);
            var last=values.GetValue(values.Length-1);
            Check((float)last.GetType().GetField("X").GetValue(last)==position.X && (float)last.GetType().GetField("Y").GetValue(last)==position.Y,
                "Installed JumpKingSaveStates saves level coordinates while native physics is suspended");
            for(int i=0;i<3;i++) controller.Advance(false);
            var load=Activator.CreateInstance(stateType); stateType.GetField("loadPos").SetValue(load,true);
            padType.GetField("last_state",F).SetValue(pad,save); padType.GetField("current_state",F).SetValue(pad,load);
            typeof(StereoMadness.Controller).GetMethod("Update",F).Invoke(controller,new object[]{1f/60});
            Check(Math.Abs(player.m_body.Position.X-position.X-(float)(Simulation.Speed*.9*.5))<.02 && controller.Model.Ship,
                "Installed JumpKingSaveStates load resumes the saved course position and ship mode");
        } finally { list.Remove(behaviour); world.Finish.Stop(); focusField.SetValue(controller,previousFocus); }
        }
    }
}
