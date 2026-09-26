using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace OverlayPlus.Api
{
    /// <summary>A game-thread provider. Draw uses the supplied active SpriteBatch;
    /// it must not begin/end batches, change device state or mutate gameplay.</summary>
    public sealed class OverlaySource
    {
        public string Id, Name;
        public Func<string> Text;
        public Func<bool> Available;
        public Action<SpriteBatch,Rectangle,float> Draw;
        public Action<bool> SetNativeVisible;
    }
    public static class OverlayRegistry
    {
        private static readonly Dictionary<string,OverlaySource> sources=new Dictionary<string,OverlaySource>(StringComparer.Ordinal);
        public static long Generation {get;private set;}
        public static IDisposable Register(OverlaySource source)
        {
            if(source==null||string.IsNullOrWhiteSpace(source.Id)||source.Id.IndexOf('.')<1||string.IsNullOrWhiteSpace(source.Name)||(source.Text==null&&source.Draw==null))throw new ArgumentException("A namespaced source ID, name and text/draw callback are required");
            if(sources.ContainsKey(source.Id))throw new InvalidOperationException("Overlay source already registered: "+source.Id);
            sources.Add(source.Id,source);Generation++;return new Registration(source);
        }
        public static OverlaySource[] Sources(){var result=new OverlaySource[sources.Count];sources.Values.CopyTo(result,0);return result;}
        private sealed class Registration:IDisposable
        {
            private OverlaySource source;
            internal Registration(OverlaySource value){source=value;}
            public void Dispose(){if(source==null)return;try{if(source.SetNativeVisible!=null)source.SetNativeVisible(true);}finally{sources.Remove(source.Id);source=null;Generation++;}}
        }
    }
}
