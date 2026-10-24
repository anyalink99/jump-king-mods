using System;
using System.IO;
using System.Xml.Serialization;

namespace JKRuntime.Settings
{
    public static class AtomicXmlFile
    {
        public static T Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (FileStream stream = File.OpenRead(path))
                return serializer.Deserialize(stream) as T;
        }

        public static void Save<T>(string path, T value)
        { SaveCore(path, value, false); }

        internal static byte[] SaveWithFingerprint<T>(string path, T value)
        { return SaveCore(path, value, true); }

        private static byte[] SaveCore<T>(string path, T value, bool fingerprint)
        {
            if (value == null) throw new ArgumentNullException("value");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            string backup = path + ".bak";
            byte[] digest = null;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (FileStream stream = new FileStream(
                    temporary,
                    FileMode.Create,
                    fingerprint ? FileAccess.ReadWrite : FileAccess.Write,
                    FileShare.None))
                {
                    serializer.Serialize(stream, value);
                    stream.Flush(true);
                    if (fingerprint)
                    {
                        stream.Position = 0;
                        using (var sha = System.Security.Cryptography.SHA256.Create()) digest = sha.ComputeHash(stream);
                    }
                }
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, backup, true);
                }
                else
                {
                    File.Move(temporary, path);
                }
                return digest;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
