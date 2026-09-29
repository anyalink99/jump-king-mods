using System;
using BehaviorTree;
using JKRuntime.UI;

namespace JKRuntime
{
    internal static class MenuTreeSessionTests
    {
        private sealed class Node : IBTnode
        {
            internal int Starts, Calls;
            internal BTresult Result=BTresult.Running;
            internal Action Work;
            protected override void OnNewRun() { Starts++; }
            protected override BTresult MyRun(TickData data) { Calls++; if(Work!=null)Work(); return Result; }
        }
        private static void Check(bool value,string name) { if(!value)throw new Exception(name); }
        public static void Main()
        {
            foreach(var result in new[]{BTresult.Success,BTresult.Failure})
            foreach(bool fast in new[]{false,true})
            {
                var node=new Node(); var session=new MenuTreeSession(new BTmanager(node));
                session.Pump(.004f); Check(node.Calls==0,"Cannot start a native lifecycle from the presentation pump");
                session.RunNative(.016f,true); node.Result=result; session.Pump(.004f);
                for(int i=0;i<20;i++) { session.Pump(.004f); Check(session.RunNative(.016f,fast)==result,"Terminal result lost across clock handoff"); }
                Check(node.Starts==1 && node.Calls==2,"A terminal native tree was restarted");
            }
            var running=new Node(); var clock=new MenuTreeSession(new BTmanager(running));
            clock.RunNative(.016f,false); clock.Pump(.004f);
            clock.RunNative(.016f,false); Check(running.Calls==2,"Mode disable must first consume the pending result");
            clock.RunNative(.016f,false); Check(running.Calls==3,"Native clock must resume on its next tick");
            var bad=new Node(); var failed=new MenuTreeSession(new BTmanager(bad));
            bad.Work=()=>{throw new Exception("fixture");};
            try { failed.RunNative(.016f,false); } catch(Exception) { }
            bool blocked=false; try { failed.RunNative(.016f,false); } catch(InvalidOperationException) { blocked=true; }
            Check(blocked && bad.Calls==1,"Failed side effects must not be retried by another clock");
            var replacement=new MenuTreeSession(new BTmanager(new Node()));
            Check(replacement.RunNative(.016f,false)==BTresult.Running,"New native lifetimes must be independent");
            Console.WriteLine("[OK] Menu tree ownership: terminal handoff, mode disable, failed calls and new lifetimes");
        }
    }
}
