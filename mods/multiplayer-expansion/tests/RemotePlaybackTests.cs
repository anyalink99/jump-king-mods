using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MultiplayerExpansion
{
    internal static class RemotePlaybackTests
    {
        private static int checks;
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        private static InteractionFrame Frame(int tick)
        {return new InteractionFrame{Epoch=1,Sequence=(ulong)tick+1,SimulationTick=(ulong)tick,RulesEpoch=1,Revision=1,Rules=InteractionRules.Solid,Active=true,Modern=true,Physics=true,Time=tick/60.0,Position=new Vector2(tick*2,100),Velocity=new Vector2(2,0),Width=18,Height=26,Grounded=true};}
        internal static void Run()
        {
            foreach(bool local in new[]{true,false})foreach(int jitter in new[]{0,8,25})Walk(local,jitter);
            Boundaries();Curves();Contacts();Pixels();
            Console.WriteLine("[OK] Remote playback: "+checks+" checks (smooth packet bursts, jitter/loss, no speculative wall crossing, bounce/apex bounds, aligned pose, contact blending, support chains and lifecycle)");
        }
        private static void Walk(bool local,int jitter)
        {
            var playback=new RemotePlayback();var prediction=new RemoteMotion();
            int sent=0,latest=-1;float prior=0,maxStep=0,predictionStep=0,priorPrediction=0;double priorTime=0;
            // 240 Hz drawing exposes corrections hidden between two 60 Hz samples
            for(int draw=0;draw<2400;draw++){
                double now=draw/240.0;
                while(sent<600 && sent/60.0+.02+(sent%5)*jitter/4000.0<=now){
                    if(sent%17!=7){var f=Frame(sent);playback.Accept(f,now);prediction.Accept(f,now);latest=sent;}sent++;
                }
                var frame=playback.Sample(now,local);if(frame==null)continue;
                var predicted=prediction.Predict(now,.02);
                if(draw>120){
                    Check(playback.Time>=priorTime,"Arrival jitter rewound the render clock");
                    Check(frame.Position.X>=prior-.001f,"Arrival jitter reversed a walking sprite");
                    maxStep=Math.Max(maxStep,frame.Position.X-prior);
                    predictionStep=Math.Max(predictionStep,Math.Abs(predicted.Position.X-priorPrediction));
                    Check(frame.Position.X<=Frame(latest).Position.X+.001f,"Playback invented unsent motion");
                }
                prior=frame.Position.X;priorPrediction=predicted.Position.X;priorTime=playback.Time;
            }
            Check(maxStep<.8f,"A received burst became a visible jump: local="+local+" jitter="+jitter+" step="+maxStep);
            if(jitter>0)Check(maxStep<predictionStep*.65f,"Playback didn't reduce prediction's correction spikes");
            Check(playback.Count<=32,"Render history grew without a bound");
            var held=playback.Sample(20,local);
            Check(held.Position==Frame(latest).Position,"Packet loss predicted a sprite past its last known body");
            Check(playback.Sample(21,local).Position==held.Position,"A stalled sprite kept drifting");
        }
        private static void Boundaries()
        {
            var playback=new RemotePlayback();playback.Accept(Frame(0),0);playback.Accept(Frame(1),1/60.0);
            var paused=Frame(2);paused.Presence=PlayerPresence.Paused;paused.Position=new Vector2(8,80);paused.Velocity=Vector2.Zero;
            playback.Accept(paused,.04);Check(playback.Sample(.05,true).Position==paused.Position,"Pause interpolated away from the frozen collider");
            var teleport=Frame(3);teleport.Transition=1;teleport.Position=new Vector2(350,-3000);
            playback.Accept(teleport,.06);Check(playback.Sample(.06,true).Position==teleport.Position && playback.Count==1,"Teleport drew a path through the level");
            var map=Frame(4);map.Map=99;playback.Accept(map,.08);Check(playback.Count==1,"Playback mixed different maps");
            var attempt=Frame(5);attempt.Epoch=2;playback.Accept(attempt,.1);Check(playback.Count==1,"Playback retained another attempt");
            var reconnect=attempt.Copy();reconnect.Sequence++;reconnect.Time=1;reconnect.Position.X=100;
            playback.Accept(reconnect,1);Check(playback.Count==1 && playback.Sample(1,true).Position==reconnect.Position,"Reconnect inherited an old render clock");
            playback=new RemotePlayback();var wall=Frame(1);wall.Position.X=1;wall.Velocity.X=3;
            playback.Accept(wall,0);Check(playback.Sample(.3,true).Position.X==1,"Walking intent drew the player through a wall");
            playback=new RemotePlayback();
            for(int tick=0;tick<12;tick++){var f=Frame(tick);f.Pose=(byte)tick;playback.Accept(f,tick/60.0);}
            var sample=playback.Sample(.12,true);sample=playback.Sample(.16,true);
            Check(sample.Pose==(byte)Math.Floor(playback.Time*60+.00001),"Pose used a different clock from movement");
        }
        private static void Curves()
        {
            var playback=new RemotePlayback();
            for(int tick=0;tick<14;tick++){
                var f=Frame(tick);f.Grounded=false;f.Position.X=tick<7 ? tick*3 : (14-tick)*3;
                f.Position.Y=100-10*tick+tick*tick;playback.Accept(f,tick/60.0);
            }
            for(int draw=0;draw<100;draw++){
                var frame=playback.Sample(draw/480.0,true);double time=playback.Time;
                int a=Math.Min(12,(int)Math.Floor(time*60)),b=a+1;
                float ax=a<7 ? a*3 : (14-a)*3,bx=b<7 ? b*3 : (14-b)*3;
                float ay=100-10*a+a*a,by=100-10*b+b*b;
                Check(frame.Position.X>=Math.Min(ax,bx)-.001 && frame.Position.X<=Math.Max(ax,bx)+.001,"Bounce curve overshot its received endpoints");
                Check(frame.Position.Y>=Math.Min(ay,by)-.001 && frame.Position.Y<=Math.Max(ay,by)+.001,"Apex curve overshot its received endpoints");
            }
        }
        private static void Contacts()
        {
            var local=new Rectangle(100,100,18,26);var current=Frame(0);current.Position=new Vector2(160,100);
            float last=0;
            for(int x=180;x>=118;x--){current.Position.X=x;float weight=RemotePlayback.ContactWeight(local,current);
                Check(weight>=last && weight>=0 && weight<=1,"Contact blend flickered while approaching a king");
                Check(weight-last<.04f,"Contact proximity caused an abrupt visual mode switch");last=weight;
            }
            Check(last==1,"Touching sprites retained playback delay");
            var buffered=current.Copy();buffered.Position.X-=4;
            Check(RemotePlayback.Blend(buffered,current,0)==buffered && RemotePlayback.Blend(buffered,current,1)==current,"Contact blend didn't preserve its exact endpoints");
            var blend=RemotePlayback.Blend(buffered,current,.5f);Check(blend.Position.X==116 && buffered.Position.X==114 && current.Position.X==118,"Render blend modified a collision snapshot");
            var rider=Frame(0);rider.Support=1;rider.SupportEpoch=1;rider.SupportOffset=new Vector2(2,-26);
            var result=SupportGraph.Resolve(2,id=>id==1 ? blend : rider);
            Check(result.Position==blend.Position+rider.SupportOffset,"Rider and carrier used different render clocks");
        }
        private static void Pixels()
        {
            var sprite=JumpKing.Sprite.CreateSpriteWithCenter(null,new Rectangle(12,24,48,48),new Vector2(.5f,1));
            Check(RemotePresentation.SpritePosition(sprite,new Vector2(100.8f,100.8f))==new Vector2(76,52),"Remote sprite wasn't aligned to native pixels");
            Check(RemotePresentation.SpritePosition(sprite,new Vector2(10.2f,.2f))==new Vector2(-13,-47),"Remote snapping differed from native Sprite.Draw above the screen");
        }
    }
}
