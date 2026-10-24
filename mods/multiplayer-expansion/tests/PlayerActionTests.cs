using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKingMultiplayer.Models;
using JumpKing.Controller;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class PlayerActionTests
    {
        private static int checks;
        private static void Check(bool value,string message) {checks++;if(!value) throw new Exception(message);}
        private static InteractionFrame Frame(float x,float y=100)
        {return new InteractionFrame{Physics=true,Modern=true,Active=true,Epoch=4,Sequence=1,RulesEpoch=8,Revision=2,Map=7,Width=18,Height=26,Position=new Vector2(x,y),Presence=PlayerPresence.Playing};}
        private static PlayerActionPacket Request()
        {return new PlayerActionPacket{Kind=PlayerActionKind.KickRequest,Session=8,Revision=2,Map=7,Serial=1,Policy=1,Actor=1,ActorEpoch=4,Target=2,TargetEpoch=4,Direction=1,Time=1,Origin=new Vector2(109,113),Hit=new Vector2(139,113)};}
        internal static void Run()
        {
            Wire();Authority();Replay();Inbox();PolicyDelivery();Teleport();Bindings();
            Console.WriteLine("[OK] Player actions: "+checks+" checks (wire validation, authority, range/terrain, cooldown, replay, epochs, placement and binding slots)");
        }
        private static void Wire()
        {
            var request=Request();var bytes=PlayerActionWire.Encode(request);PlayerActionPacket decoded;
            Check(bytes.Length==PlayerActionWire.Size && PlayerActionWire.Decode(bytes,out decoded) && decoded.Target==2 && decoded.Policy==1 && decoded.Hit==request.Hit,"Kick wire lost identity, policy or impact geometry");
            for(int size=0;size<bytes.Length;size++) {var shortBytes=bytes.Take(size).ToArray();Check(!PlayerActionWire.Decode(shortBytes,out decoded),"Truncated action packet accepted");}
            var extended=bytes.Concat(new byte[]{0}).ToArray();Check(!PlayerActionWire.Decode(extended,out decoded),"Action packet with extra bytes accepted");
            foreach(Action<PlayerActionPacket> corrupt in new Action<PlayerActionPacket>[] {
                p=>p.Kind=(PlayerActionKind)255,p=>p.Flags=4,p=>p.Flags=1,p=>p.Direction=0,p=>p.Direction=2,p=>p.Session=0,p=>p.Revision=0,p=>p.Policy=0,
                p=>p.Serial=0,p=>p.Actor=0,p=>p.ActorEpoch=0,p=>p.Target=0,p=>p.Target=1,p=>p.TargetEpoch=0,p=>p.Time=double.NaN,p=>p.Time=double.PositiveInfinity,p=>p.Time=-1,
                p=>p.Origin=new Vector2(float.NaN,0),p=>p.Hit=new Vector2(0,float.PositiveInfinity),p=>p.Hit=new Vector2(1e8f,0)
            }) {var invalid=request.Copy();corrupt(invalid);Check(!PlayerActionWire.Decode(PlayerActionWire.Encode(invalid),out decoded),"Malformed kick packet accepted");}
            foreach(var kind in new[]{PlayerActionKind.Policy,PlayerActionKind.Hello}) {
                var packet=Request();packet.Kind=kind;packet.Target=packet.TargetEpoch=packet.TargetTransition=0;packet.Direction=0;packet.Policy=kind==PlayerActionKind.Policy ? packet.Serial : 0;
                Check(PlayerActionWire.Decode(PlayerActionWire.Encode(packet),out decoded),"Policy/hello packet rejected");
                packet.Target=2;Check(!PlayerActionWire.Decode(PlayerActionWire.Encode(packet),out decoded),"Non-kick packet carried a target");
            }
            var movement=InteractionWire.Encode(Frame(100));Check(!PlayerActionWire.Recognizes(movement),"Action decoder took a movement packet");
            InteractionFrame frame;Check(!InteractionWire.Decode(bytes,out frame),"Movement decoder took an action packet");
            Check(!WorldWire.Recognizes(bytes) && !WorldInputs.Recognizes(bytes) && !PlayerActionWire.Recognizes(WorldWire.Hello(8)),"Action and world message tags overlap");
            var random=new Random(17);
            for(int i=0;i<2000;i++) {var noise=new byte[PlayerActionWire.Size];random.NextBytes(noise);Check(!PlayerActionWire.Decode(noise,out decoded),"Random packet enabled an action");}
        }
        private static void Authority()
        {
            Func<Rectangle,bool> empty=r=>false;
            var source=Frame(100);var target=Frame(130);var request=Request();var authority=new KickAuthority();
            Check(authority.Accept(request,source,target,.05,1,empty),"Host rejected a valid nearby kick");
            for(int i=0;i<100;i++) Check(!authority.Accept(request,source,target,0,1.6,empty),"Duplicate request repeated the kick");
            request.Serial=2;Check(!authority.Accept(request,source,target,0,1.2,empty),"Cooldown permitted a second request");
            Check(!authority.Accept(request,source,target,0,2,empty),"A previously rejected serial replayed after cooldown");
            request.Serial=3;Check(authority.Accept(request,source,target,0,1.56,empty),"Fresh kick didn't recover after cooldown");
            foreach(Action<PlayerActionPacket> change in new Action<PlayerActionPacket>[] {
                p=>p.ActorEpoch++,p=>p.ActorTransition++,p=>p.TargetEpoch++,p=>p.TargetTransition++,p=>p.Session++,p=>p.Revision++,p=>p.Map++,p=>p.Actor=p.Target,p=>p.Direction=0,p=>p.Kind=PlayerActionKind.KickEvent
            }) {var invalid=Request();change(invalid);Check(!new KickAuthority().Accept(invalid,source,target,0,10,empty),"Host accepted a mismatched request");}
            foreach(double age in new[]{-.1,.36,1,double.NaN,double.PositiveInfinity}) Check(!new KickAuthority().Accept(Request(),source,target,age,10,empty),"Stale or invalid request age accepted");
            Check(PlayerActionRules.Reachable(source,Frame(190),1,empty),"72-pixel edge reach rejected its boundary");
            Check(!PlayerActionRules.Reachable(source,Frame(190.1f),1,empty),"Kick exceeded its edge range");
            Check(!PlayerActionRules.Reachable(source,Frame(90),1,empty),"Forward kick hit behind its owner");
            Check(PlayerActionRules.Reachable(source,Frame(90),-1,empty),"Left-facing kick failed");
            Check(!PlayerActionRules.Reachable(source,Frame(130,139),1,empty),"Kick hit another vertical lane");
            Check(!PlayerActionRules.Reachable(source,target,1,r=>r.X>=120 && r.X<=122),"Kick passed through terrain");
            foreach(var presence in new[]{PlayerPresence.Paused,PlayerPresence.Unavailable,PlayerPresence.Teleporting,PlayerPresence.Spawning}) {
                var frozen=target.Copy();frozen.Presence=presence;
                Check(!PlayerActionRules.Reachable(source,frozen,1,empty),"Kick moved a frozen or transitioning king");
                Check(!PlayerActionRules.Reachable(frozen,source,-1,empty),"Frozen king could kick");
            }
            var otherMap=target.Copy();otherMap.Map++;Check(!PlayerActionRules.Reachable(source,otherMap,1,empty),"Kick crossed maps");
            var older=target.Copy();older.Physics=false;Check(!PlayerActionRules.Reachable(source,older,1,empty),"Kick acted on an old movement-only client");
            var velocity=new Vector2(3,5);velocity+=PlayerActionRules.Impulse(1);Check(velocity==new Vector2(9,2),"Kick replaced velocity instead of adding momentum");
            request=Request();request.ActorEpoch=source.Epoch=5;
            Check(authority.Accept(request,source,target,0,1.6,empty),"New attempt inherited the old action replay window");
        }
        private static void Replay()
        {
            var window=new ActionReplayWindow();Check(window.Accept(100) && window.Accept(99) && !window.Accept(99),"Reordered events failed or duplicates replayed");
            Check(!window.Accept(36) && window.Accept(37) && !window.Accept(0),"Replay window boundary failed");
            for(ulong id=101;id<2000;id++) Check(window.Accept(id) && !window.Accept(id-64),"Event history grew beyond its bounded window");
            Check(window.Accept(ulong.MaxValue) && !window.Accept(1),"Replay arithmetic wrapped around");
            window.Reset();Check(window.Accept(1),"New session retained the previous replay window");
        }
        private static void Teleport()
        {
            var target=Frame(100);var peers=new List<InteractionPeer>{new InteractionPeer{Id=2,Frame=target,Sample=target}};Vector2 result;
            Check(TeleportPlacement.Find(target,18,26,peers,1,r=>r.Bottom>126,out result) && result==new Vector2(82,100),"Teleport didn't choose free space beside the target");
            Check(!ContactRecovery.Overlaps(result,18,26,target),"Teleport spawned inside its target");
            var third=Frame(82);peers.Add(new InteractionPeer{Id=3,Frame=third,Sample=third});
            Check(TeleportPlacement.Find(target,18,26,peers,1,r=>r.Bottom>126,out result) && peers.All(p=>!ContactRecovery.Overlaps(result,18,26,p.Frame)),"Teleport entered a third king");
            Check(!TeleportPlacement.Find(target,18,26,peers,1,r=>true,out result),"Teleport entered terrain when no free position existed");
            Check(target.Position==new Vector2(100,100) && third.Position==new Vector2(82,100),"Placement search moved an existing king");
            Check(TeleportPlacement.Find(target,30,40,peers,1,r=>r.Bottom>126,out result) && peers.All(p=>!ContactRecovery.Overlaps(result,30,40,p.Frame)),"Placement assumed every local body was the target's size");
            var absent=target.Copy();absent.Active=false;Check(!TeleportPlacement.Find(absent,18,26,peers,1,r=>false,out result),"Teleport used an unavailable target");
        }
        private static void Inbox()
        {
            var inbox=new PlayerActionInbox();var target=Frame(130);var packet=Request();packet.Kind=PlayerActionKind.KickEvent;
            inbox.Add(packet,1);packet.Direction=-1;inbox.Add(packet,1);
            Check(inbox.Drain(2,8,2,1,target,1.1,true)==new Vector2(0,-6),"Same-frame kicks weren't additive or incoming storage stayed mutable");
            Check(inbox.Drain(2,8,2,1,target,1.1,true)==Vector2.Zero,"Confirmed impulse applied twice");
            foreach(Action<PlayerActionPacket> change in new Action<PlayerActionPacket>[] {p=>p.Target++,p=>p.Session++,p=>p.Revision++,p=>p.Policy++,p=>p.Map++,p=>p.TargetEpoch++,p=>p.TargetTransition++}) {
                packet=Request();change(packet);inbox.Add(packet,1);
                Check(inbox.Drain(2,8,2,1,target,1.1,true)==Vector2.Zero,"Impulse crossed an identity, policy or position boundary");
            }
            inbox.Add(Request(),1);Check(inbox.Drain(2,8,2,1,target,1.1,false)==Vector2.Zero,"Disabling kicks left queued momentum");
            inbox.Add(Request(),1);target.Presence=PlayerPresence.Paused;Check(inbox.Drain(2,8,2,1,target,1.1,true)==Vector2.Zero,"Queued kick moved a paused king");target.Presence=PlayerPresence.Playing;
            inbox.Add(Request(),1);Check(inbox.Drain(2,8,2,1,target,1.46,true)==Vector2.Zero,"Long frame stall replayed old momentum");
            for(int i=0;i<1000;i++) inbox.Add(Request(),1);
            Check(inbox.Drain(2,8,2,1,target,1.1,true)==PlayerActionRules.Impulse(1)*32,"Impulse staging exceeded its bound");
            inbox.Add(Request(),1);inbox.Clear();Check(inbox.Drain(2,8,2,1,target,1.1,true)==Vector2.Zero,"Attempt cleanup kept an impulse");
        }
        private static void Bindings()
        {
            var keyboard=(IPad)Activator.CreateInstance(typeof(PadInstance).Assembly.GetType("JumpKing.Controller.KeyboardPad",true),true);
            var layered=(IPad)Activator.CreateInstance(typeof(JKRuntime.UI.UIApi).Assembly.GetType("JKRuntime.UI.ChordPad",true),
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{keyboard},null);
            Check(PlayerActionBindings.Default(keyboard)[0].SequenceEqual(new[]{75}) && PlayerActionBindings.Default(layered)[0].SequenceEqual(new[]{75}),"Runtime's layered keyboard lost the default Kick binding");
            Check(PlayerActionBindings.Default(null).Length==0,"Unknown device inherited a keyboard binding");
            var supplied=new[]{new[]{75,75,16,17},null,new[]{32}};var normalized=PlayerActionBindings.Normalize(supplied);
            Check(normalized.Length==2 && normalized[0].SequenceEqual(new[]{75,16}) && normalized[1].Length==0,"Kick binding lost chord/empty slot limits");
            supplied[0][0]=0;Check(normalized[0][0]==75,"Saved chord shared mutable caller storage");
            Check(PlayerActionBindings.Normalize(new[]{new[]{-1,int.MinValue,75}})[0].SequenceEqual(new[]{75}),"Synthetic input codes reached Runtime");
            Check(PlayerActionBindings.Normalize(new[]{new[]{JKRuntime.Input.MouseButtons.Left,75},new[]{(int)Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickUp}})[0].SequenceEqual(new[]{JKRuntime.Input.MouseButtons.Left,75}),"Mouse chord was lost during normalization");
            Check(PlayerActionBindings.Normalize(new[]{new[]{(int)Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickUp}})[0].Length==1,"Controller axis button was lost during normalization");
            Check(PlayerActionBindings.Normalize(null).Length==0,"Null bindings didn't clear the action");
        }
        private static void PolicyDelivery()
        {
            var oldLink=Client.Link;int oldRole=Client.Role;var oldManager=MultiplayerManager.instance;
            string[] names={"lobby","owner","rulesEpoch","revision"};var oldValues=names.Select(n=>AccessTools.Field(typeof(AdvancedSession),n).GetValue(null)).ToArray();
            var peers=(Dictionary<ulong,InteractionPeer>)AccessTools.Field(typeof(AdvancedSession),"peers").GetValue(null);var oldPeers=peers.ToArray();
            try {
                using(var link=new LocalLink(true,Guid.NewGuid().ToString("N"))) {
                    Client.Link=link;Client.Role=2;MultiplayerManager.instance=(MultiplayerManager)FormatterServices.GetUninitializedObject(typeof(MultiplayerManager));
                    AccessTools.Field(typeof(AdvancedSession),"lobby").SetValue(null,1UL);AccessTools.Field(typeof(AdvancedSession),"owner").SetValue(null,1UL);
                    AccessTools.Field(typeof(AdvancedSession),"rulesEpoch").SetValue(null,8UL);AccessTools.Field(typeof(AdvancedSession),"revision").SetValue(null,2UL);
                    PlayerActions.Reset();peers.Clear();var host=Frame(100);host.Epoch=8;host.Map=AdvancedSession.CurrentMap;
                    peers[1]=new InteractionPeer{Id=1,Frame=host,Sample=host,Received=AdvancedSession.Now};
                    var policy=new PlayerActionPacket{Kind=PlayerActionKind.Policy,Actor=1,ActorEpoch=8,Session=8,Revision=2,Map=host.Map,Serial=5,Policy=5,Flags=3,Time=AdvancedSession.Now};
                    AdvancedSession.Receive(3,PlayerActionWire.Encode(policy));Check(!PlayerActions.Kicks && !PlayerActions.Teleports,"Nonmember supplied action permissions");
                    AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(PlayerActions.Kicks && PlayerActions.Teleports,"Host policy didn't reach the real receive router");
                    policy.Serial=policy.Policy=4;policy.Flags=0;AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(PlayerActions.Kicks && PlayerActions.Teleports,"Old policy rolled host settings back");
                    policy.Serial=policy.Policy=6;policy.ActorEpoch=9;AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(PlayerActions.Kicks,"Policy crossed the host attempt boundary");
                    policy.ActorEpoch=8;AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(!PlayerActions.Kicks && !PlayerActions.Teleports,"Host couldn't disable actions during the session");
                    policy.Serial=policy.Policy=7;policy.Flags=3;policy.Map++;AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(!PlayerActions.Kicks,"Policy crossed maps");
                    policy.Map--;policy.Actor=2;AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(!PlayerActions.Kicks,"Host policy contained an inconsistent actor");
                    policy.Actor=1;AdvancedSession.Receive(1,PlayerActionWire.Encode(policy));Check(PlayerActions.Kicks,"Fresh policy didn't restore kicks");
                }
            } finally {
                PlayerActions.Reset();Client.Link=oldLink;Client.Role=oldRole;MultiplayerManager.instance=oldManager;
                for(int i=0;i<names.Length;i++) AccessTools.Field(typeof(AdvancedSession),names[i]).SetValue(null,oldValues[i]);
                peers.Clear();foreach(var pair in oldPeers) peers.Add(pair.Key,pair.Value);
            }
        }
    }
}
