using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

internal static partial class FullModPhysicsGame
{
    private static int edgeDirection;
    private static void EdgeControls(ref InputComponent.State __result)
    { __result=new InputComponent.State {left=edgeDirection<0,right=edgeDirection>0}; }
    private static float Neighbour(float value,int ulps)
    { return BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(value),0)+ulps),0); }
    private static IEnumerable<float> EdgeIntervals()
    { return new[]{1f/240,(float)(41667.0/166667/60),(float)(41667.0/Game1.instance.TargetElapsedTime.Ticks/60)}.Distinct(); }
    private static void VerifyFractionalEdges(PlayerEntity player)
    {
        var dynamics=Find("SubframeCharge.PhysicsDynamics");
        var lease=AccessTools.Field(physics,"lease").GetValue(null);
        Check(lease!=null,"Edge matrix requires real fractional player ownership");
        var begin=AccessTools.Method(dynamics,"Begin"); var end=AccessTools.Method(dynamics,"End");
        var reset=AccessTools.Method(dynamics,"Reset"); var advance=AccessTools.Method(lease.GetType(),"Advance");
        var input=AccessTools.Method(typeof(InputComponent),"GetState");
        var pressed=AccessTools.Method(typeof(InputComponent),"GetPressedState");
        var grounded=AccessTools.Field(typeof(BodyComp),"_is_on_ground");
        var screen=((LevelScreen[])AccessTools.Field(typeof(LevelManager),"m_screens").GetValue(null))[0];
        var windFlag=AccessTools.Field(typeof(LevelScreen),"m_wind_enabled"); bool oldWind=(bool)windFlag.GetValue(screen);
        var blocks=(IBlock[])AccessTools.Field(typeof(LevelScreen),"m_hitboxes").GetValue(screen);
        hooks.Patch(input,postfix:new HarmonyMethod(typeof(FullModPhysicsGame),"EdgeControls") {priority=Priority.Last});
        hooks.Patch(pressed,postfix:new HarmonyMethod(typeof(FullModPhysicsGame),"EdgeControls") {priority=Priority.Last});
        int cases=0;
        try {
            windFlag.SetValue(screen,false);
            SetPreference("NoWalkOff.Settings","NoWalkOff",true);
            foreach(float delta in EdgeIntervals())
            foreach(bool water in new[]{false,true})
            foreach(int boundary in new[]{32,64,128,180,256,384})
            foreach(int direction in new[]{-1,1})
            {
                float stride=PlayerValues.WALK_SPEED*(water?.5f:1)*delta*60;
                var starts=new List<float>();
                float crossing=boundary-direction*stride;
                foreach(int ulps in new[]{-16,-7,-2,-1,0,1,2,7,16}) starts.Add(Neighbour(crossing,ulps));
                foreach(float phase in new[]{.1f,.25f,.5f,.75f,.99f}) starts.Add(boundary-direction*(2+phase));
                foreach(float start in starts) {
                    var floor=direction>0?new Rectangle(0,300,boundary,60):new Rectangle(boundary+17,300,480-boundary-17,60);
                    SetBlocks(water?new IBlock[]{new BoxBlock(floor),new WaterBlock(new Rectangle(0,0,480,360))}:new IBlock[]{new BoxBlock(floor)});
                    player.m_body.Position=new Vector2(start,274); player.m_body.Velocity=new Vector2(0,PlayerValues.GRAVITY); grounded.SetValue(player.m_body,true);
                    reset.Invoke(null,null); edgeDirection=0;
                    Action step=delegate {
                        begin.Invoke(null,new object[]{player.m_body,delta});
                        try { advance.Invoke(lease,new object[]{delta}); } finally { end.Invoke(null,null); }
                    };
                    for(int i=0;i<4;i++) step();
                    edgeDirection=direction;
                    float expected=direction>0?boundary-1:boundary;
                    for(int i=0;i<50;i++) {
                        step(); Check(player.m_body.IsOnGround,"Edge matrix fell: boundary="+boundary+" direction="+direction+" start="+start.ToString("R")+" water="+water+" delta="+delta.ToString("R")+" position="+player.m_body.Position);
                    }
                    Check(player.m_body.Position.X==expected,"Edge matrix stopped at wrong coordinate: "+player.m_body.Position+" expected="+expected);
                    edgeDirection=0; for(int i=0;i<4;i++) step();
                    edgeDirection=direction; for(int i=0;i<32 && player.m_body.IsOnGround;i++) step();
                    Check(!player.m_body.IsOnGround,"Edge matrix blocked release/repress: boundary="+boundary+" direction="+direction);
                    cases++;
                }
            }
            Record("EDGE MATRIX "+cases+" cases: six boundaries, both directions, adjacent floats and fractional starts, dry/water, exact and scheduled steps, hold/repress");
            VerifyExternalEdges(player,dynamics,lease,advance,begin,end,reset);
            VerifyMaterialContacts(player,dynamics,begin,end,reset);
        }
        finally {
            edgeDirection=0; hooks.Unpatch(input,HarmonyPatchType.Postfix,hooks.Id); hooks.Unpatch(pressed,HarmonyPatchType.Postfix,hooks.Id);
            windFlag.SetValue(screen,oldWind); SetBlocks(blocks); reset.Invoke(null,null);
            SetPreference("NoWalkOff.Settings","NoWalkOff",false);
        }
    }
    private sealed class EdgeCarry : IBlockBehaviour
    {
        internal float Speed;
        internal int Calls;
        public float BlockPriority {get{return 1;}}
        public bool IsPlayerOnBlock {get;set;}
        public float ModifyXVelocity(float v,BehaviourContext c){Calls++;return v+Speed;}
        public float ModifyYVelocity(float v,BehaviourContext c){return v;}
        public float ModifyGravity(float v,BehaviourContext c){return v;}
        public bool ExecuteBlockBehaviour(BehaviourContext c){return true;}
        public bool AdditionalXCollisionCheck(AdvCollisionInfo i,BehaviourContext c){return false;}
        public bool AdditionalYCollisionCheck(AdvCollisionInfo i,BehaviourContext c){return false;}
    }
    private sealed class EdgeOffset : IBodyCompBehaviour
    {
        internal float Distance;
        public bool ExecuteBehaviour(BehaviourContext c){c.BodyComp.Position.X+=Distance;return true;}
    }
    private struct EdgeFrame
    {
        internal Vector2 Position,Velocity;
        internal bool Grounded;
        internal int CarryCalls;
    }
    private static void VerifyExternalEdges(PlayerEntity player,Type dynamics,object lease,System.Reflection.MethodInfo advance,System.Reflection.MethodInfo begin,System.Reflection.MethodInfo end,System.Reflection.MethodInfo reset)
    {
        var screen=((LevelScreen[])AccessTools.Field(typeof(LevelManager),"m_screens").GetValue(null))[0];
        var flag=AccessTools.Field(typeof(LevelScreen),"m_wind_enabled");
        var windType=typeof(BodyComp).Assembly.GetTypes().Single(t=>t.Name=="WindManager");
        var windMethod=windType.GetMethods(JKRuntimeFlags()).Single(m=>m.Name.EndsWith("GetCurrentVelocity",StringComparison.Ordinal));
        hooks.Patch(windMethod,prefix:new HarmonyMethod(typeof(FullModPhysicsGame),"ContactWind"));
        var stages=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(player.m_body);
        var handlers=(LinkedList<IBlockBehaviour>)AccessTools.Field(typeof(BodyComp),"m_blockBehaviours").GetValue(player.m_body);
        int cases=0;
        try {
            foreach(float delta in EdgeIntervals())
            foreach(string kind in new[]{"wind","ice","carry","offset"})
            foreach(int direction in new[]{-1,1})
            foreach(bool held in new[]{false,true}) {
                var expected=new List<EdgeFrame>();
                foreach(bool guarded in new[]{false,true}) {
                    SetPreference("NoWalkOff.Settings","NoWalkOff",guarded);
                    var floor=direction>0?new Rectangle(0,300,128,60):new Rectangle(145,300,335,60);
                    SetBlocks(kind=="ice"?(IBlock)new IceBlock(floor):new BoxBlock(floor));
                    flag.SetValue(screen,false); contactWind=0; edgeDirection=0;
                    player.m_body.Position=new Vector2(direction>0?127.98f:128.02f,274); player.m_body.Velocity=new Vector2(0,PlayerValues.GRAVITY);
                    AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(player.m_body,true); reset.Invoke(null,null);
                    Action step=delegate {begin.Invoke(null,new object[]{player.m_body,delta});try{advance.Invoke(lease,new object[]{delta});}finally{end.Invoke(null,null);}};
                    for(int i=0;i<4;i++) step();
                    edgeDirection=held?direction:0;
                    var carry=new EdgeCarry {Speed=kind=="carry"?direction*.2f:0};
                    var offset=new EdgeOffset {Distance=kind=="offset"?direction*.05f:0};
                    if(kind=="carry") handlers.AddLast(carry);
                    if(kind=="offset") stages.AddBefore(stages.Find(stages.Single(b=>b is UpdateXPositionFromVelocityBehaviour)),offset);
                    if(kind=="ice") player.m_body.Velocity.X=direction*3.5f;
                    if(kind=="wind") {flag.SetValue(screen,true);contactWind=direction*.03f;}
                    bool left=false;
                    try {
                        for(int i=0;i<240;i++) {
                            step(); var frame=new EdgeFrame {Position=player.m_body.Position,Velocity=player.m_body.Velocity,Grounded=player.m_body.IsOnGround,CarryCalls=carry.Calls};
                            if(!guarded) expected.Add(frame);
                            else Check(i<expected.Count && frame.Position==expected[i].Position && frame.Velocity==expected[i].Velocity && frame.Grounded==expected[i].Grounded && frame.CarryCalls==expected[i].CarryCalls,
                                "No Walk Off changed external "+kind+" dir="+direction+" held="+held+" step="+i+" position="+frame.Position);
                            if(!frame.Grounded) {left=true;break;}
                        }
                        Check(left,"External "+kind+" never left the edge; dir="+direction+" held="+held);
                    } finally {handlers.Remove(carry);stages.Remove(offset);}
                }
                cases++;
            }
            Record("EXTERNAL EDGE PARITY "+cases+" cases: native wind, ice inertia, additive carry and direct offsets; held/neutral, both directions and all tested step intervals");
        } finally {hooks.Unpatch(windMethod,HarmonyPatchType.Prefix,hooks.Id);flag.SetValue(screen,false);contactWind=0;edgeDirection=0;}
    }
    private static System.Reflection.BindingFlags JKRuntimeFlags()
    {return System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;}
    private static void VerifyMaterialContacts(PlayerEntity player,Type dynamics,System.Reflection.MethodInfo begin,System.Reflection.MethodInfo end,System.Reflection.MethodInfo reset)
    {
        var bodyStep=AccessTools.Method(typeof(BodyComp),"UpdateInternal");
        var lease=AccessTools.Field(physics,"lease").GetValue(null);var advance=AccessTools.Method(lease.GetType(),"Advance");
        var grounded=AccessTools.Field(typeof(BodyComp),"_is_on_ground");
        SetPreference("NoWalkOff.Settings","NoWalkOff",false);edgeDirection=0;
        int cases=0;
        foreach(string name in new[]{typeof(IceBlock).FullName,"JumpKing_Expansion_Blocks.Blocks.SlipperyIce","JumpKing_Expansion_Blocks.Blocks.ZeroFriction","JumpKing_Expansion_Blocks.Blocks.CursedIce","MovementControl.Blocks.BlockMomentumStopSolid"})
        foreach(int phaseOffset in new[]{0,1,2,3})
        foreach(int direction in new[]{-1,1}) {
            var blockType=Find(name);Check(blockType!=null,"Reported block isn't loaded: "+name);
            SetBlocks((IBlock)Activator.CreateInstance(blockType,new object[]{new Rectangle(0,300,480,60)}));
            player.m_body.Enabled=true; AccessTools.Field(typeof(BodyComp),"_knocked").SetValue(player.m_body,false);
            reset.Invoke(null,null);player.m_body.Position=new Vector2(150,200);player.m_body.Velocity=Vector2.Zero;grounded.SetValue(player.m_body,false);
            Action<bool> step=delegate(bool controls){begin.Invoke(null,new object[]{player.m_body,1f/240});try{if(controls)advance.Invoke(lease,new object[]{1f/240});else bodyStep.Invoke(player.m_body,new object[]{1f/240});}finally{end.Invoke(null,null);}};
            for(int i=0;i<4+phaseOffset;i++) step(false);
            player.m_body.Position=new Vector2(150,274.875f);player.m_body.Velocity=new Vector2(direction*3.5f,1);grounded.SetValue(player.m_body,false);
            step(true);
            Check(player.m_body.IsOnGround,"Landing didn't touch "+name+" phase="+phaseOffset+" pos="+player.m_body.Position+" velocity="+player.m_body.Velocity+" enabled="+player.m_body.Enabled);
            bool stop=name.Contains("MomentumStop");
            Check(stop?player.m_body.Velocity.X==0:Math.Abs(player.m_body.Velocity.X)>3,"First contact lost its effect: "+name+" phase="+phaseOffset+" velocity="+player.m_body.Velocity);
            float beforeSpeed=Math.Abs(player.m_body.Velocity.X);
            for(int i=0;i<20;i++) step(false);
            float lost=beforeSpeed-Math.Abs(player.m_body.Velocity.X);
            if(!stop) {
                float expected=name.Contains("ZeroFriction")?0:name.Contains("SlipperyIce")?PlayerValues.ICE_FRICTION*5/3:PlayerValues.ICE_FRICTION*5;
                Check(Math.Abs(lost-expected)<=PlayerValues.ICE_FRICTION+.001f,"Material friction changed tempo: "+name+" phase="+phaseOffset+" lost="+lost+" expected="+expected);
            }
            cases++;
        }
        Record("MATERIAL CONTACTS "+cases+" cases: native ice, three Expansion ice types and solid Momentum Stop; landing in all four phases, both directions and friction tempo");
    }
}
