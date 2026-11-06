using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private sealed class PhysicsTick
        {internal PhysicsCoordinateState State;internal Vector2 Velocity;internal bool Ground,Knocked;}
        private static readonly FieldInfo LastPhysicsVelocity=typeof(BodyComp).GetField("_last_velocity",OwnedPatches.Members);
        private static readonly FieldInfo PhysicsGround=typeof(BodyComp).GetField("_is_on_ground",OwnedPatches.Members);
        private static IBlock[] PhysicsWorld(IBlock[] blocks,bool mirror,int offset,bool reverse)
        {
            var result=new List<IBlock>();
            foreach(var block in blocks)
            {
                var rect=block.GetRect();if(mirror)rect.X=480-rect.Right;rect.Y-=offset;
                result.Add(new BoxBlock(rect));
            }
            if(reverse)result.Reverse();return result.ToArray();
        }
        private static List<PhysicsTick> PhysicsFlight(IBlock[] blocks,float x,float y,float vx,float vy,int width,int offset,bool mirror,bool reverse)
        {
            var world=new FrameWorld{Screen=new LevelScreen(offset/360,PhysicsWorld(blocks,mirror,offset,reverse),new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)};
            var body=new BodyComp(new Vector2(mirror?480-width-x:x,y-offset),width,26){Velocity=new Vector2(mirror?-vx:vx,vy)};
            PhysicsGround.SetValue(body,false);
            var context=new BehaviourContext(body);var handlers=new LinkedList<IBlockBehaviour>();
            var mx=new UpdateXPositionFromVelocityBehaviour(handlers);var my=new UpdateYPositionFromVelocityBehaviour(handlers);
            var rx=(IBodyCompBehaviour)Activator.CreateInstance(typeof(ResolveXCollisionBehaviour),OwnedPatches.Members,null,new object[]{world,handlers},null);
            var ry=(IBodyCompBehaviour)Activator.CreateInstance(typeof(ResolveYCollisionBehaviour),OwnedPatches.Members,null,new object[]{world,handlers},null);
            var result=new List<PhysicsTick>();
            using(var physics=new PhysicsCoordinates())using(physics.Attach(body,false))
            {
                for(int tick=0;tick<140;tick++)
                {
                    LastPhysicsVelocity.SetValue(body,body.Velocity);
                    mx.ExecuteBehaviour(context);rx.ExecuteBehaviour(context);my.ExecuteBehaviour(context);ry.ExecuteBehaviour(context);
                    body.Velocity.Y+=PlayerValues.GRAVITY;
                    result.Add(new PhysicsTick{State=(PhysicsCoordinateState)physics.Capture(),Velocity=body.Velocity,Ground=body.IsOnGround,Knocked=body.IsKnocked});
                }
            }
            return result;
        }
        private static void ComparePhysics(List<PhysicsTick> original,List<PhysicsTick> other,int width,int offset,bool mirror,string label)
        {
            for(int tick=0;tick<original.Count;tick++)
            {
                var a=original[tick];var b=other[tick];
                Require((mirror?(480-width)*PhysicsCoordinates.Unit-a.State.X:a.State.X)==b.State.X&&a.State.Y-offset*PhysicsCoordinates.Unit==b.State.Y
                    &&a.Velocity.Y==b.Velocity.Y&&(mirror?-a.Velocity.X:a.Velocity.X)==b.Velocity.X&&a.Ground==b.Ground&&a.Knocked==b.Knocked,
                    label+" tick="+tick+" exact="+a.State.X+","+a.State.Y+" / "+b.State.X+","+b.State.Y+" velocity="+a.Velocity+" / "+b.Velocity);
            }
        }
        private static void SymmetricPhysicsChecks(string scratch)
        {
            string root=Path.Combine(scratch,"map-physics");Directory.CreateDirectory(root);string entry=Path.Combine(root,MappingFiles.Settings);
            WriteLayout(entry,"<Physics version='1' profile='symmetric'/>");
            Require(MapLayout.ReadForScreens(root,8).Physics.Symmetric,"Physics works without a collision layout");
            var scene=new Construction.RecipeCompiler(Construction.PrimitiveRegistry.Create()).Read(root);
            Require(Construction.SceneDocument.Copy(scene).Physics.GetAttribute("profile")=="symmetric","Physics survives scene serialization");
            WriteLayout(entry,"<Physics version='1' profile='reference'><Frame screen='3' referenceScreen='1'/></Physics>");
            File.WriteAllText(entry,File.ReadAllText(entry).Replace("><Frame",">\n  <Frame").Replace("/></Physics>","/>\n</Physics>"));
            Require(new Construction.RecipeCompiler(Construction.PrimitiveRegistry.Create()).Read(root).Physics!=null,"Indented reference settings compile and serialize");
            Require(MapLayout.Read(root).PhysicsFrames[2]==0,"Unified reference profile keeps the existing reference adapter");
            foreach(string bad in new[]{"<Physics version='2' profile='symmetric'/>","<Physics version='1' profile='guess'/>","<Physics version='1' profile='reference'/>","<Physics version='1' profile='symmetric'><Frame screen='1' referenceScreen='1'/></Physics>","<Physics version='1' profile='native'/><MapLayout version='2' source='native' screens='2'><PhysicsFrame screen='2' referenceScreen='1'/></MapLayout>"})
            {WriteLayout(entry,bad);bool rejected=false;try{MapLayout.Read(root);}catch(InvalidDataException){rejected=true;}Require(rejected,"Invalid/conflicting map physics rejected");}
            var blocks=new IBlock[]{new BoxBlock(new Rectangle(0,344,480,16)),new BoxBlock(new Rectangle(0,30,480,8)),new BoxBlock(new Rectangle(80,50,16,270)),
                new BoxBlock(new Rectangle(140,180,48,16)),new BoxBlock(new Rectangle(240,260,48,16))};
            int flights=0;
            foreach(int width in new[]{17,18,26})foreach(float fraction in new[]{0f,0.25f,0.5f,0.75f})for(int charge=0;charge<36;charge++)
            {
                float x=99+fraction,vy=PlayerValues.JUMP*(charge+1)/36f;
                var baseline=PhysicsFlight(blocks,x,318,-3.5f,vy,width,0,false,false);
                ComparePhysics(baseline,PhysicsFlight(blocks,x,318,-3.5f,vy,width,0,true,false),width,0,true,"Mirrored F"+charge);
                ComparePhysics(baseline,PhysicsFlight(blocks,x,318,-3.5f,vy,width,1440,false,true),width,1440,false,"Translated/permuted F"+charge);
                flights+=3;
            }
            using(var physics=new PhysicsCoordinates())
            {
                var body=new BodyComp(new Vector2(100,-1400000),18,26);
                using(physics.Attach(body,false))
                {
                    body.Velocity.Y=0.125f;var context=new BehaviourContext(body);var move=new UpdateYPositionFromVelocityBehaviour(new LinkedList<IBlockBehaviour>());
                    move.ExecuteBehaviour(context);var saved=(PhysicsCoordinateState)physics.Capture();move.ExecuteBehaviour(context);var expected=(PhysicsCoordinateState)physics.Capture();
                    physics.Restore(saved);move.ExecuteBehaviour(context);Require(((PhysicsCoordinateState)physics.Capture()).Y==expected.Y,"Snapshot restores exact coordinates at large world Y");
                    body.Position.X=25;Require(((PhysicsCoordinateState)physics.Capture()).Y==expected.Y,"External X writes preserve Y fraction");
                    body.Position.Y=12;Require(((PhysicsCoordinateState)physics.Capture()).Y==12*PhysicsCoordinates.Unit,"Teleport resets changed axis");
                }
            }
            PhysicsCompatibilityChecks(root);
            Console.WriteLine("[OK] Map physics: "+flights+" native-stage trajectories");
        }
    }
}
