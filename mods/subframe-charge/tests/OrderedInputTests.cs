using System;
using System.Collections.Generic;
using System.Diagnostics;
using JKRuntime.Input;
using SubframeCharge;

internal static partial class PerformanceTests
{
    private static void OrderedInputTests()
    {
        foreach(int first in new[]{1,2,4,8})
        {
            int opposite=first==1 ? 2 : first==2 ? 1 : first==4 ? 8 : 4;
            var latch=new InputLatch(); latch.Observe(first); latch.Observe(0); latch.Observe(opposite); latch.Commit();
            Check(latch.Resolve(latch.Held,latch.NativeEdges)==opposite,"An obsolete opposite tap can't cancel a held direction");
            latch=new InputLatch(); latch.Observe(first); latch.Observe(0); latch.Observe(opposite); latch.Observe(0); latch.Commit();
            Check(latch.Resolve(latch.Held,latch.NativeEdges)==opposite,"Completed direction taps retain their order");
            latch=new InputLatch(); latch.Observe(first|opposite); latch.Commit();
            Check(latch.Resolve(latch.Held,latch.NativeEdges)==(first|opposite),"Genuinely simultaneous directions keep native cancellation");
            latch.Observe(0); latch.Commit();
            Check(latch.Resolve(latch.Held,latch.NativeEdges)==0,"Consumed axes can't leak into the following frame");
            latch=new InputLatch(); latch.Observe(first|opposite); latch.Observe(opposite); latch.Observe(0); latch.Commit();
            Check(latch.Resolve(latch.Held,latch.NativeEdges)==opposite,"Release ordering retains the final direction");
            latch=new InputLatch(); latch.Observe(first); latch.Observe(0); latch.Observe(opposite);
            Check(latch.MenuFrame==opposite,"Menus use the same directional arbitration");
        }
        var actions=new int[11][][];
        var lostHistory=new InputLatch(); lostHistory.Observe(4|16|64); lostHistory.Commit(); lostHistory.Observe(8|32);
        lostHistory.ClearHistory(); lostHistory.Observe(0,false); lostHistory.Commit();
        Check(lostHistory.Resolve(0,lostHistory.NativeEdges)==0 && lostHistory.MenuFrame==0,"Lost physical history clears gameplay and menu pulses together");
        for(int i=0;i<actions.Length;i++) actions[i]=new int[0][];
        using(var worker=new KeyboardActionEdges(actions,new IntPtr(1),k=>0,()=>new IntPtr(1),false))
        {
            long now=Stopwatch.GetTimestamp(), step=Math.Max(1,Stopwatch.Frequency/10000);
            worker.Publish(0,true,now);
            worker.Publish(4,true,now+=step); worker.Publish(0,true,now+=step);
            worker.Publish(8|256,true,now+=step); worker.Publish(0,true,now+=step);
            int latest;
            Check(worker.TryReadHeld(now-step,out latest) && latest==0,"A concurrent newer physical sample must not restore a stale native hold");
            var events=new List<ActionInputTransition>(); worker.ReadTransitions(now,events);
            Check(events.Count==4 && events[0].Pressed==4 && events[1].Released==4 && events[2].Pressed==(8|256),"Physical transitions preserve order, releases and independent actions");
            var latch=new InputLatch();
            foreach(var edge in events) latch.Record(edge.Held,edge.Pressed,edge.Timestamp);
            latch.Commit(); Check(latch.Resolve(0,latch.NativeEdges)==(8|256),"Ordered keyboard delivery preserves a tap without cancelling direction");
            Check(worker.Take(now)==0,"Legacy and ordered consumers can't replay the same presses");
            events.Clear(); worker.ReadTransitions(now,events); Check(events.Count==0,"An ordered batch is consumed once");
            for(int i=0;i<260;i++) worker.Publish((i&1)==0 ? 4 : 0,true,now+=1);
            events.Clear(); worker.ReadTransitions(now,events);
            Check(events.Count<256,"Overflow drops ambiguous history without growing a queue");
            worker.Clear(); worker.Publish(0,true,++now); worker.Publish(8,true,++now);
            events.Clear(); worker.ReadTransitions(now+Stopwatch.Frequency,events);
            Check(events.Count==0,"Expired transitions aren't replayed after a stall");
        }
    }
}
