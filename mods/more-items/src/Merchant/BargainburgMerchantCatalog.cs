using System;
using System.Collections.Generic;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using Microsoft.Xna.Framework;

using JKRuntime.UI;

namespace MoreItems
{
    internal static class BargainburgMerchantCatalog
    {
        private static readonly Items[] BaseCurrencies =
        {
            Items.GoldRing,
            Items.Ruby,
            Items.Silver,
            Items.GhostFragment,
            Items.Shroom
        };

        internal static MerchantDefinition CreateDefinition()
        {
            return new MerchantDefinition(
                "more-items.bargainburg-shop",
                "Merchant",
                "Trade",
                100,
                MoreItemsRuntime.MerchantAvailable,
                CreateOffers,
                delegate
                {
                    IList<MerchantOfferDefinition> offers = CreateOffers();
                    return CreateExchanges(offers);
                });
        }

        private static IList<MerchantOfferDefinition> CreateOffers()
        {
            List<MerchantOfferDefinition> offers = new List<MerchantOfferDefinition>();
            offers.Add(CreateVanillaOffer(
                "boots",
                "Boots",
                "Reliable boots from the Bargainburg workshop.",
                Items.Shoes,
                Items.GoldRing,
                delegate { return InventoryManager.GetItemCount(Items.GoldRing) + 1; }));
            offers.Add(CreateVanillaOffer(
                "snake-ring",
                "Snake Ring",
                "A cold ring traded far above the snake's price.",
                Items.SnakeRing,
                Items.Ruby,
                delegate { return 3; }));
            offers.Add(CreateVanillaOffer(
                "giant-boots",
                "Giant Boots",
                "Heavy iron boots. Thirty pieces of silver.",
                Items.GiantBoots,
                Items.Silver,
                delegate { return 30; }));
            offers.Add(CreateVanillaOffer(
                "yellow-shoes",
                "Yellow Shoes",
                "Bright shoes imported from the haunted route.",
                Items.YellowShoes,
                Items.GhostFragment,
                delegate { return 12; }));
            offers.Add(CreateVanillaOffer(
                "tunic",
                "Tunic",
                "A scholar's tunic at a merchant's markup.",
                Items.Tunic,
                Items.Shroom,
                delegate { return 27; }));
            foreach (MerchantOfferDefinition offer in UIApi.GetMerchantOffers())
            {
                offers.Add(offer);
            }
            return offers;
        }

        private static MerchantOfferDefinition CreateVanillaOffer(
            string id,
            string name,
            string description,
            Items product,
            Items currency,
            Func<int> price)
        {
            Items captured = product;
            return new MerchantOfferDefinition(
                "more-items.bargainburg." + id,
                name,
                description,
                UIApi.VanillaCurrencyId(currency),
                price,
                Color.White,
                delegate { BargainburgNativeAdapter.GrantVanillaItem(captured); },
                delegate { return !InventoryManager.HasItem(captured); },
                delegate { return InventoryManager.HasItem(captured); },
                delegate(Rectangle bounds) { BargainburgNativeAdapter.DrawItemIcon(captured, bounds); });
        }

        private static IList<MerchantExchangeDefinition> CreateExchanges(
            IList<MerchantOfferDefinition> offers)
        {
            List<UiCurrencyDefinition> currencies = new List<UiCurrencyDefinition>();
            foreach (Items item in BaseCurrencies) AddCurrency(currencies, MerchantCurrencies.Get(item));
            foreach (MerchantOfferDefinition offer in offers)
            {
                UiCurrencyDefinition currency;
                if (UIApi.TryGetCurrency(offer.CurrencyId, out currency)) AddCurrency(currencies, currency);
            }
            List<MerchantExchangeDefinition> exchanges = new List<MerchantExchangeDefinition>();
            foreach (UiCurrencyDefinition from in currencies)
            {
                foreach (UiCurrencyDefinition to in currencies)
                {
                    if (ReferenceEquals(from, to)) continue;
                    exchanges.Add(new MerchantExchangeDefinition(
                        from.Id,
                        from.ExchangeUnit * 3,
                        to.Id,
                        to.ExchangeUnit));
                }
            }
            return exchanges;
        }

        private static void AddCurrency(
            IList<UiCurrencyDefinition> currencies,
            UiCurrencyDefinition currency)
        {
            foreach (UiCurrencyDefinition existing in currencies)
                if (string.Equals(existing.Id, currency.Id, StringComparison.OrdinalIgnoreCase)) return;
            currencies.Add(currency);
        }
    }
}
