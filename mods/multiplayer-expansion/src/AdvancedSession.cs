using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using JumpKingMultiplayer.Models;
using Microsoft.Xna.Framework;
using Steamworks;
using JKRuntime.Gameplay;
using JKRuntime.UI;

namespace MultiplayerExpansion
{
    internal static class AdvancedSession
    {
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static readonly Dictionary<ulong, InteractionPeer> peers = new Dictionary<ulong, InteractionPeer>();
        private static readonly List<InteractionPeer> contacts = new List<InteractionPeer>();
        private static readonly List<ulong> removed = new List<ulong>();
        private static readonly Dictionary<ulong,InteractionFrame> renderFrames=new Dictionary<ulong,InteractionFrame>();
        private static double renderTime;
        private static readonly PlayerContacts solver = new PlayerContacts();
        private static readonly ContactLedger impacts = new ContactLedger();
        private static readonly ImpactInbox inbox = new ImpactInbox();
        private static ulong jump;
        private static ulong simulationTick, transition;
        private static bool teleportAnnouncement, hasPosition;
        private static Vector2 previousPosition;
        private static readonly FieldInfo knocked = AccessTools.Field(typeof(BodyComp),"_knocked");
        private static readonly MethodInfo levelId = AccessTools.Method(typeof(MultiplayerManager).Assembly.GetType("JumpKingMultiplayer.Extensions.PlayerSpriteStateExtensions", true), "GetLevelId");
        // the legacy getter can read a save file; don't repeat it for every entity
        private static readonly FrameMap frameMap=new FrameMap(delegate {return (ulong?)levelId.Invoke(null,null) ?? 0;});
        private static readonly string settingsPath = Path.Combine(NativeMod.Package, "interactions.txt");
        private static Harmony hooks;
        private static BodyComp body;
        private static ulong bodyMap;
        private static bool wasPaused;
        private static double lastBody = -10, lastSend = -10, lastOwner = -10;
        private static ulong lobby, owner, epoch, sequence, rulesEpoch, revision = 1;
        private static IPacketLink labLink;
        private static InteractionRules saved;
        internal static InteractionRules SolidRules { get; private set; }
        private static BodyComp markedBody;
        private static readonly ContactMarker marker = new ContactMarker();
        private sealed class ContactMarker : JumpKing.API.IBodyCompBehaviour
        { public bool ExecuteBehaviour(JumpKing.BodyCompBehaviours.BehaviourContext context) { return true; } }
        private static InteractionRules rules;
        internal static InteractionRules Rules { get { return rules; } private set { rules = value; ModeOpacity.Sync(); } }
        internal static string Error = "";
        internal static bool InLab { get { return Client.Link != null; } }
        internal static ulong Self { get { return InLab ? (ulong)Client.Role : MultiplayerManager.instance == null ? 0 : MultiplayerManager.instance.UserSteamId.m_SteamID; } }
        internal static bool CanEdit { get { return !Connected || owner == Self; } }
        internal static bool Connected { get { return lobby != 0; } }
        internal static ulong Owner {get{return owner;}}
        internal static ulong Epoch {get{return epoch;}}
        internal static ulong RulesSession {get{return rulesEpoch;}}
        internal static ulong RulesRevision {get{return revision;}}
        internal static double Now {get{return clock.Elapsed.TotalSeconds;}}
        internal static bool IsPaused {get{return Paused;}}
        internal static ulong CurrentMap {get{return Map;}}
        internal static IEnumerable<InteractionPeer> WorldPeers {get{return peers.Values;}}
        internal static IEnumerable<InteractionFrame> SupportFrames
        {
            get {
                if(!Connected || Rules==InteractionRules.Ghosts || body==null || body.Velocity.Y<0) yield break;
                foreach(var peer in contacts) if(peer.Ready(Now,Map,rulesEpoch,revision,Rules)) {
                    var frame=peer.Sample ?? peer.Frame;
                    if(frame.Support!=Self) yield return frame;
                }
            }
        }
        internal static InteractionFrame ActionFrame(ulong id)
        {
            if(id==Self) {var frame=LocalFrame(Now);RemotePresentation.Capture(GameLoop.m_player,frame);return frame;}
            var peer=VisualPeer(id);return peer==null ? null : Presented(peer);
        }
        internal static void BroadcastAction(byte[] packet)
        { foreach(var peer in peers.Values) if(Member(peer.Id)) InteractionTransport.Send(peer.Id,packet,true); }
        internal static bool TerrainBlocked(Rectangle bounds) {return Blocked(bounds);}
        internal static void AddImpulse(Vector2 impulse)
        {
            if(body==null || Paused) return;
            solver.Takeoff(body);
            body.Velocity+=impulse;
            body.Velocity=new Vector2(MathHelper.Clamp(body.Velocity.X,-32,32),MathHelper.Clamp(body.Velocity.Y,-32,32));
            AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(body,false);knocked.SetValue(body,true);
            if(markedBody==null) {body.RegisterBehaviour(marker);markedBody=body;}
            Transition();ContactFeedback.Request(body);
        }
        internal static bool TeleportTo(ulong id)
        {
            var player=GameLoop.m_player;InteractionPeer destination;
            var target=peers.TryGetValue(id,out destination) ? destination.Frame : null;
            if(!Connected || id==Self || !PlayerActions.Teleports || player==null || !player.IsAlive || !player.m_body.Enabled || target==null || !target.Active || target.Map!=Map) {PlayerActions.Status="Player is unavailable on this map.";return false;}
            var actual=player.m_body;var bounds=actual.GetHitbox();
            var candidates=new List<InteractionPeer>();
            foreach(var peer in peers.Values) {var frame=peer.Frame;if(frame!=null && frame.Active && frame.Map==Map) {
                var sample=frame.Copy();sample.Position=JKRuntime.Geometry.MapTopology.NearestImage(target.Position,frame.Position);
                candidates.Add(new InteractionPeer{Id=peer.Id,Frame=sample,Sample=sample});
            }}
            Vector2 placement;
            if(!TeleportPlacement.Find(target,bounds.Width,bounds.Height,candidates,Self,JKRuntime.Geometry.MapTopology.IsBlocked,out placement)) {PlayerActions.Status="No free space near that player.";return false;}
            actual.Position=JKRuntime.Geometry.MapTopology.Normalize(placement,bounds.Width,bounds.Height);actual.Velocity=Vector2.Zero;
            AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(actual,false);knocked.SetValue(actual,false);
            Camera.UpdateCamera(actual.GetHitbox().Center);
            GameplayEvents.NotifyTeleport("anyalink.multiplayer-expansion.world",actual.Position,actual.Velocity,Camera.CurrentScreen);
            Transition();hasPosition=false;teleportAnnouncement=true;PlayerActions.Status="Teleported.";return true;
        }
        internal static void BroadcastWorld(byte[] packet)
        {
            foreach(var peer in peers.Values)if(WorldSession.Supports(peer.Id))InteractionTransport.Send(peer.Id,packet,true);
        }
        internal static string Status { get { return Error != "" ? Error : ModeOpacity.Error != "" ? ModeOpacity.Error : !Connected ? "Choose rules, then connect through Multiplayer." : CanEdit ? "Host rules | extension peers: " + peers.Count : "Host controls rules | extension peers: " + peers.Count; } }
        internal static string Diagnostics
        {
            get
            {
                double delay=0;float correction=0;
                foreach(var peer in peers.Values) {delay=Math.Max(delay,peer.OneWay*2000);correction=Math.Max(correction,peer.Visual.Error);}
                return " | world="+WorldSession.Diagnostics+" "+PlayerActions.Diagnostics+" interactions="+InteractionSettings.Name(Rules)+" peers="+peers.Count+" contacts="+contacts.Count
                    +" support="+solver.Support+" jump="+jump+" revision="+revision+" tick="+simulationTick+" transition="+transition
                    +" paused="+wasPaused+" rttMs="+delay.ToString("F0")+" visualPx="+correction.ToString("F2")
                    +" inbox="+inbox.Count+" expired="+inbox.Dropped+" smooth="+RemotePresentation.Updates+"/"+RemotePresentation.Draws
                    +" gear="+RemotePresentation.LastEquipment+" timing="+TwoClientTiming.Status+(InLab ? " network="+NetworkLab.Status : "");
            }
        }

        internal static void Initialize()
        {
            if (hooks != null) return;
            try { saved = InteractionSettings.Read(settingsPath); SolidRules = InteractionSettings.ReadSolid(settingsPath); }
            catch (IOException) { saved = InteractionRules.Ghosts; SolidRules = InteractionRules.Solid; }
            Rules = saved;
            hooks = new Harmony("anyalink.multiplayer-expansion.interactions");
            ModeOpacity.Install(hooks);
            LegacySafety.Install(hooks);
            WorldSession.Install();
            SupportSurface.Install(hooks);
            PlayerActions.Initialize();PlayerEffects.Install(hooks);
            hooks.Patch(AccessTools.Method(typeof(Game1), "Update"), prefix: new HarmonyMethod(typeof(AdvancedSession), "BeginFrame"), postfix: new HarmonyMethod(typeof(AdvancedSession), "Tick"));
            hooks.Patch(AccessTools.Method(typeof(BodyComp), "UpdateInternal"), prefix: new HarmonyMethod(typeof(AdvancedSession), "BeforeBody"), postfix: new HarmonyMethod(typeof(AdvancedSession), "AfterBody"));
            hooks.Patch(AccessTools.Method(typeof(JumpState),"DoJump"),postfix:new HarmonyMethod(typeof(AdvancedSession),"Jumped"));
            solver.Impact=delegate(InteractionPeer peer,Vector2 normal,Vector2 localAfter,Vector2 remoteAfter) { impacts.Record(Self,peer,normal,localAfter,remoteAfter,clock.Elapsed.TotalSeconds); };
            RemotePresentation.Install(hooks);
            PlayerIndicators.Install(hooks);
            hooks.Patch(AccessTools.Method(typeof(MultiplayerManager),"SendUpdateToOthers"),prefix:new HarmonyMethod(typeof(AdvancedSession),"LegacyNeeded"));
        }
        private static bool LegacyNeeded()
        {
            var manager=MultiplayerManager.instance;
            if(!Connected || manager==null || manager.LobbyPlayers.Count==0) return true;
            foreach(var id in manager.LobbyPlayers) {var peer=VisualPeer(id.m_SteamID);if(peer==null || !peer.Frame.Physics) return true;}
            return false;
        }
        internal static void SetRules(InteractionRules value)
        {
            if (!CanEdit || !InteractionSettings.Valid(value)) return;
            value = InteractionSettings.Normalize(value);
            try { InteractionSettings.Write(settingsPath, value); Error = ""; }
            catch (IOException e) { Error = "Cannot save settings: " + e.Message; return; }
            catch (UnauthorizedAccessException) { Error = "Cannot write the mod settings folder."; return; }
            saved = Rules = value; revision++; ResetContacts(); lastSend = -10;
            if (value != InteractionRules.Ghosts) SolidRules = value;
        }
        private static ulong NewEpoch() { return BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) | 1; }
        internal static void BeginFrame() {frameMap.Invalidate();}
        internal static void AttachBody(JKRuntime.ModuleContext context)
        {
            var player=GameLoop.m_player;
            if(player!=null) context.Track(new ContactPipeline(player.m_body,solver,()=>contacts,()=>Rules,()=>Self,Blocked));
        }
        private static ulong Map { get { return frameMap.Value; } }
        private static bool Paused { get { return NativePause.IsPaused || UIApi.IsOpen; } }
        private static void ResetContacts()
        {solver.Reset();impacts.Reset();inbox.Clear();if(body!=null) ContactFeedback.Cancel(body);}
        private static void Transition()
        {transition++;ResetContacts();lastSend=-10;}
        private static void ObservePause()
        {
            bool paused=Paused;
            if(paused==wasPaused) return;
            wasPaused=paused;Transition();
        }
        private static InteractionFrame LocalFrame(double now,bool includeImpacts=false)
        {
            var player=GameLoop.m_player;var current=player==null ? null : player.m_body;
            ulong map=Map;bool paused=Paused;
            Rectangle bounds=current==null ? new Rectangle(0,0,18,26) : current.GetHitbox();
            var frame=new InteractionFrame { Epoch=epoch,RulesEpoch=rulesEpoch,Revision=revision,Rules=Rules,Map=map,Modern=true,Physics=true,Time=now,
                Active=SessionPresence.Active(player!=null && player.IsAlive,current!=null && ReferenceEquals(body,current),map==bodyMap,paused,now-lastBody),
                Position=current==null ? Vector2.Zero : current.Position,Velocity=current==null ? Vector2.Zero : current.Velocity,Width=bounds.Width,Height=bounds.Height,
                Grounded=current!=null && current.IsOnGround && current.Velocity.Y>=0,Support=solver.Support,SupportOffset=solver.SupportOffset,Jump=jump,SimulationTick=simulationTick,Transition=transition,Impacts=includeImpacts ? impacts.Pending(now) : ContactImpact.None };
            frame.Presence=frame.Active ? PlayerPresence.Playing : player!=null && player.IsAlive ? PlayerPresence.Spawning : PlayerPresence.Unavailable;
            if(teleportAnnouncement) {frame.Presence=PlayerPresence.Teleporting;frame.Active=false;}
            InteractionPeer support;if(peers.TryGetValue(frame.Support,out support)) frame.SupportEpoch=support.Frame.Epoch;else frame.Support=0;
            SessionPresence.Freeze(frame,paused);
            return frame;
        }
        private static bool Member(ulong id)
        {
            var manager = MultiplayerManager.instance;
            if (manager == null || id == Self) return false;
            if (InLab) return id == (ulong)(3 - Client.Role);
            foreach (var member in manager.LobbyPlayers) if (member.m_SteamID == id) return true;
            return false;
        }
        internal static InteractionPeer VisualPeer(ulong id)
        {
            InteractionPeer peer;
            return Connected && peers.TryGetValue(id, out peer) && peer.Frame.Modern && clock.Elapsed.TotalSeconds - peer.Received < .5 ? peer : null;
        }
        internal static void Receive(ulong id, byte[] bytes)
        {
            if (!Connected || !Member(id)) return;
            if(WorldWire.Recognizes(bytes)){WorldSession.Receive(id,bytes);return;}
            if(WorldInputs.Recognizes(bytes)){WorldInputs.Receive(id,bytes);return;}
            if(PlayerActionWire.Recognizes(bytes)){PlayerActions.Receive(id,bytes);return;}
            ObservePause();
            InteractionFrame frame;
            if (!InteractionWire.Decode(bytes, out frame)) return;
            if(frame.Support==id || frame.Support!=0 && frame.Support!=Self && !Member(frame.Support) || id==owner && frame.RulesEpoch!=frame.Epoch) return;
            InteractionPeer peer;
            if (!peers.TryGetValue(id, out peer)) { peer = new InteractionPeer { Id = id }; peers.Add(id, peer); }
            ulong oldEpoch=peer.Frame==null ? 0 : peer.Frame.Epoch;
            if (!peer.Accept(frame, clock.Elapsed.TotalSeconds)) return;
            if(oldEpoch!=frame.Epoch) { impacts.Forget(id);inbox.Forget(id);solver.Forget(id); }
            // the new stream must create its ghost even when the legacy sender is idle
            if (frame.Modern && Game1.instance != null && GameLoop.m_player != null)
                MultiplayerManager.instance.UpdatePlayerState(new CSteamID(id), new TrackData {
                    posX=frame.Position.X, posY=frame.Position.Y, screenIndex1=1-(int)Math.Floor(frame.Position.Y/360),
                    levelId=frame.Map==0 ? (ulong?)null : frame.Map, sprite=(PlayerSpriteState)frame.Pose, flip=(PlayerSpriteEffect)frame.Flip
                });
            if (id == owner)
            {
                if (frame.RulesEpoch != frame.Epoch) return;
                if (Rules != frame.Rules || revision != frame.Revision || rulesEpoch != frame.RulesEpoch) ResetContacts();
                Rules = frame.Rules; revision = frame.Revision; rulesEpoch = frame.RulesEpoch; lastOwner = clock.Elapsed.TotalSeconds;
            }
            if(!Paused) inbox.Add(id,frame,clock.Elapsed.TotalSeconds,peer.OneWay);
        }
        private static void ApplyImpacts(double now,ulong map)
        {
            if(!Connected || Paused || Rules==InteractionRules.Ghosts) {inbox.Clear();return;}
            inbox.Drain(now,delegate(PendingImpact item,double age) {
                InteractionPeer peer;
                if(!peers.TryGetValue(item.Source,out peer) || !Member(item.Source) || !peer.Ready(now,map,rulesEpoch,revision,Rules)
                    || !item.Matches(peer.Frame,transition,map,rulesEpoch,revision)) return;
                Vector2 velocity=body.Velocity;bool firstImpact;
                if(!impacts.Confirm(item.Source,Self,epoch,jump,item.Hit,age,now,body.IsOnGround || solver.Support!=0,ref velocity,out firstImpact)) return;
                body.Velocity=velocity;knocked.SetValue(body,true);solver.Confirmed(item.Source);
                if(firstImpact) ContactFeedback.Request(body);
            });
        }
        private static void Tick()
        {
            var manager = MultiplayerManager.instance;
            ulong nextLobby = manager == null || !manager.LobbyId.HasValue ? 0 : manager.LobbyId.Value.m_SteamID;
            ulong nextOwner = InLab ? 1UL : manager == null || !manager.LobbyOwner.HasValue ? 0 : manager.LobbyOwner.Value.m_SteamID;
            if (nextLobby != lobby || nextOwner != owner || !ReferenceEquals(labLink, Client.Link))
            {
                lobby = nextLobby; owner = nextOwner; labLink = Client.Link;
                peers.Clear(); contacts.Clear(); ResetContacts();impacts.ResetSession(); epoch = NewEpoch(); sequence = jump = simulationTick = transition = 0; revision = 1;hasPosition=false;teleportAnnouncement=false;
                Rules = CanEdit ? saved : InteractionRules.Ghosts; rulesEpoch = CanEdit ? epoch : 0; lastOwner = lastSend = -10;
                WorldSession.Reset();
                PlayerActions.Reset();
            }
            if (!Connected) return;
            // the native manager only polls while the local player updates
            if(Paused) LegacySafety.Pump(manager);
            ObservePause();
            InteractionTransport.Pump(Receive);
            WorldSession.Tick();
            PlayerActions.Tick();
            double now = clock.Elapsed.TotalSeconds;
            removed.Clear();
            foreach (var peer in peers) if (!Member(peer.Key) || now - peer.Value.Received > 2) removed.Add(peer.Key);
            foreach (var id in removed) { peers.Remove(id);impacts.Forget(id);inbox.Forget(id);solver.Forget(id); }
            foreach (var peer in peers.Values) if (peer.Sample == null || now - lastBody > .05) peer.Prepare(now, InLab);
            if (now + .001 < lastSend) return;
            lastSend = Math.Max(lastSend + 1.0 / 60, now);
            var frame=LocalFrame(now,true);frame.Sequence=++sequence;
            RemotePresentation.Capture(GameLoop.m_player, frame);
            if (InLab) InteractionTransport.Send((ulong)(3-Client.Role),Packet(frame,(ulong)(3-Client.Role),now));
            else foreach (var peer in manager.LobbyPlayers) InteractionTransport.Send(peer.m_SteamID,Packet(frame,peer.m_SteamID,now));
            teleportAnnouncement=false;
        }
        private static byte[] Packet(InteractionFrame frame,ulong id,double now)
        {
            InteractionPeer peer;frame.Echo=frame.EchoAge=0;
            if(peers.TryGetValue(id,out peer)) {frame.Echo=peer.Frame.Time;frame.EchoAge=Math.Min(10,now-peer.Received);}
            return InteractionWire.Encode(frame);
        }
        private static void Jumped()
        {
            var player=GameLoop.m_player;if(player==null || !ReferenceEquals(player.m_body,body) || body.Velocity.Y>=0) return;
            solver.Takeoff(body);jump++;lastSend=-10;
        }
        internal static InteractionFrame Presented(InteractionPeer peer)
        {
            var result=peer.Sample ?? peer.Frame;
            if(!result.Physics || !result.Active || result.Support==0 || (Rules & InteractionRules.Platforms)==0) return result;
            return SupportGraph.Resolve(peer.Id,SceneFrame) ?? result;
        }
        internal static InteractionFrame VisualFrame(InteractionPeer peer)
        {
            if(!peer.Frame.Physics) return Presented(peer);
            return SupportGraph.Resolve(peer.Id,VisualSceneFrame) ?? Presented(peer);
        }
        internal static void BeginPresentation()
        {renderTime=clock.Elapsed.TotalSeconds;renderFrames.Clear();}
        private static InteractionFrame VisualSceneFrame(ulong id)
        {
            if(id==Self) return SceneFrame(id);
            var peer=VisualPeer(id);if(peer==null) return null;
            InteractionFrame cached;if(renderFrames.TryGetValue(id,out cached))return cached;
            var playback=peer.Playback.Sample(renderTime,InLab) ?? peer.Frame;
            var predicted=peer.Sample ?? peer.Motion.Predict(renderTime,peer.OneWay) ?? peer.Frame;
            var aligned=JKRuntime.Geometry.MapTopology.NearestImage(predicted.Position,playback.Position,1);
            if(aligned!=playback.Position) {playback=playback.Copy();playback.Position=aligned;}
            float weight=0;ulong ancestor=solver.Support;
            for(int i=0;i<8 && ancestor!=0;i++)
            {if(ancestor==id) {weight=1;break;}InteractionPeer parent;if(!peers.TryGetValue(ancestor,out parent)) break;ancestor=parent.Frame.Support;}
            if(Rules!=InteractionRules.Ghosts && body!=null && predicted.Map==bodyMap){
                weight=Math.Max(weight,RemotePlayback.ContactWeight(body.GetHitbox(),predicted));
                ancestor=predicted.Support;
                for(int i=0;i<8 && ancestor!=0;i++){
                    if(ancestor==Self){weight=1;break;}
                    InteractionPeer parent;if(!peers.TryGetValue(ancestor,out parent))break;ancestor=parent.Frame.Support;
                }
            }
            var current=peer.Visual.Apply(predicted,renderTime,weight>=1);
            var result=RemotePlayback.Blend(playback,current,weight);
            if(weight>=.95f && Rules!=InteractionRules.Ghosts && body!=null && result.Map==bodyMap)
                result=solver.Project(id,result,body.GetHitbox(),Self);
            renderFrames[id]=result;return result;
        }
        private static InteractionFrame SceneFrame(ulong id)
        {
            if(id==Self)
            {
                return body==null ? null : LocalFrame(clock.Elapsed.TotalSeconds);
            }
            var peer=VisualPeer(id);return peer==null ? null : peer.Sample ?? peer.Frame;
        }
        private static void BeforeBody(BodyComp __instance, out Vector2 __state)
        {
            __state = __instance.Position;
            if (GameLoop.m_player == null || !ReferenceEquals(GameLoop.m_player.m_body, __instance)) return;
            solver.BeginBodyStep();
            if(Connected) ObservePause();
            if (!ReferenceEquals(body, __instance)) { body = __instance;ResetContacts();jump=simulationTick=transition=0;hasPosition=false;epoch = NewEpoch(); if (CanEdit) rulesEpoch = epoch; }
            Vector2 seam;
            if(hasPosition && Vector2.DistanceSquared(previousPosition,__state)>4096 && !JKRuntime.Geometry.MapTopology.TryCrossing(previousPosition,__state,out seam)) {Transition();teleportAnnouncement=true;}
            simulationTick++;
            bool enabled = Connected && Rules != InteractionRules.Ghosts;
            if (markedBody != null && (!ReferenceEquals(markedBody, body) || !enabled)) { markedBody.RemoveBehaviour(marker); markedBody = null; }
            if (enabled && markedBody == null) { body.RegisterBehaviour(marker); markedBody = body; }
            lastBody = clock.Elapsed.TotalSeconds;
            bodyMap=Map;
            foreach (var peer in peers.Values) peer.Prepare(lastBody, InLab);
            ApplyImpacts(lastBody,bodyMap);
            PlayerActions.Apply();
            PrepareContacts(lastBody);
        }
        private static void AfterBody(BodyComp __instance, Vector2 __state)
        {
            if (!ReferenceEquals(body, __instance)) return;
            previousPosition=body.Position;hasPosition=true;
            // a warp mustn't accept a delayed impulse from the old position
            Vector2 seam;
            if(Vector2.DistanceSquared(__state+solver.SeamShift,body.Position)>4096 && !JKRuntime.Geometry.MapTopology.TryCrossing(__state,body.Position,out seam)) {Transition();teleportAnnouncement=true;}
            previousPosition=body.Position;
            ContactFeedback.Flush(body);
        }
        private static void PrepareContacts(double now)
        {
            contacts.Clear();
            if (!Connected || Rules == InteractionRules.Ghosts || !CanEdit && now - lastOwner >= .5) { solver.Reset();if(body!=null) ContactFeedback.Cancel(body);return; }
            ulong map = Map;
            foreach (var peer in peers.Values) if (peer.Frame.Physics && peer.Ready(now, map, rulesEpoch, revision, Rules) && Member(peer.Id)) contacts.Add(peer);
            contacts.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach(var peer in contacts) {
                var sample=Presented(peer).Copy();
                if(body!=null) {
                    var observer=body.Position+(peer.Id==solver.Support ? -solver.SupportOffset : new Vector2(9,13));
                    sample.Position=JKRuntime.Geometry.MapTopology.NearestImage(observer,sample.Position,1);
                }
                peer.Sample=sample;
            }
        }
        private static bool Blocked(Rectangle bounds)
        {
            if(body==null) return JKRuntime.Geometry.MapTopology.IsBlocked(bounds);
            var route=body.GetHitbox().Center.ToVector2();
            if(solver.Support!=0) foreach(var peer in contacts) if(peer.Id==solver.Support) {
                var carrier=peer.Sample ?? peer.Frame;route=carrier.Position+new Vector2(carrier.Width/2f,carrier.Height/2f);break;
            }
            return JKRuntime.Geometry.MapTopology.IsBlocked(bounds,route);
        }
    }
}
