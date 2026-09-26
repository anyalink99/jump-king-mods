using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using HarmonyLib;
using JKRuntime.Gameplay;
using JKRuntime.Input;
using JumpKing;
using JumpKing.BlockBehaviours;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private static bool bufferHeld, bufferPressed;
    private static bool BufferHeld(ref InputComponent.State __result)
    { __result = new InputComponent.State { jump=bufferHeld }; return false; }
    private static bool BufferPressed(ref InputComponent.State __result)
    { __result = new InputComponent.State { jump=bufferPressed }; return false; }
    private static bool BufferTrue(ref bool __result) { __result=true; return false; }
    private static bool BufferFalse(ref bool __result) { __result=false; return false; }
    private sealed class BufferSampler : IHighRateInput
    {
        internal readonly Queue<JumpInputTransition> Edges = new Queue<JumpInputTransition>();
        public bool Enabled { get { return true; } }
        public bool RequestedEnabled { get { return true; } }
        public bool Available { get { return true; } }
        public void Start() { }
        public void Configure(int[][] keys, XInputJumpBinding[] xbox, DirectInputJumpBinding[] legacy, IntPtr window, bool enabled) { }
        public bool CanMeasureDirectInput(Guid id) { return false; }
        public bool CanMeasurePhysical(int source) { return true; }
        public bool TryDequeue(out JumpInputTransition value)
        { value=Edges.Count==0 ? default(JumpInputTransition) : Edges.Dequeue(); return value.Timestamp!=0; }
        public void Reset() { Edges.Clear(); }
        public void LogHealth(long timestamp) { }
        public void Dispose() { Edges.Clear(); }
    }
    private sealed class BufferRun
    {
        internal readonly List<int> Launches = new List<int>();
        internal readonly List<float> Horizontal = new List<float>();
        internal readonly List<bool> Charging = new List<bool>();
        internal readonly List<JumpResult> Results = new List<JumpResult>();
    }

    private static void BufferedTreeTests()
    {
        var fixture=new Harmony("sfc.buffered-tree.tests");
        foreach (var pair in new[] {new[]{"IsGameActive","BufferTrue"},new[]{"ConfigureSampler","BufferTrue"},new[]{"HasUnsupportedJumpDown","BufferFalse"}})
            fixture.Patch(AccessTools.Method(typeof(SubframeChargeState),pair[0]),prefix:new HarmonyMethod(typeof(PerformanceTests),pair[1]));
        fixture.Patch(AccessTools.Method(typeof(InputComponent),"GetState"),prefix:new HarmonyMethod(typeof(PerformanceTests),"BufferHeld"));
        fixture.Patch(AccessTools.Method(typeof(InputComponent),"GetPressedState"),prefix:new HarmonyMethod(typeof(PerformanceTests),"BufferPressed"));
        foreach (var type in new[]{typeof(JumpState),typeof(IsOnGround)})
            foreach (string method in new[]{"HandleSounds","HandleParticles"})
                fixture.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
        AccessTools.Field(typeof(Game1),"_instance").SetValue(null,game);
        game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
        game.contentManager.playerSprites=new JKContentManager.PlayerSprites { _CurrentSprites=new JumpKing.JKMemory.LayeredKingSprites(null) };
        game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
        new EntityManager();
        ControllerManager.instance=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
        AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(ControllerManager.instance,new List<PadInstance>());
        var oldCallback=PlayerEntity.OnJumpCall;
        int coyoteAllowed=0, coyoteRejected=0, corrected=0, coyoteCorrected=0;
        try
        {
            foreach(bool ice in new[]{false,true})
            foreach(bool water in new[]{false,true})
            foreach(bool leave in new[]{false,true})
            foreach(int beforeLanding in new[]{1,5})
            foreach(int release in new[]{1,2,3,4,6,12,36,74,90})
            {
                var native=RunBufferedTree(false,false,ice,water,leave,beforeLanding,release,0);
                foreach(int lag in new[]{0,2})
                {
                    var baseline=lag==0 ? native : RunBufferedTree(false,false,ice,water,leave,beforeLanding,release,lag);
                    foreach(bool quarter in new[]{false,true})
                    {
                        var result=RunBufferedTree(true,quarter,ice,water,leave,beforeLanding,release,lag);
                        string context="ice="+ice+" water="+water+" leave="+leave+" release="+release+" lag="+lag+" quarter="+quarter;
                        Check(result.Launches.SequenceEqual(baseline.Launches),"Native buffered/coyote launch ticks changed: "+context);
                        Check(result.Charging.SequenceEqual(baseline.Charging),"Native charge pose/leniency lifetime changed: "+context);
                        Check(result.Horizontal.SequenceEqual(baseline.Horizontal),"Native ice drift/horizontal motion changed: "+context);
                        if(result.Results.Any(r=>r.Evidence==JumpEvidence.BufferedHold))
                        { corrected++; if(ice && leave) coyoteCorrected++; }
                    }
                }
                if(leave && ice) { if(native.Launches.Count==0) coyoteRejected++; else coyoteAllowed++; }
            }
            Check(coyoteAllowed>0 && coyoteRejected>0,"Fixture did not exercise both sides of native ice leniency");
            Check(corrected>0,"Full native tree never used fractional buffered evidence");
            Check(coyoteCorrected>0,"Quarter buffered holds were not exercised inside native ice leniency");
            foreach(bool water in new[]{false,true})
            {
                var native=RunBufferedTree(false,false,true,water,false,1,6,0,2);
                var delayed=RunBufferedTree(true,true,true,water,false,1,6,0,2);
                Check(delayed.Launches.SequenceEqual(native.Launches),"Delayed native charge acceptance changed takeoff");
                var measured=delayed.Results.Single(r=>r.Evidence==JumpEvidence.BufferedHold);
                // Release is 1/3 tick before tick 6; native accepts at tick 2.
                Check(Math.Abs(measured.HoldMilliseconds.Value-(6-2-1.0/3)*17)<.001,
                    "Buffered timing started at first eligibility instead of actual native acceptance");
                Check(measured.CorrectedFrameCount==3.75f,"Delayed buffer lost its fractional release");
            }
            Console.WriteLine("[OK] Full native buffered tree: ice leniency accepted="+coyoteAllowed+" rejected="+coyoteRejected+", corrected runs="+corrected);
        }
        finally
        {
            PlayerEntity.OnJumpCall=oldCallback;
            fixture.UnpatchAll(fixture.Id);
            AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            ControllerManager.instance=null;
        }
    }

    private static BufferRun RunBufferedTree(bool replace, bool quarter, bool ice, bool water, bool leave, int lead, int release, int lag, int acceptLag=0)
    {
        var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        AccessTools.Field(typeof(Entity),"m_components").SetValue(player,new List<Component>());
        player.m_body=new BodyComp(new Vector2(150,268),18,26);
        AccessTools.Field(typeof(PlayerEntity),"m_screen_shake").SetValue(player,FormatterServices.GetUninitializedObject(typeof(JumpKing.MiscSystems.ScreenShakeController)));
        var tree=(BehaviorTreeComp)AccessTools.Method(typeof(PlayerEntity),"MakeBT").Invoke(player,null);
        var input=new InputComponent(); player.AddComponents(player.m_body,input,tree);
        var original=tree.GetRaw().FindNode<JumpState>();
        var sampler=new BufferSampler(); var clock=new ChargeTimeline();
        SubframeChargeState replacement=null; JumpNodeBindings binding=null;
        if(replace)
        {
            replacement=(SubframeChargeState)FormatterServices.GetUninitializedObject(typeof(SubframeChargeState));
            for(Type type=typeof(JumpState);type!=null && typeof(IBTnode).IsAssignableFrom(type);type=type.BaseType)
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                    field.SetValue(replacement,field.GetValue(original));
            AccessTools.Field(typeof(SubframeChargeState),"Timeline").SetValue(replacement,clock);
            AccessTools.Field(typeof(SubframeChargeState),"sampler").SetValue(replacement,sampler);
            AccessTools.Field(typeof(SubframeChargeState),"quarterSteps").SetValue(replacement,quarter);
            AccessTools.Field(typeof(SubframeChargeState),"lastGameActive").SetValue(replacement,true);
            var probe=(JumpTrajectoryProbe)FormatterServices.GetUninitializedObject(typeof(JumpTrajectoryProbe));
            AccessTools.Field(typeof(JumpTrajectoryProbe),"body").SetValue(probe,player.m_body);
            AccessTools.Field(typeof(SubframeChargeState),"trajectoryProbe").SetValue(replacement,probe);
            binding=JumpNodeBindings.Replace(tree.GetRaw(),player,original,replacement);
        }
        var result=new BufferRun(); int frame=0;
        PlayerEntity.OnJumpCall=()=>result.Launches.Add(frame);
        var ground=AccessTools.Field(typeof(BodyComp),"_is_on_ground");
        var blocks=(Dictionary<Type,JumpKing.API.IBlockBehaviour>)AccessTools.Field(typeof(BodyComp),"m_blockBehaviourLookup").GetValue(player.m_body);
        // Occupancy is supplied by the fixture; native ice/water behaviours and
        // the complete native tree still decide charging, leniency and impulses.
        int landing=lead+1;
        long origin=Stopwatch.GetTimestamp()-Stopwatch.Frequency*10;
        long step=Stopwatch.Frequency*17/1000;
        using(JumpEvents.Subscribe(value=>result.Results.Add(value)))
        try
        {
            player.m_body.Velocity=new Vector2(2,2);
            for(frame=0;frame<landing+release+lag+5;frame++)
            {
                long stamp=origin+frame*step;
                if(frame==1) sampler.Edges.Enqueue(new JumpInputTransition(true,stamp));
                if(frame==landing+release) sampler.Edges.Enqueue(new JumpInputTransition(false,stamp-step/3));
                int nativePress=acceptLag==0 ? 1 : landing+acceptLag;
                bufferHeld=frame>=nativePress && frame<landing+release+lag;
                bufferPressed=frame==nativePress;
                bool onGround=frame>=landing && (!leave || frame==landing);
                ground.SetValue(player.m_body,onGround);
                blocks[typeof(IceBlock)].IsPlayerOnBlock=ice && onGround;
                blocks[typeof(WaterBlock)].IsPlayerOnBlock=water;
                if(frame==landing) player.m_body.Velocity.Y=0;
                AccessTools.Method(typeof(InputComponent),"Update").Invoke(input,new object[]{1f/60});
                clock.BeginFrame(stamp);
                try { tree.GetRaw().Run(1f/60); } finally { clock.EndFrame(); }
                result.Horizontal.Add(player.m_body.Velocity.X);
                result.Charging.Add(ReferenceEquals(AccessTools.Field(typeof(PlayerEntity),"m_sprite").GetValue(player),Game1.instance.contentManager.playerSprites.jump_charge));
            }
        }
        finally { if(binding!=null) binding.Restore(); sampler.Dispose(); }
        return result;
    }
}
