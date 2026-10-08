using System;
using System.IO;

namespace MegaMappingExpansion
{
    internal static class SceneCacheCompiler
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: SceneCacheCompiler <level-root> <output.mmgfx> | --validate <level-root> | --schema <output.xsd>");
                return 2;
            }
            try
            {
                if (args[0] == "--reference") { SceneContract.Write(Path.GetFullPath(args[1])); Console.WriteLine("[OK] Scene contract exported"); return 0; }
                if (args[0] == "--schema")
                {
                    var schemas = new System.Xml.Serialization.XmlSchemas();
                    var mapping = new System.Xml.Serialization.XmlReflectionImporter().ImportTypeMapping(typeof(SceneFile));
                    new System.Xml.Serialization.XmlSchemaExporter(schemas).ExportTypeMapping(mapping);
                    using (var writer = System.Xml.XmlWriter.Create(args[1], new System.Xml.XmlWriterSettings { Indent = true })) schemas[0].Write(writer);
                    Console.WriteLine("[OK] Expanded scene schema: " + Path.GetFullPath(args[1])); return 0;
                }
                bool validateOnly = args[0] == "--validate";
                if (validateOnly) args[0] = args[1];
                string root = Path.GetFullPath(args[0]);
                var endings = Endings.NativeEndings.Read(root);
                SceneFile scene = SceneValidation.Read(root);
                Endings.EndingDocument.ValidateScene(endings.Values, scene);
                if (validateOnly && scene == null && endings.Count > 0) { Console.WriteLine("[OK] Custom ending files: " + endings.Count); return 0; }
                if (scene == null) throw new FileNotFoundException("Mega Mapping scene.xml was not found", root);
                SceneValidation.Validate(scene, root, Int32.MaxValue, true);
                if (validateOnly) { Console.WriteLine("[OK] Valid scene: " + root + "; effects=" + scene.Effects.Length + ", regions=" + scene.Regions.Length + ", rules=" + scene.Rules.Length + ", light templates=" + scene.LightTemplates.Length); return 0; }
                CompiledSceneCache.Write(root, scene, args[1]);
                Console.WriteLine("[OK] " + CompiledSceneCache.CompilerVersion + " scene cache: " + Path.GetFullPath(args[1]));
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }
    }
}
