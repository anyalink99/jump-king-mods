using System;
using JKRuntime;
using Microsoft.Xna.Framework;
namespace SubframeCharge
{
    internal static class FeatureClock
    {
        internal static readonly TimeSpan RenderInterval = PresentationScheduling.Interval;
        private static PresentationScheduling.Client client;
        internal static bool Active { get { return client != null && client.Active; } }
        internal static bool HighRefresh { get { return client != null && client.HighRefresh; } }
        internal static float Alpha { get { return PresentationScheduling.Alpha; } }
        internal static void Ensure()
        {
            if (client != null) return;
            client = PresentationScheduling.Register("subframe-charge",
                delegate { return new PresentationRequest(PerformanceFeatures.InputsRequested, PerformanceFeatures.RefreshRequested); },
                delegate(float delta) {
                    using (RuntimeApi.MeasurePerformance("subframe-input.poll")) ResponsiveInput.Poll();
                    using (RuntimeApi.MeasurePerformance("subframe-input.menu")) MenuCadence.Pump(delta);
                }, delegate { ResponsiveInput.ReleaseToNative(); PlayerPresentation.Reset(); });
        }
        internal static void BeforeTick(Game game) { Ensure(); PresentationScheduling.BeginTick(game); }
        internal static void Configure(Game game, bool requested) { BeforeTick(game); }
        internal static void AssertContracts() { PresentationScheduling.ValidateContract(); }
        internal static void Release() { if (client != null) client.Dispose(); client = null; }
    }
}
