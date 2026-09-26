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
    internal sealed partial class SubframeChargeState
    {
        private long observedRestoreEpoch = JKRuntime.State.GameState.Snapshots.RestoreEpoch;
        private void CheckStateRestore()
        {
            long epoch = JKRuntime.State.GameState.Snapshots.RestoreEpoch;
            if (epoch == observedRestoreEpoch) return;
            observedRestoreEpoch = epoch;
            sampler.Reset();
            InvalidateMeasurement("state-restored");
            sampledPress = sampledRelease = false;
            sampledPressTimestamp = sampledReleaseTimestamp = nativeChargeTimestamp = 0;
            Timeline.ResetEligibility();
        }
        private void DrainSampledTransitions(long now)
        {
            JumpInputTransition transition;
            while (sampler.TryDequeue(out transition))
            {
                TraceEvidence("edge", "edgeQpc=" + transition.Timestamp + " down=" + transition.IsDown + " reliable=" + transition.Reliable);
                if (!transition.Reliable)
                {
                    InvalidateMeasurement("input-observation-lost");
                    sampledPress = sampledRelease = false;
                    nativeChargeTimestamp = 0;
                    continue;
                }
                DiagnosticLog.Write(
                    "sampler edge=" + (transition.IsDown ? "down" : "up")
                    + " deliveryMs="
                    + (SecondsBetween(transition.Timestamp, now) * 1000.0)
                        .ToString("F2"));
                if (transition.IsDown)
                {
                    if (nativeCharging)
                    {
                        // A second press cannot replace this charge's origin.
                        InvalidateMeasurement("second-sampled-press-during-charge");
                    }
                    sampledPress = true;
                    sampledRelease = false;
                    sampledPressTimestamp = transition.Timestamp;
                    sampledReleaseTimestamp = 0;
                }
                else if (sampledPress)
                {
                    sampledRelease = true;
                    sampledReleaseTimestamp = transition.Timestamp;
                }
            }
        }

        internal void ObserveFrameInput()
        {
            CheckStateRestore();
            sampler.LogHealth(Stopwatch.GetTimestamp());
            if (inputDiagnostics != null) inputDiagnostics.Observe();
            JumpPercentIntegration.EnsureDisplayHook();
            if (nativeCharging)
            {
                if (HasUnsupportedJumpDown()) InvalidateMeasurement("unsupported-device-active");
            }
            bool jump = input.GetState().jump;
            TraceEvidence("frame", "observedJump=" + jump);
            if (!frameJumpInitialized || jump != lastFrameJump)
            {
                DiagnosticLog.Write(
                    "game frame edge=" + (jump ? "down" : "up")
                    + " charging=" + nativeCharging + InputEvidence());
                frameJumpInitialized = true;
                lastFrameJump = jump;
            }
        }

    }
}
