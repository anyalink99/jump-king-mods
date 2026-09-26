using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.Simulation;
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
        private static LevelScreen[] TeleportScene(bool two)
        {
            return Enumerable.Range(0,3).Select(i => new LevelScreen(i,
                new IBlock[] { new BoxBlock(new Rectangle(0,320-360*i,480,40)) }, new LevelScreen.Graphics(), false,
                i == 0 ? (two ? new[] { new TeleportLink(2), new TeleportLink(3) } : new[] { new TeleportLink(2) }) : new TeleportLink[0],0,null)).ToArray();
        }
        private static void TeleportParityAndBudgets()
        {
            var oldScreens=FlightWorld.InstalledScreens;
            int oldTotal=LevelManager.TotalScreens, oldCamera=Camera.CurrentScreen;
            try
            {
                int comparisons=0;
                foreach (bool two in new[] { false,true }) foreach (int direction in new[] { -1,1 }) foreach(bool continuation in new[]{false,true})
                {
                    var screens=TeleportScene(two);
                    typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
                    typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,screens.Length);
                    // Exact native stage parity includes fractional/negative Y
                    // and side boundaries where the centre, not the body X, wins.
                    foreach (float y in new[] { 294.75f, -65.25f, 0.75f, -360.75f })
                    {
                        typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
                        var expected=new BodyComp(new Vector2(direction<0?-10.25f:472.25f,y),18,26) { Velocity=new Vector2(direction*3.5f,-4) };
                        var source=new BodyComp(expected.Position,18,26); NativeFlight.CopyState(expected,source);
                        var world=new FlightWorld(screens,0,0);
                        var shadow=NativeFlight.CreateShadow(source,world);
                        var expectedContext=NativeFlight.Get<BehaviourContext>(expected,"m_behaviourContext");
                        new HandlePlayerTeleportBehaviour().ExecuteBehaviour(expectedContext);
                        Require(expectedContext.ContainsKey(HandlePlayerTeleportBehaviour.TeleportedPlayerFlag),"Native fixture did not activate a teleport");
                        int nativeScreen=Camera.CurrentScreen;
                        world.HandleTeleport(NativeFlight.Get<BehaviourContext>(shadow,"m_behaviourContext"));
                        Require(shadow.Position==expected.Position && shadow.Velocity==expected.Velocity && world.Screen==nativeScreen,
                            "Teleport offset/truncation/camera differs from installed native stage");
                        Require(Camera.CurrentScreen==nativeScreen && source.Position==new Vector2(direction<0?-10.25f:472.25f,y),"Teleport forecast touched live state");
                        comparisons++;
                    }
                    typeof(Camera).GetField("_current_screen",Flags).SetValue(null,0);
                    var real=new BodyComp(new Vector2(direction<0?-8:470,230),18,26) { Velocity=new Vector2(direction*3.5f,-4) };
                    var routeWorld=new FlightWorld(screens,0,0);
                    var route=NativeFlight.CreateShadow(real,routeWorld);
                    var pipeline=NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(real,"m_behaviours");
                    foreach(var stage in pipeline.ToArray()) if(stage.GetType().Name=="PlayBumpSFXBehaviour" || stage.GetType().Name=="WaterParticleSpawningBehaviour") pipeline.Remove(stage);
                    var capture=new CaptureAtX { World=routeWorld };
                    if(continuation) pipeline.AddBefore(pipeline.Find(pipeline.Single(s=>s is UpdateXPositionFromVelocityBehaviour)),capture);
                    int tick;
                    for(tick=0;tick<200;tick++)
                    {
                        typeof(BodyComp).GetMethod("UpdateInternal",Flags).Invoke(real,new object[]{1f/60f});
                        if(real.IsOnGround) Camera.UpdateCamera(real.GetHitbox().Center);
                        else Camera.UpdateCameraWithVelocity(real.GetHitbox().Center,real.Velocity);
                        int liveCamera=Camera.CurrentScreen;
                        if(continuation && tick==0) { route=capture.Shadow; NativeFlightSimulation.AdvanceShadowFromX(route); }
                        else NativeFlightSimulation.AdvanceShadow(route,1f/60f);
                        if(!route.IsOnGround) routeWorld.AdvanceCamera(route);
                        Require(real.Position==route.Position && real.Velocity==route.Velocity && real.IsOnGround==route.IsOnGround
                            && real.LastVelocity==route.LastVelocity && real.LastScreen==route.LastScreen,"Teleport trajectory differs at native tick "+tick);
                        Require(Camera.CurrentScreen==liveCamera,"Forecast moved live camera");
                        if(real.IsOnGround) break;
                    }
                    Require(tick<200,"Teleport parity route did not land");
                }
                ForecastBudgets();
                Console.WriteLine("[OK] Native teleport parity: "+comparisons+" offsets, single/two links, both directions, fractional Y and eight complete flights (including X continuation); bounded failures");
            }
            finally
            {
                typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,oldScreens);
                typeof(LevelManager).GetField("_total_screens",Flags).SetValue(null,oldTotal);
                typeof(Camera).GetField("_current_screen",Flags).SetValue(null,oldCamera);
            }
        }
        private static void ForecastBudgets()
        {
            var source=new BodyComp(new Vector2(180,180),18,26) { Velocity=new Vector2(0,4) };
            var job=new FlightJob(new FlightJob.Seed(source,new FlightWorld(Scene(new IBlock[0]),0,0)));
            for(int i=0;i<=FlightJob.MaxSlices && !job.Done;i++) job.Step(1);
            Require(job.Failure!=null && job.Landing==null && job.Slices<=FlightJob.MaxSlices+1,"Unfinished flight has no slice bound");
            Require(source.Position==new Vector2(180,180) && source.Velocity==new Vector2(0,4),"Budget refusal changed launch body");
            source.Velocity=new Vector2(float.NaN,0);
            job=new FlightJob(new FlightJob.Seed(source,new FlightWorld(Scene(new IBlock[0]),0,0))); job.Step();
            Require(job.Failure!=null && job.Ticks==0,"Non-finite velocity entered native collision code");
            var screens=new[] { new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),false,new[]{new TeleportLink(1)},0,null) };
            var loop=new FlightWorld(screens,0,0);
            var body=new BodyComp(new Vector2(472,180),18,26);
            var context=NativeFlight.Get<BehaviourContext>(body,"m_behaviourContext");
            for(int i=0;i<80;i++) { body.Position=new Vector2(472,180); loop.HandleTeleport(context); }
            bool refused=false;
            Require(body.Position.X==-8,"Valid teleports hit an artificial crossing limit");
            screens[0]=new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),false,new[]{new TeleportLink(999)},0,null);
            refused=false;
            body.Position=new Vector2(472,180);
            try { new FlightWorld(screens,0,0).HandleTeleport(context); } catch(InvalidOperationException) { refused=true; }
            Require(refused,"Invalid teleport target accepted");
            refused=false;
            try { new FlightIndex(screens[0],new IBlock[]{new BoxBlock(new Rectangle(0,0,int.MaxValue,int.MaxValue))}); } catch(InvalidOperationException) { refused=true; }
            Require(refused,"Oversized collision index accepted");
            var index=new FlightIndex(screens[0],new IBlock[0]); refused=false;
            try { index.Select(new Rectangle(0,0,100000,100000)); } catch(InvalidOperationException) { refused=true; }
            Require(refused,"Oversized collision query accepted");
        }
    }
}
