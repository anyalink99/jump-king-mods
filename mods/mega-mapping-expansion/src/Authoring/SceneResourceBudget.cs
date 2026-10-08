using System;
using System.IO;
using System.Collections.Generic;

namespace MegaMappingExpansion
{
    internal static class SceneResourceBudget
    {
        internal const long MaximumBytes=512L*1024*1024;
        internal static long Validate(SceneFile scene,string root,bool includeVectors=true,Dictionary<string,byte[]> compiled=null)
        {
            long bytes=0;
            foreach(var texture in scene.Textures)
            {
                int firstWidth=0,firstHeight=0;
                for(int index=0;index<=texture.Pages.Length;index++)
                {
                    string name=index==0?texture.Path:texture.Pages[index-1].Path;
                    string path=SceneValidation.ResolveAsset(root,name);
                    int width,height;
                    using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream))
                    {
                        byte[] header=reader.ReadBytes(24);
                        if(header.Length!=24||header[0]!=137||header[1]!=80||header[2]!=78||header[3]!=71||header[12]!=73||header[13]!=72||header[14]!=68||header[15]!=82)
                            throw new InvalidDataException(name+": invalid PNG header");
                        width=BigEndian(header,16);height=BigEndian(header,20);
                    }
                    if(width<1||height<1||width>16384||height>16384||width%texture.Columns!=0||height%texture.Rows!=0)
                        throw new InvalidDataException(name+": PNG dimensions must fit 16384px and divide evenly into the atlas grid");
                    if(index==0){firstWidth=width;firstHeight=height;}
                    else if(firstWidth!=width||firstHeight!=height)throw new InvalidDataException(name+": atlas pages must have identical dimensions");
                    bytes+=(long)width*height*4;
                }
            }
            if(includeVectors)foreach(var asset in scene.VectorAssets)
            {
                if(compiled==null)bytes+=(long)asset.Width*asset.Height*4;
                else
                {
                    byte[] png;
                    if(!compiled.TryGetValue(asset.Id,out png)||png.Length<24)throw new InvalidDataException("Missing compiled asset: "+asset.Id);
                    int width=BigEndian(png,16),height=BigEndian(png,20);
                    if(width<1||height<1||width>16384||height>16384)throw new InvalidDataException("Invalid compiled asset dimensions: "+asset.Id);
                    bytes+=(long)width*height*4;
                }
            }
            if(bytes>MaximumBytes)throw new InvalidDataException("Scene base textures require "+(bytes/1048576)+" MiB, above the 512 MiB authoring budget. Share assets between screens, reduce atlas frames or split the map. This excludes native textures and reload overlap.");
            return bytes;
        }
        private static int BigEndian(byte[] bytes,int at)
        {return (bytes[at]<<24)|(bytes[at+1]<<16)|(bytes[at+2]<<8)|bytes[at+3];}
    }
}
