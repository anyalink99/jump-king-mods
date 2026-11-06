using JKRuntime.Gameplay;
namespace SubframeCharge
{
    internal static class MapActivation
    {
        internal const string Id = "subframe-charge.timing";
        internal static void Register()
        { MapMechanics.Register("subframe-charge", Id, delegate { if (SettingsStore.Current.Enabled) SettingsStore.SetEnabled(false); },
            new MapMechanicParameter("quarter-step", "true", "false"),
            new MapMechanicParameter("substep-charge", "true", "false"),
            new MapMechanicParameter("substep-ms", Substep.MinimumMilliseconds, Substep.MaximumMilliseconds)); }
        internal static bool Enabled { get { return MapMechanics.Current(Id, SettingsStore.Current.Enabled).Enabled; } }
        internal static double StepMilliseconds { get { return ResolveStep(MapMechanics.Current(Id, SettingsStore.Current.Enabled), SettingsStore.Current); } }
        internal static bool QuarterSteps { get { return StepMilliseconds < Substep.MaximumMilliseconds; } }
        internal static double ResolveStep(MapMechanicDecision rule, SubframeChargeSettings settings)
        {
            string interval = rule.Parameter("substep-ms");
            string enabled = rule.Parameter("substep-charge");
            string legacy = rule.Parameter("quarter-step");
            bool active = enabled != null ? bool.Parse(enabled) : interval != null ||
                (legacy != null ? bool.Parse(legacy) : !rule.Authored && settings.QuarterStepCharge);
            if (!active) return Substep.MaximumMilliseconds;
            if (interval != null) return rule.Number("substep-ms", 4.25f);
            // authored legacy quarters must stay quarters even when the player selects 1 ms
            return rule.Authored || legacy != null ? 4.25 : Substep.Milliseconds(settings.ChargeStep);
        }
        internal static int State()
        {
            return Enabled ? ((float)StepMilliseconds).GetHashCode() : 0;
        }
    }
}
