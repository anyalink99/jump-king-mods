using System.Collections.Generic;
using System.Diagnostics;

namespace SubframeCharge
{
    // a separate cursor: world/menu publication mustn't consume player edges
    internal sealed class SimulationInput
    {
        private struct Change { internal long At; internal int Held, Pressed; }
        private readonly Queue<Change> changes = new Queue<Change>();
        private int held, recorded, direction, blocked;
        private long directionAt;
        internal void Record(int state,int pressed,long timestamp)
        {
            state &= 28; pressed &= 28;
            blocked &= state; state &= ~blocked; pressed &= ~blocked;
            if(state==recorded && pressed==0) return;
            if(changes.Count==256) { Reset(); blocked=state; return; }
            recorded=state;
            changes.Enqueue(new Change { At=timestamp,Held=state,Pressed=pressed });
        }
        internal void Read(long timestamp,out int state,out int pressed)
        {
            pressed=0;
            while(changes.Count!=0 && changes.Peek().At<=timestamp)
            {
                var next=changes.Dequeue(); held=next.Held;
                if(timestamp-next.At<=Stopwatch.Frequency/20) pressed|=next.Pressed;
                if((held & 12)!=0) { direction=held & 12; directionAt=next.At; }
            }
            state=held;
            if((state & 12)==0 && timestamp-directionAt<=Stopwatch.Frequency/20) state|=pressed & direction & 12;
        }
        internal void Reset() { changes.Clear(); held=recorded=direction=blocked=0; directionAt=0; }
    }
}
