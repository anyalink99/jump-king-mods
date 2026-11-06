using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class WalkingContactTests
    {
        private static int checks;
        private static void Check(bool value,string reason) {checks++;if(!value) throw new Exception(reason);}
        private static InteractionFrame Frame(float x,float vx=0)
        { return new InteractionFrame{Epoch=1,Sequence=1,Active=true,Physics=true,Modern=true,Grounded=true,Width=18,Height=26,Position=new Vector2(x,100),Velocity=new Vector2(vx,0)}; }
        private static ContactBody Body(float x,float vx=0)
        { return new ContactBody{Before=new Vector2(x,100),Position=new Vector2(x+vx,100),Velocity=new Vector2(vx,0),NativeGrounded=true,Width=18,Height=26}; }
        internal static void Run()
        {
            foreach(int side in new[]{-1,1})
            {
                var solver=new PlayerContacts();var peer=new InteractionPeer{Id=2,Frame=Frame(100+side*18,-side*3)};
                var target=Body(100);
                for(int tick=0;tick<180;tick++)
                {
                    peer.Sample=peer.Frame.Copy();peer.Sample.Position.X-=side*(tick%7)*2;
                    target.Before=target.Position;
                    solver.Resolve(target,new[]{peer},InteractionRules.Sides,1,r=>false);
                    Check(target.Position==new Vector2(100,100) && !target.Bump,"Push-off let predicted walking displace a stationary king");
                    Check(peer.Sample.Position.X==peer.Frame.Position.X-side*(tick%7)*2,"Walking filter overwrote the received/predicted snapshot");
                }
                // actual delayed coordinates can also overlap; the established edge stays put
                peer.Frame.Position.X-=side*3;peer.Sample=peer.Frame;
                solver.Resolve(target,new[]{peer},InteractionRules.Sides,1,r=>false);
                Check(target.Position.X==100,"Push-off recovered a walking overlap by moving the victim");
                target=Body(100,-side*2);
                solver.Resolve(target,new[]{peer},InteractionRules.Sides,1,r=>false);
                Check(target.Position.X==100-side*2,"Push-off prevented walking away from contact");
            }
            var wall=new InteractionPeer{Id=2,Frame=Frame(118)};var mover=Body(100,3);
            new PlayerContacts().Resolve(mover,new[]{wall},InteractionRules.Sides,1,r=>false);
            Check(mover.Position.X==100 && mover.Velocity.X==0,"Push-off let the moving king walk through a stationary king");
            var moving=new InteractionPeer{Id=2,Frame=Frame(82,3)};var pushed=Body(100);
            new PlayerContacts().Resolve(pushed,new[]{moving},InteractionRules.Solid,1,r=>false);
            Check(pushed.Position.X==103,"Push-on stopped applying walking pushes");
            var solverReset=new PlayerContacts();var spawn=Body(100);
            solverReset.Resolve(spawn,new[]{moving},InteractionRules.Sides,1,r=>false);
            moving.Frame=Frame(100);moving.Frame.Epoch=2;
            solverReset.Resolve(spawn,new[]{moving},InteractionRules.Sides,1,r=>false);
            Check(!ContactRecovery.Overlaps(spawn.Position,18,26,moving.Frame),"No-push contact hid a respawn overlap");
            var fresh=Body(100);new PlayerContacts().Resolve(fresh,new[]{moving},InteractionRules.Sides,1,r=>false);
            Check(!ContactRecovery.Overlaps(fresh.Position,18,26,moving.Frame),"No-push disabled fresh spawn recovery");
            var flying=Body(96,8);flying.NativeGrounded=false;
            wall.Frame=Frame(118);
            new PlayerContacts().Resolve(flying,new[]{wall},InteractionRules.Sides,1,r=>false);
            Check(flying.Bump && flying.Velocity.X==-8*JumpKing.PlayerValues.BOUNCE,"No-push suppressed an airborne wall bounce");
            PairWithDelay(0);PairWithDelay(6);
            Console.WriteLine("[OK] Walking contacts: "+checks+" checks (push on/off, prediction, delayed coordinates, separation, spawn and bounce)");
        }
        private static void PairWithDelay(int delay)
        {
            var leftSolver=new PlayerContacts();var rightSolver=new PlayerContacts();
            float left=60,right=118;
            var leftPackets=new Queue<InteractionFrame>();var rightPackets=new Queue<InteractionFrame>();
            InteractionFrame receivedLeft=Frame(left),receivedRight=Frame(right);
            for(int tick=0;tick<240;tick++)
            {
                var leftBody=Body(left,3);var rightBody=Body(right);
                var leftRemote=new InteractionPeer{Id=2,Frame=receivedRight,Sample=receivedRight.Copy()};
                var rightRemote=new InteractionPeer{Id=1,Frame=receivedLeft,Sample=receivedLeft.Copy()};
                rightRemote.Sample.Position.X+=Math.Min(6,delay+1)*receivedLeft.Velocity.X;
                leftSolver.Resolve(leftBody,new[]{leftRemote},InteractionRules.Sides,1,r=>false);
                rightSolver.Resolve(rightBody,new[]{rightRemote},InteractionRules.Sides,2,r=>false);
                left=leftBody.Position.X;right=rightBody.Position.X;
                Check(right==118,"Stationary peer drifted under sustained walking with packet delay");
                Check(left<=100,"Walking peer passed through a non-pushable king");
                leftPackets.Enqueue(Frame(left,3));rightPackets.Enqueue(Frame(right));
                if(leftPackets.Count>delay) {receivedLeft=leftPackets.Dequeue();receivedRight=rightPackets.Dequeue();}
            }
            Check(left==100 && right==118,"Push-off contact didn't settle at the shared boundary");
        }
    }
}
