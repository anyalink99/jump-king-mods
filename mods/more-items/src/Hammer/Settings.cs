using System;
using JKRuntime.Settings;

namespace HammerKing
{
    internal static class Settings
    {
        internal const int MinimumStrength = 50, MaximumStrength = 125, StrengthStep = 5;
        internal static float Sensitivity
        { get { MoreItems.SettingsStore.EnsureLoaded(); return MoreItems.SettingsStore.Current.HammerSensitivity; } }
        internal static int NormalizeStrength(int value)
        {
            value = Math.Max(MinimumStrength, Math.Min(MaximumStrength, value));
            return MinimumStrength + ((value - MinimumStrength + StrengthStep / 2) / StrengthStep) * StrengthStep;
        }
        // Physics keeps the accepted 0.1.12 coefficients. The item's displayed
        // 100% maps to that controller's former 120%, including the force cap.
        internal static float PhysicsStrength(int percent) { return percent * .012f; }
        internal static readonly Setting<int> Strength = new Setting<int>("hammer-king.strength", "Hammer strength",
            delegate { MoreItems.SettingsStore.EnsureLoaded(); return MoreItems.SettingsStore.Current.HammerStrength; },
            MoreItems.SettingsStore.SetHammerStrength, null,
            delegate(int value) { return value >= MinimumStrength && value <= MaximumStrength && value % StrengthStep == 0; });
    }
}
