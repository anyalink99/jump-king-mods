using JKRuntime.Gameplay;
namespace CasualJumping
{
    internal static class MapActivation
    {
        internal const string Id = "casual.controls";
        internal static void Register()
        { MapMechanics.Register("casual-jumping", Id, SettingsStore.DisableForMap, new MapMechanicParameter("mode", "casual", "casual-plus")); }
        internal static ControlMode Mode
        {
            get
            {
                SettingsStore.EnsureLoaded(); var settings = SettingsStore.Current;
                var rule = MapMechanics.Current(Id, settings.Enabled && settings.Mode != ControlMode.Vanilla);
                if (!rule.Enabled) return ControlMode.Vanilla;
                return rule.Authored ? (rule.Parameter("mode", "casual-plus") == "casual" ? ControlMode.Casual : ControlMode.CasualPlus) : settings.Mode;
            }
        }
    }
}
