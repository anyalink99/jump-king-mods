using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing.GameManager;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class PlayerActions
    {
        private sealed class Pending {internal PlayerActionPacket Packet;internal double Until,Next;}
        private static readonly KickAuthority authority=new KickAuthority();
        private static readonly ActionReplayWindow replay=new ActionReplayWindow();
        private static readonly List<Pending> outgoing=new List<Pending>();
        private static readonly PlayerActionInbox impulses=new PlayerActionInbox();
        private static readonly Dictionary<ulong,ulong> compatible=new Dictionary<ulong,ulong>();
        private static bool savedKicks=true,savedTeleports=true,known;
        private static bool kicks,teleports;
        private static ulong session,revision,serial,requestSerial,policySerial=1,receivedPolicy;
        private static double nextPolicy,nextHello,lastKick=-10;
        private static int confirmed,applied;
        internal static string Status="";
        internal static string Diagnostics {get{return "actions="+compatible.Count+"/"+(known || AdvancedSession.CanEdit ? 1 : 0)+" kicks="+confirmed+"/"+applied;}}
        private static string SettingsPath {get{return Path.Combine(NativeMod.Package,"actions.txt");}}
        internal static bool Kicks {get{return AdvancedSession.CanEdit ? savedKicks : known && kicks;}}
        internal static bool Teleports {get{return AdvancedSession.CanEdit ? savedTeleports : known && teleports;}}
        internal static bool CanKick {get{var p=GameLoop.m_player;return AdvancedSession.Connected && Kicks && !Client.OverlayActive && (!AdvancedSession.InLab || Client.InputAvailable) && PlayerActionBindings.Available && p!=null && p.IsAlive && p.m_body.Enabled && !AdvancedSession.IsPaused && AdvancedSession.Now-lastKick>=PlayerActionRules.Cooldown;}}
        internal static void Initialize()
        {
            try {
                if(File.Exists(SettingsPath) && new FileInfo(SettingsPath).Length<=32) {
                    var text=File.ReadAllText(SettingsPath).Trim().Split(' ');
                    if(text.Length==2 && (text[0]=="0" || text[0]=="1") && (text[1]=="0" || text[1]=="1")) {savedKicks=text[0]=="1";savedTeleports=text[1]=="1";}
                }
            } catch(IOException e) {Status=e.Message;} catch(UnauthorizedAccessException e) {Status=e.Message;}
            PlayerActionBindings.Register();
        }
        internal static void Set(bool allowKicks,bool allowTeleports)
        {
            if(!AdvancedSession.CanEdit) return;
            try {
                string temp=SettingsPath+".tmp";File.WriteAllText(temp,(allowKicks ? "1" : "0")+" "+(allowTeleports ? "1" : "0"));
                if(File.Exists(SettingsPath)) File.Replace(temp,SettingsPath,SettingsPath+".bak");else File.Move(temp,SettingsPath);
            }
            catch(IOException e) {Status=e.Message;return;} catch(UnauthorizedAccessException e) {Status=e.Message;return;}
            savedKicks=allowKicks;savedTeleports=allowTeleports;policySerial++;nextPolicy=0;Status="";
            outgoing.Clear();impulses.Clear();
        }
        internal static void Reset()
        {
            authority.Reset();replay.Reset();outgoing.Clear();impulses.Clear();compatible.Clear();known=false;session=revision=serial=requestSerial=receivedPolicy=0;nextPolicy=nextHello=0;lastKick=-10;confirmed=applied=0;PlayerEffects.Reset();
        }
        private static PlayerActionPacket Packet(PlayerActionKind kind)
        {
            var own=AdvancedSession.ActionFrame(AdvancedSession.Self);
            return new PlayerActionPacket{Kind=kind,Session=AdvancedSession.RulesSession,Revision=AdvancedSession.RulesRevision,Map=own.Map,Actor=AdvancedSession.Self,
                ActorEpoch=own.Epoch,ActorTransition=own.Transition,Time=AdvancedSession.Now,Policy=kind==PlayerActionKind.Hello ? 0 : AdvancedSession.CanEdit ? policySerial : receivedPolicy};
        }
        internal static void Kick()
        {
            if(!CanKick) return;
            var source=AdvancedSession.ActionFrame(AdvancedSession.Self);int direction=source.Flip==1 ? -1 : 1;
            var targets=AdvancedSession.WorldPeers.Where(p=>Supports(p.Id) && PlayerActionRules.Reachable(source,AdvancedSession.ActionFrame(p.Id),direction,AdvancedSession.TerrainBlocked))
                .OrderBy(p=>Vector2.DistanceSquared(source.Position,AdvancedSession.ActionFrame(p.Id).Position)).ThenBy(p=>p.Id).ToArray();
            lastKick=AdvancedSession.Now;PlayerEffects.Swing(PlayerActionRules.Center(source),direction,lastKick);
            if(targets.Length==0) return;
            var target=AdvancedSession.ActionFrame(targets[0].Id);var request=Packet(PlayerActionKind.KickRequest);
            request.Serial=++requestSerial;request.Target=targets[0].Id;request.TargetEpoch=target.Epoch;request.TargetTransition=target.Transition;
            request.Direction=direction;request.Origin=PlayerActionRules.Center(source);
            if(AdvancedSession.Owner==AdvancedSession.Self) HandleRequest(request,source,0);
            else Queue(request,.35);
        }
        private static void Queue(PlayerActionPacket packet,double lifetime)
        {
            if(outgoing.Count>=32) outgoing.RemoveAt(0);
            outgoing.Add(new Pending{Packet=packet,Until=AdvancedSession.Now+lifetime});
        }
        internal static void Tick()
        {
            if(!AdvancedSession.Connected) return;
            double now=AdvancedSession.Now;
            if(session!=AdvancedSession.RulesSession || revision!=AdvancedSession.RulesRevision) {
                Reset();session=AdvancedSession.RulesSession;revision=AdvancedSession.RulesRevision;
            }
            if(session==0) return;
            if(now>=nextHello) {
                nextHello=now+.5;var hello=Packet(PlayerActionKind.Hello);hello.Serial=1;
                AdvancedSession.BroadcastAction(PlayerActionWire.Encode(hello));
            }
            if(AdvancedSession.Owner==AdvancedSession.Self && now>=nextPolicy) {
                nextPolicy=now+.5;var policy=Packet(PlayerActionKind.Policy);policy.Serial=policySerial;policy.Flags=(byte)((savedKicks ? 1 : 0)|(savedTeleports ? 2 : 0));
                AdvancedSession.BroadcastAction(PlayerActionWire.Encode(policy));
            }
            for(int i=outgoing.Count-1;i>=0;i--) {
                var item=outgoing[i];if(now>item.Until) {outgoing.RemoveAt(i);continue;}
                if(now<item.Next) continue;item.Next=now+.05;
                byte[] bytes=PlayerActionWire.Encode(item.Packet);
                if(item.Packet.Kind==PlayerActionKind.KickRequest) InteractionTransport.Send(AdvancedSession.Owner,bytes,true);
                else AdvancedSession.BroadcastAction(bytes);
            }
        }
        internal static void Receive(ulong sender,byte[] bytes)
        {
            PlayerActionPacket packet;
            if(!PlayerActionWire.Decode(bytes,out packet) || packet.Session!=AdvancedSession.RulesSession || packet.Revision!=AdvancedSession.RulesRevision || packet.Map!=AdvancedSession.CurrentMap) return;
            if(packet.Kind==PlayerActionKind.Hello) {
                var participant=AdvancedSession.VisualPeer(sender);
                if(packet.Actor==sender && participant!=null && participant.Frame.Epoch==packet.ActorEpoch) compatible[sender]=packet.ActorEpoch;
                return;
            }
            if(packet.Kind==PlayerActionKind.KickRequest) {
                if(AdvancedSession.Owner!=AdvancedSession.Self || packet.Actor!=sender || !Supports(sender)) return;
                var source=AdvancedSession.ActionFrame(sender);var peer=AdvancedSession.VisualPeer(sender);
                if(source==null || peer==null) return;
                HandleRequest(packet,source,Math.Max(0,peer.Frame.Time-packet.Time)+peer.OneWay+AdvancedSession.Now-peer.Received);return;
            }
            if(sender!=AdvancedSession.Owner) return;
            var host=AdvancedSession.VisualPeer(sender);
            if(packet.Kind==PlayerActionKind.Policy) {
                if(host==null || packet.Actor!=sender || packet.ActorEpoch!=host.Frame.Epoch || packet.Serial<receivedPolicy) return;
                receivedPolicy=packet.Serial;known=true;kicks=(packet.Flags&1)!=0;teleports=(packet.Flags&2)!=0;return;
            }
            if(Kicks && host!=null && Math.Max(0,host.Frame.Time-packet.Time)+host.OneWay+AdvancedSession.Now-host.Received<=.45) AcceptEvent(packet);
        }
        private static bool Supports(ulong id)
        { ulong value;var frame=AdvancedSession.ActionFrame(id);return id==AdvancedSession.Self || frame!=null && compatible.TryGetValue(id,out value) && value==frame.Epoch; }
        private static void HandleRequest(PlayerActionPacket request,InteractionFrame source,double age)
        {
            if(!Kicks || request.Policy!=policySerial || !Supports(request.Target)) return;
            var target=AdvancedSession.ActionFrame(request.Target);
            if(!authority.Accept(request,source,target,age,AdvancedSession.Now,AdvancedSession.TerrainBlocked)) return;
            var hit=request.Copy();hit.Kind=PlayerActionKind.KickEvent;hit.Serial=++serial;hit.Time=AdvancedSession.Now;
            hit.Origin=PlayerActionRules.Center(source);hit.Hit=PlayerActionRules.Center(target);
            AcceptEvent(hit);Queue(hit,.4);
        }
        private static void AcceptEvent(PlayerActionPacket packet)
        {
            if(packet.Policy!=(AdvancedSession.CanEdit ? policySerial : receivedPolicy) || !replay.Accept(packet.Serial)) return;
            var target=AdvancedSession.ActionFrame(packet.Target);
            if(target==null || target.Epoch!=packet.TargetEpoch || target.Transition!=packet.TargetTransition || target.Presence!=PlayerPresence.Playing) return;
            confirmed++;
            PlayerEffects.Hit(packet.Origin,packet.Hit,packet.Direction,AdvancedSession.Now);
            if(packet.Target==AdvancedSession.Self) impulses.Add(packet,AdvancedSession.Now);
        }
        internal static void Apply()
        {
            if(!impulses.HasPending) return;
            Vector2 impulse=impulses.Drain(AdvancedSession.Self,AdvancedSession.RulesSession,AdvancedSession.RulesRevision,
                AdvancedSession.CanEdit ? policySerial : receivedPolicy,AdvancedSession.ActionFrame(AdvancedSession.Self),AdvancedSession.Now,Kicks);
            if(impulse!=Vector2.Zero) {AdvancedSession.AddImpulse(impulse);applied++;}
        }
    }
}
