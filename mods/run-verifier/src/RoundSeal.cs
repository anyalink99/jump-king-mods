using System;
using System.Collections.Generic;
namespace RunVerifier
{
    // Three independently decodable sectors share a payload and error correction.
    internal static class RoundSeal
    {
        internal const int Size=124;
        internal static readonly string[] Finder={"000111100","001000010","010000010","010000100","010111000","011010000","010001000","100000100","100000010"};
        internal static int[][][] Sectors()
        {
            var lists=new[]{new List<int[]>(),new List<int[]>(),new List<int[]>()};
            for(int y=0;y<62;y++)for(int x=0;x<62;x++)
            {
                double dx=x-30.5,dy=y-30.5,d=dx*dx+dy*dy;
                if(d<18*18||d>30*30)continue;
                double angle=(Math.Atan2(dy,dx)+Math.PI*2)%(Math.PI*2);
                lists[(int)(angle/(Math.PI*2/3))].Add(new[]{x*2,y*2});
            }
            var result=new int[3][][];
            for(int s=0;s<3;s++){result[s]=new int[592][];for(int i=0;i<592;i++)result[s][i]=lists[s][i*lists[s].Count/592];}
            return result;
        }
        internal static readonly int[][][] Positions=Sectors();
        internal static bool[,] Raster(byte[] payload)
        {
            var pixels=new bool[Size,Size];var bits=SealCodec.Encode(payload);
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {double d=Math.Sqrt((x-61.5)*(x-61.5)+(y-61.5)*(y-61.5));pixels[x,y]=(d>=60.5&&d<61.5)||(d>=33.5&&d<34.5);}
            foreach(var sector in Positions)for(int i=0;i<bits.Length;i++)for(int y=0;y<2;y++)for(int x=0;x<2;x++)pixels[sector[i][0]+x,sector[i][1]+y]=bits[i];
            var signature=Signature(BitConverter.ToUInt64(payload,16));
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(signature[x,y])pixels[x,y]=true;
            return pixels;
        }
        // The fixed initial locates the code; all following strokes derive from Steam identity.
        internal static bool[,] Signature(ulong steam)
        {
            var pixels=new bool[Size,Size];
            for(int y=0;y<9;y++)for(int x=0;x<9;x++)for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)pixels[35+x*2+dx,48+y*2+dy]=Finder[y][x]=='1';
            ulong seed;
            unchecked {seed=steam+0x9e3779b97f4a7c15UL;seed=(seed^(seed>>30))*0xbf58476d1ce4e5b9UL;seed=(seed^(seed>>27))*0x94d049bb133111ebUL;seed^=seed>>31;}
            int px=53,py=65;
            for(int i=0;i<8;i++)
            {
                int v=(int)((seed>>(i*8))&255),ax=px,ay=py,bx=54+i*4+4,by=62+(v&7),cx=54+i*4+2,cy=43+((v>>3)&15);
                for(int t=1;t<=16;t++){int u=16-t,x=(u*u*ax+2*u*t*cx+t*t*bx)/256,y=(u*u*ay+2*u*t*cy+t*t*by)/256;Line(pixels,px,py,x,y);px=x;py=y;}
            }
            Line(pixels,37,78,88,73);Line(pixels,88,73,76+(int)(seed&7),80);
            return pixels;
        }
        private static void Line(bool[,] pixels,int ax,int ay,int bx,int by)
        {
            int n=Math.Max(Math.Abs(bx-ax),Math.Abs(by-ay));if(n==0){pixels[ax,ay]=true;return;}
            for(int k=0;k<=n;k++)pixels[ax+(bx-ax)*k/n,ay+(by-ay)*k/n]=true;
        }
    }
}
