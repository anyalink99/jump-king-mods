using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Serialization;

namespace JKRuntime.Settings
{
    /// <summary>The result of loading a settings file; recovered data stays read-only.</summary>
    public enum SettingsReadStatus { Missing, Loaded, Unreadable, Unsupported, Recovered }

    /// <summary>Loads settings once, preserving unreadable/newer files and detecting external edits before saving.</summary>
    public sealed class SettingsFile<T> where T : class
    {
        private sealed class UnsupportedData : Exception
        { internal UnsupportedData(string message) : base(message) { } }
        private static readonly XmlSerializer Serializer = CreateSerializer();
        private readonly string path;
        private readonly Action<T> validate;
        private byte[] fingerprint;
        public T Value { get; private set; }
        public SettingsReadStatus Status { get; private set; }
        public string Error { get; private set; }
        public bool CanSave { get { return Status == SettingsReadStatus.Missing || Status == SettingsReadStatus.Loaded; } }

        /// <summary>Reads a bounded XML document. The validator may reject unsupported schema versions or invalid data.</summary>
        public SettingsFile(string file, Func<T> defaults, Action<T> validator = null)
        {
            if (defaults == null) throw new ArgumentNullException("defaults");
            path = Path.GetFullPath(file);
            validate = validator;
            Error = "";
            Value = defaults();
            if (Value == null) throw new ArgumentException("Settings defaults cannot be null", "defaults");
            Status = SettingsReadStatus.Missing;
            if (!File.Exists(path)) return;
            try
            {
                byte[] bytes = ReadBytes(path);
                Value = Read(bytes);
                fingerprint = Hash(bytes);
                Status = SettingsReadStatus.Loaded;
            }
            catch (Exception error)
            {
                Exception cause = error.GetBaseException();
                Status = cause is UnsupportedData ? SettingsReadStatus.Unsupported : SettingsReadStatus.Unreadable;
                Error = "Settings left unchanged: " + path + ". " + cause.Message;
                if (File.Exists(path + ".bak"))
                {
                    try { Value = Read(ReadBytes(path + ".bak")); Status = SettingsReadStatus.Recovered; }
                    catch (Exception backupError) { Error += " Backup unavailable: " + backupError.GetBaseException().Message; }
                }
                Error += " Restore or move the original file and restart before saving.";
                Console.WriteLine("[JK Runtime] " + Error);
            }
        }

        /// <summary>Commits a valid value and retains the previous file as .bak; failed writes do not publish the new value.</summary>
        public void Save(T value)
        {
            if (!CanSave) throw new InvalidOperationException(Error);
            if (value == null) throw new ArgumentNullException("value");
            if (validate != null) validate(value);
            byte[] actual = File.Exists(path) ? Hash(ReadBytes(path)) : null;
            if (!Equal(actual, fingerprint))
                throw new IOException("Settings changed outside the game; reload before saving: " + path);
            fingerprint = AtomicXmlFile.SaveWithFingerprint(path, value);
            Value = value;
            Status = SettingsReadStatus.Loaded;
        }

        private T Read(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                T result = Serializer.Deserialize(reader) as T;
                if (result == null) throw new InvalidDataException("Missing settings object");
                if (validate != null) validate(result);
                return result;
            }
        }
        private static XmlSerializer CreateSerializer()
        {
            var serializer = new XmlSerializer(typeof(T));
            // Unknown members may be data from a newer mod. Do not silently drop
            // them on the next edit, including an unknown schema-version field.
            serializer.UnknownElement += delegate(object sender, XmlElementEventArgs args) { throw new UnsupportedData("Unsupported settings element: " + args.Element.Name); };
            serializer.UnknownAttribute += delegate(object sender, XmlAttributeEventArgs args) { throw new UnsupportedData("Unsupported settings attribute: " + args.Attr.Name); };
            return serializer;
        }
        private static byte[] ReadBytes(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("Settings exceed 8 MB");
                var bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                return bytes;
            }
        }
        private static byte[] Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static bool Equal(byte[] left, byte[] right)
        {
            if (left == null || right == null) return left == right;
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
