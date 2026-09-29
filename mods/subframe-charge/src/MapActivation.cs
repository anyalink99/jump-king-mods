using JKRuntime.Gameplay;
namespace SubframeCharge
{
    internal static class MapActivation
    {
        internal const string Id = "subframe-charge.timing";
        internal static void Register()
        { MapMechanics.Register("subframe-charge", Id, delegate { if (SettingsStore.Current.Enabled) SettingsStore.SetEnabled(false); }, new MapMechanicParameter("quarter-step", "true", "false")); }
        internal static bool Enabled { get { return MapMechanics.Current(Id, SettingsStore.Current.Enabled).Enabled; } }
        internal static bool QuarterSteps { get { var rule = MapMechanics.Current(Id, SettingsStore.Current.Enabled); return rule.Boolean("quarter-step", rule.Authored ? false : SettingsStore.Current.QuarterStepCharge); } }
        internal static int State() { return (Enabled ? 1 : 0) | (QuarterSteps ? 2 : 0); }
    }
}
