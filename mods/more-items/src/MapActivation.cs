using JKRuntime.Gameplay;

namespace MoreItems
{
    internal static class MapActivation
    {
        internal static void Register()
        {
            MapMechanics.Register("more-items", "more-items.jetpack", delegate { SettingsStore.EnsureLoaded(); if (SettingsStore.Current.EnableJetpack) SettingsStore.SetJetpackEnabled(false); },
                new MapMechanicParameter("equipment", "owned", "loan-equipped"));
            MapMechanics.Register("more-items", "more-items.hammer", delegate { SettingsStore.EnsureLoaded(); if (SettingsStore.Current.EnableHammer) SettingsStore.SetHammerEnabled(false); },
                new MapMechanicParameter("equipment", "owned", "loan-equipped"));
            MapMechanics.Register("more-items", "more-items.rewinder", delegate { SettingsStore.EnsureLoaded(); if (SettingsStore.Current.EnableRewinders) SettingsStore.SetRewindersEnabled(false); });
        }
        internal static MapMechanicDecision Permission(string id, bool preference)
        { return MapMechanics.Current("more-items." + id, preference); }
        internal static bool Equipment(string id, bool preference, bool ownedEquipped)
        {
            var rule = Permission(id, preference);
            return rule.Enabled && (ownedEquipped || rule.Authored && rule.Parameter("equipment", "loan-equipped") == "loan-equipped");
        }
        internal static bool Loan(string id)
        {
            var rule = Permission(id, false);
            return rule.Enabled && rule.Authored && rule.Parameter("equipment", "loan-equipped") == "loan-equipped";
        }
        internal static int EquipmentState()
        { return (JetpackDefinition.IsEnabledForPlayer() ? 1 : 0) | (CanInstallHammer() ? 2 : 0); }
        internal static bool CanInstallHammer()
        {
            var player = JumpKing.GameManager.GameLoop.m_player;
            return HammerDefinition.IsEnabledForPlayer() && (player == null || JKRuntime.Gameplay.PlayerControl.Available(player.m_body, "more-items.hammer"));
        }
        internal static void Refresh()
        { ItemModuleRegistry.RefreshRuntime("hammer"); ItemModuleRegistry.RefreshRuntime("jetpack"); }
    }
}
