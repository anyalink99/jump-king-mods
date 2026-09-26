using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Mods;
using Microsoft.Xna.Framework;

// A temporary, independent diagnostic mod. No Runtime dependency, settings,
// input hooks, save hooks, clock resets or changes to native return values.
namespace JKRestartProbe
{
    [JumpKingMod("JK Restart Probe")]
    public static class Probe
    {
        internal struct Entry
        {
            internal string Name;
            internal MethodBase Method;
            internal Type Subject;
            internal long Start, End;
        }
        internal sealed class Capture
        {
            internal Entry[] Entries;
            internal long Start;
            internal int Attempt, Dropped, Gen0, Gen1, Gen2;
        }
        private const string Owner = "jk-runtime.restart-probe";
        private static readonly Entry[] entries = new Entry[8192];
        private static readonly List<MethodBase> callSites = new List<MethodBase>();
        private static readonly long[] callStarts = new long[256];
        private static bool installed, active;
        private static int gameThread, count, dropped, frames, attempt, gen0, gen1, gen2;
        private static long started, updateStart, drawStart, previousDraw;
        private static string output;
        private static readonly object gate = new object();
        private static Capture pending;
        private static bool writing;
        private static readonly Queue<string> history = new Queue<string>();
        internal static Action<Capture> Sink = Enqueue;
        private static bool Recording { get { return active && Thread.CurrentThread.ManagedThreadId == gameThread; } }

        [BeforeLevelLoad]
        public static void Install()
        {
            if (installed) return;
            output = Path.Combine(Path.GetDirectoryName(typeof(Probe).Assembly.Location), "JKRestartProbe.txt");
            var harmony = new Harmony(Owner);
            try
            {
                // Patch callees before their callers, so the .NET Framework JIT
                // cannot inline the uninstrumented dispatch into the new callers.
                foreach (var name in new[] { "LowUpdate", "LowLateUpdate" })
                    Patch(harmony, AccessTools.Method(typeof(EntityComponent.Component), name), "WorkBegin", "ObjectEnd", null);
                Patch(harmony, AccessTools.Method(typeof(EntityComponent.Entity), "UpdateComponents"), "WorkBegin", "ObjectEnd", null);
                foreach (var type in new[] { typeof(EntityComponent.EntityManager), typeof(JumpKing.Level.LevelManager),
                    typeof(JumpKing.Controller.ControllerManager), typeof(WeatherManager) })
                    Patch(harmony, AccessTools.Method(type, "Update"), "WorkBegin", "WorkEnd", null);
                Patch(harmony, AccessTools.Method(typeof(ModLoader), "WriteLoadLogs"), "WorkBegin", "WorkEnd", null);
                Patch(harmony, AccessTools.Method(typeof(ModLoader), "CallOnLevelStartMethods"), "WorkBegin", "WorkEnd", "CallbackCalls");
                Patch(harmony, AccessTools.Method(typeof(GameLoop), "OnNewRun"), "HandoffBegin", "HandoffEnd", "HandoffCalls");
                Patch(harmony, AccessTools.Method(typeof(JumpGame), "Update"), "WorkBegin", "WorkEnd", null);
                Patch(harmony, AccessTools.Method(typeof(Game1), "Update"), "UpdateBegin", "UpdateEnd", null);
                Patch(harmony, AccessTools.Method(typeof(Game), "DoDraw"), "DrawBegin", "DrawEnd", null);
                installed = true;
                File.WriteAllText(output, "JK Restart Probe 1; installed; waiting for the intro-to-control transition.\r\n"
                    + "Runtime's separate startup trace should remain disabled.\r\n"
                    + "Game: " + typeof(Game1).Assembly.Location + "\r\n");
            }
            catch (Exception error)
            {
                harmony.UnpatchAll(Owner);
                try { File.WriteAllText(output, "JK Restart Probe installation failed: " + error); } catch { }
            }
        }
        private static void Patch(Harmony harmony, MethodBase method, string prefix, string postfix, string transpiler)
        {
            if (method == null) throw new MissingMethodException("Restart probe native contract did not match");
            harmony.Patch(method, Hook(prefix, Priority.First), Hook(postfix, Priority.Last), Hook(transpiler, Priority.Normal));
        }
        private static HarmonyMethod Hook(string name, int priority)
        { return name == null ? null : new HarmonyMethod(typeof(Probe), name) { priority = priority }; }

        internal static void HandoffBegin()
        {
            Finish();
            gameThread = Thread.CurrentThread.ManagedThreadId;
            started = Stopwatch.GetTimestamp();
            count = dropped = frames = 0; attempt++;
            gen0 = GC.CollectionCount(0); gen1 = GC.CollectionCount(1); gen2 = GC.CollectionCount(2);
            active = true;
        }
        private static void HandoffEnd()
        { Record("handoff.complete", null, null, started, Stopwatch.GetTimestamp()); }
        private static void WorkBegin(out long __state)
        { __state = Recording ? Stopwatch.GetTimestamp() : 0; }
        private static void WorkEnd(MethodBase __originalMethod, long __state)
        { Record("phase", __originalMethod, null, __state, Stopwatch.GetTimestamp()); }
        private static void ObjectEnd(object __instance, MethodBase __originalMethod, long __state)
        {
            if (__state == 0) return;
            long end = Stopwatch.GetTimestamp();
            if (end - __state >= Stopwatch.Frequency / 2000)
                Record(__originalMethod.Name, null, __instance.GetType(), __state, end);
        }
        internal static void UpdateBegin() { updateStart = Stopwatch.GetTimestamp(); }
        internal static void UpdateEnd()
        {
            Record("frame.update", null, null, updateStart, Stopwatch.GetTimestamp());
        }
        internal static void DrawBegin()
        {
            drawStart = Stopwatch.GetTimestamp();
            Record("frame.gap", null, null, previousDraw, drawStart);
            previousDraw = drawStart;
        }
        internal static void DrawEnd()
        {
            if (!Recording) return;
            Record("frame.draw", null, null, drawStart, Stopwatch.GetTimestamp());
            if (++frames == 120) Finish();
        }
        private static void Record(string name, MethodBase method, Type subject, long begin, long end)
        {
            if (begin == 0 || !Recording) return;
            if (count == entries.Length) { dropped++; return; }
            entries[count++] = new Entry { Name = name, Method = method, Subject = subject, Start = begin, End = end };
        }

        // The native loader retains its own reflection invocation and exception
        // handling. This wrapper also measures callbacks that throw; it never
        // swallows or unwraps TargetInvocationException.
        internal static object InvokeCallback(MethodBase method, object target, object[] args)
        {
            long begin = Recording ? Stopwatch.GetTimestamp() : 0;
            try { return method.Invoke(target, args); }
            finally { Record("mod.callback", method, null, begin, Stopwatch.GetTimestamp()); }
        }
        internal static IEnumerable<CodeInstruction> CallbackCalls(IEnumerable<CodeInstruction> source)
        {
            var invoke = AccessTools.Method(typeof(MethodBase), "Invoke", new[] { typeof(object), typeof(object[]) });
            int replaced = 0;
            foreach (var instruction in source)
            {
                if (instruction.Calls(invoke))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Probe), "InvokeCallback");
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Unexpected native callback dispatch contract");
        }
        internal static void CallBegin(int id)
        { callStarts[id] = Recording ? Stopwatch.GetTimestamp() : 0; }
        internal static void CallEnd(int id)
        { Record("handoff.call", callSites[id], null, callStarts[id], Stopwatch.GetTimestamp()); }
        internal static IEnumerable<CodeInstruction> HandoffCalls(IEnumerable<CodeInstruction> source)
        {
            foreach (var instruction in source)
            {
                // This native body has no exception regions or constrained/tail
                // calls. Fail closed if a different build changes that contract.
                if (instruction.blocks.Count != 0 || instruction.opcode.OpCodeType == OpCodeType.Prefix)
                    throw new InvalidOperationException("Unsupported handoff IL structure");
                var method = instruction.operand as MethodBase;
                if (method == null || (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt && instruction.opcode != OpCodes.Newobj))
                { yield return instruction; continue; }
                int id = callSites.IndexOf(method);
                if (id < 0)
                {
                    id = callSites.Count;
                    if (id >= callStarts.Length) throw new InvalidOperationException("Handoff call-site limit exceeded");
                    callSites.Add(method);
                }
                var begin = new CodeInstruction(OpCodes.Ldc_I4, id);
                begin.labels.AddRange(instruction.labels); instruction.labels.Clear();
                yield return begin;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Probe), "CallBegin"));
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldc_I4, id);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Probe), "CallEnd"));
            }
        }
        internal static void Finish()
        {
            if (!Recording) return;
            active = false;
            var copy = new Entry[count]; Array.Copy(entries, copy, count);
            var capture = new Capture { Entries = copy, Start = started, Attempt = attempt, Dropped = dropped,
                Gen0 = GC.CollectionCount(0) - gen0, Gen1 = GC.CollectionCount(1) - gen1, Gen2 = GC.CollectionCount(2) - gen2 };
            try { Sink(capture); } catch { /* Diagnostics must not break play. */ }
        }
        private static void Enqueue(Capture capture)
        {
            lock (gate) { pending = capture; if (writing) return; writing = true; }
            try { if (!ThreadPool.QueueUserWorkItem(delegate { Drain(); })) throw new InvalidOperationException(); }
            catch { lock (gate) { pending = null; writing = false; } }
        }
        private static void Drain()
        {
            while (true)
            {
                Capture capture;
                lock (gate) { capture = pending; pending = null; if (capture == null) { writing = false; return; } }
                try
                {
                    if (history.Count == 6) history.Dequeue();
                    history.Enqueue(Format(capture));
                    File.WriteAllText(output, string.Join("\r\n", history.ToArray()));
                }
                catch { }
            }
        }
        internal static string Format(Capture capture)
        {
            var text = new StringBuilder();
            text.AppendLine("JK Restart Probe 1; attempt " + capture.Attempt + "; zero = GameLoop.OnNewRun entry (intro ends)");
            text.AppendLine("GC collections: " + capture.Gen0 + "/" + capture.Gen1 + "/" + capture.Gen2 + "; dropped: " + capture.Dropped);
            text.AppendLine("start_ms\tduration_ms\tstage (nested timings overlap; frame.draw includes presentation)");
            foreach (var entry in capture.Entries)
            {
                string subject = entry.Method == null ? (entry.Subject == null ? "" : entry.Subject.FullName)
                    : entry.Method.DeclaringType.FullName + "." + entry.Method.Name + " [" + entry.Method.Module.Assembly.GetName().Name + "]";
                text.AppendLine(((entry.Start - capture.Start) * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture)
                    + "\t" + ((entry.End - entry.Start) * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture)
                    + "\t" + entry.Name + (subject.Length == 0 ? "" : ": " + subject));
            }
            return text.ToString();
        }
    }
}
