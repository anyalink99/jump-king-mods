using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SubframeCharge;
using BehaviorTree;
using JumpKing.Util;

internal static class SubframeChargeTests
{
    private static int failures;

    private static int Main(string[] args)
    {
        AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: -",
            "before any measurement show a neutral placeholder, not an unsupported verdict");
        DirectInputTests.Run(args[0]);
        InputReliabilityTests.Run();
        InputDiagnosticsTests.Run();
        ChargeTimelineTests.Run();
        TestStableSeventeenMillisecondQuantization();
        TestNativeClockAndAllFrameWindows();
        TestObservedMillisecondRanges();
        TestMinimumReleaseReachesNativeJumpPath();
        TestCompletedSubframeTapReplay();
        TestBoundsAndMonotonicity();
        TestSurfaceMultiplier();
        TestQuarterSteps();
        TestSharedIceJumpReferences();
        TestJumpPercentConversion();
        TestJumpPercentMeasurementAndNativePreservation();
        TestMeasurementSettings();
        TestLevelPermission();
        TestPermittedLevelBehaviourRegistration();
        TestCasualPlusDetection();
        TestOneMillisecondSamplerStarts();
        TestKeyboardTransitionCapture();
        TestResetPreservesHeldKeyState();
        TestMultipleJumpKeys();
        TestXInputCustomBinding();
        TestKeyboardAndXInputAggregation();
        TestXInputBindingEvaluation();
        TestXInputChordTransitions();
        TestKeyboardChordTransitions();
        if (failures != 0)
        {
            Console.Error.WriteLine(
                "Subframe Charge tests failed: " + failures);
            return 1;
        }
        Console.WriteLine("[OK] Subframe Charge quantization tests");
        return 0;
    }

    private static void TestStableSeventeenMillisecondQuantization()
    {
        AssertEqual(
            3,
            ChargeQuantizer.Quantize(0.028, 1f),
            "28 ms must select displayed frame 2 / timer step 3");
        AssertEqual(
            2,
            ChargeQuantizer.Quantize(0.001, 1f),
            "a started tap must clamp to displayed frame 1");
        AssertNear(
            3f / 36f,
            ChargeQuantizer.StrengthForStep(3),
            0.000001f,
            "release-inclusive stock strength ratio");
    }

    private static void TestObservedMillisecondRanges()
    {
        AssertEqual(
            2,
            ChargeQuantizer.Quantize(0.024, 1f),
            "24 ms must remain displayed frame 1");
        AssertEqual(
            3,
            ChargeQuantizer.Quantize(0.026, 1f),
            "26 ms must select displayed frame 2");
        AssertEqual(
            3,
            ChargeQuantizer.Quantize(0.042, 1f),
            "42 ms must remain displayed frame 2 (boundary 42.5 ms)");
        AssertEqual(
            5,
            ChargeQuantizer.Quantize(0.068, 1f),
            "68 ms must select displayed frame 4");
        AssertEqual(
            5,
            ChargeQuantizer.Quantize(0.074, 1f),
            "74 ms must remain displayed frame 4");
        AssertEqual(
            5,
            ChargeQuantizer.Quantize(0.076, 1f),
            "76 ms must remain displayed frame 4 (boundary 76.5 ms)");
        AssertEqual(6, ChargeQuantizer.Quantize(0.077, 1f), "77 ms selects frame 5");
    }

    private static void TestNativeClockAndAllFrameWindows()
    {
        AssertNear(17f, (float)TimeSpan.FromSeconds((double)(1f / 60f)).TotalMilliseconds,
            0.00001f, "installed .NET Framework Game1 target interval");
        for (int frame = 1; frame <= 35; frame++)
        {
            double hold = frame * 0.017;
            AssertEqual(frame + 1, ChargeQuantizer.Quantize(hold, 1f), "17 ms grid center " + frame);
            AssertEqual(frame + 1, ChargeQuantizer.Quantize(hold - 0.008499, 1f), "lower edge " + frame);
            AssertEqual(frame + 1, ChargeQuantizer.Quantize(hold + 0.008499, 1f), "upper edge " + frame);
            if (frame < 35)
                AssertEqual(frame + 2, ChargeQuantizer.Quantize(hold + 0.008501, 1f), "next bin " + frame);
        }
        AssertEqual(27, JumpPercentIntegration.ToJumpPercentFrames(ChargeQuantizer.Quantize(0.459, 1f)),
            "controller report regression: 459 ms is 27f, never 28f");
        AssertNear(28f / 60f - 1f / 60f,
            ChargeQuantizer.TimerBeforeNativeRelease(28, 1f / 60f), 0.000001f,
            "17 ms wall-clock must not change 60 Hz simulation timer");
        AssertEqual(36, ChargeQuantizer.Quantize(double.MaxValue, 1f), "huge valid holds saturate without int overflow");
    }

    private static void TestMinimumReleaseReachesNativeJumpPath()
    {
        int step = ChargeQuantizer.Quantize(0.001, 1f);
        float timer = ChargeQuantizer.TimerBeforeNativeRelease(
            step,
            1.0 / ChargeQuantizer.StockStepsPerSecond);
        AssertTrue(
            timer > 0f,
            "minimum release timer must bypass JumpState's zero-timer failure path");
        AssertNear(
            1f / ChargeQuantizer.StockStepsPerSecond,
            timer,
            0.000001f,
            "minimum release timer before native increment");
    }

    private static void TestCompletedSubframeTapReplay()
    {
        double heldSeconds;
        AssertTrue(
            SubframeTapReplay.TryMeasureCompletedTap(
                false,
                true,
                true,
                1000,
                1005,
                1010,
                1000,
                0.05,
                out heldSeconds),
            "a 5 ms press released between updates must be replayed");
        AssertNear(
            0.005f,
            (float)heldSeconds,
            0.000001f,
            "completed tap duration");
        AssertEqual(
            ChargeQuantizer.MinimumStep,
            ChargeQuantizer.Quantize(heldSeconds, 1f),
            "a completed 5 ms tap must select the minimum jump");

        AssertTrue(
            !SubframeTapReplay.TryMeasureCompletedTap(
                true,
                true,
                true,
                1000,
                1005,
                1010,
                1000,
                0.05,
                out heldSeconds),
            "an active native charge must not be replayed");
        AssertTrue(
            !SubframeTapReplay.TryMeasureCompletedTap(
                false,
                true,
                true,
                1000,
                1005,
                1100,
                1000,
                0.05,
                out heldSeconds),
            "an expired tap must not become a delayed buffered jump");
    }

    private static void TestBoundsAndMonotonicity()
    {
        int previous = ChargeQuantizer.MinimumStep;
        for (int milliseconds = 0; milliseconds <= 1000; milliseconds++)
        {
            int step = ChargeQuantizer.Quantize(
                milliseconds / 1000.0,
                1f);
            AssertTrue(step >= previous, "charge steps must be monotonic");
            AssertTrue(
                step >= ChargeQuantizer.MinimumStep && step <= 36,
                "charge step bounds");
            previous = step;
        }
        AssertEqual(
            36,
            ChargeQuantizer.Quantize(10.0, 1f),
            "long holds must clamp to full charge");
    }

    private static void TestSurfaceMultiplier()
    {
        for (int frames = 1; frames <= 71; frames++)
        {
            ChargeResult charge = ChargeQuantizer.QuantizeRelease(frames * 0.017, 0.5f);
            AssertEqual(frames, charge.Frames, "water retains input frame " + frames);
            AssertNear((frames + 1) * 0.5f / 60f, charge.TimerSeconds, 0.000001f,
                "water retains half-step strength " + frames);
            AssertTrue(ChargeQuantizer.TimerBeforeNativeRelease(charge, 0.5f / 60f) > 0,
                "water minimum must not trigger zero-timer failure");
        }
        AssertNear(0.6f, ChargeQuantizer.QuantizeRelease(10, 0.5f).TimerSeconds, 0.000001f, "water full clamp");
    }

    private static void TestQuarterSteps()
    {
        foreach (float scale in new[] { 1f, .5f, .75f, 2f })
        {
            int last = (int)Math.Ceiling((36 / (double)scale - 1) * 4);
            float previous = 0;
            for (int q = 4; q <= last; q++)
            {
                float frames = q / 4f;
                double seconds = frames * .017;
                var charge = ChargeQuantizer.QuantizeRelease(seconds, scale, true);
                float expected = Math.Min(.6f, (frames + 1) * scale / 60);
                AssertNear(frames, charge.ExactFrames, .000001f, "quarter frame count, scale=" + scale);
                AssertNear(expected, charge.TimerSeconds, .000001f, "surface multiplier after quarter quantization");
                AssertTrue(charge.TimerSeconds >= previous, "quarter powers monotone through full charge");
                AssertNear(charge.TimerSeconds, ChargeQuantizer.TimerBeforeNativeRelease(charge, scale / 60f) + scale / 60f,
                    .000001f, "native release increment gives exact quarter charge");
                AssertNear(frames, ChargeQuantizer.QuantizeRelease(seconds - .002124, scale, true).ExactFrames,
                    .000001f, "quarter lower timing boundary");
                AssertNear(frames, ChargeQuantizer.QuantizeRelease(seconds + .002124, scale, true).ExactFrames,
                    .000001f, "quarter upper timing boundary");
                if (q < last) AssertNear(frames + .25f, ChargeQuantizer.QuantizeRelease(seconds + .002126, scale, true).ExactFrames,
                    .000001f, "quarter next timing bin");
                if (q % 4 == 0)
                    AssertNear(ChargeQuantizer.QuantizeRelease(seconds, scale).TimerSeconds, charge.TimerSeconds, .000001f,
                        "integer strengths preserved in quarter mode");
                previous = charge.TimerSeconds;
            }
            AssertNear(ChargeQuantizer.QuantizeRelease(0, scale).TimerSeconds,
                ChargeQuantizer.QuantizeRelease(0, scale, true).TimerSeconds, .000001f, "minimum preserved");
            AssertNear(.6f, ChargeQuantizer.QuantizeRelease(double.MaxValue, scale, true).TimerSeconds, .000001f, "full charge saturates");
        }
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SubframeChargeSettings));
        var old = (SubframeChargeSettings)serializer.Deserialize(new System.IO.StringReader("<SubframeChargeSettings><Enabled>true</Enabled></SubframeChargeSettings>"));
        AssertTrue(!old.QuarterStepCharge, "old settings retain whole-step gameplay");
        old.QuarterStepCharge = true; old.SetRefresh(true); old.SetInputs(false);
        var text = new System.IO.StringWriter(); serializer.Serialize(text, old);
        var restored = (SubframeChargeSettings)serializer.Deserialize(new System.IO.StringReader(text.ToString()));
        AssertTrue(restored.QuarterStepCharge && !restored.HighRefresh && !restored.SubframeInputs, "quarter mode round-trip independent of presentation");
    }

    private static void TestSharedIceJumpReferences()
    {
        TestJumpNode original = new TestJumpNode();
        TestJumpNode replacement = new TestJumpNode();
        BTsequencor ground = new BTsequencor(original);
        BTIsNodeRunning running = new BTIsNodeRunning(original);
        StaticNode airborne = new StaticNode(original, BTresult.Success);
        BTsequencor air = new BTsequencor(running,
            new BTsimultaneous(new PauseNode(1f / 30f), airborne));
        TestGroundNode groundCondition = new TestGroundNode();
        BTsequencor airBranch = new BTsequencor(
            new BTselector(air, StaticNodeSimple.Success), new TestAirNode());
        BTselector root = new BTselector(new BTevaluator(groundCondition, ground), airBranch);
        PlayerEntity player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
        FieldInfo playerJump = typeof(PlayerEntity).GetField("m_jump_state", BindingFlags.NonPublic | BindingFlags.Instance);
        playerJump.SetValue(player, original);
        JKRuntime.Gameplay.JumpNodeBindings bindings = JKRuntime.Gameplay.JumpNodeBindings.Replace(root, player, original, replacement);
        AssertTrue(object.ReferenceEquals(ground.Children[0], replacement), "ground jump reference");
        AssertTrue(object.ReferenceEquals(airborne.Child, replacement), "airborne StaticNode reference");
        AssertTrue(object.ReferenceEquals(running.GetRelatedNodes()[0], replacement), "airborne running check reference");
        AssertTrue(object.ReferenceEquals(playerJump.GetValue(player), replacement), "player registration target");
        FieldInfo particles = typeof(JumpState).GetField("customBlockParticleSpawningActions", BindingFlags.NonPublic | BindingFlags.Instance);
        AssertTrue(object.ReferenceEquals(particles.GetValue(original), particles.GetValue(replacement)),
            "custom particle registrations must be shared with the native jump");
        replacement.RegisterJumpParticleSpawningAction<JumpKing.Level.WaterBlock>(delegate { });
        root.Run(new TickData(1f / 60f, 1));
        groundCondition.Grounded = false;
        root.Run(new TickData(1f / 60f, 2));
        root.Run(new TickData(1f / 60f, 3));
        AssertEqual(3, replacement.Runs, "same charging node must run on ground and both leniency frames");
        root.Run(new TickData(1f / 60f, 4));
        AssertEqual(3, replacement.Runs, "native leniency must expire after two airborne updates");
        AssertEqual(0, replacement.Resumes, "moving into air must not reset active charge");
        AssertEqual(0, original.Runs, "old node must never receive airborne updates");
        bindings.Restore();
        Action registeredParticleAction;
        AssertTrue(original.UnregisterJumpParticleSpawningAction<JumpKing.Level.WaterBlock>(out registeredParticleAction),
            "registration during replacement must survive restoration");
        AssertTrue(object.ReferenceEquals(airborne.Child, original)
            && object.ReferenceEquals(running.GetRelatedNodes()[0], original)
            && object.ReferenceEquals(ground.Children[0], original)
            && object.ReferenceEquals(playerJump.GetValue(player), original), "restore every native jump reference");
    }

    private sealed class TestJumpNode : JumpState
    {
        internal int Runs;
        internal int Resumes;
        internal TestJumpNode() : base(null) { }
        protected override BTresult MyRun(TickData data) { Runs++; return BTresult.Running; }
        protected override void ResumeRun() { Resumes++; base.ResumeRun(); }
    }

    private sealed class TestGroundNode : IBTnode
    {
        internal bool Grounded = true;
        protected override BTresult MyRun(TickData data) { return Grounded ? BTresult.Success : BTresult.Failure; }
    }

    private sealed class TestAirNode : IBTnode
    {
        protected override BTresult MyRun(TickData data) { return BTresult.Running; }
    }

    private static void TestJumpPercentConversion()
    {
        AssertEqual(
            1,
            JumpPercentIntegration.ToJumpPercentFrames(2),
            "Jump% minimum completed charge frame");
        AssertEqual(
            2,
            JumpPercentIntegration.ToJumpPercentFrames(3),
            "Jump% 28 ms charge frame");
        AssertEqual(
            35,
            JumpPercentIntegration.ToJumpPercentFrames(36),
            "Jump% maximum charge frame");
        AssertNear(
            3f / 36f,
            JumpPercentIntegration.ToJumpPercentPercentage(3),
            0.000001f,
            "Jump% corrected percentage");
    }

    private static void TestJumpPercentMeasurementAndNativePreservation()
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
        FieldInfo frames = typeof(JumpPercentIntegration).GetField("jumpFramesProperty", Flags);
        FieldInfo percentage = typeof(JumpPercentIntegration).GetField("jumpPercentageProperty", Flags);
        frames.SetValue(null, typeof(FakeJumpPercent).GetProperty("JumpFrames"));
        percentage.SetValue(null, typeof(FakeJumpPercent).GetProperty("JumpPercentage"));
        try
        {
            FakeJumpPercent.JumpFrames = 27;
            FakeJumpPercent.JumpPercentage = 28f / 36f;
            JumpPercentIntegration.RecordLaunch(null, null, false);
            AssertEqual(27, FakeJumpPercent.JumpFrames, "unsupported device keeps native frames");
            AssertNear(28f / 36f, FakeJumpPercent.JumpPercentage, 0.000001f, "unsupported device keeps native percentage");
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: Not supported", "unsupported device status");
            JumpPercentIntegration.RecordLaunch(29, null, false);
            AssertEqual(27, FakeJumpPercent.JumpFrames, "no correction without a real measurement");
            JumpPercentIntegration.RecordLaunch(13, 0.200, false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 200 ms", "measured time is not reconstructed from 12 frames");
            AssertEqual(12, FakeJumpPercent.JumpFrames, "corrected release updates native display");
            JumpPercentIntegration.RecordLaunch(28, 0.45912, false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 459.12 ms", "preserve sub-millisecond precision");
            AssertEqual(27, FakeJumpPercent.JumpFrames, "long charge corrected frames");
            FakeJumpPercent.JumpFrames = 35;
            JumpPercentIntegration.RecordLaunch(null, 0.595, true);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 595 ms (max)", "automatic takeoff is not a release measurement");
            AssertEqual(35, FakeJumpPercent.JumpFrames, "auto launch keeps native frames");
            JumpPercentIntegration.RecordLaunch(null, null, false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: Not supported", "unsupported next jump clears stale ms");
            JumpPercentIntegration.RecordCharging(0.2);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 200 ms", "live timer has no charging suffix");
            AssertEqual(35, FakeJumpPercent.JumpFrames, "live elapsed time never overwrites native frames");
            JumpPercentIntegration.RecordCharging(null);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: Not supported", "unsupported charge clears previous supported timer");
            JumpPercentIntegration.ResetMeasurement(false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: -", "disabled mode reset clears stale measurement without claiming unsupported input");
            JumpPercentIntegration.ResetMeasurement(true);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: -", "enabled mode reset awaits a fresh measurement");
            ChargeResult water = ChargeQuantizer.QuantizeRelease(0.034, 0.5f);
            JumpPercentIntegration.RecordMeasuredLaunch(water, 0.034, false);
            AssertEqual(2, FakeJumpPercent.JumpFrames, "water display counts input frames, not dry power steps");
            AssertNear(1.5f / 36f, FakeJumpPercent.JumpPercentage, 0.000001f, "water displays native half-step power");
            ChargeResult quarter = ChargeQuantizer.QuantizeRelease(12.25 * .017, .5f, true);
            JumpPercentIntegration.RecordMeasuredLaunch(quarter, 12.25 * .017, false);
            AssertNear(6.625f / 36, FakeJumpPercent.JumpPercentage, .000001f, "fractional water percentage retains eighth-step power");
            AssertTrue(JumpPercentIntegration.FrameLabel(FakeJumpPercent.JumpFrames, FakeJumpPercent.JumpPercentage) == "12.25 frames", "Jump% exact fractional frame label");
            JumpPercentIntegration.RecordCharging(.3);
            AssertTrue(JumpPercentIntegration.FrameLabel(FakeJumpPercent.JumpFrames, FakeJumpPercent.JumpPercentage) == "12.25 frames", "last jump remains fractional during next charge");
            JumpPercentIntegration.RecordBuffered();
            AssertTrue(JumpPercentIntegration.FrameLabel(12, FakeJumpPercent.JumpPercentage) == "12 frames", "native buffer clears fractional label");
            JumpPercentIntegration.RecordMeasuredLaunch(quarter, 12.25 * .017, false);
            AssertTrue(JumpPercentIntegration.FrameLabel(11, .5f) == "11 frames", "foreign result cannot inherit a fractional label");
            FakeJumpPercent.JumpFrames = 14;
            FakeJumpPercent.JumpPercentage = 15f / 36f;
            ChargeResult prediction = ChargeQuantizer.QuantizeRelease(0.250, 1f);
            JumpPercentIntegration.RecordObservedLaunch(prediction, 0.250, false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 250 ms (would 15f)", "show counterfactual only when frames differ");
            AssertEqual(14, FakeJumpPercent.JumpFrames, "observation never overwrites actual native frames");
            AssertNear(15f / 36f, FakeJumpPercent.JumpPercentage, 0.000001f, "observation preserves native percentage");
            JumpPercentIntegration.RecordBuffered();
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: Buffered", "buffer replaces stale ms and would suffix");
            AssertEqual(14, FakeJumpPercent.JumpFrames, "buffer preserves native frames");
            AssertNear(15f / 36f, FakeJumpPercent.JumpPercentage, 0.000001f, "buffer preserves native percentage");
            FakeJumpPercent.JumpFrames = 15;
            JumpPercentIntegration.RecordObservedLaunch(prediction, 0.250, false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 250 ms", "equal frames omit redundant prediction");
            FakeJumpPercent.JumpFrames = 16;
            JumpPercentIntegration.RecordObservedLaunch(prediction, 0.250, false);
            AssertTrue(JumpPercentIntegration.MeasurementText.EndsWith("(would 15f)"), "also show lower predicted frames");
            JumpPercentIntegration.RecordObservedLaunch(prediction, null, false);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: Not supported", "no prediction without measured input");
            JumpPercentIntegration.RecordObservedLaunch(prediction, 0.612, true);
            AssertTrue(JumpPercentIntegration.MeasurementText == "SFC: 612 ms (max)", "automatic max is not a hypothetical release");
        }
        finally
        {
            frames.SetValue(null, null);
            percentage.SetValue(null, null);
        }
    }

    public static class FakeJumpPercent
    {
        public static int JumpFrames { get; set; }
        public static float JumpPercentage { get; set; }
    }

    private static void TestMeasurementSettings()
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SubframeChargeSettings));
        using (var reader = new System.IO.StringReader("<SubframeChargeSettings><Enabled>false</Enabled></SubframeChargeSettings>"))
        {
            var migrated = (SubframeChargeSettings)serializer.Deserialize(reader);
            AssertTrue(!migrated.Enabled && migrated.ShowMeasurement, "old settings retain Enabled and default display on");
        }
        using (var stream = new System.IO.MemoryStream())
        {
            serializer.Serialize(stream, new SubframeChargeSettings { Enabled = false, ShowMeasurement = false });
            stream.Position = 0;
            var restored = (SubframeChargeSettings)serializer.Deserialize(stream);
            AssertTrue(!restored.Enabled && !restored.ShowMeasurement, "independent display choice survives settings roundtrip");
        }
        SettingsStore.EnsureLoaded();
        bool oldVisible = SettingsStore.Current.ShowMeasurement;
        try
        {
            SettingsStore.Current.ShowMeasurement = false;
            JumpPercentIntegration.RecordCharging(0.123);
            typeof(JumpPercentIntegration).GetMethod("DrawMeasurement", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            AssertTrue(!JumpPercentIntegration.ShowMeasurement && JumpPercentIntegration.MeasurementText == "SFC: 123 ms",
                "hidden overlay neither draws nor disables measurement");
        }
        finally { SettingsStore.Current.ShowMeasurement = oldVisible; }
    }

    private static void TestLevelPermission()
    {
        AssertTrue(
            !LevelPermission.AllowsSubframeCharge(null),
            "missing level tags must keep the modified-run marker");
        AssertTrue(
            LevelPermission.AllowsSubframeCharge(
                new[] { "AllowSubframeCharge" }),
            "the Subframe Charge level tag must allow the mod");
        AssertTrue(
            !LevelPermission.AllowsSubframeCharge(
                new[] { "allowsubframecharge" }),
            "the level tag must be exact and case-sensitive");
        AssertTrue(
            !LevelPermission.AllowsSubframeCharge(
                new[] { "AllowJetpack", "AllowBallKing" }),
            "other mod permissions must remain independent");
    }

    private static void TestPermittedLevelBehaviourRegistration()
    {
        FieldInfo behavioursField = typeof(BodyComp).GetField(
            "m_behaviours",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo externalCountField = typeof(BodyComp).GetField(
            "m_externalBehavioursCount",
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue(
            behavioursField != null && externalCountField != null,
            "installed BodyComp behaviour contract");
        if (behavioursField == null || externalCountField == null)
        {
            return;
        }

        BodyComp body = (BodyComp)FormatterServices.GetUninitializedObject(
            typeof(BodyComp));
        LinkedList<IBodyCompBehaviour> behaviours =
            new LinkedList<IBodyCompBehaviour>();
        TestBodyBehaviour anchor = new TestBodyBehaviour();
        TestBodyBehaviour inserted = new TestBodyBehaviour();
        behaviours.AddLast(anchor);
        behavioursField.SetValue(body, behaviours);

        JKRuntime.Gameplay.BodyPipeline registry =
            new JKRuntime.Gameplay.BodyPipeline(body, false);
        AssertTrue(
            registry.RegisterBefore(inserted, anchor),
            "permitted level must insert the lifecycle behaviour");
        AssertTrue(
            object.ReferenceEquals(behaviours.First.Value, inserted),
            "permitted registration order");
        AssertEqual(
            0,
            Convert.ToInt32(externalCountField.GetValue(body)),
            "permitted level must not increment the external behaviour count");
        AssertTrue(
            registry.Remove(inserted),
            "permitted level must remove its lifecycle behaviour");
        AssertEqual(
            0,
            Convert.ToInt32(externalCountField.GetValue(body)),
            "permitted removal must not decrement another mod's count");
    }

    private sealed class Rules : JKRuntime.Gameplay.IMovementRules
    { public JKRuntime.Gameplay.MovementMode Value; public JKRuntime.Gameplay.MovementMode Mode { get { return Value; } } }
    private static void TestCasualPlusDetection()
    {
        var rules = new Rules { Value = JKRuntime.Gameplay.MovementMode.AirControl };
        using (JKRuntime.Gameplay.GameFeatures.RegisterMovement(rules))
        {
            AssertTrue(JKRuntime.Gameplay.GameFeatures.Movement != JKRuntime.Gameplay.MovementMode.VariableJump, "Casual keeps native charge");
            rules.Value = JKRuntime.Gameplay.MovementMode.VariableJump;
            AssertTrue(JKRuntime.Gameplay.GameFeatures.Movement == JKRuntime.Gameplay.MovementMode.VariableJump, "Casual+ owns jump implementation");
        }
    }

    private static void TestKeyboardTransitionCapture()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            AssertTrue(
                sampler.Available,
                "keyboard sampler test source must start");
            sampler.Configure(new[] { 32 }, true);
            sampler.ObserveKey(65, true, 5);
            sampler.ObserveKey(32, true, 10);
            sampler.ObserveKey(32, true, 11);
            sampler.ObserveKey(32, false, 20);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && transition.IsDown
                    && transition.Timestamp == 10,
                "keyboard sampler must capture one press edge");
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && !transition.IsDown
                    && transition.Timestamp == 20,
                "keyboard sampler must capture one release edge");
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "keyboard sampler must ignore repeats and unrelated keys");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestResetPreservesHeldKeyState()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            sampler.Configure(new[] { 32 }, true);
            sampler.ObserveKey(32, true, 10);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition) && transition.IsDown,
                "initial key press must be captured");
            sampler.Reset();
            sampler.ObserveKey(32, true, 20);
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "queue reset must not turn auto-repeat into a new press");
            sampler.ObserveKey(32, false, 30);
            AssertTrue(
                sampler.TryDequeue(out transition) && !transition.IsDown,
                "release after queue reset must remain observable");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestMultipleJumpKeys()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            sampler.Configure(new[] { 32, 13 }, true);
            sampler.ObserveKey(32, true, 10);
            sampler.ObserveKey(13, true, 20);
            sampler.ObserveKey(32, false, 30);
            sampler.ObserveKey(13, false, 40);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && transition.IsDown
                    && transition.Timestamp == 10,
                "first bound key must start the effective press");
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && !transition.IsDown
                    && transition.Timestamp == 40,
                "last bound key release must end the effective press");
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "overlapping bound keys must not create extra edges");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestXInputCustomBinding()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            sampler.Configure(
                new int[0],
                new[]
                {
                    new XInputJumpBinding(1, new[] { 8192 })
                },
                true);
            sampler.ObserveXInputButton(1, 4096, true, 5);
            sampler.ObserveXInputButton(1, 8192, true, 10);
            sampler.ObserveXInputButton(1, 8192, false, 20);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && transition.IsDown
                    && transition.Timestamp == 10,
                "custom XInput Jump binding must capture its press");
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && !transition.IsDown
                    && transition.Timestamp == 20,
                "custom XInput Jump binding must capture its release");
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "unbound XInput buttons must be ignored");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestKeyboardAndXInputAggregation()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            sampler.Configure(
                new[] { 32 },
                new[]
                {
                    new XInputJumpBinding(0, new[] { 4096 })
                },
                true);
            sampler.ObserveXInputButton(0, 4096, true, 10);
            sampler.ObserveKey(32, true, 20);
            sampler.ObserveXInputButton(0, 4096, false, 30);
            sampler.ObserveKey(32, false, 40);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && transition.IsDown
                    && transition.Timestamp == 10,
                "first device must start the combined Jump press");
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && !transition.IsDown
                    && transition.Timestamp == 40,
                "last device release must end the combined Jump press");
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "overlapping keyboard and XInput must not create extra edges");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestXInputBindingEvaluation()
    {
        GamePadState state = new GamePadState(
            new Vector2(0.8f, 0f),
            Vector2.Zero,
            0.75f,
            0f,
            Buttons.A | Buttons.LeftTrigger);
        AssertTrue(
            HighRateInputSampler.IsXInputBindingDown(
                state,
                new XInputJumpBinding(
                    0,
                    new[] { (int)Buttons.A })),
            "XInput face-button binding must be evaluated");
        AssertTrue(
            HighRateInputSampler.IsXInputBindingDown(
                state,
                new XInputJumpBinding(0, new[] { 1073741824 })),
            "XInput stick-direction binding must be evaluated");
        AssertTrue(
            HighRateInputSampler.IsXInputBindingDown(
                state,
                new XInputJumpBinding(0, new[] { 8388608 })),
            "XInput trigger binding must be evaluated");
        AssertTrue(
            !HighRateInputSampler.IsXInputBindingDown(
                state,
                new XInputJumpBinding(
                    0,
                    new[] { (int)Buttons.B })),
            "unpressed XInput binding must remain inactive");
        AssertTrue(
            HighRateInputSampler.IsXInputBindingDown(
                state,
                new XInputJumpBinding(
                    0,
                    new[]
                    {
                        new[] { (int)Buttons.A, 1073741824 }
                    })),
            "complete XInput chord must be evaluated");
        AssertTrue(
            !HighRateInputSampler.IsXInputBindingDown(
                state,
                new XInputJumpBinding(
                    0,
                    new[]
                    {
                        new[] { (int)Buttons.A, (int)Buttons.B }
                    })),
            "partial XInput chord must remain inactive");
    }

    private static void TestXInputChordTransitions()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            sampler.Configure(
                new int[0],
                new[]
                {
                    new XInputJumpBinding(
                        0,
                        new[]
                        {
                            new[]
                            {
                                (int)Buttons.LeftShoulder,
                                (int)Buttons.A
                            }
                        })
                },
                true);
            sampler.ObserveXInputButton(
                0,
                (int)Buttons.LeftShoulder,
                true,
                10);
            sampler.ObserveXInputButton(
                0,
                (int)Buttons.A,
                true,
                20);
            sampler.ObserveXInputButton(
                0,
                (int)Buttons.LeftShoulder,
                false,
                30);
            sampler.ObserveXInputButton(
                0,
                (int)Buttons.A,
                false,
                40);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && transition.IsDown
                    && transition.Timestamp == 20,
                "last chord button must start the Jump press");
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && !transition.IsDown
                    && transition.Timestamp == 30,
                "first chord release must end the Jump press");
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "partial XInput chords must not create extra transitions");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestKeyboardChordTransitions()
    {
        HighRateInputSampler sampler = new HighRateInputSampler(true);
        try
        {
            sampler.Start();
            sampler.Configure(
                new[] { new[] { 16, 32 } },
                new XInputJumpBinding[0],
                true);
            sampler.ObserveKey(16, true, 10);
            sampler.ObserveKey(32, true, 20);
            sampler.ObserveKey(16, false, 30);
            sampler.ObserveKey(32, false, 40);

            JumpInputTransition transition;
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && transition.IsDown
                    && transition.Timestamp == 20,
                "last keyboard chord key must start the Jump press");
            AssertTrue(
                sampler.TryDequeue(out transition)
                    && !transition.IsDown
                    && transition.Timestamp == 30,
                "first keyboard chord release must end the Jump press");
            AssertTrue(
                !sampler.TryDequeue(out transition),
                "partial keyboard chords must not create extra transitions");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private static void TestOneMillisecondSamplerStarts()
    {
        HighRateInputSampler sampler = new HighRateInputSampler();
        try
        {
            sampler.Start();
            AssertTrue(
                sampler.Available,
                "one-millisecond physical keyboard sampler must start");
        }
        finally
        {
            sampler.Dispose();
        }
    }

    private sealed class TestBodyBehaviour : IBodyCompBehaviour
    {
        public bool ExecuteBehaviour(BehaviourContext context)
        {
            return true;
        }
    }

    private static void AssertEqual(int expected, int actual, string name)
    {
        if (expected != actual)
        {
            Fail(name + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertNear(
        float expected,
        float actual,
        float tolerance,
        string name)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            Fail(name + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertTrue(bool value, string name)
    {
        if (!value)
        {
            Fail(name);
        }
    }

    private static void Fail(string message)
    {
        failures++;
        Console.Error.WriteLine("[FAIL] " + message);
    }
}
