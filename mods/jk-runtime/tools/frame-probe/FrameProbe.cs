using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Mods;
using Microsoft.Xna.Framework;

namespace JKFrameProbe
{
    // Temporary opt-in diagnostics. No input, state, scheduler or save changes.
    [JumpKingMod("JK Frame Probe")]
    public static class Probe
    {
        internal sealed class Metric
        {
            internal string Name;
            internal int Count;
            internal double Total, Maximum;
            internal void Add(double ms) { Count++; Total += ms; Maximum = Math.Max(Maximum, ms); }
            internal Metric Copy() { return (Metric)MemberwiseClone(); }
        }
        internal struct Sample
        {
            internal string Kind;
            internal int Mode, Gen0;
            internal long Allocated;
            internal double Start, Milliseconds;
        }
        internal struct Stamp { internal long Time, Bytes; internal int Gen0; }
        internal sealed class Capture
        {
            internal Metric[] Metrics;
            internal Sample[] Samples;
            internal string Reason, Inventory;
            internal int Dropped;
        }
        private const string Owner = "jk-runtime.frame-probe";
        private const int Capacity = 32768;
        private static readonly Sample[] samples = new Sample[Capacity];
        private static readonly Dictionary<Type, Metric[]> objects = new Dictionary<Type, Metric[]>();
        private static readonly Dictionary<MethodBase, Metric> phases = new Dictionary<MethodBase, Metric>();
        private static readonly Func<long> Allocated = AllocationReader();
        private static readonly Type Pause = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager");
        private static readonly FieldInfo PauseInstance = Pause.GetField("instance");
        private static readonly PropertyInfo Paused = Pause.GetProperty("IsPaused");
        private static long started, previousDraw, warmUntil;
        private static int count, dropped, thread, serial;
        private static bool installed, active;
        private static int automaticWindows = 2;
        private static string output, inventory;
        internal static Action<Capture> Sink = WriteAsync;
        private static bool Recording { get { return active && Thread.CurrentThread.ManagedThreadId == thread; } }
        private static Func<long> AllocationReader()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            return method == null ? (Func<long>)(() => 0) : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }
        [BeforeLevelLoad] public static void Install()
        {
            if (installed) return;
            output = Path.Combine(Path.GetDirectoryName(typeof(Probe).Assembly.Location), "JKFrameProbe");
            var h = new Harmony(Owner);
            try
            {
                Hook(h, typeof(Component), "LowUpdate", "Begin", "ComponentEnd");
                Hook(h, typeof(Component), "LowLateUpdate", "Begin", "LateEnd");
                Hook(h, typeof(Entity), "UpdateComponents", "Begin", "EntityEnd");
                foreach (var type in new[] { typeof(JumpKing.Controller.ControllerManager), typeof(JumpKing.Level.LevelManager), typeof(EntityManager), typeof(JumpGame) })
                    Hook(h, type, "Update", "Begin", "PhaseEnd");
                Hook(h, typeof(Game1), "Update", "FrameBegin", "UpdateEnd");
                Hook(h, typeof(Game), "DoDraw", "FrameBegin", "DrawEnd");
                h.Patch(AccessTools.Method(typeof(Game), "OnExiting"), prefix: new HarmonyMethod(typeof(Probe), "Exiting"));
                installed = true;
                inventory = string.Join("\r\n", ModLoader.Instance.LoadedMods.Select(m => m.Assembly.FullName + " | " + m.Assembly.Location));
                File.WriteAllText(output + ".status.txt", "Installed; load a map, play and visit pause menus for 45 seconds. No input or save data is recorded.\r\n");
            }
            catch (Exception error) { h.UnpatchAll(Owner); File.WriteAllText(output + ".status.txt", "Install failed: " + error); throw; }
        }
        private static void Hook(Harmony h, Type type, string method, string before, string after)
        {
            var target = AccessTools.Method(type, method);
            if (target == null) throw new MissingMethodException(type.FullName, method);
            h.Patch(target, new HarmonyMethod(typeof(Probe), before) { priority = Priority.First }, new HarmonyMethod(typeof(Probe), after) { priority = Priority.Last });
        }
        [OnLevelStart] public static void Start()
        {
            Finish("attempt changed"); thread = Thread.CurrentThread.ManagedThreadId; started = Stopwatch.GetTimestamp();
            warmUntil = started + Stopwatch.Frequency * 2; previousDraw = 0; count = dropped = 0; objects.Clear(); phases.Clear(); active = true;
        }
        [OnLevelEnd] public static void End() { Finish("level ended"); }
        private static void Exiting() { Finish("game exiting"); }
        internal static bool Rearm(bool eligible)
        {
            if (active || !eligible || automaticWindows == 0) return false;
            automaticWindows--; Start(); return true;
        }
        private static void Begin(out long __state) { __state = Recording ? Stopwatch.GetTimestamp() : 0; }
        private static double Milliseconds(long ticks) { return ticks * 1000d / Stopwatch.Frequency; }
        private static void ObjectEnd(object value, int slot, long start)
        {
            if (start == 0 || start < warmUntil || !Recording) return;
            double ms = Milliseconds(Stopwatch.GetTimestamp() - start);
            Type type = value.GetType(); Metric[] metrics;
            if (!objects.TryGetValue(type, out metrics))
            {
                if (objects.Count >= 2048) { dropped++; return; }
                metrics = new[] { new Metric { Name = type.FullName + ".UpdateComponents" }, new Metric { Name = type.FullName + ".LowUpdate" }, new Metric { Name = type.FullName + ".LowLateUpdate" } };
                objects.Add(type, metrics);
            }
            metrics[slot].Add(ms);
        }
        private static void EntityEnd(object __instance, long __state) { ObjectEnd(__instance, 0, __state); }
        private static void ComponentEnd(object __instance, long __state) { ObjectEnd(__instance, 1, __state); }
        private static void LateEnd(object __instance, long __state) { ObjectEnd(__instance, 2, __state); }
        private static void PhaseEnd(MethodBase __originalMethod, long __state)
        {
            if (__state == 0 || __state < warmUntil || !Recording) return;
            double ms = Milliseconds(Stopwatch.GetTimestamp() - __state); Metric metric;
            if (!phases.TryGetValue(__originalMethod, out metric)) { metric = new Metric { Name = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name }; phases.Add(__originalMethod, metric); }
            metric.Add(ms);
        }
        private static void FrameBegin(out Stamp __state)
        {
            if (!active) Rearm(Game1.instance != null && Game1.instance.IsActive && GameLoop.m_player != null && GameLoop.m_player.IsAlive);
            __state = Recording ? new Stamp { Time = Stopwatch.GetTimestamp(), Bytes = Allocated(), Gen0 = GC.CollectionCount(0) } : default(Stamp);
        }
        private static int Mode()
        {
            if (Game1.instance == null || !Game1.instance.IsActive) return 3;
            if (GameLoop.m_player == null || !GameLoop.m_player.IsAlive) return 0;
            object pause = PauseInstance.GetValue(null); return pause != null && (bool)Paused.GetValue(pause, null) ? 2 : 1;
        }
        private static void Record(string kind, Stamp stamp, long end)
        {
            if (stamp.Time == 0 || stamp.Time < warmUntil || !Recording) return;
            if (count == samples.Length) { dropped++; return; }
            samples[count++] = new Sample { Kind = kind, Mode = Mode(), Start = Milliseconds(stamp.Time - started), Milliseconds = Milliseconds(end - stamp.Time), Allocated = Allocated() - stamp.Bytes, Gen0 = GC.CollectionCount(0) - stamp.Gen0 };
        }
        private static void UpdateEnd(Stamp __state) { Record("update", __state, Stopwatch.GetTimestamp()); }
        private static void DrawEnd(Stamp __state)
        {
            long end = Stopwatch.GetTimestamp(); Record("draw", __state, end);
            if (__state.Time == 0 || !Recording) return;
            if (previousDraw >= warmUntil && count < samples.Length) samples[count++] = new Sample { Kind = "gap", Mode = Mode(), Start = Milliseconds(previousDraw - started), Milliseconds = Milliseconds(__state.Time - previousDraw) };
            previousDraw = __state.Time;
            if (end - started >= Stopwatch.Frequency * 45) Finish("45 seconds completed");
        }
        internal static void Finish(string reason)
        {
            if (!active) return; active = false;
            Sink(new Capture { Reason = reason, Inventory = inventory + LiveSettings(), Dropped = dropped, Samples = samples.Take(count).ToArray(), Metrics = objects.Values.SelectMany(x => x).Concat(phases.Values).Where(m => m.Count > 0).Select(m => m.Copy()).ToArray() });
        }
        private static string LiveSettings()
        {
            var text = new StringBuilder("\r\nLoaded in-memory settings (after capture):\r\n");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) foreach (string name in new[] { "SubframeCharge.SettingsStore", "SmoothCamera.Settings", "MoreItems.SettingsStore", "CasualJumping.SettingsStore" })
            {
                var type = assembly.GetType(name); if (type == null) continue;
                try {
                    var current = type.GetProperty("Current", flags); object value = current == null ? null : current.GetValue(null, null); if (value == null) continue;
                    foreach (var p in value.GetType().GetProperties().Where(p => p.PropertyType == typeof(bool))) text.AppendLine(name + "." + p.Name + "=" + p.GetValue(value, null));
                    foreach (var f in value.GetType().GetFields().Where(f => f.FieldType == typeof(bool))) text.AppendLine(name + "." + f.Name + "=" + f.GetValue(value));
                } catch (Exception error) { text.AppendLine(name + ": " + error.GetBaseException().Message); }
            }
            return text.ToString();
        }
        internal static string Format(Capture capture)
        {
            var text = new StringBuilder("JK Frame Probe 1; " + capture.Reason + "\r\nNested timings overlap. First 2 seconds excluded. Modes: 0 menu, 1 gameplay, 2 pause, 3 unfocused.\r\nInstrumentation adds CPU work; compare like-for-like captures. Draw submission is not end-to-end display latency.\r\nDropped: " + capture.Dropped + "\r\n");
            foreach (var group in capture.Samples.GroupBy(s => s.Kind + "/" + s.Mode))
            {
                var ordered = group.Select(s => s.Milliseconds).OrderBy(v => v).ToArray();
                text.AppendFormat(CultureInfo.InvariantCulture, "{0}: n={1} median={2:F3} p95={3:F3} max={4:F3} ms; meanAllocated={5:F0} B; Gen0={6}\r\n", group.Key, ordered.Length, ordered[ordered.Length / 2], ordered[Math.Min(ordered.Length - 1, (int)(ordered.Length * .95))], ordered.Last(), group.Average(s => s.Allocated), group.Sum(s => s.Gen0));
            }
            text.AppendLine("Count\tTotal_ms\tMax_ms\tStage");
            foreach (var metric in capture.Metrics.OrderByDescending(m => m.Total)) text.AppendFormat(CultureInfo.InvariantCulture, "{0}\t{1:F3}\t{2:F3}\t{3}\r\n", metric.Count, metric.Total, metric.Maximum, metric.Name);
            text.AppendLine("Loaded packages:"); text.AppendLine(capture.Inventory); return text.ToString();
        }
        private static void WriteAsync(Capture capture)
        {
            string path = output + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + (++serial);
            Action write = delegate {
                try {
                    File.WriteAllText(path + ".txt", Format(capture));
                    var csv = new StringBuilder("start_ms,kind,mode,duration_ms,allocated_bytes,gen0\r\n");
                    foreach (var sample in capture.Samples) csv.AppendFormat(CultureInfo.InvariantCulture, "{0:F3},{1},{2},{3:F3},{4},{5}\r\n", sample.Start, sample.Kind, sample.Mode, sample.Milliseconds, sample.Allocated, sample.Gen0);
                    File.WriteAllText(path + ".csv", csv.ToString()); File.WriteAllText(output + ".status.txt", "Complete: " + path + ".txt\r\n");
                } catch (Exception e) { Console.WriteLine("[JK Frame Probe] " + e.Message); }
            };
            if (capture.Reason == "game exiting") write(); else ThreadPool.QueueUserWorkItem(delegate { write(); });
        }
    }
}
