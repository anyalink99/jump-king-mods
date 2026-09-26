using System;
using System.Collections.Generic;
using System.Linq;
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
        private sealed class ExternalForce : IBodyCompBehaviour
        {
            internal float Force;
            public bool ExecuteBehaviour(BehaviourContext context) { context.BodyComp.Velocity.X+=Force; return true; }
        }
        private sealed class ExternalCarry : IBlockBehaviour
        {
            internal float Speed;
            internal int Calls;
            public float BlockPriority { get { return 2; } }
            public bool IsPlayerOnBlock { get; set; }
            public float ModifyXVelocity(float input, BehaviourContext context) { Calls++; return input+Speed; }
            public float ModifyYVelocity(float input, BehaviourContext context) { return input; }
            public float ModifyGravity(float input, BehaviourContext context) { return input; }
            public bool ExecuteBlockBehaviour(BehaviourContext context) { return true; }
            public bool AdditionalXCollisionCheck(AdvCollisionInfo info, BehaviourContext context) { return false; }
            public bool AdditionalYCollisionCheck(AdvCollisionInfo info, BehaviourContext context) { return false; }
        }
        private static void AddForce(PlayerEntity player, ExternalForce force)
        {
            var list=NativeFlight.Get<LinkedList<IBodyCompBehaviour>>(player.m_body,"m_behaviours");
            list.AddBefore(list.Find(list.Single(b=>b is UpdateXPositionFromVelocityBehaviour)),force);
        }
        private static void AddCarry(PlayerEntity player, ExternalCarry carry)
        { NativeFlight.Get<LinkedList<IBlockBehaviour>>(player.m_body,"m_blockBehaviours").AddLast(carry); }
        private static void NoWalkOffExternalMotion(WalkInputFixture input)
        {
            MapPixels.NoWalkOff.Screens.Add(0);
            foreach(bool carry in new[]{false,true}) foreach(int direction in new[]{-1,1}) foreach(bool held in new[]{false,true})
            {
                WalkScene(new BoxBlock(new Rectangle(80,320,160,40)));
                var expected=NoWalkPlayer(); var actual=NoWalkPlayer();
                expected.m_body.Position.X=actual.m_body.Position.X=150;
                var expectedCarry=new ExternalCarry { Speed=direction*.5f }; var actualCarry=new ExternalCarry { Speed=direction*.5f };
                using(var guard=new NoWalkOffController(actual))
                {
                    if(carry) { AddCarry(expected,expectedCarry); AddCarry(actual,actualCarry); }
                    else { AddForce(expected,new ExternalForce { Force=direction*.5f }); AddForce(actual,new ExternalForce { Force=direction*.5f }); }
                    bool fell=false;
                    for(int tick=0;tick<350;tick++)
                    {
                        input.Direction(held?direction:0); expected.UpdateComponents(1f/60f); actual.UpdateComponents(1f/60f);
                        Require(actual.m_body.Position==expected.m_body.Position && actual.m_body.Velocity==expected.m_body.Velocity
                            && actual.m_body.IsOnGround==expected.m_body.IsOnGround,"External movement was caught by No Walk Off: carry="+carry+" held="+held);
                        Require(actualCarry.Calls==expectedCarry.Calls,"Movement observer replayed a stateful block callback");
                        if(actual.m_body.Position.Y>295) { fell=true; break; }
                    }
                    Require(fell,"External force/carry could not leave the platform");
                }
            }
            WalkScene(new BoxBlock(new Rectangle(80,320,160,40)));
            var player=NoWalkPlayer(); player.m_body.Position.X=150;
            var conveyor=new ExternalCarry(); AddCarry(player,conveyor);
            using(var guard=new NoWalkOffController(player))
            {
                WalkTicks(player,input,1,100);
                Require(player.m_body.Position.X==239,"Inactive foreign callback disabled ordinary edge protection");
                conveyor.Speed=.5f;
                var reference=NoWalkPlayer(); NativeFlight.CopyState(player.m_body,reference.m_body);
                AddCarry(reference,new ExternalCarry { Speed=.5f });
                for(int i=0;i<10;i++) {
                    input.Direction(1); reference.UpdateComponents(1f/60f); player.UpdateComponents(1f/60f);
                    Require(player.m_body.Position==reference.m_body.Position && player.m_body.Velocity==reference.m_body.Velocity,
                        "New external motion remained caught in an existing edge latch");
                }
            }
            Console.WriteLine("[OK] One motion guard: additive wind-like velocity and stateful conveyor-like offsets, held/neutral input, both directions, inactive callbacks and release of an existing edge latch");
            NoWalkOffNativeWind(input);
            NoWalkOffUnknownScale(input);
            NoWalkOffDeclaredSupport(input);
        }
        private static void NoWalkOffDeclaredSupport(WalkInputFixture input)
        {
            using(JKRuntime.RuntimeApi.Materials.Register("test.foreign-support",new JKRuntime.Gameplay.MaterialCapabilities(
                typeof(UnknownProvider.DeclaredOneWay),JKRuntime.Gameplay.SpeedCapability.Identity,
                JKRuntime.Gameplay.SupportPredicates.TopFace,captureState:UnknownProvider.DeclaredOneWay.Capture)))
            foreach(int direction in new[]{-1,1})
            {
                var floor=new UnknownProvider.DeclaredOneWay(new Rectangle(80,320,160,40)); WalkScene(floor);
                var player=NoWalkPlayer(); player.m_body.Position.X=150;
                player.m_body.RegisterBlockBehaviour(typeof(UnknownProvider.DeclaredOneWay),new UnknownProvider.DeclaredOneWayBehaviour());
                using(var guard=new NoWalkOffController(player))
                {
                    WalkTicks(player,input,direction,200);
                    Require(player.m_body.IsOnGround && player.m_body.Position.X==(direction>0?239:63),"Declared foreign support missed edge");
                    Require(floor.Reads>0,"Declared capture was unused");
                    WalkTicks(player,input,0,5); WalkTicks(player,input,direction,20);
                    Require(!player.m_body.IsOnGround,"Declared support blocked repress");
                }
                player=NoWalkPlayer();player.m_body.Position.X=150;
                player.m_body.RegisterBlockBehaviour(typeof(UnknownProvider.DeclaredOneWay),new UnknownProvider.DeclaredOneWayBehaviour());
                using(var guard=new NoWalkOffController(player))
                {
                    WalkTicks(player,input,direction,200);floor.Active=false;WalkTicks(player,input,direction,10);
                    Require(!player.m_body.IsOnGround,"Disabled provider support remained latched");
                }
            }
            Require(JKRuntime.RuntimeApi.Materials.Resolve(typeof(UnknownProvider.DeclaredOneWay))==null,"Foreign declaration leaked after disposal");
            Console.WriteLine("[OK] Declared unfamiliar one-way: both edges, release/repress, live state changes and owned cleanup");
        }
        private static void NoWalkOffUnknownScale(WalkInputFixture input)
        {
            foreach(int direction in new[]{-1,1}) foreach(bool water in new[]{false,true})
            {
                var blocks=new List<IBlock>{new BoxBlock(new Rectangle(80,320,160,40))};
                if(water)blocks.Add(new WaterBlock(new Rectangle(0,0,480,360)));
                WalkScene(blocks.ToArray());var player=NoWalkPlayer();player.m_body.Position.X=150;
                var medium=new UnknownProvider.SpeedMedium();player.m_body.RegisterBlockBehaviour(typeof(UnknownProvider.Material),medium);
                using(var guard=new NoWalkOffController(player))
                {
                    WalkTicks(player,input,direction,10);medium.Factor=1.7f;
                    WalkTicks(player,input,direction,190);
                    Require(player.m_body.IsOnGround && player.m_body.Position.X==(direction>0?239:63),"Unknown DLL speed scaling lost edge protection");
                    Require(medium.Calls==200 && medium.Reads==200,"Unknown DLL callback/getter was replayed");
                    WalkTicks(player,input,0,5);WalkTicks(player,input,direction,20);
                    Require(!player.m_body.IsOnGround,"Unknown scaling prevented second-press departure");
                }
            }
            Console.WriteLine("[OK] Unknown provider DLL: changing speed coefficients, water composition, both edges, release/repress and exactly one callback/getter per tick");
        }
        private static void NoWalkOffNativeWind(WalkInputFixture input)
        {
            var type=typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager",true);
            var instance=type.GetField("instance",Flags); var statsField=type.GetField("m_all_time_stats",Flags);
            var saved=instance.GetValue(null);
            try {
                var manager=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type); instance.SetValue(null,manager);
                typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime",Flags).SetValue(Game1.instance,TimeSpan.FromMilliseconds(17));
                foreach(int direction in new[]{-1,1}) foreach(bool held in new[]{false,true}) {
                    var screens=new[]{new LevelScreen(0,new IBlock[]{new BoxBlock(new Rectangle(80,320,160,40))},new LevelScreen.Graphics(),true,new TeleportLink[0],8,direction<0)};
                    typeof(LevelManager).GetField("m_screens",Flags).SetValue(null,screens);
                    var real=NoWalkPlayer(); var guarded=NoWalkPlayer(); real.m_body.Position.X=guarded.m_body.Position.X=direction>0?234:68;
                    bool fell=false;
                    using(var guard=new NoWalkOffController(guarded)) for(int tick=0;tick<700;tick++) {
                        var stats=Activator.CreateInstance(statsField.FieldType); stats.GetType().GetField("_ticks",Flags).SetValue(stats,180+tick); statsField.SetValue(manager,stats);
                        input.Direction(held?direction:0); real.UpdateComponents(1f/60f); guarded.UpdateComponents(1f/60f);
                        Require(real.m_body.Position==guarded.m_body.Position && real.m_body.Velocity==guarded.m_body.Velocity,
                            "No Walk Off changed actual native wind with held="+held);
                        if(real.m_body.Position.Y>295) { fell=true; break; }
                    }
                    Require(fell,"Native wind fixture never left support");
                }
                Console.WriteLine("[OK] No Walk Off preserves installed native wind in both directions, with held and neutral controls");
            } finally { instance.SetValue(null,saved); }
        }
    }
}
