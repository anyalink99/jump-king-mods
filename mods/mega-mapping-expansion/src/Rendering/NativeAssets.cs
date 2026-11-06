using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Construction
{
    internal sealed class PreparedBitmap
    {
        internal readonly AssetData Definition;
        internal readonly Texture2D Texture;
        internal readonly Color[] Pixels;
        internal int FrameWidth { get { return Texture.Width/Definition.Columns; } }
        internal int FrameHeight { get { return Texture.Height/Definition.Rows; } }
        internal int Frames { get { return Definition.Frames==0?Definition.Columns*Definition.Rows:Definition.Frames; } }
        internal PreparedBitmap(AssetData data,Texture2D texture)
        {
            Definition=new AssetData{Id=data.Id,Path=data.Path,Kind=data.Kind,Columns=data.Columns,Rows=data.Rows,Frames=data.Frames,Fps=data.Fps};Texture=texture;
            if(texture.Width%data.Columns!=0||texture.Height%data.Rows!=0)throw new InvalidDataException(data.Id+": atlas dimensions must divide into its grid");
            Pixels=new Color[texture.Width*texture.Height];texture.GetData(Pixels);
        }
        internal Rectangle Frame(int index)
        {index=Math.Max(0,Math.Min(Frames-1,index));return new Rectangle(index%Definition.Columns*FrameWidth,index/Definition.Columns*FrameHeight,FrameWidth,FrameHeight);}
        internal bool Opaque(int x,int y) {return x>=0&&y>=0&&x<Texture.Width&&y<Texture.Height&&Pixels[y*Texture.Width+x].A!=0;}
    }
    internal sealed class NativeAssets : IDisposable
    {
        internal readonly Dictionary<string,PreparedBitmap> Bitmaps=new Dictionary<string,PreparedBitmap>(StringComparer.Ordinal);
        private readonly JKRuntime.RuntimeScope resources=new JKRuntime.RuntimeScope();
        private readonly GraphicsDevice device;
        private readonly Dictionary<string,Texture2D> alpha=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        internal long Bytes{get;private set;}
        internal string ContentIdentity{get;private set;}
        internal NativeAssets(GraphicsDevice device,string root,AssetData[] definitions)
        {
            this.device=device;
            long bytes=0;
            try
            {
                foreach(var definition in definitions)
                {
                    string path=Resolve(root,definition.Path);Texture2D texture;
                    if(definition.Kind=="vector")
                    {
                        VectorAssetData vector;
                        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000}))
                            vector=(VectorAssetData)new XmlSerializer(typeof(VectorAssetData)).Deserialize(reader);
                        FiniteNumbers.Validate(vector,"vector");
                        if(vector.Width<1||vector.Width>2048||vector.Height<1||vector.Height>2048||vector.Supersample<1||vector.Supersample>4)throw new InvalidDataException("Vector dimensions or supersampling exceed the scene limit");
                        texture=resources.Own(VectorGraphics.Render(device,vector));
                    }
                    else if(Path.GetExtension(path).Equals(".xnb",StringComparison.OrdinalIgnoreCase))
                    {
                        texture=null;
                        CollisionXnb.ReadTexture(path,delegate(BinaryReader reader,int width,int height){
                            CheckDimensions(width,height,ref bytes);var pixels=new Color[width*height];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(reader.ReadUInt32());
                            texture=resources.Own(new Texture2D(device,width,height));texture.SetData(pixels);
                        });
                    }
                    else
                    {
                        using(var stream=File.OpenRead(path))using(var image=System.Drawing.Image.FromStream(stream,false,false))CheckDimensions(image.Width,image.Height,ref bytes);
                        using(var stream=File.OpenRead(path))texture=resources.Own(Texture2D.FromStream(device,stream));
                        var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
                        for(int i=0;i<pixels.Length;i++){float alpha=pixels[i].A/255f;pixels[i]=new Color((byte)(pixels[i].R*alpha),(byte)(pixels[i].G*alpha),(byte)(pixels[i].B*alpha),pixels[i].A);}texture.SetData(pixels);
                    }
                    if(definition.Kind=="vector")CheckDimensions(texture.Width,texture.Height,ref bytes);
                    Bitmaps.Add(definition.Id,new PreparedBitmap(definition,texture));
                }
                Bytes=bytes;
                using(var hash=System.Security.Cryptography.SHA256.Create())
                {
                    using(var stream=new System.Security.Cryptography.CryptoStream(Stream.Null,hash,System.Security.Cryptography.CryptoStreamMode.Write))using(var writer=new BinaryWriter(stream))
                    {
                        foreach(var bitmap in System.Linq.Enumerable.OrderBy(Bitmaps.Values,b=>b.Definition.Id,StringComparer.Ordinal))
                        {writer.Write(bitmap.Definition.Id);writer.Write(bitmap.Texture.Width);writer.Write(bitmap.Texture.Height);foreach(var pixel in bitmap.Pixels)writer.Write(pixel.PackedValue);}
                    }
                    ContentIdentity=Convert.ToBase64String(hash.Hash);
                }
            }
            catch{resources.Dispose();throw;}
        }
        private static void CheckDimensions(int width,int height,ref long bytes)
        {
            if(width<1||height<1||width>16384||height>16384)throw new InvalidDataException("Scene texture dimensions must fit 16384 pixels");
            bytes+=(long)width*height*8;
            if(bytes>512L*1024*1024)throw new InvalidDataException("Scene GPU textures and CPU copies exceed 512 MiB");
        }
        internal Texture2D PrepareAlpha(string id)
        {
            Texture2D texture;if(alpha.TryGetValue(id,out texture))return texture;var bitmap=Bitmaps[id];long bytes=Bytes+(long)bitmap.Pixels.Length*4;
            if(bytes>512L*1024*1024)throw new InvalidDataException("Prepared scene textures and alpha channels exceed 512 MiB");
            var pixels=new Color[bitmap.Pixels.Length];for(int i=0;i<pixels.Length;i++){byte a=bitmap.Pixels[i].A;pixels[i]=new Color(a,a,a,a);}
            texture=resources.Own(new Texture2D(device,bitmap.Texture.Width,bitmap.Texture.Height));texture.SetData(pixels);alpha.Add(id,texture);Bytes=bytes;return texture;
        }
        internal static string Resolve(string root,string relative)
        {
            string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new InvalidDataException("Asset paths must be relative to the map");
            string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Asset path escapes the map: "+relative);
            if(!File.Exists(path)){string compiled=Path.ChangeExtension(path,".xnb");if(!File.Exists(compiled))throw new FileNotFoundException("Scene asset is missing",path);path=compiled;}
            return path;
        }
        public void Dispose(){resources.Dispose();Bitmaps.Clear();alpha.Clear();}
    }
}
