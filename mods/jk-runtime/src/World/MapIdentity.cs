using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Xml;

namespace JKRuntime.World
{
    /// <summary>Versioned map-file identities. Read during preparation, never on a frame callback.</summary>
    public static class MapIdentity
    {
        private static readonly string[] replayInputs={"level.xnb","slopes.xnb","level+.xnb","sizes.xnb","props/mega-mapping-expansion/map.xml",
            "mapping_settings.xml","screens/mega-mapping-expansion/moving-platforms.xml","screens/mega-mapping-expansion/transitions.xml","screens/mega-mapping-expansion/screens.xml",
            "props/mega-mapping-expansion/prop_settings.xml","props/mega-mapping-expansion/props.xml","particles/mega-mapping-expansion/particles.xml","gui/mega-mapping-expansion/gui.xml"};
        /// <summary>Frozen original replay digest, retained for old recordings and verifier links.</summary>
        public static string ReplayV1(string root){return Hash(root,replayInputs,false);}
        /// <summary>Collision, topology, materials, endings and declared map policy; separate from recorder-specific eligibility.</summary>
        public static string GameplayV2(string root)
        {
            root=Path.GetFullPath(root);
            string[] files=replayInputs.Concat(new[]{"level_settings.xml","gui/location_settings.xml","screens/mega-mapping-expansion/materials.xml","jk-runtime/policy.xml","jk-runtime/mechanics.xml","camera.xml"}).ToArray();
            // map-owned rules may live beside their declared modules; additions and removals matter too
            foreach(string directory in new[]{"jk-runtime/modules","ending"}){
                string full=Path.Combine(root,directory);
                if(Directory.Exists(full))files=files.Concat(Directory.GetFiles(full,"*",SearchOption.AllDirectories)
                    .Where(p=>p.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jkmod",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))
                    .Select(p=>p.Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length+1).Replace('\\','/'))).ToArray();
            }
            var inputs=new HashSet<string>(files,StringComparer.OrdinalIgnoreCase);
            var pending=new Queue<string>(files.Where(f=>f.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)));
            string boundary=root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            while(pending.Count>0){
                string relative=pending.Dequeue(),path=Path.Combine(root,relative);
                if(!File.Exists(path))continue;
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16000000})){
                    while(reader.Read())if(reader.NodeType==XmlNodeType.Element&&reader.LocalName=="Include"){
                        string source=reader.GetAttribute("src");if(string.IsNullOrWhiteSpace(source))continue;
                        string include=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),source));
                        if(!include.StartsWith(boundary,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Map identity include escapes its root: "+relative);
                        string name=include.Substring(boundary.Length).Replace('\\','/');
                        if(inputs.Add(name)){if(inputs.Count>4096)throw new InvalidDataException("Too many map identity inputs");pending.Enqueue(name);}
                    }
                }
            }
            return "gameplay-v2:"+Hash(root,inputs.OrderBy(p=>p,StringComparer.Ordinal).ToArray(),true);
        }
        private static string Hash(string root,string[] names,bool missing)
        {
            root=Path.GetFullPath(root);
            using(var aggregate=new MemoryStream())using(var sha=SHA256.Create()){
                int found=0;
                foreach(string name in names){
                    string path=Path.Combine(root,name);bool exists=File.Exists(path);
                    if(!exists&&!missing)continue;
                    byte[] label=Encoding.UTF8.GetBytes(name+"\0");aggregate.Write(label,0,label.Length);
                    if(missing)aggregate.WriteByte(exists?(byte)1:(byte)0);
                    if(exists){found++;using(var file=File.OpenRead(path)){byte[] hash=sha.ComputeHash(file);aggregate.Write(hash,0,hash.Length);}}
                }
                if(found==0)throw new InvalidOperationException("Map assets are unavailable at "+root);
                return BitConverter.ToString(sha.ComputeHash(aggregate.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
    }
}
