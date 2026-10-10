using System;
using Microsoft.Xna.Framework;
namespace MultiplayerExpansion
{
    internal static class NetworkMotionTests
    {
        internal static void PausedGhost()
        {
            var type=typeof(AdvancedSession);
            var lobby=HarmonyLib.AccessTools.Field(type,"lobby");var peersField=HarmonyLib.AccessTools.Field(type,"peers");
            var peers=(System.Collections.Generic.Dictionary<ulong,InteractionPeer>)peersField.GetValue(null);
            object previous=lobby.GetValue(null);
            var ghostType=typeof(JumpKingMultiplayer.Models.MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Models.GhostPlayer",true);
            var ghost=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(ghostType);
            HarmonyLib.AccessTools.Property(ghostType,"SteamId").SetValue(ghost,new Steamworks.CSteamID(987),null);
            var trackerType=ghostType.GetNestedType("Tracker",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            var tracker=Activator.CreateInstance(trackerType,new[]{ghost});
            try
            {
                lobby.SetValue(null,1UL);
                double now=((System.Diagnostics.Stopwatch)HarmonyLib.AccessTools.Field(type,"clock").GetValue(null)).Elapsed.TotalSeconds;
                peers.Add(987,new InteractionPeer{Id=987,Frame=Frame(0),Received=now});
                var track=HarmonyLib.AccessTools.Method(trackerType,"Track");
                for(int i=0;i<1000;i++) track.Invoke(tracker,new object[]{new JumpKingMultiplayer.Models.TrackData()});
                var queue=(System.Collections.IList)HarmonyLib.AccessTools.Property(trackerType,"data").GetValue(tracker,null);
                Check(queue.Count==0,"Paused enhanced ghost accumulated legacy replay packets");
                peers.Remove(987);track.Invoke(tracker,new object[]{new JumpKingMultiplayer.Models.TrackData()});
                Check(queue.Count==1,"Legacy tracker was disabled for an unmodified peer");
                for(int i=0;i<1000;i++) track.Invoke(tracker,new object[]{new JumpKingMultiplayer.Models.TrackData{posX=i}});
                Check(queue.Count==1 && ((JumpKingMultiplayer.Models.TrackData)queue[0]).posX==999,"Legacy pause kept a stale movement backlog");
                var sync=HarmonyLib.AccessTools.Field(trackerType,"enableSync");
                sync.SetValue(null,true);
                try { for(int i=0;i<100;i++) track.Invoke(tracker,new object[]{new JumpKingMultiplayer.Models.TrackData()});Check(queue.Count==32,"Explicit legacy replay wasn't bounded"); }
                finally {sync.SetValue(null,false);}
            }
            finally { peers.Remove(987);lobby.SetValue(null,previous); }
            Console.WriteLine("[OK] Installed ghost tracker: pause doesn't accumulate packets; legacy peers keep their tracker");
        }
        private static int checks;
        private static void Check(bool value,string reason) { checks++; if(!value) throw new Exception(reason); }
        private static InteractionFrame Frame(int tick)
        { return new InteractionFrame { Modern=true,Epoch=1,Sequence=(ulong)(tick+1),Revision=1,RulesEpoch=1,Active=true,Time=tick/60.0,Position=new Vector2(tick*2,100),Width=18,Height=26 }; }
        internal static void Run()
        {
            var motion=new RemoteMotion();
            motion.Accept(Frame(0),10); motion.Accept(Frame(2),10+2/60.0);
            var middle=motion.Sample(10+2/60.0,true);
            Check(Math.Abs(middle.Position.X-2)<.001,"Local interpolation didn't fill a missing frame");
            var next=motion.Sample(10+3/60.0,true);
            Check(Math.Abs(next.Position.X-4)<.001,"Local trajectory didn't advance smoothly");
            var held=motion.Sample(11,true);
            Check(held.Position.X<=8.01f && held.Position.X>=4,"Loss prediction wasn't bounded");
            Check(motion.Sample(12,true).Position==held.Position,"Prediction snapped back after a stalled peer");
            var teleport=Frame(3); teleport.Position=new Vector2(350,-3000);motion.Accept(teleport,12.1);
            Check(motion.Sample(12.1,true).Position==teleport.Position,"Teleport was smoothed through the map");
            var paused=Frame(4);paused.Position=teleport.Position;paused.Active=false;motion.Accept(paused,12.12);
            Check(motion.Sample(13,true).Position==paused.Position,"Paused peer drifted");
            motion=new RemoteMotion();motion.Accept(Frame(0),0);motion.Accept(Frame(1),1/60.0);
            var stop=Frame(2);stop.Position=Frame(1).Position;stop.Velocity=new Vector2(3,0);motion.Accept(stop,2/60.0);
            Check(motion.Sample(.1,true).Position==stop.Position,"Walking intent pushed a stopped ghost through a wall");
            var reverse=Frame(3);reverse.Position=Vector2.Zero;motion.Accept(reverse,3/60.0);
            Check(motion.Sample(.1,true).Position.X<=0,"Direction reversal retained old velocity");
            var mapChange=Frame(4);mapChange.Map=44;motion.Accept(mapChange,.1);
            Check(motion.Count==1 && motion.Sample(.1,true).Position==mapChange.Position,"Map transition interpolated across worlds");
            var respawn=Frame(0);respawn.Epoch=2;respawn.Position=new Vector2(300,200);motion.Accept(respawn,.12);
            Check(motion.Count==1 && motion.Sample(.12,true).Position==respawn.Position,"New attempt inherited the old timeline");
            // jitter, one dropped packet per eleven updates, and burst delivery
            motion=new RemoteMotion(); int sent=0; float previous=0; float maxStep=0;
            for(int tick=0;tick<600;tick++)
            {
                double now=tick/60.0;
                while(sent<=tick && sent/60.0+.025+(sent%5)*.004<=now)
                { if(sent%11!=7) motion.Accept(Frame(sent),now); sent++; }
                var sample=motion.Sample(now,false); if(sample==null) continue;
                if(tick>20) { Check(sample.Position.X>=previous-.001,"Jitter reversed constant forward motion");maxStep=Math.Max(maxStep,sample.Position.X-previous); }
                previous=sample.Position.X;
            }
            Check(maxStep<4.1f,"Jitter created large visible steps");
            Check(motion.Count<=32,"Snapshot history grew without a bound");
            var peer=new InteractionPeer(); peer.Accept(Frame(10),1);
            Check(!peer.Accept(Frame(9),1.1),"Late packet rewound the timeline");
            var packetFrame=Frame(0);packetFrame.Pose=12;packetFrame.Flip=2;packetFrame.Equipment=(1UL<<8)|(1UL<<6);
            InteractionFrame decoded;
            Check(InteractionWire.Decode(InteractionWire.Encode(packetFrame),out decoded) && decoded.Modern && decoded.Equipment==packetFrame.Equipment && decoded.Pose==12 && decoded.Flip==2,"Equipment/pose round trip failed");
            packetFrame.Equipment=1UL<<40;
            Check(!InteractionWire.Decode(InteractionWire.Encode(packetFrame),out decoded),"Unknown equipment bits accepted");
            packetFrame.Equipment=0;
            Check(InteractionWire.Decode(InteractionWire.Encode(packetFrame),out decoded) && decoded.Equipment==0,"Removing equipment retained old bits");
            var baseSprite=JumpKing.Sprite.CreateSprite(null,new Rectangle(0,0,48,48));
            var receiverHat=JumpKing.Sprite.CreateSprite(null,new Rectangle(48,0,48,48));
            var layerType=typeof(JumpKing.Game1).Assembly.GetType("JumpKing.XnaWrappers.LayeredSprite",true);
            var layered=(JumpKing.Sprite)Activator.CreateInstance(layerType,new object[]{baseSprite,new[]{receiverHat}});
            Check(ReferenceEquals(RemotePresentation.BaseSprite(layered),baseSprite),"Receiver equipment leaked into remote base sprite");
            // carrying uses the same interpolated position as the drawn ghost
            peer=new InteractionPeer{Id=2};peer.Accept(Frame(0),10);peer.Accept(Frame(2),10+2/60.0);peer.Prepare(10+2/60.0,true);
            var rider=new ContactBody{Before=new Vector2(2,73),Position=new Vector2(2,75),Velocity=new Vector2(0,2),Width=18,Height=26};
            var solver=new PlayerContacts();solver.Resolve(rider,new[]{peer},InteractionRules.Platforms,1,r=>false);
            peer.Prepare(10+3/60.0,true);rider.Before=rider.Position;rider.Velocity=new Vector2(0,.3f);
            solver.Resolve(rider,new[]{peer},InteractionRules.Platforms,1,r=>false);
            Check(Math.Abs(rider.Position.X-4)<.001 && rider.Grounded,"Rider followed raw packets instead of the displayed support");
            Console.WriteLine("[OK] Network motion and equipment: "+checks+" checks (jitter, loss, bounded prediction, teleport, pause, carry, wire)");
        }
    }
}
