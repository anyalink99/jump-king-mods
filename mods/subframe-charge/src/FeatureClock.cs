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
            PerformanceFeatures.EnsurePresentation();
            if (client != null) return;
            client = PresentationScheduling.RegisterSimulation("subframe-charge",
                () => new PresentationRequest(PerformanceFeatures.InputsRequested, PerformanceFeatures.RefreshRequested),
                SubframePhysics.Advance, null, PlayerPresentation.Reset);
        }
        internal static void BeforeTick(Game game) { Ensure(); PresentationScheduling.BeginTick(game); }
        internal static void Configure(Game game, bool requested) { BeforeTick(game); }
        internal static void AssertContracts() { PresentationScheduling.ValidateContract(); }
        internal static void Release() { if (client != null) client.Dispose(); client = null; }
    }
}
