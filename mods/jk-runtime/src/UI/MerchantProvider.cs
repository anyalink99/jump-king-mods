using System;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal static class MerchantProvider
    {
        internal static bool CanPurchase(MerchantOfferDefinition offer)
        {
            try { return offer.CanPurchase(); }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Merchant availability failed for "
                    + offer.Id + ": " + error.Message);
                return false;
            }
        }

        internal static bool IsOwned(MerchantOfferDefinition offer)
        {
            try { return offer.IsOwned(); }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Merchant ownership failed for "
                    + offer.Id + ": " + error.Message);
                return true;
            }
        }

        internal static int Balance(UiCurrencyDefinition currency)
        {
            try { return Math.Max(0, currency.GetBalance()); }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Currency balance failed for "
                    + currency.Id + ": " + error.Message);
                return 0;
            }
        }

        internal static bool Spend(UiCurrencyDefinition currency, int amount)
        {
            try { return amount > 0 && currency.Spend(amount); }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Currency spend failed for "
                    + currency.Id + ": " + error.Message);
                return false;
            }
        }

        internal static bool Grant(UiCurrencyDefinition currency, int amount)
        {
            try
            {
                currency.Grant(amount);
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Currency grant failed for "
                    + currency.Id + ": " + error.Message);
                return false;
            }
        }

        internal static void DrawIcon(UiCurrencyDefinition currency, Rectangle bounds)
        {
            try { currency.DrawIcon(bounds); }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Currency icon failed for "
                    + currency.Id + ": " + error.Message);
                Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
                Game1.spriteBatch.Draw(pixel, bounds, UiTheme.Muted);
            }
        }
    }
}
