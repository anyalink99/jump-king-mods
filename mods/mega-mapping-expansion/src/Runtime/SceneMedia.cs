using System;
using System.Collections.Generic;
using System.Linq;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SceneMedia:IMappingMedia,IDisposable
    {
        private sealed class Entry:IDisposable
        {
            internal SceneMedia Owner;
            internal string Id;
            internal Texture2D Texture;
            internal Action<bool> Pause;
            internal Action Stop;
            internal bool Active,Paused;
            internal void Release()
            {if(!Active)return;Active=false;Paused=false;if(Stop!=null)Stop();}
            public void Dispose()
            {if(Owner==null)return;Owner.entries.Remove(Id);Owner=null;Release();}
        }
        private readonly SceneProgram scene;
        private readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private bool disposed;
        internal SceneMedia(SceneProgram program){scene=program;}
        public IDisposable Register(string source,object texture,Action<bool> pause,Action stop)
        {
            scene.Signals.Check();if(disposed)throw new ObjectDisposedException("Scene media");
            if(string.IsNullOrWhiteSpace(source)||source.Length>128||entries.ContainsKey(source)||entries.Count>=64)throw new ArgumentException("Media requires a unique source key, up to 64 sources");
            var bitmap=texture as Texture2D;
            if(texture!=null&&(bitmap==null||bitmap.IsDisposed||bitmap.Width>16384||bitmap.Height>16384))throw new ArgumentException("Media texture must be a live Texture2D within 16384 pixels per axis");
            if((pause==null)!=(stop==null))throw new ArgumentException("Audio media requires both pause and stop callbacks");
            var entry=new Entry{Owner=this,Id=source,Texture=bitmap,Pause=pause,Stop=stop};entries.Add(source,entry);return entry;
        }
        internal Texture2D Texture(string source)
        {Entry entry;return entries.TryGetValue(source,out entry)&&entry.Texture!=null&&!entry.Texture.IsDisposed?entry.Texture:null;}
        internal void Tick(bool enabled,bool paused)
        {
            foreach(var entry in entries.Values.ToArray())
            {
                bool requested=enabled&&scene.Components.Values.Any(c=>c.Descriptor.Kind=="media"&&scene.Properties.Read(c.Id,"source").Text==entry.Id&&scene.Properties.Read(c.Id,"active").X!=0);
                if(!requested){entry.Release();continue;}
                entry.Active=true;
                if(entry.Paused!=paused){if(entry.Pause!=null)entry.Pause(paused);entry.Paused=paused;}
            }
        }
        internal void Stop(){foreach(var entry in entries.Values.ToArray())entry.Release();}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var entry in entries.Values.ToArray())entry.Dispose();}
    }
}
