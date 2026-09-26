using System;
using System.Globalization;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace MoreItems
{
    internal static class HammerDefinition
    {
        internal const string Id = "hammer";
        private static readonly Setting<bool> Equipped = new Setting<bool>("more-items.hammer.equipped", "Hammer equipped",
            IsEquipped, SettingsStore.SetHammerEquipped,
            delegate { ItemModuleRegistry.RefreshRuntime(Id); UIApi.NotifyInventoryItemChanged("more-items." + Id); });

        internal static void RegisterModule()
        {
            MoreItemsApi.RegisterModule(new ItemModuleDefinition(Id, "Hammer",
                delegate { SettingsStore.EnsureLoaded(); return SettingsStore.Current.EnableHammer; },
                SettingsStore.SetHammerEnabled, null, HammerKing.HammerInstaller.Apply, HammerKing.HammerInstaller.Uninstall));
        }

        internal static void Register()
        {
            MoreItemsApi.Register(new ConsumableDefinition(Id, "Hammer",
                "Equip and move the mouse to climb with the hammer. Unequip to restore ordinary movement.",
                delegate { return MoreItemsApi.GetCount(Id) > 0; }, null,
                new Color(157, 132, 105), "Hammers", null, DrawIcon, false, null,
                delegate { return MoreItemsApi.IsModuleEnabled(Id); }, IsEquipped, SetEquipped));
            UIApi.RegisterMerchantOffer(new MerchantOfferDefinition("more-items.hammer", "Hammer",
                "A climbing hammer. Three silver coins.", UIApi.VanillaCurrencyId(Items.Silver),
                delegate { return 3; }, new Color(157, 132, 105), Add,
                delegate { return MoreItemsApi.IsModuleEnabled(Id); },
                delegate { return MoreItemsApi.GetCount(Id) > 0; }, DrawIcon));
            UIApi.RegisterDebugAction(new UiDebugActionDefinition("more-items.add-hammer", "More Items", "Add Hammer",
                Add, delegate { return MoreItemsApi.IsModuleEnabled(Id); }));
            UIApi.RegisterDebugAction(new UiDebugActionDefinition("more-items.hammer-strength", "More Items", "Hammer: 100%",
                delegate { HammerKing.Settings.Strength.Set(100); }, null,
                delegate { return "Hammer: " + HammerKing.Settings.Strength.Value.ToString(CultureInfo.InvariantCulture) + "%"; },
                delegate(int direction)
                {
                    int next = HammerKing.Settings.NormalizeStrength(HammerKing.Settings.Strength.Value + Math.Sign(direction) * HammerKing.Settings.StrengthStep);
                    HammerKing.Settings.Strength.Set(next);
                }, "RESET"));
        }

        private static void Add() { if (MoreItemsApi.GetCount(Id) == 0) MoreItemsApi.Add(Id, 1); }
        private static bool IsEquipped() { SettingsStore.EnsureLoaded(); return SettingsStore.Current.HammerEquipped; }
        private static bool SetEquipped(bool equipped)
        {
            if (MoreItemsApi.GetCount(Id) <= 0) return false;
            Equipped.Set(equipped); return true;
        }
        internal static bool IsEnabledForPlayer()
        { return MoreItemsApi.IsModuleEnabled(Id) && IsEquipped() && MoreItemsApi.GetCount(Id) > 0; }

        private static void DrawIcon(Rectangle destination)
        {
            Vector2 center = destination.Center.ToVector2();
            // Reuse the gameplay silhouette at its native pixel scale.
            HammerKing.HammerSprite.DrawHammer(JumpKing.Game1.spriteBatch,
                JumpKing.Game1.instance.contentManager.Pixel.texture, center + new Vector2(-6, 8), center + new Vector2(8, -8), false);
        }
    }
}
