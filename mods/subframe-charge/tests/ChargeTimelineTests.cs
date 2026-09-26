using JKRuntime.Input;
using System;
using System.Linq;
using System.Runtime.Serialization;
using EntityComponent;
using EntityComponent.BT;
using BehaviorTree;
using JumpKing.Player;
using SubframeCharge;

internal static class ChargeTimelineTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }

    internal static void Run()
    {
        // Microseconds: a press after landing must not be retroactively marked
        // buffered on the following update (the original 34 -> would 32 bug).
        ChargeTimeline clock = new ChargeTimeline();
        Frame(clock, 1000000, false);
        Frame(clock, 1017000, true); // landing; native JumpState visited idle
        clock.BeginFrame(1034000);
        clock.VisitNative();
        long press = 1021170;
        Check(clock.OriginFor(press) == press, "post-landing press lost its original timestamp");
        long release = press + 575810;
        Check(ChargeQuantizer.QuantizeRelease((release - clock.OriginFor(press)) / 1000000.0, 1).Frames == 34,
            "34f regression: spurious buffered origin selected 32f");
        clock.EndFrame();

        // A long delivery while already eligible does not imply buffering.
        Frame(clock, 1200000, true);
        clock.BeginFrame(1400000);
        clock.VisitNative();
        Check(clock.OriginFor(press) == press, "long held input was classified by age");
        clock.EndFrame();

        // Both long and short real buffers use the eligibility UPDATE boundary.
        foreach (long lead in new long[] { 1, 1000, 10000, 50000, 5000000 })
        {
            clock = new ChargeTimeline();
            Frame(clock, 9000000, false);
            clock.BeginFrame(10000000);
            clock.VisitNative();
            Check(clock.OriginFor(10000000 - lead) == 10000000, "airborne time counted as charge");
            // Arbitrary time in physics/handlers cannot alter that origin.
            for (int delay = 0; delay < 100; delay++)
            {
                clock.VisitNative();
                Check(clock.OriginFor(10000000 - lead) == 10000000, "handler delay changed origin");
            }
            clock.EndFrame();
        }

        // Catch-up frames and splat recovery: absence, not wall duration, gates
        // entry. Ice continuation remains eligible when the native node runs.
        clock = new ChargeTimeline();
        Frame(clock, 100, false);
        Frame(clock, 101, false);
        Frame(clock, 102, true);
        clock.BeginFrame(103);
        clock.VisitNative();
        Check(clock.OriginFor(50) == 102, "ice continuation restarted charge");
        clock.EndFrame();
        Frame(clock, 104, false);
        clock.BeginFrame(105);
        clock.VisitNative();
        Check(clock.OriginFor(50) == 105, "recovery reused stale availability");
        clock.EndFrame();

        // Exhaustive sub-ms press phases around landing and all dry/water
        // charge windows; no dependence on the time of the native call.
        for (int phase = -17000; phase <= 17000; phase += 125)
        {
            clock = new ChargeTimeline();
            Frame(clock, 980000, false);
            clock.BeginFrame(1000000);
            clock.VisitNative();
            long origin = clock.OriginFor(1000000 + phase);
            Check(origin == 1000000 + Math.Max(phase, 0), "landing phase origin");
            foreach (float multiplier in new[] { 1f, 0.5f })
                for (int count = 1; count <= (multiplier == 1 ? 35 : 71); count++)
                    Check(ChargeQuantizer.QuantizeRelease(count * .017, multiplier).Frames == count,
                        "frame curve changed at landing");
            clock.EndFrame();
        }

        // Real Entity.UpdateComponents dispatch, including disabled BodyComp.
        // Native BT callback observes CURRENT physics state and still runs its
        // original update; unrelated components retain their relative order.
        Entity owner = new Entity();
        BodyComp body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        clock = new ChargeTimeline();
        bool landed = false;
        bool visit = false;
        ActionComponent physics = new ActionComponent(delegate { landed = true; });
        BehaviorTreeComp tree = new BehaviorTreeComp(new ActionNode(delegate {
            Check(landed, "tree observer ran before current physics");
            Check(clock.InFrame, "disabled body prevented frame observer");
            if (visit) clock.VisitNative();
        }));
        owner.AddComponents(false, body);
        owner.AddComponents(physics, tree);
        Component[] original = owner.GetComponents();
        using (new ChargeFrameComponents(owner, body, tree, clock))
        {
            owner.UpdateComponents(1f / 60f); // unavailable
            Check(!clock.InFrame, "frame observer was not closed");
            landed = false;
            visit = true;
            owner.UpdateComponents(1f / 60f); // current landing/recovery
            Check(clock.EligibleSince == clock.FrameTimestamp, "wrong eligibility update");
        }
        Check(owner.GetComponents().SequenceEqual(original), "observer removal changed unrelated component order");
        TestPauseObserver();
        Console.WriteLine("[OK] Charge timeline: 34f regression, landing phase sweep, held >50ms, catch-up/recovery/ice, native component dispatch and restoration");
    }

    private static void TestPauseObserver()
    {
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(PlayerEntity).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        Entity manager = (Entity)FormatterServices.GetUninitializedObject(type);
        typeof(Entity).GetField("m_components", fields).SetValue(manager, new System.Collections.Generic.List<Component>());
        System.Reflection.FieldInfo instance = type.GetField("instance");
        object previous = instance.GetValue(null);
        instance.SetValue(null, manager);
        ChargeTimeline first = new ChargeTimeline(), second = new ChargeTimeline();
        int swaps = 0;
        // This action runs inside the real Entity component foreach loop.
        manager.AddComponents(new ActionComponent(delegate {
            PauseClockObserver.Detach(first);
            PauseClockObserver.Attach(second);
            swaps++;
        }));
        try
        {
            PauseClockObserver.InstallForLevel();
            int count = manager.GetComponents().Length;
            PauseClockObserver.Attach(first);
            type.GetField("_paused", fields).SetValue(manager, true);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            manager.UpdateComponents(1f / 60f);
            System.Threading.Thread.Sleep(15);
            type.GetField("_paused", fields).SetValue(manager, false);
            manager.UpdateComponents(1f / 60f);
            long ended = System.Diagnostics.Stopwatch.GetTimestamp();
            Check(swaps == 2 && manager.GetComponents().Length == count, "menu toggle mutated enumerated pause components");
            Check(second.ActiveSeconds(started, ended) < (ended - started) / (double)System.Diagnostics.Stopwatch.Frequency,
                "native pause manager observation did not exclude suspended time");
            PauseClockObserver.Detach(second);
            PauseClockObserver.InstallForLevel();
            Check(manager.GetComponents().Length == count, "pause observer duplicated on reinstall");
        }
        finally { PauseClockObserver.Clear(); instance.SetValue(null, previous); }
        // A slow frame without an explicit pause must retain all wall time.
        ChargeTimeline clock = new ChargeTimeline();
        long f = System.Diagnostics.Stopwatch.Frequency;
        Check(Math.Abs(clock.ActiveSeconds(f, f * 2) - 1) < .000001, "slow frame was classified as pause");
        clock.ObservePause(true, f * 2);
        clock.ObservePause(false, f * 3);
        clock.BeginFrame(f * 4); clock.VisitNative();
        Check(clock.OriginFor(f * 5 / 2) == f * 3, "pause-only press origin was not clamped to resume");
        clock.EndFrame();
        Console.WriteLine("[OK] Native pause manager dispatch, menu-toggle list safety, resume origin, slow frame is not pause");
    }

    private static void Frame(ChargeTimeline clock, long stamp, bool visit)
    {
        clock.BeginFrame(stamp);
        if (visit) clock.VisitNative();
        clock.EndFrame();
    }
    private sealed class ActionComponent : Component
    {
        private readonly Action action;
        internal ActionComponent(Action value) { action = value; }
        protected override void Update(float delta) { action(); }
    }
    private sealed class ActionNode : IBTnode
    {
        private readonly Action action;
        internal ActionNode(Action value) { action = value; }
        protected override BTresult MyRun(TickData data) { action(); return BTresult.Running; }
    }
}
