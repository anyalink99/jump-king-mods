using System;
using System.IO;
using System.Reflection;

namespace WorldsmithExtension
{
    internal static class NativeCompiler
    {
        static readonly object PipelineGate = new object();
        static object Pipeline
        {
            get
            {
                return Engine.Get(Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "PipelineManager");
            }
        }

        internal static void Configure(string source, string output)
        {
            var field = Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager").GetField("PipelineManager");
            object p = Activator.CreateInstance(field.FieldType, new object[]{source, output, Path.Combine(Path.GetDirectoryName(output), "pipeline-temp")});
            field.SetValue(null, p);
            Engine.Set(p, "CompressContent", false);
            foreach (var pair in new[]{Tuple.Create("Platform", "Windows"), Tuple.Create("Profile", "Reach")})
            {
                var prop = p.GetType().GetProperty(pair.Item1);
                prop.SetValue(p, Enum.Parse(prop.PropertyType, pair.Item2), null);
            }

            Engine.Set(p, "Logger", Activator.CreateInstance(Engine.Type("JKWorldsmith.Models.XNBConverters+MonogameLogger")));
        }

        static void Compile(string source, string output, string importer, string processor, object options)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("Source asset is missing", source);
            lock (PipelineGate)
            {
                object p = Pipeline;
                if (p == null)
                    throw new InvalidOperationException("The content compiler has not been initialized.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                try
                {
                    p.GetType().InvokeMember("BuildContent", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance | BindingFlags.OptionalParamBinding, null, p, new[]{(object)source, output, importer, processor, options});
                }
                catch (TargetInvocationException e)
                {
                    throw new IOException("Compilation failed: " + source, e.InnerException);
                }

                if (!File.Exists(output) || new FileInfo(output).Length == 0)
                    throw new IOException("Compiler produced no output for " + source);
            }
        }

        internal static void NativeTexture(string input, string output)
        {
            object options = Engine.Get(Engine.Type("JKWorldsmith.Models.XNBConverters"), "Texture2DOpaqueDataDictionary");
            Compile(input, output, "TextureImporter", "TextureProcessor", options);
        }

        internal static void NativeSound(string input, string output, string ext)
        {
            object template = Engine.Get(Engine.Type("JKWorldsmith.Models.XNBConverters"), "Texture2DOpaqueDataDictionary");
            var options = Activator.CreateInstance(template.GetType());
            template.GetType().GetMethod("Add", new[]{typeof(string), typeof(object)}).Invoke(options, new object[]{"Quality", "Best"});
            Compile(input, output, ext.ToLowerInvariant() == ".mp3" ? "Mp3Importer" : "WavImporter", "SoundEffectProcessor", options);
        }

    }
}
