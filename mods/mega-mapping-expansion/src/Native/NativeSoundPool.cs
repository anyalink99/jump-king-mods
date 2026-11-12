using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JKRuntime.Audio;
using JumpKing;
using JumpKing.XnaWrappers;

namespace MegaMappingExpansion.Construction
{
    // inactive voices stay warm for restarts; a reload candidate needs its own voice
    internal sealed class NativeSoundPool : IDisposable
    {
        internal sealed class Lease : IDisposable
        {
            private NativeSoundPool pool;
            private Entry entry;
            internal PreparedSound Sound {get{return entry.Sound;}}
            internal string Identity {get{return entry.Key;}}
            internal Lease(NativeSoundPool owner,Entry value){pool=owner;entry=value;entry.Used=true;}
            public void Dispose(){if(pool==null)return;entry.Sound.Stop();entry.Used=false;pool.Trim();pool=null;}
        }
        internal sealed class Entry {internal string Key;internal PreparedSound Sound;internal bool Used;internal long Bytes;}
        private readonly List<Entry> entries=new List<Entry>();
        internal Lease Acquire(string root,string relative,SoundType bus)
        {
            string path=NativeAssets.Resolve(root,relative);
            if(new FileInfo(path).Length>64*1024*1024)throw new InvalidDataException("Sound exceeds 64 MiB: "+relative);
            byte[] bytes=File.ReadAllBytes(path);
            string key;using(var hash=SHA256.Create())key=bus+":"+Convert.ToBase64String(hash.ComputeHash(bytes));
            var entry=entries.FirstOrDefault(e=>!e.Used&&e.Key==key);
            if(entry==null)
            {
                foreach(var old in entries.Where(e=>!e.Used).ToArray())
                {
                    if(entries.Sum(e=>e.Bytes)+bytes.Length<=256L*1024*1024)break;
                    old.Sound.Dispose();entries.Remove(old);
                }
                if(entries.Sum(e=>e.Bytes)+bytes.Length>256L*1024*1024)throw new InvalidDataException("Prepared audio exceeds 256 MiB of encoded data");
                PreparedSound sound;using(var stream=new MemoryStream(bytes,false))sound=Path.GetExtension(path).Equals(".xnb",StringComparison.OrdinalIgnoreCase)
                    ?PreparedSound.FromContent(stream,Game1.instance.Services,bus):PreparedSound.FromStream(stream,bus);
                entry=new Entry{Key=key,Sound=sound,Bytes=bytes.Length};entries.Add(entry);
            }
            return new Lease(this,entry);
        }
        private void Trim()
        {foreach(var entry in entries.Where(e=>!e.Used).Skip(64).ToArray()){entry.Sound.Dispose();entries.Remove(entry);}}
        public void Dispose(){foreach(var entry in entries)entry.Sound.Dispose();entries.Clear();}
    }
}
