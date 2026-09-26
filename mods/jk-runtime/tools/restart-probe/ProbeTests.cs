using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using JumpKing.Mods;
using JKRestartProbe;

internal static class ProbeTests
{
    private static readonly List<string> calls = new List<string>();
    private static readonly List<Probe.Capture> captures = new List<Probe.Capture>();
    public static void First() { calls.Add("first"); }
    public static void Broken() { calls.Add("broken"); throw new InvalidOperationException("probe-test-error"); }
    public static void Skipped() { calls.Add("skipped"); }
    public static void Last() { calls.Add("last"); }
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    private static MethodInfo Method(string name) { return typeof(ProbeTests).GetMethod(name); }
    private static ModLoader Loader()
    {
        var loader = new ModLoader();
        var a = new ModAssembly(typeof(ProbeTests).Assembly, new JumpKingModAttribute("fixture A"));
        var b = new ModAssembly(typeof(ProbeTests).Assembly, new JumpKingModAttribute("fixture B"));
        a.OnLevelStartMethods.AddRange(new[] { Method("First"), Method("Broken"), Method("Skipped") });
        b.OnLevelStartMethods.Add(Method("Last"));
        loader.LoadedMods.Add(a); loader.LoadedMods.Add(b);
        return loader;
    }
    private sealed class Value
    {
        internal int Number;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal Value(int number) { Number = number; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal int Add(ref int number) { number += Number; return number; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int BranchCalls(bool branch, ref int number)
    {
        var value = new Value(branch ? 3 : 5);
        if (branch) return value.Add(ref number) + Math.Abs(-7);
        return value.Add(ref number) - Math.Abs(-2);
    }
    private sealed class TimedComponent : EntityComponent.Component
    {
        internal int Updates, LateUpdates;
        protected override void Update(float delta) { Updates++; SpendTime(); }
        protected override void LateUpdate(float delta) { LateUpdates++; SpendTime(); }
    }
    private static void SpendTime()
    {
        long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 400;
        while (Stopwatch.GetTimestamp() < until) { }
    }
    // Keep dispatch callers out of Main's JIT compilation before Install.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunEntity(EntityComponent.Entity entity) { entity.UpdateComponents(1f / 60f); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunLoader(ModLoader loader) { loader.CallOnLevelStartMethods(); }
    private static void Main()
    {
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        Probe.Sink = captures.Add;
        var loader = Loader();
        RunLoader(loader);
        var expected = calls.ToArray(); calls.Clear();
        Probe.Install();
        string status = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JKRestartProbe.txt"));
        Check(status.Contains("installed; waiting"), "all installed native dispatch/handoff methods accept diagnostic patches");
        Probe.HandoffBegin();
        RunLoader(loader);
        Probe.Finish();
        Check(expected.SequenceEqual(calls) && string.Join(",", calls) == "first,broken,last", "native callback order, per-mod exception handling and continuation are unchanged");
        var callbackEntries = captures.Last().Entries.Where(e => e.Name == "mod.callback").ToArray();
        Check(callbackEntries.Select(e => e.Method.Name).SequenceEqual(new[] { "First", "Broken", "Last" }), "throwing callbacks retain their own timing and identity");
        Check(captures.Last().Entries.Any(e => e.Method != null && e.Method.Name == "WriteLoadLogs"), "native synchronous log cost is recorded separately");
        bool wrapped = false;
        try { Probe.InvokeCallback(Method("Broken"), null, null); }
        catch (TargetInvocationException e) { wrapped = e.InnerException is InvalidOperationException; }
        Check(wrapped, "reflection exception semantics are preserved");

        int beforeA = 10, beforeB = 10;
        int resultA = BranchCalls(true, ref beforeA), resultB = BranchCalls(false, ref beforeB);
        new Harmony("probe.fixture").Patch(AccessTools.Method(typeof(ProbeTests), "BranchCalls"),
            transpiler: new HarmonyMethod(typeof(Probe), "HandoffCalls"));
        Probe.HandoffBegin();
        int afterA = 10, afterB = 10;
        Check(BranchCalls(true, ref afterA) == resultA && BranchCalls(false, ref afterB) == resultB
            && afterA == beforeA && afterB == beforeB, "call-site instrumentation preserves constructors, branches, return values and ref arguments");
        Probe.Finish();
        Check(captures.Last().Entries.Any(e => e.Method is ConstructorInfo), "constructor time is captured before Runtime callbacks");

        var entity = new EntityComponent.Entity();
        var component = new TimedComponent(); entity.AddComponents(component);
        Probe.HandoffBegin(); RunEntity(entity); Probe.Finish();
        Check(component.Updates == 1 && component.LateUpdates == 1, "native component callbacks execute exactly once");
        Check(captures.Last().Entries.Count(e => e.Subject == typeof(TimedComponent)) == 2, "both slow component phases are attributed");
        entity.Destroy();

        Probe.UpdateBegin(); Probe.DrawBegin();
        Probe.HandoffBegin(); Probe.UpdateEnd();
        var worker = new Thread(delegate() { Probe.InvokeCallback(Method("First"), null, null); });
        worker.Start(); worker.Join();
        for (int i = 0; i < 120; i++) { Probe.DrawBegin(); Probe.DrawEnd(); }
        var frameCapture = captures.Last();
        Check(frameCapture.Entries.Count(e => e.Name == "frame.draw") == 120, "capture ends after 120 draws");
        Check(frameCapture.Entries.Any(e => e.Name == "frame.update" && e.Start < frameCapture.Start), "handoff frame includes work preceding GameLoop entry");
        Check(!frameCapture.Entries.Any(e => e.Name == "mod.callback"), "background callbacks cannot corrupt game-thread trace storage");
        string formatted = Probe.Format(frameCapture);
        Check(formatted.Contains("zero = GameLoop.OnNewRun") && formatted.Contains("dropped: 0"), "trace states its native time origin and record completeness");

        Probe.HandoffBegin();
        for (int i = 0; i < 10000; i++) { Probe.UpdateBegin(); Probe.UpdateEnd(); }
        Probe.Finish();
        Check(captures.Last().Entries.Length == 8192 && captures.Last().Dropped > 0, "storage remains bounded when updates outnumber draws");
        Check(frameCapture.Entries.Count(e => e.Name == "frame.draw") == 120, "completed snapshots are independent of later captures");
        Console.WriteLine("Restart probe checks passed; this fixture does not measure live startup performance.");
    }
}
