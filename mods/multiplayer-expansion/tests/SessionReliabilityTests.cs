using System;
using Microsoft.Xna.Framework;
using HarmonyLib;
using JumpKingMultiplayer.Models;

namespace MultiplayerExpansion
{
    internal static class SessionReliabilityTests
    {
        private static int checks;
        private static void Check(bool value,string reason) {checks++;if(!value) throw new Exception(reason);}
        private static InteractionFrame Frame(float x,float y)
        {return new InteractionFrame{Active=true,Physics=true,Modern=true,Epoch=1,Sequence=1,RulesEpoch=1,Revision=1,Rules=InteractionRules.Solid,Position=new Vector2(x,y),Width=18,Height=26};}
        internal static void Run()
        {
            Hotkey();GroundedContacts();Presence();
            Console.WriteLine("[OK] Session reliability: "+checks+" checks (6 key edges/focus, grounded contact ownership, pause presence/support and draw lifecycle)");
        }
        private static void Hotkey()
        {
            var key=new ClientSwitchKey();
            Check(!key.Poll(false,true,true) && !key.Poll(true,true,true),"Held 6 switched on client startup");
            Check(!key.Poll(true,true,false) && key.Poll(true,true,true),"6 didn't switch on a fresh press");
            for(int i=0;i<60;i++) Check(!key.Poll(true,true,true),"Held 6 repeatedly switched clients");
            Check(!key.Poll(true,false,false) && !key.Poll(true,false,true) && !key.Poll(true,true,true),"Focus changes replayed an external keypress");
            Check(!key.Poll(true,true,false) && key.Poll(true,true,true),"6 didn't recover after returning to the game");
            string previous=Environment.GetEnvironmentVariable("MPEX_NATIVE");
            try {Environment.SetEnvironmentVariable("MPEX_NATIVE","1");Check(Client.ReservedKey(0x36) && !Client.ReservedKey(117) && !Client.ReservedKey(27),"Native key reservation intercepted the wrong game keys");}
            finally {Environment.SetEnvironmentVariable("MPEX_NATIVE",previous);}
        }
        private static void GroundedContacts()
        {
            foreach(ulong self in new[]{1UL,2UL}) foreach(var rules in new[]{InteractionRules.Solid,InteractionRules.Sides|InteractionRules.Platforms})
            {
                var solver=new PlayerContacts();int hits=0;solver.Impact=(p,n,a,b)=>hits++;
                var lower=new ContactBody{Before=new Vector2(100,100),Position=new Vector2(100,100),Width=18,Height=26,NativeGrounded=true};
                var air=Frame(100,90);air.Velocity=new Vector2(4,10);
                var peer=new InteractionPeer{Id=3-self,Frame=air};
                for(int tick=0;tick<120;tick++)
                {
                    air.Position=new Vector2(tick%2==0 ? 100 : 90, tick%3==0 ? 90 : 105);
                    solver.Resolve(lower,new[]{peer},rules,self,r=>false);
                    Check(lower.Position==new Vector2(100,100) && lower.Velocity==Vector2.Zero && !lower.Bump && hits==0,"Airborne overlap recovery moved the anchored king");
                }
                var upper=new ContactBody{Before=new Vector2(100,65),Position=new Vector2(100,85),Velocity=new Vector2(0,20),Width=18,Height=26};
                var floor=Frame(100,100);floor.Grounded=true;
                var upperSolver=new PlayerContacts();upperSolver.Resolve(upper,new[]{new InteractionPeer{Id=self,Frame=floor}},rules,3-self,r=>false);
                Check(upper.Grounded && upper.Position.Y==74 && upperSolver.Support==self,"Landing wasn't resolved on the airborne owner's side");
                air.Position=upper.Position;air.Support=self;air.SupportOffset=new Vector2(0,-26);air.Grounded=true;
                solver.Resolve(lower,new[]{peer},rules,self,r=>false);
                Check(lower.Position==new Vector2(100,100),"Attached passenger moved its grounded carrier");
            }
        }
        private static void Presence()
        {
            Check(SessionPresence.Active(true,true,true,true,120),"Pause turned a live body into a ghost after heartbeat timeout");
            Check(!SessionPresence.Active(false,true,true,true,0) && !SessionPresence.Active(true,false,true,true,0) && !SessionPresence.Active(true,true,false,true,0),"Pause retained a dead, replaced or other-map body");
            Check(!SessionPresence.Active(true,true,true,false,1),"Stopped simulation remained solid without an explicit pause");
            var frozen=Frame(100,100);frozen.Velocity=new Vector2(3,-12);frozen.Support=3;frozen.SupportEpoch=1;frozen.SupportOffset=new Vector2(0,-26);
            frozen.Impacts=new[]{new ContactImpact()};SessionPresence.Freeze(frozen,true);
            Check(frozen.Active && frozen.Presence==PlayerPresence.Paused && !frozen.Grounded && frozen.Anchored && frozen.Velocity==Vector2.Zero && frozen.Support==0 && frozen.Impacts.Length==0,"Paused frame kept motion or lost collision presence");
            InteractionFrame decoded;Check(InteractionWire.Decode(InteractionWire.Encode(frozen),out decoded) && decoded.Active && decoded.Presence==PlayerPresence.Paused && decoded.Anchored && decoded.Velocity==Vector2.Zero,"Pause presence didn't survive the network packet");
            var peer=new InteractionPeer{Id=1};var solver=new PlayerContacts();var rider=new ContactBody{Before=new Vector2(100,73),Position=new Vector2(100,75),Velocity=new Vector2(0,2),Width=18,Height=26};
            for(int i=0;i<180;i++)
            {
                frozen=frozen.Copy();frozen.Sequence=(ulong)i+1;frozen.Time=i/60.0;peer.Accept(frozen,i/60.0);peer.Prepare(i/60.0+.08,false);
                Check(peer.Ready(i/60.0+.08,0,1,1,InteractionRules.Solid) && peer.Sample.Position==frozen.Position,"Paused platform drifted or expired despite live heartbeats");
                solver.Resolve(rider,new[]{peer},InteractionRules.Solid,2,r=>false);
                Check(rider.Grounded && rider.Position.Y==74,"Rider fell through a king in the pause menu");
                rider.Before=rider.Position;rider.Position.Y+=.3f;rider.Velocity=new Vector2(0,.3f);
            }
            Check(!peer.Ready(5,0,1,1,InteractionRules.Solid),"Disconnected paused peer stayed solid forever");
            var manager=MultiplayerManager.instance;
            try
            {
                MultiplayerManager.instance=(MultiplayerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MultiplayerManager));
                var drawn=AccessTools.Field(typeof(MultiplayerManager),"_alreadyDrawedPlayers");
                for(int i=0;i<3;i++) {drawn.SetValue(MultiplayerManager.instance,true);RemotePresentation.BeginFrame();Check(!(bool)drawn.GetValue(MultiplayerManager.instance),"Pause retained the previous draw's ghost suppression flag");}
            }
            finally {MultiplayerManager.instance=manager;}
        }
    }
}
