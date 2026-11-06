using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    public sealed class SavedSceneVariables
    {
        [XmlAttribute("version")] public int Version = 1;
        [XmlAttribute("map")] public string Map;
        [XmlAttribute("epoch")] public string Epoch;
        [XmlElement("Variable")] public SavedSceneVariable[] Variables = new SavedSceneVariable[0];
    }
    public sealed class SavedSceneVariable
    {
        [XmlAttribute("id")] public string Id;
        [XmlAttribute("value")] public string Value;
    }

    internal sealed class ScenePersistence
    {
        private readonly string directory, path, map, epoch;
        private readonly Func<string> currentDirectory;
        internal ScenePersistence(string saveId, Func<string> directoryProvider)
        {
            map = saveId; currentDirectory = directoryProvider;
            directory = Path.GetFullPath(directoryProvider());
            epoch = Epoch(directory);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(saveId))).Replace("-", "");
            path = Path.Combine(directory, "MegaMapping", "Construction", hash + ".xml");
        }
        private ScenePersistence(ScenePersistence source, string nextEpoch)
        { directory = source.directory; path = source.path; map = source.map; currentDirectory = source.currentDirectory; epoch = nextEpoch; }
        internal ScenePersistence AfterReset(string resetDirectory, string nextEpoch)
        { return nextEpoch != null && string.Equals(directory, resetDirectory, StringComparison.OrdinalIgnoreCase) ? new ScenePersistence(this, nextEpoch) : null; }
        private void CheckContext()
        { if (!string.Equals(directory, Path.GetFullPath(currentDirectory()), StringComparison.OrdinalIgnoreCase) || Epoch(directory) != epoch) throw new InvalidOperationException("Native save context changed/reset; reload the map before saving scene variables"); }
        private static string Epoch(string directory)
        { string marker = Path.Combine(directory, "MegaMapping", "epoch.txt"); return File.Exists(marker) ? File.ReadAllText(marker) : ""; }
        internal static string ResetContext(string directory)
        {
            string folder = Path.Combine(Path.GetFullPath(directory), "MegaMapping");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "epoch.txt"), temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            string nextEpoch = Guid.NewGuid().ToString("N");
            try { File.WriteAllText(temporary, nextEpoch); if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return nextEpoch;
        }
        internal Dictionary<string, string> Load()
        {
            CheckContext(); var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return values;
            if (new FileInfo(path).Length > 8388608) throw new InvalidDataException("Scene flag save exceeds 8 MiB");
            SavedSceneVariables data;
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                data = (SavedSceneVariables)new XmlSerializer(typeof(SavedSceneVariables)).Deserialize(reader);
            if (data.Version != 1 || data.Map != map || data.Variables == null || data.Variables.Length > 256) throw new InvalidDataException("Unsupported scene variable save version/identity");
            if ((data.Epoch ?? "") != epoch) return values;
            foreach (SavedSceneVariable flag in data.Variables)
            {
                if (flag == null || string.IsNullOrWhiteSpace(flag.Id) || flag.Id.Length > 128 || flag.Value == null || flag.Value.Length > 4096 || values.ContainsKey(flag.Id))
                    throw new InvalidDataException("Invalid saved scene variable");
                values.Add(flag.Id, flag.Value);
            }
            return values;
        }
        internal void Save(Dictionary<string, string> values)
        {
            CheckContext();
            // don't recreate the directory after native save deletion
            if (!Directory.Exists(directory)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var data = new SavedSceneVariables { Map = map, Epoch = epoch, Variables = values.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new SavedSceneVariable { Id = p.Key, Value = p.Value }).ToArray() };
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new XmlSerializer(typeof(SavedSceneVariables)).Serialize(stream, data); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static string NativeDirectory()
        {
            Assembly game = typeof(JumpKing.Game1).Assembly;
            Type helper = game.GetType("JumpKing.SaveThread.SaveHelper", true), lube = game.GetType("JumpKing.SaveThread.SaveLube", true);
            var prefix = helper.GetProperty("PREFIX", BindingFlags.Static | BindingFlags.NonPublic);
            var folder = lube.GetField("SAVE_FOLDER", BindingFlags.Static | BindingFlags.Public);
            if (prefix == null || folder == null || prefix.PropertyType != typeof(string) || folder.FieldType != typeof(string)) throw new NotSupportedException("Native scene save context unavailable");
            return Path.GetFullPath(Path.Combine((string)prefix.GetValue(null, null), (string)folder.GetValue(null)));
        }
    }

    // the native SaveManager runs on a worker. it receives immutable copies, never engine dictionaries
    internal static class SceneSaveCoordinator
    {
        internal static readonly object Sync = new object();
        private static readonly object WriteSync = new object();
        private static ScenePersistence store;
        private static Dictionary<string, string> snapshot;
        internal static int Revision;
        private static long published, saved;
        private static string lastError;
        private static bool resetting;
        private static string resetDirectory, resetEpoch;
        internal static void Load(string saveId, Func<string> directory, out ScenePersistence value, out Dictionary<string, string> flags, out int revision)
        {
            lock (WriteSync)
            {
                value = new ScenePersistence(saveId, directory); flags = value.Load();
                lock (Sync) revision = Revision;
            }
        }
        internal static ScenePersistence ResetStore(ScenePersistence previous, out int revision)
        {
            lock (Sync)
            { revision = Revision; return resetting ? null : previous.AfterReset(resetDirectory, resetEpoch); }
        }
        internal static string ResetEpochFor(string directory)
        { lock (Sync) { return !resetting && string.Equals(Path.GetFullPath(directory), resetDirectory, StringComparison.OrdinalIgnoreCase) ? resetEpoch : null; } }
        internal static void Publish(ScenePersistence value, Dictionary<string, string> flags)
        {
            var copy = flags == null ? null : new Dictionary<string, string>(flags, StringComparer.Ordinal);
            lock (Sync) { if (resetting) return; store = value; snapshot = copy; published++; }
        }
        internal static void Save()
        {
            // native save/reset workers serialize disk commits. Publishing flags
            // on the game thread never waits on this lock or performs storage I/O
            lock (WriteSync)
            {
                ScenePersistence target; Dictionary<string, string> packet; long generation;
                lock (Sync)
                {
                    if (store == null || snapshot == null || saved == published) return;
                    target = store; packet = snapshot; generation = published;
                }
                try
                {
                    target.Save(packet);
                    lock (Sync) { if (published == generation) saved = generation; lastError = null; }
                }
                catch (Exception error)
                {
                    // keep the newest packet dirty for the next native save
                    // report a repeated storage failure only once until recovery
                    bool report;
                    lock (Sync) { report = lastError != error.Message; lastError = error.Message; }
                    if (report) ModEntry.Log("Scene variables remain pending: " + error);
                }
            }
        }
        internal static void Reset()
        { Reset(ScenePersistence.NativeDirectory()); }
        internal static void Reset(string directory)
        {
            lock (Sync)
            {
                store = null; snapshot = null; resetting = true;
                System.Threading.Interlocked.Increment(ref Revision);
            }
            lock (WriteSync)
            {
                string nextEpoch = null;
                try { nextEpoch = ScenePersistence.ResetContext(directory); }
                catch (Exception error) { ModEntry.Log("Scene save reset failed: " + error); }
                finally
                {
                    lock (Sync)
                    {
                        store = null; snapshot = null; lastError = null;
                        resetDirectory = Path.GetFullPath(directory); resetEpoch = nextEpoch; resetting = false;
                        System.Threading.Interlocked.Increment(ref Revision);
                    }
                }
            }
        }
    }

}
