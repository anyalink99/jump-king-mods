using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;
using JKRuntime.UI;

namespace MoreItems
{
    internal static class JetpackDefinition
    {
        internal const string Id = "jetpack";

        internal static void RegisterModule()
        {
            MoreItemsApi.RegisterModule(new ItemModuleDefinition(
                Id,
                "Jetpack",
                delegate
                {
                    SettingsStore.EnsureLoaded();
                    return SettingsStore.Current.EnableJetpack;
                },
                SettingsStore.SetJetpackEnabled,
                delegate(JumpKing.PauseMenu.BT.MenuSelector page)
                {
                    page.AddChild(new JumpKingJetpack.JetpackVisibilityOption());
                    page.AddChild(new JumpKingJetpack.JetpackTrailOption());
                    page.AddChild(new JumpKingJetpack.JetpackVolumeOption());
                },
                RefreshRuntime,
                JumpKingJetpack.JetpackInstaller.Uninstall));
        }

        internal static void Register()
        {
            MoreItemsApi.Register(new ConsumableDefinition(
                Id,
                "Jetpack",
                "A momentum-preserving jetpack. Equip it, release Jump after takeoff, then press and hold Jump in the air.",
                delegate { return MoreItemsApi.GetCount(Id) > 0; },
                null,
                new Color(244, 190, 48),
                "Jetpacks",
                null,
                JumpKingJetpack.JetpackArt.DrawBody,
                false,
                null,
                delegate { return MoreItemsApi.IsModuleEnabled(Id); },
                IsEquipped,
                SetEquipped));

            UIApi.RegisterMerchantOffer(new MerchantOfferDefinition(
                "more-items.jetpack",
                "Jetpack",
                "A compact engine for controlled mid-air thrust.",
                UIApi.VanillaCurrencyId(Items.GhostFragment),
                delegate { return 12; },
                new Color(244, 190, 48),
                delegate { MoreItemsApi.Add(Id, 1); },
                delegate { return MoreItemsApi.IsModuleEnabled(Id); },
                delegate { return MoreItemsApi.GetCount(Id) > 0; },
                JumpKingJetpack.JetpackArt.DrawBody));

            UIApi.RegisterDebugAction(new UiDebugActionDefinition(
                "more-items.add-jetpack",
                "More Items",
                "Add Jetpack",
                delegate
                {
                    if (MoreItemsApi.GetCount(Id) == 0) MoreItemsApi.Add(Id, 1);
                },
                delegate { return MoreItemsApi.IsModuleEnabled(Id); }));
        }

        private static bool IsEquipped()
        {
            SettingsStore.EnsureLoaded();
            return SettingsStore.Current.JetpackEquipped;
        }

        private static bool SetEquipped(bool equipped)
        {
            if (MoreItemsApi.GetCount(Id) <= 0) return false;
            SettingsStore.EnsureLoaded();
            SettingsStore.SetJetpackEquipped(equipped);
            ItemModuleRegistry.RefreshRuntime(Id);
            UIApi.NotifyInventoryItemChanged("more-items." + Id);
            return true;
        }

        internal static bool IsEnabledForPlayer()
        {
            SettingsStore.EnsureLoaded();
            return MoreItemsApi.IsModuleEnabled(Id)
                && SettingsStore.Current.JetpackEquipped
                && MoreItemsApi.GetCount(Id) > 0;
        }

        private static void RefreshRuntime()
        {
            if (IsEnabledForPlayer()) JumpKingJetpack.JetpackInstaller.Apply();
            else JumpKingJetpack.JetpackInstaller.Uninstall();
        }
    }
}
