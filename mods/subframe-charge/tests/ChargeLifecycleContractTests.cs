using JKRuntime.Input;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using JumpKing.Player;
using System.Collections.Generic;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.BlockBehaviours;

// Runs the real compiled SubframeChargeState.MyRun and sampler queue.
// Only native/environment boundaries are stubbed, not SF's bookkeeping.
// This is a lifecycle test, not a simulation or hardware timing test.
internal static class ChargeLifecycleContractTests
{
    private const BindingFlags Private = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static BTresult nativeResult;
    private static bool frameJump;
    private static bool unsupported;
    private static BodyComp body;
    private static InputComponent input;
    private static bool runNative;
    private static float multiplier = 1f;
    private static float launchIntensity;
    private static bool runImpulse;
    private static int direction;
    private static PlayerEntity impulsePlayer;

    internal static void Run(Assembly mod, Assembly harmonyAssembly)
    {
        Type stateType = mod.GetType("SubframeCharge.SubframeChargeState", true);
        Type samplerType = typeof(HighRateInputSampler);
        Type integration = mod.GetType("SubframeCharge.JumpPercentIntegration", true);
        Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
        Type harmonyMethod = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);
        object harmony = Activator.CreateInstance(harmonyType, new object[] { "SubframeCharge.Tests.Lifecycle" });
        Patch(harmony, harmonyType, harmonyMethod, stateType.GetMethod("IsGameActive", Private), "ReturnTrue");
        Patch(harmony, harmonyType, harmonyMethod, stateType.GetMethod("ConfigureSampler", Private), "ReturnTrue");
        Patch(harmony, harmonyType, harmonyMethod, stateType.GetMethod("HasUnsupportedJumpDown", Private), "Unsupported");
        Patch(harmony, harmonyType, harmonyMethod, stateType.GetMethod("GetChargeMultiplier", Private), "Multiplier");
        Patch(harmony, harmonyType, harmonyMethod, typeof(PlayerNode).GetProperty("body").GetGetMethod(), "Body");
        Patch(harmony, harmonyType, harmonyMethod, typeof(PlayerNode).GetProperty("input").GetGetMethod(), "Input");
        Patch(harmony, harmonyType, harmonyMethod, typeof(InputComponent).GetMethod("GetState"), "InputState");
        Patch(harmony, harmonyType, harmonyMethod, typeof(JumpState).GetMethod("MyRun", Private), "NativeRun");
        body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        input = (InputComponent)FormatterServices.GetUninitializedObject(typeof(InputComponent));
        object state = FormatterServices.GetUninitializedObject(stateType);
        Type timelineType = mod.GetType("SubframeCharge.ChargeTimeline", true);
        object timeline = Activator.CreateInstance(timelineType, true);
        stateType.GetField("Timeline", Private).SetValue(state, timeline);
        object sampler = Activator.CreateInstance(samplerType, Private, null, new object[] { true }, null);
        samplerType.GetMethod("Start", Private).Invoke(sampler, null);
        samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
            .Invoke(sampler, new object[] { new[] { 32 }, true });
        stateType.GetField("sampler", Private).SetValue(state, sampler);
        stateType.GetField("lastGameActive", Private).SetValue(state, true);
        Type probeType = mod.GetType("SubframeCharge.JumpTrajectoryProbe", true);
        object probe = FormatterServices.GetUninitializedObject(probeType);
        probeType.GetField("body", Private).SetValue(probe, body);
        stateType.GetField("trajectoryProbe", Private).SetValue(state, probe);
        MethodInfo run = stateType.GetMethod("MyRun", Private);
        MethodInfo observe = samplerType.GetMethod("ObserveKey", Private);
        MethodInfo clear = stateType.GetMethod("ClearSampledCharge", Private);
        PropertyInfo text = integration.GetProperty("MeasurementText", Private);
        object[] tick = { new TickData(1f / 60f, 0) };
        try
        {
            // Warm native-boundary detours before constructing recent timestamps.
            nativeResult = BTresult.Failure;
            RunFrame(state, run, tick);
            // External native ResetResult (e.g. conveyor exit) cancels only
            // active charge bookkeeping, not a new pending sampler edge.
            stateType.GetField("sampledPress", Private).SetValue(state, true);
            ((IBTnode)state).ResetResult();
            Require((bool)stateType.GetField("sampledPress", Private).GetValue(state), "idle reset lost early sampled press");
            stateType.GetField("nativeCharging", Private).SetValue(state, true);
            stateType.GetField("sampledRelease", Private).SetValue(state, true);
            stateType.GetField("deferredNativeTimer", Private).SetValue(state, (float?)0.2f);
            ((HighRateInputSampler)sampler).Reset();
            long pendingPress = Stopwatch.GetTimestamp();
            observe.Invoke(sampler, new object[] { 32, true, pendingPress });
            ((IBTnode)state).ResetResult();
            Require(!(bool)stateType.GetField("nativeCharging", Private).GetValue(state)
                && !(bool)stateType.GetField("sampledPress", Private).GetValue(state)
                && !(bool)stateType.GetField("sampledRelease", Private).GetValue(state)
                && stateType.GetField("deferredNativeTimer", Private).GetValue(state) == null,
                "external reset retained a cancelled charge or deferred timer");
            JumpInputTransition pendingTransition;
            Require(((HighRateInputSampler)sampler).TryDequeue(out pendingTransition)
                && pendingTransition.IsDown && pendingTransition.Timestamp == pendingPress,
                "external reset discarded a new queued input edge");
            foreach (int desktopButton in new[] { 32, MouseButtons.Left, MouseButtons.X2 })
            foreach (bool earlyPress in new[] { true, false })
            {
                ((HighRateInputSampler)sampler).Configure(new[] { desktopButton }, true);
                clear.Invoke(state, null);
                unsupported = false;
                frameJump = false;
                long press = Stopwatch.GetTimestamp();
                observe.Invoke(sampler, new object[] { desktopButton, true, press });
                if (earlyPress)
                {
                    nativeResult = BTresult.Failure;
                    RunFrame(state, run, tick);
                    Require((bool)stateType.GetField("sampledPress", Private).GetValue(state),
                        "sampler press must survive idle native Failure before the game sees it");
                }
                frameJump = true;
                nativeResult = BTresult.Running;
                RunFrame(state, run, tick);
                Require((long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state) == press,
                    "native charge must use the original sampler timestamp");
                // Release and its delivery may also precede the native frame.
                long release = press + (long)(0.028 * Stopwatch.Frequency);
                observe.Invoke(sampler, new object[] { desktopButton, false, release });
                RunFrame(state, run, tick);
                Require((bool)stateType.GetField("sampledRelease", Private).GetValue(state),
                    "early release must remain pending while native input is down");
                frameJump = false;
                nativeResult = BTresult.Success;
                RunFrame(state, run, tick);
                Require((string)text.GetValue(null, null) == "SFC: 28 ms",
                    "both press delivery orders must produce the measured 28 ms release");
                float timer = (float)typeof(JumpState).GetField("m_timer", Private).GetValue(state);
                Require(Math.Abs(timer - 2f / 60f) < 0.000001f,
                    "real state must preload timer step 3, not merely display milliseconds");
                Require(!(bool)stateType.GetField("sampledPress", Private).GetValue(state),
                    "completed jump must clear its sample");
            }
            ((HighRateInputSampler)sampler).Configure(new[] { 32 }, true);
            // A real unsupported contributor still invalidates the charge.
            unsupported = true;
            frameJump = true;
            nativeResult = BTresult.Running;
            observe.Invoke(sampler, new object[] { 32, true, Stopwatch.GetTimestamp() });
            RunFrame(state, run, tick);
            unsupported = false;
            frameJump = false;
            nativeResult = BTresult.Success;
            observe.Invoke(sampler, new object[] { 32, false, Stopwatch.GetTimestamp() });
            RunFrame(state, run, tick);
            Require((string)text.GetValue(null, null) == "SFC: Not supported",
                "mixed unsupported input must not be certified by retaining an early press");

            // A still-held native buffer is valid even after 50 ms. Time spent
            // waiting in the air must NOT become extra jump charge.
            long oldPress = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            observe.Invoke(sampler, new object[] { 32, true, oldPress });
            nativeResult = BTresult.Failure;
            RunFrame(state, run, tick);
            frameJump = true;
            nativeResult = BTresult.Running;
            long beforeEntry = Stopwatch.GetTimestamp();
            UnavailableFrame(timeline);
            RunFrame(state, run, tick);
            long bufferedOrigin = (long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state);
            Require(bufferedOrigin >= beforeEntry && bufferedOrigin <= Stopwatch.GetTimestamp(),
                "held buffer must be measured from native charge entry, not rejected or charged from airborne press");
            nativeResult = BTresult.Failure;
            RunFrame(state, run, tick);
            Require(!(bool)stateType.GetField("sampledPress", Private).GetValue(state),
                "an interrupted active charge must still clear its input");
            observe.Invoke(sampler, new object[] { 32, false, Stopwatch.GetTimestamp() });
            frameJump = false;
            RunFrame(state, run, tick);

            // ResumeRun must preserve a NEW queued press, while clearing the
            // abandoned charge's bookkeeping and native timer.
            FieldInfo inputBuffer = typeof(JumpState).GetField("m_left_right_input_buffer", Private);
            inputBuffer.SetValue(state, inputBuffer.GetValue(new JumpState(null)));
            long bufferedPress = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            observe.Invoke(sampler, new object[] { 32, true, bufferedPress });
            stateType.GetMethod("ResumeRun", Private).Invoke(state, null);
            frameJump = true;
            nativeResult = BTresult.Running;
            beforeEntry = Stopwatch.GetTimestamp();
            UnavailableFrame(timeline);
            RunFrame(state, run, tick);
            bufferedOrigin = (long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state);
            Require(bufferedOrigin >= beforeEntry, "resumed buffered charge must retain its physical press evidence");
            observe.Invoke(sampler, new object[] { 32, false, bufferedOrigin + (long)(0.028 * Stopwatch.Frequency) });
            frameJump = false;
            nativeResult = BTresult.Success;
            RunFrame(state, run, tick);
            Require((string)text.GetValue(null, null) == "SFC: Buffered",
                "buffered release must identify native tick-aligned charging");
            Require((float)typeof(JumpState).GetField("m_timer", Private).GetValue(state) == 0,
                "buffered release must not preload the stubbed native timer");

            // Even a <50 ms buffered press must start at landing, not in air.
            long shortBufferedPress = Stopwatch.GetTimestamp() - Stopwatch.Frequency / 100;
            UnavailableFrame(timeline);
            observe.Invoke(sampler, new object[] { 32, true, shortBufferedPress });
            frameJump = true;
            nativeResult = BTresult.Running;
            beforeEntry = Stopwatch.GetTimestamp();
            RunFrame(state, run, tick);
            Require((long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state) >= beforeEntry,
                "short buffer must exclude the pre-landing portion too");
            observe.Invoke(sampler, new object[] { 32, false, Stopwatch.GetTimestamp() });
            frameJump = false;
            nativeResult = BTresult.Success;
            RunFrame(state, run, tick);
            timelineType.GetMethod("ResetEligibility", Private).Invoke(timeline, null);

            // Both physical edges may arrive before the native first down frame.
            long fastPress = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 28 / 1000;
            observe.Invoke(sampler, new object[] { 32, true, fastPress });
            observe.Invoke(sampler, new object[] { 32, false, fastPress + Stopwatch.Frequency * 27 / 1000 });
            frameJump = true;
            nativeResult = BTresult.Running;
            RunFrame(state, run, tick);
            Require((long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state) == fastPress,
                "recent released sample must remain usable while native frame still sees down");
            frameJump = false;
            nativeResult = BTresult.Success;
            RunFrame(state, run, tick);
            Require((string)text.GetValue(null, null) == "SFC: 27 ms", "fast release remains measured");

            // Native automatic max still reports a genuine measured origin.
            nativeResult = BTresult.Failure;
            observe.Invoke(sampler, new object[] { 32, true, Stopwatch.GetTimestamp() });
            RunFrame(state, run, tick); // native idle Failure, sampler already down
            frameJump = true;
            nativeResult = BTresult.Running;
            RunFrame(state, run, tick);
            nativeResult = BTresult.Success;
            RunFrame(state, run, tick);
            Require(((string)text.GetValue(null, null)).EndsWith(" ms (max)"),
                "early press must also survive until automatic native maximum");
            Console.WriteLine("[OK] Compiled charge lifecycle: early input, buffered/resumed charge, correction, unsupported input, cancel, max");

            // DI timestamps must drive the SAME real charge correction path.
            stateType.GetField("sampledPress", Private).SetValue(state, true);
            stateType.GetField("sampledRelease", Private).SetValue(state, true);
            stateType.GetField("nativeChargeTimestamp", Private).SetValue(state, Stopwatch.GetTimestamp());
            stateType.GetField("observedRestoreEpoch", Private).SetValue(state, -1L);
            stateType.GetMethod("CheckStateRestore", Private).Invoke(state, null);
            Require(!(bool)stateType.GetField("sampledPress", Private).GetValue(state)
                && !(bool)stateType.GetField("sampledRelease", Private).GetValue(state)
                && (long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state) == 0,
                "Rewind/replay state restoration discards old physical hold associations");
            clear.Invoke(state, null);
            Type directBindingType = typeof(DirectInputJumpBinding);
            Guid directId = new Guid("184ff590-b295-11f0-8002-444553540000");
            Array directBindings = Array.CreateInstance(directBindingType, 1);
            directBindings.SetValue(Activator.CreateInstance(directBindingType, Private, null,
                new object[] { directId, new[] { new[] { 2 } } }, null), 0);
            Array xboxBindings = Array.CreateInstance(typeof(XInputJumpBinding), 0);
            MethodInfo configureDirect = samplerType.GetMethod("Configure", Private, null,
                new[] { typeof(int[][]), xboxBindings.GetType(), directBindings.GetType(), typeof(IntPtr), typeof(bool) }, null);
            configureDirect.Invoke(sampler, new object[] { new int[0][], xboxBindings, directBindings, IntPtr.Zero, true });
            long generation = (long)samplerType.GetProperty("ConfigurationGeneration", Private).GetValue(sampler, null);
            MethodInfo directSample = samplerType.GetMethod("ObserveDirectInput", Private);
            long directPress = Stopwatch.GetTimestamp();
            directSample.Invoke(sampler, new object[] { generation, directId, false, true, directPress - 1 });
            directSample.Invoke(sampler, new object[] { generation, directId, true, true, directPress });
            nativeResult = BTresult.Running;
            frameJump = true;
            RunFrame(state, run, tick);
            directSample.Invoke(sampler, new object[] { generation, directId, false, true, directPress + Stopwatch.Frequency * 28 / 1000 });
            frameJump = false;
            nativeResult = BTresult.Success;
            RunFrame(state, run, tick);
            Require((string)text.GetValue(null, null) == "SFC: 28 ms", "DI must report actual hold");
            Require(Math.Abs((float)typeof(JumpState).GetField("m_timer", Private).GetValue(state) - 2f / 60f) < 0.000001f,
                "DI must preload native timer, not merely replace Not supported text");
            directSample.Invoke(sampler, new object[] { generation, directId, true, true, Stopwatch.GetTimestamp() });
            nativeResult = BTresult.Running;
            frameJump = true;
            RunFrame(state, run, tick);
            directSample.Invoke(sampler, new object[] { generation, directId, false, false, Stopwatch.GetTimestamp() });
            frameJump = false;
            nativeResult = BTresult.Success;
            RunFrame(state, run, tick);
            Require((string)text.GetValue(null, null) == "SFC: Not supported", "DI fault must invalidate active correction");
            samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                .Invoke(sampler, new object[] { new[] { 32 }, true });
            Console.WriteLine("[OK] Compiled DirectInput charge: corrected timer/Jump%, fault invalidation");

            // Now execute the INSTALLED native JumpState.MyRun, not NativeRun's
            // stub. Suppress only audiovisual Start/DoJump side effects; record
            // the actual intensity passed by the game's release routine.
            Patch(harmony, harmonyType, harmonyMethod, typeof(JumpState).GetMethod("Start", Private), "Skip");
            Patch(harmony, harmonyType, harmonyMethod, typeof(JumpState).GetMethod("DoJump", Private), "CaptureJump");
            WaterBlockBehaviour water = new WaterBlockBehaviour();
            typeof(BodyComp).GetField("m_blockBehaviourLookup", Private).SetValue(body,
                new Dictionary<Type, IBlockBehaviour> { { typeof(WaterBlock), water } });
            MethodInfo nativeRunMethod = typeof(JumpState).GetMethod("MyRun", Private);
            FieldInfo timerField = typeof(JumpState).GetField("m_timer", Private);
            runNative = true;
            // Finish the sampler's last held press before starting the sweep.
            observe.Invoke(sampler, new object[] { 32, false, Stopwatch.GetTimestamp() });
            samplerType.GetMethod("Reset", Private).Invoke(sampler, null);
            foreach (bool underwater in new[] { false, true })
            {
                water.IsPlayerOnBlock = underwater;
                multiplier = underwater ? 0.5f : 1f;
                for (int frames = 1; frames <= (underwater ? 71 : 35); frames++)
                {
                    JumpState native = new JumpState(null);
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                    frameJump = true;
                    for (int frame = 0; frame < frames; frame++) nativeRunMethod.Invoke(native, tick);
                    frameJump = false;
                    nativeRunMethod.Invoke(native, tick);
                    float expected = launchIntensity;

                    clear.Invoke(state, null);
                    timerField.SetValue(state, 0f);
                    FieldInfo buffer = typeof(JumpState).GetField("m_left_right_input_buffer", Private);
                    buffer.SetValue(state, buffer.GetValue(new JumpState(null)));
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                    long press = Stopwatch.GetTimestamp();
                    observe.Invoke(sampler, new object[] { 32, true, press });
                    frameJump = true;
                    for (int frame = 0; frame < frames; frame++) RunFrame(state, run, tick);
                    observe.Invoke(sampler, new object[] { 32, false, press + (long)(frames * 0.017 * Stopwatch.Frequency) });
                    frameJump = false;
                    launchIntensity = -1;
                    RunFrame(state, run, tick);
                    Require(Math.Abs(launchIntensity - expected) < 0.000002f,
                        "native power mismatch water=" + underwater + " frames=" + frames
                        + " expected=" + expected + " actual=" + launchIntensity);
                }
            }
            Console.WriteLine("[OK] Installed native JumpState release powers: all 35 dry and 71 underwater frame counts");

            // Observation mode runs the real native routine and the real Jump%
            // calculator postfix through IBTnode.Run, including last_result.
            Type calculator = null;
            foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                if (loaded.GetName().Name == "JumpKingLastJumpValue")
                    calculator = loaded.GetType("JumpKingLastJumpValue.Models.JumpChargeCalc", true);
            Activator.CreateInstance(calculator, new[] { harmony });
            PropertyInfo actualFrames = calculator.GetProperty("JumpFrames");
            PropertyInfo actualPercentage = calculator.GetProperty("JumpPercentage");
            stateType.GetField("observationOnly", Private).SetValue(state, true);
            int tickNumber = 100;
            foreach (bool underwater in new[] { false, true })
            {
                water.IsPlayerOnBlock = underwater;
                multiplier = underwater ? 0.5f : 1f;
                int maximum = underwater ? 71 : 35;
                for (int count = 1; count <= maximum; count++)
                {
                    JumpState native = new JumpState(null);
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                    frameJump = true;
                    for (int frame = 0; frame < count; frame++) native.Run(new TickData(1f / 60f, tickNumber++));
                    frameJump = false;
                    native.Run(new TickData(1f / 60f, tickNumber++));
                    float expected = launchIntensity;

                    clear.Invoke(state, null);
                    ((IBTnode)state).ResetResult();
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                    long press = Stopwatch.GetTimestamp();
                    observe.Invoke(sampler, new object[] { 32, true, press });
                    frameJump = true;
                    for (int frame = 0; frame < count; frame++) RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) });
                    // Deliberately feed a physical hold one step longer than
                    // native's frame count: observer must NOT fix the power.
                    double seconds = (count + 1) * 0.017;
                    observe.Invoke(sampler, new object[] { 32, false, press + (long)(seconds * Stopwatch.Frequency) });
                    frameJump = false;
                    launchIntensity = -1;
                    RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) });
                    Require(Math.Abs(launchIntensity - expected) < 0.000002f,
                        "disabled correction changed native strength water=" + underwater + " count=" + count);
                    Require((int)actualFrames.GetValue(null, null) == count,
                        "observer overwrote actual Jump% frames");
                    Require(Math.Abs((float)actualPercentage.GetValue(null, null) - expected) < 0.000002f,
                        "observer overwrote actual Jump% percentage");
                    string displayed = (string)text.GetValue(null, null);
                    int predicted = Math.Min(maximum, count + 1);
                    Require(displayed.StartsWith("SFC: ") && displayed.Contains(" ms") && !displayed.Contains("Not supported"),
                        "disabled mode lost physical millisecond measurement");
                    Require(predicted != count ? displayed.EndsWith("(would " + predicted + "f)") : !displayed.Contains("would"),
                        "counterfactual must appear exactly when native and SFC frames differ");
                }
            }
            // A tap invisible to vanilla MUST stay invisible with correction off.
            clear.Invoke(state, null);
            ((IBTnode)state).ResetResult();
            typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, false);
            long shortTap = Stopwatch.GetTimestamp() - Stopwatch.Frequency / 1000;
            observe.Invoke(sampler, new object[] { 32, true, shortTap });
            observe.Invoke(sampler, new object[] { 32, false, shortTap + Stopwatch.Frequency / 2000 });
            frameJump = false;
            launchIntensity = -999;
            Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }) == BTresult.Failure && launchIntensity == -999,
                "disabled correction must never replay a subframe tap");
            Console.WriteLine("[OK] Observation-only: all native dry/water powers and actual Jump% preserved, measured ms/diff-only prediction, no tap replay");

            // Replay eligibility ordering through the compiled replacement AND
            // installed native JumpState/Jump% (not only the pure quantizer).
            foreach (bool observation in new[] { false, true })
            foreach (bool buffered in new[] { false, true })
            foreach (bool underwater in new[] { false, true })
            {
                clear.Invoke(state, null);
                ((IBTnode)state).ResetResult();
                samplerType.GetMethod("Reset", Private).Invoke(sampler, null);
                stateType.GetField("observationOnly", Private).SetValue(state, observation);
                timeline = Activator.CreateInstance(timelineType, true);
                stateType.GetField("Timeline", Private).SetValue(state, timeline);
                water.IsPlayerOnBlock = underwater;
                multiplier = underwater ? .5f : 1f;
                // Deliberately old delivery: the removed 50ms heuristic would
                // misclassify the non-buffered case regardless of the curve.
                long epoch = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 2;
                long dt = Stopwatch.Frequency * 17 / 1000;
                timelineType.GetMethod("BeginFrame", Private).Invoke(timeline, new object[] { epoch });
                timelineType.GetMethod("EndFrame", Private).Invoke(timeline, null); // air / recovery
                long landing = epoch + dt;
                if (!buffered)
                {
                    frameJump = false;
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, false);
                    RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, landing);
                }
                long physicalPress = buffered ? landing - dt : landing + Stopwatch.Frequency * 417 / 100000;
                long expectedOrigin = buffered ? landing : physicalPress;
                long firstChargeFrame = buffered ? landing : landing + dt;
                observe.Invoke(sampler, new object[] { 32, true, physicalPress });
                frameJump = true;
                typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                int count = buffered ? 4 : 34;
                for (int frame = 0; frame < count; frame++)
                {
                    RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, firstChargeFrame + dt * frame);
                    Require((long)stateType.GetField("nativeChargeTimestamp", Private).GetValue(state) == expectedOrigin,
                        "compiled origin shifted: buffered=" + buffered + " observer=" + observation);
                }
                double milliseconds = buffered ? 68 : 575.81;
                observe.Invoke(sampler, new object[] { 32, false, expectedOrigin + (long)(milliseconds * Stopwatch.Frequency / 1000) });
                frameJump = false;
                RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, firstChargeFrame + dt * count);
                Require(Math.Abs(launchIntensity - (count + 1) * multiplier / 36f) < .000002f,
                    "frame-ordered native launch changed strength");
                Require((int)actualFrames.GetValue(null, null) == count, "frame-ordered Jump% count changed");
                Require((string)text.GetValue(null, null) == (buffered ? "SFC: Buffered" : "SFC: 575.81 ms"),
                    "post-landing 34f must not become would 32f; buffers must exclude ONLY unavailable time");
            }
            Console.WriteLine("[OK] Compiled ordered-frame regression: post-landing 34f, real buffer, >50ms delivery, native dry/water power and observer parity");
            // Do not resurrect an airborne press, including up between the
            // eligible update's timestamp and its native cached-input check.
            foreach (int releaseOffsetMs in new[] { -1, 1 })
            {
                clear.Invoke(state, null);
                ((IBTnode)state).ResetResult();
                samplerType.GetMethod("Reset", Private).Invoke(sampler, null);
                stateType.GetField("observationOnly", Private).SetValue(state, false);
                timeline = Activator.CreateInstance(timelineType, true);
                stateType.GetField("Timeline", Private).SetValue(state, timeline);
                long landedAt = Stopwatch.GetTimestamp() - Stopwatch.Frequency / 200;
                timelineType.GetMethod("BeginFrame", Private).Invoke(timeline, new object[] { landedAt - Stopwatch.Frequency / 50 });
                timelineType.GetMethod("EndFrame", Private).Invoke(timeline, null);
                observe.Invoke(sampler, new object[] { 32, true, landedAt - Stopwatch.Frequency / 100 });
                observe.Invoke(sampler, new object[] { 32, false, landedAt + releaseOffsetMs * Stopwatch.Frequency / 1000 });
                frameJump = false;
                typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, false);
                launchIntensity = -999;
                Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, landedAt) == BTresult.Failure
                    && launchIntensity == -999, "completed airborne tap was replayed after landing");
            }
            Console.WriteLine("[OK] Completed airborne tap is not replayed into a later eligibility interval");
            // A high-rate release arriving after the native input snapshot must
            // beat the native automatic maximum, on dry ground AND underwater.
            foreach (float scale in new[] { 1f, .5f })
            {
                clear.Invoke(state, null);
                ((IBTnode)state).ResetResult();
                samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                    .Invoke(sampler, new object[] { new[] { 32 }, true });
                timeline = Activator.CreateInstance(timelineType, true);
                stateType.GetField("Timeline", Private).SetValue(state, timeline);
                multiplier = scale;
                water.IsPlayerOnBlock = scale == .5f;
                stateType.GetField("observationOnly", Private).SetValue(state, false);
                long origin = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3;
                long step = Stopwatch.Frequency * 17 / 1000;
                int count = scale == 1f ? 35 : 71;
                double holdMs = (count - 1) * 17 - 2.19;
                observe.Invoke(sampler, new object[] { 32, true, origin });
                typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                frameJump = true;
                for (int i = 0; i < count; i++)
                    RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, origin + i * step);
                observe.Invoke(sampler, new object[] { 32, false, origin + (long)(holdMs * Stopwatch.Frequency / 1000) });
                launchIntensity = -999;
                BTresult pending = (BTresult)RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, origin + count * step);
                Require(pending == BTresult.Running && launchIntensity == -999, "stale native down overrode sampled release with maximum");
                frameJump = false;
                RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, origin + (count + 1) * step);
                Require(Math.Abs(launchIntensity - count * scale / 36f) < .000002f, "pending release power changed at native maximum: scale=" + scale + " intensity=" + launchIntensity + " text=" + text.GetValue(null, null));
                Require((int)actualFrames.GetValue(null, null) == count - 1, "max race Jump% mismatch");
            }
            Console.WriteLine("[OK] Sampled release beats stale native maximum, dry and underwater");

            clear.Invoke(state, null);
            ((IBTnode)state).ResetResult();
            samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                .Invoke(sampler, new object[] { new[] { 32 }, true });
            timeline = Activator.CreateInstance(timelineType, true);
            stateType.GetField("Timeline", Private).SetValue(state, timeline);
            multiplier = 1;
            water.IsPlayerOnBlock = false;
            long pauseOrigin = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 2;
            long millisecond = Stopwatch.Frequency / 1000;
            observe.Invoke(sampler, new object[] { 32, true, pauseOrigin });
            typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
            frameJump = true;
            for (int i = 0; i < 4; i++)
                RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, pauseOrigin + i * 17 * millisecond);
            MethodInfo pause = timelineType.GetMethod("ObservePause", Private);
            pause.Invoke(timeline, new object[] { true, pauseOrigin + 60 * millisecond });
            pause.Invoke(timeline, new object[] { false, pauseOrigin + 1060 * millisecond });
            observe.Invoke(sampler, new object[] { 32, false, pauseOrigin + 1068 * millisecond });
            frameJump = false;
            RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, pauseOrigin + 1085 * millisecond);
            Require(Math.Abs(launchIntensity - 5f / 36f) < .000002f, "pause became extra charge power");
            Require((string)text.GetValue(null, null) == "SFC: 68 ms", "pause was included in measured milliseconds");
            Console.WriteLine("[OK] Explicit 1000 ms pause excluded: native 4f / SFC 68 ms");
            clear.Invoke(state, null);
            ((IBTnode)state).ResetResult();
            samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                .Invoke(sampler, new object[] { new[] { 32 }, true });
            timeline = Activator.CreateInstance(timelineType, true);
            stateType.GetField("Timeline", Private).SetValue(state, timeline);
            long recoveryOrigin = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            observe.Invoke(sampler, new object[] { 32, true, recoveryOrigin });
            typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
            frameJump = true;
            for (int i = 0; i < 10; i++)
                RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, recoveryOrigin + i * 17 * millisecond);
            observe.Invoke(sampler, new object[] { 32, false, recoveryOrigin + 68 * millisecond });
            RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, recoveryOrigin + 170 * millisecond);
            unsupported = true; frameJump = false;
            RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, recoveryOrigin + 187 * millisecond);
            Require(Math.Abs(launchIntensity - 12f / 36f) < .000002f, "evidence loss left a partially corrected timer behind");
            Require((string)text.GetValue(null, null) == "SFC: Not supported", "evidence loss retained a precise label");
            unsupported = false;
            Console.WriteLine("[OK] Evidence loss while release waits restores independent native timer");

            // Buffers keep exact native release/max timing. Only quarter mode
            // changes the power of a completed, below-max native release.
            // Include sampled up ahead of a still-held native max frame.
            foreach (bool quarterMode in new[] { false, true })
            foreach (bool observation in new[] { false, true })
            foreach (bool underwater in new[] { false, true })
            foreach (int holdMs in new[] { 10, 20, 25, 26, 28, 60, 75, 80, -1, -2 })
            {
                clear.Invoke(state, null);
                ((IBTnode)state).ResetResult();
                samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                    .Invoke(sampler, new object[] { new[] { 32 }, true });
                stateType.GetField("observationOnly", Private).SetValue(state, observation);
                stateType.GetField("quarterSteps", Private).SetValue(state, quarterMode);
                timeline = Activator.CreateInstance(timelineType, true);
                stateType.GetField("Timeline", Private).SetValue(state, timeline);
                water.IsPlayerOnBlock = underwater;
                multiplier = underwater ? .5f : 1f;
                long origin = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3;
                long step = Stopwatch.Frequency * 17 / 1000;
                timelineType.GetMethod("BeginFrame", Private).Invoke(timeline, new object[] { origin - step });
                timelineType.GetMethod("EndFrame", Private).Invoke(timeline, null);
                observe.Invoke(sampler, new object[] { 32, true, origin - step });
                int count = holdMs < 0 ? 0 : (int)Math.Ceiling(holdMs / 17.0);
                if (holdMs < 0)
                {
                    // Use the installed game's automatic threshold, including
                    // native float accumulation (water can need an extra tick).
                    JumpState maximumProbe = new JumpState(null);
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                    frameJump = true;
                    count = 0;
                    while (maximumProbe.Run(new TickData(1f / 60f, tickNumber++)) == BTresult.Running)
                        Require(++count < 100, "native maximum probe did not finish");
                }
                typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                frameJump = true;
                for (int i = 0; i < count; i++)
                {
                    Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, origin + i * step)
                        == BTresult.Running, "buffer launched before native release/maximum");
                    Require((string)text.GetValue(null, null) == "SFC: Buffered", "buffer must be identified while charging");
                    Require(stateType.GetField("deferredNativeTimer", Private).GetValue(state) == null,
                        "buffer must never enter the corrected-release timer path");
                }
                if (holdMs != -1)
                    observe.Invoke(sampler, new object[] { 32, false,
                        origin + (holdMs == -2 ? step : Stopwatch.Frequency * holdMs / 1000) });
                frameJump = holdMs < 0;
                launchIntensity = -999;
                Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f / 60f, tickNumber++) }, origin + count * step)
                    == BTresult.Success, "buffer did not launch at native release/maximum: ms=" + holdMs
                        + " water=" + underwater + " observer=" + observation + " timer=" + timerField.GetValue(state));
                float bufferedIntensity = launchIntensity;
                int bufferedFrames = (int)actualFrames.GetValue(null, null);
                float bufferedPercentage = (float)actualPercentage.GetValue(null, null);
                bool correctedBuffer = quarterMode && !observation && holdMs >= 0;
                Require(correctedBuffer ? ((string)text.GetValue(null, null)).EndsWith(" (buffered)")
                    : (string)text.GetValue(null, null) == "SFC: Buffered", "buffer measurement must identify corrected versus native power");

                JumpState vanilla = new JumpState(null);
                typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                frameJump = true;
                for (int i = 0; i < count; i++) vanilla.Run(new TickData(1f / 60f, tickNumber++));
                frameJump = holdMs < 0;
                vanilla.Run(new TickData(1f / 60f, tickNumber++));
                if (correctedBuffer)
                {
                    float expectedFrames = (float)Math.Max(1, Math.Floor(holdMs / 4.25 + .5) / 4);
                    float expectedPower = (expectedFrames + 1) * multiplier / 36;
                    Require(Math.Abs(bufferedIntensity - expectedPower) < .000002f
                        && Math.Abs(bufferedPercentage - expectedPower) < .000002f
                        && bufferedFrames == (int)Math.Floor(expectedFrames + .5),
                        "buffer did not use fractional time since native charge start");
                }
                else Require(bufferedIntensity == launchIntensity
                        && bufferedFrames == (int)actualFrames.GetValue(null, null)
                        && bufferedPercentage == (float)actualPercentage.GetValue(null, null),
                        "native buffer changed: ms=" + holdMs + " water=" + underwater + " observer=" + observation);
            }
            Console.WriteLine("[OK] Buffered phase sweep: quarter releases, unchanged native release/max ticks, dry/water and observation parity");
            stateType.GetField("observationOnly", Private).SetValue(state, false);
            stateType.GetField("quarterSteps", Private).SetValue(state, true);
            JKRuntime.Gameplay.JumpResult reported = null;
            int fractionalChecks = 0;
            using (JKRuntime.Gameplay.JumpEvents.Subscribe(value => reported = value))
            foreach (bool underwater in new[] { false, true })
            {
                water.IsPlayerOnBlock = underwater;
                multiplier = underwater ? .5f : 1f;
                int maximum = underwater ? 284 : 140;
                for (int q = 4; q <= maximum; q++)
                {
                    float frames = q / 4f;
                    clear.Invoke(state, null); ((IBTnode)state).ResetResult();
                    timeline = Activator.CreateInstance(timelineType, true);
                    stateType.GetField("Timeline", Private).SetValue(state, timeline);
                    samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                        .Invoke(sampler, new object[] { new[] { 32 }, true });
                    typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                    long origin = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3;
                    observe.Invoke(sampler, new object[] { 32, true, origin });
                    frameJump = true;
                    Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f/60, tickNumber++) }, origin) == BTresult.Running,
                        "fractional charge did not start natively");
                    long release = origin + (long)(frames * .017 * Stopwatch.Frequency);
                    observe.Invoke(sampler, new object[] { 32, false, release }); frameJump = false;
                    reported = null; launchIntensity = -999;
                    Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f/60, tickNumber++) }, release) == BTresult.Success,
                        "fractional charge did not release natively");
                    float expected = (frames + 1) * multiplier / 36;
                    Require(Math.Abs(launchIntensity - expected) < .000002f,
                        "quarter native release power, water=" + underwater + " frames=" + frames);
                    Require(reported != null && reported.CorrectedFrameCount == frames
                        && (q % 4 == 0 ? reported.CorrectedFrames == (int)frames : reported.CorrectedFrames == null),
                        "quarter event lost exact input frames");
                    Require(Math.Abs((float)actualPercentage.GetValue(null, null) - expected) < .000002f,
                        "actual Jump% lost surface-scaled fractional power");
                    string label = (string)integration.GetMethod("FrameLabel", Private).Invoke(null,
                        new object[] { actualFrames.GetValue(null, null), actualPercentage.GetValue(null, null) });
                    Require(label == frames.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " frames",
                        "actual Jump% frame text rounded fractional value");
                    fractionalChecks++;
                }
                // Both edges between physics ticks must still enter native Start/release once.
                clear.Invoke(state, null); ((IBTnode)state).ResetResult();
                timeline = Activator.CreateInstance(timelineType, true);
                stateType.GetField("Timeline", Private).SetValue(state, timeline);
                samplerType.GetMethod("Configure", Private, null, new[] { typeof(int[]), typeof(bool) }, null)
                    .Invoke(sampler, new object[] { new[] { 32 }, true });
                typeof(InputComponent).GetField("_can_jump", Private).SetValue(input, true);
                long tap = Stopwatch.GetTimestamp() - (long)(.03 * Stopwatch.Frequency);
                observe.Invoke(sampler, new object[] { 32, true, tap });
                observe.Invoke(sampler, new object[] { 32, false, tap + (long)(.02125 * Stopwatch.Frequency) });
                frameJump = false; reported = null;
                Require((BTresult)RunFrame(state, null, new object[] { new TickData(1f/60, tickNumber++) }) == BTresult.Success
                    && reported != null && reported.CorrectedFrameCount == 1.25f
                    && Math.Abs(launchIntensity - 2.25f * multiplier / 36) < .000002f,
                    "fractional completed tap lost or rounded, water=" + underwater);
            }
            Console.WriteLine("[OK] Quarter-step native release: " + fractionalChecks + " dry/water strengths, exact Runtime/Jump% values and completed taps");
            // Exercise the installed DoJump arithmetic too, including its snow
            // thresholds and horizontal clamp. Only audiovisual effects are skipped.
            Patch(harmony, harmonyType, harmonyMethod, typeof(PlayerEntity).GetMethod("SetDirection", Private), "Skip");
            Patch(harmony, harmonyType, harmonyMethod, typeof(JumpState).GetMethod("HandleSounds", Private), "Skip");
            Patch(harmony, harmonyType, harmonyMethod, typeof(JumpState).GetMethod("HandleParticles", Private), "Skip");
            impulsePlayer = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            impulsePlayer.m_body = body;
            typeof(EntityComponent.Entity).GetField("m_components", Private).SetValue(impulsePlayer,
                new List<EntityComponent.Component> { body, input });
            var nativeImpulse = new JumpState(impulsePlayer);
            var nativeJump = typeof(JumpState).GetMethod("DoJump", Private);
            var oldCallback = PlayerEntity.OnJumpCall;
            PlayerEntity.OnJumpCall = null;
            runImpulse = true;
            try
            {
                foreach (bool snow in new[] { false, true })
                foreach (float scale in new[] { 1f, .5f })
                foreach (int sign in new[] { -1, 0, 1 })
                {
                    typeof(BodyComp).GetField("m_blockBehaviourLookup", Private).SetValue(body,
                        new Dictionary<Type, IBlockBehaviour> { { typeof(SnowBlock), new WaterBlockBehaviour { IsPlayerOnBlock = snow } } });
                    direction = sign;
                    for (int q = 4; q <= (scale == 1 ? 140 : 284); q++)
                    {
                        float intensity = (q / 4f + 1) * scale / 36;
                        body.Velocity = new Microsoft.Xna.Framework.Vector2(1, 0);
                        nativeJump.Invoke(nativeImpulse, new object[] { intensity });
                        bool blocked = snow && intensity <= .2f;
                        float effective = snow && intensity > .2f && intensity < .3f ? .3f : intensity;
                        Require(Math.Abs(body.Velocity.Y - (blocked ? 0 : JumpKing.PlayerValues.JUMP * effective)) < .00001f,
                            "native fractional impulse or snow threshold changed");
                        float expectedX = blocked ? 1 : Math.Max(-JumpKing.PlayerValues.SPEED, Math.Min(JumpKing.PlayerValues.SPEED, 1 + sign * JumpKing.PlayerValues.SPEED));
                        Require(body.Velocity.X == expectedX, "native horizontal impulse/clamp changed");
                    }
                }
            }
            finally { PlayerEntity.OnJumpCall = oldCallback; runImpulse = false; direction = 0; }
            Console.WriteLine("[OK] Quarter-step installed DoJump impulses: dry/water scaling, snow thresholds and native direction/clamp");
        }
        finally
        {
            ((IDisposable)sampler).Dispose();
        }
    }

    private static void Patch(object harmony, Type type, Type methodType, MethodInfo original, string prefixName)
    {
        MethodInfo patch = type.GetMethod("Patch");
        object[] args = new object[patch.GetParameters().Length];
        args[0] = original;
        args[1] = Activator.CreateInstance(methodType,
            new object[] { typeof(ChargeLifecycleContractTests).GetMethod(prefixName, Private) });
        patch.Invoke(harmony, args);
    }
    private static object RunFrame(object state, MethodInfo method, object[] args, long stamp = 0)
    {
        object timeline = state.GetType().GetField("Timeline", Private).GetValue(state);
        Type type = timeline.GetType();
        type.GetMethod("BeginFrame", Private).Invoke(timeline, new object[] { stamp == 0 ? Stopwatch.GetTimestamp() : stamp });
        try { return method != null ? method.Invoke(state, args) : (object)((IBTnode)state).Run((TickData)args[0]); }
        finally { type.GetMethod("EndFrame", Private).Invoke(timeline, null); }
    }
    private static void UnavailableFrame(object timeline)
    {
        timeline.GetType().GetMethod("BeginFrame", Private).Invoke(timeline, new object[] { Stopwatch.GetTimestamp() });
        timeline.GetType().GetMethod("EndFrame", Private).Invoke(timeline, null);
    }
    private static bool ReturnTrue(ref bool __result) { __result = true; return false; }
    private static bool Unsupported(ref bool __result) { __result = unsupported; return false; }
    private static bool Multiplier(ref float __result) { __result = multiplier; return false; }
    private static bool Body(ref BodyComp __result) { __result = body; return false; }
    private static bool Input(ref InputComponent __result) { __result = input; return false; }
    private static bool InputState(ref InputComponent.State __result)
    { __result = new InputComponent.State { jump = frameJump, left = direction < 0, right = direction > 0 }; return false; }
    private static bool NativeRun(ref BTresult __result) { __result = nativeResult; return runNative; }
    private static bool Skip() { return false; }
    private static bool CaptureJump(float __0) { launchIntensity = __0; return runImpulse; }
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}
