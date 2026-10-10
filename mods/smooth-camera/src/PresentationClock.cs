using System;
using JKRuntime;
namespace SmoothCamera
{
    internal static class PresentationClock
    {
        internal static readonly TimeSpan RenderInterval = PresentationScheduling.Interval;
        private static PresentationScheduling.Client client;
        internal static bool Active { get { return client != null && client.Active; } }
        internal static void Ensure()
        {
            if (client == null) client = PresentationScheduling.Register("smooth-camera",
                delegate { return new PresentationRequest(false, Renderer.HighRefreshRequested); });
        }
        internal static void Release() { if (client != null) client.Dispose(); client = null; }
        internal static void AssertContracts() { PresentationScheduling.ValidateContract(); }
    }
}
