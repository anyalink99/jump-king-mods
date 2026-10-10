using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class ContactPhaseTests
    {
        private static int checks;
        private static bool snake;
        private static void Check(bool value,string message) {checks++;if(!value) throw new Exception(message);}
        private static bool NoWind(ref bool __result) {__result=true;return false;}
        private static bool Equipment(Items p_item,ref bool __result) {__result=snake && p_item==Items.SnakeRing;return false;}
        private sealed class Map : ICollisionQuery
        {
            internal int Floor=126;
            internal Rectangle Roof;
            internal Func<Rectangle,bool> Query;
            internal Func<Rectangle,AdvCollisionInfo> Info;
            internal bool Blocked(Rectangle b) {return Query!=null ? Query(b) : b.Bottom>Floor || Roof.Intersects(b);}
            public AdvCollisionInfo GetCollisionInfo(Rectangle b)
            {return Info!=null ? Info(b) : new AdvCollisionInfo(Blocked(b) ? new List<IBlock>{new BoxBlock(b.Bottom>Floor ? new Rectangle(-10000,Floor,20000,10000) : Roof)} : new List<IBlock>(),false,SlopeType.None,Vector2.Zero);}
            public bool CheckCollision(Rectangle b,out Rectangle r,out AdvCollisionInfo i) {r=Rectangle.Empty;i=GetCollisionInfo(b);return Blocked(b);}
            public bool CheckCollision(Rectangle b,out Rectangle r,out AggregateCollisionInfo i) {r=Rectangle.Empty;i=new AggregateCollisionInfo(GetCollisionInfo(b));return Blocked(b);}
            public bool IsInWater(Rectangle b) {return false;}
        }
        private sealed class Bumps : IBumpSFXPlayer
        {internal int Count;public void PlayBumpSFX(bool water) {Count++;}}
        private static BodyComp Native(Vector2 position,Map map,Bumps bumps)
        {
            var body=new BodyComp(position,18,26);
            var blocks=(LinkedList<IBlockBehaviour>)AccessTools.Field(typeof(BodyComp),"m_blockBehaviours").GetValue(body);
            // sand's query was built with the real LevelManager, which has no loaded map here
            foreach(var block in blocks.Where(b=>b is JumpKing.BlockBehaviours.SandBlockBehaviour).ToArray()) blocks.Remove(block);
            var list=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);
            var cache=list.First(b=>b.GetType().Name=="CacheVelocityBehaviour");list.Clear();
            list.AddLast((IBodyCompBehaviour)Activator.CreateInstance(typeof(BodyComp).Assembly.GetType("JumpKing.BodyCompBehaviours.CacheCollisionStateBehaviour",true),BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,new object[]{map},null));
            list.AddLast(new WindVelocityUpdateBehaviour());list.AddLast(cache);
            list.AddLast(new UpdateXPositionFromVelocityBehaviour(blocks));
            list.AddLast((IBodyCompBehaviour)Activator.CreateInstance(typeof(ResolveXCollisionBehaviour),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{map,blocks},null));
            list.AddLast(new UpdateYPositionFromVelocityBehaviour(blocks));
            list.AddLast((IBodyCompBehaviour)Activator.CreateInstance(typeof(ResolveYCollisionBehaviour),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{map,blocks},null));
            list.AddLast(new ApplyGravityBehaviour(blocks));list.AddLast(new ExecuteBlockBehaviours(blocks));
            list.AddLast((IBodyCompBehaviour)Activator.CreateInstance(typeof(PlayBumpSFXBehaviour),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{bumps},null));
            return body;
        }
        private static InteractionFrame Frame(float x,float y)
        {return new InteractionFrame{Physics=true,Modern=true,Active=true,Epoch=1,Sequence=1,RulesEpoch=1,Revision=1,Map=1,Rules=InteractionRules.Solid,Width=18,Height=26,Position=new Vector2(x,y)};}
        private static void Step(BodyComp body)
        {AccessTools.Method(typeof(BodyComp),"UpdateInternal").Invoke(body,new object[]{1f/60});ContactFeedback.Flush(body);}
        internal static void Run()
        {
            var hooks=new Harmony("multiplayer-expansion.contact-phase-test");
            hooks.Patch(AccessTools.Method(typeof(WindVelocityUpdateBehaviour),"ExecuteBehaviour"),prefix:new HarmonyMethod(typeof(ContactPhaseTests),"NoWind"));
            hooks.Patch(AccessTools.Method(typeof(InventoryManager),"HasItemEnabled"),prefix:new HarmonyMethod(typeof(ContactPhaseTests),"Equipment"));
            try {Rider();Seams();Landing();Sound();Probes();WalkingVisuals();}
            finally {snake=false;hooks.UnpatchAll(hooks.Id);}
            Console.WriteLine("[OK] Native contact phases: "+checks+" checks (Snake Ring friction, carry once, landing, jump, one sound, generic foot geometry, stable visual side)");
        }
        private static void Rider()
        {
            var map=new Map();var body=Native(new Vector2(100,73),map,new Bumps());body.Velocity=new Vector2(2,2);
            var frame=Frame(100,100);frame.Grounded=true;
            IList<InteractionPeer> peers=new[]{new InteractionPeer{Id=2,Frame=frame,Sample=frame}};var solver=new PlayerContacts();
            var list=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);var original=list.ToArray();
            using(new ContactPipeline(body,solver,()=>peers,()=>InteractionRules.Solid,()=>1,map.Blocked)) {
                snake=true;Step(body);
                Check(body.IsOnGround && solver.Support==2 && body.Position.Y==74,"Rider wasn't grounded before native materials");
                Check(Math.Abs(body.Velocity.X-(2-JumpKing.PlayerValues.ICE_FRICTION))<.0001,"Native Snake Ring friction skipped head support");
                float expected=body.Velocity.X;
                for(int tick=0;tick<8;tick++) {body.Velocity.X=expected;Step(body);expected=Math.Max(0,expected-JumpKing.PlayerValues.ICE_FRICTION);Check(Math.Abs(body.Velocity.X-expected)<.0001,"Head friction wasn't native ice friction each tick");}
                body.Velocity=Vector2.Zero;float x=body.Position.X;
                frame.Position+=new Vector2(3,-12);frame.Grounded=false;frame.Velocity=new Vector2(3,-12);Step(body);
                Check(body.Position==new Vector2(x+3,62) && body.IsOnGround,"Carry ran twice or detached during ascent");
                body.Velocity=new Vector2(0,-10);solver.Takeoff(body);Step(body);
                Check(solver.Support==0 && !body.IsOnGround && body.Position.Y==40 && body.Velocity.X==3,"Rider couldn't jump with inherited airborne momentum");
                peers=new InteractionPeer[0];Step(body);Check(!body.IsOnGround,"Missing support left a grounded rider");
            }
            Check(list.SequenceEqual(original),"Contact scope removed another native behavior or leaked its phase");snake=false;
        }
        private static void Seams()
        {
            var screensField=AccessTools.Field(typeof(LevelManager),"m_screens");
            var oldScreens=screensField.GetValue(null);
            var countField=AccessTools.Field(typeof(LevelManager),"_total_screens");var oldCount=countField.GetValue(null);
            var cameraField=AccessTools.Field(typeof(JumpKing.Camera),"_current_screen");var oldCamera=cameraField.GetValue(null);
            try {
                foreach(int height in new[]{10,100}) foreach(int destination in new[]{0,2}) foreach(int direction in new[]{-1,1}) {
                    var screens=Enumerable.Range(0,4).Select(i=>new LevelScreen(i,new IBlock[]{new BoxBlock(new Rectangle(0,height+26-i*360,480,234))},
                        new LevelScreen.Graphics(),false,new[]{new TeleportLink(i==0 ? destination+1 : 1)},0,null)).ToArray();
                    screensField.SetValue(null,screens);JKRuntime.Geometry.MapTopology.Invalidate();
                    countField.SetValue(null,4);cameraField.SetValue(null,0);
                    var map=new Map{Query=b=>JKRuntime.Geometry.MapTopology.IsBlocked(screens,b)};
                    float x=direction>0 ? 468 : -6;
                    var body=Native(new Vector2(x,height-27),map,new Bumps());body.Velocity=new Vector2(0,2);
                    var frame=Frame(x,height);frame.Grounded=true;
                    var peer=new InteractionPeer{Id=2,Frame=frame,Sample=frame};IList<InteractionPeer> peers=new[]{peer};
                    var solver=new PlayerContacts();
                    using(new ContactPipeline(body,solver,()=>peers,()=>InteractionRules.Solid,()=>1,map.Blocked)) {
                        Step(body);Check(solver.Support==2,"Seam fixture didn't attach the rider");
                        Vector2 resting=body.Velocity;
                        for(int wrap=0;wrap<20;wrap++) {
                            solver.BeginBodyStep();
                            frame.Position=new Vector2(direction>0 ? -8 : 470,height-destination*360);
                            peer.Sample=frame;Step(body);
                            Check(solver.Support==2 && body.IsOnGround,"A seamless side crossing detached the rider");
                            Check(Math.Abs(body.Position.X-frame.Position.X)<.001 && Math.Abs(body.Position.Y-(frame.Position.Y-26))<.001,"A rider wasn't rebased with its carrier");
                            Check(solver.SeamShift==new Vector2(direction>0 ? -480 : 480,-destination*360),"Attachment seam lost its lifecycle rebase");
                            Check(body.Velocity==resting,"A seam changed the native resting velocity");
                            if(destination!=0) break;
                            frame.Position=new Vector2(x,height);peer.Sample=frame;
                            // move back through the same self seam and repeat
                            Step(body);
                            Check(solver.Support==2 && Math.Abs(body.Position.X-x)<.001,"Reverse self seam lost the attachment");
                        }
                    }
                    map.Info=b=>screens[JKRuntime.Geometry.MapTopology.ScreenAt(b.Center.Y,screens.Length)].GetCollisionInfo(b);
                    body=Native(new Vector2(x,height),map,new Bumps());body.Velocity=new Vector2(direction*4,0);
                    AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(body,true);
                    var behaviours=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);
                    behaviours.AddAfter(behaviours.Find(behaviours.First(b=>b is UpdateXPositionFromVelocityBehaviour)),new HandlePlayerTeleportBehaviour());
                    var passenger=Frame(x,height-26);passenger.Support=1;passenger.SupportOffset=new Vector2(0,-26);
                    peer=new InteractionPeer{Id=2,Frame=passenger,Sample=passenger};peers=new[]{peer};solver=new PlayerContacts();
                    using(new ContactPipeline(body,solver,()=>peers,()=>InteractionRules.Solid,()=>1,map.Blocked)) {
                        Step(body);
                        Check(Math.Abs(body.Position.X-(direction>0 ? -8 : 470))<.001,"A carrier couldn't cross a native seam with a rider");
                        Check(Math.Abs(body.Position.Y-(height-destination*360))<.001 && body.Velocity.X*direction>0,"Carrier seam became a bounce or a vertical correction");
                        Check(Math.Abs(peer.Sample.Position.X-body.Position.X)<=4 && Math.Abs(peer.Sample.Position.Y+26-body.Position.Y)<.001,"Passenger contact stayed in the departed chart");
                    }
                }
                Vector2 point;float rotation;
                Check(PlayerIndicators.Place(new Vector2(-30,180),out point,out rotation) && point==new Vector2(9,180),"Side-linked player didn't receive a horizontal indicator");
                Check(PlayerIndicators.Place(new Vector2(240,-20),out point,out rotation) && point==new Vector2(240,9),"Vertical indicator left the viewport");
                Check(!PlayerIndicators.Place(new Vector2(200,100),out point,out rotation),"Visible player still received an indicator");
            } finally {screensField.SetValue(null,oldScreens);countField.SetValue(null,oldCount);cameraField.SetValue(null,oldCamera);JKRuntime.Geometry.MapTopology.Invalidate();}
        }
        private static void Landing()
        {
            // the stale passenger pose intersects an overhang; terrain landing must still win
            var map=new Map{Roof=new Rectangle(90,80,60,4)};var body=Native(new Vector2(100,94),map,new Bumps());body.Velocity=new Vector2(0,12);
            var passenger=Frame(100,68);passenger.Support=1;passenger.SupportEpoch=1;passenger.SupportOffset=new Vector2(0,-26);
            IList<InteractionPeer> peers=new[]{new InteractionPeer{Id=2,Frame=passenger,Sample=passenger}};var solver=new PlayerContacts();
            using(new ContactPipeline(body,solver,()=>peers,()=>InteractionRules.Solid,()=>1,map.Blocked)) {
                Step(body);Check(body.Position.Y==100 && body.IsOnGround && !body.IsKnocked,"Passenger rewound a native terrain landing");
                for(int tick=0;tick<120;tick++) {body.Velocity.X=2;Step(body);Check(body.Position.Y>=100 && body.Position.Y<101 && !body.IsKnocked,"Carrier landing left an invalid map pose");}
                Check(body.Position.X==340,"Landed carrier couldn't walk with a passenger");
                passenger.Position=body.Position+passenger.SupportOffset;
                body.Velocity=new Vector2(0,-10);Step(body);Check(body.Position.Y<100 && body.Velocity.Y<0,"Landed carrier couldn't jump again");
                var airRider=Frame(100,74);airRider.Grounded=true;airRider.Support=1;
                Check(!airRider.Anchored,"An airborne passenger became a fixed wall");
            }
        }
        private static void Sound()
        {
            var map=new Map{Floor=1000};var bumps=new Bumps();var body=Native(new Vector2(78,100),map,bumps);body.Velocity=new Vector2(8,-1);
            var frame=Frame(100,100);frame.Grounded=true;IList<InteractionPeer> peers=new[]{new InteractionPeer{Id=2,Frame=frame,Sample=frame}};
            using(new ContactPipeline(body,new PlayerContacts(),()=>peers,()=>InteractionRules.Solid,()=>1,map.Blocked)) {
                Step(body);Check(bumps.Count==1 && body.IsKnocked,"Native phase missed or doubled the king bounce sound");
                Step(body);Check(bumps.Count==1,"Leaving the contact repeated its sound");
            }
        }
        private static void Probes()
        {
            var terrain=new AdvCollisionInfo(new List<IBlock>{new BoxBlock(new Rectangle(0,100,10,10))},true,(SlopeType)1,new Vector2(.6f,-.8f));
            var king=Frame(100,100);var feet=new Rectangle(101,100,10,1);
            var merged=SupportSurface.Merge(terrain,feet,new[]{king});
            Check(merged.GetCollidedBlocks().Count==2 && terrain.GetCollidedBlocks().Count==1,"Foot probe mutated shared native terrain data");
            Check(merged.IsInWind() && merged.SlopeType==terrain.SlopeType && merged.SlopeNormal==terrain.SlopeNormal,"Foot probe lost wind or slope metadata");
            Rectangle intersection;
            Check(((IBlock)merged.GetCollidedBlocks().OfType<PlayerHeadSurface>().Single()).Intersects(feet,out intersection)==BlockCollisionType.Collision_Blocking,"Generic walking query couldn't see the head plane");
            Check(ReferenceEquals(SupportSurface.Merge(terrain,new Rectangle(118,100,10,1),new[]{king}),terrain),"Walk-off query extended a king beyond its edge");
            Check(ReferenceEquals(SupportSurface.Merge(terrain,new Rectangle(101,99,10,1),new[]{king}),terrain),"Foot probe fabricated support above the head");
            Check(ReferenceEquals(SupportSurface.Merge(terrain,new Rectangle(101,100,18,26),new[]{king}),terrain),"Peer plane entered native body collision geometry");
        }
        private static void WalkingVisuals()
        {
            foreach(int side in new[]{-1,1}) {
                var solver=new PlayerContacts();var frame=Frame(100+18*side,100);frame.Grounded=true;
                var peer=new InteractionPeer{Id=2,Frame=frame,Sample=frame.Copy()};
                var body=new ContactBody{Before=new Vector2(100,100),Position=new Vector2(100,100),Width=18,Height=26,NativeGrounded=true};
                for(int tick=0;tick<100;tick++) {
                    peer.Sample.Position.X=frame.Position.X-side*(tick%10)*2;
                    solver.Resolve(body,new[]{peer},InteractionRules.Sides,1,r=>false);
                    var visual=solver.Project(2,peer.Sample,new Rectangle(100,100,18,26),1);
                    Check(!ContactRecovery.Overlaps(body.Position,18,26,visual),"Walking projection left overlapping drawn kings");
                    Check(side>0 ? visual.Position.X>=118 : visual.Position.X+18<=100,"Walking prediction flipped the established visual side");
                    Check(peer.Sample.Position.X==frame.Position.X-side*(tick%10)*2,"Visual projection changed the physics snapshot");
                }
            }
        }
    }
}
