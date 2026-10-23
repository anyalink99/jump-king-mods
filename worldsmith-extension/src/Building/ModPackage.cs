using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class ModPackage
    {
        internal static bool IsSourceProject(string root)
        {
            return Directory.Exists(root) && Directory.GetFiles(root, "*.csproj").Length == 1;
        }

        static string Compile(string root, string output, Action<string> status)
        {
            string project = Directory.GetFiles(root, "*.csproj").Single();
            string compiled = Path.Combine(Path.GetDirectoryName(output), "compiled-mod");
            if (Directory.Exists(compiled))
                throw new IOException("The compiler destination must be new.");
            status("Compiling the C# mod project in Release. A compatible .NET SDK is required.");
            WorkerProcess.Run("dotnet", "build " + WorkerProcess.Quote(project) + " -c Release --output " + WorkerProcess.Quote(compiled), root, status, "C# compilation failed. See the compiler output; nothing is ready to upload.");
            return compiled;
        }

        internal static void Validate(string root)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Select a physical package directory.");
            bool assembly = false;
            foreach (string file in Files.Sources(root))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (new[]{".cs", ".csproj", ".sln", ".user", ".suo"}.Contains(ext))
                    throw new InvalidDataException("Select the prepared release package, not the source project: " + Files.Relative(root, file));
                if (ext == ".dll" && Path.GetDirectoryName(file) == Path.GetFullPath(root).TrimEnd('\\'))
                {
                    try
                    {
                        AssemblyName.GetAssemblyName(file);
                        assembly = true;
                    }
                    catch (BadImageFormatException)
                    { /* native dependencies may accompany a managed mod */
                    }
                }
            }

            if (!assembly)
                throw new InvalidDataException("The package needs a compiled managed mod DLL at its root.");
        }

        internal static void Build(string root, string output, Action<string> status)
        {
            root = Path.GetFullPath(root).TrimEnd('\\');
            output = Path.GetFullPath(output);
            Files.PrepareOutput(root, output);
            string revision = Files.Fingerprint(root);
            string payload = IsSourceProject(root) ? Compile(root, output, status) : root;
            Validate(payload);
            Directory.CreateDirectory(output);
            Files.CopyDirectories(payload, output);
            foreach (string file in Files.Sources(payload))
            {
                string relative = Files.Relative(payload, file), target = Path.Combine(output, relative);
                status("Packaging mod: " + relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string hash = Files.Hash(file);
                File.Copy(file, target, false);
                if (Files.Hash(target) != hash)
                    throw new IOException("Package changed while copying: " + relative);
            }

            BuildReceipt.Save(root, output, revision);
            status("Mod package ready: " + output);
        }
    }
}
