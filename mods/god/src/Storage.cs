using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using JKRuntime.UI;

namespace God
{
    public sealed class Preferences
    {
        public int Grid = 8;
        public bool TransferForeground;
        public List<Binding> Bindings = new List<Binding>();
    }
    public sealed class Binding { public int[] Buttons; }
    public sealed class SavedWorld
    {
        public int Version = 1;
        public List<SavedScreen> Screens = new List<SavedScreen>();
    }
    public sealed class SavedScreen
    {
        public int Index;
        public string Baseline, Cells;
    }
    internal static class Storage
    {
        internal static string Root;
        internal static Preferences Preferences;
        internal static void Initialize(string root)
        {
            Root = root; Directory.CreateDirectory(root);
            Preferences = Read<Preferences>(Path.Combine(root, "Settings.xml")) ?? new Preferences { Bindings = new List<Binding> { new Binding { Buttons = new[] { 119 } } } };
            if (Preferences.Grid != 1 && Preferences.Grid != 4 && Preferences.Grid != 8) Preferences.Grid = 8;
            if (Preferences.Bindings == null) Preferences.Bindings = new List<Binding>();
        }
        internal static UiChord[] Chords()
        { return Preferences.Bindings.Where(b => b != null && b.Buttons != null).Select(b => new UiChord(b.Buttons)).ToArray(); }
        internal static void Bind(UiChord[] chords)
        {
            Preferences.Bindings = (chords ?? new UiChord[0]).Where(c => c != null && !c.IsEmpty).Select(c => new Binding { Buttons = c.Buttons }).ToList();
            Write(Path.Combine(Root, "Settings.xml"), Preferences);
        }
        internal static void SetTransferForeground(bool value)
        {
            bool before = Preferences.TransferForeground; Preferences.TransferForeground = value;
            try { Write(Path.Combine(Root, "Settings.xml"), Preferences); }
            catch { Preferences.TransferForeground = before; throw; }
        }
        internal static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return ReadExact<T>(path); }
            catch (Exception error)
            {
                Console.WriteLine("[God] Cannot read " + path + ": " + error.Message);
                if (File.Exists(path + ".bak")) return ReadExact<T>(path + ".bak");
                throw;
            }
        }
        private static T ReadExact<T>(string path) where T : class
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("God data exceeds 16 MB.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 };
            using (var reader = XmlReader.Create(path, settings)) return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }
        internal static void Write<T>(string path, T value)
        {
            string temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { new XmlSerializer(typeof(T)).Serialize(stream, value); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static string Encode(int[] cells)
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new GZipStream(stream, CompressionMode.Compress, true))
                using (var writer = new BinaryWriter(zip))
                {
                    writer.Write(Terrain.Count);
                    for (int i = 0; i < cells.Length;)
                    {
                        int end = i + 1; while (end < cells.Length && cells[end] == cells[i]) end++;
                        writer.Write(end - i); writer.Write(cells[i]); i = end;
                    }
                }
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        internal static int[] Decode(string text, int materialCount)
        {
            if (text == null || text.Length > 2 * 1024 * 1024) throw new InvalidDataException("Invalid God screen payload.");
            using (var stream = new MemoryStream(Convert.FromBase64String(text)))
            using (var zip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(zip))
            {
                if (reader.ReadInt32() != Terrain.Count) throw new InvalidDataException("God screen dimensions changed.");
                var cells = new int[Terrain.Count]; int at = 0;
                while (at < cells.Length)
                {
                    int count = reader.ReadInt32(), material = reader.ReadInt32();
                    if (count < 1 || count > cells.Length - at || material < 0 || material > materialCount) throw new InvalidDataException("Invalid God terrain run.");
                    for (int end = at + count; at < end; at++) cells[at] = material;
                }
                if (zip.ReadByte() != -1) throw new InvalidDataException("Trailing God screen data.");
                return cells;
            }
        }
        internal static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        internal static string WorldKey(string root)
        {
            using (var stream = new MemoryStream())
            {
                byte[] path = Encoding.UTF8.GetBytes(root.ToLowerInvariant()); stream.Write(path, 0, path.Length);
                string level = Path.Combine(root, "level.xnb");
                if (File.Exists(level)) using (var input = File.OpenRead(level)) using (var sha = SHA256.Create())
                { byte[] digest = sha.ComputeHash(input); stream.Write(digest, 0, digest.Length); }
                return Hash(stream.ToArray());
            }
        }
    }
}
