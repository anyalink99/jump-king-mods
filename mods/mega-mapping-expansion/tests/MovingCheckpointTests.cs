using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void MovingCheckpointChecks(string provider)
        {
            var assembly=Assembly.LoadFrom(provider);
            var factory=(IBlockFactory)Activator.CreateInstance(assembly.GetType("CheckpointBlock.Factories.FactoryCheckpoint",true));
            var factories=(List<IBlockFactory>)typeof(LevelManager).GetField("BlockFactories",OwnedPatches.Members).GetValue(null);factories.Add(factory);
            var screens=typeof(LevelManager).GetField("m_screens",OwnedPatches.Members);var count=typeof(LevelManager).GetField("_total_screens",OwnedPatches.Members);var old=screens.GetValue(null);var oldCount=count.GetValue(null);var prior=NativeMaterials.Loaded;
            try
            {
                var level=(JumpKing.Workshop.Level)FormatterServices.GetUninitializedObject(typeof(JumpKing.Workshop.Level));
                var materials=new NativeMaterials(MaterialPlan.Parse("<Materials version='1'><Source id='reset' color='#01EF7C' sizing='rectangle'/><Material id='return' collision='trigger'><Use id='reset' source='reset'/></Material></Materials>",1),"");
                materials.Level=level;
                materials.Texture=new PixelLevelTexture(new MapLayout{Screens=1,Columns=1,Rows=1,CellWidth=240,CellHeight=180},new Color[240*180]);NativeMaterials.Loaded=materials;
                var screen=new LevelScreen(0,new IBlock[0],new LevelScreen.Graphics(),false,new TeleportLink[0],0,null);screens.SetValue(null,new[]{screen});count.SetValue(null,1);JumpKing.Camera.UpdateCamera(Point.Zero);
                var plan=MovingPlatformPlan.Parse("<MovingPlatforms version='1'><Motion id='m' axis='horizontal' distance='100' speed='600'/><Profile id='p' motion='m' collision='trigger' material='return'/><Platform id='hazard' profile='p' rect='100,100,2,30'/></MovingPlatforms>",1);
                var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));player.m_body=new BodyComp(new Vector2(130,100),18,26);
                var setType=assembly.GetType("CheckpointBlock.Data.CheckpointSet",true);var sets=Array.CreateInstance(setType,10);
                sets.SetValue(Activator.CreateInstance(setType,new object[]{new Point(60,280)}),0);
                var handler=(IBlockBehaviour)Activator.CreateInstance(assembly.GetType("CheckpointBlock.Behaviours.BehaviourReset",true),new object[]{LevelManager.Instance,sets,new Point(231,302)});
                player.m_body.RegisterBlockBehaviour(assembly.GetType("CheckpointBlock.Blocks.BlockReset",true),handler);
                using(var moving=new NativeMovingPlatforms(plan,"",false))using(moving.Activate(player,false))
                {
                    materials.Prepare();using(materials.Activate(player.m_body,false))
                    {
                        var block=materials.Instances.Single().Blocks.Single();moving.Tick(player.m_body,.1);
                        Require(ReferenceEquals(block,materials.Instances.Single().Blocks.Single())&&block.GetRect().X==160,"checkpoint reset retains its native instance at the accepted moving position");
                        MaterialContacts.Body=player.m_body;
                        var context=new BehaviourContext(player.m_body);typeof(BehaviourContext).GetMethod("ClearCollisionForNewFrame",OwnedPatches.Members).Invoke(context,null);
                        typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo",OwnedPatches.Members).Invoke(context.CollisionInfo,new object[]{screen.GetCollisionInfo(player.m_body.GetHitbox())});
                        player.m_body.Velocity=new Vector2(3,4);handler.ExecuteBlockBehaviour(context);
                        Require(player.m_body.Position==new Vector2(51,254)&&player.m_body.Velocity==Vector2.Zero,"installed checkpoint handler returns a swept-contact player to the actual checkpoint and resets velocity");
                        Require(!screen.GetCollisionInfo(player.m_body.GetHitbox()).GetCollidedBlocks().Contains(block),"teleporting out of a swept contact does not create a phantom contact at the destination");
                        MaterialContacts.ClearSweeps(player.m_body);MaterialContacts.Body=null;
                    }
                }
            }
            finally{MaterialContacts.Body=null;factories.Remove(factory);NativeMaterials.Loaded=prior;MaterialContacts.Clear();screens.SetValue(null,old);count.SetValue(null,oldCount);}
            Console.WriteLine("[OK] Installed Checkpoint Block: 240x180 moving reset, swept native contact, checkpoint destination, velocity and teleport exit");
        }
    }
}
