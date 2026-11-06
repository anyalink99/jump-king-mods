using System;
using System.Collections.Generic;
using System.Linq;

namespace JKRuntime.Presentation
{
    /// <summary>Optional cosmetic actor factory and bounded serialized event history. No dependency on a skin package.</summary>
    public static class CosmeticPlayback
    {
        private static Func<bool,ICosmeticActor> factory;
        private static readonly Queue<Tuple<long,string>> history=new Queue<Tuple<long,string>>();
        private static long sequence;
        private static int playback;
        public static long Sequence {get{return sequence;}}
        public static bool Playing {get{return playback>0;}}
        public static bool Available {get{return factory!=null;}}
        public static IDisposable Register(string owner,Func<bool,ICosmeticActor> create)
        {
            RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(owner);if(create==null)throw new ArgumentNullException("create");
            if(factory!=null)throw new InvalidOperationException("A cosmetic actor provider is already registered");factory=create;
            return new ActionLease(()=>{if(factory==create)factory=null;});
        }
        public static ICosmeticActor Create(bool ghost) {RuntimeApi.Kernel.CheckThread();return factory==null?null:factory(ghost);}
        public static IDisposable Playback() {RuntimeApi.Kernel.CheckThread();playback++;return new ActionLease(()=>playback--);}
        public static void Record(string data)
        {
            RuntimeApi.Kernel.CheckThread();if(Playing)return;
            if(data==null||System.Text.Encoding.UTF8.GetByteCount(data)>4096)throw new ArgumentException("Cosmetic payload exceeds 4096 bytes");
            history.Enqueue(Tuple.Create(++sequence,data));while(history.Count>256)history.Dequeue();
        }
        public static string[] ReadEvents(long after) {RuntimeApi.Kernel.CheckThread();return history.Where(e=>e.Item1>after).Select(e=>e.Item2).ToArray();}
    }
}
