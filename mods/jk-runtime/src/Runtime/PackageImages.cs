using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;

namespace JKRuntime
{
    // reuse verified file images without adding native *.dll discoveries
    // dependencies still belong beside the original package shell
    internal static class PackageImages
    {
        private static readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);

        internal static Assembly Load(byte[] bytes, string root)
        {
            string hash;
            using (var stream = new MemoryStream(bytes, false)) hash = Hash(stream);
            Assembly existing;
            if (loaded.TryGetValue(hash, out existing)) return existing;
            Assembly result;
            try { result = LoadFile(bytes, hash, root); }
            catch (IOException) { result = Assembly.Load(bytes); }
            catch (UnauthorizedAccessException) { result = Assembly.Load(bytes); }
            catch (SecurityException) { result = Assembly.Load(bytes); }
            loaded.Add(hash, result);
            return result;
        }

        private static Assembly LoadFile(byte[] bytes, string hash, string root)
        {
            string directory = Path.Combine(root, hash);
            string path = Path.GetFullPath(Path.Combine(directory, "implementation.jkcode"));
            Directory.CreateDirectory(directory);
            if (!File.Exists(path))
            {
                string pending = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        file.Write(bytes, 0, bytes.Length);
                    try { File.Move(pending, path); }
                    catch (IOException) { if (!File.Exists(path)) throw; }
                }
                finally { if (File.Exists(pending)) File.Delete(pending); }
            }
            // never trust old cache bytes just because the name has the right hash
            using (var file = File.OpenRead(path))
                if (Hash(file) != hash) throw new IOException("Package image cache checksum mismatch");
            var identity = AssemblyName.GetAssemblyName(path);
            // LoadFrom unifies identities. byte loading didn't, so retain the old
            // isolation when another assembly already owns this identity
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName == identity.FullName))
                return Assembly.Load(bytes);
            var result = Assembly.LoadFrom(path);
            if (!string.Equals(result.Location, path, StringComparison.OrdinalIgnoreCase))
                return Assembly.Load(bytes);
            return result;
        }

        private static string Hash(Stream stream)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
