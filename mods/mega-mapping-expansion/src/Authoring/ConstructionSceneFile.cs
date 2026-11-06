using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MegaMappingExpansion.Construction
{
    internal static class ConstructionSceneFile
    {
        private const int MaximumPayload=16*1024*1024;
        private static readonly byte[] Magic=Encoding.ASCII.GetBytes("MMECON1\n");
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        private static byte[] Hash(byte[] bytes){using(var hash=SHA256.Create())return hash.ComputeHash(bytes);}
        private static byte[] SourceHash(XDocument source){return Hash(Utf8.GetBytes(SemanticIdentity.Value+"\n"+source.ToString(SaveOptions.DisableFormatting)));}
        internal static string CacheDirectory
        {
            get
            {
                string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if(string.IsNullOrEmpty(local))throw new IOException("Local application data is unavailable");
                return Path.Combine(local,"JumpKing","MegaMappingExpansion","SceneCache");
            }
        }
        internal static string CacheFile(string root,string directory)
        {
            string key=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(Path.DirectorySeparatorChar=='\\')key=key.ToUpperInvariant();
            return Path.Combine(directory,BitConverter.ToString(Hash(Utf8.GetBytes(key))).Replace("-","").ToLowerInvariant()+".mmgfx");
        }
        internal static SceneDocument Read(string root,bool sourceOnly=false,string cacheDirectory=null)
        {
            // source errors must stay visible, even when an older cache still loads
            var source=RecipeCompiler.Source(root);if(source==null)return null;
            byte[] fingerprint=SourceHash(source);string path=null;
            try
            {
                path=CacheFile(root,cacheDirectory??CacheDirectory);
                if(!sourceOnly&&File.Exists(path))return ReadCache(path,fingerprint);
            }
            catch(Exception error){if(!CacheFailure(error))throw;}
            var document=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(source);
            using(var program=new SceneProgram(document)){}
            if(path!=null)try{WriteCache(document,fingerprint,path);TrimCache(Path.GetDirectoryName(path),path);}
            catch(Exception error){if(!CacheFailure(error))throw;}
            return document;
        }
        private static bool CacheFailure(Exception error)
        {
            return error is InvalidDataException||error is IOException||error is UnauthorizedAccessException||error is SecurityException||
                error is XmlException||error is InvalidOperationException||error is ArgumentException;
        }
        private static SceneDocument ReadCache(string path,byte[] fingerprint)
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))using(var reader=new BinaryReader(stream))
            {
                if(!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))throw new InvalidDataException("Unknown scene cache format");
                if(!reader.ReadBytes(32).SequenceEqual(fingerprint))throw new InvalidDataException("Scene source or MME changed");
                byte[] digest=reader.ReadBytes(32);int count=reader.ReadInt32();
                if(count<1||count>MaximumPayload||stream.Length-stream.Position!=count)throw new InvalidDataException("Invalid expanded scene size");
                byte[] payload=reader.ReadBytes(count);if(!Hash(payload).SequenceEqual(digest))throw new InvalidDataException("Scene cache checksum differs");
                using(var xml=XmlReader.Create(new StringReader(Utf8.GetString(payload)),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumPayload}))
                    return RecipeCompiler.Deserialize(XDocument.Load(xml));
            }
        }
        // offline exports are for developer tools; runtime never reads them from a map
        internal static SceneDocument Compile(string root,string output)
        {
            var source=RecipeCompiler.Source(root);if(source==null)throw new FileNotFoundException("mapping_settings.xml is missing",root);
            var document=new RecipeCompiler(PrimitiveRegistry.Create()).Compile(source);using(var program=new SceneProgram(document)){}
            WriteCache(document,SourceHash(source),output);return document;
        }
        private static void WriteCache(SceneDocument document,byte[] fingerprint,string output)
        {
            byte[] payload=Utf8.GetBytes(SceneDocument.Xml(document));if(payload.Length>MaximumPayload)throw new InvalidDataException("Expanded scene cache exceeds 16 MiB");
            string full=Path.GetFullPath(output),temporary=full+".tmp-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(Path.GetDirectoryName(full));
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new BinaryWriter(stream))
                {writer.Write(Magic);writer.Write(fingerprint);writer.Write(Hash(payload));writer.Write(payload.Length);writer.Write(payload);writer.Flush();stream.Flush(true);}
                // readers keep the complete old file until this replacement commits
                if(File.Exists(full))File.Replace(temporary,full,null,true);else File.Move(temporary,full);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static void TrimCache(string directory,string current)
        {
            long bytes=0;int count=0;
            foreach(var file in new DirectoryInfo(directory).GetFiles("*.mmgfx").OrderByDescending(f=>f.FullName==current).ThenByDescending(f=>f.LastWriteTimeUtc))
            {
                // only our hashed entries belong to this eviction policy
                string key=Path.GetFileNameWithoutExtension(file.Name);
                if(key.Length!=64||key.Any(c=>!Uri.IsHexDigit(c)))continue;
                bytes+=file.Length;count++;
                if(count>128||bytes>64L*1024*1024)file.Delete();
            }
            foreach(var file in new DirectoryInfo(directory).GetFiles("*.mmgfx.tmp-*"))
            {
                string key=file.Name.Split('.')[0];
                if(key.Length==64&&key.All(Uri.IsHexDigit)&&file.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-1))file.Delete();
            }
        }
    }
}
