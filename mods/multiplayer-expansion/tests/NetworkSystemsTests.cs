using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class NetworkSystemsTests
    {
        private static int checks;
        private static void Check(bool value,string message) {checks++;if(!value) throw new Exception(message);}
        private static InteractionFrame Frame(float x=100)
        {return new InteractionFrame{Epoch=1,Sequence=1,RulesEpoch=1,Revision=1,Active=true,Modern=true,Physics=true,Rules=InteractionRules.Solid,Width=18,Height=26,Position=new Vector2(x,100)};}
        internal static void Run(string root)
        {
            Events();States();Smoothing();Conditions(root);
            foreach(int latency in new[]{0,100,250})
            {var first=World(latency);Check(first==World(latency),"Four-peer simulation wasn't repeatable");}
            Console.WriteLine("[OK] Network systems: "+checks+" checks (event continuity, staged impacts, protocol states, smoothing, fault injection, bounded recording/replay, four-peer seeded worlds)");
        }
        private static void Events()
        {
            var ledger=new ContactLedger();var receiver=new ContactLedger();var remote=new InteractionPeer{Id=2,Frame=Frame()};
            ledger.Record(1,remote,new Vector2(-1,0),new Vector2(-4,0),new Vector2(4,0),1);
            var hit=ledger.Pending(1)[0];var velocity=new Vector2(-8,0);bool sound;
            Check(receiver.Confirm(1,2,1,0,hit,0,1,false,ref velocity,out sound),"First collision was rejected");
            ledger.Reset();receiver.Reset();
            Check(!receiver.Confirm(1,2,1,0,hit,0,1.01,false,ref velocity,out sound),"Pause reset forgot a consumed event");
            ledger.Record(1,remote,new Vector2(-1,0),new Vector2(-4,0),new Vector2(4,0),2);
            var next=ledger.Pending(2)[0];velocity=new Vector2(-8,0);
            Check(next.Id>hit.Id && receiver.Confirm(1,2,1,0,next,0,2,false,ref velocity,out sound),"New collision after pause reused a consumed ID");
            var inbox=new ImpactInbox();var frame=Frame();frame.Time=2;frame.Impacts=new[]{next};
            inbox.Add(1,frame,2,0);inbox.Add(1,frame,2.01,0);
            Check(inbox.Count==1,"Repeated packet queued an impact twice");
            int applied=0;inbox.Drain(2.02,(item,age)=>{applied++;Check(age>=.02-1e-6 && item.Matches(frame,0,0,1,1),"Queued event lost timing or identity");Check(!item.Matches(frame,1,0,1,1),"Pre-menu event crossed a state transition");});
            Check(applied==1 && inbox.Count==0,"Impact was applied outside a single physics drain");
            inbox.Add(1,frame,2,0);inbox.Drain(3,(item,age)=>applied++);Check(applied==1,"Old queued event survived a stall");
            for(int i=0;i<100;i++) {frame.Impacts=new[]{new ContactImpact{Id=(ulong)i+1,Time=2}};inbox.Add(1,frame,2,0);}
            Check(inbox.Count==64 && inbox.Dropped>=36,"Impact inbox grew without a bound");inbox.Forget(1);Check(inbox.Count==0,"Departed peer retained pending impacts");
            var order=new List<ulong>();
            frame.Impacts=new[]{next};inbox.Add(3,frame,2,0);inbox.Add(1,frame,2,0);inbox.Drain(2,(item,age)=>order.Add(item.Source));
            Check(order.Count==2 && order[0]==1 && order[1]==3,"Packet arrival order changed contact event order");
        }
        private static void States()
        {
            foreach(PlayerPresence state in Enum.GetValues(typeof(PlayerPresence)))
            {
                var frame=Frame();frame.Presence=state;frame.Active=state==PlayerPresence.Playing || state==PlayerPresence.Paused;frame.SimulationTick=50;frame.Transition=7;
                InteractionFrame decoded;Check(InteractionWire.Decode(InteractionWire.Encode(frame),out decoded) && decoded.Physics && decoded.Presence==state && decoded.SimulationTick==50 && decoded.Transition==7,"Explicit presence round trip failed");
            }
            var paused=Frame();SessionPresence.Freeze(paused,true);
            Check(!paused.Grounded && paused.Anchored,"Paused midair king was mislabeled as grounded");
            var malformed=InteractionWire.Encode(paused);malformed[7]=99;InteractionFrame read;
            Check(!InteractionWire.Decode(malformed,out read),"Unknown player presence was accepted");
            paused.Velocity.X=3;Check(!InteractionWire.Decode(InteractionWire.Encode(paused),out read),"Paused body retained movement");
            var ordinary=Frame();byte[] current=InteractionWire.Encode(ordinary);
            var old=new byte[136];Array.Copy(current,0,old,0,135);old[4]=3;old[7]=0;old[135]=0;
            Check(InteractionWire.Decode(old,out read) && read.Modern && !read.Physics,"Old protocol was treated as a compatible physical peer");
            var peer=new InteractionPeer();ordinary.SimulationTick=10;ordinary.Transition=3;peer.Accept(ordinary,1);
            var delayed=ordinary.Copy();delayed.Sequence++;delayed.Transition=2;
            Check(!peer.Accept(delayed,1.1),"Old presence revision was accepted with a newer packet number");
            delayed.Transition=3;delayed.SimulationTick=9;
            Check(!peer.Accept(delayed,1.1),"Simulation tick moved backwards");
        }
        private static void Smoothing()
        {
            var visual=new VisualCorrection();var before=Frame(100);var after=Frame(104);
            visual.Correct(before,after,0,true);
            Check(visual.Apply(after,0,false).Position.X==100 && after.Position.X==104,"Smoothing changed the physical snapshot or snapped a small correction");
            float halfway=visual.Apply(after,.04,false).Position.X;
            Check(halfway>100 && halfway<104 && Math.Abs(visual.Apply(after,.4,false).Position.X-104)<.01,"Visual correction didn't converge");
            visual.Correct(before,after,1,true);Check(visual.Apply(after,1,true).Position==after.Position,"Visual offset separated a local rider from its support");
            after.Position.X=400;visual.Correct(before,after,2,true);Check(visual.Apply(after,2,false).Position==after.Position,"Teleport was smoothed through the world");
            var motion=new InteractionPeer();for(int i=0;i<100;i++) {var frame=Frame(i*2);frame.Time=i/60.0;frame.Sequence=(ulong)i+1;frame.Velocity.X=2;motion.Accept(frame,frame.Time);}
            Check(motion.Visual.Error<.001,"Smoothing introduced lag during uniform movement");
            var parent=Frame(100);var child=Frame(100);child.Support=1;child.SupportEpoch=1;child.SupportOffset=new Vector2(0,-26);
            visual=new VisualCorrection();after=parent.Copy();after.Position.X=104;visual.Correct(parent,after,0,true);
            var rendered=visual.Apply(after,.02,false);
            Check(SupportGraph.Resolve(2,id=>id==1 ? rendered : child).Position==rendered.Position+child.SupportOffset,"Passenger didn't share its carrier's visual correction");
        }
        private static void Conditions(string root)
        {
            Check(NetworkConditions.Parse("100 30 5 10 123 0")!=null && NetworkConditions.Parse("99999 30 5 10 123 0")==null,"Unsafe network conditions accepted");
            var options=new NetworkConditions{Delay=100,Jitter=90,Loss=15,Duplicate=20,Seed=7};
            var a=new PacketSchedule(7);var b=new PacketSchedule(7);var order=new List<int>();
            for(int i=0;i<200;i++) {var packet=BitConverter.GetBytes(i);a.Enqueue(packet,i*.001,options);b.Enqueue(packet,i*.001,options);}
            for(int i=0;i<1000;i++)
            {
                byte[] x,y;do {x=a.Take(i*.001);y=b.Take(i*.001);Check((x==null)==(y==null) && (x==null || BitConverter.ToInt32(x,0)==BitConverter.ToInt32(y,0)),"Seeded packet schedules diverged");if(x!=null) order.Add(BitConverter.ToInt32(x,0));} while(x!=null);
            }
            bool reordered=false;for(int i=1;i<order.Count;i++) reordered|=order[i]<order[i-1];
            Check(reordered && a.Dropped>0 && a.Duplicated>0 && a.Count==0,"Fault injection didn't cover loss, duplicates and reorder");
            var bounded=new PacketSchedule(1);for(int i=0;i<1000;i++) bounded.Enqueue(new byte[65536],0,new NetworkConditions{Delay=500});
            Check(bounded.Count<=64 && bounded.Dropped>0,"Fault queue exceeded its byte budget");
            string path=Path.Combine(root,"replay-test.trace");
            using(var trace=new NetworkTrace(path,1,0))
            {
                for(int i=0;i<60;i++) {var frame=Frame(i);frame.Sequence=(ulong)i+1;frame.Time=i/60.0;trace.Record(i%2==0,InteractionWire.Encode(frame),i/60.0);}
                trace.Record(false,InteractionWire.Encode(Frame()),121);Check(!trace.Recording,"Trace recording ignored its time bound");
            }
            var first=NetworkReplay.Run(path);var second=NetworkReplay.Run(path);
            Check(first.Hash==second.Hash && first.Records==60 && first.Accepted==60,"Recorded packets didn't replay deterministically");
            for(int i=0;i<4;i++) using(var trace=new NetworkTrace(path,1,0)) trace.Record(true,InteractionWire.Encode(Frame()),0);
            Check(File.Exists(path+".1") && File.Exists(path+".2") && !File.Exists(path+".3"),"Trace retention exceeded three files");
            string bad=Path.Combine(root,"invalid.trace");File.WriteAllText(bad,"MPEXTRACE1 1\nNaN R AA==\n");
            bool rejected=false;try {NetworkReplay.Run(bad);}catch(IOException) {rejected=true;}Check(rejected,"Replay accepted a non-finite timestamp");
        }
        private static string World(int latency)
        {
            const int count=4;var bodies=new ContactBody[count];var solvers=new PlayerContacts[count];
            var views=new InteractionPeer[count,count];var links=new PacketSchedule[count,count];
            var conditions=new NetworkConditions{Delay=latency,Jitter=latency/2,Loss=latency==0 ? 0 : 10,Duplicate=10};
            var result=new StringBuilder();
            for(int i=0;i<count;i++) {bodies[i]=new ContactBody{Position=new Vector2(100+i*20,100),Width=18,Height=26,NativeGrounded=true};solvers[i]=new PlayerContacts();for(int j=0;j<count;j++) {views[i,j]=new InteractionPeer{Id=(ulong)j+1};links[i,j]=new PacketSchedule(31+i*13+j);}}
            for(int tick=0;tick<540;tick++)
            {
                double now=tick/60.0;
                for(int i=0;i<count;i++) for(int j=0;j<count;j++) if(i!=j)
                {
                    byte[] packet;while((packet=links[i,j].Take(now))!=null) {InteractionFrame frame;Check(InteractionWire.Decode(packet,out frame),"Simulated world produced an invalid wire frame");views[i,j].Accept(frame,now);}
                    if(views[i,j].Frame!=null) views[i,j].Prepare(now,false);
                }
                for(int i=0;i<count;i++)
                {
                    bool paused=i==0 && tick>=180 && tick<240;var body=bodies[i];body.Before=body.Position;
                    if(i==2 && tick==300) {body.Position=new Vector2(40,50);body.Velocity=Vector2.Zero;solvers[i].Reset();}
                    if(!paused)
                    {
                        body.Velocity.X=tick%160<100 ? (i%2==0 ? 2 : -2) : 0;
                        if(body.NativeGrounded && tick%120==i*15) body.Velocity.Y=-8;
                        body.Position+=body.Velocity;body.Velocity.Y+=.3f;body.NativeGrounded=false;
                        if(body.Position.Y>=100) {body.Position.Y=100;body.Velocity.Y=0;body.NativeGrounded=true;}
                        body.Position.X=Math.Max(0,Math.Min(462,body.Position.X));
                        var peers=new List<InteractionPeer>();for(int j=0;j<count;j++) if(i!=j && views[i,j].Ready(now,0,1,1,InteractionRules.Solid)) peers.Add(views[i,j]);
                        solvers[i].Resolve(body,peers,InteractionRules.Solid,(ulong)i+1,r=>r.Left<0 || r.Right>480 || r.Bottom>126);
                        body.NativeGrounded|=body.Grounded;
                    }
                    Check(!float.IsNaN(body.Position.X) && !float.IsNaN(body.Position.Y) && body.Position.X>=0 && body.Position.X<=462 && body.Position.Y<=100,"Network contacts escaped terrain or produced a non-finite body: delay="+latency+" tick="+tick+" peer="+i+" position="+body.Position);
                    var frame=Frame(body.Position.X);frame.Position=body.Position;frame.Velocity=body.Velocity;frame.Grounded=body.NativeGrounded;
                    frame.Time=now;frame.Sequence=(ulong)tick+1;frame.SimulationTick=(ulong)tick;frame.Epoch=i==3 && tick>=450 ? 2UL : 1UL;
                    frame.Transition=(ulong)(tick>=180 ? 1 : 0)+(ulong)(tick>=240 ? 1 : 0)+(ulong)(i==2 && tick>=300 ? 1 : 0);
                    frame.Support=solvers[i].Support;frame.SupportEpoch=frame.Support==0 ? 0UL : 1UL;frame.SupportOffset=solvers[i].SupportOffset;
                    SessionPresence.Freeze(frame,paused);
                    if(!(i==3 && tick>=400 && tick<450)) for(int j=0;j<count;j++) if(i!=j) links[j,i].Enqueue(InteractionWire.Encode(frame),now,conditions);
                    result.Append(body.Position.X.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(body.Position.Y.ToString("R",CultureInfo.InvariantCulture)).Append(';');
                }
            }
            return result.ToString();
        }
    }
}
