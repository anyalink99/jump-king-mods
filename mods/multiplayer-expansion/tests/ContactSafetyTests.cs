using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.BlockBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class ContactSafetyTests
    {
        private static int checks;
        private static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
        private static ContactBody Body(float x=100,float y=100)
        { return new ContactBody { Before=new Vector2(x,y),Position=new Vector2(x,y),Width=18,Height=26 }; }
        private static InteractionPeer Peer(ulong id=2,float x=100,float y=100)
        { return new InteractionPeer { Id=id, Frame=new InteractionFrame { Active=true,Physics=true,Grounded=true,Width=18,Height=26,Position=new Vector2(x,y),Epoch=1 } }; }
        private static bool Clear(ContactBody body, InteractionPeer[] peers,Func<Rectangle,bool> blocked)
        {
            if(blocked(new Rectangle((int)body.Position.X,(int)body.Position.Y,body.Width,body.Height))) return false;
            foreach(var peer in peers) if(ContactRecovery.Overlaps(body.Position,body.Width,body.Height,peer.Frame)) return false;
            return true;
        }
        internal static void Run()
        {
            var peers=new[]{Peer()};Func<Rectangle,bool> empty=r=>false;
            foreach(var obstacle in new Func<Rectangle,bool>[] { empty, r=>r.Left<100, r=>r.Right>118,
                r=>r.Bottom>126, r=>r.Top<100, r=>r.Left<100 || r.Right>118 || r.Bottom>126 })
            {
                var body=Body();body.NativeGrounded=true;int impacts=0;
                var solver=new PlayerContacts();solver.Impact=(p,n,a,b)=>impacts++;
                solver.Resolve(body,peers,InteractionRules.Solid,1,obstacle);
                Check(Clear(body,peers,obstacle),"Spawn recovery couldn't find reachable space beside terrain");
                Check(!body.Bump && !body.Knocked && impacts==0,"Spawn correction produced an impact");
            }
            var left=Body();var right=Body();
            new PlayerContacts().Resolve(left,new[]{Peer(2)},InteractionRules.Solid,1,empty);
            new PlayerContacts().Resolve(right,new[]{Peer(1)},InteractionRules.Solid,2,empty);
            Check(left.Position.X==82 && right.Position.X==118,"Equal-position peers chose the same escape side");
            peers=new[]{Peer(2),Peer(3,82),Peer(4,118)};
            var crowded=Body();new PlayerContacts().Resolve(crowded,peers,InteractionRules.Solid,1,empty);
            Check(Clear(crowded,peers,empty),"Recovery placed the player inside a third king");
            var corner=Body();peers=new[]{Peer(2),Peer(3,82),Peer(4,118),Peer(5,100,74),Peer(6,100,126)};
            new PlayerContacts().Resolve(corner,peers,InteractionRules.Solid,1,empty);
            Check(corner.Position==new Vector2(100,100),"Recovery crossed a surrounding king to reach a free endpoint");
            peers=new[]{Peer()};var trapped=Body();var trappedSolver=new PlayerContacts();
            Func<Rectangle,bool> box=r=>r.Left<100 || r.Right>118 || r.Top<100 || r.Bottom>126;
            for(int i=0;i<120;i++)
            {
                trappedSolver.Resolve(trapped,peers,InteractionRules.Solid,1,box);
                Check(trapped.Position==new Vector2(100,100) && trapped.Velocity==Vector2.Zero && !trapped.Bump,"No-space recovery jittered or crossed terrain");
            }
            trappedSolver.Resolve(trapped,peers,InteractionRules.Solid,1,empty);
            Check(Clear(trapped,peers,empty),"Recovery didn't resume after space became available");
            var teleport=Body();teleport.Before=new Vector2(400,-500);teleport.Velocity=new Vector2(5,8);
            new PlayerContacts().Resolve(teleport,peers,InteractionRules.Solid,1,empty);
            Check(Clear(teleport,peers,empty) && !teleport.Bump && !teleport.Knocked,"Teleport was bounced instead of separated");
            foreach(var rules in new[]{InteractionRules.Ghosts,InteractionRules.Platforms})
            {
                var passthrough=Body();new PlayerContacts().Resolve(passthrough,peers,rules,1,empty);
                Check(passthrough.Position==new Vector2(100,100),"Recovery made a pass-through mode solid");
            }
            // repeat the same received correction; it must settle instead of alternating sides
            var corrected=Body();var stableSolver=new PlayerContacts();
            stableSolver.Resolve(corrected,peers,InteractionRules.Solid,1,empty);
            var settled=corrected.Position;
            for(int i=0;i<60;i++) {corrected.Before=corrected.Position;stableSolver.Resolve(corrected,peers,InteractionRules.Solid,1,empty);}
            Check(corrected.Position==settled && !corrected.Bump,"Separated stationary peers didn't settle");
            var shallow=Body(117,100);shallow.NativeGrounded=true;var shallowSolver=new PlayerContacts();
            for(int i=0;i<3;i++) shallowSolver.Resolve(shallow,new[]{Peer()},InteractionRules.Solid,1,empty);
            Check(Clear(shallow,new[]{Peer()},empty) && !shallow.Bump,"A shallow unresolved overlap stayed stuck");
            Feedback();
            Console.WriteLine("[OK] Contact safety: "+checks+" checks (spawn, teleport, crowded/blocked recovery, native dry/wet SFX and impact deduplication)");
        }
        private sealed class Bumps : IBumpSFXPlayer
        {
            internal int Count; internal bool Water;
            public void PlayBumpSFX(bool water) { Count++;Water=water; }
        }
        private static void Feedback()
        {
            var native=new BodyComp(new Vector2(86,104),18,26);native.Velocity=new Vector2(8,-1);
            AccessTools.Field(typeof(BodyComp),"_is_on_ground").SetValue(native,false);
            var context=(BehaviourContext)AccessTools.Field(typeof(BodyComp),"m_behaviourContext").GetValue(native);
            var behaviours=(LinkedList<IBodyCompBehaviour>)AccessTools.Field(typeof(BodyComp),"m_behaviours").GetValue(native);
            var bumps=new Bumps();
            var nativeSound=(IBodyCompBehaviour)Activator.CreateInstance(typeof(PlayBumpSFXBehaviour),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{bumps},null);
            behaviours.Clear();behaviours.AddLast(nativeSound);
            var wall=Peer();wall.Frame.Grounded=true;var solver=new PlayerContacts();
            solver.Apply(native,new Vector2(78,105),new[]{wall},InteractionRules.Solid,1,r=>false);
            Check(bumps.Count==1 && !bumps.Water && native.IsKnocked,"Real contact didn't dispatch the native dry bump");
            ContactFeedback.Request(native);ContactFeedback.Flush(native);
            Check(bumps.Count==1,"Native and peer contact sounded twice in one frame");
            context.Clear();((WaterBlockBehaviour)AccessTools.Method(typeof(BodyComp),"GetBlockBehaviour",new[]{typeof(Type)}).Invoke(native,new object[]{typeof(WaterBlock)})).IsPlayerOnBlock=true;
            ContactFeedback.Request(native);ContactFeedback.Request(native);ContactFeedback.Flush(native);ContactFeedback.Flush(native);
            Check(bumps.Count==2 && bumps.Water,"Contact didn't use the native underwater SFX selection");
            context.Clear();context[PlayBumpSFXBehaviour.PlayBumpSFXFlag]=true;nativeSound.ExecuteBehaviour(context);
            ContactFeedback.Request(native);ContactFeedback.Flush(native);
            Check(bumps.Count==3,"A native wall plus a king impact doubled the sound");
            context.Clear();native.Position=new Vector2(100,128);native.Velocity=new Vector2(0,-10);
            native.Position.Y=118;
            solver.Apply(native,new Vector2(100,128),new[]{wall},InteractionRules.Solid,1,r=>false);
            Check(bumps.Count==4 && native.Velocity.Y==0,"Underside contact missed its native bump");
            context.Clear();native.Position=new Vector2(100,100);native.Velocity=Vector2.Zero;
            solver.Apply(native,native.Position,new[]{wall},InteractionRules.Solid,1,r=>false);
            Check(bumps.Count==4,"Spawn separation played an impact sound");
            context.Clear();ContactFeedback.Request(native);ContactFeedback.Cancel(native);ContactFeedback.Flush(native);
            Check(bumps.Count==4,"An inactive session retained a queued bump");
            var hit=new ContactImpact{Id=1,Target=2,TargetEpoch=1,Normal=new Vector2(1,0),Outgoing=new Vector2(4,0)};
            var ledger=new ContactLedger();var velocity=new Vector2(-8,2);bool first;
            Check(ledger.Confirm(1,2,1,0,hit,.1,1,false,ref velocity,out first) && first,"First remote-confirmed impact lost its sound");
            Check(!ledger.Confirm(1,2,1,0,hit,.1,1.01,false,ref velocity,out first) && !first,"Repeated packet replayed its sound");
            ledger=new ContactLedger();var owner=Peer(1);
            ledger.Record(2,owner,new Vector2(1,0),new Vector2(3,0),new Vector2(-3,0),1);
            velocity=new Vector2(3,2);
            Check(ledger.Confirm(1,2,1,0,hit,.1,1.1,false,ref velocity,out first) && !first,"Prediction confirmation replayed its sound");
        }
    }
}
