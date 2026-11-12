using System;
using System.Collections.Generic;
using System.Reflection;
using JKRuntime;
using JKRuntime.Gameplay;
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
        private sealed class FrameWorld:ICollisionQuery
        {
            internal LevelScreen Screen;
            public LevelScreen GetCurrentScreen(){return Screen;}
            public int GetCurrentScreenIndex(){return 0;}
            public float GetCurrentVelocity(){return 0;}
            public void CreateWaterSplashParticle(Point p,bool enter){}
            public void PlayBumpSFX(bool water){}
            public bool IsInWater(Rectangle box){return false;}
            public AdvCollisionInfo GetCollisionInfo(Rectangle box){return Screen.GetCollisionInfo(box);}
            public bool CheckCollision(Rectangle box,out Rectangle overlap,out AdvCollisionInfo info){return Screen.TryCollision(box,out overlap,out info);}
            public bool CheckCollision(Rectangle box,out Rectangle overlap,out AggregateCollisionInfo info)
            {AdvCollisionInfo raw;bool hit=CheckCollision(box,out overlap,out raw);info=new AggregateCollisionInfo(raw);return hit;}
        }
        private static BodyComp FrameBody(int offset)
        {
            var world=new FrameWorld{Screen=new LevelScreen(offset/360,new IBlock[]{
                new BoxBlock(new Rectangle(0,344-offset,480,16)),new BoxBlock(new Rectangle(0,40-offset,480,8)),
                new SlopeBlock(new Rectangle(96,224-offset,96,96),SlopeType.BottomLeft),
                new BoxBlock(new Rectangle(8,48-offset,8,296))
            },new LevelScreen.Graphics(),false,new TeleportLink[0],0,null)};
            var body=new BodyComp(new Vector2(260,318-offset),18,26);
            var context=new BehaviourContext(body);var handlers=new LinkedList<IBlockBehaviour>();
            var move=new UpdateYPositionFromVelocityBehaviour(handlers);
            var resolve=(ResolveYCollisionBehaviour)Activator.CreateInstance(typeof(ResolveYCollisionBehaviour),OwnedPatches.Members,null,new object[]{world,handlers},null);
            frameTicks.Add(body,delegate{
                move.ExecuteBehaviour(context);resolve.ExecuteBehaviour(context);
                body.Velocity.Y+=PlayerValues.GRAVITY;
            });
            return body;
        }
        private static readonly Dictionary<BodyComp,Action> frameTicks=new Dictionary<BodyComp,Action>();

        private static void PhysicsFrameChecks()
        {

            var layout=new MapLayout{Screens=5};layout.PhysicsFrames.Add(4,0);
            using(var frame=new NativePhysicsFrame(layout))
            using(var observed=MovementObservation.Prepare())
            {
                for(int charge=0;charge<=35;charge++)
                {
                    var original=FrameBody(0);var translated=FrameBody(1440);
                    using(frame.Attach(translated,false))
                    using(var observer=observed.Observe(translated))
                    {
                        original.Velocity=translated.Velocity=new Vector2(-3.5f,PlayerValues.JUMP*(charge+1)/36f);
                        object saved=null;Vector2 savedPosition=Vector2.Zero,savedVelocity=Vector2.Zero;
                        for(int n=0;n<120;n++)
                        {
                            frameTicks[original]();frameTicks[translated]();
                            var box=translated.GetHitbox();box.Y+=1440;
                            Require(box==original.GetHitbox()&&original.Velocity==translated.Velocity,"Reference frame preserves native contacts and velocities at F"+charge+" tick "+n+": "+original.GetHitbox()+" / "+box+" velocity "+original.Velocity+" / "+translated.Velocity);
                            if(n==10){saved=frame.Capture();savedPosition=translated.Position;savedVelocity=translated.Velocity;}
                        }
                        Require(observer.Read(MovementStage.YCollision).Covered,"Reference-frame hooks retain movement observation coverage");
                        translated.Position=savedPosition;translated.Velocity=savedVelocity;frame.Restore(saved);
                        var first=frame.Capture();frameTicks[translated]();var expected=translated.Position;var expectedBox=translated.GetHitbox();
                        translated.Position=savedPosition;translated.Velocity=savedVelocity;frame.Restore(first);frameTicks[translated]();
                        Require(translated.Position==expected&&translated.GetHitbox()==expectedBox,"Snapshot restores the retained fraction before the next native tick");
                        var paused=(PhysicsFrameState)frame.Capture();
                        for(int read=0;read<20;read++)translated.GetHitbox();
                        Require(((PhysicsFrameState)frame.Capture()).ReferenceY==paused.ReferenceY&&translated.Position==expected,"Hitbox reads during pause don't advance reference coordinates");
                        translated.Position=new Vector2(260,318-1440);translated.Velocity=Vector2.Zero;
                        Require(translated.GetHitbox().Y==318-1440,"A direct teleport discards the old reference fraction");
                        translated.Position=new Vector2(260,-400.25f);
                        Require(translated.GetHitbox().Y==-400,"Unconfigured screens retain native truncation");
                    }
                }
            }
            var unbound=FrameBody(1440);unbound.Position.Y=-1121.75f;
            Require(unbound.GetHitbox().Y==-1121,"World teardown clears reference-frame ownership");frameTicks.Clear();
        }
    }
}
