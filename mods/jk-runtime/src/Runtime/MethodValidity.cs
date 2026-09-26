using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Reflection;

namespace JKRuntime
{
    /// <summary>A snapshot of one shared-Harmony method generation. Reacquire after invalidation; validity is not semantic correctness.</summary>
    public sealed class MethodValidityLease : IDisposable
    {
        internal MethodValidity.Entry Entry;
        private readonly long generation;
        private readonly string refusal;
        internal MethodValidityLease(MethodValidity.Entry entry, string reason) { Entry=entry; generation=Interlocked.Read(ref entry.Generation); refusal=reason; }
        public long Generation { get { return generation; } }
        public bool IsValid { get { var e=Entry; return e!=null && refusal==null && System.Threading.Interlocked.Read(ref e.Generation)==generation; } }
        public string Reason { get { return Entry==null?"Disposed":refusal!=null?refusal:!IsValid?"Shared Harmony patch graph changed":null; } }
        public void Dispose() { RuntimeApi.Kernel.CheckThread(); if(Entry==null)return; MethodValidity.Release(Entry); Entry=null; }
    }

    /// <summary>Mutation-driven method leases. Covers the loaded shared Harmony 2 engine, not independent detours or semantic changes in dependencies.</summary>
    public static class MethodValidity
    {
        internal sealed class Entry { internal MethodBase Method; internal long Generation; internal int References; }
        private static readonly object gate=new object();
        private static readonly ConcurrentDictionary<MethodBase,Entry> entries=new ConcurrentDictionary<MethodBase,Entry>();
        private static OwnedPatches hooks;
        private static long nextGeneration,mutationEpoch;
        /// <summary>Capture current method identity and generation. Supply only reviewed patch owners; other existing patches refuse the lease. Call during preparation.</summary>
        public static MethodValidityLease Watch(MethodBase method, params string[] reviewedOwners)
        {
            RuntimeApi.Kernel.CheckThread();
            if(method==null || method.ContainsGenericParameters)throw new ArgumentException("Concrete method required", "method");
            lock(gate)
            {
                if(hooks==null)
                {
                    var engine=OwnedPatches.SharedEngine();
                    var update=engine.GetType("HarmonyLib.PatchFunctions",true).GetMethod("UpdateWrapper",OwnedPatches.Members);
                    if(update==null || update.GetParameters().Length==0 || update.GetParameters()[0].ParameterType!=typeof(MethodBase))throw new NotSupportedException("Harmony mutation observer unavailable");
                    var pending=new OwnedPatches("jkruntime.method-validity");
                    try { pending.Add(update,prefix:typeof(MethodValidity).GetMethod("Changing",OwnedPatches.Members)); hooks=pending; }
                    catch { pending.Dispose(); throw; }
                }
                try
                {
                    long epoch=Interlocked.Read(ref mutationEpoch);
                    bool accepted=true;
                    var engine=OwnedPatches.SharedEngine();
                    var info=engine.GetType("HarmonyLib.Harmony",true).GetMethod("GetPatchInfo",new[]{typeof(MethodBase)}).Invoke(null,new object[]{method});
                    if(info!=null) foreach(string kind in new[]{"Prefixes","Postfixes","Transpilers","Finalizers"})
                        foreach(var patch in (IEnumerable)info.GetType().GetField(kind).GetValue(info))
                            if(reviewedOwners==null || Array.IndexOf(reviewedOwners,(string)patch.GetType().GetField("owner").GetValue(patch))<0)accepted=false;
                    Entry entry;
                    if(!entries.TryGetValue(method,out entry)) { entry=new Entry {Method=method,Generation=Interlocked.Increment(ref nextGeneration)}; entries.TryAdd(method,entry); }
                    entry.References++; return new MethodValidityLease(entry,!accepted?"Unreviewed patches already present":epoch!=Interlocked.Read(ref mutationEpoch)?"Patch graph changed during acquisition":null);
                }
                catch { if(entries.Count==0) { hooks.Dispose(); hooks=null; } throw; }
            }
        }
        private static void Changing(MethodBase __0)
        {
            // Harmony may invoke this while holding its own patch lock on a
            // foreign thread. Never take the lifecycle lock in that direction.
            Interlocked.Increment(ref mutationEpoch);
            Entry entry;
            if(entries.TryGetValue(__0,out entry))Interlocked.Exchange(ref entry.Generation,Interlocked.Increment(ref nextGeneration));
        }
        internal static void Release(Entry entry)
        {
            lock(gate)
            {
                if(entry.References==1 && entries.Count==1) { hooks.Dispose(); hooks=null; }
                if(--entry.References==0) { Entry removed; entries.TryRemove(entry.Method,out removed); }
            }
        }
    }
}
