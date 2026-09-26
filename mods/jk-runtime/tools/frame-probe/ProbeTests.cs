using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityComponent;
using JKFrameProbe;

internal static class ProbeTests
{
    private sealed class Counter : Component
    {
        internal int Updates, LateUpdates;
        protected override void Update(float delta) { Updates++; }
        protected override void LateUpdate(float delta) { LateUpdates++; }
    }
    private sealed class EmptyEntity : Entity { internal EmptyEntity() : base(false) { } }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    [MethodImpl(MethodImplOptions.NoInlining)] private static void Dispatch(Entity entity) { entity.UpdateComponents(.017f); }
    private static void Main()
    {
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var captures = new List<Probe.Capture>(); Probe.Sink = captures.Add;
        new JumpKing.Mods.ModLoader(); Probe.Install();
        Check(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JKFrameProbe.status.txt")).StartsWith("Installed"), "installed-game methods accept all diagnostic hooks");
        var entity = new EmptyEntity(); var counter = new Counter(); entity.AddComponents(counter);
        Probe.Start(); typeof(Probe).GetField("warmUntil", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0L);
        for (int i = 0; i < 30; i++) Dispatch(entity); Probe.End();
        Check(counter.Updates == 30 && counter.LateUpdates == 30, "native component callback counts unchanged");
        Check(captures.Count == 1 && captures[0].Metrics.Any(m => m.Name.EndsWith("Counter.LowUpdate") && m.Count == 30), "component metrics observe actual callbacks");
        int n = captures[0].Metrics.Sum(m => m.Count); Dispatch(entity); Probe.End();
        Check(captures.Count == 1 && captures[0].Metrics.Sum(m => m.Count) == n, "completed capture is detached and capture stops");
        Check(Probe.Format(captures[0]).Contains("First 2 seconds excluded"), "report distinguishes measured warm interval");
        Check(!Probe.Rearm(false) && Probe.Rearm(true) && !Probe.Rearm(true), "completed probe rearms only for an eligible game and never interrupts an active capture");
        Probe.End(); Check(Probe.Rearm(true), "second extra diagnostic window can start"); Probe.End(); Check(!Probe.Rearm(true), "automatic recording remains bounded");
        var watch = Stopwatch.StartNew(); for (int i = 0; i < 100000; i++) Dispatch(entity); watch.Stop();
        Console.WriteLine("Inactive dispatch fixture: " + watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms / 100000 entity updates");
    }
}
