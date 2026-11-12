using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JKRuntime.Compatibility;
using JKRuntime.World;

namespace JKRuntime
{
    internal static class ReliabilityTests
    {
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Main()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"map-identity-fixture");
            Directory.CreateDirectory(root);
            try{
                File.WriteAllBytes(Path.Combine(root,"level.xnb"),new byte[]{1,2,3});
                string replay=MapIdentity.ReplayV1(root),current=MapIdentity.GameplayV2(root);
                using(var sha=SHA256.Create()){
                    byte[] expected=Encoding.UTF8.GetBytes("level.xnb\0").Concat(sha.ComputeHash(new byte[]{1,2,3})).ToArray();
                    Check(replay==BitConverter.ToString(sha.ComputeHash(expected)).Replace("-","").ToLowerInvariant(),"Replay v1 bytes changed");
                }
                foreach(string path in new[]{"level+.xnb","sizes.xnb","mapping_settings.xml","screens/mega-mapping-expansion/materials.xml","jk-runtime/policy.xml","ending/custom_main_ending.xml","jk-runtime/modules/test/test.jkmod"}){
                    string file=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,path.EndsWith(".xml")?"<Root value='first'/>":"first");
                    string added=MapIdentity.GameplayV2(root);Check(added!=current,"Added gameplay input ignored: "+path);
                    File.WriteAllText(file,path.EndsWith(".xml")?"<Root value='other'/>":"other");Check(MapIdentity.GameplayV2(root)!=added,"Changed gameplay input ignored: "+path);
                    File.Delete(file);Check(MapIdentity.GameplayV2(root)==current,"Removed input did not restore identity: "+path);
                }
                File.WriteAllText(Path.Combine(root,"unrelated.txt"),"notes");Check(MapIdentity.GameplayV2(root)==current,"Unrelated notes affect gameplay identity");
                File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),"<MappingSettings><Include src='fragment.xml'/></MappingSettings>");
                File.WriteAllText(Path.Combine(root,"fragment.xml"),"<MappingFragment value='first'/>");string included=MapIdentity.GameplayV2(root);
                File.WriteAllText(Path.Combine(root,"fragment.xml"),"<MappingFragment value='other'/>");Check(MapIdentity.GameplayV2(root)!=included,"Included gameplay rules were ignored");
                var registry=new WorldRegistry();using(registry.Block("fixture.adapter","unreviewed provider")){
                    bool blocked=false;try{registry.Capture();}catch(NotSupportedException){blocked=true;}Check(blocked,"Incomplete world state was captured as supported");
                }
                Check(registry.Capture().Length==1,"Releasing adapter blocker did not restore world snapshots");
                using(WorldRegistry.Shared.Block("fixture.authority","unreviewed provider")){
                    bool blocked=false;try{WorldControl.Begin("fixture.owner","world",1,WorldRole.Host);}catch(NotSupportedException){blocked=true;}Check(blocked&&WorldControl.Owner==null,"Unknown world adapter acquired shared authority");
                }
                for(int i=0;i<140;i++)CompatibilityEvidence.Record("fixture."+i,"provider","shape-v1",CompatibilityOutcome.Unknown,"metadata only","refuse replication");
                Check(CompatibilityEvidence.Inspect().Length==128,"Compatibility observations are not bounded");
                CompatibilityEvidence.Record("fixture.139","provider","shape-v1",CompatibilityOutcome.Unsupported,"changed member","native fallback");
                var rows=CompatibilityEvidence.Inspect();Check(rows.Length==128&&rows.Last().Outcome==CompatibilityOutcome.Unsupported,"Latest scoped outcome not retained");
                rows[0]=null;Check(CompatibilityEvidence.Inspect()[0]!=null,"Diagnostic caller mutated retained observations");
                bool rejected=false;
                try{CompatibilityEvidence.RequireReviewed("fixture.private",typeof(ReliabilityTests).Assembly,"v1",new[]{new string('0',64)},"refuse");}catch(NotSupportedException){rejected=true;}
                Check(rejected&&CompatibilityEvidence.Inspect().Last().Outcome==CompatibilityOutcome.Unknown,"Unreviewed private execution was accepted");
                rejected=false;try{PackageHost.ValidateDependencyImage(typeof(ReliabilityTests).Assembly,new string('0',64));}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Loaded identity bypassed private dependency integrity");
                foreach(string kind in new[]{"shared-api","legacy"})PackageHost.ValidateLoadedDependency(typeof(ReliabilityTests).Assembly,kind,new string('0',64));
                rejected=false;try{PackageHost.ValidateLoadedDependency(typeof(ReliabilityTests).Assembly,"private",new string('0',64));}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Private dependency silently acquired shared API semantics");
                using(var sha=SHA256.Create())PackageHost.ValidateDependencyImage(typeof(ReliabilityTests).Assembly,BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(typeof(ReliabilityTests).Assembly.Location))).Replace("-",""));
                Check(CompatibilityEvidence.Identity(typeof(ReliabilityTests).Assembly).Contains("mvid="),"Loaded binary identity missing");
            }finally{Directory.Delete(root,true);}
            Console.WriteLine("[OK] Versioned map identities, unchanged replay digest, bounded immutable compatibility evidence");
        }
    }
}
