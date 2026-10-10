using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WorldsmithExtension
{
    internal static class Files
    {
        internal static bool IgnoredDirectory(string name)
        {
            return new[] { "bin", "obj", "temp", ".git", ".vs", ".worldsmith-extension", "saves", "savesperma" }.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        internal static bool IgnoredFile(string name)
        {
            return name.EndsWith(".temp", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".worldsmith-backup", StringComparison.OrdinalIgnoreCase) || name.Equals("worldsmith-extension.xml", StringComparison.OrdinalIgnoreCase);
        }

        internal static void RequirePhysicalPath(string path)
        {
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked project paths are not supported: " + current);
        }

        internal static void PrepareOutput(string root, string output)
        {
            RequirePhysicalPath(root);
            RequirePhysicalPath(output);
            root = Path.GetFullPath(root).TrimEnd('\\', '/');
            output = Path.GetFullPath(output).TrimEnd('\\', '/');
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(root);
            if (String.Equals(root, output, StringComparison.OrdinalIgnoreCase) || output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a new empty destination outside the original project.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Choose a new empty destination.");
        }

        internal static string Hash(string path)
        {
            using (var h = SHA256.Create())
                using (var s = File.OpenRead(path))
                    return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "");
        }

        internal static void Atomic(string path, Action<Stream> write)
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".temp";
            try
            {
                using (var s = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(s);
                    s.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temp, path, path + ".worldsmith-backup");
                else
                    File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        internal static void Text(string path, string value)
        {
            Atomic(path, s =>
            {
                byte[] b = Encoding.UTF8.GetBytes(value);
                s.Write(b, 0, b.Length);
            });
        }

        internal static XDocument Xml(string path)
        {
            using (var r = XmlReader.Create(path, new XmlReaderSettings{DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null}))
                return XDocument.Load(r);
        }

        internal static string Relative(string root, string path)
        {
            root = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Path leaves the project: " + path);
            return path.Substring(root.Length);
        }

        internal static IEnumerable<string> Sources(string root)
        {
            RequirePhysicalPath(root);
            return SourceFiles(new DirectoryInfo(root));
        }

        static IEnumerable<string> SourceFiles(DirectoryInfo root)
        {
            // GetFiles already gives us the attributes
            // don't stat each file and walk its parents again
            foreach (var file in root.GetFiles().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (IgnoredFile(file.Name))
                    continue;
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked files are not supported: " + file.FullName);
                yield return file.FullName;
            }

            foreach (var d in SourceChildren(root))
                foreach (string p in SourceFiles(d))
                    yield return p;
        }

        static IEnumerable<DirectoryInfo> SourceChildren(DirectoryInfo root)
        {
            foreach (var d in root.GetDirectories().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if ((d.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked directories are not supported in a publish source: " + d.FullName);
                if (IgnoredDirectory(d.Name))
                    continue;
                yield return d;
            }
        }

        internal static IEnumerable<string> SourceDirectories(string root)
        {
            RequirePhysicalPath(root);
            return SourceDirectories(new DirectoryInfo(root));
        }

        static IEnumerable<string> SourceDirectories(DirectoryInfo root)
        {
            foreach (var directory in SourceChildren(root))
            {
                yield return directory.FullName;
                foreach (string child in SourceDirectories(directory))
                    yield return child;
            }
        }

        internal static void CopyDirectories(string root, string output)
        {
            foreach (string directory in SourceDirectories(root))
            {
                string target = Path.Combine(output, Relative(root, directory));
                RequirePhysicalPath(target);
                Directory.CreateDirectory(target);
            }
        }

        internal static string Fingerprint(string root)
        {
            var value = new StringBuilder();
            foreach (string directory in SourceDirectories(root))
                value.Append(Relative(root, directory)).Append(":directory\n");
            foreach (string f in Sources(root))
                value.Append(Relative(root, f)).Append(':').Append(Hash(f)).Append('\n');
            string config = Path.Combine(root, "worldsmith-extension.xml");
            if (File.Exists(config))
            {
                RequirePhysicalPath(config);
                value.Append("worldsmith-extension.xml:").Append(Hash(config)).Append('\n');
            }

            using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()))).Replace("-", "");
        }

        static IEnumerable<string> ContentEntries(string root)
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked build content is not supported: " + root);
            foreach (string path in Directory.EnumerateFileSystemEntries(root))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked build content is not supported: " + path);
                yield return path;
                if ((attributes & FileAttributes.Directory) != 0)
                    foreach (string child in ContentEntries(path))
                        yield return child;
            }
        }

        internal static string ContentFingerprint(string root)
        {
            var text = new StringBuilder();
            foreach (string path in ContentEntries(root).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked build content is not supported: " + path);
                text.Append(Relative(root, path)).Append(':').Append((attributes & FileAttributes.Directory) != 0 ? "directory" : Hash(path)).Append('\n');
            }

            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
    }
}
