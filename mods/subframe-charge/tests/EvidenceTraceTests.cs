using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Input;
using JumpKing.Player;
using SubframeCharge;

internal static class EvidenceTraceTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static int Main()
    {
        var output = new List<string>();
        var window = new EvidenceWindow(3, output.Add);
        for (int i = 0; i < 6; i++) window.Add("frame" + i, i);
        Check(output.Count == 0, "Healthy history must stay in memory");
        window.Trigger("reason=missing-release", 5);
        Check(output.Count == 4 && output[1].EndsWith("frame3") && output[3].EndsWith("frame5"), "Incident lost ordered prehistory");
        window.Add("late-edge", 6);
        Check(output.Last().Contains("after") && output.Last().EndsWith("late-edge"), "Late release not retained after failure");
        int before = output.Count;
        window.Trigger("reason=second-press", 7);
        Check(output.Count == before + 1 && output.Last().Contains("related"), "Overlapping incidents redumped history");
        window.Add("closed", 38);
        Check(output.Last().Contains("end"), "Post-window never closed");

        // Exercise the real Runtime observation history with a separate consumer.
        // Reading diagnostic evidence must neither dequeue nor reconfigure it.
        Type sourceType = typeof(SharedKeyboard).Assembly.GetType("JKRuntime.Input.KeyboardSource", true);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        bool down = false;
        Func<int,short> reader = key => (short)(down && key == 32 ? -32768 : 0);
        object source = Activator.CreateInstance(sourceType, flags, null,
            new object[] { reader, new Func<IntPtr>(() => new IntPtr(42)), false }, null);
        var subscription = (KeyboardSubscription)sourceType.GetMethod("Subscribe", flags).Invoke(source, new object[] { new[] { 32 } });
        var poll = sourceType.GetMethod("Poll", flags);
        var cursor = typeof(KeyboardSubscription).GetField("Cursor", flags);
        var generation = sourceType.GetField("generation", flags);
        long startCursor = (long)cursor.GetValue(subscription), startGeneration = (long)generation.GetValue(source);
        poll.Invoke(source, null); down = true; poll.Invoke(source, null);
        var sampler = new HighRateInputSampler(true);
        sampler.Start(); sampler.Configure(new[] { 32 }, true);
        var observe = typeof(HighRateInputSampler).GetMethod("ObserveKey", flags);
        observe.Invoke(sampler, new object[] { 32, true, System.Diagnostics.Stopwatch.GetTimestamp() });
        var input = new InputComponent();
        var canJump = typeof(InputComponent).GetField("_can_jump", flags);
        bool canBefore = (bool)canJump.GetValue(input);
        var jump = FormatterServices.GetUninitializedObject(typeof(JumpState));
        var trace = new ChargeEvidenceTrace(source);
        trace.Capture("test", null, 1, 1, input, null, jump, sampler, new[] { new[] { 32 } }, 0,0,0,false,false,false,false,0,null);
        Check((long)cursor.GetValue(subscription) == startCursor && (long)generation.GetValue(source) == startGeneration,
            "Diagnostics consumed or reconfigured shared physical history");
        Check((bool)canJump.GetValue(input) == canBefore, "Diagnostics consumed native jump eligibility");
        JumpInputTransition edge;
        Check(sampler.TryDequeue(out edge) && edge.IsDown, "Diagnostics consumed gameplay sampler edge");
        KeyboardSample sample;
        Check(subscription.TryRead(out sample) && !sample.IsDown(32) && subscription.TryRead(out sample) && sample.IsDown(32),
            "Diagnostics changed another observer's physical samples");
        trace.Incident("synthetic-missing-release", 1, 1);
        DiagnosticLog.Flush();
        var benchmark = new ChargeEvidenceTrace(source);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            benchmark.Capture("benchmark", null, i, 0, input, null, jump, sampler, new[] { new[] { 32 } }, 0,0,0,false,false,false,false,0,null);
        clock.Stop();
        Console.WriteLine("[PERF] Diagnostic capture, synthetic idle input: " + (clock.Elapsed.TotalMilliseconds / 1000).ToString("F4") + " ms/capture");
        subscription.Dispose(); sampler.Dispose();
        Console.WriteLine("[OK] Diagnostic pre/post history, overlapping incidents, native eligibility and independent Runtime queues preserved");
        return 0;
    }
}
