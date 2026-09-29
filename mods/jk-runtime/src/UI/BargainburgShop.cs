using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime.UI
{
    internal sealed class ShopOffer
    {
        internal UiCurrencyDefinition Currency;
        internal MerchantOfferDefinition Definition;
        private bool reportedPriceFailure;
        internal int Price
        {
            get
            {
                try { return Definition.Price; }
                catch (Exception error)
                {
                    if (!reportedPriceFailure)
                    {
                        reportedPriceFailure = true;
                        Console.WriteLine("[JK Runtime UI] Merchant price failed for "
                            + Definition.Id + ": " + error.Message);
                    }
                    return int.MaxValue;
                }
            }
        }
    }

    internal sealed class ExchangeOffer
    {
        internal UiCurrencyDefinition From;
        internal int FromAmount;
        internal UiCurrencyDefinition To;
        internal int ToAmount;
    }

    internal sealed class MerchantPage : IUiPage, IUiPageInputPolicy
    {
        private readonly List<ShopOffer> offers = new List<ShopOffer>();
        private readonly List<ExchangeOffer> exchanges = new List<ExchangeOffer>();
        private readonly List<UiCurrencyDefinition> currencies = new List<UiCurrencyDefinition>();
        private readonly string title;
        private readonly UiFrame outerFrame = new UiFrame(new Rectangle(22, 18, 436, 324));
        private readonly UiFrame detailFrame = new UiFrame(new Rectangle(280, 92, 162, 205));
        private readonly UiFrame emptyFrame = new UiFrame(new Rectangle(36, 92, 406, 205));
        private int tab;
        private int index;
        private readonly UiListViewport viewport = new UiListViewport();
        private float feedbackTime;
        private string feedback = string.Empty;
        private Color feedbackColor = UiTheme.Text;
        private bool close;

        public bool WantsClose { get { return close; } }
        public bool HandlesCancel { get { return true; } }

        internal MerchantPage(
            string merchantTitle,
            IList<MerchantOfferDefinition> merchantOffers,
            IList<MerchantExchangeDefinition> merchantExchanges)
        {
            title = string.IsNullOrWhiteSpace(merchantTitle) ? "Merchant" : merchantTitle;
            foreach (MerchantOfferDefinition definition in merchantOffers ?? new List<MerchantOfferDefinition>())
            {
                UiCurrencyDefinition currency = ResolveCurrency(definition.CurrencyId);
                offers.Add(new ShopOffer
                {
                    Currency = currency,
                    Definition = definition
                });
                AddCurrency(currency);
            }
            foreach (MerchantExchangeDefinition definition in merchantExchanges ?? new List<MerchantExchangeDefinition>())
            {
                UiCurrencyDefinition from = ResolveCurrency(definition.FromCurrencyId);
                UiCurrencyDefinition to = ResolveCurrency(definition.ToCurrencyId);
                AddCurrency(from);
                AddCurrency(to);
                exchanges.Add(new ExchangeOffer
                {
                    From = from,
                    FromAmount = definition.FromAmount,
                    To = to,
                    ToAmount = definition.ToAmount
                });
            }
        }

        public void OnOpen() { close = false; }
        public void OnClose() { }

        public void Update(UiInput input, float delta)
        {
            feedbackTime = Math.Max(0f, feedbackTime - delta);
            if (input.Action == UiAction.Cancel) { close = true; UiSounds.Play(UiSound.Back); return; }
            if (input.Action == UiAction.Left || input.Action == UiAction.Right)
            {
                tab = 1 - tab;
                index = 0;
                viewport.Reset();
                Move();
            }
            int count = tab == 0 ? offers.Count : exchanges.Count;
            if (count == 0) return;
            if (input.Action == UiAction.Up) { UiSounds.Select(ref index, (index + count - 1) % count); viewport.FollowSelection(index, count, 5); }
            else if (input.Action == UiAction.Down) { UiSounds.Select(ref index, (index + 1) % count); viewport.FollowSelection(index, count, 5); }
            else if (input.Action == UiAction.Confirm)
            {
                if (tab == 0) Buy(offers[index]);
                else Exchange(exchanges[index]);
            }
        }

        public void Draw()
        {
            UiPointer.BeginSurface(this);
            UiPointer.Region(new Rectangle(36, 62, 72, 21), null, () => SelectTab(0));
            UiPointer.Region(new Rectangle(116, 62, 96, 21), null, () => SelectTab(1));
            UiPointer.ScrollRegion(new Rectangle(36, 92, 232, 205), delta => UiSounds.Select(ref index, viewport.Scroll(-delta, tab == 0 ? offers.Count : exchanges.Count, 5, index)));
            outerFrame.Draw();
            UiTheme.TextLine(UiTheme.FitText(title.ToUpperInvariant(), 170, false), new Vector2(38, 31), UiTheme.Text, false);
            UiTheme.Tab("Buy", new Rectangle(36, 62, 72, 21), tab == 0);
            UiTheme.Tab("Exchange", new Rectangle(116, 62, 96, 21), tab == 1);
            DrawWallet();
            if (tab == 0) DrawOffers(); else DrawExchanges();
            UiTheme.CommandBar(UiTheme.FooterRow(outerFrame.Bounds),
                new UiCommand(UiInputHints.Key(UiAction.Left) + "/" + UiInputHints.Key(UiAction.Right), "CATEGORY"),
                UiInputHints.Command(UiAction.Confirm, "TRADE"),
                UiInputHints.Command(UiAction.Cancel, "CLOSE"));
        }

        private void SelectTab(int next)
        { if (tab == next) return; tab = next; index = 0; viewport.Reset(); Move(); }

        private void DrawWallet()
        {
            const int startX = 222;
            const int width = 214;
            if (currencies.Count == 0) return;
            int rows = currencies.Count > 6 ? 2 : 1;
            int columns = (currencies.Count + rows - 1) / rows;
            int slotWidth = Math.Max(1, width / Math.Max(1, columns));
            int iconSize = slotWidth < 28
                ? Math.Max(4, Math.Min(10, slotWidth - 2))
                : 14;
            for (int i = 0; i < currencies.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                DrawWalletEntry(
                    currencies[i],
                    startX + column * slotWidth,
                    48 + row * 19,
                    slotWidth - 2,
                    iconSize);
            }
        }

        private static void DrawWalletEntry(
            UiCurrencyDefinition item,
            int x,
            int y,
            int width,
            int iconSize)
        {
            MerchantProvider.DrawIcon(item, new Rectangle(x, y, iconSize, iconSize));
            string count = UiTheme.FitText(
                MerchantProvider.Balance(item).ToString(),
                Math.Max(6, width - iconSize - 2),
                true);
            UiTheme.TextLine(count, new Vector2(x + iconSize + 2, y + 1), UiTheme.Text, true);
        }

        private void DrawOffers()
        {
            if (offers.Count == 0)
            {
                DrawEmpty("NO ITEMS AVAILABLE");
                return;
            }
            int first = viewport.FirstVisible(offers.Count, 5, index);
            int last = Math.Min(offers.Count, first + 5);
            for (int i = first; i < last; i++)
            {
                ShopOffer offer = offers[i];
                int y = 92 + (i - first) * 41;
                Rectangle row = new Rectangle(36, y, 232, 35);
                int rowIndex = i;
                UiPointer.ActionRegion(row, UiAction.Confirm, () => UiSounds.Select(ref index, rowIndex));
                bool selected = i == index;
                UiTheme.Panel(row, selected ? new Color(31, 35, 32) : UiTheme.PanelFill, selected ? UiTheme.Gold : UiTheme.Border);
                DrawShopIcon(offer, new Rectangle(row.X + 7, row.Y + 6, 22, 22));
                UiTheme.TextLine(UiTheme.FitText(offer.Definition.Name, 118, true), new Vector2(row.X + 36, row.Y + 5), UiTheme.Text, true);
                DrawPrice(offer.Currency, offer.Price, row.X + 159, row.Y + 10);
            }
            DrawScroll(first, last, offers.Count);
            DrawOfferDetails(offers[index]);
        }

        private void DrawOfferDetails(ShopOffer offer)
        {
            detailFrame.Draw();
            UiTheme.TextLine(UiTheme.FitText(offer.Definition.Name.ToUpperInvariant(), 136, true), new Vector2(293, 107), UiTheme.Text, true);
            UiTheme.WrappedText(offer.Definition.Description, new Rectangle(293, 136, 136, 62), UiTheme.Muted);
            UiTheme.TextLine("PRICE", new Vector2(293, 216), UiTheme.Muted, true);
            DrawPrice(offer.Currency, offer.Price, 338, 213);
            bool owned = MerchantProvider.IsOwned(offer.Definition);
            bool allowed = MerchantProvider.CanPurchase(offer.Definition);
            bool affordable = MerchantProvider.Balance(offer.Currency) >= offer.Price;
            string action = owned ? "OWNED" : !allowed ? "UNAVAILABLE" : affordable ? "CONFIRM TO BUY" : "NOT ENOUGH";
            UiTheme.TextLine(UiTheme.FitText(action, 136, true), new Vector2(293, 250), owned ? UiTheme.Muted : affordable && allowed ? UiTheme.Gold : UiTheme.Red, true);
            DrawFeedback();
        }

        private void DrawExchanges()
        {
            if (exchanges.Count == 0)
            {
                DrawEmpty("NO EXCHANGES AVAILABLE");
                return;
            }
            int first = viewport.FirstVisible(exchanges.Count, 5, index);
            int last = Math.Min(exchanges.Count, first + 5);
            for (int i = first; i < last; i++)
            {
                ExchangeOffer offer = exchanges[i];
                int y = 92 + (i - first) * 41;
                Rectangle row = new Rectangle(36, y, 232, 35);
                int rowIndex = i;
                UiPointer.ActionRegion(row, UiAction.Confirm, () => UiSounds.Select(ref index, rowIndex));
                bool selected = i == index;
                UiTheme.Panel(row, selected ? new Color(31, 35, 32) : UiTheme.PanelFill, selected ? UiTheme.Gold : UiTheme.Border);
                MerchantProvider.DrawIcon(offer.From, new Rectangle(row.X + 8, row.Y + 8, 18, 18));
                UiTheme.TextLine(offer.FromAmount.ToString(), new Vector2(row.X + 31, row.Y + 9), UiTheme.Text, true);
                UiTheme.TextLine(">", new Vector2(row.X + 103, row.Y + 9), selected ? UiTheme.Gold : UiTheme.Muted, true);
                MerchantProvider.DrawIcon(offer.To, new Rectangle(row.X + 130, row.Y + 8, 18, 18));
                UiTheme.TextLine(offer.ToAmount.ToString(), new Vector2(row.X + 153, row.Y + 9), UiTheme.Text, true);
            }
            DrawScroll(first, last, exchanges.Count);
            DrawExchangeDetails(exchanges[index]);
        }

        private void DrawExchangeDetails(ExchangeOffer offer)
        {
            detailFrame.Draw();
            UiTheme.TextLine(UiTheme.FitText("CURRENCY EXCHANGE", 136, true), new Vector2(293, 107), UiTheme.Text, true);
            UiTheme.WrappedText(
                "Trade " + offer.FromAmount + " " + offer.From.GetName(offer.FromAmount)
                + " for " + offer.ToAmount + " " + offer.To.GetName(offer.ToAmount) + ".",
                new Rectangle(293, 137, 136, 70),
                UiTheme.Muted);
            bool affordable = MerchantProvider.Balance(offer.From) >= offer.FromAmount;
            UiTheme.TextLine(
                UiTheme.FitText(affordable ? "CONFIRM TO TRADE" : "NOT ENOUGH", 136, true),
                new Vector2(293, 250),
                affordable ? UiTheme.Gold : UiTheme.Red,
                true);
            DrawFeedback();
        }

        private void DrawEmpty(string message)
        {
            emptyFrame.Draw();
            UiTheme.TextLine(UiTheme.FitText(message, 380, true), new Vector2(49, 183), UiTheme.Muted, true);
        }

        private void Buy(ShopOffer offer)
        {
            if (MerchantProvider.IsOwned(offer.Definition)) { SetFeedback("Already owned", UiTheme.Muted); return; }
            if (!MerchantProvider.CanPurchase(offer.Definition)) { SetFeedback("Unavailable", UiTheme.Red); return; }
            int price = offer.Price;
            if (MerchantProvider.Balance(offer.Currency) < price) { SetFeedback("Not enough currency", UiTheme.Red); return; }
            if (!MerchantProvider.Spend(offer.Currency, price))
            {
                SetFeedback("Not enough currency", UiTheme.Red);
                return;
            }
            try
            {
                offer.Definition.Grant();
            }
            catch (Exception error)
            {
                try { offer.Definition.Revoke(); }
                catch (Exception rollbackError)
                {
                    Console.WriteLine("[JK Runtime UI] Merchant product rollback failed for "
                        + offer.Definition.Id + ": " + rollbackError.Message);
                }
                MerchantProvider.Grant(offer.Currency, price);
                Console.WriteLine("[JK Runtime UI] Merchant purchase failed for "
                    + offer.Definition.Id
                    + ": " + error.Message);
                SetFeedback("Purchase failed", UiTheme.Red);
                return;
            }
            SetFeedback("Purchased", UiTheme.Gold);
            UiSounds.Play(UiSound.Confirm);
        }

        private void Exchange(ExchangeOffer offer)
        {
            if (MerchantProvider.Balance(offer.From) < offer.FromAmount)
            {
                SetFeedback("Not enough currency", UiTheme.Red);
                return;
            }
            if (!MerchantProvider.Spend(offer.From, offer.FromAmount)) { SetFeedback("Not enough currency", UiTheme.Red); return; }
            if (!MerchantProvider.Grant(offer.To, offer.ToAmount))
            {
                MerchantProvider.Grant(offer.From, offer.FromAmount);
                SetFeedback("Exchange failed", UiTheme.Red);
                return;
            }
            SetFeedback("Exchange complete", UiTheme.Cyan);
            UiSounds.Play(UiSound.Confirm);
        }

        private void AddCurrency(UiCurrencyDefinition item)
        {
            if (item != null && !currencies.Contains(item)) currencies.Add(item);
        }

        private static int FirstVisible(int count, int selected, int visible)
        {
            return Math.Max(0, Math.Min(selected - visible / 2, count - visible));
        }

        private static void DrawScroll(int first, int last, int count)
        {
            if (first > 0) UiTheme.TextLine("^", new Vector2(263, 94), UiTheme.Cyan, true);
            if (last < count) UiTheme.TextLine("v", new Vector2(263, 282), UiTheme.Cyan, true);
        }

        private void SetFeedback(string text, Color color)
        {
            if (color == UiTheme.Red || color == UiTheme.Muted) UiSounds.Play(UiSound.Error);
            feedback = text;
            feedbackColor = color;
            feedbackTime = 1.8f;
        }

        private void DrawFeedback()
        {
            if (feedbackTime > 0f)
                UiTheme.TextLine(UiTheme.FitText(feedback, 136, true), new Vector2(293, 276), feedbackColor, true);
        }

        private static void DrawPrice(UiCurrencyDefinition currency, int price, int x, int y)
        {
            MerchantProvider.DrawIcon(currency, new Rectangle(x, y, 15, 15));
            UiTheme.TextLine(price == int.MaxValue ? "-" : price.ToString(), new Vector2(x + 19, y + 1), UiTheme.Text, true);
        }

        private static void DrawShopIcon(ShopOffer offer, Rectangle destination)
        {
            if (offer.Definition.DrawIcon != null)
            {
                try
                {
                    offer.Definition.DrawIcon(destination);
                    return;
                }
                catch (Exception error)
                {
                    Console.WriteLine("[JK Runtime UI] Merchant icon failed for "
                        + offer.Definition.Id + ": " + error.Message);
                }
            }
            Texture2D pixel = Game1.instance.contentManager.Pixel.texture;
            Color color = offer.Definition.Color;
            Game1.spriteBatch.Draw(pixel, new Rectangle(destination.X + 4, destination.Y + 4, destination.Width - 8, destination.Height - 8), new Color(color, 80));
            Game1.spriteBatch.Draw(pixel, new Rectangle(destination.X + 3, destination.Y + 4, destination.Width - 6, 2), color);
            Game1.spriteBatch.Draw(pixel, new Rectangle(destination.X + 3, destination.Bottom - 6, destination.Width - 6, 2), color);
            Game1.spriteBatch.Draw(pixel, new Rectangle(destination.X + 2, destination.Y + 3, 3, 6), color);
        }

        private static UiCurrencyDefinition ResolveCurrency(string id)
        {
            UiCurrencyDefinition result;
            if (!UIApi.TryGetCurrency(id, out result))
                throw new InvalidOperationException("Merchant currency is not registered: " + id);
            return result;
        }

        private static void Move() { UiSounds.Play(UiSound.Move); }
    }
}
