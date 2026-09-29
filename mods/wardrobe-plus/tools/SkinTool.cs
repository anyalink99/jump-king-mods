using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using WardrobePlus.Advanced;

internal static class SkinTool
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==2 && args[0]=="layout") { Layout(args[1]);return 0; }
            if(args.Length<2 || (args[0]!="validate" && args[0]!="build")) throw new ArgumentException("SkinTool validate SOURCE [DEPENDENCY...] | build SOURCE NEW_OUTPUT [DEPENDENCY...] | layout OUTPUT_JSON");
            string root=Path.GetFullPath(args[1]); var manifest=ManifestIO.Read(Path.Combine(root,"wardrobe","skin.json"));
            var library=new SkinLibrary();library.Add(root,"source");foreach(var dependency in args.Skip(args[0]=="build"?3:2))library.Add(Path.GetFullPath(dependency),dependency);
            library.ValidateDependencies();if(library.Problems.Count>0)throw new InvalidDataException(string.Join("; ",library.Problems));
            var files=Files(root,manifest);
            if(args[0]=="build")
            {
                if(args.Length<3)throw new ArgumentException("An output folder is required");
                string output=Path.GetFullPath(args[2]);if(Directory.Exists(output))throw new IOException("Output already exists. Use a new directory to preserve prior packages.");
                Directory.CreateDirectory(output);
                foreach(var relative in files)
                {
                    string source=ManifestIO.Asset(root,relative),target=ManifestIO.Asset(output,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target);
                }
                foreach(var image in Fallbacks(root)) PackTexture(ManifestIO.Asset(root,image+".png"),ManifestIO.Asset(output,image+".xnb"));
                Console.WriteLine("[OK] Skin package: "+output);
            }
            Console.WriteLine("[OK] "+manifest.name+": schema "+manifest.schema+", "+manifest.effects.Count+" effects, "+manifest.animations.Count+" animations; "+files.Count+" packaged files");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.GetBaseException().Message);return 1;}
    }
    private static string[] Fallbacks(string root)
    {
        string single=Path.Combine(root,"cosmetic_settings.xml"),set=Path.Combine(root,"set_settings.xml");
        if(File.Exists(single)==File.Exists(set))throw new InvalidDataException("Exactly one native cosmetic_settings.xml or set_settings.xml is required");
        var xml=XDocument.Load(File.Exists(single)?single:set);
        var names=xml.Descendants("name").Select(x=>x.Value).Distinct().ToArray();
        if(names.Length==0)throw new InvalidDataException("Native fallback textures are missing");
        foreach(var name in names)ManifestIO.Asset(root,name+".png");return names;
    }
    private static HashSet<string> Files(string root,SkinManifest m)
    {
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"wardrobe/skin.json",File.Exists(Path.Combine(root,"cosmetic_settings.xml"))?"cosmetic_settings.xml":"set_settings.xml"};
        Action<string,string> add=(name,extension)=> {
            if(string.IsNullOrEmpty(name))return;
            string file=ManifestIO.Asset(Path.Combine(root,"wardrobe"),name);
            if(!File.Exists(file)||!string.Equals(Path.GetExtension(file),extension,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Missing or unsupported asset: "+name);
            if(new FileInfo(file).Length>32*1024*1024)throw new InvalidDataException("Asset exceeds 32 MiB: "+name);
            files.Add("wardrobe/"+name);
        };
        foreach(var e in m.effects){add(e.texture,".png");if(e.texture.Length>0)using(var image=Image.FromFile(ManifestIO.Asset(Path.Combine(root,"wardrobe"),e.texture)))if(image.Width%e.columns!=0||image.Height%e.rows!=0)throw new InvalidDataException(e.id+": particle grid does not divide texture");foreach(var sound in e.sounds)add(sound,".wav");}
        foreach(var a in m.animations)
        {
            foreach(var c in a.clips){add(c.texture,".png");using(var image=Image.FromFile(ManifestIO.Asset(Path.Combine(root,"wardrobe"),c.texture)))
                foreach(var f in c.frames)if(f.x+f.width>image.Width||f.y+f.height>image.Height)throw new InvalidDataException(a.id+": frame exceeds image");}
            foreach(var n in a.attachments){add(n.texture,".png");using(var image=Image.FromFile(ManifestIO.Asset(Path.Combine(root,"wardrobe"),n.texture)))if(n.x+n.width>image.Width||n.y+n.height>image.Height)throw new InvalidDataException(n.id+": attachment exceeds image");}
            if(a.material.Length>0&&!m.materials.Any(v=>v.id==a.material))throw new InvalidDataException(a.id+": missing material");
        }
        foreach(var material in m.materials){add(material.texture,".png");add(material.mask,".png");add(material.shader,".mgfxo");}
        foreach(var fallback in Fallbacks(root))
        {
            using(var image=Image.FromFile(ManifestIO.Asset(root,fallback+".png")))
            {
                var sprites=new JumpKing.JKMemory.KingSprites(null);
                var field=typeof(JumpKing.JKMemory.IKingSpriteGroup).GetField("m_key_sprites",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(var group in sprites.m_groups)foreach(var sprite in ((IDictionary<int,JumpKing.Sprite>)field.GetValue(group)).Values)
                    if(sprite.source.Right>image.Width||sprite.source.Bottom>image.Height)throw new InvalidDataException("Fallback atlas does not contain all native poses: "+fallback);
            }
        }
        if(File.Exists(Path.Combine(root,"workshop-preview.png")))files.Add("workshop-preview.png");
        if(File.Exists(Path.Combine(root,"README.md")))files.Add("README.md");
        if(File.Exists(Path.Combine(root,"LICENSE.md")))files.Add("LICENSE.md");
        return files;
    }
    private static void PackTexture(string source,string target)
    {
        using(var image=new Bitmap(source))using(var data=new MemoryStream())using(var writer=new BinaryWriter(data,Encoding.UTF8))
        {
            writer.Write((byte)1);writer.Write("Microsoft.Xna.Framework.Content.Texture2DReader");writer.Write(0);writer.Write((byte)0);writer.Write((byte)1);
            writer.Write(0);writer.Write(image.Width);writer.Write(image.Height);writer.Write(1);writer.Write(image.Width*image.Height*4);
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var c=image.GetPixel(x,y);writer.Write((byte)(c.R*c.A/255));writer.Write((byte)(c.G*c.A/255));writer.Write((byte)(c.B*c.A/255));writer.Write(c.A);}
            using(var output=new BinaryWriter(File.Create(target))){output.Write(new byte[]{88,78,66,119,5,0});output.Write((int)data.Length+10);output.Write(data.ToArray());}
        }
    }
    private static void Layout(string path)
    {
        var result=new List<object>();var sprites=new JumpKing.JKMemory.KingSprites(null);
        var field=typeof(JumpKing.JKMemory.IKingSpriteGroup).GetField("m_key_sprites",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(var group in sprites.m_groups)foreach(var frame in (IDictionary<int,JumpKing.Sprite>)field.GetValue(group))
            result.Add(new{group=group.GetType().Name,key=frame.Key,x=frame.Value.source.X,y=frame.Value.source.Y,width=frame.Value.source.Width,height=frame.Value.source.Height,originX=frame.Value.center.X,originY=frame.Value.center.Y});
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,new JavaScriptSerializer().Serialize(result));
    }
}
