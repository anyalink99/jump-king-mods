using System;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void ChargeChecks()
        {
            double now = 0;
            using (var charge = new ChargeMonitor(delegate { return now; }))
            {
                int samples = 0;
                var subscription = charge.Subscribe(delegate(ChargeSample value) { samples++; value.HeldFrames = 999; });
                charge.Begin(); now = 170;
                Require(charge.Read().HeldFrames == 10, "Live charge uses the native 17 ms hold clock without writing physics");
                charge.Pause(true); now = 1170;
                Require(charge.Read().HeldFrames == 10, "Native/modal pause excludes elapsed charge time");
                charge.Pause(false); now = 1187;
                Require(charge.Read().HeldFrames == 11, "Charge resumes without counting paused time");
                var snapshot = charge.Capture();
                charge.End(); charge.Release(11.5177f, "subframe-charge");
                now = 9000;
                var result = charge.Read();
                Require(result.Phase == ChargePhase.Released && result.ReleasedFrames == 11.5177f && result.HeldFrames == 11.5177f,
                    "The exact Runtime/SFC launch replaces the live estimate and stays latched");
                Require(result.Provider == "subframe-charge", "Charge preserves the provider of launch evidence");
                charge.Restore(snapshot); now += 17;
                Require(Math.Abs(charge.Read().HeldFrames - 12) < .0001, "Snapshot restore rebases the charge clock without old wall time");
                charge.End();
                Require(charge.Read().Phase == ChargePhase.Cancelled && !charge.Read().ReleasedFrames.HasValue, "A cancelled charge doesn't invent launch evidence");
                charge.Begin(); charge.End(); charge.Release(null, "native.jump");
                Require(!charge.Read().ReleasedFrames.HasValue, "Unknown native launch F stays unknown");
                int before = samples; subscription.Dispose(); charge.Tick(.25f);
                Require(samples == before, "Disposing the charge observer stops callbacks");
                Require(charge.Read().NativeTimerSeconds == .25f, "Native physics timer is separate from held-frame timing");
                charge.Begin();now+=591.6;charge.Release(null,"subframe-charge",true);
                Require(charge.Read().HeldFrames==35&&charge.Read().ReleasedFrames==35,"A native automatic full jump is F35 even when the observed hold was only F34.8");
                charge.Begin();now+=1200;
                Require(charge.Read().HeldFrames==35,"A delayed sample can't run past the native full-charge limit");
                charge.Tick(.6f);
                Require(charge.Read().HeldFrames==35,"The saturated native timer wins over a short wall-clock estimate");
                charge.Limits(.6f,.5f);charge.Begin();now+=2000;
                Require(charge.Read().HeldFrames==71,"The observation respects slow-charge surfaces instead of imposing F35 on every map");
                var slow=charge.Capture();charge.Limits(.6f,1);charge.Restore(slow);charge.Release(null,"native.jump",true);
                Require(charge.Read().ReleasedFrames==71,"A charge restore retains its native multiplier and full-charge boundary");
            }
        }
    }
}
