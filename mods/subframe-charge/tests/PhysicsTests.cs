using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using HarmonyLib;
using JKRuntime.Gameplay;
using JKRuntime.Input;
using JumpKing;
using JumpKing.API;
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private sealed class PhysicsPlayer : PlayerEntity
    {
        internal int Saves;
        protected override void Update(float delta) { Saves++; }
        protected override void OnDestroy() { }
    }
    private static IEnumerable<CodeInstruction> HeadlessPhysics(IEnumerable<CodeInstruction> source) { return HeadlessMenu(HeadlessTick(source)); }
    private static bool TestWindVelocity(ref float __result) { __result=.03f; return false; }
    private static BodyComp PhysicsBody(Vector2 position,Vector2 velocity)
    {
        var body=new BodyComp(position,18,26) { Velocity=velocity };
        AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(body,false);
        var stages=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);
        foreach(var stage in stages.ToArray())
            if(new[]{"WindVelocityUpdateBehaviour","CacheLastScreenBehaviour","WaterParticleSpawningBehaviour","CapPositionBehaviour","HandlePlayerTeleportBehaviour","PlayBumpSFXBehaviour"}.Contains(stage.GetType().Name)) stages.Remove(stage);
        return body;
    }
    private static void PhysicsBodyStep(BodyComp body,float delta,bool fractional)
    {
        if(fractional) PhysicsDynamics.Begin(body,delta);
        try { AccessTools.Method(typeof(BodyComp),"UpdateInternal").Invoke(body,new object[]{delta}); }
        finally { if(fractional) PhysicsDynamics.End(); }
    }
    private static void PhysicsScene(params IBlock[] blocks)
    {
        AccessTools.Field(typeof(LevelManager),"_total_screens").SetValue(null,1);
        AccessTools.Field(typeof(Camera),"_current_screen").SetValue(null,0);
        AccessTools.Field(typeof(LevelManager),"m_screens").SetValue(null,new[]{new LevelScreen(0,blocks,new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)});
    }
    private static void PhysicsStartupTests()
    {
        int streams=SharedActionSampler.ActiveStreams;
        SettingsStore.Current.Enabled=false; SettingsStore.Current.ShowMeasurement=false;
        using(var attempt=new JKRuntime.RuntimeScope())
        {
            SubframeChargeState.PrepareInput(attempt);
            Check(SharedActionSampler.ActiveStreams==streams,"Disabled charge preparation started an input stream");
        }
        SettingsStore.Current.Enabled=true;
        for(int i=0;i<2;i++)
        {
            using(var attempt=new JKRuntime.RuntimeScope())
            {
                SubframeChargeState.PrepareInput(attempt);
                Check(SharedActionSampler.ActiveStreams==streams+1,"Charge input wasn't prepared before activation");
                using(var playerInput=SharedActionSampler.Acquire("subframe-charge","native.jump",true))
                {
                    Check(!playerInput.RequestedEnabled,"Preparation configured player bindings before activation");
                    JumpInputTransition edge;
                    Check(!playerInput.TryDequeue(out edge),"Prepared stream replayed loading input into a new player");
                }
                Check(SharedActionSampler.ActiveStreams==streams+1,"Controller teardown released the preparation's stream");
            }
            Check(SharedActionSampler.ActiveStreams==streams,"Cancelled intro or attempt teardown leaked input preparation");
        }
        PhysicsScene(new BoxBlock(new Rectangle(0,300,480,60)));
        var target=PhysicsDynamics.Targets[0];
        using(var world=new JKRuntime.RuntimeScope())
        {
            SubframePhysics.PrepareWorld(world);
            foreach(var mode in new[]{SubframeMode.Charge,SubframeMode.AllInputs})
            {
                SettingsStore.Current.Mode=mode;
                using(var attempt=new JKRuntime.RuntimeScope()) SubframePhysics.Prepare(attempt);
                Check(!HasPhysicsPatches(),"Input-only preparation installed physics adapters");
            }
            SettingsStore.Current.Mode=SubframeMode.InputsAndPhysics;
            JKRuntime.MethodValidityLease generation=null;
            try
            {
                for(int attempt=0;attempt<3;attempt++)
                {
                    var scope=new JKRuntime.RuntimeScope();
                    var timer=Stopwatch.StartNew();
                    SubframePhysics.Prepare(scope);
                    Console.WriteLine("physics prepare {0}: {1:F3} ms",attempt,timer.Elapsed.TotalMilliseconds);
                    Check(HasPhysicsPatches() && !SubframePhysics.Active,"Preparation must install dormant adapters only");
                    if(generation==null) generation=JKRuntime.MethodValidity.Watch(target,PhysicsDynamics.Owner);
                    timer.Restart();
                    var watches=new List<JKRuntime.MethodValidityLease>();
                    foreach(var method in PhysicsDynamics.Targets) watches.Add(JKRuntime.MethodValidity.Watch(method,PhysicsDynamics.Owner));
                    Console.WriteLine("physics watches {0}: {1:F3} ms",attempt,timer.Elapsed.TotalMilliseconds);
                    timer.Restart();
                    foreach(var watch in watches) watch.Dispose();
                    scope.Dispose();
                    Console.WriteLine("physics release {0}: {1:F3} ms",attempt,timer.Elapsed.TotalMilliseconds);
                    Check(generation.IsValid,"Restart or cancelled intro rebuilt the physics patch graph");
                }
            }
            finally { if(generation!=null) generation.Dispose(); }
        }
        Check(!HasPhysicsPatches(),"World exit retained physics adapters");
        PhysicsScene(new UnauditedBlock());
        using(var world=new JKRuntime.RuntimeScope())
        {
            SubframePhysics.PrepareWorld(world);
            using(var attempt=new JKRuntime.RuntimeScope()) SubframePhysics.Prepare(attempt);
            Check(HasPhysicsPatches(),"Custom geometry incorrectly disables fractional physics");
        }
        PhysicsScene();
        var foreign=new Harmony("sfc.physics-startup.foreign");
        foreign.Patch(target,prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        try
        {
            using(var world=new JKRuntime.RuntimeScope())
            {
                SubframePhysics.PrepareWorld(world);
                var timer=Stopwatch.StartNew();
                using(var attempt=new JKRuntime.RuntimeScope()) SubframePhysics.Prepare(attempt);
                Console.WriteLine("unsupported physics prepare: {0:F3} ms",timer.Elapsed.TotalMilliseconds);
                Check(HasPhysicsPatches(),"Foreign patch presence incorrectly disables fractional physics");
                foreign.UnpatchAll(foreign.Id);
                using(var attempt=new JKRuntime.RuntimeScope()) SubframePhysics.Prepare(attempt);
                Check(HasPhysicsPatches(),"A previous world's material or an old patch refusal leaked into preparation");
            }
        }
        finally { foreign.UnpatchAll(foreign.Id); }
        Check(!HasPhysicsPatches(),"Replacement world leaked physics adapters");
        var option=(SubframeModeOption)FormatterServices.GetUninitializedObject(typeof(SubframeModeOption));
        foreach(SubframeMode mode in Enum.GetValues(typeof(SubframeMode)))
        {
            SettingsStore.Current.Mode=mode;
            AccessTools.Property(typeof(SubframePhysics),"Reason").SetValue(null,"custom controller",null);
            Check((string)AccessTools.Method(typeof(SubframeModeOption),"CurrentOptionName").Invoke(option,null)=="Mode: "+SubframeModes.Label(mode),"Mode label exposed runtime state");
        }
        SubframePhysics.Stop();
        Console.WriteLine("Physics startup checks: "+checks);
    }
    private static bool HasPhysicsPatches()
    { return PhysicsDynamics.Targets.Any(target=>{ var info=Harmony.GetPatchInfo(target); return info!=null && info.Owners.Contains(PhysicsDynamics.Owner); }); }
    private static void PhysicsTests()
    {
        var dynamics=PhysicsDynamics.Acquire();
        var screens=AccessTools.Field(typeof(LevelManager),"m_screens");
        var total=AccessTools.Field(typeof(LevelManager),"_total_screens");
        var camera=AccessTools.Field(typeof(Camera),"_current_screen");
        object oldScreens=screens.GetValue(null),oldTotal=total.GetValue(null),oldCamera=camera.GetValue(null);
        try
        {
            foreach(bool water in new[]{false,true})
            foreach(float initialY in new[]{-10f,-3f,0f,4f})
            {
                PhysicsScene(water ? new IBlock[]{new WaterBlock(new Rectangle(-1000,-1000,3000,3000))} : new IBlock[0]);
                var native=PhysicsBody(new Vector2(150,150),new Vector2(3.5f,initialY));
                var fast=PhysicsBody(native.Position,native.Velocity);
                foreach(var b in new[]{native,fast})
                    ((Dictionary<Type,IBlockBehaviour>)AccessTools.Field(typeof(BodyComp),"m_blockBehaviourLookup").GetValue(b))[typeof(WaterBlock)].IsPlayerOnBlock=water;
                for(int frame=0;frame<20;frame++)
                {
                    PhysicsBodyStep(native,1f/60,false);
                    for(int sub=0;sub<4;sub++) PhysicsBodyStep(fast,1f/240,true);
                    // bounding backwards movement near the apex costs less than g/4 per flight
                    Check(Math.Abs(native.Position.X-fast.Position.X)<.002f && Math.Abs(native.Position.Y-fast.Position.Y)<PlayerValues.GRAVITY*(water?.5f:1)/4+.002f,"Fractional free flight changes native strength/trajectory: water="+water+" frame="+frame+" native="+native.Position+" fast="+fast.Position);
                    Check(Vector2.Distance(native.Velocity,fast.Velocity)<.0001f,"Fractional gravity changes native velocity units");
                }
            }
            PhysicsScene();
            var capped=PhysicsBody(new Vector2(100,100),new Vector2(0,PlayerValues.MAX_FALL-.01f));
            PhysicsBodyStep(capped,1f/240,true);
            Check(capped.Velocity.Y==PlayerValues.MAX_FALL,"Fractional gravity approaches terminal velocity without ever reaching the native cap");
            var timed=PhysicsBody(new Vector2(100,200),new Vector2(3.5f,-10));
            float delta=(float)(41667.0/170000/60),h=delta*60;
            for(int step=1;step<=100;step++)
            {
                PhysicsBodyStep(timed,delta,true);
                double t=step*(double)h;
                Check(Math.Abs(timed.Position.X-(100+3.5*t))<.003,"Wall-clock interval accelerates walking/jumping");
                Check(Math.Abs(timed.Position.Y-(200-10*t+PlayerValues.GRAVITY*t*(t-1)/2))<PlayerValues.GRAVITY/4+.003,"Actual scheduler step loses native parabola");
            }
            foreach(IBlock surface in new IBlock[]{new IceBlock(new Rectangle(0,250,480,40)),new SnowBlock(new Rectangle(0,250,480,40)),new SandBlock(new Rectangle(0,250,480,100))})
            {
                PhysicsScene(surface);
                var body=PhysicsBody(new Vector2(150,200),new Vector2(0,4));
                for(int step=0;step<400;step++)
                {
                    PhysicsBodyStep(body,1f/240,true);
                    Check(!float.IsNaN(body.Position.Y) && body.Velocity.Y<=PlayerValues.MAX_FALL,"Surface produced invalid or excessive velocity");
                    if(!(surface is SandBlock)) Check(body.Position.Y<=224.001f,"Fractional collision entered solid ground");
                }
                if(!(surface is SandBlock)) Check(body.IsOnGround && Math.Abs(body.Position.Y-224)<.001f,"Ground support drifts under repeated fractional steps");
            }
            SimulationInputTests();
            PhysicsCallbackTests();
            MaterialCadenceTests();
            WindAndCollisionTests();
            PhysicsJumpTests();
            Console.WriteLine("[OK] Fractional native dynamics: trajectory, scheduler tempo, water, sand, ice/snow support, direction memory and ordered input");
        }
        finally { PhysicsDynamics.Reset(); dynamics.Dispose(); screens.SetValue(null,oldScreens); total.SetValue(null,oldTotal); camera.SetValue(null,oldCamera); }
    }
    private sealed class ForeignTick : IBodyCompBehaviour
    {
        internal int Calls;
        internal float Time;
        internal bool Continue = true, Throw;
        public bool ExecuteBehaviour(JumpKing.BodyCompBehaviours.BehaviourContext context)
        { Calls++; Time += context.FrameDelta; if (Throw) throw new InvalidOperationException("fixture"); return Continue; }
    }
    private static void PhysicsCallbackTests()
    {
        PhysicsScene();
        var body=PhysicsBody(new Vector2(150,150),Vector2.Zero);
        var context=new JumpKing.BodyCompBehaviours.BehaviourContext(body);
        var foreign=new ForeignTick(); var fine=new ForeignTick();
        PhysicsDynamics.Reset();
        using(var pipeline=new BodyPipeline(body,false))
        using(var lease=pipeline.RegisterSubstep(BodyPhase.BeforeXMovement,fine))
        {
            for(int i=0;i<240;i++)
            {
                context.FrameDelta=1f/240;
                PhysicsDynamics.Begin(body,context.FrameDelta);
                try { PhysicsCallbacks.Body(foreign,context); PhysicsCallbacks.Body(fine,context); }
                finally { PhysicsDynamics.End(); }
                Check(context.FrameDelta==1f/240,"Foreign callback leaked native delta into smaller steps");
                Check(PlayerPhysicsStep.Fraction(body)==1,"Fraction escaped its owned step");
            }
            Check(foreign.Calls==60 && Math.Abs(foreign.Time-1)<.00001,"Foreign fixed-tick behaviour accelerated");
            Check(fine.Calls==240 && Math.Abs(fine.Time-1)<.00001,"Opted-in collision behaviour missed substeps");
        }
        Check(!BodyPipeline.RunsAtSubsteps(body,fine),"Disposed registration retained its substep permission");
        PhysicsDynamics.Reset(); foreign.Continue=false;
        for(int i=0;i<4;i++)
        {
            PhysicsDynamics.Begin(body,1f/240);
            try { Check(!PhysicsCallbacks.Body(foreign,context),"Cached abort allowed a partial native pipeline to resume"); }
            finally { PhysicsDynamics.End(); }
        }
        PhysicsDynamics.Reset(); foreign.Throw=true;
        PhysicsDynamics.Begin(body,1f/240);
        try { try { PhysicsCallbacks.Body(foreign,context); throw new Exception("Missing callback failure"); } catch(InvalidOperationException) { } }
        finally { PhysicsDynamics.End(); }
        Check(context.FrameDelta==1f/240 && PlayerPhysicsStep.Fraction(body)==1,"Throwing foreign callback leaked timing state");
        PhysicsDynamics.Reset();
    }
    private static void WindAndCollisionTests()
    {
        foreach(int top in new[]{90,-270})
        foreach(float speed in new[]{0,PlayerValues.GRAVITY*.1f,PlayerValues.GRAVITY*.25f,-.01f,-5f})
        {
            PhysicsScene(new BoxBlock(new Rectangle(0,top,480,11)));
            var ceilingContact=PhysicsBody(new Vector2(150,top+11),new Vector2(0,speed));
            for(int i=0;i<16;i++)
            {
                PhysicsBodyStep(ceilingContact,1f/240,true);
                Check(ceilingContact.GetHitbox().Y>=top+11,"Ceiling contact pushed the player through the platform: "+ceilingContact.Position);
            }
        }
        var windType=typeof(BodyComp).Assembly.GetTypes().Single(t=>t.Name=="WindManager");
        var fixture=new Harmony("sfc.physics-wind.tests");
        fixture.Patch(windType.GetMethods(Flags).Single(m=>m.Name.EndsWith("GetCurrentVelocity",StringComparison.Ordinal)),prefix:new HarmonyMethod(typeof(PerformanceTests),"TestWindVelocity"));
        object windQuery=FormatterServices.GetUninitializedObject(windType);
        PhysicsScene();
        AccessTools.Field(typeof(LevelManager),"m_screens").SetValue(null,new[]{new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),true,new TeleportLink[0],0,null)});
        var native=PhysicsBody(new Vector2(150,150),new Vector2(1,-5));
        var fast=PhysicsBody(native.Position,native.Velocity);
        foreach(var body in new[]{native,fast})
        {
            var wind=(IBodyCompBehaviour)Activator.CreateInstance(typeof(JumpKing.BodyCompBehaviours.WindVelocityUpdateBehaviour),Flags,null,
                new object[]{LevelManager.Instance,LevelManager.Instance,windQuery},null);
            AccessTools.Field(wind.GetType(),"m_wind_enabled").SetValue(wind,true);
            var stages=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);
            stages.AddBefore(stages.Find(stages.First(s=>s.GetType().Name=="CacheVelocityBehaviour")),wind);
        }
        for(int frame=0;frame<20;frame++)
        {
            PhysicsBodyStep(native,1f/60,false);
            for(int i=0;i<4;i++) PhysicsBodyStep(fast,1f/240,true);
            Check(Math.Abs(native.Position.X-fast.Position.X)<.003 && Math.Abs(native.Position.Y-fast.Position.Y)<PlayerValues.GRAVITY/4+.003 && Vector2.Distance(native.Velocity,fast.Velocity)<.0001,"Wind force or native kick-before-move trajectory changed");
        }
        PhysicsScene(new BoxBlock(new Rectangle(200,0,8,360)));
        AccessTools.Field(typeof(LevelManager),"m_screens").SetValue(null,new[]{new LevelScreen(0,new IBlock[]{new BoxBlock(new Rectangle(200,0,8,360))},new LevelScreen.Graphics(),true,new TeleportLink[0],0,null)});
        fast.Position=new Vector2(182.9999f,150); fast.Velocity=new Vector2(-.01f,0);
        PhysicsBodyStep(fast,1f/240,true);
        Check(fast.Velocity.X>-.01f && fast.Velocity.X<0,"Wall reversal fixture didn't receive opposing wind");
        Check(fast.Position.X<183,"Wind reversal pushed the player through the wall: "+fast.Position);
        Check(native.Velocity.X>1.5f,"Wind fixture never applied wind");
        fixture.UnpatchAll(fixture.Id);
        PhysicsScene(new IceBlock(new Rectangle(0,250,480,40)));
        native=PhysicsBody(new Vector2(150,224),new Vector2(3.5f,PlayerValues.GRAVITY)); fast=PhysicsBody(native.Position,native.Velocity);
        foreach(var body in new[]{native,fast})
        {
            AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(body,true);
            ((Dictionary<Type,IBlockBehaviour>)AccessTools.Field(typeof(BodyComp),"m_blockBehaviourLookup").GetValue(body))[typeof(IceBlock)].IsPlayerOnBlock=true;
        }
        for(int frame=0;frame<40;frame++)
        {
            PhysicsBodyStep(native,1f/60,false); for(int i=0;i<4;i++) PhysicsBodyStep(fast,1f/240,true);
            Check(Math.Abs(native.Velocity.X-fast.Velocity.X)<.0001,"Ice friction accelerated with the step count");
        }
        foreach(IBlock block in new IBlock[]{new BoxBlock(new Rectangle(200,-1000,1,3000)),new BoxBlock(new Rectangle(-1000,100,3000,1)),
            new SlopeBlock(new Rectangle(180,200,48,48),SlopeType.TopLeft),new SlopeBlock(new Rectangle(180,200,48,48),SlopeType.TopRight)})
        {
            PhysicsScene(block); var body=PhysicsBody(new Vector2(150,150),new Vector2(3.5f,-5));
            for(int step=0;step<160;step++)
            {
                PhysicsBodyStep(body,1f/240,true); Rectangle overlap;
                Check(block.Intersects(body.GetHitbox(),out overlap)!=BlockCollisionType.Collision_Blocking,"Fractional collision ended inside a thin wall, ceiling or slope");
            }
        }
    }
    private static void SimulationInputTests()
    {
        var input=new SimulationInput(); long start=Stopwatch.GetTimestamp(),ms=Stopwatch.Frequency/1000; int state,pressed;
        input.Record(4|16,4|16,start+ms); input.Record(8|16,8,start+2*ms); input.Record(8,0,start+3*ms);
        input.Read(start,out state,out pressed); Check(state==0 && pressed==0,"Catch-up step consumed a future input");
        input.Read(start+ms,out state,out pressed); Check(state==(4|16) && pressed==(4|16),"First physical edge was reordered");
        input.Read(start+3*ms,out state,out pressed); Check(state==8 && pressed==8,"Opposite pulse cancels the newest held direction in physics");
        input.Read(start+4*ms,out state,out pressed); Check(state==8 && pressed==0,"Physics replayed a consumed edge");
        input.Reset(); input.Read(start+5*ms,out state,out pressed); Check(state==0 && pressed==0,"Pause/restore leaked old physics state");
        for(int i=0;i<257;i++) input.Record((i&1)==0 ? 4 : 0,(i&1)==0 ? 4 : 0,start+i);
        input.Read(start+300,out state,out pressed); Check(state==0 && pressed==0,"Overflow replayed unordered physics input");
        input.Record(0,0,start+301); input.Record(8,8,start+302); input.Read(start+303,out state,out pressed);
        Check(state==8 && pressed==8,"A released overflow guard failed to rearm");
    }
    private static void PhysicsJumpTests()
    {
        var fixture=new Harmony("sfc.physics-native.tests"); var game=MakeGame();
        fixture.Patch(AccessTools.Method(typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.FullRunManager",true),"OnBodyCompBehaviourChanged"),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        foreach(var pair in new[]{new[]{"IsGameActive","BufferTrue"},new[]{"ConfigureSampler","BufferTrue"},new[]{"HasUnsupportedJumpDown","BufferFalse"}})
            fixture.Patch(AccessTools.Method(typeof(SubframeChargeState),pair[0]),prefix:new HarmonyMethod(typeof(PerformanceTests),pair[1]));
        game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
        game.contentManager.playerSprites=new JKContentManager.PlayerSprites { _CurrentSprites=new JumpKing.JKMemory.LayeredKingSprites(null) };
        game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
        foreach(var type in new[]{typeof(JumpState),typeof(IsOnGround)})
            foreach(string method in new[]{"HandleSounds","HandleParticles"}) fixture.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        new EntityManager();
        var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
        var pad=new PadInstance(new Pad()); AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>{pad});
        var callback=PlayerEntity.OnJumpCall;
        fixture.Patch(AccessTools.Method(typeof(SubframePhysics),"Advance"),transpiler:new HarmonyMethod(typeof(PerformanceTests),"HeadlessPhysics"));
        fixture.Patch(AccessTools.Method(typeof(SubframePhysics),"AfterNativeUpdate"),transpiler:new HarmonyMethod(typeof(PerformanceTests),"HeadlessPhysics"));
        AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,FormatterServices.GetUninitializedObject(typeof(JumpGame))); menuPlaying=true;
        try
        {
            foreach(bool sand in new[]{false,true})
            foreach(int side in new[]{-1,1})
            foreach(int gap in new[]{2,10,18})
            foreach(int correction in new[]{0,1,2})
            {
                PhysicsDynamics.Reset(); PhysicsScene(sand ? new IBlock[]{new SandBlock(new Rectangle(0,200,480,150))} : new IBlock[]{new IceBlock(new Rectangle(0,300,480,40))});
                var player=(PhysicsPlayer)FormatterServices.GetUninitializedObject(typeof(PhysicsPlayer));
                AccessTools.Field(typeof(Entity),"m_components").SetValue(player,new List<Component>());
                player.m_body=PhysicsBody(new Vector2(150,274),Vector2.Zero);
                AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(player.m_body,true);
                AccessTools.Field(typeof(PlayerEntity),"m_screen_shake").SetValue(player,FormatterServices.GetUninitializedObject(typeof(JumpKing.MiscSystems.ScreenShakeController)));
                var tree=(BehaviorTreeComp)AccessTools.Method(typeof(PlayerEntity),"MakeBT").Invoke(player,null);
                AccessTools.Field(typeof(PlayerEntity),"m_bt").SetValue(player,tree);
                AccessTools.Field(typeof(PlayerEntity),"m_jump_state").SetValue(player,tree.GetRaw().FindNode<JumpState>());
                player.AddComponents(player.m_body,new InputComponent(),tree);
                var original=tree.GetRaw().FindNode<JumpState>();
                var sampler=new BufferSampler(); JumpNodeBindings binding=null; ChargeFrameComponents observers=null;
                long stamp=Stopwatch.GetTimestamp(),origin=stamp,step=Stopwatch.Frequency*17/4000;
                ChargeFrameComponents.Clock=delegate { return stamp; };
                if(correction!=0)
                {
                    var replacement=(SubframeChargeState)FormatterServices.GetUninitializedObject(typeof(SubframeChargeState));
                    for(Type type=typeof(JumpState);type!=null && typeof(IBTnode).IsAssignableFrom(type);type=type.BaseType)
                        foreach(var field in type.GetFields(Flags | System.Reflection.BindingFlags.DeclaredOnly).Where(f=>!f.IsStatic)) field.SetValue(replacement,field.GetValue(original));
                    var timeline=new ChargeTimeline();
                    AccessTools.Field(typeof(SubframeChargeState),"Timeline").SetValue(replacement,timeline);
                    AccessTools.Field(typeof(SubframeChargeState),"sampler").SetValue(replacement,sampler);
                    AccessTools.Field(typeof(SubframeChargeState),"quarterSteps").SetValue(replacement,correction==2);
                    AccessTools.Field(typeof(SubframeChargeState),"stepMilliseconds").SetValue(replacement,correction==2 ? 1.0 : 17.0);
                    AccessTools.Field(typeof(SubframeChargeState),"lastGameActive").SetValue(replacement,true);
                    var probe=(JumpTrajectoryProbe)FormatterServices.GetUninitializedObject(typeof(JumpTrajectoryProbe));
                    AccessTools.Field(typeof(JumpTrajectoryProbe),"body").SetValue(probe,player.m_body);
                    AccessTools.Field(typeof(SubframeChargeState),"trajectoryProbe").SetValue(replacement,probe);
                    binding=JumpNodeBindings.Replace(tree.GetRaw(),player,original,replacement);
                    observers=new ChargeFrameComponents(player,player.m_body,tree,timeline);
                    // deliver a future release early, as in a blocked-render catch-up batch
                    sampler.Edges.Enqueue(new JumpInputTransition(true,origin+20*step));
                    sampler.Edges.Enqueue(new JumpInputTransition(false,origin+60*step));
                }
                int jumps=0,actual=0,launch=-1,tick=0; float launchY=0;
                PlayerEntity.OnJumpCall=delegate { jumps++; actual=Math.Sign(player.m_body.Velocity.X); launch=tick; launchY=player.m_body.Velocity.Y; };
                using(var lease=PlayerUpdates.RegisterSubsteps(player,"test.physics",ChargeFrameComponents.IsObserver))
                {
                    for(tick=0;tick<=60;tick++)
                    {
                        stamp=origin+tick*step;
                        var held=new PadState { jump=tick>=20 && tick<60,left=side<0 && tick==60-gap,right=side>0 && tick==60-gap };
                        using(NativeInputFrames.BeginPlayerFrame(held,new PadState { jump=tick==20 }))
                        {
                            PhysicsDynamics.Begin(player.m_body,1f/240);
                            try { lease.Advance(1f/240); } finally { PhysicsDynamics.End(); }
                        }
                    }
                }
                if(observers!=null) observers.Dispose(); if(binding!=null) binding.Restore(); sampler.Dispose();
                ChargeFrameComponents.Clock=Stopwatch.GetTimestamp;
                Check(jumps==1,"Real fractional player tree didn't jump once in sand="+sand);
                Check(launch==60,"Catch-up delivered the sampled release before its physical timestamp");
                if(correction!=0)
                {
                    var expected=ChargeQuantizer.QuantizeRelease(40*17.0/4000,1,correction==2 ? 1 : 17);
                    Check(Math.Abs(launchY-PlayerValues.JUMP*expected.TimerSeconds/PlayerValues.JUMP_TIME)<.0001f,"Fractional update changed corrected charge power: "+launchY);
                }
                Check(actual==(gap<16 ? side : 0),"Direction buffer shrank to four microsteps or retained expired direction: sand="+sand+" gap="+gap+" actual="+actual);
                if(!sand && side==1 && gap==2 && correction==0) PhysicsLifetime(player,pad);
            }
        }
        finally { SubframePhysics.Stop(); PlayerEntity.OnJumpCall=callback; fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null; GameLoop.m_player=null; AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null); AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,null); menuPlaying=false; }
    }
    private static void PhysicsLifetime(PhysicsPlayer player,PadInstance pad)
    {
        var definition=new JKRuntime.ModuleDefinition("subframe-charge",new Version(1,0),c=>{});
        var context=(JKRuntime.ModuleContext)Activator.CreateInstance(typeof(JKRuntime.ModuleContext),Flags,null,
            new[]{AccessTools.Field(typeof(JKRuntime.RuntimeApi),"Kernel").GetValue(null),(object)definition},null);
        GameLoop.m_player=player; SettingsStore.Current.Mode=SubframeMode.Charge;
        SettingsStore.Current.Enabled=false;
        try
        {
            SubframePhysics.Start(context);
            Check(!SubframePhysics.Active,"Charge startup unexpectedly enabled physics");
            SettingsStore.Current.Mode=SubframeMode.InputsAndPhysics;
            SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
            Check(!SubframePhysics.Active,"Physics enabled while the player was airborne");
            PhysicsRest(player,pad);
            SubframePhysics.AfterNativeUpdate();
            Check(!SubframePhysics.Active,"One ground flag was enough to change physics");
            SubframePhysics.AfterNativeUpdate();
            Check(SubframePhysics.Active && SubframePhysics.Reason==null,"Native profile activation refused: "+SubframePhysics.Reason);
            player.m_body.Velocity=new Vector2(1,-3);
            var start=player.m_body.Position;
            SubframePhysics.Advance(1f/240,Stopwatch.GetTimestamp());
            Check(player.m_body.Position!=start && player.Saves==0,"Production simulation failed to advance body independently of saves");
            var fast=player.m_body.Position; player.UpdateComponents(1f/60);
            Check(player.m_body.Position==fast && player.Saves==1,"Native entity tick double-integrated player physics");
            SettingsStore.Current.Mode=SubframeMode.Charge;
            Check(SubframePhysics.Active && SubframePhysics.Status=="waiting for safe moment" && PerformanceFeatures.InputsRequested,"Mode change dropped physics ownership midflight");
            SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
            Check(SubframePhysics.Active,"Physics disabled during flight");
            using(PerformanceSuspension.Acquire()) SubframePhysics.Advance(1f/240,Stopwatch.GetTimestamp());
            Check(player.m_body.Position==fast,"External world control advanced local physics");
            Check(PlayerPresentation.Position(player.m_body)==player.m_body.Position,"Physics rendering returned a predicted position");
            PhysicsRest(player,pad);
            foreach(var held in new[]{new PadState { jump=true },new PadState { left=true },new PadState { right=true }})
            {
                NativeInputFrames.Publish(pad,held,new PadState());
                SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
                Check(SubframePhysics.Active,"Held player input was discarded by a mode switch");
            }
            NativeInputFrames.Publish(pad,new PadState(),new PadState());
            var jump=(JumpState)AccessTools.Field(typeof(PlayerEntity),"m_jump_state").GetValue(player);
            AccessTools.Field(typeof(IBTnode),"m_last_result").SetValue(jump,BTresult.Running);
            SubframePhysics.AfterNativeUpdate();
            Check(SubframePhysics.Active,"Native charge was interrupted by a mode switch");
            jump.ResetResult(); player.m_body.Position.X+=1;
            SubframePhysics.AfterNativeUpdate();
            Check(SubframePhysics.Active,"Moving ground contact was treated as stable");
            var resting=player.m_body.Position; int saves=player.Saves;
            SubframePhysics.AfterNativeUpdate();
            Check(!SubframePhysics.Active && SubframePhysics.Status==null && player.m_body.Position==resting && player.Saves==saves,"Safe deactivation moved or double-updated the player");
            foreach(var nativeMode in new[]{SubframeMode.Charge,SubframeMode.AllInputs})
            {
                SettingsStore.Current.Mode=SubframeMode.InputsAndPhysics;
                using(PerformanceSuspension.Acquire()) { SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate(); }
                Check(!SubframePhysics.Active,"External control allowed an ownership handoff");
                SettingsStore.Current.Mode=nativeMode; SubframePhysics.AfterNativeUpdate();
                Check(SubframePhysics.Status==null,"Cancelled pending mode remained queued");
                SettingsStore.Current.Mode=SubframeMode.InputsAndPhysics;
                PhysicsRest(player,pad); SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
                Check(SubframePhysics.Active,"Repeated activation failed");
                SettingsStore.Current.Mode=nativeMode;
                PhysicsRest(player,pad); SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
                Check(!SubframePhysics.Active && SubframePhysics.Status==null,"Repeated deactivation failed");
            }
            player.m_body.Velocity=new Vector2(1,-3); resting=player.m_body.Position;
            player.UpdateComponents(1f/60);
            Check(player.Saves==saves+1 && player.m_body.Position!=resting,"Native updates didn't resume after switching back");
            var foreign=new Harmony("sfc.physics-lifetime.foreign");
            foreign.Patch(PhysicsDynamics.Targets[0],prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
            try
            {
                SettingsStore.Current.Mode=SubframeMode.InputsAndPhysics;
                PhysicsRest(player,pad); SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
                Check(SubframePhysics.Active && SubframePhysics.Reason==null,"Foreign patch presence refused activation");
                foreign.UnpatchAll(foreign.Id);
                SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
                Check(SubframePhysics.Active,"Removing a foreign patch lost simulation ownership");
                SettingsStore.Current.Mode=SubframeMode.Charge; SubframePhysics.AfterNativeUpdate();
                SettingsStore.Current.Mode=SubframeMode.InputsAndPhysics;
                SubframePhysics.AfterNativeUpdate(); SubframePhysics.AfterNativeUpdate();
                Check(SubframePhysics.Active,"Explicitly reselecting physics didn't retry compatibility");
            }
            finally { foreign.UnpatchAll(foreign.Id); }
        }
        finally
        {
            var errors=new List<string>(); AccessTools.Method(typeof(JKRuntime.ModuleContext),"Release").Invoke(context,new object[]{errors});
            Check(errors.Count==0 && !SubframePhysics.Active,"Attempt teardown failed to release physics ownership");
            SettingsStore.Current.Mode=SubframeMode.Charge;
        }
    }
    private static void PhysicsRest(PhysicsPlayer player,PadInstance pad)
    {
        ResponsiveInput.Reset();
        player.m_body.Position=new Vector2(150,274); player.m_body.Velocity=new Vector2(0,PlayerValues.GRAVITY);
        AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(player.m_body,true);
        AccessTools.Field(typeof(BodyComp),"_knocked").SetValue(player.m_body,false);
        ((JumpState)AccessTools.Field(typeof(PlayerEntity),"m_jump_state").GetValue(player)).ResetResult();
        NativeInputFrames.Publish(pad,new PadState(),new PadState());
    }
}
