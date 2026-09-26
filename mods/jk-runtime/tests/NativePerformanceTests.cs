using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;
using EntityComponent;
using JumpKing.Player;
using JumpKing;
namespace JKRuntime
{
    internal static class NativePerformanceTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static PlayerEntity EmptyPlayer() { var p=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity)); GC.SuppressFinalize(p); return p; }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-performance-"+Guid.NewGuid().ToString("N")+".xml");
            NativePerformance.Load(path);
            Check(!NativePerformance.Enabled && !File.Exists(path),"Default does not create a preference before migration");
            NativePerformance.ImportLegacy(true);
            NativePerformance.ImportLegacy(false);
            Check(NativePerformance.Enabled,"Runtime choice survives repeated legacy import");
            OptimizationTests();
            Console.WriteLine("[OK] Runtime native optimizations: isolated migration, exact lookup semantics and watched XML recovery");
        }
    private static void OptimizationTests()
    {
        NativePerformance.Install(); Check(NativePerformance.Status == "Active", NativePerformance.Status);
        var manager = new EntityManager();
        var first=EmptyPlayer(); var second=EmptyPlayer(); manager.AddObject(first); manager.AddObject(second);
        NativeCaches.Searches=0;
        for (int i=0;i<100;i++) Check(ReferenceEquals(NativeCaches.FindPlayer(manager),first), "Rayman uses exact native first-match semantics");
        Check(NativeCaches.Searches==1, "One player search for 100 walls");
        manager.MoveToFront(first); Check(ReferenceEquals(NativeCaches.FindPlayer(manager),second), "Reorder invalidates cached first player");
        manager.RemoveObject(second); Check(ReferenceEquals(NativeCaches.FindPlayer(manager),first), "Removal invalidates cache immediately");
        manager.RemoveObject(first); Check(NativeCaches.FindPlayer(manager)==null, "Empty list never returns previous player");
        string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"earthquake-fixture"); Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"settings.xml");
        Action<int> write = screen => File.WriteAllText(path,"<EarthquakeSettings><screens><int>"+screen+"</int></screens></EarthquakeSettings>");
        write(1);
        var timing = Stopwatch.StartNew();
        for (int i = 0; i < 500; i++) XmlSerializerHelper.Deserialize<JumpKing.MiscEntities.EarthquakeEntity.EarthquakeSettings>(path);
        timing.Stop(); double nativeXml = timing.Elapsed.TotalMilliseconds / 500;
        NativeCaches.ReadEarthquake(path);
        timing.Restart();
        for (int i = 0; i < 100000; i++) NativeCaches.ReadEarthquake(path);
        timing.Stop();
        Console.WriteLine("[COST] Earthquake settings: native " + nativeXml.ToString("F6") + " ms/read; enabled warm " + (timing.Elapsed.TotalMilliseconds / 100000).ToString("F6") + " ms/read");
        NativePerformance.Requested = false;
        timing.Restart();
        for (int i = 0; i < 500; i++) NativeCaches.ReadEarthquake(path);
        timing.Stop();
        Console.WriteLine("[COST] Earthquake settings: disabled adapter " + (timing.Elapsed.TotalMilliseconds / 500).ToString("F6") + " ms/read");
        NativePerformance.Requested = true;
        const string absentProcess = "jk-runtime-test-absent-process";
        timing.Restart();
        for (int i = 0; i < 32; i++) foreach (var process in Process.GetProcessesByName(absentProcess)) process.Dispose();
        timing.Stop(); double nativeProcesses = timing.Elapsed.TotalMilliseconds / 32;
        NativeCaches.CaptureProcesses(absentProcess);
        timing.Restart();
        for (int i = 0; i < 100000; i++) NativeCaches.CaptureProcesses(absentProcess);
        timing.Stop();
        Console.WriteLine("[COST] Title process query: native " + nativeProcesses.ToString("F6") + " ms/read; enabled warm " + (timing.Elapsed.TotalMilliseconds / 100000).ToString("F6") + " ms/read (one-second refresh)");
        using (var cache = new EarthquakeCache(path))
        {
            Check(cache.Read().screens.Single()==1,"Prepared XML read");
            var timestamp=File.GetLastWriteTimeUtc(path); write(2); File.SetLastWriteTimeUtc(path,timestamp);
            var timeout=Stopwatch.StartNew();
            while (cache.Read().screens.Single()!=2 && timeout.ElapsedMilliseconds<2000) Thread.Sleep(10);
            Check(cache.Read().screens.Single()==2,"Same-length and same-timestamp changes invalidate via watcher");
            File.WriteAllText(path,"invalid"); cache.Invalidate(); bool failed=false;
            try { cache.Read(); } catch { failed=true; }
            Check(failed,"Invalid XML preserves native failure instead of stale defaults");
            write(3); cache.Invalidate(); Check(cache.Read().screens.Single()==3,"Valid replacement recovers after parse failure");
        }
        NativeCaches.Clear(); NativePerformance.Uninstall(); typeof(EntityManager).GetField("_instance", OwnedPatches.Members).SetValue(null,null);
    }
    }
}
