using System;
using System.Collections.Generic;
using JKRuntime;

namespace MegaMappingExpansion.Construction
{
    // reload candidates share dispatch hooks; only the active scene receives callbacks
    internal static class PreparedNativeHooks
    {
        private sealed class Entry {internal OwnedPatches Hooks;internal int Users;}
        private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private sealed class Lease : IDisposable
        {
            private string key;
            internal Lease(string value){key=value;}
            public void Dispose()
            {
                if(key==null)return;var entry=entries[key];
                if(entry.Users==1){entry.Hooks.Dispose();entries.Remove(key);}else entry.Users--;
                key=null;
            }
        }
        internal static void Acquire(string key,RuntimeScope scope,Action<OwnedPatches> install)
        {
            Entry entry;
            if(!entries.TryGetValue(key,out entry))
            {
                entry=new Entry{Hooks=new OwnedPatches(key)};
                try{install(entry.Hooks);}catch{entry.Hooks.Dispose();throw;}
                entries.Add(key,entry);
            }
            entry.Users++;var lease=new Lease(key);
            try{scope.Own(lease);}catch{lease.Dispose();throw;}
        }
    }
}
