using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;

namespace SubframeCharge
{
    internal sealed partial class SubframeChargeState : JumpState, IDisposable
    {
        private const double CompletedTapDeliveryDeadlineSeconds = 0.05;

        private static readonly FieldInfo TimerField =
            typeof(JumpState).GetField(
                "m_timer",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo StartMethod =
            typeof(JumpState).GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IHighRateInput sampler;
        private readonly JumpTrajectoryProbe trajectoryProbe;
        private bool lastGameActive;
        private bool frameJumpInitialized;
        private bool lastFrameJump;
        private bool nativeCharging;
        private bool bufferedCharge;
        private bool unsupportedCharge;
        private bool sampledPress;
        private bool sampledRelease;
        private long sampledPressTimestamp;
        private long sampledReleaseTimestamp;
        private long nativeChargeTimestamp;
        internal readonly ChargeTimeline Timeline = new ChargeTimeline();
        private long chargeEntryFrame;
        private int chargeUpdates;
        private long previousChargeUpdate;
        private double minimumUpdateMs;
        private double maximumUpdateMs;
        private readonly InputDiagnostics inputDiagnostics;
        private string invalidationReason;
        private static long nextChargeId;
        private long chargeId;
        private readonly bool observationOnly;
        private readonly bool quarterSteps;
        private float? deferredNativeTimer;

        internal SubframeChargeState(
            PlayerEntity playerEntity,
            JumpTrajectoryProbe jumpTrajectoryProbe,
            bool correctionEnabled = true, bool quarterStepCharge = false)
            : base(playerEntity)
        {
            if (playerEntity == null)
            {
                throw new ArgumentNullException("playerEntity");
            }
            if (TimerField == null || StartMethod == null)
            {
                throw new MissingMemberException(
                    typeof(JumpState).FullName,
                    "m_timer/Start");
            }
            if (jumpTrajectoryProbe == null)
            {
                throw new ArgumentNullException("jumpTrajectoryProbe");
            }
            trajectoryProbe = jumpTrajectoryProbe;
            observationOnly = !correctionEnabled;
            quarterSteps = correctionEnabled && quarterStepCharge;
            sampler = SharedActionSampler.Acquire("subframe-charge", "native.jump", true);
            try
            {
            inputDiagnostics = new InputDiagnostics(IsKeyboardPad, delegate(PadInstance pad)
            {
                int slot;
                return TryGetXboxUserIndex(pad, out slot) ? slot : -1;
            }, DiagnosticLog.Write, sampler.CanMeasureDirectInput);
            inputDiagnostics.Observe();
                sampler.Start();
                if (!sampler.Available)
                {
                    sampler.Dispose();
                    throw new InvalidOperationException(
                        "Subframe Charge requires the Windows input sampler");
                }
                if (!ConfigureSampler())
                {
                    DiagnosticLog.Write("no supported Jump binding; native input remains available");
                }
                TraceEnvironment();
                lastGameActive = IsGameActive();
                if (!lastGameActive)
                {
                    sampler.Reset();
                }
            }
            catch { sampler.Dispose(); throw; }
        }

        internal bool Charging
        {
            get { return nativeCharging; }
        }

        protected override BTresult MyRun(TickData data)
        {
            TraceEvidence("run-enter", "delta=" + data.delta_time);
            CheckStateRestore();
            long now = Stopwatch.GetTimestamp();
            bool gameActive = IsGameActive();
            if (gameActive != lastGameActive)
            {
                DiagnosticLog.Write("input focus active=" + gameActive + " pendingCharge=" + chargeId);
                sampler.Reset();
                ClearSampledCharge();
                Timeline.ResetEligibility();
                lastGameActive = gameActive;
            }
            if (!gameActive)
            {
                return observationOnly ? base.MyRun(data) : BTresult.Failure;
            }

            Timeline.VisitNative();
            // A sampled release can wait one update for native cached input.
            // Keep native's independent timer too: if evidence is lost before
            // takeoff, we must not leave a partially corrected timer behind.
            if (deferredNativeTimer.HasValue)
            {
                TimerField.SetValue(this, deferredNativeTimer.Value);
                deferredNativeTimer = null;
            }

            if (!ConfigureSampler())
            {
                bool wasNativeCharging = nativeCharging;
                BTresult nativeResult = base.MyRun(data);
                TraceEvidence("native-return-unbound", "result=" + nativeResult);
                nativeCharging = nativeResult == BTresult.Running;
                unsupportedCharge = true;
                if (!wasNativeCharging && (nativeCharging || nativeResult == BTresult.Success))
                    chargeId = System.Threading.Interlocked.Increment(ref nextChargeId);
                if ((!wasNativeCharging && nativeCharging) || nativeResult == BTresult.Success)
                    DiagnosticLog.Write("native unmeasured result=" + nativeResult
                        + " charge=" + chargeId + " reason=no-active-sampler-binding" + InputEvidence());
                if (nativeResult == BTresult.Success)
                {
                    TraceIncident("no-active-sampler-binding");
                    JumpPercentIntegration.RecordLaunch(null, null, false);
                }
                else if (nativeCharging)
                {
                    JumpPercentIntegration.RecordCharging(null);
                }
                if (!nativeCharging) ClearSampledCharge();
                return nativeResult;
            }
            DrainSampledTransitions(now);
            if (sampledPress && !Timeline.HasHistory(sampledPressTimestamp)) InvalidateMeasurement("pause-history-expired");
            if (HasUnsupportedJumpDown()) InvalidateMeasurement("unsupported-device-active");
            bool frameJump = input.GetState().jump;
            TraceEvidence("drained", "frameJump=" + frameJump);
            ChargeResult? appliedCharge = null;
            ChargeResult? predictedCharge = null;
            double? measuredSeconds = null;

            float multiplier = GetChargeMultiplier(body);
            float timerWithoutCorrection = (float)TimerField.GetValue(this);
            double completedTapSeconds = 0;
            long sampledOrigin = sampledPress ? Timeline.OriginFor(sampledPressTimestamp) : 0;
            // Only native eligibility identifies a buffer. Pause clipping and
            // delivery latency must not turn an ordinary physical press into one.
            bool bufferedInput = sampledPress && Timeline.EligibleSince > sampledPressTimestamp;
            bool replayCompletedTap =
                !unsupportedCharge && !frameJump && !bufferedInput
                && sampledReleaseTimestamp >= sampledOrigin
                && SubframeTapReplay.TryMeasureCompletedTap(
                    nativeCharging,
                    sampledPress,
                    sampledRelease,
                    sampledOrigin,
                    sampledReleaseTimestamp,
                    now,
                    Stopwatch.Frequency,
                    CompletedTapDeliveryDeadlineSeconds,
                    out completedTapSeconds);
            if (replayCompletedTap)
            {
                completedTapSeconds = Timeline.ActiveSeconds(sampledOrigin, sampledReleaseTimestamp);
                ChargeResult charge = ChargeQuantizer.QuantizeRelease(
                    completedTapSeconds,
                    multiplier, quarterSteps);
                double nextIncrement = data.delta_time * multiplier;

                // A press and release can both occur between game updates.
                // Recreate JumpState's missed Start call, then preload its
                // timer so the native release path launches on this update.
                nativeChargeTimestamp = sampledOrigin;
                predictedCharge = charge;
                if (!observationOnly)
                {
                    StartMethod.Invoke(this, null);
                    TimerField.SetValue(this, ChargeQuantizer.TimerBeforeNativeRelease(charge, nextIncrement));
                    appliedCharge = charge;
                }
                measuredSeconds = completedTapSeconds;
                if (!observationOnly) DiagnosticLog.Write(
                    "completed subframe tap replayed holdMs="
                    + (completedTapSeconds * 1000.0).ToString("F2")
                    + " frames=" + charge.ExactFrames);
            }
            else if (nativeCharging
                && !unsupportedCharge
                && nativeChargeTimestamp != 0
                && sampledRelease
                && sampledReleaseTimestamp >= sampledPressTimestamp)
            {
                double heldSeconds = Timeline.ActiveSeconds(
                    nativeChargeTimestamp,
                    sampledReleaseTimestamp);
                // A buffer must keep its native clock until the native release
                // is actually visible. Never postpone or advance automatic max
                // while a sampled release is ahead of the cached native input.
                bool quarterBufferRelease = bufferedCharge && quarterSteps && !observationOnly
                    && !frameJump && sampledReleaseTimestamp >= nativeChargeTimestamp
                    && timerWithoutCorrection + data.delta_time * multiplier < CHARGE_TIME;
                if (!bufferedCharge || quarterBufferRelease)
                {
                    ChargeResult charge = ChargeQuantizer.QuantizeRelease(heldSeconds, multiplier, quarterSteps);
                    double nextIncrement = data.delta_time * multiplier;
                    predictedCharge = charge;
                    if (!observationOnly)
                    {
                        TimerField.SetValue(this, ChargeQuantizer.TimerBeforeNativeRelease(charge, nextIncrement));
                        appliedCharge = charge;
                    }
                }
                measuredSeconds = heldSeconds;
            }

            float velocityBefore = body.Velocity.Y;
            bool wasCharging = nativeCharging;
            TraceEvidence("native-before", "frameJump=" + frameJump + " correction=" + appliedCharge.HasValue);
            BTresult result = base.MyRun(data);
            TraceEvidence("native-after", "result=" + result);
            nativeCharging = result == BTresult.Running;
            if (nativeCharging && appliedCharge.HasValue && wasCharging)
                deferredNativeTimer = timerWithoutCorrection + data.delta_time * multiplier;
            if (!wasCharging && (nativeCharging || result == BTresult.Success))
                chargeId = System.Threading.Interlocked.Increment(ref nextChargeId);
            if (!wasCharging && nativeCharging)
            {
                // An old STILL HELD press is a valid native input buffer, not
                // a stale tap. Only the game decides when that charge starts.
                bool measuredPress = !unsupportedCharge && sampledPress && frameJump
                    && (!sampledRelease || sampledReleaseTimestamp >= sampledOrigin);
                bufferedCharge = measuredPress && bufferedInput;
                nativeChargeTimestamp = measuredPress
                    ? (bufferedCharge && quarterSteps ? Timeline.FrameTimestamp : sampledOrigin) : 0;
                chargeEntryFrame = Timeline.FrameNumber;
                chargeUpdates = 0;
                previousChargeUpdate = 0;
                minimumUpdateMs = double.PositiveInfinity;
                maximumUpdateMs = 0;
                DiagnosticLog.Write(
                    "native charge started pressAgeMs="
                    + (sampledPress
                        ? (SecondsBetween(sampledPressTimestamp, now) * 1000.0).ToString("F2")
                        : "missing")
                    + " measured=" + measuredPress
                    + " buffered=" + bufferedCharge
                    + " unsupportedInput=" + unsupportedCharge
                    + " charge=" + chargeId
                    + " reason=" + (measuredPress ? "measured" : MeasurementFailureReason(frameJump))
                    + " origin=" + (bufferedCharge ? "native-eligible-frame" : "physical-press")
                    + " excludedMs=" + (measuredPress ? (SecondsBetween(sampledPressTimestamp, nativeChargeTimestamp) * 1000).ToString("F2") : "unknown")
                    + " frame=" + Timeline.FrameNumber + " frameStamp=" + Timeline.FrameTimestamp
                    + " eligibleStamp=" + Timeline.EligibleSince + " originStamp=" + nativeChargeTimestamp
                    + " handlerOffsetMs=" + (SecondsBetween(Timeline.FrameTimestamp, now) * 1000).ToString("F2")
                    + InputEvidence());
            }
            if (nativeCharging || result == BTresult.Success)
            {
                chargeUpdates++;
                if (previousChargeUpdate != 0)
                {
                    double interval = SecondsBetween(previousChargeUpdate, Timeline.FrameTimestamp) * 1000;
                    minimumUpdateMs = Math.Min(minimumUpdateMs, interval);
                    maximumUpdateMs = Math.Max(maximumUpdateMs, interval);
                }
                previousChargeUpdate = Timeline.FrameTimestamp;
            }
            if (result == BTresult.Success)
            {
                // Auto-launch has no release edge. Report measured time to
                // takeoff explicitly as max, and leave the native Jump% alone.
                bool automatic = !predictedCharge.HasValue && frameJump
                    && !unsupportedCharge && nativeChargeTimestamp != 0;
                if (automatic)
                {
                    measuredSeconds = Timeline.ActiveSeconds(nativeChargeTimestamp, now);
                }
                if (!measuredSeconds.HasValue && !(bufferedCharge && !unsupportedCharge))
                    TraceIncident(MeasurementFailureReason(frameJump));
                if (appliedCharge.HasValue)
                {
                    trajectoryProbe.RecordLaunch(appliedCharge.Value.TimerSeconds * 60f, body.Velocity.Y);
                }
                JKRuntime.Gameplay.JumpEvents.Publish(new JKRuntime.Gameplay.JumpResult(
                    "subframe-charge", bufferedCharge && !unsupportedCharge
                        ? (appliedCharge.HasValue ? JKRuntime.Gameplay.JumpEvidence.BufferedHold : JKRuntime.Gameplay.JumpEvidence.BufferedNative)
                        : measuredSeconds.HasValue ? JKRuntime.Gameplay.JumpEvidence.PhysicalHold : JKRuntime.Gameplay.JumpEvidence.Unavailable,
                    measuredSeconds.HasValue ? (double?)(measuredSeconds.Value * 1000) : null,
                    appliedCharge.HasValue ? appliedCharge.Value.WholeFrames : null,
                    predictedCharge.HasValue ? predictedCharge.Value.WholeFrames : null,
                    appliedCharge.HasValue ? (float?)appliedCharge.Value.TimerSeconds : null,
                    observationOnly, automatic, velocityBefore, body.Velocity.Y,
                    appliedCharge.HasValue ? (float?)appliedCharge.Value.ExactFrames : null,
                    predictedCharge.HasValue ? (float?)predictedCharge.Value.ExactFrames : null));
                DiagnosticLog.Write(
                    "native jump completed frames=" + (appliedCharge.HasValue
                        ? appliedCharge.Value.ExactFrames.ToString() : "native")
                    + " multiplier=" + multiplier
                    + " " + JumpPercentIntegration.MeasurementText
                    + " velocityY=" + velocityBefore.ToString("F4")
                    + "->" + body.Velocity.Y.ToString("F4")
                    + " charge=" + chargeId
                    + " reason=" + (bufferedCharge && !unsupportedCharge ? (appliedCharge.HasValue ? "quarter-buffered-release" : "native-buffered")
                        : appliedCharge.HasValue ? "corrected-release"
                        : automatic ? "measured-native-max" : observationOnly && measuredSeconds.HasValue
                            ? "observed-native-release" : MeasurementFailureReason(frameJump))
                    + " rawHoldMs=" + (sampledPress && sampledRelease ? (SecondsBetween(sampledPressTimestamp, sampledReleaseTimestamp) * 1000).ToString("F2") : "none")
                    + " excludedMs=" + (nativeChargeTimestamp != 0 ? (SecondsBetween(sampledPressTimestamp, nativeChargeTimestamp) * 1000).ToString("F2") : "unknown")
                    + " pauseExcludedMs=" + (nativeChargeTimestamp != 0
                        ? ((SecondsBetween(nativeChargeTimestamp, sampledRelease ? sampledReleaseTimestamp : now)
                            - Timeline.ActiveSeconds(nativeChargeTimestamp, sampledRelease ? sampledReleaseTimestamp : now)) * 1000).ToString("F2") : "unknown")
                    + " entryFrame=" + chargeEntryFrame + " releaseFrame=" + Timeline.FrameNumber
                    + " nativeUpdates=" + chargeUpdates
                    + " updateMsMin=" + (double.IsPositiveInfinity(minimumUpdateMs) ? "none" : minimumUpdateMs.ToString("F2"))
                    + " updateMsMax=" + maximumUpdateMs.ToString("F2")
                    + " releaseDeliveryMs=" + (sampledRelease ? (SecondsBetween(sampledReleaseTimestamp, now) * 1000).ToString("F2") : "none")
                    + InputEvidence());
                ClearSampledCharge();
            }
            else if (nativeCharging)
            {
                if (bufferedCharge && !unsupportedCharge)
                    JumpPercentIntegration.RecordBuffered();
                else
                    JumpPercentIntegration.RecordCharging(
                        !unsupportedCharge && nativeChargeTimestamp != 0
                            ? (double?)Timeline.ActiveSeconds(nativeChargeTimestamp, now) : null);
            }
            else if (result == BTresult.Failure)
            {
                if (wasCharging)
                {
                    ClearSampledCharge();
                }
                else
                {
                    // Failure while idle means the frame-based input has not
                    // consumed Jump yet, not that the sampler press is invalid.
                    // Preserve early edges for the next frame; charge entry and
                    // completed-tap replay already reject stale timestamps.
                    unsupportedCharge = false;
                    invalidationReason = null;
                }
            }
            return result;
        }

        public override void ResetResult()
        {
            TraceEvidence("reset-result", null);
            base.ResetResult();
            // A block can cancel a charge before the next tree tick (conveyor
            // walk-off). Do not carry its sampled release/deferred timer into
            // the new native run. Idle early presses and queued edges survive.
            if (nativeCharging) ClearSampledCharge();
        }

        protected override void ResumeRun()
        {
            TraceEvidence("resume-run", null);
            base.ResumeRun();
            ClearSampledCharge();
            // Do not discard edges queued while the native node was inactive
            // (e.g. a new buffered press during the fall after ice leniency).
            // Focus/binding changes still invalidate input in their own paths.
        }

        public void Dispose()
        {
            TraceEvidence("dispose", null);
            ClearSampledCharge();
            sampler.Dispose();
        }

        private void ClearSampledCharge()
        {
            TraceEvidence("clear-charge", null);
            deferredNativeTimer = null;
            nativeCharging = false;
            bufferedCharge = false;
            unsupportedCharge = false;
            sampledPress = false;
            sampledRelease = false;
            sampledPressTimestamp = 0;
            sampledReleaseTimestamp = 0;
            nativeChargeTimestamp = 0;
            invalidationReason = null;
            chargeId = 0;
            chargeUpdates = 0;
            chargeEntryFrame = 0;
            previousChargeUpdate = 0;
            minimumUpdateMs = double.PositiveInfinity;
            maximumUpdateMs = 0;
        }

        private void InvalidateMeasurement(string reason)
        {
            TraceEvidence("invalidate", reason);
            if (!unsupportedCharge && nativeCharging)
                DiagnosticLog.Write("charge measurement invalidated charge=" + chargeId
                    + " reason=" + reason + InputEvidence());
            unsupportedCharge = true;
            if (invalidationReason == null) invalidationReason = reason;
        }

        private string MeasurementFailureReason(bool frameJump)
        {
            if (unsupportedCharge) return invalidationReason ?? "unsupported-input";
            if (!sampledPress) return "missing-sampled-press";
            if (nativeChargeTimestamp == 0)
                return sampledRelease ? "released-press-not-accepted" : "no-measured-charge-origin";
            if (!frameJump && !sampledRelease) return "missing-sampled-release";
            return "native-unmeasured";
        }

        private string InputEvidence()
        {
            return " activeDevices=" + (inputDiagnostics == null ? "unavailable" : inputDiagnostics.ActiveDevices)
                + " sampledPress=" + sampledPress + " sampledRelease=" + sampledRelease
                + " samplerEnabled=" + sampler.Enabled;
        }

        private static float GetChargeMultiplier(BodyComp playerBody)
        {
            float multiplier = playerBody.GetMultipliers();
            return multiplier <= 0f
                    || float.IsNaN(multiplier)
                    || float.IsInfinity(multiplier)
                ? 1f
                : multiplier;
        }

        private static double SecondsBetween(long start, long end)
        {
            return (double)Math.Max(0L, end - start) / Stopwatch.Frequency;
        }

    }
}
