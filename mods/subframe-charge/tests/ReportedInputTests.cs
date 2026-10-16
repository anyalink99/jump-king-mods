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
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private sealed class ReportResume : IBTnode
    {
        protected override BTresult MyRun(TickData data)
        {
            var state=ControllerManager.instance.MenuController.GetPadState();
            if(state.confirm || state.pause)
                AccessTools.Field(MenuCadence.Pause,"_paused").SetValue(AccessTools.Field(MenuCadence.Pause,"instance").GetValue(null),false);
            return BTresult.Running;
        }
    }
    private sealed class ReportRun
    {
        internal readonly List<int> Directions=new List<int>();
        internal readonly List<bool> Poses=new List<bool>();
        internal readonly List<bool> Holds=new List<bool>();
        internal readonly List<bool> Charges=new List<bool>();
        internal readonly List<int> LaunchFrames=new List<int>();
    }
    private static void ReportedInputTests()
    {
        foreach(bool keyboard in new[]{false,true})
        foreach(bool sand in new[]{false,true})
        foreach(int direction in new[]{-1,1})
        foreach(int scenario in new[]{0,1,2,3,4,5})
        foreach(bool correction in new[]{false,true})
        {
            var native=RunReportedInput(false,keyboard,sand,scenario,correction,false,direction);
            foreach(bool refresh in new[]{false,true})
            {
                var actual=RunReportedInput(true,keyboard,sand,scenario,correction,refresh,direction);
                string context="keyboard="+keyboard+" sand="+sand+" scenario="+scenario+" direction="+direction+" correction="+correction+" refresh="+refresh;
                Check(actual.Poses.SequenceEqual(native.Poses),"Charge animation differs from native: "+context);
                Check(actual.Holds.SequenceEqual(native.Holds),"Held directions changed across pause: "+context);
                Check(actual.Charges.SequenceEqual(native.Charges),"Native charge lifetime changed: "+context);
                Check(actual.LaunchFrames.SequenceEqual(native.LaunchFrames),"Native launch frame changed: "+context);
                Check(actual.Directions.SequenceEqual(native.Directions),"Sideways jump lost its native direction: "+context);
                if(scenario!=2) Check(native.Directions.Count>0 && native.Directions.All(x=>x==direction),"Report fixture didn't exercise a sideways jump: "+context);
                if(scenario==1) Check(native.Poses.Any(x=>x),"Unpause fixture didn't enter the native charge pose");
            }
        }
        Console.WriteLine("[OK] Reported input: native sand directions, pause-held movement, Jump-to-resume charge poses and interrupted charges");
    }
    private static ReportRun RunReportedInput(bool accelerated,bool keyboard,bool sand,int scenario,bool correction,bool refresh,int direction)
    {
        var fixture=new Harmony("sfc.reported-input.tests"); var game=MakeGame();
        fixture.Patch(AccessTools.Method(typeof(PadInstance),"GetPadState"),transpiler:new HarmonyMethod(typeof(PerformanceTests),"HeadlessTick"));
        fixture.Patch(AccessTools.Method(typeof(ControllerManager),"Update"),transpiler:new HarmonyMethod(typeof(PerformanceTests),"HeadlessController"));
        fixture.Patch(AccessTools.Method(typeof(ResponsiveInput),"Poll"),transpiler:new HarmonyMethod(typeof(PerformanceTests),"HeadlessTick"));
        fixture.Patch(AccessTools.Method(typeof(MenuCadence),"Pump"),transpiler:new HarmonyMethod(typeof(PerformanceTests),"HeadlessMenu"));
        fixture.Patch(AccessTools.Method(typeof(JKRuntime.UI.UiPointer),"Update"),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"){priority=Priority.First});
        fixture.Patch(AccessTools.Method(typeof(GameLoop),"OnResume"),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        fixture.Patch(AccessTools.Method(typeof(GameLoop),"OnPause"),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        fixture.Patch(AccessTools.Method(typeof(SubframeKeyboard),"Refresh"),prefix:new HarmonyMethod(typeof(PerformanceTests),"KeepKeyboardWorker"));
        foreach(var pair in new[]{new[]{"IsGameActive","BufferTrue"},new[]{"ConfigureSampler","BufferTrue"},new[]{"HasUnsupportedJumpDown","BufferFalse"}})
            fixture.Patch(AccessTools.Method(typeof(SubframeChargeState),pair[0]),prefix:new HarmonyMethod(typeof(PerformanceTests),pair[1]));
        foreach(var type in new[]{typeof(JumpState),typeof(IsOnGround)})
            foreach(string method in new[]{"HandleSounds","HandleParticles"})
                fixture.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(PerformanceTests),"Skip"));
        game.contentManager=(JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
        game.contentManager.playerSprites=new JKContentManager.PlayerSprites { _CurrentSprites=new JumpKing.JKMemory.LayeredKingSprites(null) };
        game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
        Check(!ReferenceEquals(game.contentManager.playerSprites.idle,game.contentManager.playerSprites.jump_charge),"Charge pose fixture needs distinct sprites");
        if(EntityManager.instance==null) new EntityManager();
        var manager=(ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager)); ControllerManager.instance=manager;
        var device=new Pad { Identifier=keyboard ? "pc_keyboard_jump_king" : "fixture", Binding=new PadBinding {left=new[]{1},right=new[]{2},jump=new[]{3},confirm=new[]{3},pause=new[]{4}} };
        var pad=new PadInstance(device);
        AccessTools.Field(typeof(ControllerManager),"m_pads").SetValue(manager,new List<PadInstance>{pad});
        AccessTools.Field(typeof(ControllerManager),"_menu_controller").SetValue(manager,new MenuController(manager));
        var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        AccessTools.Field(typeof(Entity),"m_components").SetValue(player,new List<Component>());
        player.m_body=new BodyComp(new Vector2(150,268),18,26);
        AccessTools.Field(typeof(PlayerEntity),"m_screen_shake").SetValue(player,FormatterServices.GetUninitializedObject(typeof(JumpKing.MiscSystems.ScreenShakeController)));
        var tree=(BehaviorTreeComp)AccessTools.Method(typeof(PlayerEntity),"MakeBT").Invoke(player,null);
        var input=new InputComponent(); player.AddComponents(player.m_body,input,tree);
        var original=tree.GetRaw().FindNode<JumpState>(); var sampler=new BufferSampler(); var clock=new ChargeTimeline();
        JumpState jumpNode=original; JumpNodeBindings binding=null;
        if(correction)
        {
            var replacement=(SubframeChargeState)FormatterServices.GetUninitializedObject(typeof(SubframeChargeState));
            for(Type type=typeof(JumpState);type!=null && typeof(IBTnode).IsAssignableFrom(type);type=type.BaseType)
                foreach(var field in type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                    field.SetValue(replacement,field.GetValue(original));
            AccessTools.Field(typeof(SubframeChargeState),"Timeline").SetValue(replacement,clock);
            AccessTools.Field(typeof(SubframeChargeState),"sampler").SetValue(replacement,sampler);
            AccessTools.Field(typeof(SubframeChargeState),"stepMilliseconds").SetValue(replacement,17.0);
            AccessTools.Field(typeof(SubframeChargeState),"lastGameActive").SetValue(replacement,true);
            var probe=(JumpTrajectoryProbe)FormatterServices.GetUninitializedObject(typeof(JumpTrajectoryProbe));
            AccessTools.Field(typeof(JumpTrajectoryProbe),"body").SetValue(probe,player.m_body);
            AccessTools.Field(typeof(SubframeChargeState),"trajectoryProbe").SetValue(replacement,probe);
            binding=JumpNodeBindings.Replace(tree.GetRaw(),player,original,replacement); jumpNode=replacement;
        }
        var pause=(Entity)FormatterServices.GetUninitializedObject(MenuCadence.Pause);
        AccessTools.Field(typeof(Entity),"m_components").SetValue(pause,new List<Component>());
        var pauseTree=new BehaviorTreeComp(new ReportResume()); pause.AddComponents(pauseTree);
        AccessTools.Field(MenuCadence.Pause,"m_bt_manager").SetValue(pause,pauseTree);
        AccessTools.Field(MenuCadence.Pause,"instance").SetValue(null,pause);
        AccessTools.Field(MenuCadence.Pause,"_paused").SetValue(pause,scenario==1 || scenario==2);
        AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,FormatterServices.GetUninitializedObject(typeof(JumpGame)));
        AccessTools.Field(typeof(GameLoop),"_instance").SetValue(null,FormatterServices.GetUninitializedObject(typeof(GameLoop))); GameLoop.m_player=player; menuPlaying=true;
        SettingsStore.Current.Enabled=correction; SettingsStore.Current.SetInputs(accelerated); SettingsStore.Current.HighRefresh=refresh;
        if(accelerated) { PerformanceFeatures.Install(); FeatureClock.BeforeTick(game); }
        var actions=new int[11][][]; for(int i=0;i<actions.Length;i++)actions[i]=new int[0][];
        actions[2]=new[]{new[]{1}}; actions[3]=new[]{new[]{2}}; actions[4]=actions[6]=new[]{new[]{3}}; actions[5]=new[]{new[]{4}};
        using(var worker=new KeyboardActionEdges(actions,new IntPtr(1),key=>(short)(device.Buttons.Contains(key)?-32768:0),()=>new IntPtr(1),false))
        {
            if(accelerated && keyboard) { AccessTools.Field(typeof(SubframeKeyboard),"worker").SetValue(null,worker); AccessTools.Field(typeof(SubframeKeyboard),"owner").SetValue(null,pad); }
            var result=new ReportRun(); int frame=0; bool wasJump=false;
            var oldCallback=PlayerEntity.OnJumpCall;
            PlayerEntity.OnJumpCall=()=> { result.Directions.Add(Math.Sign(player.m_body.Velocity.X)); result.LaunchFrames.Add(frame); };
            try
            {
                for(frame=0;frame<25;frame++)
                {
                    bool heldJump=scenario!=2 && frame>=(scenario==1 ? 5 : 4) && frame<17;
                    // late direction and release just before takeoff exercise the native four-frame buffer
                    bool heldDirection=frame>=(scenario==4 ? 16 : 2) && (scenario!=5 || frame<15);
                    device.Buttons=(heldDirection ? new[]{direction<0 ? 1 : 2} : new int[0]).Concat(heldJump ? new[]{3} : new int[0]).Concat(scenario==2 && frame==5 || scenario==3 && frame==12 ? new[]{4} : new int[0]).ToArray();
                    if(scenario==3 && frame==7) AccessTools.Field(MenuCadence.Pause,"_paused").SetValue(pause,true);
                    long stamp=Stopwatch.GetTimestamp();
                    if(wasJump!=heldJump) sampler.Edges.Enqueue(new JumpInputTransition(heldJump,stamp)); wasJump=heldJump;
                    clock.ObservePause((bool)AccessTools.Field(MenuCadence.Pause,"_paused").GetValue(pause),stamp);
                    if(accelerated) { ResponsiveInput.Poll(); MenuCadence.Pump(1f/240); }
                    manager.Update();
                    if(!accelerated) AccessTools.Method(MenuCadence.Pause,"PauseUpdate").Invoke(pause,new object[]{1f/60});
                    bool paused=(bool)AccessTools.Field(MenuCadence.Pause,"_paused").GetValue(pause);
                    clock.ObservePause(paused,Stopwatch.GetTimestamp());
                    if(paused)continue;
                    AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(player.m_body,!sand);
                    ((Dictionary<Type,JumpKing.API.IBlockBehaviour>)AccessTools.Field(typeof(BodyComp),"m_blockBehaviourLookup").GetValue(player.m_body))[typeof(SandBlock)].IsPlayerOnBlock=sand;
                    AccessTools.Method(typeof(InputComponent),"Update").Invoke(input,new object[]{1f/60});
                    clock.BeginFrame(Stopwatch.GetTimestamp());
                    try { tree.GetRaw().Run(1f/60); } finally { clock.EndFrame(); }
                    result.Holds.Add(direction<0 ? input.GetState().left : input.GetState().right);
                    result.Charges.Add(jumpNode.IsRunning());
                    result.Poses.Add(ReferenceEquals(AccessTools.Field(typeof(PlayerEntity),"m_sprite").GetValue(player),game.contentManager.playerSprites.jump_charge));
                }
                return result;
            }
            finally
            {
                PlayerEntity.OnJumpCall=oldCallback; if(binding!=null)binding.Restore(); sampler.Dispose();
                PerformanceFeatures.Uninstall(); fixture.UnpatchAll(fixture.Id); ControllerManager.instance=null; GameLoop.m_player=null; AccessTools.Field(typeof(GameLoop),"_instance").SetValue(null,null);
                AccessTools.Field(MenuCadence.Pause,"instance").SetValue(null,null); AccessTools.Field(typeof(JumpGame),"_instance").SetValue(null,null); AccessTools.Field(typeof(Game1),"_instance").SetValue(null,null);
            }
        }
    }
}
