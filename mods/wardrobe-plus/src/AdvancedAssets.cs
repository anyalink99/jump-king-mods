using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus.Advanced
{
    internal sealed class AnimationBinding
    {
        internal SkinPackage Package;
        internal AnimationSet Definition;
        internal int Item;
        internal MaterialKind Material;
        internal readonly Dictionary<AnimationFrame,Sprite> MaterialFrames=new Dictionary<AnimationFrame,Sprite>();
    }
    internal sealed class PresentationAssets : IDisposable
    {
        internal readonly SkinLibrary Library;
        internal readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
        internal readonly Dictionary<string,SoundEffect> Sounds=new Dictionary<string,SoundEffect>();
        internal readonly Dictionary<string,Effect> Shaders=new Dictionary<string,Effect>();
        internal readonly Dictionary<int,AnimationBinding> Animations=new Dictionary<int,AnimationBinding>();
        internal readonly List<SkinPackage> Packages=new List<SkinPackage>();
        internal long TextureBytes;
        private long fileBytes;
        private int references=1;
        internal bool Prepared,Failed;
        internal Effect SurfaceShader;
        internal Texture2D White, Cosmic;
        internal PresentationAssets(SkinLibrary library) { Library=library; }
        internal PresentationAssets Retain() { if(references<=0) throw new ObjectDisposedException("PresentationAssets"); references++; return this; }
        internal static PresentationAssets Build(PreparedAppearance appearance,Catalog catalog,bool defer=false)
        {
            var result=new PresentationAssets(catalog.Advanced);
            try
            {
                foreach(var resolution in appearance.Resolved)
                    foreach(var package in catalog.Advanced.Packages.Values.Where(p=>resolution.Value.Id.StartsWith(p.Source+":",StringComparison.Ordinal)))
                    {
                        result.AddPackage(package);
                        var animation=package.Manifest.animations.Find(a=>(int)Enum.Parse(typeof(JumpKing.MiscEntities.WorldItems.Items),a.item)==resolution.Key);
                        if(animation!=null) result.Animations[resolution.Key]=new AnimationBinding{Package=package,Definition=animation,Item=resolution.Key};
                    }
                var selection=appearance.SourceOutfit.Presentation;
                foreach(var reference in new[]{selection.Animation,selection.Particles,selection.SurfaceSound,selection.EquipmentSound,selection.Shake})
                {
                    if(string.IsNullOrEmpty(reference)||reference=="native") continue;
                    var parts=reference.Split('/'); SkinPackage package;
                    if(catalog.Advanced.Packages.TryGetValue(parts[0],out package)) result.AddPackage(package);
                }
                if(selection.Animation=="native") result.Animations.Clear();
                else if(selection.Animation.Length>0)
                {
                    var parts=selection.Animation.Split('/'); SkinPackage p;
                    if(catalog.Advanced.Packages.TryGetValue(parts[0],out p))
                    { var a=p.Manifest.animations.Find(x=>x.id==parts[1]); if(a!=null) { int item=(int)Enum.Parse(typeof(JumpKing.MiscEntities.WorldItems.Items),a.item); result.Animations[item]=new AnimationBinding{Package=p,Definition=a,Item=item}; } }
                }
                foreach(var binding in result.Animations.Values)binding.Material=appearance.SourceOutfit.MaterialFor(binding.Item);
                if(!defer)result.Prepare();return result;
            }
            catch { result.Dispose(); throw; }
        }
        private void AddPackage(SkinPackage package)
        {
            if(Packages.Contains(package)) return; Packages.Add(package);
            foreach(var d in package.Manifest.dependencies) AddPackage(Library.Packages[d.id]);
        }
        internal void Prepare()
        {
            if(Prepared)return;if(Failed)throw new InvalidOperationException("Advanced resource preparation failed; reload the package");
            try {using(JKRuntime.RuntimeApi.MeasureStartup("wardrobe-plus.presentation-assets"))foreach(var package in Packages)LoadPackage(package);foreach(var binding in Animations.Values)AnimatedMaterials.Prepare(this,binding);Prepared=true;}
            catch{Failed=true;ReleaseResources();throw;}
        }
        private void LoadPackage(SkinPackage package)
        {
            foreach(var e in package.Manifest.effects)
            {
                if(e.texture.Length>0) { var t=Texture(package,e.texture); ManifestIO.Require(t.Width%e.columns==0 && t.Height%e.rows==0,e.id+": grid does not divide texture"); }
                foreach(var file in e.sounds) Sound(package,file);
            }
            foreach(var a in package.Manifest.animations)
            {
                foreach(var c in a.clips)
                {
                    var t=Texture(package,c.texture);
                    foreach(var f in c.frames) ManifestIO.Require(f.x+f.width<=t.Width && f.y+f.height<=t.Height,a.id+": frame exceeds texture");
                }
                foreach(var n in a.attachments) { var t=Texture(package,n.texture); ManifestIO.Require(n.x+n.width<=t.Width && n.y+n.height<=t.Height,n.id+": attachment exceeds texture"); }
                if(a.material.Length>0) ManifestIO.Require(package.Manifest.materials.Any(m=>m.id==a.material),a.id+": material missing");
            }
            foreach(var m in package.Manifest.materials)
            {
                PrepareSurface();
                if(m.mask.Length>0) Texture(package,m.mask);
                if(m.texture.Length>0) Texture(package,m.texture);
                if(m.shader.Length>0)
                {
                    string file=File(package,m.shader,".mgfxo",1024*1024);
                    var effect=new Effect(Game1.spriteBatch.GraphicsDevice,System.IO.File.ReadAllBytes(file));
                    Shaders.Add(package.Manifest.id+"/"+m.id,effect);
                    ManifestIO.Require(effect.Parameters["MatrixTransform"]!=null,"Shader must expose MatrixTransform: "+m.id);
                }
                if(m.palette.Count>0)
                {
                    var palette=new Texture2D(Game1.spriteBatch.GraphicsDevice,m.palette.Count,1);
                    Textures.Add(package.Manifest.id+"/@palette/"+m.id,palette);
                    palette.SetData(m.palette.Select(PresentationActor.Color).ToArray());
                }
            }
        }
        private void PrepareSurface()
        {
            if(SurfaceShader!=null)return;
            var assembly=typeof(PresentationAssets).Assembly;var device=Game1.spriteBatch.GraphicsDevice;
            using(var stream=assembly.GetManifestResourceStream("WardrobePlus.Advanced.mgfxo"))using(var reader=new BinaryReader(stream))SurfaceShader=new Effect(device,reader.ReadBytes((int)stream.Length));
            White=new Texture2D(device,1,1);White.SetData(new[]{Color.White});
            using(var stream=assembly.GetManifestResourceStream("WardrobePlus.cosmic-nebula.png"))Cosmic=Texture2D.FromStream(device,stream);
        }
        private string File(SkinPackage package,string relative,string extension,int max)
        {
            string file=ManifestIO.Asset(package.Root,relative);
            ManifestIO.Require(string.Equals(Path.GetExtension(file),extension,StringComparison.OrdinalIgnoreCase),"Expected "+extension+": "+relative);
            long length=new FileInfo(file).Length; fileBytes+=length;
            ManifestIO.Require(length<=max && fileBytes<=128*1024*1024,"Selected packages exceed asset budget"); return file;
        }
        internal Texture2D Texture(SkinPackage package,string relative)
        {
            string key=package.Manifest.id+"/"+relative; Texture2D texture; if(Textures.TryGetValue(key,out texture)) return texture;
            string file=File(package,relative,".png",32*1024*1024);
            using(var stream=System.IO.File.OpenRead(file)) using(var reader=new BinaryReader(stream))
            {
                byte[] header=reader.ReadBytes(24);
                ManifestIO.Require(header.Length==24 && header[0]==137 && header[1]==80 && header[2]==78 && header[3]==71,"Invalid PNG "+relative);
                int width=Big(header,16),height=Big(header,20);
                ManifestIO.Require(width>0 && height>0 && width<=8192 && height<=8192,"PNG dimensions exceed limits");
                TextureBytes+=(long)width*height*4; ManifestIO.Require(TextureBytes<=128*1024*1024,"Selected textures exceed 128 MiB");
                stream.Position=0; texture=Texture2D.FromStream(Game1.spriteBatch.GraphicsDevice,stream);
            }
            // PNG uploads are straight alpha, all game sprite passes expect premultiplied data
            Textures.Add(key,texture);
            var pixels=new Color[texture.Width*texture.Height]; texture.GetData(pixels);
            for(int i=0;i<pixels.Length;i++) { var c=pixels[i]; pixels[i]=new Color((byte)(c.R*c.A/255),(byte)(c.G*c.A/255),(byte)(c.B*c.A/255),c.A); }
            texture.SetData(pixels); return texture;
        }
        private static int Big(byte[] bytes,int offset) { return (bytes[offset]<<24)|(bytes[offset+1]<<16)|(bytes[offset+2]<<8)|bytes[offset+3]; }
        internal SoundEffect Sound(SkinPackage package,string relative)
        {
            string key=package.Manifest.id+"/"+relative; SoundEffect sound; if(Sounds.TryGetValue(key,out sound)) return sound;
            string file=File(package,relative,".wav",8*1024*1024);
            using(var stream=System.IO.File.OpenRead(file)) sound=SoundEffect.FromStream(stream);
            Sounds.Add(key,sound); ManifestIO.Require(sound.Duration.TotalSeconds<=30,"Audio exceeds 30 seconds: "+relative); return sound;
        }
        public void Dispose()
        {
            if(references<=0) return; if(--references!=0) return;
            ReleaseResources();
        }
        private void ReleaseResources()
        {
            foreach(var value in Shaders.Values) value.Dispose(); foreach(var value in Sounds.Values) value.Dispose(); foreach(var value in Textures.Values) value.Dispose();
            if(SurfaceShader!=null)SurfaceShader.Dispose();if(White!=null)White.Dispose();if(Cosmic!=null)Cosmic.Dispose();
            Shaders.Clear(); Sounds.Clear(); Textures.Clear();
            SurfaceShader=null;White=Cosmic=null;
        }
    }
}
