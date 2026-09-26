using System;
using System.IO;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.GameManager.TitleScreen;
using JumpKing.MiscEntities;
using JumpKing.Player;
using JumpKing.Util;
using JKRuntime.Settings;

namespace JKRuntime
{
    public sealed class NativePerformanceSettings
    {
        public bool Enabled { get; set; }
    }

    /// <summary>Runtime-owned optional native caches. Physics and native simulation cadence are unchanged.</summary>
    public static class NativePerformance
    {
        private static SettingsFile<NativePerformanceSettings> file;
        private static OwnedPatches patches;
        private static bool installed;
        internal static bool Requested;
        public static bool Enabled { get { EnsureLoaded(); return Requested; } }
        public static string Status { get; private set; }
        public static readonly Setting<bool> Setting = new Setting<bool>("jk-runtime.optimizations", "Optimizations",
            delegate { return Enabled; }, SetEnabled, Prepare);

        private static void EnsureLoaded()
        {
            if (file != null) return;
            Load(Path.Combine(Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location), "JKRuntime.NativePerformance.xml"));
        }
        internal static void Load(string path)
        {
            file = new SettingsFile<NativePerformanceSettings>(path, delegate { return new NativePerformanceSettings(); });
            Requested = file.Value.Enabled;
        }
        private static void SetEnabled(bool value)
        {
            EnsureLoaded();
            file.Save(new NativePerformanceSettings { Enabled = value });
            Requested = value;
        }
        /// <summary>Import the former SFC option once, only if Runtime has no saved preference. Never overwrites a Runtime choice or the source file.</summary>
        public static void ImportLegacy(bool enabled)
        {
            EnsureLoaded();
            if (file.Status != SettingsReadStatus.Missing) return;
            SetEnabled(enabled);
            Prepare();
        }
        internal static void Prepare()
        {
            EnsureLoaded();
            if (!Requested) { NativeCaches.Clear(); Status = "Disabled"; return; }
            using (RuntimeApi.MeasureStartup("runtime.native-optimizations"))
            {
                Install();
                if (installed) NativeCaches.Prepare();
            }
        }
        internal static void Install()
        {
            if (installed) return;
            try
            {
                if (patches != null) patches.Dispose();
                patches = new OwnedPatches("jk-runtime.native-optimizations");
                var wall = typeof(Game1).Assembly.GetType("JumpKing.Props.RaymanWall.RaymanWallEntity", true);
                patches.ReplaceCalls(Method(wall, "TouchPlayer"), typeof(EntityManager).GetMethod("Find").MakeGenericMethod(typeof(PlayerEntity)), Method(typeof(NativeCaches), "FindPlayer"), 1);
                patches.ReplaceCalls(Method(typeof(EarthquakeEntity), "Update"), typeof(XmlSerializerHelper).GetMethod("Deserialize").MakeGenericMethod(typeof(EarthquakeEntity.EarthquakeSettings)), Method(typeof(NativeCaches), "ReadEarthquake"), 1);
                patches.ReplaceCalls(Method(typeof(GameTitleScreen), "Draw"), typeof(System.Diagnostics.Process).GetMethod("GetProcessesByName", new[] { typeof(string) }), Method(typeof(NativeCaches), "CaptureProcesses"), 2, true);
                foreach (string name in new[] { "AddObject", "RemoveObject", "MoveToFront" })
                    patches.Add(Method(typeof(EntityManager), name), postfix: Method(typeof(NativeCaches), "InvalidatePlayer"));
                patches.Add(Method(typeof(EntityManager), "Update"), prefix: Method(typeof(NativeCaches), "InvalidatePlayer"));
                patches.Add(Method(typeof(Game1), "OnExiting"), prefix: Method(typeof(NativeCaches), "Clear"));
                installed = true; Status = "Active";
            }
            catch (Exception error)
            {
                Status = "Unavailable: " + error.GetBaseException().Message;
                if (patches != null)
                    try { patches.Dispose(); patches = null; }
                    catch (Exception cleanup) { Status += "; cleanup pending: " + cleanup.GetBaseException().Message; }
                Console.WriteLine("[JK Runtime] Native optimizations " + Status);
            }
        }
        internal static void Uninstall()
        {
            NativeCaches.Clear();
            if (patches != null) patches.Dispose();
            patches = null; installed = false;
        }
        private static MethodInfo Method(Type type, string name)
        { return type.GetMethod(name, OwnedPatches.Members); }
    }
}
