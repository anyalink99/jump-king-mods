using System;

namespace MultiplayerExpansion
{
    internal sealed class RenderCadence
    {
        private long lastSlot = -1;
        private int lastRate;

        internal bool Allow(long simulationTicks, int rate)
        {
            // only gate presentation; the native simulation clock keeps every update
            long slot = simulationTicks * rate / TimeSpan.TicksPerSecond;
            if (lastRate == rate && lastSlot == slot) return false;
            lastRate = rate;
            lastSlot = slot;
            return true;
        }
    }
}
