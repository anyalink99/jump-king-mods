using System.Collections.Generic;
using JKRuntime.Modules;
using Microsoft.Xna.Framework;
using JKRuntime.UI;

namespace JKRuntime.UIExample
{
    [RuntimeModule("example.ui", "JK Runtime UI Example")]
    public static class ExampleEntry
    {
        private const string CurrencyId = "example.tokens";
        private static WorldInteractionPoint merchantPoint, panelPoint;
        private static UiChord[] panelBindings = { new UiChord(40, 88) };
        private static int tokens = 5;

        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainBinds(object factory, JumpKing.PauseMenu.GuiFormat format)
        {
            return new JumpKing.PauseMenu.BT.TextButton("Binds", UIApi.CreateMenuPage(factory,
                new UiBindingsPage("Binds", "Open the example page.", "example.open-panel")));
        }
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseBinds(object factory, JumpKing.PauseMenu.GuiFormat format)
        { return MainBinds(factory, format); }

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            UIApi.RegisterCurrency(new UiCurrencyDefinition(
                CurrencyId,
                "Token",
                "Tokens",
                1,
                delegate { return tokens; },
                delegate(int amount)
                {
                    if (amount <= 0 || tokens < amount) return false;
                    tokens -= amount;
                    return true;
                },
                delegate(int amount) { if (amount > 0) tokens += amount; },
                delegate(Rectangle bounds)
                {
                    JumpKing.Game1.spriteBatch.Draw(
                        JumpKing.Game1.instance.contentManager.Pixel.texture,
                        bounds,
                        new Color(244, 194, 58));
                }));
            UIApi.RegisterBinding(new UiBindingDefinition(
                "example.open-panel",
                "JK Runtime UI Example",
                "Open example page",
                GetPanelBindings,
                SetPanelBindings,
                delegate { panelBindings = new[] { new UiChord(40, 88) }; }));
            UIApi.RegisterInputAction(UiInputActionDefinition.FromChords(
                "example.open-panel",
                "Open example page",
                160,
                GetPanelBindings,
                delegate { return !UIApi.IsOpen; },
                delegate { UIApi.Open(new ExamplePage()); }));
        }

        [BeforeAttempt]
        public static void PrepareAttempt(RuntimeScope scope)
        {
            scope.Defer(delegate { merchantPoint = null; panelPoint = null; });
            IList<WorldInteractionPoint> points = WorldInteractionMap.LoadLevel();
            merchantPoint = WorldInteractionMap.Require(points, "example.merchant");
            panelPoint = WorldInteractionMap.Require(points, "example.toggle-door");
        }

        [OnLevelStart]
        public static void OnLevelStart(ModuleContext context)
        {
            WorldInteractionPoint merchant = merchantPoint;
            WorldInteractionPoint panel = panelPoint;
            var levelScope = context.Track(new UiRegistrationScope("example.level"));
            levelScope.RegisterMerchant(new MerchantDefinition(
                "example.merchant",
                "Example Merchant",
                "Trade",
                180,
                merchant.IsPlayerInside,
                CreateOffers));
            levelScope.RegisterInteraction(WorldInteraction.Page(
                "example.panel",
                "Inspect",
                170,
                panel.IsPlayerInside,
                delegate { return new ExamplePage(); }));
        }

        private static IList<MerchantOfferDefinition> CreateOffers()
        {
            return new List<MerchantOfferDefinition>
            {
                new MerchantOfferDefinition(
                    "example.keepsake",
                    "Keepsake",
                    "A sample purchase for two Tokens.",
                    CurrencyId,
                    2,
                    new Color(66, 220, 255),
                    delegate { },
                    delegate { return true; })
            };
        }

        private static UiChord[] GetPanelBindings()
        {
            return CloneChords(panelBindings);
        }

        private static void SetPanelBindings(UiChord[] chords)
        {
            panelBindings = CloneChords(chords);
        }

        private static UiChord[] CloneChords(UiChord[] chords)
        {
            List<UiChord> result = new List<UiChord>();
            foreach (UiChord chord in chords ?? new UiChord[0])
                if (chord != null && !chord.IsEmpty) result.Add(new UiChord(chord.Buttons));
            return result.ToArray();
        }
    }

    internal sealed class ExamplePage : ScopedUiPage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(74, 54, 332, 252));
        private readonly UiPageCommand back;
        internal ExamplePage() { back = new UiPageCommand(UiAction.Cancel, "Back", () => WantsClose = true); }

        public override void Update(UiInput input, float delta)
        {
            back.Handle(input);
        }

        public override void Draw()
        {
            frame.Draw();
            UiTheme.TextLine("EXAMPLE PAGE", new Vector2(94, 76), UiTheme.Text, false);
            UiTheme.Tab("Details", new Rectangle(94, 111, 92, 22), true);
            UiTheme.WrappedText(
                "Custom pages use Jump King's own frame and the shared UIApi+ layout helpers.",
                new Rectangle(94, 151, 292, 76),
                UiTheme.Muted);
            new UiPageLayout(frame.Bounds, new[] { back }, 0).DrawCommands();
        }
    }
}
