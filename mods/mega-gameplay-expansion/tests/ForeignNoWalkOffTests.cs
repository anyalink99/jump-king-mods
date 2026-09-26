using System;
using System.IO;
using System.Reflection;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void ForeignNoWalkOff(string gameDir)
        {
            string workshop = Path.GetFullPath(Path.Combine(gameDir, "../../workshop/content/1061090"));
            string path = Path.Combine(workshop, "3140151035/JumpKingPlus.dll");
            if (!File.Exists(path)) { Console.WriteLine("[SKIP] JumpKingPlus is not installed"); return; }
            var assembly = Assembly.LoadFrom(path);
            using(var providerObservation=JKRuntime.Gameplay.MotionObservation.Prepare(assembly.GetTypes()))
            using(var stageObservation=JKRuntime.Gameplay.MovementObservation.Prepare())
            {
            var factory = (IBlockFactory)Activator.CreateInstance(assembly.GetType("JumpKingPlus.JumpKingPlusBlockFactory", true));
            var flags = factory.GetType().GetField("Flags", Flags);
            var oldFlags = flags.GetValue(null);
            bool enabled = Settings.Current.NoWalkOff;
            try
            {
                Settings.Current.NoWalkOff = true;
                using (var input = new WalkInputFixture())
                foreach (bool oneWay in new[] { false, true })
                foreach (bool lowGravity in new[] { false, true })
                foreach (bool legacy in new[] { false, true })
                foreach (bool water in new[] { false, true })
                foreach (int direction in new[] { -1, 1 })
                {
                    flags.SetValue(null, legacy ? new[] { "LegacyWalkSpeedInLowGravity" } : new string[0]);
                    var floor = oneWay ? factory.GetBlock(new Color(65,65,65), new Rectangle(80,320,160,40), null,null,0,0,0)
                        : (IBlock)new BoxBlock(new Rectangle(80,320,160,40));
                    var blocks = new System.Collections.Generic.List<IBlock> { floor };
                    if (lowGravity) blocks.Add(factory.GetBlock(new Color(128,255,255), new Rectangle(0,0,480,360),null,null,0,0,0));
                    if (water) blocks.Add(new WaterBlock(new Rectangle(0,0,480,360)));
                    WalkScene(blocks.ToArray());
                    var player = NoWalkPlayer(); player.m_body.Position.X = 150;
                    RegisterPlusWalk(player, assembly);
                    var reference = NoWalkPlayer(); reference.m_body.Position.X = 150;
                    RegisterPlusWalk(reference, assembly);
                    using (var guard = new NoWalkOffController(player))
                    using (var evidence = providerObservation.Observe(player.m_body))
                    using (var stages = stageObservation.Observe(player.m_body))
                    {
                        string label = "oneWay="+oneWay+" lowGravity="+lowGravity+" legacy="+legacy+" water="+water+" direction="+direction;
                        for (int tick=0;tick<20;tick++)
                        {
                            input.Direction(direction); reference.UpdateComponents(1f/60f); player.UpdateComponents(1f/60f);
                            RequireSameWalk(reference,player,"Approach speed changed: "+label);
                        }
                        foreach(JKRuntime.Gameplay.MovementStage stage in Enum.GetValues(typeof(JKRuntime.Gameplay.MovementStage)))
                        {
                            var sample=stages.Read(stage);
                            Require(sample.Sequence>0 && sample.Covered,"Missing native stage coverage: "+stage+" "+label);
                        }
                        var contact=stages.Read(JKRuntime.Gameplay.MovementStage.YCollision).Contact;
                        Require(contact.Path==(oneWay?JKRuntime.Gameplay.ContactPath.Additional:JKRuntime.Gameplay.ContactPath.Native),"Wrong actual support path: "+label);
                        WalkTicks(player,input,direction,350);
                        float edge = direction > 0 ? 239 : 63;
                        Require(player.m_body.IsOnGround && player.m_body.Position.X == edge, "JumpKingPlus missed edge: "+label+" position="+player.m_body.Position+" motion="+evidence.Read().Reason+" source="+evidence.Read().Source);
                        WalkTicks(player,input,direction,30);
                        Require(player.m_body.Position.X == edge, "JumpKingPlus held edge drift: "+label);
                        WalkTicks(player,input,0,5);
                        WalkTicks(player,input,direction,20);
                        Require(!player.m_body.IsOnGround, "JumpKingPlus release/repress failed: "+label);
                    }
                }
                using (var input = new WalkInputFixture()) ForeignWalkBoundaries(input,assembly,factory);
            }
            finally { flags.SetValue(null,oldFlags); Settings.Current.NoWalkOff = enabled; }
            Console.WriteLine("[OK] Installed JumpKingPlus: 64 solid/one-way, low-gravity/legacy, water and direction cases");
            }
            OtherInstalledMotion(workshop);
        }

        private static void OtherInstalledMotion(string workshop)
        {
            string expansion=Path.Combine(workshop,"3214349391/JumpKing-Expansion-Blocks.dll");
            if(File.Exists(expansion))
            {
                var assembly=Assembly.LoadFrom(expansion);
                var block=assembly.GetType("JumpKing_Expansion_Blocks.Blocks.SuperLowGravity",true);
                var behaviour=assembly.GetType("JumpKing_Expansion_Blocks.Behaviours.SuperLowGravity",true);
                using(var observation=JKRuntime.Gameplay.MotionObservation.Prepare(new[]{behaviour}))
                using(var input=new WalkInputFixture())
                foreach(int direction in new[]{-1,1})
                {
                    WalkScene(new BoxBlock(new Rectangle(80,320,160,40)),(IBlock)Activator.CreateInstance(block,new object[]{new Rectangle(0,0,480,360)}));
                    var player=NoWalkPlayer();player.m_body.Position.X=150;
                    player.m_body.RegisterBlockBehaviour(block,(IBlockBehaviour)Activator.CreateInstance(behaviour,true));
                    bool enabled=Settings.Current.NoWalkOff;Settings.Current.NoWalkOff=true;
                    try {using(var guard=new NoWalkOffController(player)) {WalkTicks(player,input,direction,200);Require(player.m_body.IsOnGround && player.m_body.Position.X==(direction>0?239:63),"SuperLowGravity was not automatically supported");}}
                    finally {Settings.Current.NoWalkOff=enabled;}
                }
                Console.WriteLine("[OK] Installed Expansion Blocks SuperLowGravity: automatic arithmetic support, both edges");
            }
            string conveyor=Path.Combine(workshop,"3330536917/ConveyorBlockMod.dll");
            if(!File.Exists(conveyor))return;
            var source=Assembly.LoadFrom(conveyor);var type=source.GetType("ConveyorBlockMod.BlocksBehaviour.ConveyorBlockBehaviour",true);
            var surface=(IBlock)Activator.CreateInstance(source.GetType("ConveyorBlockMod.Blocks.ConveyorBlock",true),new object[]{new Rectangle(0,0,100,40),(byte)1,(byte)100});
            var handler=(IBlockBehaviour)Activator.CreateInstance(type);
            var body=new BodyComp(Vector2.Zero,18,26);var context=new JumpKing.BodyCompBehaviours.BehaviourContext(body);
            typeof(JumpKing.BodyCompBehaviours.BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo",Flags).Invoke(context.LastFrameCollisionInfo,
                new object[]{new AdvCollisionInfo(new System.Collections.Generic.List<IBlock>{surface},false,SlopeType.None,Vector2.Zero)});
            var handlers=new System.Collections.Generic.LinkedList<IBlockBehaviour>();handlers.AddLast(handler);
            var step=new JumpKing.BodyCompBehaviours.UpdateXPositionFromVelocityBehaviour(handlers);
            using(var scope=JKRuntime.Gameplay.MotionObservation.Prepare(new[]{type}))
            using(var observer=scope.Observe(body))
            foreach(bool on in new[]{false,true,false,true})
            {
                bool last=(bool)type.GetField("IsPlayerOnBlockLastFrame").GetValue(handler);
                bool earlier=(bool)type.GetField("IsPlayerOnBlock2FramesBefore").GetValue(handler);
                handler.IsPlayerOnBlock=on;body.Position=Vector2.Zero;body.Velocity=new Vector2(1.5f,0);step.ExecuteBehaviour(context);
                var sample=observer.Read();
                Require(sample.Kind==(on?JKRuntime.Gameplay.MotionKind.ExternalMotion:JKRuntime.Gameplay.MotionKind.ControlledOnly),"Installed conveyor classification: "+sample.Reason);
                Require(body.Position.X==(on?2:1.5f) && sample.NeutralStep==(on?.5f:0),"Installed conveyor displacement changed");
                Require((bool)type.GetField("IsPlayerOnBlockLastFrame").GetValue(handler)==on && (bool)type.GetField("IsPlayerOnBlock2FramesBefore").GetValue(handler)==last
                    && (bool)type.GetField("IsPlayerOnBlock3FramesBefore").GetValue(handler)==earlier,"Installed conveyor state history advanced more than once");
            }
            Console.WriteLine("[OK] Installed ConveyorBlockMod: active/inactive arithmetic and exactly one state-history advance");
        }

        private static void RequireSameWalk(PlayerEntity expected, PlayerEntity actual, string label)
        {
            Require(expected.m_body.Position == actual.m_body.Position && expected.m_body.Velocity == actual.m_body.Velocity
                && expected.m_body.IsOnGround == actual.m_body.IsOnGround,label);
        }

        private static void ForeignWalkBoundaries(WalkInputFixture input, Assembly assembly, IBlockFactory factory)
        {
            var bounds = new Rectangle(80,320,160,40);
            foreach (int side in new[] {65,66,67,68})
            {
                var block = factory.GetBlock(new Color(side,65,65),bounds,null,null,0,0,0);
                WalkScene(block);
                var player = NoWalkPlayer();
                using (var guard = new NoWalkOffController(player))
                {
                    Require(!guard.Supported(new Rectangle(150,294,18,26)),"Unregistered one-way falsely supplied support");
                    RegisterPlusWalk(player,assembly);
                    Require(guard.Supported(new Rectangle(150,294,18,26)) == (side==65),"One-way orientation ignored");
                    Require(!guard.Supported(new Rectangle(150,300,18,26)),"Embedded body falsely supplied support");
                    Require(!guard.Supported(new Rectangle(150,293,18,26)),"Lower one-way falsely supplied support");
                }
                // The guard must not change passage upward through Top, or the
                // provider's own collision on the other three orientations.
                var expected = NoWalkPlayer(); var actual = NoWalkPlayer();
                RegisterPlusWalk(expected,assembly); RegisterPlusWalk(actual,assembly);
                expected.m_body.Position = actual.m_body.Position = new Vector2(150,361);
                expected.m_body.Velocity = actual.m_body.Velocity = new Vector2(1.5f,-6);
                NativeFlight.Set(expected.m_body,"_is_on_ground",false);
                NativeFlight.Set(actual.m_body,"_is_on_ground",false);
                using (var guard = new NoWalkOffController(actual))
                    for (int tick=0;tick<12;tick++)
                    {
                        input.Direction(1); expected.UpdateComponents(1f/60f); actual.UpdateComponents(1f/60f);
                        RequireSameWalk(expected,actual,"One-way upward passage changed");
                    }
            }
            foreach (int direction in new[] {-1,1})
            {
                WalkScene(new BoxBlock(bounds),factory.GetBlock(new Color(128,255,255),new Rectangle(110,250,90,70),null,null,0,0,0));
                var player=NoWalkPlayer(); player.m_body.Position.X=direction>0?85:215;
                RegisterPlusWalk(player,assembly);
                using(var guard=new NoWalkOffController(player))
                {
                    WalkTicks(player,input,direction,240);
                    Require(player.m_body.IsOnGround && player.m_body.Position.X==(direction>0?239:63),"Low-gravity entry/exit lost edge guard");
                }
                // External displacement must still release a latched edge,
                // including when low gravity scales the incoming force.
                foreach(bool carry in new[]{false,true})
                {
                    WalkScene(factory.GetBlock(new Color(65,65,65),bounds,null,null,0,0,0),
                        factory.GetBlock(new Color(128,255,255),new Rectangle(0,0,480,360),null,null,0,0,0));
                    player=NoWalkPlayer(); player.m_body.Position.X=150; RegisterPlusWalk(player,assembly);
                    using(var guard=new NoWalkOffController(player))
                    {
                        WalkTicks(player,input,direction,250);
                        var reference=NoWalkPlayer(); RegisterPlusWalk(reference,assembly);
                        NativeFlight.CopyState(player.m_body,reference.m_body);
                        if(carry) { AddCarry(player,new ExternalCarry {Speed=direction*.5f}); AddCarry(reference,new ExternalCarry {Speed=direction*.5f}); }
                        else { AddForce(player,new ExternalForce {Force=direction*.5f}); AddForce(reference,new ExternalForce {Force=direction*.5f}); }
                        for(int tick=0;tick<12;tick++)
                        {
                            input.Direction(direction); reference.UpdateComponents(1f/60f); player.UpdateComponents(1f/60f);
                            RequireSameWalk(reference,player,"Low gravity masked an external force at the edge");
                        }
                    }
                }
            }
            Console.WriteLine("[OK] JumpKingPlus: registration, orientation, embedded/lower support, upward passage, low-gravity boundaries and external-force release");
        }

        private static void RegisterPlusWalk(PlayerEntity player, Assembly assembly)
        {
            foreach (string name in new[] { "LowGravity", "OneWay" })
                player.m_body.RegisterBlockBehaviour(assembly.GetType("JumpKingPlus.Blocks."+name+"Block",true),
                    (IBlockBehaviour)Activator.CreateInstance(assembly.GetType("JumpKingPlus.BlockBehaviours."+name+"BlockBehaviour",true),true));
        }
    }
}
