using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed class NativeAssetPool:IDisposable
    {
        internal readonly NativeSoundPool Sounds=new NativeSoundPool();
        internal sealed class Lease:IDisposable
        {
            private NativeAssetPool pool;
            private Entry entry;
            internal NativeAssets Assets{get{return entry.Assets;}}
            internal string Identity{get{return entry.Key;}}
            internal Lease(NativeAssetPool owner,Entry value){pool=owner;entry=value;entry.Readers++;}
            public void Dispose(){if(pool==null)return;entry.Readers--;pool.Trim();pool=null;}
        }
        internal sealed class Entry
        {
            internal string Key;
            internal NativeAssets Assets;
            internal GraphicsDevice Device;
            internal int Readers;
            internal long Used;
        }
        private readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private long serial;
        private bool disposed;
        internal static string Identity(string root,AssetData[] definitions)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))using(var hash=SHA256.Create())
            {
                foreach(var definition in definitions.OrderBy(a=>a.Id,StringComparer.Ordinal))
                {
                    string path=NativeAssets.Resolve(root,definition.Path);writer.Write(definition.Id);writer.Write(definition.Path);writer.Write(definition.Kind);writer.Write(definition.Columns);writer.Write(definition.Rows);writer.Write(definition.Frames);writer.Write(definition.Fps);
                    using(var source=File.OpenRead(path))writer.Write(hash.ComputeHash(source));
                }
                writer.Flush();return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }
        internal Lease Acquire(GraphicsDevice device,string root,AssetData[] definitions)
        {
            if(disposed)throw new ObjectDisposedException("NativeAssetPool");string key=Identity(root,definitions);Entry entry;
            if(entries.TryGetValue(key,out entry)&&entry.Device!=device)
            {
                if(entry.Readers!=0)throw new InvalidOperationException("An active scene still owns resources from another graphics device");entry.Assets.Dispose();entries.Remove(key);entry=null;
            }
            if(entry==null)
            {
                var assets=new NativeAssets(device,root,definitions);
                if(entries.Values.Sum(e=>e.Assets.Bytes)+assets.Bytes>1024L*1024*1024){assets.Dispose();throw new InvalidDataException("World scene asset cache exceeds 1 GiB");}
                entry=new Entry{Key=key,Assets=assets,Device=device};entries.Add(key,entry);
            }
            entry.Used=++serial;var lease=new Lease(this,entry);Trim();return lease;
        }
        private void Trim()
        {
            // keep the latest warm set for restarts; active candidates survive until their owners leave
            long latest=entries.Count==0?0:entries.Values.Max(e=>e.Used);
            foreach(var entry in entries.Values.Where(e=>e.Readers==0&&e.Used!=latest).ToArray()){entry.Assets.Dispose();entries.Remove(entry.Key);}
        }
        public void Dispose(){if(disposed)return;disposed=true;foreach(var entry in entries.Values)entry.Assets.Dispose();entries.Clear();Sounds.Dispose();}
    }
}
