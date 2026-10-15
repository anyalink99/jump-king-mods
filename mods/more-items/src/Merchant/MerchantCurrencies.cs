using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;

using JKRuntime.UI;

namespace MoreItems
{
    internal static class MerchantCurrencies
    {
        private static readonly Items[] ItemsToRegister =
        {
            Items.GoldRing,
            Items.Ruby,
            Items.Silver,
            Items.GhostFragment,
            Items.Shroom
        };

        internal static void Register()
        {
            foreach (Items item in ItemsToRegister)
            {
                Items captured = item;
                UIApi.RegisterCurrency(new UiCurrencyDefinition(
                    UIApi.VanillaCurrencyId(captured),
                    Name(captured, false),
                    Name(captured, true),
                    Unit(captured),
                    delegate { return InventoryManager.GetItemCount(captured); },
                    delegate(int amount)
                    {
                        if (amount <= 0 || InventoryManager.GetItemCount(captured) < amount) return false;
                        InventoryManager.RemoveItems(captured, amount);
                        return true;
                    },
                    delegate(int amount) { if (amount > 0) InventoryManager.AddItems(captured, amount); },
                    delegate(Microsoft.Xna.Framework.Rectangle bounds) { BargainburgNativeAdapter.DrawItemIcon(captured, bounds); }));
            }
        }

        internal static UiCurrencyDefinition Get(Items item)
        {
            UiCurrencyDefinition result;
            if (!UIApi.TryGetCurrency(UIApi.VanillaCurrencyId(item), out result))
                throw new System.InvalidOperationException("Vanilla merchant currencies are not registered");
            return result;
        }

        private static int Unit(Items item)
        {
            switch (item)
            {
                case Items.Silver: return 10;
                case Items.GhostFragment: return 4;
                case Items.Shroom: return 9;
                default: return 1;
            }
        }

        private static string Name(Items item, bool plural)
        {
            switch (item)
            {
                case Items.GoldRing: return plural ? "Gold Rings" : "Gold Ring";
                case Items.Ruby: return plural ? "Rubies" : "Ruby";
                case Items.Silver: return "Silver Coins";
                case Items.GhostFragment: return plural ? "Ghost Fragments" : "Ghost Fragment";
                case Items.Shroom: return plural ? "Shrooms" : "Shroom";
                default: return item.ToString();
            }
        }
    }
}
