using System;
using System.IO;

namespace MegaMappingExpansion
{
    internal sealed class PlatformMotionState
    {
        internal int From,To,Direction=1;
        internal double Distance,Wait,Time;
        internal bool Playing,Blocked;
        internal PlatformMotionState Copy(){return (PlatformMotionState)MemberwiseClone();}
    }
    // the editor and runtime advance exactly the same route, including leftover time
    internal sealed class PlatformMotionPlayer
    {
        internal readonly PlatformMotion Definition;
        internal PlatformMotionState State;
        internal Action<int> Arrived;
        internal PlatformMotionPlayer(PlatformMotion definition){Definition=definition;Reset();}
        internal PlatformPoint Position
        {
            get
            {
                var a=Definition.Points[State.From].Position;var b=Definition.Points[State.To].Position;double length=MovingPlatformPlan.Distance(a,b);
                double t=length==0?0:State.Distance/length;return new PlatformPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
            }
        }
        internal PlatformPoint Velocity
        {
            get
            {
                if(!State.Playing||State.Blocked||State.Wait>0||State.From==State.To)return new PlatformPoint(0,0);
                var a=Definition.Points[State.From].Position;var b=Definition.Points[State.To].Position;double length=MovingPlatformPlan.Distance(a,b);
                return length==0?new PlatformPoint(0,0):new PlatformPoint((b.X-a.X)*Definition.Speed/length,(b.Y-a.Y)*Definition.Speed/length);
            }
        }
        internal void Obstruct()
        {
            State.Blocked=true;
            if(Definition.OnBlocked=="stop")State.Playing=false;
            else if(Definition.OnBlocked=="reverse"){Command("reverse");State.Wait=Math.Max(State.Wait,Definition.BlockedWait);}
        }
        internal void Reset()
        {
            State=new PlatformMotionState{From=0,To=1,Distance=Definition.StartDistance,Wait=Definition.Points[0].Wait,Playing=true};
            if(Definition.Phase>0)Advance(Definition.Phase,false);
            State.Playing=State.Playing&&Definition.Playing;
        }
        internal void Command(string command)
        {
            State.Blocked=false;
            switch(command)
            {
                case "start":State.Playing=true;break;
                case "stop":State.Playing=false;break;
                case "reset":Reset();break;
                case "reverse":
                    if(State.Distance==0)
                    {
                        State.Direction=-State.Direction;int next=State.From+State.Direction;
                        if(next<0||next>=Definition.Points.Count)
                        {
                            if(Definition.Mode=="loop")next=next<0?Definition.Points.Count-1:0;
                            else if(Definition.Mode=="pingPong"){State.Direction=-State.Direction;next=State.From+State.Direction;}
                            else{next=State.From;State.Playing=false;}
                        }
                        State.To=next;break;
                    }
                    double length=MovingPlatformPlan.Distance(Definition.Points[State.From].Position,Definition.Points[State.To].Position);
                    int from=State.From;State.From=State.To;State.To=from;State.Distance=length-State.Distance;State.Direction=-State.Direction;break;
                default:throw new InvalidDataException("Unknown platform command: "+command);
            }
        }
        internal void Advance(double delta,bool emit=true)
        {
            if(double.IsNaN(delta)||double.IsInfinity(delta)||delta<0||delta>86400)throw new ArgumentOutOfRangeException("delta");
            if(!State.Playing||delta==0)return;
            State.Blocked=false;
            bool initialInterior=Definition.StartDistance>0&&State.Time==0&&State.From==0&&State.To==1&&State.Distance==Definition.StartDistance;
            State.Time+=delta;
            // phase offsets may cover days; skip whole periods without spending a frame per lap
            if(!emit&&Definition.Mode!="once")
            {
                if(initialInterior){double initialWait=Math.Min(delta,State.Wait);State.Wait-=initialWait;delta-=initialWait;}
                double period=0;for(int i=1;i<Definition.Points.Count;i++)period+=MovingPlatformPlan.Distance(Definition.Points[i-1].Position,Definition.Points[i].Position)/Definition.Speed;
                if(Definition.Mode=="loop")period+=MovingPlatformPlan.Distance(Definition.Points[Definition.Points.Count-1].Position,Definition.Points[0].Position)/Definition.Speed;
                else period*=2;
                for(int i=0;i<Definition.Points.Count;i++)period+=Definition.Points[i].Wait*(Definition.Mode=="pingPong"&&i>0&&i<Definition.Points.Count-1?2:1);
                if(period>0)delta%=period;
            }
            int transitions=0;
            while(delta>1e-12&&State.Playing)
            {
                if(++transitions>200000)throw new InvalidOperationException("Platform route transition budget exceeded");
                double wait=Math.Min(State.Wait,delta);State.Wait-=wait;delta-=wait;if(delta<=1e-12)break;
                double length=MovingPlatformPlan.Distance(Definition.Points[State.From].Position,Definition.Points[State.To].Position);
                double seconds=(length-State.Distance)/Definition.Speed;
                if(delta<seconds){State.Distance+=delta*Definition.Speed;break;}
                delta-=Math.Max(0,seconds);int arrived=State.To;State.From=arrived;State.Distance=0;State.Wait=Definition.Points[arrived].Wait;
                int next=arrived+State.Direction;
                if(next<0||next>=Definition.Points.Count)
                {
                    if(Definition.Mode=="once"){State.To=arrived;State.Playing=false;}
                    else if(Definition.Mode=="loop")next=next<0?Definition.Points.Count-1:0;
                    else{State.Direction=-State.Direction;next=arrived+State.Direction;}
                }
                if(State.Playing)State.To=next;
                if(emit&&Arrived!=null)Arrived(arrived);
            }
        }
        internal void Validate(PlatformMotionState state)
        {
            if(state==null||state.From<0||state.From>=Definition.Points.Count||state.To<0||state.To>=Definition.Points.Count||Math.Abs(state.Direction)!=1||!Finite(state.Distance)||!Finite(state.Wait)||!Finite(state.Time)||state.Distance<0||state.Distance>MovingPlatformPlan.Distance(Definition.Points[state.From].Position,Definition.Points[state.To].Position)+1e-7||state.Wait<0||state.Wait>3600||state.Time<0)throw new InvalidDataException("Invalid platform motion state");
            int expected=state.From+state.Direction;
            if(Definition.Mode=="loop")expected=(expected+Definition.Points.Count)%Definition.Points.Count;
            bool endpoint=state.From==state.To&&Definition.Mode=="once"&&(state.From==0||state.From==Definition.Points.Count-1);
            if(!endpoint&&state.To!=expected)throw new InvalidDataException("Platform state references an unauthored route segment");
        }
        private static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
        internal void Restore(PlatformMotionState state){Validate(state);State=state.Copy();}
    }
}
