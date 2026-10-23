using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void SceneCacheChecks(string scratch)
        {
            string root=Path.Combine(scratch,"cache-map"),store=Path.Combine(scratch,"scene-cache");Directory.CreateDirectory(root);
            string entry=Path.Combine(root,"mapping_settings.xml"),fragment=Path.Combine(root,"part.xml");
            File.WriteAllText(entry,"<MappingSettings version='1'><Include src='part.xml'/></MappingSettings>");
            Action<string> edit=id=>File.WriteAllText(fragment,"<MappingFragment version='1'><Components><Component id='"+id+"' kind='material'/></Components></MappingFragment>");
            edit("first");
            // even a broken old map-local cache must have no say in loading
            string old=Path.Combine(root,"props/mega-mapping-expansion/scene.mmgfx");Directory.CreateDirectory(Path.GetDirectoryName(old));File.WriteAllText(old,"obsolete");
            var before=Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(p=>p).ToArray();
            string cache=ConstructionSceneFile.CacheFile(root,store);
            Require(ConstructionSceneFile.Read(root,false,store).Components[0].Id=="first"&&File.Exists(cache),"First load prepares XML and writes an external cache");
            DateTime stamp=new DateTime(2020,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(cache,stamp);
            Require(ConstructionSceneFile.Read(root,false,store).Components[0].Id=="first"&&File.GetLastWriteTimeUtc(cache)==stamp,"An unchanged scene reuses its cache without rewriting it");
            edit("other");
            Require(ConstructionSceneFile.Read(root,false,store).Components[0].Id=="other","An included XML edit automatically replaces the cached scene");
            byte[] good=File.ReadAllBytes(cache);
            foreach(int length in new[]{0,5,40,good.Length-1})
            {
                File.WriteAllBytes(cache,good.Take(length).ToArray());
                Require(ConstructionSceneFile.Read(root,false,store).Components[0].Id=="other","Truncated caches recover from source: "+length);
            }
            foreach(int offset in new[]{0,8,40,good.Length-1})
            {
                byte[] bad=(byte[])good.Clone();bad[offset]^=1;File.WriteAllBytes(cache,bad);
                Require(ConstructionSceneFile.Read(root,false,store).Components[0].Id=="other","Format, source/engine identity and payload damage recover: "+offset);
                Require(File.ReadAllBytes(cache).SequenceEqual(good),"Recovered cache is complete");
            }
            File.SetLastWriteTimeUtc(cache,stamp);ConstructionSceneFile.Read(root,true,store);
            Require(File.GetLastWriteTimeUtc(cache)!=stamp,"Inspector reload refreshes the cache from XML");
            using(var held=new FileStream(cache,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                Require(ConstructionSceneFile.Read(root,false,store).Components[0].Id=="other","A locked cache cannot prevent scene loading");
            string blocked=Path.Combine(scratch,"blocked-cache");File.WriteAllText(blocked,"not a directory");
            Require(ConstructionSceneFile.Read(root,false,blocked).Components[0].Id=="other","Unavailable cache storage falls back to XML");
            File.WriteAllText(fragment,"<broken");bool rejected=false;
            try{ConstructionSceneFile.Read(root,false,store);}catch(System.Xml.XmlException){rejected=true;}
            Require(rejected,"Malformed XML cannot hide behind a previously valid cache");
            edit("other");File.WriteAllText(fragment,File.ReadAllText(fragment).Replace("kind='material'","kind='unknown'"));rejected=false;
            try{ConstructionSceneFile.Read(root,false,store);}catch(Exception){rejected=true;}
            Require(rejected,"Semantic source errors remain errors");
            edit("other");File.Delete(cache);
            Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>{if(ConstructionSceneFile.Read(root,false,store).Components[0].Id!="other")throw new Exception("Concurrent read differs");})).ToArray());
            Require(File.ReadAllBytes(cache).SequenceEqual(good)&&!Directory.GetFiles(store,"*.tmp-*").Any(),"Concurrent writers leave one complete cache and no temporary files");
            Require(Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(p=>p).SequenceEqual(before)&&File.ReadAllText(old)=="obsolete","Scene loading never writes into the map");
            File.Delete(entry);Require(ConstructionSceneFile.Read(root,false,store)==null,"A cache cannot resurrect a removed scene entry");
            string prune=Path.Combine(scratch,"cache-eviction");Directory.CreateDirectory(prune);
            for(int i=0;i<130;i++)File.WriteAllText(Path.Combine(prune,i.ToString("x64")+".mmgfx"),"cache");
            string current=Path.Combine(prune,new string('0',64)+".mmgfx");File.SetLastWriteTimeUtc(current,stamp);
            string foreign=Path.Combine(prune,"notes.mmgfx");File.WriteAllText(foreign,"keep");
            ConstructionSceneFile.TrimCache(prune,current);
            Require(File.Exists(current)&&File.Exists(foreign)&&Directory.GetFiles(prune).Length==129,"Cache eviction is bounded and preserves the current entry and unrelated files");
            string large=Path.Combine(prune,new string('f',64)+".mmgfx");using(var stream=File.Create(large))stream.SetLength(65L*1024*1024);
            ConstructionSceneFile.TrimCache(prune,current);Require(!File.Exists(large),"Cache eviction enforces its disk budget");
        }
    }
}
