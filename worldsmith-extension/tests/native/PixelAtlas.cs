using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Media.Imaging;

internal static class PixelAtlas
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    internal static void Run(Assembly engine,Assembly native,string fixtures)
    {
        foreach(int columns in new[]{13,20})
        {
            int screens=columns*13;
            string root=Path.Combine(fixtures,"pixel-"+columns),output=Path.Combine(fixtures,"pixel-build-"+columns,"content");
            Directory.CreateDirectory(root);
            foreach(string folder in new[]{"audio","ending","king","particles","props","screens","gui","props/textures","props/mega-mapping-expansion"})Directory.CreateDirectory(Path.Combine(root,folder));
            foreach(string file in new[]{"level_settings.xml","gui/location_settings.xml","gui/earthquake_settings.xml","props/textures/prop_settings.xml","particles/weather.xml"})File.WriteAllText(Path.Combine(root,file),"<Settings/>");
            string metadata=Path.Combine(root,"mapping_settings.xml");
            File.WriteAllText(metadata,"<MappingSettings version='1'><MapLayout version='2' screens='"+screens+"' columns='"+columns+"' rows='13' cellWidth='480' cellHeight='360' source='atlas'>"+(columns==13?"<Resolution screen='1' width='60' height='45'/>":"")+"</MapLayout></MappingSettings>");
            using(var blank=new Bitmap(780,585))blank.Save(Path.Combine(root,"level.png"));
            string source=Path.Combine(root,"level+.png");
            using(var image=new Bitmap(columns*480,13*360))
            {
                for(int screen=0;screen<screens;screen++)
                {
                    int x=(screen/13)*480,y=(screen%13)*360;
                    if(columns==13&&screen==0)
                    {
                        using(var graphics=Graphics.FromImage(image))
                        {graphics.FillRectangle(Brushes.Black,16,24,8,8);graphics.FillRectangle(Brushes.Cyan,208,200,8,8);}
                        continue;
                    }
                    image.SetPixel(x+17,y+23,Color.Black);image.SetPixel(x+18,y+23,Color.Magenta);
                    image.SetPixel(x+30,y+30,Color.Red);image.SetPixel(x+210,y+201,Color.Cyan);
                }
                image.Save(source);
            }
            string before=Hash(source);
            var type=native.GetType("JKWorldsmith.ViewModels.Level.HitboxViewModel");
            object vm=FormatterServices.GetUninitializedObject(type);type.GetField("folder",All).SetValue(vm,root);
            type.GetMethod("InitializeHitbox",All).Invoke(vm,null);
            var frames=(CroppedBitmap[])type.GetProperty("HitboxFrames").GetValue(vm,null);
            if(frames.Length!=screens||frames[screens-1].SourceRect.X!=(columns-1)*480||frames[screens-1].SourceRect.Y!=12*360||frames[0].SourceRect.Width!=480)
                throw new Exception("Pixel atlas preview has wrong source rectangles or count");
            var previewMethod=engine.GetType("WorldsmithExtension.CollisionPatches").GetMethod("Preview",All);
            object[] previewArgs={vm,new Rectangle((columns-1)*480,12*360,480,360),null};
            previewMethod.Invoke(null,previewArgs);
            using(var preview=(Bitmap)previewArgs[2])
                if(preview.GetPixel(17,23).ToArgb()!=Color.Black.ToArgb()||preview.GetPixel(18,23).A!=0||preview.GetPixel(210,201).ToArgb()!=Color.Cyan.ToArgb())
                    throw new Exception("Pixel preview lost exact collision, colour key or material");
            if(columns==13)
            {
                object[] coarseArgs={vm,new Rectangle(0,0,480,360),null};previewMethod.Invoke(null,coarseArgs);
                using(var preview=(Bitmap)coarseArgs[2])if(preview.GetPixel(16,24).ToArgb()!=Color.Black.ToArgb()||preview.GetPixel(23,31).ToArgb()!=Color.Black.ToArgb()||preview.GetPixel(24,31).A!=0)
                    throw new Exception("Per-screen resolution preview lost coarse geometry in the common atlas");
            }
            type.GetMethod("GenerateInGameLevelHitbox",All).Invoke(vm,null);
            ((Bitmap)type.GetProperty("GdiImage").GetValue(vm,null)).Dispose();
            if(Hash(source)!=before)throw new Exception("Native generation changed level+.png");
            engine.GetType("WorldsmithExtension.Builder").GetMethod("Build",All).Invoke(null,new object[]{root,output,new Action<string>(text=>{})});
            var layout=engine.GetType("WorldsmithExtension.Layout").GetMethod("Read",All).Invoke(null,new object[]{output,169});
            engine.GetType("WorldsmithExtension.Collision").GetMethod("ValidatePixelXnb",All).Invoke(null,new object[]{Path.Combine(output,"level+.xnb"),layout});
            if(File.ReadAllText(Path.Combine(output,"mapping_settings.xml"))!=File.ReadAllText(metadata))
            {
                var doc=System.Xml.Linq.XDocument.Load(Path.Combine(output,"mapping_settings.xml"));
                if((int)doc.Root.Element("MapLayout").Attribute("version")!=2||(int)doc.Root.Element("MapLayout").Attribute("screens")!=screens)throw new Exception("Build changed pixel metadata");
            }
            if(Hash(source)!=before)throw new Exception("Checked build changed pixel authoring source");
        }
        string legacy=Path.Combine(fixtures,"legacy-sizes");Directory.CreateDirectory(legacy);
        using(var scaffold=new Bitmap(780,585))scaffold.Save(Path.Combine(legacy,"level.png"));
        using(var image=new Bitmap(180,135))using(var sizes=new Bitmap(180,135))
        {image.SetPixel(10,10,Color.Black);sizes.SetPixel(10,10,Color.FromArgb(255,19,2,4));image.Save(Path.Combine(legacy,"level+.png"));sizes.Save(Path.Combine(legacy,"sizes.png"));}
        var legacyType=native.GetType("JKWorldsmith.ViewModels.Level.HitboxViewModel");
        object legacyVM=FormatterServices.GetUninitializedObject(legacyType);legacyType.GetField("folder",All).SetValue(legacyVM,legacy);
        legacyType.GetMethod("InitializeHitbox",All).Invoke(legacyVM,null);
        var legacyFrames=(CroppedBitmap[])legacyType.GetProperty("HitboxFrames").GetValue(legacyVM,null);
        if(legacyFrames.Length!=9)throw new Exception("Legacy level+ preview inferred its count from stock scaffold");
        object[] legacyPreview={legacyVM,new Rectangle(0,8*45,60,45),null};
        engine.GetType("WorldsmithExtension.CollisionPatches").GetMethod("Preview",All).Invoke(null,legacyPreview);
        using(var image=(Bitmap)legacyPreview[2])
            if(image.GetPixel(83,82).ToArgb()!=Color.Black.ToArgb()||image.GetPixel(82,82).A!=0||image.GetPixel(85,82).A!=0)
                throw new Exception("Legacy sizes preview did not apply offset, width and transparency");
        ((Bitmap)legacyType.GetProperty("GdiImage").GetValue(legacyVM,null)).Dispose();
        Slopes(engine,native);
        Console.WriteLine("[OK] 13x13 and 20x13 pixel atlases: native preview, source preservation, checked build and CPU XNB validation.");
    }
    private static void Slopes(Assembly engine,Assembly native)
    {
        var detect=native.GetType("JKWorldsmith.Shared.SlopeCompatibleBlocks").GetMethod("GetSlopeType",All);
        var indication=native.GetType("JKWorldsmith.Models.SlopeIndication");
        var draw=engine.GetType("WorldsmithExtension.CollisionPatches").GetMethod("DrawSlope",All);
        var kinds=new HashSet<string>();
        for(int mask=0;mask<16;mask++)using(var sample=new Bitmap(62,47))
        {
            sample.SetPixel(11,11,Color.Red);
            var neighbours=new[]{new Point(10,11),new Point(12,11),new Point(11,10),new Point(11,12)};
            for(int i=0;i<4;i++)if((mask&(1<<i))!=0)sample.SetPixel(neighbours[i].X,neighbours[i].Y,Color.Black);
            object kind=detect.Invoke(null,new object[]{sample,11,11});
            if(kind.ToString()=="None")continue;
            kinds.Add(kind.ToString());
            foreach(int width in new[]{1,3,8})using(var expected=new Bitmap(32,32))using(var actual=new Bitmap(32,32))
            {
                var target=new Rectangle(12,13,width,5);
                using(var graphics=Graphics.FromImage(expected))
                {
                    graphics.TranslateTransform(target.X-10*target.Width,target.Y-10*target.Height);
                    graphics.ScaleTransform(target.Width/8f,target.Height/8f);
                    indication.GetMethod("DrawSlope").Invoke(Activator.CreateInstance(indication,new[]{(object)10,10,kind}),new object[]{graphics});
                }
                using(var graphics=Graphics.FromImage(actual))
                {
                    var before=graphics.Transform.Elements;
                    if(!(bool)draw.Invoke(null,new object[]{graphics,sample,11,11,target})||!before.SequenceEqual(graphics.Transform.Elements))
                        throw new Exception("Slope preview changed the caller's transform");
                }
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(actual.GetPixel(x,y).ToArgb()!=expected.GetPixel(x,y).ToArgb())
                    throw new Exception("Scaled slope preview differs from native geometry: "+kind+" width "+width);
            }
        }
        if(kinds.Count!=4)throw new Exception("Native slope preview did not cover all four orientations");
    }
    private static string Hash(string path)
    {using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file));}
}
