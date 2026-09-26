using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static JKRuntime.Gameplay.MotionObservationScope testMotion;
        private static void Require(bool value, string text) { if (!value) throw new Exception(text); }
        private static void Run(string name,Action test)
        {
            if(testMotion!=null) ModEntry.MotionScope=testMotion;
            var elapsed=System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine("[TEST] "+name);
            test();
            Console.WriteLine("[TIME] "+name+": "+elapsed.Elapsed.TotalSeconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"s");
        }
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request) {
                string path = Path.Combine(args[0], new AssemblyName(request.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            try
            {
                string tier=args.Length>1?args[1]:"Fast";
                if(tier=="ConstructionAudit") return InstalledConstruction(args[0]);
                if(tier=="StartupAudit") return GimmickStartupAudit(args[0]);
                Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"0Harmony.dll"));
                Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"UnknownProvider.dll"));
                var motionTimer=System.Diagnostics.Stopwatch.StartNew();
                testMotion=ModEntry.MotionScope=JKRuntime.Gameplay.MotionObservation.PrepareLoaded();
                Console.WriteLine("[TIME] MGE loaded motion catalogue: "+motionTimer.Elapsed.TotalMilliseconds.ToString("F2")+"ms, types="+testMotion.Inspect().Length);
                Require(tier=="Fast" || tier=="Integration" || tier=="Full","Unknown test tier: "+tier);
                Console.WriteLine("[TIER] "+tier);
                WarpDiagnostics.OutputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics-test");
                NativeFlight.Validate();
                var factory = new MegaBlockFactory();
                Require(factory.IsSolidBlock(MapPixels.WarpSurface) && !factory.IsSolidBlock(MapPixels.WarpScreen), "Solid vs screen marker");
                Require(!factory.CanMakeBlock(Color.Black, null), "Factory must not steal vanilla colours");
                factory.GetBlock(MapPixels.WarpScreen, Rectangle.Empty, null, null, 3, 0, 0);
                Require(MegaBlockFactory.WarpScreens.Contains(3), "Screen marker");
                Run("Map variants",MapVariants);
                Run("Native trajectory parity",()=>PhysicsParity(tier!="Fast"));
                Run("Forecast diagnostic parity",FlightProfileRegression);
                Run("Non-mutating refusal",RefusalIsNonMutating);
                Run("Warp lifecycle",()=>{ using(new SpriteGameFixture()) InstalledControllerTick(); });
                Run("Native component order",()=>{ using(new SpriteGameFixture()) ResumeOrderRegression(); });
                Run("Warp buffer and landing poses",()=>{ using(new SpriteGameFixture()) WarpBufferRegression(); });
                Run("Warp zones",()=>{ using(new SpriteGameFixture()) WarpZoneActivation(); });
                Run("No Walk Off",()=>{ using(new SpriteGameFixture()) NoWalkOffRegression(); });
                Run("Air Dash",()=>{ using(new SpriteGameFixture()) AirDashRegression(); });
                Run("Dash bindings",()=>{ using(new SpriteGameFixture()) DashBindingRegression(); });
                Run("Settings commit rollback",SettingsCommitRollback);
                Run("Dash audio",()=>DashSoundRegression(tier!="Fast"));
                if (args.Length == 4) Run("Ball/Dash/Jetpack composition",()=> { using (new SpriteGameFixture()) MovementComposition(args[2], args[3]); });
                Run("Particle collisions",ParticleCollisions);
                Run("Teleport boundaries",TeleportBoundaryRegression);
                Run("Teleport parity and forecast budgets",TeleportParityAndBudgets);
                Run("Pending forecast lifecycle",()=>{ using(new SpriteGameFixture()) PendingForecastLifecycle(); });
                if(tier!="Fast")
                {
                    Run("Installed No Walk Off compatibility",()=>{ using(new SpriteGameFixture()) ForeignNoWalkOff(args[0]); });
                    Run("Installed vanilla map",()=>InstalledVanillaMap(args[0]));
                    Run("Recorded media routes",()=>InstalledMediaRoutes(args[0]));
                    Run("Wind and continuation grid",WindAndContinuation);
                }
                else Console.WriteLine("[SKIP Integration] Installed map/media routes and exhaustive wind grid");
                Run("Universal gimmick catalogue and overrides",()=>GimmickRegression(args[0]));
                if(tier!="Fast")
                {
                    Run("Gimmick native menu and graphics",()=>GimmickGraphics(args[0]));
                    Run("Custom sprite capture and Warp",()=>CustomSpriteGraphics(args[0]));
                }
                Console.WriteLine("[OK] Mega Gameplay Expansion: block ownership and native flight parity");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
            finally { if(testMotion!=null) { testMotion.Dispose(); testMotion=null; ModEntry.MotionScope=null; } }
        }
        private static LevelScreen[] Scene(IBlock[] blocks)
        { return new[] { new LevelScreen(0, blocks, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) }; }
        private static void PhysicsParity(bool exhaustive)
        {
            int comparisons=0;
            // Keep falls, minimum jumps, middle charge and maximum charge across
            // every material/direction/obstacle combination in the quick tier.
            // Integration/Full retain all 36 levels, without removing cases.
            int[] frames=exhaustive?Enumerable.Range(0,36).ToArray():new[]{0,1,17,35};
            Type save = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            // Native inventory reads are global; an empty isolated test inventory.
            FieldInfo inventory = save.GetField("inventory", Flags);
            if (inventory != null) inventory.SetValue(null, Activator.CreateInstance(inventory.FieldType));
            foreach (IBlock material in new IBlock[] { new BoxBlock(new Rectangle(0,320,480,40)),
                new IceBlock(new Rectangle(0,320,480,40)), new SnowBlock(new Rectangle(0,320,480,40)),
                new SandBlock(new Rectangle(0,320,480,40)), new WarpSurfaceBlock(new Rectangle(0,320,480,40)) })
            foreach (bool water in new[] { false, true })
            foreach (bool obstacles in new[] { false, true })
            {
            var blocks = new List<IBlock> { material };
            if (material is SandBlock) blocks.Add(new BoxBlock(new Rectangle(0,352,480,8))); // sand itself is nonblocking
            if (water) blocks.Add(new WaterBlock(new Rectangle(0,-300,480,620)));
            if (obstacles) { blocks.Add(new BoxBlock(new Rectangle(270,160,16,160))); blocks.Add(new BoxBlock(new Rectangle(100,220,160,8))); }
            var screens = Scene(blocks.ToArray());
            typeof(LevelManager).GetField("m_screens", Flags).SetValue(null, screens);
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, screens.Length);
            typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
            foreach (int frame in frames)
            foreach (int direction in new[] { -1,0,1 })
            {
                comparisons++;
                BodyComp source = new BodyComp(new Vector2(180, 294), 18, 26);
                // This suite compares actual native trajectories from the same impulse;
                // the mod does not contain or need a replacement charge formula.
                source.Velocity = new Vector2(direction * PlayerValues.SPEED, frame == 0 ? 4 : frame / 35f * PlayerValues.JUMP);
                NativeFlight.Set(source, "_is_on_ground", false);
                BodyComp expected = new BodyComp(source.Position, 18, 26); NativeFlight.CopyState(source, expected);
                // Silence only audiovisual endpoints, retaining the native movement order.
                var list = NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(expected, "m_behaviours");
                foreach (var b in list.ToArray()) if (b.GetType().Name == "PlayBumpSFXBehaviour" || b.GetType().Name == "WaterParticleSpawningBehaviour") list.Remove(b);
                MethodInfo tick = typeof(BodyComp).GetMethod("UpdateInternal", Flags);
                BodyComp trace = NativeFlight.CreateShadow(source, new FlightWorld(screens, 0, 0));
                int realTicks = 0;
                do {
                    tick.Invoke(expected, new object[] { 1f/60f }); realTicks++;
                    JKRuntime.Simulation.NativeFlightSimulation.AdvanceShadow(trace, 1f/60f);
                    var comparison = JKRuntime.Simulation.SimulationConformance.CompareFields("installed BodyComp vs Runtime native ballistic adapter",
                        realTicks, new JKRuntime.Simulation.SimulationInput(direction, false),
                        JKRuntime.Simulation.SimulationConformance.ReadBody(expected), JKRuntime.Simulation.SimulationConformance.ReadBody(trace));
                    Require(comparison.Matches, "First divergent tick=" + comparison.Tick + " impulse=" + frame + " material=" + material.GetType().Name + ": " + string.Join("; ", comparison.Differences));
                } while (!expected.IsOnGround && realTicks < 1000);
                BodyComp landing; int ticks; string reason;
                bool predicted = NativeFlight.TryPredict(source, new FlightWorld(screens, 0, 0), out landing, out ticks, out reason);
                Require(predicted, "Prediction failed: " + reason + " material=" + material.GetType().Name + " water=" + water + " obstacles=" + obstacles + " impulse=" + frame + " direction=" + direction + " nativeGround=" + expected.IsOnGround + " nativePosition=" + expected.Position);
                Require(landing.Position == expected.Position && landing.Velocity == expected.Velocity && ticks == realTicks, "Native flight mismatch at impulse " + frame);
                Require(source.Position == new Vector2(180,294), "Prediction mutated live position");
                var context = new BehaviourContext(source);
                NativeFlight.Commit(landing, source, context);
                Require(source.Position == expected.Position && source.Velocity == expected.Velocity && source.IsOnGround == expected.IsOnGround
                    && source.LastVelocity == expected.LastVelocity, "Commit lost native landing state");
            }
            }
            Console.WriteLine("[OK] "+comparisons+" native impulse/fall comparisons: both directions/vertical, solid/ice/snow/sand/warp, dry/water, wall/ceiling, landing commit");
            MultiScreenAndSlope();
        }
        private static void MultiScreenAndSlope()
        {
            var screens = new[] {
                new LevelScreen(0, new IBlock[] { new BoxBlock(new Rectangle(0,320,480,40)), new SlopeBlock(new Rectangle(150,200,96,96), SlopeType.TopRight) }, new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
                new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null)
            };
            typeof(LevelManager).GetField("m_screens", Flags).SetValue(null, screens);
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 2);
            for (int direction = -1; direction <= 1; direction++)
            {
                typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 1);
                BodyComp source = new BodyComp(new Vector2(180,-180),18,26) { Velocity = new Vector2(direction*3, 2) };
                NativeFlight.Set(source,"_is_on_ground",false);
                BodyComp expected = new BodyComp(source.Position,18,26); NativeFlight.CopyState(source,expected);
                var list = NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(expected,"m_behaviours");
                foreach (var b in list.ToArray()) if (b.GetType().Name == "PlayBumpSFXBehaviour" || b.GetType().Name == "WaterParticleSpawningBehaviour") list.Remove(b);
                int count = 0;
                do {
                    typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(expected,new object[]{1f/60f});
                    Camera.UpdateCameraWithVelocity(expected.GetHitbox().Center,expected.Velocity); count++;
                } while (!expected.IsOnGround && count < 1000);
                int oldCamera = Camera.CurrentScreen;
                BodyComp landing; int ticks; string reason;
                Require(NativeFlight.TryPredict(source,new FlightWorld(screens,1,0),out landing,out ticks,out reason),reason);
                Require(landing.Position == expected.Position && landing.Velocity == expected.Velocity && ticks == count,"Multi-screen/slope native parity");
                Require(Camera.CurrentScreen == oldCamera,"Prediction changed real camera");
            }
            Console.WriteLine("[OK] Multi-screen falls and native slope collision parity; camera unchanged by prediction");
        }
        private sealed class ForeignBlock : BoxBlock { internal ForeignBlock() : base(new Rectangle(0,320,480,40)) { } }
        private static void InstalledControllerTick(Sprite custom=null)
        {
            var screens = Scene(new IBlock[] { new WarpSurfaceBlock(new Rectangle(0,320,480,40)) });
            typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
            typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,1);
            typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
            PlayerEntity player = (PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            typeof(EntityComponent.Entity).GetField("m_components",Flags).SetValue(player,new List<EntityComponent.Component>());
            player.m_body = new BodyComp(new Vector2(180,294),18,26) { Velocity = new Vector2(3,-6) };
            var brain=new EntityComponent.BT.BehaviorTreeComp(new BehaviorTree.BTsequencor());
            var input=new InputComponent();
            typeof(PlayerEntity).GetField("m_bt",Flags).SetValue(player,brain);
            player.AddComponents(player.m_body,input,brain);
            var ground=new IsOnGround(player);
            typeof(PlayerEntity).GetField("m_is_on_ground_state",Flags).SetValue(player,ground);
            var sound=new CountingSound(); ground.RegisterLandSound<WarpSurfaceBlock>(sound);
            // Native solid blocks have no default OnBlocks entry. Supply a
            // sound-only contact flag for this fixture, outside its physics list.
            var lookup=NativeFlight.Get<Dictionary<Type,IBlockBehaviour>>(player.m_body,"m_blockBehaviourLookup");
            var soundContact=(IBlockBehaviour)Activator.CreateInstance(lookup[typeof(WaterBlock)].GetType());
            soundContact.IsPlayerOnBlock=true; lookup.Add(typeof(WarpSurfaceBlock),soundContact);
            var charge=Game1.instance.contentManager.playerSprites.jump_charge;
            var standing=Game1.instance.contentManager.playerSprites.idle;
            player.SetSprite(charge);
            typeof(PlayerEntity).GetField("m_flip",Flags).SetValue(player,Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally);
            var captured=new WarpVisual(player).CaptureImage();
            Require(captured.Original==charge && captured.ArrivalSprite==standing && captured.Layers.Length==2 && captured.ArrivalLayers.Length==2,"Departure/standing outfit capture");
            Require(captured.Layers[0].Source!=captured.ArrivalLayers[0].Source && captured.Flip==Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally,"Standing atlas/facing must differ from charge pose, not facing");
            if(custom!=null)player.SetSprite(custom);
            BodyComp expected; int ticks; string reason;
            Require(NativeFlight.TryPredict(player.m_body,new FlightWorld(screens,0,0),out expected,out ticks,out reason),reason);
            int before = player.m_body.GetBehaviourList().Count;
            using (WarpController controller = new WarpController(player))
            {
                Require(player.m_body.GetBehaviourList().Count == before+1,"One owned pipeline insertion");
                typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(player.m_body,new object[]{1f/60f});
                Require(player.m_body.Position==new Vector2(180,294),"Teleport must not happen at launch");
                Require(!player.m_body.Enabled && !brain.Enabled && input.Enabled,"Transition must lock movement/charge but keep native buffering alive");
                Require(JKRuntime.Gameplay.PresentationActivity.IsActive(player.m_body), "Warp must retain presentation recording ticks");
                AdvanceFor(controller,MatrixPixels.Dissolve*.6f);
                Require(sound.Plays==0,"Sound played before real landing");
                Require(player.m_body.Position==new Vector2(180,294),"Dissolving player left origin early");
                object state=controller.Capture(); controller.Validate(state);
                controller.Advance(0);
                Require(player.m_body.Position==new Vector2(180,294),"Paused transition advanced");
                AdvanceFor(controller,MatrixPixels.Transfer-MatrixPixels.Dissolve*.6f+.005f);
                Require(player.m_body.Position==expected.Position && !player.m_body.Enabled,"Transfer must precede assembly, not unlock controls");
                Require(sound.Plays==0,"Sound played before assembly completed");
                for(int i=0;i<10;i++) controller.Advance(.05f);
                Require(player.m_body.Position==expected.Position && player.m_body.Velocity==expected.Velocity,"Completed transition lost native landing state");
                Require(player.m_body.Enabled && brain.Enabled && input.Enabled,"Transition left controls locked");
                Require(!JKRuntime.Gameplay.PresentationActivity.IsActive(player.m_body), "Finished warp leaked presentation lease");
                Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==standing,"Completed warp restored Charge instead of standing");
                Require(sound.Plays==1,"Missing or duplicated native custom landing sound");
                ground.Run(new BehaviorTree.TickData());
                Require(sound.Plays==1,"Ordinary ground evaluation duplicated warp sound");
                controller.Restore(state);
                Require(player.m_body.Position==new Vector2(180,294) && !brain.Enabled,"Mid-dissolve snapshot failed");
                Require(JKRuntime.Gameplay.PresentationActivity.IsActive(player.m_body), "Restored warp lost presentation recording lease");
                for(int i=0;i<20;i++) controller.Advance(.05f);
                Require(player.m_body.Position==expected.Position && brain.Enabled,"Restored transition failed to finish");
                Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==standing,"Restored warp lost standing pose");
                Require(sound.Plays==2,"Restored transition did not produce one landing sound");
            }
            Require(player.m_body.GetBehaviourList().Count == before,"Controller teardown left hooks");
            using (WarpController controller = new WarpController(player))
            {
                Require(player.GetComponents().Length == 4,"Controller reload leaked visual components");
                player.m_body.Position=new Vector2(180,294); player.m_body.Velocity=new Vector2(3,-6);
                player.SetSprite(charge);
                NativeFlight.Set(player.m_body,"_is_on_ground",true);
                typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(player.m_body,new object[]{1f/60f});
                Require(!brain.Enabled,"Cancellation fixture did not start");
            }
            Require(player.m_body.Enabled && brain.Enabled && input.Enabled && player.m_body.Velocity==new Vector2(3,-6),"Dispose must resume launch, not leave a frozen player");
            Require(!JKRuntime.Gameplay.PresentationActivity.IsActive(player.m_body), "Cancelled warp leaked presentation lease");
            Require(sound.Plays==2,"Cancelled launch incorrectly played landing sound");
            Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==charge,"Pre-transfer cancellation must retain departure pose");
            using(WarpController controller=new WarpController(player))
            {
                NativeFlight.Set(player.m_body,"_is_on_ground",true);
                typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(player.m_body,new object[]{1f/60f});
                var plan=typeof(WarpController).GetField("plan",Flags).GetValue(controller);
                plan.GetType().GetField("Global",Flags).SetValue(plan,true);
                Settings.Current.WarpJump=false;
                controller.Advance(.016f);
                Require(player.m_body.Enabled && brain.Enabled && input.Enabled && player.m_body.Velocity==new Vector2(3,-6),"Disabling global warp did not cancel/release before transfer");
            }
            brain.Enabled=false;
            using(WarpController controller=new WarpController(player))
            {
                NativeFlight.Set(player.m_body,"_is_on_ground",true);
                typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(player.m_body,new object[]{1f/60f});
                AdvanceFor(controller,MatrixPixels.Transfer+.005f);
                Require(!player.m_body.Enabled,"After-transfer cancellation test already finished");
            }
            Require(player.m_body.Enabled && !brain.Enabled && input.Enabled && player.m_body.Position==expected.Position,"After-transfer cleanup lost destination or pre-existing disabled state");
            Require(sound.Plays==3,"After-transfer cancellation must acknowledge landing once");
            Require(typeof(PlayerEntity).GetField("m_sprite",Flags).GetValue(player)==standing,"After-transfer cancellation must leave standing pose");
            brain.Enabled=true;
            sound.Throw=true;
            new WarpLandingSound(player).Play(player.m_body);
            Require(ground.last_result==BehaviorTree.BTresult.Success,"Audio error left duplicate landing pending");
            for(int y=0;y<26;y++) for(int x=0;x<18;x++)
            {
                var start=MatrixPixels.At(x,y,18,26,0,false); var gone=MatrixPixels.At(x,y,18,26,1,false);
                var returned=MatrixPixels.At(x,y,18,26,1,true);
                Require(start.Offset==Vector2.Zero && start.Alpha==1 && gone.Alpha==0 && returned.Offset==Vector2.Zero && returned.Alpha==1,"Pixel endpoints must reconstruct exact sprite");
            }
            Require(Math.Abs(MatrixPixels.Duration-.24f)<.00001f,"Transition must be 3x faster (240ms)");
            Console.WriteLine("[OK] 240ms warp: delayed transfer, locked assembly, exact native landing, pause/snapshot/cancel/teardown, single native landing sound, exact pixel endpoints");
        }
        private static void AdvanceFor(WarpController controller,float seconds)
        {
            while(seconds>0) { float delta=Math.Min(.005f,seconds); controller.Advance(delta); seconds-=delta; }
        }
        private sealed class SpriteGameFixture : IDisposable
        {
            private readonly FieldInfo instance=typeof(Game1).GetField("_instance",Flags);
            private readonly object previous;
            internal SpriteGameFixture()
            {
                previous=instance.GetValue(null);
                var game=(Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Game1));
                game.contentManager=(JKContentManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JKContentManager));
                game.contentManager.playerSprites=new JKContentManager.PlayerSprites { _CurrentSprites=new JumpKing.JKMemory.LayeredKingSprites(null) };
                game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
                instance.SetValue(null,game);
            }
            public void Dispose() { instance.SetValue(null,previous); }
        }
        private sealed class CountingSound : JumpKing.XnaWrappers.IJKSound
        {
            internal int Plays; internal bool Throw;
            public bool IsLooped { get; set; }
            public JumpKing.XnaWrappers.JKSoundState State { get { return default(JumpKing.XnaWrappers.JKSoundState); } }
            public float Volume { get; set; }
            public TimeSpan Duration { get { return TimeSpan.Zero; } }
            public void Play() { Plays++; if(Throw) throw new InvalidOperationException("Test audio failure"); }
            public void Pause() {} public void Resume() {} public void Stop() {}
        }
        private static void RefusalIsNonMutating()
        {
            BodyComp source = new BodyComp(new Vector2(180,100),18,26) { Velocity = new Vector2(2,4) };
            NativeFlight.Set(source,"_is_on_ground",false);
            BodyComp landing; int ticks; string reason;
            Require(!NativeFlight.TryPredict(source,new FlightWorld(Scene(new IBlock[]{new ForeignBlock()}),0,0),out landing,out ticks,out reason)
                && reason.Contains("Unsupported block"),"Foreign geometry must not be guessed");
            Require(!NativeFlight.TryPredict(source,new FlightWorld(Scene(new IBlock[0]),0,0),out landing,out ticks,out reason),"Endless fall must not warp");
            Require(source.Position == new Vector2(180,100) && source.Velocity == new Vector2(2,4),"Failed prediction changed source");
            Console.WriteLine("[OK] Unsupported block and no-landing refusal leave live body unchanged");
        }
    }
}
