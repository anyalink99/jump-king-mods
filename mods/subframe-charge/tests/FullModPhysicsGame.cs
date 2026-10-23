using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Mods;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Steamworks;

// run only in a staged game tree: real loading, real mods, separate saves
internal static partial class FullModPhysicsGame
{
    private static readonly Harmony hooks = new Harmony("test.full-mod-physics");
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static readonly List<string> evidence = new List<string>();
    private static Type physics;
    private static int updates, steps, fractional, labels, phase, sceneFrame;
    private static int platformTicks, conveyorQueries;
    private static int releaseFrames;
    private static float releasePercent;
    private static IBlock[] originalBlocks;
    private static float contactWind;
    private static Vector2 before;
    private static bool finished, failed;
    private static string root;
    private static object movingPlatform;
    private static object motionObserver;
    private static int sandJumps;
    private static bool sandDirections;
    private static bool Right { get { return phase == 1 && sceneFrame >= 15 && sceneFrame < 70 || phase == 4 && sceneFrame >= 15 || phase == 5 && sceneFrame >= 15 && sceneFrame < 70 || phase==8 && sceneFrame>=40 && sceneFrame<110 || SandTest && phase%2==0 && sceneFrame>=40 && sceneFrame<70; } }
    private static bool SandTest { get { return phase >= 10 && phase <= 15; } }
    private static bool Left { get { return SandTest && phase%2==1 && sceneFrame>=40 && sceneFrame<70; } }
    private static bool Jump { get { return phase==1 && sceneFrame>=75 && sceneFrame<90 || SandTest && sceneFrame>=40 && sceneFrame<55; } }
    private static Type Find(string name) { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null); }
    private static void Record(string value) { evidence.Add(value); Console.WriteLine(value); File.WriteAllLines(Path.Combine(root, "physics-game.txt"), evidence); }
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    [STAThread] private static int Main()
    {
        root = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            Check(Directory.Exists(Path.Combine(root, "WorkshopMods")), "A staged WorkshopMods directory is required");
            Environment.SetEnvironmentVariable("SteamAppId", "1061090");
            Environment.SetEnvironmentVariable("SteamGameId", "1061090");
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args) {
                string name = new AssemblyName(args.Name).Name;
                if (name.EndsWith(".resources") || name.EndsWith(".Module")) return null;
                var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
                if (loaded != null) return loaded;
                string[] files = Directory.GetFiles(Path.Combine(root, "WorkshopMods"), name + ".dll", SearchOption.AllDirectories);
                return files.Length == 1 ? Assembly.LoadFrom(files[0]) : null;
            };
            Check(SteamAPI.Init(), "Steam initialization failed");
            // the staged game mustn't make noise or leave a blocking crash dialog
            Patch(typeof(Microsoft.Xna.Framework.Audio.SoundEffect), "set_MasterVolume", "Mute", null);
            Patch(typeof(Microsoft.Xna.Framework.Media.MediaPlayer), "set_Volume", "Mute", null);
            Microsoft.Xna.Framework.Audio.SoundEffect.MasterVolume = 0;
            Microsoft.Xna.Framework.Media.MediaPlayer.Volume = 0;
            hooks.Patch(AccessTools.Method(typeof(System.Windows.Forms.MessageBox), "Show", new[] {
                typeof(string), typeof(string), typeof(System.Windows.Forms.MessageBoxButtons), typeof(System.Windows.Forms.MessageBoxIcon) }),
                prefix: new HarmonyMethod(typeof(FullModPhysicsGame), "CrashDialog"));
            Patch(typeof(JumpKing.Program), "InitSteam", "SteamReady", null);
            Patch(typeof(ModLoader), "LoadMods", "LoadMods", null);
            foreach (string method in new[] { "GetModAssemblies", "LoadReferencedAssemblies" })
                hooks.Patch(AccessTools.Method(typeof(ModLoader), method), transpiler: new HarmonyMethod(typeof(FullModPhysicsGame), "SharedAssemblies"));
            Patch(typeof(SteamUGC), "GetItemInstallInfo", null, "WorkshopPath");
            foreach (var method in typeof(SteamUserStats).GetMethods().Where(m => m.ReturnType == typeof(bool) &&
                (m.Name.StartsWith("Set") || m.Name.StartsWith("Clear") || m.Name == "StoreStats" || m.Name == "ResetAllStats" || m.Name == "IndicateAchievementProgress" || m.Name == "UpdateAvgRateStat")))
                hooks.Patch(method, prefix: new HarmonyMethod(typeof(FullModPhysicsGame), "NoSteamWrite"));
            Patch(typeof(Game), "get_IsActive", "Active", null);
            Patch(typeof(Game1), "Update", null, "Updated");
            Patch(typeof(PadInstance), "GetPadState", "Pad", null);
            hooks.Patch(AccessTools.Method(typeof(Microsoft.Xna.Framework.Input.Keyboard), "GetState", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(FullModPhysicsGame), "EmptyKeyboard"));
            hooks.Patch(AccessTools.Method(typeof(Microsoft.Xna.Framework.Input.Mouse), "GetState", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(FullModPhysicsGame), "EmptyMouse"));
            Patch(typeof(JumpKing.Util.TextHelper), "DrawString", null, "Text");
            var prefs = typeof(Game1).Assembly.GetType("JumpKing.PlayerPreferences.Persocom.GraphicsPreferencesRuntime", true);
            Patch(prefs, "InitAllPrefs", "Windowed", null); Patch(prefs, "OnSetPrefs", "Windowed", null);
            JumpKing.Program.Run();
            Check(finished && !failed, "Game exited before passing the combined scenario");
            return 0;
        }
        catch (Exception error) { Record("FAIL " + error); return 1; }
        finally { PlayerEntity.OnJumpCall -= SandJump; SteamAPI.Shutdown(); }
    }
    private static void Patch(Type type, string method, string prefix, string postfix)
    { hooks.Patch(AccessTools.Method(type, method), prefix: prefix == null ? null : new HarmonyMethod(typeof(FullModPhysicsGame), prefix), postfix: postfix == null ? null : new HarmonyMethod(typeof(FullModPhysicsGame), postfix)); }
    private static bool SteamReady(ref bool __result) { PadInstance.RegisterSteamCallback(); __result = true; return false; }
    private static bool NoSteamWrite(ref bool __result) { __result = false; return false; }
    private static void Mute(ref float __0) { __0 = 0; }
    private static bool CrashDialog(string __0, ref System.Windows.Forms.DialogResult __result)
    { failed=true; Record("FAIL native game: " + __0); __result=System.Windows.Forms.DialogResult.OK; return false; }
    private static bool EmptyKeyboard(ref Microsoft.Xna.Framework.Input.KeyboardState __result) { __result=default(Microsoft.Xna.Framework.Input.KeyboardState); return false; }
    private static bool EmptyMouse(ref Microsoft.Xna.Framework.Input.MouseState __result) { __result=default(Microsoft.Xna.Framework.Input.MouseState); return false; }
    private static bool Active(ref bool __result) { __result = true; return false; }
    private static void Windowed(ref JumpKing.PlayerPreferences.Persocom.GraphicPrefs p_settings)
    { p_settings.screen_mode = JumpKing.PlayerPreferences.Persocom.ScreenMode.Windowed; p_settings.size_mode = JumpKing.PlayerPreferences.Persocom.SizeMode.x1; }
    private static bool LoadMods(string modDirectory, ref List<ModAssembly> loadedModAssemblies)
    {
        if (Path.GetFullPath(modDirectory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        loadedModAssemblies = new List<ModAssembly>(); return false;
    }
    private static void WorkshopPath(PublishedFileId_t nPublishedFileID, ref string pchFolder, bool __result)
    { string path = Path.Combine(root, "WorkshopMods", nPublishedFileID.m_PublishedFileId.ToString()); if (__result && Directory.Exists(path)) pchFolder = path; }
    private static Assembly Shared(string path)
    {
        string identity = AssemblyName.GetAssemblyName(path).FullName;
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == identity);
        if (loaded != null) return loaded;
        var assembly = Assembly.LoadFrom(path);
        if (assembly.GetName().Name == "JKRuntime")
            hooks.Patch(AccessTools.Constructor(assembly.GetType("JKRuntime.Input.KeyboardSource", true), new[] { typeof(Func<int, short>), typeof(Func<IntPtr>), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(FullModPhysicsGame), "Keyboard"));
        return assembly;
    }
    private static void Keyboard(ref Func<int, short> read, ref Func<IntPtr> focus, bool start)
    {
        if (!start) return;
        read = key => (short)(((key == 39 || key == 68) && Right || (key == 37 || key == 65) && Left || key == 32 && Jump) ? -32768 : 0);
        focus = () => Game1.instance == null ? IntPtr.Zero : Game1.instance.Window.Handle;
    }
    private static IEnumerable<CodeInstruction> SharedAssemblies(IEnumerable<CodeInstruction> source)
    {
        var original = typeof(Assembly).GetMethod("LoadFrom", new[] { typeof(string) });
        foreach (var instruction in source) { if (instruction.Calls(original)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(FullModPhysicsGame), "Shared"); } yield return instruction; }
    }
    private static bool Pad(ref PadState __result)
    {
        bool playing = JumpGame.instance != null && JumpGame.instance.IsPlaying();
        __result = new PadState { confirm = !playing && (clock.ElapsedMilliseconds / 300) % 2 == 0,
            right = playing && Right, left = playing && Left, jump = playing && Jump };
        return false;
    }
    private static void Text(string p_text) { if (p_text.StartsWith("SFC:")) labels++; }
    private static void BeforeStep() { if (GameLoop.m_player != null) before = GameLoop.m_player.m_body.Position; }
    private static void AfterStep()
    {
        if (GameLoop.m_player == null || !(bool)AccessTools.Property(physics, "Active").GetValue(null, null)) return;
        steps++;
        float x = GameLoop.m_player.m_body.Position.X;
        if (x != before.X && Math.Abs(x * 2 - Math.Round(x * 2)) > .001) fractional++;
    }
    private static void PlatformTick() { platformTicks++; }
    private static void Updated(Game1 __instance)
    {
        if (finished) return;
        try
        {
            if (clock.Elapsed.TotalSeconds > 120) throw new Exception("Combined game timed out");
            if (physics == null)
            {
                physics = Find("SubframeCharge.SubframePhysics");
                if (physics == null) return;
                Patch(physics, "Advance", "BeforeStep", "AfterStep");
                Patch(Find("SubframeCharge.PhysicsCallbacks"), "Body", "BeforeBody", "AfterBody");
                Patch(Find("MorphBallMod.MorphInput"), "IsMorphHeld", "Morph", null);
                var platform = Find("MovingPlatformMod.MovingPlatformUpdater");
                Check(platform != null, "Installed MovingPlatform updater wasn't loaded");
                Patch(platform, "ExecuteBehaviour", "PlatformTick", null);
                var belt = Find("ConveyorBlockMod.BlocksBehaviour.ConveyorBlockBehaviour");
                Check(belt != null, "Installed Conveyor wasn't loaded");
                // count original execution, not prefixes skipped by the cadence adapter
                Patch(Find("SubframeCharge.PhysicsCallbacks"), "BeforeConveyor", null, "CountConveyorCall");
                Record("Loaded " + ModLoader.Instance.LoadedMods.Count + " native mods");
                File.WriteAllLines(Path.Combine(root, "loaded-mods.txt"), ModLoader.Instance.LoadedMods.Select(m => m.ModName + " | " + m.Assembly.Location));
            }
            var player = GameLoop.m_player;
            if (player == null || !player.IsAlive || JumpGame.instance == null || !JumpGame.instance.IsPlaying()) return;
            updates++; sceneFrame++;
            if(phase==4 && sceneFrame%30==0 && motionObserver!=null)
            {
                var sample=AccessTools.Method(motionObserver.GetType(),"Read").Invoke(motionObserver,null);
                Record("MOTION "+sample.GetType().GetProperty("Kind").GetValue(sample,null)+" reason="+sample.GetType().GetProperty("Reason").GetValue(sample,null)+" source="+sample.GetType().GetProperty("Source").GetValue(sample,null));
            }
            if (phase == 1 && sceneFrame % 30 == 0) Record("sample " + sceneFrame + " position=" + player.m_body.Position + " velocity=" + player.m_body.Velocity + " input=" + player.GetComponent<InputComponent>().GetState().right + " nativeControls=" + AccessTools.Method(physics, "NativeControls").Invoke(null, null));
            if (phase == 0)
            {
                originalBlocks=(IBlock[])AccessTools.Field(typeof(LevelScreen),"m_hitboxes").GetValue(((LevelScreen[])AccessTools.Field(typeof(LevelManager),"m_screens").GetValue(null))[0]);
                // a previous failed run may have saved ball form in this staging tree
                SetPreference("MorphBallMod.SettingsStore","EnableMorphBall",false);
                AccessTools.Method(Find("MorphBallMod.MorphStateStore"),"Save").Invoke(null,new object[]{false});
                SetPreference("MegaGameplayExpansion.Settings","NoWalkOff",false);
                SetScene(player,new BoxBlock(new Rectangle(0,300,480,60)));
                phase=-1; sceneFrame=0;
            }
            else if(phase==-1 && sceneFrame>=20)
            {
                Check((bool)AccessTools.Property(physics, "Active").GetValue(null, null), "Physics refused with installed mods: " + AccessTools.Property(physics, "Reason").GetValue(null, null));
                Record("ACTIVE body callbacks: " + string.Join(", ", player.m_body.GetBehaviourList().Select(b => b.GetType().FullName)));
                if(Environment.GetEnvironmentVariable("JK_PHYSICS_EDGES_ONLY")=="1") {
                    VerifyFractionalEdges(player); Record("PASS installed-set fractional edge matrix");
                    finished=true; __instance.Exit(); return;
                }
                VerifyStatefulGravity();
                VerifyChargeCounter(player);
                VerifyContacts(player);
                var jump = Find("JumpKingLastJumpValue.JumpKingLastJumpValue");
                Check(jump != null, "Jump% wasn't loaded");
                var preferences = jump.GetProperty("Preferences").GetValue(null, null);
                preferences.GetType().GetProperty("IsEnabled").SetValue(preferences, false, null);
                Check(!(bool)AccessTools.Method(physics,"NativeControls").Invoke(null,null),"Baseline retained an alternate controller");
                SetScene(player, new BoxBlock(new Rectangle(0, 300, 480, 60)));
                updates=steps=fractional=labels=platformTicks=0;
                phase = 1; sceneFrame = 0;
            }
            else if (phase == 1 && sceneFrame >= 150)
            {
                Record("WALK/JUMP updates=" + updates + " substeps=" + steps + " fractional=" + fractional + " standaloneSFC=" + labels + " platformTicks=" + platformTicks);
                Check(fractional > 10, "Real player never acquired fractional horizontal coordinates");
                Check(labels > 10, "Show SFC disappeared with installed Jump% disabled");
                Check(platformTicks <= updates + 3 && platformTicks >= updates - 6, "Platform updater cadence changed");
                var type = Find("ConveyorBlockMod.Blocks.ConveyorBlock");
                var block = (IBlock)Activator.CreateInstance(type, new object[] { new Rectangle(0, 300, 480, 60), (byte)2, (byte)100 });
                SetScene(player, block); phase = 2; sceneFrame = 0; conveyorQueries = 0;
            }
            else if (phase == 2 && sceneFrame >= 90)
            {
                Record("CONVEYOR x=" + player.m_body.Position.X + " queries=" + conveyorQueries + " native=" + sceneFrame);
                Check(player.m_body.Position.X > 160, "Installed conveyor didn't carry the real player");
                Check(conveyorQueries >= sceneFrame - 3 && conveyorQueries <= sceneFrame + 3, "Conveyor contact history advanced at substep cadence");
                var conveyor = Find("ConveyorBlockMod.Blocks.ConveyorBlock");
                SetBlocks((IBlock)Activator.CreateInstance(conveyor, new object[] { new Rectangle(0,300,190,60), (byte)2, (byte)100 }));
                phase=7; sceneFrame=0;
            }
            else if(phase==7 && sceneFrame>=45)
            {
                Record("BELT EXIT position="+player.m_body.Position+" velocity="+player.m_body.Velocity);
                Check(player.m_body.Position.X>200 && player.m_body.Velocity.X>0,"Conveyor exit lost its momentum");
                var type = Find("JumpKingPlus.Blocks.WarpBlock");
                var block = (IBlock)Activator.CreateInstance(type, new object[] { new Rectangle(80, 250, 100, 50), 40, 25 });
                SetScene(player, new BoxBlock(new Rectangle(0, 300, 480, 60)), block); phase = 3; sceneFrame = 0;
            }
            else if (phase == 3 && sceneFrame >= 20)
            {
                Check(player.m_body.Position.X >= 300, "Installed WarpBlock failed to teleport");
                Check((bool)AccessTools.Property(physics, "Active").GetValue(null, null), "Custom block disabled physics");
                SetPreference("MegaGameplayExpansion.Settings","NoWalkOff",true);
                var motionScope=AccessTools.Field(Find("MegaGameplayExpansion.ModEntry"),"MotionScope").GetValue(null);
                motionObserver=AccessTools.Method(motionScope.GetType(),"Observe").Invoke(motionScope,new object[]{player.m_body});
                motionObserver.GetType().GetProperty("Enabled").SetValue(motionObserver,true,null);
                SetScene(player,new BoxBlock(new Rectangle(0,300,180,60)));
                phase=4; sceneFrame=0;
            }
            else if(phase==4 && sceneFrame>=130)
            {
                Record("NO WALK OFF position="+player.m_body.Position+" grounded="+player.m_body.IsOnGround);
                Check(player.m_body.IsOnGround && player.m_body.Position.X>170 && player.m_body.Position.X<190,"No Walk Off failed with fractional movement and loaded custom handlers");
                phase=5; sceneFrame=0;
            }
            else if(phase==5 && sceneFrame>=90)
            {
                Record("REPRESS position="+player.m_body.Position+" input="+player.GetComponent<InputComponent>().GetState().right);
                Check(player.m_body.Position.X>200 && player.m_body.Position.Y>280,"No Walk Off didn't release after repressing Right");
                SetPreference("MegaGameplayExpansion.Settings","NoWalkOff",false);
                SetScene(player);
                var axis=Enum.Parse(Find("MovingPlatformMod.MovingAxis"),"Vertical");
                Activator.CreateInstance(Find("MovingPlatformMod.MovingRangeBlock"),new object[]{new Rectangle(80,180,80,120),0,axis});
                movingPlatform=Activator.CreateInstance(Find("MovingPlatformMod.MovingPlatformEntity"),new object[]{new Rectangle(80,300,80,8),Enum.Parse(Find("MovingPlatformMod.MovingPlatformTextureType"),"None"),0,axis});
                SetBlocks((IBlock)Activator.CreateInstance(Find("MovingPlatformMod.MovingBlock"),new[]{movingPlatform}));
                AccessTools.Method(Find("MovingPlatformMod.MovingPlatformGroup"),"RebuildGroups").Invoke(null,null);
                phase=6; sceneFrame=0;
            }
            else if(phase==6 && sceneFrame>=60)
            {
                var hitbox=(Rectangle)AccessTools.Field(movingPlatform.GetType(),"Hitbox").GetValue(movingPlatform);
                Record("PLATFORM y="+hitbox.Y+" player="+player.m_body.Position+" grounded="+player.m_body.IsOnGround);
                Check(hitbox.Y>=239 && hitbox.Y<=242,"Moving platform speed changed");
                Check(player.m_body.IsOnGround && Math.Abs(player.m_body.GetHitbox().Bottom-hitbox.Top)<=1,"Moving platform lost its rider");
                ((System.Collections.IList)AccessTools.Field(movingPlatform.GetType(),"Instances").GetValue(null)).Clear();
                AccessTools.Method(Find("MovingPlatformMod.MovingPlatformGroup"),"ClearGroups").Invoke(null,null);
                SetPreference("MorphBallMod.SettingsStore","EnableMorphBall",true);
                SetScene(player,new BoxBlock(new Rectangle(0,300,480,60)),new BoxBlock(new Rectangle(230,150,20,150)));
                phase=8; sceneFrame=0;
            }
            else if(phase==8 && sceneFrame>=130)
            {
                bool morphed=(bool)AccessTools.Property(Find("MorphBallMod.BallFormState"),"Morphed").GetValue(null,null);
                bool attached=(bool)AccessTools.Property(Find("MorphBallMod.BallFormState"),"Attached").GetValue(null,null);
                Record("BALL morphed="+morphed+" attached="+attached+" position="+player.m_body.Position+" hitbox="+player.m_body.GetHitbox());
                Check(morphed && (bool)AccessTools.Method(physics,"NativeControls").Invoke(null,null),"Ball controller didn't retain native input/tree ownership");
                Check(player.m_body.Position.X>140 && player.m_body.GetHitbox().Right<=230 && (player.m_body.IsOnGround || attached),"Ball movement or wall/floor attachment failed with fractional body steps");
                phase=9; sceneFrame=0;
            }
            else if(phase==9 && sceneFrame>=60)
            {
                bool morphed=(bool)AccessTools.Property(Find("MorphBallMod.BallFormState"),"Morphed").GetValue(null,null);
                Check(!morphed && !(bool)AccessTools.Method(physics,"NativeControls").Invoke(null,null),"Unfolding didn't return the native tree and its observers to substeps");
                Record("UNFOLD restored fractional controls; position="+player.m_body.Position);
                if(motionObserver is IDisposable) ((IDisposable)motionObserver).Dispose();
                SetPreference("MegaGameplayExpansion.Settings", "NoWalkOff", false);
                PlayerEntity.OnJumpCall += SandJump;
                BeginSand(player, 10);
            }
            else if (SandTest)
            {
                if (sceneFrame == 25) {
                    Check((bool)AccessTools.Property(physics, "Active").GetValue(null, null) == (phase >= 14), "Mode switch didn't complete before sand input");
                    SetScene(player, new SandBlock(new Rectangle(0, 200, 480, 150)), new BoxBlock(new Rectangle(0, 350, 480, 40)));
                }
                if (sceneFrame == 85) {
                    float delta = player.m_body.Position.X - 100;
                    Check(sandJumps > 0 && sandDirections, "Native sand jump didn't commit the requested direction");
                    Check(phase%2==0 ? delta>5 : delta < -5, "Sand side jump lost direction in mode=" + (phase-10)/2 + " side=" + phase%2 + " delta=" + delta);
                    Record("SAND mode=" + (phase-10)/2 + " side=" + (phase%2==0 ? "right" : "left") + " jumps=" + sandJumps + " delta=" + delta);
                    if (phase < 15) BeginSand(player, phase+1);
                    else {
                        VerifyFractionalEdges(player);
                        Record("PASS loaded mod set: fractional movement, conveyor ride/exit, warp, No Walk Off/repress, moving platform ride, Ball controller/collisions, disabled Jump% HUD, sand side jumps in all modes and fractional edge matrix");
                        finished = true; __instance.Exit();
                    }
                }
            }
            if (phase == 1 && sceneFrame >= 80 && sceneFrame <= 130)
            {
                var calc=Find("JumpKingLastJumpValue.Models.JumpChargeCalc");
                int frames=(int)AccessTools.Property(calc,"JumpFrames").GetValue(null,null);
                float percent=(float)AccessTools.Property(calc,"JumpPercentage").GetValue(null,null);
                if(sceneFrame<90) Check(frames<=sceneFrame-75+2,"Jump% counts physics steps as native frames: "+frames);
                if(sceneFrame==95) { releaseFrames=frames; releasePercent=percent; Record("JUMP% released frames="+frames+" percentage="+percent); }
                if(sceneFrame>95) Check(frames==releaseFrames && percent==releasePercent,"Jump% changed after release without another jump");
            }
        }
        catch (Exception error) { failed = finished = true; Record("FAIL " + error); __instance.Exit(); }
    }
    private static void CountConveyorCall(bool __result) { if(__result) conveyorQueries++; }
    private static bool Morph(ref bool __result) { __result=(phase==8 || phase==9) && sceneFrame>=15 && sceneFrame<20; return false; }
    private static void BeforeBody(JumpKing.BodyCompBehaviours.BehaviourContext context, out Vector2 __state) { __state = context.BodyComp.Position; }
    private static void AfterBody(JumpKing.API.IBodyCompBehaviour callback, JumpKing.BodyCompBehaviours.BehaviourContext context, Vector2 __state)
    { if (Vector2.Distance(__state, context.BodyComp.Position) > 10) Record("move " + callback.GetType().Name + " " + __state + " -> " + context.BodyComp.Position + " scene=" + phase); }
    private static void SetScene(PlayerEntity player, params IBlock[] blocks)
    {
        SetBlocks(blocks);
        player.m_body.Position = new Vector2(100, 274); player.m_body.Velocity = Vector2.Zero;
        AccessTools.Field(typeof(BodyComp), "_is_on_ground").SetValue(player.m_body, true);
        Camera.UpdateCamera(player.m_body.GetHitbox().Center);
    }
    private static void BeginSand(PlayerEntity player, int next)
    {
        phase = next; sceneFrame = sandJumps = 0; sandDirections = true;
        SetScene(player, new BoxBlock(new Rectangle(0, 300, 480, 60)));
        object setting = AccessTools.Field(Find("SubframeCharge.Options"), "Mode").GetValue(null);
        setting.GetType().GetMethod("Set").Invoke(setting, new[] { Enum.ToObject(Find("SubframeCharge.SubframeMode"), (next-10)/2) });
    }
    private static void SandJump()
    {
        if (!SandTest) return;
        sandJumps++;
        sandDirections &= Math.Sign(GameLoop.m_player.m_body.Velocity.X) == (phase%2==0 ? 1 : -1);
    }
    private static void SetPreference(string typeName,string name,bool value)
    {
        var type=Find(typeName);
        if(typeName=="MorphBallMod.SettingsStore" && name=="EnableMorphBall")
        {
            AccessTools.Method(type,"SetEnabled").Invoke(null,new object[]{value});
            AccessTools.Method(Find("MorphBallMod.MorphBallInstaller"),"Apply").Invoke(null,null);
            return;
        }
        var current=AccessTools.Property(type,"Current");
        var settings=current!=null?current.GetValue(null,null):AccessTools.Field(type,"Current").GetValue(null);
        var property=AccessTools.Property(settings.GetType(),name);
        if(property!=null)property.SetValue(settings,value,null);else AccessTools.Field(settings.GetType(),name).SetValue(settings,value);
        if(typeName=="MegaGameplayExpansion.Settings" && name=="NoWalkOff" && value)
            AccessTools.Method(Find("MegaGameplayExpansion.ModEntry"),"EnsureMotion").Invoke(null,null);
    }
    private static void VerifyStatefulGravity()
    {
        var dynamics=Find("SubframeCharge.PhysicsDynamics");
        foreach(string name in new[]{"JumpKing_Expansion_Blocks.Behaviours.SpecialHighGravity","JumpKing_GhostOfTheImmortalBabeBlocks.Behaviours.SpecialRainGravity"})
        {
            var nativeBody=new BodyComp(Vector2.Zero,18,26){Velocity=new Vector2(0,2)};
            var fastBody=new BodyComp(Vector2.Zero,18,26){Velocity=new Vector2(0,2)};
            var type=Find(name); Check(type!=null,"Missing installed gravity fixture: "+name);
            var regular=(JumpKing.API.IBlockBehaviour)Activator.CreateInstance(type);
            var smaller=(JumpKing.API.IBlockBehaviour)Activator.CreateInstance(type);
            regular.IsPlayerOnBlock=smaller.IsPlayerOnBlock=true;
            regular.ModifyYVelocity(2,new JumpKing.BodyCompBehaviours.BehaviourContext(nativeBody));
            AccessTools.Method(dynamics,"Reset").Invoke(null,null);
            for(int i=0;i<4;i++)
            {
                AccessTools.Method(dynamics,"Begin").Invoke(null,new object[]{fastBody,1f/240});
                try { smaller.ModifyYVelocity(2,new JumpKing.BodyCompBehaviours.BehaviourContext(fastBody)); }
                finally { AccessTools.Method(dynamics,"End").Invoke(null,null); }
            }
            Check(Math.Abs(nativeBody.Velocity.Y-fastBody.Velocity.Y)<.00001,"Stateful gravity accelerated: "+name);
            Record("GRAVITY "+name+" native="+nativeBody.Velocity.Y+" fractional="+fastBody.Velocity.Y);
        }
        AccessTools.Method(dynamics,"Reset").Invoke(null,null);
        var reverse=AccessTools.Field(Find("UpsideDownCore.Models.Manager"),"isReverseGravity");
        bool previous=(bool)reverse.GetValue(null);
        try
        {
            reverse.SetValue(null,true);
            var fast=new BodyComp(Vector2.Zero,18,26){Velocity=new Vector2(0,-PlayerValues.MAX_FALL+.01f)};
            var gravity=new JumpKing.BodyCompBehaviours.ApplyGravityBehaviour(new LinkedList<JumpKing.API.IBlockBehaviour>());
            AccessTools.Method(dynamics,"Begin").Invoke(null,new object[]{fast,1f/240});
            try { gravity.ExecuteBehaviour(new JumpKing.BodyCompBehaviours.BehaviourContext(fast)); }
            finally { AccessTools.Method(dynamics,"End").Invoke(null,null); }
            Check(fast.Velocity.Y==-PlayerValues.MAX_FALL,"UpsideDownCore's reverse gravity or negative cap was replaced");
            var movement=new JumpKing.BodyCompBehaviours.UpdateYPositionFromVelocityBehaviour(new LinkedList<JumpKing.API.IBlockBehaviour>());
            for(int i=0;i<4;i++)
            {
                AccessTools.Method(dynamics,"Begin").Invoke(null,new object[]{fast,1f/240});
                try { movement.ExecuteBehaviour(new JumpKing.BodyCompBehaviours.BehaviourContext(fast)); }
                finally { AccessTools.Method(dynamics,"End").Invoke(null,null); }
            }
            Check(Math.Abs(fast.Position.Y+PlayerValues.MAX_FALL)<.00001,"Reverse terminal movement retained an acceleration correction");
            Record("REVERSE GRAVITY negative cap="+fast.Velocity.Y);
        }
        finally { reverse.SetValue(null,previous); AccessTools.Method(dynamics,"Reset").Invoke(null,null); }
    }
    private static void VerifyChargeCounter(PlayerEntity player)
    {
        var calc=Find("JumpKingLastJumpValue.Models.JumpChargeCalc");
        var frames=AccessTools.Property(calc,"JumpFrames");
        var percent=AccessTools.Property(calc,"JumpPercentage");
        var dynamics=Find("SubframeCharge.PhysicsDynamics");
        var input=player.GetComponent<InputComponent>();
        var getState=AccessTools.Method(typeof(InputComponent),"GetState");
        hooks.Patch(getState,postfix:new HarmonyMethod(typeof(FullModPhysicsGame),"ChargeHeld") { priority=Priority.Last });
        try
        {
        foreach(int rate in new[]{60,240,1000})
        {
            var state=new JumpState(player);
            AccessTools.Field(typeof(InputComponent),"_can_jump").SetValue(input,true);
            int changes=0; float previous=-1;
            for(int tick=0;tick<rate/2;tick++)
            {
                AccessTools.Method(dynamics,"Begin").Invoke(null,new object[]{player.m_body,1f/rate});
                try { Check(state.Run(new BehaviorTree.TickData(1f/rate,tick))==BehaviorTree.BTresult.Running,"Counter fixture didn't charge"); }
                finally { AccessTools.Method(dynamics,"End").Invoke(null,null); }
                float current=(float)percent.GetValue(null,null);
                if(current!=previous) changes++;
                previous=current;
            }
            Check((int)frames.GetValue(null,null)==29,"Jump% counts substeps: rate="+rate+" frames="+frames.GetValue(null,null));
            Check(changes<=31,"Jump% live value updated at physics cadence: "+changes);
            Record("JUMP% rate="+rate+" frames="+frames.GetValue(null,null)+" live changes="+changes);
        }
        }
        finally { hooks.Unpatch(getState,HarmonyPatchType.Postfix,hooks.Id); AccessTools.Method(dynamics,"Reset").Invoke(null,null); }
    }
    private static void ChargeHeld(ref InputComponent.State __result) { __result=new InputComponent.State { jump=true }; }
    private static bool ContactWind(ref float __result) { __result=contactWind; return false; }
    private static void VerifyContacts(PlayerEntity player)
    {
        var dynamics=Find("SubframeCharge.PhysicsDynamics");
        Action reset=()=>AccessTools.Method(dynamics,"Reset").Invoke(null,null);
        Action step=()=> {
            AccessTools.Method(dynamics,"Begin").Invoke(null,new object[]{player.m_body,1f/240});
            try { AccessTools.Method(typeof(BodyComp),"UpdateInternal").Invoke(player.m_body,new object[]{1f/240}); }
            finally { AccessTools.Method(dynamics,"End").Invoke(null,null); }
        };
        var grounded=AccessTools.Field(typeof(BodyComp),"_is_on_ground");
        SetBlocks(originalBlocks);
        int originalContacts=0;
        foreach(var block in originalBlocks.Where(b=>b.GetType()==typeof(BoxBlock)))
        {
            var rect=block.GetRect();
            if(rect.Bottom>300 || rect.Center.X<10 || rect.Center.X>460) continue;
            player.m_body.Position=new Vector2(rect.Center.X-9,rect.Bottom);
            Rectangle overlap; AggregateCollisionInfo info;
            if(LevelManager.CheckCollision(player.m_body.GetHitbox(),out overlap,out info)) continue;
            player.m_body.Velocity=new Vector2(0,PlayerValues.GRAVITY*.25f); grounded.SetValue(player.m_body,false);
            reset(); step(); originalContacts++;
            Check(player.m_body.Position.Y>=rect.Bottom,"Original first-screen ceiling crossed: "+rect+" -> "+player.m_body.Position);
        }
        Check(originalContacts>0,"No original first-screen ceiling was exercised");
        foreach(float speed in new[]{0,PlayerValues.GRAVITY*.25f,-5f})
        {
            SetBlocks(new BoxBlock(new Rectangle(0,90,480,11)));
            player.m_body.Position=new Vector2(150,101); player.m_body.Velocity=new Vector2(0,speed);
            grounded.SetValue(player.m_body,false); reset();
            for(int i=0;i<16;i++) { step(); Check(player.m_body.Position.Y>=101,"Combined mods crossed the ceiling: "+player.m_body.Position); }
        }
        var screen=((LevelScreen[])AccessTools.Field(typeof(LevelManager),"m_screens").GetValue(null))[0];
        var windEnabled=AccessTools.Field(typeof(LevelScreen),"m_wind_enabled");
        bool wasWind=(bool)windEnabled.GetValue(screen);
        var windType=typeof(BodyComp).Assembly.GetTypes().Single(t=>t.Name=="WindManager");
        var windMethod=windType.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name.EndsWith("GetCurrentVelocity",StringComparison.Ordinal));
        hooks.Patch(windMethod,prefix:new HarmonyMethod(typeof(FullModPhysicsGame),"ContactWind"));
        try
        {
            windEnabled.SetValue(screen,true);
            foreach(bool ice in new[]{false,true})
            foreach(int sign in new[]{-1,1})
            {
                SetBlocks(new BoxBlock(new Rectangle(200,0,8,360)),ice?(IBlock)new IceBlock(new Rectangle(0,300,480,60)):new BoxBlock(new Rectangle(0,300,480,60)));
                player.m_body.Position=new Vector2(sign>0?182.9999f:208.0001f,274);
                player.m_body.Velocity=new Vector2(-sign*.01f,PlayerValues.GRAVITY);
                grounded.SetValue(player.m_body,true); contactWind=sign*.03f; reset();
                step();
                Check(sign>0?player.m_body.Position.X<183:player.m_body.Position.X>=208,"Combined wind/ice crossed the wall: "+player.m_body.Position);
            }
        }
        finally { hooks.Unpatch(windMethod,HarmonyPatchType.All,hooks.Id); windEnabled.SetValue(screen,wasWind); reset(); }
        Record("CONTACTS original first-screen ceilings="+originalContacts+", repeated ceiling contact, both wind directions, ice and ordinary ground");
    }
    private static void SetBlocks(params IBlock[] blocks)
    {
        var screens = (LevelScreen[])AccessTools.Field(typeof(LevelManager), "m_screens").GetValue(null);
        for(int i=1;i<screens.Length;i++) AccessTools.Field(typeof(LevelScreen),"m_hitboxes").SetValue(screens[i],new IBlock[0]);
        AccessTools.Field(typeof(LevelScreen), "m_hitboxes").SetValue(screens[0], blocks);
    }
}
