using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal sealed class PackageFile
    {
        internal string Hash;
        internal long Size;
    }

    internal static class PackageManifest
    {
        internal const string Name = "package-manifest.xml";
        internal const long MaxExpanded = 256L * 1024 * 1024;
        internal static void CheckName(string name)
        {
            if (String.IsNullOrEmpty(name) || name.Length > 200 || name.IndexOfAny(new[]{'\\', ':', '*', '?', '"', '<', '>', '|'}) >= 0 || name.Any(Char.IsControl))
                throw new InvalidDataException("Unsafe package path.");
            foreach (string part in name.Split('/'))
                if (part == "" || part == "." || part == ".." || part.EndsWith(" ") || part.EndsWith(".") || Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Unsafe package path: " + name);
            if (new[]{"verified.xml", "worldsmith-path.txt", "game-path.txt"}.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Local settings aren't release files: " + name);
        }

        internal static Dictionary<string, PackageFile> Read(Stream input, string version)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 };
            XElement root;
            using (var reader = XmlReader.Create(input, settings)) root = XDocument.Load(reader).Root;
            if (root == null || root.Name != "Package" || (string)root.Attribute("version") != "1" || (string)root.Attribute("release") != version)
                throw new InvalidDataException("Unsupported package manifest or release version.");
            var files = new Dictionary<string, PackageFile>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var item in root.Elements())
            {
                string name = (string)item.Attribute("name"), hash = (string)item.Attribute("sha256");
                long size;
                CheckName(name);
                if (item.Name != "File" || item.HasElements || name.Equals(Name, StringComparison.OrdinalIgnoreCase) || files.ContainsKey(name) || files.Count >= 4096 || hash == null || !Regex.IsMatch(hash, "^[a-f0-9]{64}$") || !Int64.TryParse((string)item.Attribute("size"), out size) || size < 0 || size > MaxExpanded)
                    throw new InvalidDataException("Invalid package file entry: " + name);
                total += size;
                if (total > MaxExpanded) throw new InvalidDataException("Package contents exceed the size limit.");
                files.Add(name, new PackageFile { Hash = hash, Size = size });
            }
            if (Updates.Required.Any(p => !files.ContainsKey(p) || files[p].Size == 0)) throw new InvalidDataException("The package manifest omits a required application file.");
            foreach (string name in files.Keys)
                for (int slash = name.IndexOf('/'); slash >= 0; slash = name.IndexOf('/', slash + 1))
                    if (files.ContainsKey(name.Substring(0, slash))) throw new InvalidDataException("A package file is also a directory: " + name);
            return files;
        }
    }
}
