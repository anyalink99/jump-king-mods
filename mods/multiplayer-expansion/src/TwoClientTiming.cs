using System;
namespace MultiplayerExpansion
{
    internal static class TwoClientTiming
    {
        private static IDisposable suspension;
        private static System.Reflection.PropertyInfo clockOwner, highRefresh;
        internal static string Status { get { return suspension==null ? "native" : "SFC-inputs="+clockOwner.GetValue(null,null)+"/240="+highRefresh.GetValue(null,null); } }
        internal static void Set(bool active)
        {
            if(!active) { if(suspension!=null) suspension.Dispose(); suspension=null; return; }
            if(suspension!=null) return;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var entry=assembly.GetType("SubframeCharge.ModEntry",false); if(entry==null) continue;
                var method=entry.GetMethod("SuspendPerformanceFeatures");
                if(method==null) throw new InvalidOperationException("Two-client mode needs Subframe Charge 0.27.2 or newer.");
                clockOwner=entry.GetProperty("OwnsPresentationClock");highRefresh=entry.GetProperty("HasSubframePresentation");
                suspension=(IDisposable)method.Invoke(null,null); return;
            }
        }
    }
}
