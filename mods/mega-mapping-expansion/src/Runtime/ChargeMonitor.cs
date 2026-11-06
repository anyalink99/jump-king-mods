using System;
using System.Collections.Generic;
using System.Diagnostics;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion
{
    // live phase is presentation timing; only a launch result can claim exact F
    internal sealed class ChargeMonitor : IDisposable
    {
        private readonly Func<double> clock;
        private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly List<Action<ChargeSample>> observers = new List<Action<ChargeSample>>();
        private ChargeSample state = new ChargeSample { Provider = "native" };
        private double began, frozen, pauseBegan;
        private bool disposed;
        internal ChargeMonitor() : this(delegate { return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency; }) { }
        internal ChargeMonitor(Func<double> milliseconds) { clock = milliseconds; }
        internal void Begin()
        {
            began = clock(); frozen = 0; pauseBegan = began;
            state = new ChargeSample { Sequence = state.Sequence + 1, Phase = ChargePhase.Charging, Provider = "native", Paused = state.Paused,
                MaximumFrames=state.MaximumFrames,NativeChargeSeconds=state.NativeChargeSeconds,ChargeMultiplier=state.ChargeMultiplier };
            Publish();
        }
        internal void Limits(float seconds,float multiplier)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0||float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<=0)return;
            state.NativeChargeSeconds=seconds;state.ChargeMultiplier=multiplier;
            state.MaximumFrames=Math.Max(1,seconds*60f/multiplier-1f);
        }
        internal void Pause(bool paused)
        {
            if (state.Paused == paused) return;
            if (paused) pauseBegan = clock(); else frozen += Math.Max(0, clock() - pauseBegan);
            state.Paused = paused;
        }
        internal void End()
        {
            if (state.Phase != ChargePhase.Charging) return;
            state = Read(); state.Phase = ChargePhase.Cancelled; Publish();
        }
        internal void Release(float? frames, string provider,bool automatic=false)
        {
            state = Read(); state.Phase = ChargePhase.Released;
            // native auto-launch proves saturation even when there's no physical release edge
            if(!frames.HasValue&&automatic)frames=(float)state.MaximumFrames;
            state.ReleasedFrames = frames; state.Provider = provider;
            if (frames.HasValue) state.HeldFrames = frames.Value;
            Publish();
        }
        internal void Tick(float timer) { state.NativeTimerSeconds = timer; Publish(); }
        internal ChargeSample Read()
        {
            if (disposed) throw new ObjectDisposedException("Mapping charge");
            var result = Copy(state);
            if (state.Phase == ChargePhase.Charging)
            {
                result.ElapsedMilliseconds = Math.Max(0, (state.Paused ? pauseBegan : clock()) - began - frozen);
                result.HeldFrames = state.NativeTimerSeconds>=state.NativeChargeSeconds?state.MaximumFrames:
                    Math.Min(state.MaximumFrames,Math.Max(1,result.ElapsedMilliseconds/17.0));
            }
            return result;
        }
        internal IDisposable Subscribe(Action<ChargeSample> observer)
        {
            if (disposed) throw new ObjectDisposedException("Mapping charge");
            if (observer == null) throw new ArgumentNullException("observer");
            observers.Add(observer); return new Subscription(this, observer);
        }
        private sealed class Subscription : IDisposable
        {
            private ChargeMonitor owner; private readonly Action<ChargeSample> observer;
            internal Subscription(ChargeMonitor value, Action<ChargeSample> action) { owner = value; observer = action; }
            public void Dispose()
            {
                if (owner == null) return;
                if (System.Threading.Thread.CurrentThread.ManagedThreadId != owner.thread)
                    throw new InvalidOperationException("Mapping charge subscriptions require the game thread");
                owner.observers.Remove(observer); owner = null;
            }
        }
        private void Publish()
        {
            foreach (var observer in observers.ToArray())
                try { observer(Read()); }
                catch (Exception error) { observers.Remove(observer); ModEntry.Log("Charge observer disabled: " + error); }
        }
        private static ChargeSample Copy(ChargeSample value)
        { return new ChargeSample { Sequence = value.Sequence, Phase = value.Phase, Paused = value.Paused,
            ElapsedMilliseconds = value.ElapsedMilliseconds, HeldFrames = value.HeldFrames, NativeTimerSeconds = value.NativeTimerSeconds,
            ReleasedFrames = value.ReleasedFrames, Provider = value.Provider,MaximumFrames=value.MaximumFrames,
            NativeChargeSeconds=value.NativeChargeSeconds,ChargeMultiplier=value.ChargeMultiplier }; }
        internal ChargeSample Capture() { return Read(); }
        internal void Restore(ChargeSample value)
        {
            state = Copy(value); began = clock() - state.ElapsedMilliseconds;
            frozen = 0; pauseBegan = clock(); Publish();
        }
        public void Dispose() { disposed = true; observers.Clear(); }
    }

}
