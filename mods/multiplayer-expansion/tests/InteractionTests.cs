using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using JKRuntime.UI;

namespace MultiplayerExpansion
{
    internal static class InteractionTests
    {
        private static int checks;
        private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        private static InteractionPeer Peer(float x = 100, float y = 100)
        { return new InteractionPeer { Id = 2, Received = 10, Frame = new InteractionFrame { Epoch = 1, Sequence = 1, RulesEpoch = 1, Revision = 1, Rules = InteractionRules.Solid, Active = true, Width = 18, Height = 26, Position = new Vector2(x, y) } }; }
        private static ContactBody Body(float x, float y, float nextX, float nextY, float vx, float vy)
        { return new ContactBody { Before = new Vector2(x,y), Position = new Vector2(nextX,nextY), Velocity = new Vector2(vx,vy), Width = 18, Height = 26 }; }
        internal static void Run(string root)
        {
            var peer = Peer(); var peers = new[] { peer }; var solver = new PlayerContacts();
            Func<Rectangle,bool> empty = r => false;
            var body = Body(100,65,100,82,0,17);
            solver.Resolve(body,peers,InteractionRules.Ghosts,1,empty);
            Check(body.Position.Y == 82 && !body.Grounded,"Ghost mode changed physics");
            body = Body(100,65,100,82,0,17);
            solver.Resolve(body,peers,InteractionRules.Platforms,1,empty);
            Check(body.Position.Y == 74 && body.Grounded && body.Velocity.Y == 0,"Falling player didn't land on head");
            body = Body(100,74,100,74,0,.3f);
            solver.Resolve(body,peers,InteractionRules.Platforms,1,empty);
            Check(body.Grounded && body.Position.Y == 74,"Standing player lost support");
            peer.Frame.Position.X += 2;
            body = Body(100,74,100,74,0,.3f);
            solver.Resolve(body,peers,InteractionRules.Platforms,1,empty);
            Check(body.Position.X == 102 && body.Grounded,"Moving support did not carry rider");
            body = Body(102,74,102,64,0,-10);
            solver.Resolve(body,peers,InteractionRules.Platforms,1,empty);
            Check(body.Position.Y == 64 && !body.Grounded && solver.Support == 0,"Jump remained attached to support");
            body = Body(102,128,102,118,0,-10);
            solver.Resolve(body,peers,InteractionRules.Platforms,1,empty);
            Check(body.Position.Y == 118 && !body.Grounded,"One-way platforms blocked from below");
            peer.Frame.Position = new Vector2(100,100);
            body = Body(100,128,100,118,0,-10);
            solver.Resolve(body,peers,InteractionRules.Solid,1,empty);
            Check(body.Position.Y == 126 && body.Velocity.Y == 0,"Solid underside didn't block rising player");
            body = Body(80,100,85,100,5,.3f);
            solver.Resolve(body,peers,InteractionRules.Solid,1,empty);
            Check(body.Position.X == 82 && body.Velocity.X == 0,"Side collision failed");
            body = Body(75,100,130,100,55,.3f);
            solver.Resolve(body,peers,InteractionRules.Solid,1,empty);
            Check(body.Position.X == 82,"Fast horizontal movement tunneled through player");
            peer.Frame.Velocity.X = 2;
            body = Body(118,100,118,100,0,.3f);
            solver.Resolve(body,peers,InteractionRules.Solid,1,empty);
            Check(body.Position.X == 120,"Walking contact didn't push stationary player");
            body = Body(118,100,118,100,0,.3f);
            solver.Resolve(body,peers,InteractionRules.Sides,1,empty);
            Check(body.Position.X == 118,"Push-off setting moved player");
            body = Body(118,100,118,100,0,.3f);
            solver.Resolve(body,peers,InteractionRules.Solid,1,r => r.Right > 137);
            Check(body.Position.X == 119,"Push penetrated terrain or ignored available space");
            body = Body(100,65,100,82,0,17);
            solver.Resolve(body,peers,InteractionRules.Platforms,1,r => r.Top < 80);
            Check(!body.Grounded && body.Position.Y >= 80,"Head landing moved into ceiling");
            solver.Reset(); body = Body(0,0,100,110,0,1);
            solver.Resolve(body,peers,InteractionRules.Solid,1,empty);
            Check(!ContactRecovery.Overlaps(body.Position,body.Width,body.Height,peer.Frame) && !body.Bump && !body.Knocked,"Teleport overlap wasn't repaired quietly");
            body = Body(100,100,100,100,0,.3f);
            solver.Resolve(body,peers,InteractionRules.Solid,1,empty);
            Check(body.Position.X == 82,"Equal-position spawn did not separate deterministically");

            Check(peer.Ready(10.1,0,1,1,InteractionRules.Solid),"Fresh compatible peer refused");
            Check(!peer.Ready(10.6,0,1,1,InteractionRules.Solid),"Stale peer remained solid");
            Check(!peer.Ready(10.1,4,1,1,InteractionRules.Solid),"Different map remained solid");
            Check(!peer.Ready(10.1,0,1,2,InteractionRules.Solid),"Unacknowledged rules remained solid");
            var packet = InteractionWire.Encode(peer.Frame); InteractionFrame decoded = null;
            Check(packet.Length == InteractionWire.Size && InteractionWire.Decode(packet,out decoded) && decoded.Position == peer.Frame.Position,"Wire round trip failed");
            Check(!peer.Accept(decoded,11),"Out-of-order duplicate accepted");
            packet[4] = 99; Check(!InteractionWire.Decode(packet,out decoded),"Unknown protocol accepted");
            packet = InteractionWire.Encode(peer.Frame); packet[5] = 4; Check(!InteractionWire.Decode(packet,out decoded),"Push without collisions accepted");
            peer.Frame.Position.X = float.NaN; Check(!InteractionWire.Decode(InteractionWire.Encode(peer.Frame),out decoded),"Nonfinite body accepted");
            Check(!InteractionWire.Decode(new byte[100],out decoded),"Malformed packet accepted");
            string settings = Path.Combine(root,"interaction-settings.txt");
            InteractionSettings.Write(settings,InteractionRules.Platforms); InteractionSettings.Write(settings,InteractionRules.Solid);
            Check(InteractionSettings.Read(settings) == InteractionRules.Solid && File.Exists(settings+".bak"),"Settings or rollback persistence failed");
            File.WriteAllText(settings,"255"); Check(InteractionSettings.Read(settings) == InteractionRules.Ghosts,"Invalid saved flags enabled physics");
            var layout = new UiPageLayout(new Rectangle(12,12,456,336),1,4);
            Check(layout.Content.Bottom <= layout.Status.Top && layout.Content.Height >= 5*22 && layout.Status.Bottom <= layout.Footers[0].Top,"UI list/status/footer overlap");
            Check(typeof(NativeMod).GetMethod("MainSettings").IsDefined(typeof(JKRuntime.Modules.MainMenuItemSettingAttribute),false) &&
                typeof(NativeMod).GetMethod("PauseSettings").IsDefined(typeof(JKRuntime.Modules.PauseMenuItemSettingAttribute),false),"Settings page isn't reachable from both menus");

            // these are the actual installed native fields consumed by input after body physics
            var native = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
            AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(native,true);
            AccessTools.Field(typeof(BodyComp),"_knocked").SetValue(native,false);
            Check(native.IsOnGround && !native.IsKnocked,"Installed native grounding contract changed");
            Authority();
            NativePhysics();
            Console.WriteLine("[OK] Player interactions: " + checks + " checks (contacts, terrain, wire, expiry, rules, storage, UI layout, native grounding)");
        }
        private sealed class EmptyMap : JumpKing.API.ICollisionQuery
        {
            public JumpKing.Level.AdvCollisionInfo GetCollisionInfo(Rectangle bounds) { return new JumpKing.Level.AdvCollisionInfo(); }
            public bool CheckCollision(Rectangle bounds,out Rectangle overlap,out JumpKing.Level.AdvCollisionInfo info) { overlap=Rectangle.Empty;info=GetCollisionInfo(bounds);return false; }
            public bool CheckCollision(Rectangle bounds,out Rectangle overlap,out JumpKing.Level.AggregateCollisionInfo info) { overlap=Rectangle.Empty;info=new JumpKing.Level.AggregateCollisionInfo(GetCollisionInfo(bounds));return false; }
            public bool IsInWater(Rectangle bounds) { return false; }
        }
        private static void NativePhysics()
        {
            var body = new BodyComp(new Vector2(100,65),18,26);
            var handlers = new LinkedList<JumpKing.API.IBlockBehaviour>();
            var behaviors = (LinkedList<JumpKing.API.IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(body);
            // real native integration/resolution/gravity, without screen, audio or particle services
            behaviors.Clear();
            behaviors.AddLast(new JumpKing.BodyCompBehaviours.UpdateXPositionFromVelocityBehaviour(handlers));
            behaviors.AddLast(new JumpKing.BodyCompBehaviours.UpdateYPositionFromVelocityBehaviour(handlers));
            behaviors.AddLast((JumpKing.API.IBodyCompBehaviour)Activator.CreateInstance(typeof(JumpKing.BodyCompBehaviours.ResolveYCollisionBehaviour),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{new EmptyMap(),handlers},null));
            behaviors.AddLast(new JumpKing.BodyCompBehaviours.ApplyGravityBehaviour(handlers));
            var step=AccessTools.Method(typeof(BodyComp),"UpdateInternal");
            var contacts=new PlayerContacts(); var peers=new[]{Peer()};
            body.Velocity=new Vector2(0,17);
            Action tick=delegate { var before=body.Position; step.Invoke(body,new object[]{1f/60});contacts.Apply(body,before,peers,InteractionRules.Solid,1,r=>false); };
            tick(); Check(body.IsOnGround && body.Position.Y==74,"Native gravity/contact integration didn't land");
            for(int i=0;i<120;i++) tick();
            Check(body.IsOnGround && body.Position.Y==74,"Native standing drifted over 120 ticks");
            body.Velocity.Y=-10; tick();
            Check(!body.IsOnGround && body.Position.Y==64,"Native jump didn't leave remote support");
            body.Position=new Vector2(100,74);body.Velocity=Vector2.Zero;tick();
            var previous=body.Position; step.Invoke(body,new object[]{1f/60});contacts.Apply(body,previous,new InteractionPeer[0],InteractionRules.Solid,1,r=>false);
            Check(!body.IsOnGround && contacts.Support==0,"Disconnected support kept native grounding");
        }
        private static void Authority()
        {
            var oldManager = JumpKingMultiplayer.Models.MultiplayerManager.instance;
            var oldLink = Client.Link; int oldRole = Client.Role;
            var fields = new[] { "lobby", "owner", "rulesEpoch", "revision", "lastOwner" };
            var oldValues = new object[fields.Length];
            for (int i=0;i<fields.Length;i++) oldValues[i]=AccessTools.Field(typeof(AdvancedSession),fields[i]).GetValue(null);
            var rules = AccessTools.Property(typeof(AdvancedSession),"Rules"); var oldRules = rules.GetValue(null,null);
            try
            {
                using (var link = new LocalLink(true,Guid.NewGuid().ToString("N")))
                {
                    Client.Link=link; Client.Role=2;
                    var manager=(JumpKingMultiplayer.Models.MultiplayerManager)FormatterServices.GetUninitializedObject(typeof(JumpKingMultiplayer.Models.MultiplayerManager));
                    JumpKingMultiplayer.Models.MultiplayerManager.instance=manager;
                    AccessTools.Field(typeof(AdvancedSession),"lobby").SetValue(null,1UL);
                    AccessTools.Field(typeof(AdvancedSession),"owner").SetValue(null,1UL);
                    var frame=Peer().Frame; frame.Epoch=frame.RulesEpoch=99; frame.Revision=4;
                    frame.Support=1;frame.SupportEpoch=99;
                    AdvancedSession.Receive(1,InteractionWire.Encode(frame));
                    Check(((Dictionary<ulong,InteractionPeer>)AccessTools.Field(typeof(AdvancedSession),"peers").GetValue(null)).Count==0,"Invalid support left a half-created peer");
                    frame.Support=0;
                    AdvancedSession.Receive(1,InteractionWire.Encode(frame));
                    Check(AdvancedSession.Rules==InteractionRules.Solid && !AdvancedSession.CanEdit,"Client didn't adopt host rules");
                    AdvancedSession.SetRules(InteractionRules.Ghosts);
                    Check(AdvancedSession.Rules==InteractionRules.Solid,"Client changed authoritative settings");
                    frame.Sequence++; frame.Rules=InteractionRules.Ghosts;
                    AdvancedSession.Receive(3,InteractionWire.Encode(frame));
                    Check(AdvancedSession.Rules==InteractionRules.Solid,"Nonmember changed host rules");
                    AdvancedSession.Receive(1,InteractionWire.Encode(frame));
                    Check(AdvancedSession.Rules==InteractionRules.Ghosts,"Host change didn't reach client");
                    frame.Sequence--; frame.Rules=InteractionRules.Solid;
                    AdvancedSession.Receive(1,InteractionWire.Encode(frame));
                    Check(AdvancedSession.Rules==InteractionRules.Ghosts,"Delayed state rolled host rules back");
                }
            }
            finally
            {
                JumpKingMultiplayer.Models.MultiplayerManager.instance=oldManager; Client.Link=oldLink; Client.Role=oldRole;
                for (int i=0;i<fields.Length;i++) AccessTools.Field(typeof(AdvancedSession),fields[i]).SetValue(null,oldValues[i]);
                rules.SetValue(null,oldRules,null);
                ((Dictionary<ulong,InteractionPeer>)AccessTools.Field(typeof(AdvancedSession),"peers").GetValue(null)).Clear();
            }
        }
    }
}
