using JKRuntime.Input;
using System;

namespace SubframeCharge
{
    internal static class SubframeTapReplay
    {
        internal static bool TryMeasureCompletedTap(
            bool nativeCharging,
            bool sampledPress,
            bool sampledRelease,
            long pressTimestamp,
            long releaseTimestamp,
            long now,
            long timestampFrequency,
            double maximumDeliveryAgeSeconds,
            out double heldSeconds)
        {
            heldSeconds = 0.0;
            if (timestampFrequency <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "timestampFrequency");
            }
            if (double.IsNaN(maximumDeliveryAgeSeconds)
                || double.IsInfinity(maximumDeliveryAgeSeconds)
                || maximumDeliveryAgeSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    "maximumDeliveryAgeSeconds");
            }
            if (nativeCharging
                || !sampledPress
                || !sampledRelease
                || releaseTimestamp < pressTimestamp
                || now < releaseTimestamp)
            {
                return false;
            }

            double deliveryAgeSeconds =
                (double)(now - releaseTimestamp) / timestampFrequency;
            if (deliveryAgeSeconds > maximumDeliveryAgeSeconds)
            {
                return false;
            }

            heldSeconds =
                (double)(releaseTimestamp - pressTimestamp)
                / timestampFrequency;
            return true;
        }
    }
}
