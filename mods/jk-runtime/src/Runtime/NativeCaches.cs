using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Diagnostics;
using EntityComponent;
using JumpKing;
using JumpKing.MiscEntities;
using JumpKing.Player;
using JumpKing.Util;

namespace JKRuntime
{
    // Only the earthquake call site borrows this immutable settings value.
    // A watcher detects replacements even when size and timestamps are retained.
    internal sealed class EarthquakeCache : IDisposable
    {
        private readonly string path;
        private readonly FileSystemWatcher watcher;
        private int revision = 1, loadedRevision;
        private EarthquakeEntity.EarthquakeSettings value;
        internal EarthquakeCache(string file)
        {
            path = Path.GetFullPath(file);
            watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path));
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;
            watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed;
            watcher.Renamed += Renamed; watcher.Error += Failed;
            watcher.EnableRaisingEvents = true;
        }
        private void Changed(object sender, FileSystemEventArgs args) { Invalidate(); }
        private void Renamed(object sender, RenamedEventArgs args) { Invalidate(); }
        private volatile bool unreliable;
        private void Failed(object sender, ErrorEventArgs args) { unreliable = true; Invalidate(); }
        internal void Invalidate() { Interlocked.Increment(ref revision); }
        internal EarthquakeEntity.EarthquakeSettings Read()
        {
            int observed = Volatile.Read(ref revision);
            if (unreliable || observed != loadedRevision)
            {
                // Preserve native failure behavior; a failed read never becomes
                // a cached default or silently retains old map data.
                var next = XmlSerializerHelper.Deserialize<EarthquakeEntity.EarthquakeSettings>(path);
                value = next; loadedRevision = observed;
            }
            return value;
        }
        public void Dispose() { watcher.Dispose(); }
    }

    internal static class NativeCaches
    {
        private static EarthquakeCache earthquake;
        private static string earthquakePath;
        private static EntityManager cachedManager;
        private static PlayerEntity cachedPlayer;
        private static bool found;
        internal static int Searches;
        private sealed class Processes { internal long Expires; internal Process[] Value; }
        private static readonly Dictionary<string, Processes> captureApps = new Dictionary<string, Processes>(StringComparer.OrdinalIgnoreCase);
        internal static Process[] CaptureProcesses(string name)
        {
            if (!NativePerformance.Requested) return Process.GetProcessesByName(name);
            Processes cached; long now = Stopwatch.GetTimestamp();
            if (!captureApps.TryGetValue(name, out cached) || now >= cached.Expires)
            {
                var processes = Process.GetProcessesByName(name);
                // The guarded native call sites use only array.Length.
                foreach (var process in processes) process.Dispose();
                cached = new Processes { Expires = now + Stopwatch.Frequency, Value = processes };
                captureApps[name] = cached;
            }
            return cached.Value;
        }

        internal static void Prepare()
        {
            InvalidatePlayer();
            if (!NativePerformance.Requested || Game1.instance == null || Game1.instance.contentManager == null) return;
            string path = Game1.instance.contentManager.root + "/gui/earthquake_settings.xml";
            // Optional optimization: missing/invalid data still follows the
            // native path when the entity actually needs it.
            try { ReadEarthquake(path); }
            catch (Exception error) { Clear(); Console.WriteLine("optimization preparation: " + error.Message); }
        }
        internal static void Clear()
        {
            if (earthquake != null) earthquake.Dispose();
            earthquake = null; earthquakePath = null; captureApps.Clear(); InvalidatePlayer();
        }
        internal static void InvalidatePlayer() { found = false; cachedManager = null; cachedPlayer = null; }
        internal static PlayerEntity FindPlayer(EntityManager manager)
        {
            if (!NativePerformance.Requested) return manager.Find<PlayerEntity>();
            if (!found || !ReferenceEquals(cachedManager, manager))
            { cachedPlayer = manager.Find<PlayerEntity>(); cachedManager = manager; found = true; Searches++; }
            return cachedPlayer;
        }
        internal static EarthquakeEntity.EarthquakeSettings ReadEarthquake(string path)
        {
            if (!NativePerformance.Requested) return XmlSerializerHelper.Deserialize<EarthquakeEntity.EarthquakeSettings>(path);
            string full = Path.GetFullPath(path);
            if (earthquake == null || !string.Equals(earthquakePath, full, StringComparison.OrdinalIgnoreCase))
            {
                if (earthquake != null) earthquake.Dispose();
                earthquake = null; earthquakePath = null;
                try { earthquake = new EarthquakeCache(full); earthquakePath = full; }
                catch (IOException) { return XmlSerializerHelper.Deserialize<EarthquakeEntity.EarthquakeSettings>(path); }
                catch (UnauthorizedAccessException) { return XmlSerializerHelper.Deserialize<EarthquakeEntity.EarthquakeSettings>(path); }
            }
            return earthquake.Read();
        }
    }
}
