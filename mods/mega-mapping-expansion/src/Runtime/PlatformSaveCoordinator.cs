using System;
using System.Collections.Generic;
namespace MegaMappingExpansion
{
    // a separate immutable packet lets native save workers handle routes without a scene
    internal static class PlatformSaveCoordinator
    {
        private static readonly object Sync = new object(), WriteSync = new object();
        private static ScenePersistence store;
        private static Dictionary<string, string> snapshot;
        private static long published, saved;
        private static string resetDirectory, resetEpoch, lastError;
        private static bool resetting;
        internal static int Revision;
        internal static void Load(string id, Func<string> directory, out ScenePersistence target, out Dictionary<string, string> values, out int revision)
        {
            lock (WriteSync) { target = new ScenePersistence("platforms:" + id, directory); values = target.Load(); lock (Sync) revision = Revision; }
        }
        internal static bool TryResetStore(ScenePersistence previous, out ScenePersistence next, out int revision)
        { lock (Sync) { revision = Revision; next = resetting || previous == null ? null : previous.AfterReset(resetDirectory, resetEpoch); return !resetting; } }
        internal static void Publish(ScenePersistence target, Dictionary<string, string> values)
        { lock (Sync) { if (resetting) return; store = target; snapshot = new Dictionary<string, string>(values, StringComparer.Ordinal); published++; } }
        internal static void Save()
        {
            lock (WriteSync)
            {
                ScenePersistence target; Dictionary<string, string> values; long generation;
                lock (Sync) { if (store == null || saved == published) return; target = store; values = snapshot; generation = published; }
                try { target.Save(values); lock (Sync) { if (generation == published) saved = generation; lastError = null; } }
                catch (Exception error) { bool report; lock (Sync) { report = lastError != error.Message; lastError = error.Message; } if (report) ModEntry.Log("Platform save remains pending: " + error); }
            }
        }
        internal static void Reset(string directory)
        {
            lock (Sync) { resetting = true; store = null; snapshot = null; System.Threading.Interlocked.Increment(ref Revision); }
            lock (WriteSync)
            {
                string epoch = null;
                try { epoch = SceneSaveCoordinator.ResetEpochFor(directory); }
                catch (Exception error) { ModEntry.Log("Platform save reset failed: " + error); }
                finally { lock (Sync) { resetDirectory = System.IO.Path.GetFullPath(directory); resetEpoch = epoch; resetting = false; System.Threading.Interlocked.Increment(ref Revision); } }
            }
        }
    }
}
