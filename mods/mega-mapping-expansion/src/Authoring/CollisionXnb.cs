using System;
using System.IO;
using System.Text;

namespace MegaMappingExpansion
{
    // stock Worldsmith emits an uncompressed Color texture with one mip
    // read it on the CPU so collision doesn't depend on the GPU's texture limit
    internal static class CollisionXnb
    {
        internal static void Read(string path,MapLayout layout,Action<BinaryReader,int> pixels)
        {
            layout.ValidatePixel();
            ReadTexture(path,delegate(BinaryReader reader,int width,int height){
                layout.ValidateAtlas(width,height);pixels(reader,checked(width*height));
            });
        }
        internal static void ReadTexture(string path,Action<BinaryReader,int,int> pixels)
        {
            using(var stream=File.OpenRead(path))
            using(var reader=new BinaryReader(stream,Encoding.UTF8))
            {
                if(stream.Length<32||stream.Length>512L*1024*1024+4096)throw new InvalidDataException(path+": invalid texture size.");
                if(reader.ReadByte()!=88||reader.ReadByte()!=78||reader.ReadByte()!=66||reader.ReadByte()!=119||reader.ReadByte()!=5||reader.ReadByte()!=0)
                    throw new InvalidDataException(path+": expected uncompressed Windows XNB v5. Rebuild with Worldsmith's default texture settings.");
                if(reader.ReadInt32()!=stream.Length||Index(reader)!=1)throw new InvalidDataException(path+": invalid texture header.");
                int length=Index(reader);
                if(length<1||length>1024)throw new InvalidDataException(path+": invalid texture reader.");
                string name=Encoding.UTF8.GetString(reader.ReadBytes(length));
                if((name!="Microsoft.Xna.Framework.Content.Texture2DReader"&&!name.StartsWith("Microsoft.Xna.Framework.Content.Texture2DReader,",StringComparison.Ordinal))||reader.ReadInt32()!=0||Index(reader)!=0||Index(reader)!=1||reader.ReadInt32()!=0)
                    throw new InvalidDataException(path+": expected one SurfaceFormat.Color texture.");
                int width=reader.ReadInt32(),height=reader.ReadInt32();
                if(width<1||height<1||(long)width*height>128L*1024*1024)throw new InvalidDataException(path+": invalid texture dimensions.");
                if(reader.ReadInt32()!=1||reader.ReadInt32()!=(long)width*height*4||stream.Length-stream.Position!=(long)width*height*4)
                    throw new InvalidDataException(path+": invalid texture pixel payload or mip count.");
                pixels(reader,width,height);
                if(stream.Position!=stream.Length)throw new InvalidDataException(path+": texture reader did not consume the pixel payload.");
            }
        }
        private static int Index(BinaryReader reader)
        {
            int value=0;
            for(int shift=0;shift<35;shift+=7)
            {
                byte b=reader.ReadByte();if(shift==28&&(b&240)!=0)throw new InvalidDataException("Invalid XNB integer.");
                value|=(b&127)<<shift;if((b&128)==0)return value;
            }
            throw new InvalidDataException("Invalid XNB integer.");
        }
        internal static uint Colour(uint packed)
        {
            uint alpha=packed>>24;
            if(alpha==0||packed==0xffff00ff)return 0;
            if(alpha!=255)throw new InvalidDataException("Collision pixels must be opaque or fully transparent; remove anti-aliasing and partial alpha from level+.png.");
            return packed;
        }
    }
}
