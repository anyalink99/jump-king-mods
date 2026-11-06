using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using JumpKing.Player;
namespace MultiplayerExpansion
{
    internal static class ContactPhysicsTests
    {
        private static int checks;
        private static void Check(bool value,string message) {checks++;if(!value) throw new Exception(message);}
        private static InteractionFrame Frame(float x,float y,float vx=0,float vy=0)
        { return new InteractionFrame{Physics=true,Modern=true,Active=true,Epoch=1,Sequence=1,RulesEpoch=1,Revision=1,Rules=InteractionRules.Solid,Width=18,Height=26,Position=new Vector2(x,y),Velocity=new Vector2(vx,vy)}; }
        private static InteractionPeer Peer(InteractionFrame frame) {return new InteractionPeer{Id=2,Frame=frame,Sample=frame};}
        private static ContactBody Body(float x,float y,float vx,float vy)
        {return new ContactBody{Before=new Vector2(x,y),Position=new Vector2(x+vx,y+vy),Velocity=new Vector2(vx,vy),Width=18,Height=26};}
        internal static void Run()
        {
            Func<Rectangle,bool> empty=r=>false;
            var lower=Frame(100,100);var rider=Frame(100,74);rider.Support=1;rider.SupportEpoch=1;rider.SupportOffset=new Vector2(0,-26);
            var scene=new Dictionary<ulong,InteractionFrame>{{1,lower},{2,rider}};
            Func<ulong,InteractionFrame> lookup=id=>scene.ContainsKey(id)?scene[id]:null;
            // the carrier moves locally while the last passenger packet is 100 ms old
            for(int tick=0;tick<120;tick++)
            {
                lower.Position.X+=3;lower.Position.Y=tick<60 ? 100-tick*2 : -20+(tick-60)*2;
                var drawn=SupportGraph.Resolve(2,lookup);
                Check(drawn.Position==lower.Position+rider.SupportOffset,"Passenger drawing waited for another network packet");
            }
            var top=Frame(100,48);top.Support=2;top.SupportEpoch=1;top.SupportOffset=new Vector2(0,-26);scene.Add(3,top);
            Check(SupportGraph.Resolve(3,lookup).Position==lower.Position+new Vector2(0,-52),"Three-player support chain broke");
            lower.Support=2;lower.SupportEpoch=1;lower.SupportOffset=new Vector2(0,-26);
            Check(SupportGraph.Resolve(2,lookup)==null,"Cyclic support produced a moving feedback loop");
            lower.Support=0;lower.Epoch=2;
            Check(SupportGraph.Resolve(2,lookup).Position==rider.Position,"Old passenger attached to a respawned carrier");
            lower.Epoch=1;lower.Position=new Vector2(100,100);
            var solver=new PlayerContacts();var passenger=Peer(rider);
            var carrier=Body(100,100,0,-12);
            solver.Resolve(carrier,new[]{passenger},InteractionRules.Solid,1,empty);
            Check(carrier.Position.Y==88 && carrier.Velocity.Y==-12,"Passenger blocked the carrier's jump");
            carrier=Body(100,100,0,-17);
            solver.Resolve(carrier,new[]{passenger},InteractionRules.Solid,1,r=>r.Top<60);
            Check(carrier.Position.Y==86 && carrier.Velocity.Y==0 && carrier.Bump,"Carrier drove its passenger through a ceiling or missed its impact sound");
            solver=new PlayerContacts();var platform=Peer(lower);var body=Body(100,73,0,2);
            solver.Resolve(body,new[]{platform},InteractionRules.Solid,1,empty);
            Check(body.Grounded && solver.Support==2,"Rider did not attach");
            lower.Position+=new Vector2(3,-12);lower.Velocity=new Vector2(3,-12);lower.Grounded=false;
            body.Before=body.Position;body.Position.Y+=.3f;body.Velocity=new Vector2(0,.3f);
            solver.Resolve(body,new[]{platform},InteractionRules.Solid,1,empty);
            Check(body.Grounded && body.Position==new Vector2(103,62),"Rider detached when carrier jumped");
            var native=new BodyComp(body.Position,18,26);native.Velocity=new Vector2(0,-10);solver.Takeoff(native);
            Check(solver.Support==0 && native.Velocity==new Vector2(3,-22),"Midair passenger jump didn't detach with carrier momentum");
            // match the native wall restitution against an anchored king
            var wall=Frame(100,100);wall.Grounded=true;var peer=Peer(wall);solver=new PlayerContacts();
            body=Body(78,105,8,-1);solver.Resolve(body,new[]{peer},InteractionRules.Solid,1,empty);
            Check(body.Position.X==82 && body.Velocity.X==-8*JumpKing.PlayerValues.BOUNCE && body.Knocked && body.Bump,"Airborne king didn't bounce like a native wall");
            var groundedBody=Body(100,100,0,0);groundedBody.NativeGrounded=true;
            var flying=Peer(Frame(83,103,8,-1));solver=new PlayerContacts();
            solver.Resolve(groundedBody,new[]{flying},InteractionRules.Solid,1,empty);
            Check(groundedBody.Position==new Vector2(100,100),"Air impact displaced the grounded wall king");
            wall.Grounded=false;wall.Velocity=new Vector2(-8,0);solver=new PlayerContacts();body=Body(78,105,8,0);
            Vector2 target=Vector2.Zero;Vector2 normal=Vector2.Zero;
            solver.Impact=(p,n,a,b)=>{target=b;normal=n;};
            solver.Resolve(body,new[]{peer},InteractionRules.Solid,1,empty);
            Check(body.Velocity.X==-4 && target.X==4 && normal.X==-1 && body.Bump,"Two airborne kings didn't exchange a symmetric bounce");
            var authority=new ContactLedger();authority.Record(1,peer,normal,body.Velocity,target,1);
            var hit=authority.Pending(1.1)[0];var replica=new ContactLedger();var ownerPeer=new InteractionPeer{Id=1,Frame=Frame(82,105,8,0)};
            replica.Record(2,ownerPeer,-normal,new Vector2(3,0),new Vector2(-3,0),1.05);
            var velocity=new Vector2(3,2);
            Check(replica.Confirm(1,2,1,0,hit,.1,1.15,false,ref velocity) && velocity==new Vector2(4,2),"Authority correction overwrote gravity or failed to reconcile prediction");
            Check(!replica.Confirm(1,2,1,0,hit,.11,1.16,false,ref velocity) && velocity.X==4,"Duplicate impact bounced twice");
            replica=new ContactLedger();velocity=new Vector2(-8,-12);
            Check(!replica.Confirm(1,2,1,1,hit,.1,1.15,false,ref velocity),"Late collision overrode a newer jump");
            Check(!replica.Confirm(1,2,2,0,hit,.1,1.15,false,ref velocity),"Late collision crossed an attempt boundary");
            Check(!replica.Confirm(1,2,1,0,hit,.3,1.35,false,ref velocity),"Expired impact was applied");
            Check(!replica.Confirm(3,2,1,0,hit,.1,1.15,false,ref velocity),"Non-owner overrode the pair result");
            Check(authority.Pending(1.5).Length==0,"Expired impacts weren't released");
            var wire=Frame(100,74);wire.Support=2;wire.SupportEpoch=1;wire.SupportOffset=new Vector2(0,-26);wire.Jump=3;wire.Time=1.2;wire.Echo=.7;wire.EchoAge=.01;wire.Impacts=new[]{hit};
            InteractionFrame decoded;
            Check(InteractionWire.Decode(InteractionWire.Encode(wire),out decoded) && decoded.Support==2 && decoded.SupportEpoch==1 && decoded.Impacts[0].Outgoing==target && decoded.Jump==3,"Physics wire round trip lost support or impacts");
            var truncated=InteractionWire.Encode(wire);Array.Resize(ref truncated,truncated.Length-1);
            Check(!InteractionWire.Decode(truncated,out decoded),"Truncated impact payload accepted");
            var timing=new InteractionPeer();wire.Impacts=new ContactImpact[0];wire.Time=1;wire.Echo=.8;wire.EchoAge=.05;
            timing.Accept(wire,1.05);Check(Math.Abs(timing.OneWay-.1)<.0001,"RTT included remote scheduling delay");
            wire=Frame(1,1);wire.Epoch=2;timing.Accept(wire,1.1);wire=Frame(2,2);wire.Sequence=999;
            Check(!timing.Accept(wire,1.2),"Delayed packet restored a retired attempt");
            var motion=new RemoteMotion();var flight=Frame(100,100,0,-12);flight.Time=0;motion.Accept(flight,0);
            flight=Frame(100,88,0,-11.7f);flight.Time=1/60.0;flight.Sequence=2;motion.Accept(flight,1/60.0);
            var future=motion.Predict(1/60.0,.1);
            float expectedY=88,vy=-11.7f;for(int i=0;i<6;i++){expectedY+=vy;vy+=.3f;}
            Check(Math.Abs(future.Position.Y-expectedY)<.001,"Air prediction didn't match six native integration steps");
            JitterAndLoss();
            NativeRider();
            ContactSafetyTests.Run();
            Console.WriteLine("[OK] Contact physics: "+checks+" checks (support graph, moving launch, air jump, native bounce, pair authority, replay protection, jitter/loss)");
        }
        private sealed class EmptyMap : JumpKing.API.ICollisionQuery
        {
            public JumpKing.Level.AdvCollisionInfo GetCollisionInfo(Rectangle bounds) {return new JumpKing.Level.AdvCollisionInfo();}
            public bool CheckCollision(Rectangle b,out Rectangle r,out JumpKing.Level.AdvCollisionInfo i) {r=Rectangle.Empty;i=GetCollisionInfo(b);return false;}
            public bool CheckCollision(Rectangle b,out Rectangle r,out JumpKing.Level.AggregateCollisionInfo i) {r=Rectangle.Empty;i=new JumpKing.Level.AggregateCollisionInfo(GetCollisionInfo(b));return false;}
            public bool IsInWater(Rectangle b) {return false;}
        }
        private static void NativeRider()
        {
            var body=new BodyComp(new Vector2(100,73),18,26);body.Velocity=new Vector2(0,2);
            var handlers=new LinkedList<JumpKing.API.IBlockBehaviour>();
            var behaviors=(LinkedList<JumpKing.API.IBodyCompBehaviour>)HarmonyLib.AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);behaviors.Clear();
            behaviors.AddLast(new JumpKing.BodyCompBehaviours.UpdateXPositionFromVelocityBehaviour(handlers));
            behaviors.AddLast(new JumpKing.BodyCompBehaviours.UpdateYPositionFromVelocityBehaviour(handlers));
            behaviors.AddLast((JumpKing.API.IBodyCompBehaviour)Activator.CreateInstance(typeof(JumpKing.BodyCompBehaviours.ResolveYCollisionBehaviour),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{new EmptyMap(),handlers},null));
            behaviors.AddLast(new JumpKing.BodyCompBehaviours.ApplyGravityBehaviour(handlers));
            var integrate=HarmonyLib.AccessTools.Method(typeof(BodyComp),"UpdateInternal");
            var carrier=Frame(100,100);var peers=new[]{Peer(carrier)};var solver=new PlayerContacts();
            Action step=delegate {var before=body.Position;integrate.Invoke(body,new object[]{1f/60});solver.Apply(body,before,peers,InteractionRules.Solid,1,r=>false);};
            step();carrier.Velocity=new Vector2(2,-12);
            for(int tick=0;tick<12;tick++)
            {
                carrier.Position+=carrier.Velocity;carrier.Velocity.Y+=JumpKing.PlayerValues.GRAVITY;step();
                Check(body.IsOnGround && Math.Abs(body.Position.Y+26-carrier.Position.Y)<.001 && Math.Abs(body.Position.X-carrier.Position.X)<.001,"Native gravity lost the rider during the carrier's ascent");
            }
            body.Velocity=new Vector2(0,-10);solver.Takeoff(body);step();
            Check(!body.IsOnGround && body.Position.Y+26<carrier.Position.Y && solver.Support==0,"Native rider couldn't launch from an airborne carrier");
        }
        private static void JitterAndLoss()
        {
            foreach(double latency in new[]{.0,.04,.08,.12})
            {
                var remote=new InteractionPeer();var packets=new Queue<Tuple<double,InteractionFrame>>();
                float last=0;bool sampled=false;
                for(int tick=0;tick<300;tick++)
                {
                    double now=tick/60.0;var frame=Frame(tick*2,100,2,0);frame.Time=now;frame.Sequence=(ulong)(tick+1);frame.Grounded=true;
                    if(tick%13!=6) packets.Enqueue(Tuple.Create(now+latency+(tick%3)*.004,frame));
                    while(packets.Count>0 && packets.Peek().Item1<=now) {var packet=packets.Dequeue();remote.Accept(packet.Item2,now);}
                    if(remote.Frame==null) continue;
                    remote.OneWay=latency;remote.Prepare(now,latency==0);
                    if(sampled && tick>20) Check(remote.Sample.Position.X>=last-.01f && remote.Sample.Position.X-last<8,"Jitter/loss caused a large contact rewind");
                    last=remote.Sample.Position.X;sampled=true;
                }
            }
        }
    }
}
