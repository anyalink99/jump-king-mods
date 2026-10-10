using System;
using System.Collections.Generic;

namespace MultiplayerExpansion
{
    internal sealed class PendingImpact
    {
        internal ulong Source, Epoch, Map, RulesEpoch, Revision, Transition;
        internal double Received, Age;
        internal ContactImpact Hit;
        internal bool Matches(InteractionFrame source,ulong targetTransition,ulong map,ulong rulesEpoch,ulong revision)
        {
            return source.Epoch==Epoch && source.Transition==Transition && source.Presence==PlayerPresence.Playing
                && Map==map && RulesEpoch==rulesEpoch && Revision==revision && Hit.TargetTransition==targetTransition;
        }
    }
    internal sealed class ImpactInbox
    {
        private readonly List<PendingImpact> pending=new List<PendingImpact>(64);
        internal int Count { get { return pending.Count; } }
        internal long Dropped;
        internal void Clear() {pending.Clear();}
        internal void Forget(ulong source) {pending.RemoveAll(x=>x.Source==source);}
        internal void Add(ulong source,InteractionFrame frame,double now,double oneWay)
        {
            if(!frame.Physics || !frame.Active || frame.Presence!=PlayerPresence.Playing) return;
            foreach(var hit in frame.Impacts)
            {
                if(frame.Time-hit.Time+oneWay>.25) continue;
                bool exists=false;
                foreach(var item in pending) if(item.Source==source && item.Epoch==frame.Epoch && item.Hit.Id==hit.Id) {exists=true;break;}
                if(exists) continue;
                if(pending.Count>=64) {Dropped++;continue;}
                pending.Add(new PendingImpact{Source=source,Epoch=frame.Epoch,Map=frame.Map,RulesEpoch=frame.RulesEpoch,Revision=frame.Revision,Transition=frame.Transition,Received=now,Age=frame.Time-hit.Time+oneWay,Hit=hit});
            }
        }
        internal void Drain(double now,Action<PendingImpact,double> apply)
        {
            pending.Sort((a,b)=>a.Source==b.Source ? a.Hit.Id.CompareTo(b.Hit.Id) : a.Source.CompareTo(b.Source));
            foreach(var item in pending) {double age=item.Age+now-item.Received;if(age<=.25) apply(item,age);else Dropped++;}
            pending.Clear();
        }
    }
}
