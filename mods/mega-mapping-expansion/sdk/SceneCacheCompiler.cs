using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using MegaMappingExpansion.Construction;

namespace MegaMappingExpansion
{
    internal static class SceneCacheCompiler
    {
        private static int Main(string[] args)
        {
            if(args.Length!=2){Console.Error.WriteLine("Usage: SceneCacheCompiler <level-root> <output.mmgfx> | --validate <level-root> | --reference <directory> | --schema <output.xsd>");return 2;}
            try
            {
                if(args[0]=="--reference"){Reference(Path.GetFullPath(args[1]));return 0;}
                if(args[0]=="--schema")
                {
                    var schemas=new XmlSchemas();new XmlSchemaExporter(schemas).ExportTypeMapping(new XmlReflectionImporter().ImportTypeMapping(typeof(SceneDocument)));
                    using(var writer=XmlWriter.Create(args[1],new XmlWriterSettings{Indent=true}))schemas[0].Write(writer);return 0;
                }
                bool validate=args[0]=="--validate";string root=Path.GetFullPath(validate?args[1]:args[0]);
                var layout=MapLayout.Read(root);CollisionShapes.Read(root);var platforms=MovingPlatformPlan.Read(root,layout==null?4096:layout.Screens);var endings=Endings.NativeEndings.Read(root);
                ValidatePlatforms(root,layout,platforms);
                foreach(var profile in platforms.Profiles.Values.Where(p=>p.Texture!=""&&p.Texture!="none"))if(!File.Exists(NativeAssets.Resolve(root,profile.Texture)))throw new FileNotFoundException("Platform texture is missing",profile.Texture);
                var document=new RecipeCompiler(PrimitiveRegistry.Create()).Read(root);
                NativeMovingPlatforms.ValidateTargets(platforms,document,File.Exists(Path.Combine(root,layout!=null&&layout.Pixel?"level+.xnb":"level.xnb")));
                if(document==null)
                {
                    if(layout==null&&!File.Exists(Path.Combine(root,MovingPlatformPlan.PathName)))throw new FileNotFoundException("mapping_settings.xml is missing",root);
                    Endings.EndingDocument.ValidateScene(endings.Values,null);Console.WriteLine("[OK] Native routes without scene resources: "+root);return 0;
                }
                using(var program=new SceneProgram(document)){}
                Endings.EndingDocument.ValidateScene(endings.Values,document);
                ValidateAssets(root,document);
                if(!validate)ConstructionSceneFile.Compile(root,args[1]);
                Console.WriteLine("[OK] Construction scene: "+root+"; components="+document.Components.Length+", graph="+document.Graph.Length+", rules="+document.Rules.Length);return 0;
            }
            catch(Exception error){Console.Error.WriteLine(error);return 1;}
        }
        private static void ValidateAssets(string root,SceneDocument document)
        {
            using(var program=new SceneProgram(document))foreach(var component in program.Components.Values.Where(c=>c.Descriptor.Kind=="audio"))
            {
                string path=NativeAssets.Resolve(root,program.Properties.Read(component.Id,"path").Text);
                if(new FileInfo(path).Length>64L*1024*1024)throw new InvalidDataException(component.Id+": sound exceeds 64 MiB");
            }
            long pixels=0;
            foreach(var asset in document.Assets)
            {
                string path=NativeAssets.Resolve(root,asset.Path);int width=0,height=0;
                if(asset.Kind=="vector")
                {
                    using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
                    {var vector=(VectorAssetData)new XmlSerializer(typeof(VectorAssetData)).Deserialize(reader);FiniteNumbers.Validate(vector,"vector");width=vector.Width;height=vector.Height;if(width>2048||height>2048||vector.Supersample<1||vector.Supersample>4)throw new InvalidDataException(asset.Id+": invalid vector dimensions");}
                }
                else if(Path.GetExtension(path).Equals(".xnb",StringComparison.OrdinalIgnoreCase))CollisionXnb.ReadTexture(path,(reader,w,h)=>{width=w;height=h;reader.BaseStream.Seek((long)w*h*4,SeekOrigin.Current);});
                else using(var image=System.Drawing.Image.FromFile(path)){width=image.Width;height=image.Height;}
                pixels+=(long)width*height;
                if(width<1||height<1||width>16384||height>16384||pixels>512L*1024*1024/8)throw new InvalidDataException(asset.Id+": texture budget exceeded");
                if(width%asset.Columns!=0||height%asset.Rows!=0)throw new InvalidDataException(asset.Id+": atlas dimensions must divide into its grid");
            }
        }
        private static void ValidatePlatforms(string root,MapLayout layout,MovingPlatformPlan platforms)
        {
            string source=Path.Combine(root,layout!=null&&layout.Pixel?"level+.xnb":"level.xnb");
            if(File.Exists(source))
            {
                uint[] pixels=null;int width=0,height=0;
                CollisionXnb.ReadTexture(source,(reader,w,h)=>{width=w;height=h;pixels=new uint[checked(w*h)];for(int i=0;i<pixels.Length;i++){uint c=CollisionXnb.Colour(reader.ReadUInt32());pixels[i]=(c&0xff000000U)|((c&255)<<16)|(c&0xff00)|((c>>16)&255);}});
                var format=layout!=null&&layout.Pixel?layout:new MapLayout{Source="atlas",Screens=layout==null?width/60*(height/45):layout.Screens,Columns=width/60,Rows=height/45,CellWidth=60,CellHeight=45};
                format.ValidatePixel();if(width!=format.ImageWidth||height!=format.ImageHeight)throw new InvalidDataException("Platform collision source dimensions differ from MapLayout");
                platforms.AddPainted(PlatformImageScan.Collect(platforms,format,(x,y)=>pixels[y*width+x]));PlatformImageScan.ValidateBounds(platforms,format.Screens);
            }
            else PlatformImageScan.ValidateBounds(platforms,layout==null?4096:layout.Screens);
        }
        private static void Reference(string directory)
        {
            Directory.CreateDirectory(directory);var root=new XElement("SceneCatalog",new XAttribute("version",1));
            var markdown=new StringBuilder("# Construction reference\n\nGenerated from the component registry and embedded recipes. Structural fields require resource preparation; other fields accept typed graph bindings and owned effect contributions.\n\n");
            foreach(var descriptor in PrimitiveRegistry.Create().All)
            {
                var component=new XElement("Component",new XAttribute("kind",descriptor.Kind),new XAttribute("scope",descriptor.Scope));root.Add(component);
                markdown.Append("## ").Append(descriptor.Kind).Append("\n\n| Property | Type | Default | Units | Bindable | Impact | Choices |\n|---|---|---|---|---|---|---|\n");
                foreach(var field in descriptor.Fields)
                {
                    component.Add(new XElement("Field",new XAttribute("name",field.Name),new XAttribute("kind",field.Kind),new XAttribute("default",field.Default),new XAttribute("min",field.Minimum),new XAttribute("max",field.Maximum),new XAttribute("units",field.Units),new XAttribute("bindable",field.Bindable),new XAttribute("impact",field.Impact),new XAttribute("reference",field.Reference),new XAttribute("choices",string.Join(";",field.Choices))));
                    markdown.Append("| ").Append(field.Name).Append(" | ").Append(field.Kind).Append(" | ").Append(field.Default).Append(" | ").Append(field.Units).Append(" | ").Append(field.Bindable?"yes":"no").Append(" | ").Append(field.Impact).Append(" | ").Append(string.Join(", ",field.Choices)).Append(" |\n");
                }
                markdown.AppendLine();
            }
            var operations=new XElement("GraphOperations");root.Add(operations);markdown.Append("## Graph operations\n\n");
            foreach(var op in Enum.GetNames(typeof(ValueOperation))){operations.Add(new XElement("Operation",new XAttribute("id",op.ToLowerInvariant())));markdown.Append('`').Append(op.ToLowerInvariant()).Append("` ");}
            markdown.Length--;markdown.Append("\n\n## Recipes\n\nRecipes expand to ordinary components, graph nodes and bindings. They have no dedicated runtime objects.\n\n");var recipes=new XElement("Recipes");root.Add(recipes);
            foreach(string resource in typeof(RecipeCompiler).Assembly.GetManifestResourceNames().Where(n=>n.StartsWith("MegaMapping.Recipes.",StringComparison.Ordinal)).OrderBy(n=>n,StringComparer.Ordinal))
                using(var stream=typeof(RecipeCompiler).Assembly.GetManifestResourceStream(resource))foreach(var recipe in XDocument.Load(stream).Root.Elements())
                {recipes.Add(new XElement(recipe));markdown.Append("### ").Append((string)recipe.Attribute("id")).Append("\n\n");foreach(var param in recipe.Element("Parameters").Elements())markdown.Append("- `").Append((string)param.Attribute("name")).Append("` (").Append((string)param.Attribute("kind")).Append("): ").Append(param.Attribute("default")==null?"required":"default `"+(string)param.Attribute("default")+"`").Append("\n");markdown.AppendLine();}
            new XDocument(root).Save(Path.Combine(directory,"scene-parameters.xml"));File.WriteAllText(Path.Combine(directory,"PARAMETERS_GENERATED.md"),markdown.ToString(),new UTF8Encoding(false));
            Console.WriteLine("[OK] Registry and recipe reference: "+directory);
        }
    }
}
