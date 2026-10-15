using System;
using System.Collections.Generic;

namespace Replays
{
    /// <summary>optional reflection-friendly v1 integration. Calls except Saved callbacks
    /// are game-thread only. saved is invoked on the writer thread after atomic publication</summary>
    public static class VerificationBridge
    {
        public static int ApiVersion { get { return 1; } }
        public static bool IsPlayback { get { return ReplayRuntime.ViewerActive; } }
        public static event Action<string, string, string, double, double> Saved;
        private static string binding;
        private static readonly Dictionary<string,string> associations = new Dictionary<string,string>();
        private static readonly object gate = new object();
        public static void BindRun(string runSession)
        { lock(gate) binding = runSession; ReplayRuntime.BindVerification(); }
        internal static void Associate(ReplayData data, string source = null)
        {
            if(data==null) return;
            lock(gate) { string id; if(source!=null && associations.TryGetValue(source,out id)) associations[data.Header.Id]=id;
                else if(binding!=null) associations[data.Header.Id]=binding; }
        }
        internal static void Forget(ReplayData data) { if(data!=null)lock(gate)associations.Remove(data.Header.Id); }
        internal static void Published(ReplaySummary value)
        {
            string id; lock(gate) { if(!associations.TryGetValue(value.Header.Id,out id)) return; associations.Remove(value.Header.Id); }
            var handlers=Saved; if(handlers==null) return;
            foreach(Action<string,string,string,double,double> h in handlers.GetInvocationList()) try
            { double start=ReplayTiming.InitialSeconds(value.Header); h(id,value.Header.Id,value.FilePath,start,start+value.Duration.TotalSeconds); }
            catch(Exception e) { Console.WriteLine("[Replays] Verification listener failed: "+e.Message); }
        }
        public static bool Watch(string id) { return ReplayRuntime.RequestWatch(id); }
    }
}
