using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class PixelCompilation
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    internal static void Run(Assembly engine,string fixtures)
    {
        string root=Path.Combine(fixtures,"pixel-compiler"),output=Path.Combine(fixtures,"pixel-compiler-output");
        Directory.CreateDirectory(root);Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),"<MappingSettings version='1'><MapLayout version='2' screens='2' columns='2' rows='1' cellWidth='60' cellHeight='45' source='atlas'/></MappingSettings>");
        object layout=engine.GetType("WorldsmithExtension.Layout").GetMethod("Read",All).Invoke(null,new object[]{root,2});
        var compiler=engine.GetType("WorldsmithExtension.NativeCompiler");
        compiler.GetMethod("Configure",All).Invoke(null,new object[]{root,output});
        var direct=engine.GetType("WorldsmithExtension.Collision").GetMethod("PixelXnb",All);
        foreach(bool indexed in new[]{false,true})
        {
            string png=Path.Combine(root,indexed?"indexed.png":"rgba.png");
            using(var image=new Bitmap(120,45,indexed?PixelFormat.Format8bppIndexed:PixelFormat.Format32bppArgb))
            {
                if(indexed)
                {
                    var palette=image.Palette;
                    for(int i=0;i<256;i++)palette.Entries[i]=Colour(i);
                    image.Palette=palette;
                    var bits=image.LockBits(new Rectangle(0,0,120,45),ImageLockMode.WriteOnly,PixelFormat.Format8bppIndexed);
                    try
                    {
                        byte[] row=new byte[120];
                        for(int y=0;y<45;y++) { for(int x=0;x<120;x++)row[x]=(byte)((x+y*120)%256);Marshal.Copy(row,0,IntPtr.Add(bits.Scan0,y*bits.Stride),row.Length); }
                    }
                    finally { image.UnlockBits(bits); }
                }
                else for(int y=0;y<45;y++)for(int x=0;x<120;x++)image.SetPixel(x,y,Colour((x+y*120)%256));
                image.Save(png,ImageFormat.Png);
            }
            string native=Path.Combine(output,indexed?"native-indexed.xnb":"native-rgba.xnb"),fast=Path.Combine(output,indexed?"direct-indexed.xnb":"direct-rgba.xnb");
            compiler.GetMethod("NativeTexture",All).Invoke(null,new object[]{png,native});
            direct.Invoke(null,new object[]{png,fast,layout});
            if(!Payload(engine,native).SequenceEqual(Payload(engine,fast)))throw new Exception("Direct pixel compiler differs from native RGBA payload: "+png);
            engine.GetType("WorldsmithExtension.Collision").GetMethod("ValidatePixelXnb",All).Invoke(null,new object[]{fast,layout});
            byte[] before=File.ReadAllBytes(fast);
            using(var bad=new Bitmap(120,45)) { bad.SetPixel(0,0,Color.FromArgb(127,1,2,3));bad.Save(png,ImageFormat.Png); }
            try { direct.Invoke(null,new object[]{png,fast,layout});throw new Exception("Partial alpha unexpectedly compiled"); }
            catch(TargetInvocationException error) { if(!(error.InnerException is InvalidDataException))throw; }
            if(!before.SequenceEqual(File.ReadAllBytes(fast)))throw new Exception("Failed pixel validation replaced compiled output");
        }
        Console.WriteLine("[OK] Streaming pixel compiler matches native RGBA and indexed PNG payloads; failed validation preserves existing XNBs.");
    }
    static Color Colour(int value)
    {
        if(value==0)return Color.Magenta;
        if(value==1)return Color.FromArgb(0,17,29,43);
        if(value==2)return Color.FromArgb(255,255,0,254);
        return Color.FromArgb(255,value,(value*71)%256,(value*113)%256);
    }
    static byte[] Payload(Assembly engine,string path)
    {
        byte[] data=null;
        engine.GetType("MegaMappingExpansion.CollisionXnb").GetMethod("ReadTexture",All).Invoke(null,new object[]{path,new Action<BinaryReader,int,int>((reader,width,height)=>{
            if(width!=120||height!=45)throw new Exception("Wrong collision dimensions");data=reader.ReadBytes(width*height*4);
        })});
        return data;
    }
}
