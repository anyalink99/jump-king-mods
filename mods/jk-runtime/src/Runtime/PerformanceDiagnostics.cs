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
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Mods;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    // Opt-in bounded profiling. No input, state, scheduler or save changes.
    internal static class PerformanceDiagnostics
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
            internal int Mode, Gen0, Gen1, Gen2;
            internal long Allocated;
            internal double Start, Milliseconds, SimulationMilliseconds;
        }
        internal struct Stamp { internal long Time, Bytes; internal int Gen0, Gen1, Gen2, Generation; }
        internal struct SwitchStamp { internal Stamp Timing; internal string Source, Target, Title; }
        private static readonly List<string> switches=new List<string>();
        internal sealed class Capture
        {
            internal Metric[] Metrics;
            internal Sample[] Samples;
            internal string Reason, Inventory;
            internal int Dropped;
            internal int Generation;
            internal DateTime Utc;
        }
        private const int Capacity = 65536;
        private static readonly Sample[] samples = new Sample[Capacity];
        private static readonly Dictionary<Type, Metric[]> objects = new Dictionary<Type, Metric[]>();
        private static readonly Dictionary<MethodBase, Metric> phases = new Dictionary<MethodBase, Metric>();
        private static readonly Func<long> Allocated = AllocationReader();
        private static readonly Type Pause = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager");
        private static readonly FieldInfo PauseInstance = Pause.GetField("instance");
        private static readonly PropertyInfo Paused = Pause.GetProperty("IsPaused");
        private static long started, previousDraw;
        private static int count, dropped, thread, serial, generation;
        private static bool active;
        private static DiagnosticHooks hooks;
        private static readonly object writeGate = new object();
        private static Capture pending;
        private static bool writing;
        internal static string Status = "Disabled";
        internal static string LastReport = "No performance report yet";
        internal static bool Enabled { get { return active; } }
        internal static bool SupportedAllocations { get { return typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes) != null; } }
        private static string output, inventory;
        internal static string OutputDirectory = null;
        internal static Action<Capture> Sink = WriteAsync;
        internal static bool IsRecording { get { return Recording; } }
        private static bool Recording { get { return active && Thread.CurrentThread.ManagedThreadId == thread; } }
        private static Func<long> AllocationReader()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            return method == null ? (Func<long>)(() => 0) : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }
        internal static void Configure(bool enabled)
        {
            if (!enabled)
            {
                Finish("disabled");
                if (hooks != null) { hooks.Dispose(); hooks = null; }
                RuntimeJournal.ProfileCallbacks = false; Status = "Disabled"; return;
            }
            if (active) return;
            if (hooks == null)
            {
                output = Path.Combine(OutputDirectory ?? Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location), "JKRuntime.Performance");
                hooks = new DiagnosticHooks("jk-runtime.performance");
                try
                {
                    AnimationDiagnostics.Install(hooks);
                    Hook(typeof(Component), "LowUpdate", "Begin", "ComponentEnd");
                    Hook(typeof(Component), "LowLateUpdate", "Begin", "LateEnd");
                    Hook(typeof(Entity), "UpdateComponents", "Begin", "EntityEnd");
                    foreach (var type in new[] { typeof(JumpKing.Controller.ControllerManager), typeof(JumpKing.Level.LevelManager), typeof(EntityManager), typeof(WeatherManager), typeof(JumpKing.Level.ScrollingBackground), typeof(JumpGame) })
                        Hook(type, "Update", "Begin", "PhaseEnd");
                    Hook(typeof(JumpGame), "Draw", "Begin", "PhaseEnd");
                    // Loading used to appear as one opaque long menu/update call.
                    // Preserve each native operation and attribute its cost separately.
                    foreach(var pair in new[] {
                        Tuple.Create(typeof(JKContentManager),"ReinitializeAssets"),
                        Tuple.Create(typeof(JKContentManager),"LoadAssets"),
                        Tuple.Create(typeof(JumpKing.Level.LevelManager),"LoadScreens"),
                        Tuple.Create(typeof(IntroState),"OnNewRun"),
                        Tuple.Create(typeof(IntroState),"InitializeEntities"),
                        Tuple.Create(typeof(Game1).Assembly.GetType("JumpKing.Props.PropManager",true),"Load"),
                        Tuple.Create(typeof(GameLoop),"OnPreGameStart"),
                        Tuple.Create(typeof(GameLoop),"OnNewRun") })
                        Hook(pair.Item1,pair.Item2,"Begin","PhaseEnd");
                    Hook(typeof(JumpKing.Workshop.Nodes.SetContentNode),"MyRun","SwitchBegin","SwitchEnd");
                    InstallAssetStages();
                    Hook(typeof(Game1), "Update", "FrameBegin", "UpdateEnd");
                    // Observe actual Draw, not DoDraw: a presentation mod can skip DoDraw.
                    Hook(typeof(Game1), "Draw", "FrameBegin", "DrawEnd");
                    Hook(typeof(Game), "EndDraw", "FrameBegin", "PresentEnd");
                    Hook(typeof(Game), "Tick", "FrameBegin", "TickEnd");
                    Hook(typeof(Game1), "OnExiting", "Exiting", null);
                }
                catch
                { hooks.Dispose(); hooks = null; Status = "Hook installation failed"; throw; }
            }
            inventory = string.Join("\r\n", AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Select(a => a.FullName));
            Start(); RuntimeJournal.ProfileCallbacks = true; Status = "Recording (45 s rolling windows)";
        }
        private static void Hook(Type type, string method, string before, string after)
        { hooks.Add(type, method, typeof(PerformanceDiagnostics), before, after); }
        private static void InstallAssetStages()
        {
            // Observe concrete boundaries, never patch shared generic Load<T> IL.
            // Asset readers cover directory texture loads; the nested groups also
            // expose XML/sprite construction and background audio synchronization.
            Hook(typeof(JKContentManager),"LoadPlayerSprites","Begin","PhaseEnd");
            foreach(var type in typeof(JKContentManager).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))
                foreach(var method in type.GetMethods(DiagnosticHooks.Flags|BindingFlags.DeclaredOnly))
                    if(method.Name=="Load" && !method.IsGenericMethod && method.GetParameters().Length==1)
                        hooks.Add(method,typeof(PerformanceDiagnostics),"Begin","PhaseEnd");
            var framework=typeof(Game).Assembly;
            foreach(string name in new[]{"Texture2DReader","SoundEffectReader","SpriteFontReader","EffectReader"})
            {
                var type=framework.GetType("Microsoft.Xna.Framework.Content."+name,true);
                foreach(var method in type.GetMethods(DiagnosticHooks.Flags|BindingFlags.DeclaredOnly))
                    if(method.Name=="Read" && !method.IsGenericMethod)
                        hooks.Add(method,typeof(PerformanceDiagnostics),"Begin","PhaseEnd");
            }
            Hook(typeof(Game1).Assembly.GetType("JumpKing.JKMemory.ManagedAssets.ThreadLube.ContentThread",true),
                "UnloadAndClearSounds","Begin","PhaseEnd");
        }
        private static void SwitchBegin(object __instance,out SwitchStamp __state)
        {
            Stamp timing; Begin(out timing);
            __state=new SwitchStamp { Timing=timing };
            if(timing.Time==0) return;
            var type=typeof(JumpKing.Workshop.Nodes.SetContentNode);
            __state.Source=Game1.instance==null || Game1.instance.contentManager==null ? "unavailable" : Game1.instance.contentManager.root;
            __state.Target=(string)type.GetField("m_dir",DiagnosticHooks.Flags).GetValue(__instance);
            __state.Title=(string)type.GetField("m_title",DiagnosticHooks.Flags).GetValue(__instance);
        }
        private static void SwitchEnd(MethodBase __originalMethod,SwitchStamp __state)
        {
            PhaseEnd(__originalMethod,__state.Timing);
            if(__state.Timing.Time==0 || __state.Timing.Generation!=generation || !Recording) return;
            if(switches.Count==16) { dropped++; return; }
            switches.Add(string.Format(CultureInfo.InvariantCulture,"{0:F3} ms: {1} -> {2}; title={3}",
                Milliseconds(Stopwatch.GetTimestamp()-__state.Timing.Time),Label(__state.Source),Label(__state.Target),Label(__state.Title)));
        }
        private static string Label(string value)
        { value=(value ?? "").Replace('\r',' ').Replace('\n',' '); return value.Length<=512 ? value : value.Substring(0,512); }
        internal static void Start()
        {
            thread = Thread.CurrentThread.ManagedThreadId; started = Stopwatch.GetTimestamp(); generation++;
            previousDraw = 0; count = dropped = 0; objects.Clear(); phases.Clear(); named.Clear(); switches.Clear(); AnimationDiagnostics.Reset(); active = true;
        }
        private static void Exiting() { Finish("game exiting"); Flush(); }
        private static void Begin(out Stamp __state) { __state = Recording ? new Stamp { Time = Stopwatch.GetTimestamp(), Generation = generation } : default(Stamp); }
        private static double Milliseconds(long ticks) { return ticks * 1000d / Stopwatch.Frequency; }
        private static void ObjectEnd(object value, int slot, Stamp stamp)
        {
            if (stamp.Time == 0 || stamp.Generation != generation || !Recording) return;
            long start = stamp.Time;
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
        private static void EntityEnd(object __instance, Stamp __state) { ObjectEnd(__instance, 0, __state); }
        private static void ComponentEnd(object __instance, Stamp __state) { ObjectEnd(__instance, 1, __state); }
        private static void LateEnd(object __instance, Stamp __state) { ObjectEnd(__instance, 2, __state); }
        private static void PhaseEnd(MethodBase __originalMethod, Stamp __state)
        {
            if (__state.Time == 0 || __state.Generation != generation || !Recording) return;
            double ms = Milliseconds(Stopwatch.GetTimestamp() - __state.Time); Metric metric;
            if (!phases.TryGetValue(__originalMethod, out metric)) { metric = new Metric { Name = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name }; phases.Add(__originalMethod, metric); }
            metric.Add(ms);
        }
        private static void FrameBegin(out Stamp __state)
        {
            __state = Recording ? new Stamp { Time = Stopwatch.GetTimestamp(), Bytes = Allocated(), Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2), Generation = generation } : default(Stamp);
        }
        private static int Mode()
        {
            if (Game1.instance == null || !Game1.instance.IsActive) return 3;
            if (JumpGame.instance == null || !JumpGame.instance.IsPlaying() || GameLoop.m_player == null || !GameLoop.m_player.IsAlive) return 0;
            object pause = PauseInstance.GetValue(null); return pause != null && (bool)Paused.GetValue(pause, null) ? 2 : 1;
        }
        private static void Record(string kind, Stamp stamp, long end, double simulationMilliseconds = 0)
        {
            if (stamp.Time == 0 || stamp.Generation != generation || !Recording) return;
            if (count == samples.Length) { dropped++; return; }
            samples[count++] = new Sample { Kind = kind, Mode = Mode(), Start = Milliseconds(stamp.Time - started), Milliseconds = Milliseconds(end - stamp.Time), SimulationMilliseconds = simulationMilliseconds, Allocated = Allocated() - stamp.Bytes, Gen0 = GC.CollectionCount(0) - stamp.Gen0, Gen1 = GC.CollectionCount(1) - stamp.Gen1, Gen2 = GC.CollectionCount(2) - stamp.Gen2 };
        }
        private static void UpdateEnd(GameTime gameTime, Stamp __state) { Record("update", __state, Stopwatch.GetTimestamp(), gameTime.ElapsedGameTime.TotalMilliseconds); }
        private static void DrawEnd(Stamp __state)
        {
            long end = Stopwatch.GetTimestamp(); Record("draw", __state, end);
            if (__state.Time == 0 || __state.Generation != generation || !Recording) return;
            if (previousDraw != 0 && count < samples.Length) samples[count++] = new Sample { Kind = "gap", Mode = Mode(), Start = Milliseconds(previousDraw - started), Milliseconds = Milliseconds(__state.Time - previousDraw) };
            previousDraw = __state.Time;
        }
        private static void PresentEnd(Stamp __state) { Record("present", __state, Stopwatch.GetTimestamp()); }
        private static void TickEnd(Stamp __state)
        {
            long end = Stopwatch.GetTimestamp(); Record("tick", __state, end);
            if (Recording && end - started >= Stopwatch.Frequency * 45) { Finish("45 seconds completed"); Start(); }
        }
        internal static void ExportWindow()
        { if (active) { Finish("manual export"); Start(); } }
        internal static void Finish(string reason)
        {
            if (!active) return; active = false;
            Sink(new Capture { Reason = reason, Utc = DateTime.UtcNow, Generation = generation, Inventory = inventory + LiveSettings()
                + "\r\nMap switches (inclusive, game thread):\r\n" + string.Join("\r\n",switches) + "\r\n" + AnimationDiagnostics.Capture(), Dropped = dropped, Samples = samples.Take(count).ToArray(), Metrics = objects.Values.SelectMany(x => x).Concat(phases.Values).Concat(named.Values).Where(m => m.Count > 0).Select(m => m.Copy()).ToArray() });
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
            var text = new StringBuilder("JK Runtime performance; " + capture.Utc.ToString("o") + "; " + capture.Reason + "\r\nNested timings overlap. Includes cold frames; windows are bounded to 45 seconds. Modes: 0 menu, 1 gameplay, 2 pause, 3 unfocused.\r\nInstrumentation adds CPU work; compare like-for-like captures. Draw submission is not end-to-end display latency.\r\nDropped: " + capture.Dropped + "\r\n");
            foreach (var group in capture.Samples.GroupBy(s => s.Kind + "/" + s.Mode))
            {
                var ordered = group.Select(s => s.Milliseconds).OrderBy(v => v).ToArray();
                text.AppendFormat(CultureInfo.InvariantCulture, "{0}: n={1} median={2:F3} p95={3:F3} max={4:F3} ms; meanAllocated={5:F0} B; GC0/1/2={6}/{7}/{8}\r\n", group.Key, ordered.Length, ordered[ordered.Length / 2], ordered[Math.Min(ordered.Length - 1, (int)(ordered.Length * .95))], ordered.Last(), group.Average(s => s.Allocated), group.Sum(s => s.Gen0), group.Sum(s => s.Gen1), group.Sum(s => s.Gen2));
            }
            var updateSamples = capture.Samples.Where(s => s.Kind == "update").ToArray();
            if (updateSamples.Length > 1)
            {
                double elapsed = updateSamples.Last().Start - updateSamples[0].Start;
                if (elapsed > 0) text.AppendFormat(CultureInfo.InvariantCulture,
                    "Observed native updates/sec: {0:F2}; supplied simulation/wall-time ratio: {1:F3}\r\n",
                    (updateSamples.Length-1)*1000/elapsed, updateSamples.Skip(1).Sum(s=>s.SimulationMilliseconds)/elapsed);
            }
            text.AppendLine("Allocation counter available: " + SupportedAllocations);
            text.AppendLine("Count\tTotal_ms\tMax_ms\tStage");
            foreach (var metric in capture.Metrics.OrderByDescending(m => m.Total)) text.AppendFormat(CultureInfo.InvariantCulture, "{0}\t{1:F3}\t{2:F3}\t{3}\r\n", metric.Count, metric.Total, metric.Maximum, metric.Name);
            text.AppendLine("Loaded packages:"); text.AppendLine(capture.Inventory); return text.ToString();
        }
        private static readonly Dictionary<string, Metric> named = new Dictionary<string, Metric>(StringComparer.Ordinal);
        internal static long BeginMeasure() { return Recording ? Stopwatch.GetTimestamp() : 0; }
        internal static int Generation { get { return generation; } }
        internal static void EndMeasure(string name, long start, int session)
        {
            if (start == 0 || session != generation || !Recording) return;
            Metric metric;
            if (!named.TryGetValue(name, out metric))
            {
                if (named.Count == 256) { dropped++; return; }
                named.Add(name, metric = new Metric { Name = name });
            }
            metric.Add(Milliseconds(Stopwatch.GetTimestamp() - start));
        }
        private static void WriteAsync(Capture capture)
        {
            lock (writeGate)
            {
                if (pending != null) capture.Dropped += pending.Samples.Length;
                pending = capture;
                if (writing) return;
                writing = true;
            }
            if (!ThreadPool.QueueUserWorkItem(delegate { Drain(); }))
            { lock(writeGate) { writing = false; Status = "Report worker unavailable"; } }
        }
        private static void Drain()
        {
            while (true)
            {
                Capture capture;
                lock(writeGate)
                {
                    capture = pending; pending = null;
                    if (capture == null) { writing = false; System.Threading.Monitor.PulseAll(writeGate); return; }
                }
                try
                {
                    // Four owned slots bound disk usage even when the switch stays on.
                    string path = output + "." + (++serial % 4);
                    RuntimeDiagnostics.AtomicWrite(path + ".txt", Format(capture));
                    var csv = new StringBuilder("start_ms,kind,mode,duration_ms,allocated_bytes,gen0,gen1,gen2,simulation_ms\r\n");
                    foreach(var sample in capture.Samples) csv.AppendFormat(CultureInfo.InvariantCulture,
                        "{0:F3},{1},{2},{3:F3},{4},{5},{6},{7},{8:F3}\r\n", sample.Start, sample.Kind, sample.Mode, sample.Milliseconds, sample.Allocated, sample.Gen0, sample.Gen1, sample.Gen2, sample.SimulationMilliseconds);
                    RuntimeDiagnostics.AtomicWrite(path + ".csv", csv.ToString());
                    LastReport = Path.GetFileName(path) + ".txt (" + capture.Utc.ToString("o") + ")";
                }
                catch(Exception error) { Status = "Report write failed: " + error.Message; }
            }
        }
        internal static void Flush()
        {
            lock(writeGate) if (writing) System.Threading.Monitor.Wait(writeGate, 2000);
        }
    }
}
