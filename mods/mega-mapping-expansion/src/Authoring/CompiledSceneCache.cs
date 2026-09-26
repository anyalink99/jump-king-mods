using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MegaMappingExpansion
{
    internal static class CompiledSceneCache
    {
        internal const string RelativeCachePath = "props/mega-mapping-expansion/scene.mmgfx";
        internal const string CompilerVersion = "MMGFX3-vector-2026-09-08";
        private const string Magic = "MMGFX3";
        private const int MaximumAssetBytes = 32 * 1024 * 1024;
        private const int MaximumTotalBytes = 256 * 1024 * 1024;

        internal static bool TryLoad(string root, SceneFile scene, out Dictionary<string, byte[]> assets)
        {
            assets = null;
            string path = Path.Combine(Path.GetFullPath(root), RelativeCachePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return false;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadString() != Magic) throw new InvalidDataException("Unsupported cache format");
                    string expected = SourceHash(root, scene);
                    if (!string.Equals(reader.ReadString(), expected, StringComparison.Ordinal)) throw new InvalidDataException("Cache does not match source files or compiler; rebuild scene.mmgfx, or explicitly remove it to use source preparation");
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 4096) throw new InvalidDataException("Invalid cache asset count");
                    Dictionary<string, byte[]> loaded = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                    int total = 0;
                    for (int i = 0; i < count; i++)
                    {
                        string id = reader.ReadString();
                        int length = reader.ReadInt32();
                        total += length;
                        if (string.IsNullOrWhiteSpace(id) || length < 1 || length > MaximumAssetBytes || total > MaximumTotalBytes)
                            throw new InvalidDataException("Invalid cache asset size or ID");
                        byte[] png = reader.ReadBytes(length);
                        if (png.Length != length || loaded.ContainsKey(id)) throw new InvalidDataException("Truncated or duplicate cache asset: " + id);
                        loaded.Add(id, png);
                    }
                    foreach (VectorAssetData asset in scene.VectorAssets ?? new VectorAssetData[0])
                        if (!loaded.ContainsKey(asset.Id)) throw new InvalidDataException("Missing cache asset: " + asset.Id);
                    if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected bytes after cache assets");
                    assets = loaded;
                    return true;
                }
            }
            catch (Exception error) { throw new InvalidDataException(path + ": " + error.Message, error); }
        }

        internal static void Write(string root, SceneFile scene, string output)
        {
            string full = Path.GetFullPath(output);
            string parent = Path.GetDirectoryName(full);
            if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
            string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
            using (FileStream stream = File.Create(temporary))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(SourceHash(root, scene));
                VectorAssetData[] vectors = scene.VectorAssets ?? new VectorAssetData[0];
                writer.Write(vectors.Length);
                foreach (VectorAssetData asset in vectors)
                {
                    byte[] png = VectorGraphics.RenderPngBytes(asset);
                    if (png.Length > MaximumAssetBytes) throw new InvalidDataException("Compiled vector asset is too large: " + asset.Id);
                    writer.Write(asset.Id);
                    writer.Write(png.Length);
                    writer.Write(png);
                }
            }
            if (File.Exists(full)) File.Replace(temporary, full, null);
            else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static string SourceHash(string root, SceneFile scene)
        {
            using (SHA256 hash = SHA256.Create())
            using (CryptoStream aggregate = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write))
            {
                byte[] version = Encoding.UTF8.GetBytes(CompilerVersion + "/" + CompilerIdentity() + "\n" + SceneAuthoring.Expand(Path.Combine(Path.GetFullPath(root),
                    SceneValidation.RelativeScenePath.Replace('/', Path.DirectorySeparatorChar))).OuterXml);
                aggregate.Write(version, 0, version.Length);
                SortedSet<string> files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (VectorAssetData asset in scene.VectorAssets ?? new VectorAssetData[0])
                {
                    if (string.IsNullOrWhiteSpace(asset.Source)) continue;
                    files.Add(SceneValidation.ResolveVectorSource(root, asset.Source));
                }
                foreach (string file in files) AppendFile(aggregate, file, Path.GetFullPath(root));
                aggregate.FlushFinalBlock();
                return Convert.ToBase64String(hash.Hash);
            }
        }

        private static string CompilerIdentity()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MegaMapping.CompilerIdentity"))
            {
                if (stream == null) throw new InvalidOperationException("Compiler identity resource is missing from the Mapping build");
                using (StreamReader reader = new StreamReader(stream)) return reader.ReadToEnd().Trim();
            }
        }

        private static void AppendFile(Stream output, string path, string root)
        {
            byte[] name = Encoding.UTF8.GetBytes(path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/').ToLowerInvariant());
            output.Write(name, 0, name.Length);
            output.WriteByte(0);
            byte[] size = BitConverter.GetBytes(new FileInfo(path).Length);
            output.Write(size, 0, size.Length);
            using (FileStream input = File.OpenRead(path)) input.CopyTo(output);
            output.WriteByte(0xFF);
        }
    }
}
