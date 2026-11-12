using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;

namespace JKRuntime.Compatibility
{
    /// <summary>Adapter outcomes. Unknown and absent are never evidence of support.</summary>
    public enum CompatibilityOutcome { Absent, Supported, Unsupported, Failed, Unknown }

    /// <summary>One adapter's latest observation, with an explicit fallback and evidence scope.</summary>
    public sealed class CompatibilityRecord
    {
        public string Feature { get; private set; }
        public string Provider { get; private set; }
        public string Contract { get; private set; }
        public CompatibilityOutcome Outcome { get; private set; }
        public string Evidence { get; private set; }
        public string Fallback { get; private set; }
        public DateTime ObservedUtc { get; private set; }
        internal CompatibilityRecord(string feature,string provider,string contract,CompatibilityOutcome outcome,string evidence,string fallback)
        { Feature=feature;Provider=provider;Contract=contract;Outcome=outcome;Evidence=evidence;Fallback=fallback;ObservedUtc=DateTime.UtcNow; }
    }

    /// <summary>Bounded local adapter observations. Recording a claim does not activate or approve an adapter.</summary>
    public static class CompatibilityEvidence
    {
        public const int SchemaVersion=1;
        private static readonly object sync=new object();
        private static readonly List<CompatibilityRecord> records=new List<CompatibilityRecord>();
        private static readonly Dictionary<Assembly,string> fingerprints=new Dictionary<Assembly,string>();
        /// <summary>Identify loaded metadata without executing provider code or reading mutable DLL bytes.</summary>
        public static string Identity(Assembly assembly)
        { return assembly==null?"absent":assembly.FullName+"; mvid="+assembly.ManifestModule.ModuleVersionId.ToString("D"); }
        /// <summary>Gate private execution adapters on reviewed bytes. Hash once per loaded assembly, outside frame work.</summary>
        public static void RequireReviewed(string feature,Assembly provider,string contract,string[] reviewedHashes,string fallback)
        {
            string digest=null;
            if(provider!=null)lock(sync){
                if(!fingerprints.TryGetValue(provider,out digest)){
                    try{
                        if(string.IsNullOrEmpty(provider.Location))throw new IOException("No file-backed provider identity");
                        byte[] bytes=File.ReadAllBytes(provider.Location);
                        var metadata=Assembly.ReflectionOnlyLoadFrom(provider.Location);
                        if(metadata.ManifestModule.ModuleVersionId!=provider.ManifestModule.ModuleVersionId)throw new IOException("Provider file changed after loading");
                        using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");
                    }catch(Exception error){digest="unknown: "+error.GetBaseException().Message;}
                    if(fingerprints.Count<128)fingerprints[provider]=digest;
                }
            }
            string name=provider==null?"absent":provider.GetName().Name;
            bool supported=digest!=null&&reviewedHashes!=null&&reviewedHashes.Contains(digest,StringComparer.OrdinalIgnoreCase);
            Record(feature,name,contract,supported?CompatibilityOutcome.Supported:provider==null?CompatibilityOutcome.Absent:CompatibilityOutcome.Unknown,
                Identity(provider)+"; sha256="+(digest??"unavailable"),fallback);
            if(!supported)throw new NotSupportedException(feature+": unreviewed "+name+" ("+digest+"). "+fallback);
        }
        /// <summary>Replace this feature/provider observation. Old unrelated observations expire after 128 records.</summary>
        public static void Record(string feature,string provider,string contract,CompatibilityOutcome outcome,string evidence,string fallback)
        {
            if(string.IsNullOrWhiteSpace(feature)||string.IsNullOrWhiteSpace(provider)||string.IsNullOrWhiteSpace(contract))throw new ArgumentException("Feature, provider and contract are required");
            if(!Enum.IsDefined(typeof(CompatibilityOutcome),outcome))throw new ArgumentOutOfRangeException("outcome");
            if(new[]{feature,provider,contract,evidence,fallback}.Any(s=>s!=null&&s.Length>4096))throw new ArgumentException("Compatibility text exceeds 4096 characters");
            lock(sync){
                records.RemoveAll(r=>r.Feature==feature&&r.Provider==provider);
                if(records.Count==128)records.RemoveAt(0);
                records.Add(new CompatibilityRecord(feature,provider,contract,outcome,evidence??"",fallback??""));
            }
        }
        /// <summary>Return an immutable snapshot for diagnostics or a local evidence receipt.</summary>
        public static CompatibilityRecord[] Inspect(){lock(sync)return records.ToArray();}
    }
}
