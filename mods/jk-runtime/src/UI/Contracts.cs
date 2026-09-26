using System;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing.MiscEntities.WorldItems;
using LanguageJK;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    public sealed class UiChord
    {
        private readonly int[] buttons;

        public UiChord(params int[] values)
        {
            List<int> normalized = new List<int>();
            foreach (int value in values ?? new int[0])
            {
                if (value < 0 || normalized.Contains(value)) continue;
                normalized.Add(value);
            }
            if (normalized.Count > 2)
                throw new ArgumentException("A binding chord supports at most two buttons", "values");
            buttons = normalized.ToArray();
        }

        public int[] Buttons { get { return (int[])buttons.Clone(); } }
        public bool IsEmpty { get { return buttons.Length == 0; } }

        internal bool IsHeld(int[] pressed)
        {
            if (buttons.Length == 0) return false;
            foreach (int button in buttons)
                if (Array.IndexOf(pressed, button) < 0) return false;
            return true;
        }

        public static UiChord[] FromAlternatives(int[] values)
        {
            List<UiChord> result = new List<UiChord>();
            foreach (int value in values ?? new int[0])
                if (value >= 0) result.Add(new UiChord(value));
            return result.ToArray();
        }

        public static int[] ToAlternatives(UiChord[] chords)
        {
            List<int> result = new List<int>();
            foreach (UiChord chord in chords ?? new UiChord[0])
            {
                int[] values = chord == null ? new int[0] : chord.buttons;
                if (values.Length > 0 && !result.Contains(values[0])) result.Add(values[0]);
            }
            return result.ToArray();
        }
    }

    public sealed class UiCurrencyDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string PluralName { get; private set; }
        public int ExchangeUnit { get; private set; }
        public Func<int> GetBalance { get; private set; }
        public Func<int, bool> Spend { get; private set; }
        public Action<int> Grant { get; private set; }
        public Action<Rectangle> DrawIcon { get; private set; }

        public UiCurrencyDefinition(
            string id,
            string name,
            string pluralName,
            int exchangeUnit,
            Func<int> getBalance,
            Func<int, bool> spend,
            Action<int> grant,
            Action<Rectangle> drawIcon)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Currency id is required", "id");
            if (getBalance == null) throw new ArgumentNullException("getBalance");
            if (spend == null) throw new ArgumentNullException("spend");
            if (grant == null) throw new ArgumentNullException("grant");
            if (drawIcon == null) throw new ArgumentNullException("drawIcon");
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            PluralName = string.IsNullOrWhiteSpace(pluralName) ? Name + "s" : pluralName;
            ExchangeUnit = Math.Max(1, exchangeUnit);
            GetBalance = getBalance;
            Spend = spend;
            Grant = grant;
            DrawIcon = drawIcon;
        }

        public string GetName(int count) { return count == 1 ? Name : PluralName; }
    }

    public sealed class UiBindingDefinition
    {
        public string Id { get; private set; }
        public string Group { get; private set; }
        public string Label { get; private set; }
        public Func<int[]> GetBindings { get; private set; }
        public Action<int[]> SetBindings { get; private set; }
        public Func<UiChord[]> GetChords { get; private set; }
        public Action<UiChord[]> SetChords { get; private set; }
        public bool SupportsChords { get; private set; }
        public Action Reset { get; private set; }

        public UiBindingDefinition(
            string id,
            string group,
            string label,
            Func<int[]> getBindings,
            Action<int[]> setBindings,
            Action reset)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Binding id is required", "id");
            if (getBindings == null) throw new ArgumentNullException("getBindings");
            if (setBindings == null) throw new ArgumentNullException("setBindings");
            Id = id;
            Group = string.IsNullOrWhiteSpace(group) ? "Mods" : group;
            Label = string.IsNullOrWhiteSpace(label) ? id : label;
            GetBindings = getBindings;
            SetBindings = setBindings;
            GetChords = delegate { return UiChord.FromAlternatives(getBindings()); };
            SetChords = delegate(UiChord[] chords) { setBindings(UiChord.ToAlternatives(chords)); };
            SupportsChords = false;
            Reset = reset ?? delegate { };
        }

        public UiBindingDefinition(
            string id,
            string group,
            string label,
            Func<UiChord[]> getChords,
            Action<UiChord[]> setChords,
            Action reset)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Binding id is required", "id");
            if (getChords == null) throw new ArgumentNullException("getChords");
            if (setChords == null) throw new ArgumentNullException("setChords");
            Id = id;
            Group = string.IsNullOrWhiteSpace(group) ? "Mods" : group;
            Label = string.IsNullOrWhiteSpace(label) ? id : label;
            GetChords = getChords;
            SetChords = setChords;
            GetBindings = delegate { return UiChord.ToAlternatives(getChords()); };
            SetBindings = delegate(int[] values) { setChords(UiChord.FromAlternatives(values)); };
            SupportsChords = true;
            Reset = reset ?? delegate { };
        }
    }

    public sealed class WorldInteraction
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public int Priority { get; private set; }
        public Func<bool> IsAvailable { get; private set; }
        public Action Activate { get; private set; }
        public int Screen { get; private set; }

        public WorldInteraction(string id, string label, int priority, Func<bool> isAvailable, Action activate)
            : this(id, label, priority, 0, isAvailable, activate)
        {
        }

        public WorldInteraction(
            string id,
            string label,
            int priority,
            int screen,
            Func<bool> isAvailable,
            Action activate)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Interaction id is required", "id");
            if (isAvailable == null) throw new ArgumentNullException("isAvailable");
            if (activate == null) throw new ArgumentNullException("activate");
            Id = id;
            Label = string.IsNullOrWhiteSpace(label) ? "Interact" : label;
            Priority = priority;
            Screen = Math.Max(0, screen);
            IsAvailable = isAvailable;
            Activate = activate;
        }

        public static WorldInteraction ScreenAction(
            string id,
            string label,
            int priority,
            int screen,
            Func<bool> isAvailable,
            Action activate)
        {
            if (screen < 1) throw new ArgumentOutOfRangeException("screen");
            return new WorldInteraction(id, label, priority, screen, isAvailable, activate);
        }

        public static WorldInteraction ScreenPage(
            string id,
            string label,
            int priority,
            int screen,
            Func<bool> isAvailable,
            Func<IUiPage> createPage)
        {
            if (createPage == null) throw new ArgumentNullException("createPage");
            return ScreenAction(
                id,
                label,
                priority,
                screen,
                isAvailable,
                delegate
                {
                    IUiPage page = createPage();
                    if (page != null) UIApi.Open(page);
                });
        }

        public static WorldInteraction Action(
            string id,
            string label,
            int priority,
            Func<bool> isAvailable,
            Action activate)
        {
            return new WorldInteraction(id, label, priority, isAvailable, activate);
        }

        public static WorldInteraction Page(
            string id,
            string label,
            int priority,
            Func<bool> isAvailable,
            Func<IUiPage> createPage)
        {
            if (createPage == null) throw new ArgumentNullException("createPage");
            return new WorldInteraction(
                id,
                label,
                priority,
                isAvailable,
                delegate
                {
                    IUiPage page = createPage();
                    if (page != null) UIApi.Open(page);
                });
        }
    }

    public sealed class UiInputActionDefinition
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public int Priority { get; private set; }
        public Func<int[]> GetBindings { get; private set; }
        public Func<UiChord[]> GetChords { get; private set; }
        public Func<bool> IsAvailable { get; private set; }
        public Action Execute { get; private set; }


        public UiInputActionDefinition(
            string id,
            string label,
            int priority,
            Func<int[]> getBindings,
            Func<bool> isAvailable,
            Action execute)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Input action id is required", "id");
            if (getBindings == null) throw new ArgumentNullException("getBindings");
            if (isAvailable == null) throw new ArgumentNullException("isAvailable");
            if (execute == null) throw new ArgumentNullException("execute");
            Id = id;
            Label = string.IsNullOrWhiteSpace(label) ? id : label;
            Priority = priority;
            GetBindings = getBindings;
            GetChords = delegate { return UiChord.FromAlternatives(getBindings()); };
            IsAvailable = isAvailable;
            Execute = execute;
        }

        private UiInputActionDefinition(
            string id,
            string label,
            int priority,
            Func<bool> isAvailable,
            Action execute,
            Func<UiChord[]> getChords)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Input action id is required", "id");
            if (getChords == null) throw new ArgumentNullException("getChords");
            if (isAvailable == null) throw new ArgumentNullException("isAvailable");
            if (execute == null) throw new ArgumentNullException("execute");
            Id = id;
            Label = string.IsNullOrWhiteSpace(label) ? id : label;
            Priority = priority;
            GetChords = getChords;
            GetBindings = delegate { return UiChord.ToAlternatives(getChords()); };
            IsAvailable = isAvailable;
            Execute = execute;
        }

        public static UiInputActionDefinition FromChords(
            string id,
            string label,
            int priority,
            Func<UiChord[]> getChords,
            Func<bool> isAvailable,
            Action execute)
        {
            return new UiInputActionDefinition(
                id,
                label,
                priority,
                isAvailable,
                execute,
                getChords);
        }
    }

    public sealed class UiDebugActionDefinition
    {
        // Optional value actions retain the original constructor for binary
        // compatibility with existing debug registrations.
        public Func<string> GetLabel { get; private set; }
        public Action<int> Adjust { get; private set; }
        public string ConfirmLabel { get; private set; }
        public string Id { get; private set; }
        public string Group { get; private set; }
        public string Label { get; private set; }
        public Func<bool> IsAvailable { get; private set; }
        public Action Execute { get; private set; }

        public UiDebugActionDefinition(
            string id,
            string group,
            string label,
            Action execute,
            Func<bool> isAvailable = null)
            : this(id, group, label, execute, isAvailable, null, null, "RUN") { }

        public UiDebugActionDefinition(string id, string group, string label, Action execute,
            Func<bool> isAvailable, Func<string> getLabel, Action<int> adjust, string confirmLabel)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Debug action id is required", "id");
            if (execute == null) throw new ArgumentNullException("execute");
            Id = id;
            Group = string.IsNullOrWhiteSpace(group) ? "Mods" : group;
            Label = string.IsNullOrWhiteSpace(label) ? id : label;
            Execute = execute;
            IsAvailable = isAvailable ?? delegate { return true; };
            GetLabel = getLabel ?? delegate { return Label; };
            Adjust = adjust;
            ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "RUN" : confirmLabel;
        }
    }

    public sealed class UiInventoryItemDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string PluralName { get; private set; }
        public string Description { get; private set; }
        public Color Color { get; private set; }
        public Func<int> GetCount { get; private set; }
        public Func<bool> IsVisible { get; private set; }
        public Func<bool> CanActivate { get; private set; }
        public Func<string> GetActionLabel { get; private set; }
        public Func<bool> Activate { get; private set; }
        public Func<bool> IsEquipped { get; private set; }
        public Func<bool, bool> SetEquipped { get; private set; }
        public bool IsEquipment { get { return SetEquipped != null; } }

        public UiInventoryItemDefinition(
            string id,
            string name,
            string description,
            Color color,
            Func<int> getCount,
            Func<bool> isVisible = null,
            Func<bool> canActivate = null,
            Func<string> getActionLabel = null,
            Func<bool> activate = null,
            string pluralName = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Inventory item id is required", "id");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Inventory item name is required", "name");
            if (getCount == null) throw new ArgumentNullException("getCount");
            Id = id.Trim();
            Name = name;
            PluralName = string.IsNullOrWhiteSpace(pluralName) ? name + "s" : pluralName;
            Description = description ?? string.Empty;
            Color = color;
            GetCount = getCount;
            IsVisible = isVisible ?? delegate { return getCount() > 0; };
            CanActivate = canActivate ?? delegate { return activate != null; };
            GetActionLabel = getActionLabel ?? delegate { return "Use"; };
            Activate = activate;
        }

        public static UiInventoryItemDefinition Equipment(
            string id,
            string name,
            string description,
            Color color,
            Func<int> getCount,
            Func<bool> isEquipped,
            Func<bool, bool> setEquipped,
            Func<bool> isVisible = null,
            Func<bool> canEquip = null,
            string pluralName = null)
        {
            if (isEquipped == null) throw new ArgumentNullException("isEquipped");
            if (setEquipped == null) throw new ArgumentNullException("setEquipped");
            UiInventoryItemDefinition definition = new UiInventoryItemDefinition(
                id,
                name,
                description,
                color,
                getCount,
                isVisible,
                canEquip,
                delegate { return language.TOGGLEITEM_EQUIP; },
                null,
                pluralName);
            definition.IsEquipped = isEquipped;
            definition.SetEquipped = setEquipped;
            return definition;
        }
    }

    public sealed class UiCompactGridItemDefinition
    {
        public string Label { get; private set; }
        public string Subtitle { get; private set; }
        public IBTnode Child { get; private set; }
        public Func<bool> IsVisible { get; private set; }

        public UiCompactGridItemDefinition(
            string label,
            IBTnode child,
            string subtitle = null,
            Func<bool> isVisible = null)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Grid item label is required", "label");
            if (child == null) throw new ArgumentNullException("child");
            Label = label;
            Child = child;
            Subtitle = subtitle ?? string.Empty;
            IsVisible = isVisible ?? delegate { return true; };
        }
    }

    public sealed class MerchantOfferDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Items Currency { get; private set; }
        public string CurrencyId { get; private set; }
        public int Price { get { return Math.Max(1, GetPrice()); } }
        public Color Color { get; private set; }
        public Func<bool> CanPurchase { get; private set; }
        public Func<bool> IsOwned { get; private set; }
        public Func<int> GetPrice { get; private set; }
        public Action Grant { get; private set; }
        public Action Revoke { get; private set; }
        public Action<Rectangle> DrawIcon { get; private set; }

        public MerchantOfferDefinition(
            string id,
            string name,
            string description,
            Items currency,
            int price,
            Color color,
            Action grant,
            Func<bool> canPurchase = null,
            Action<Rectangle> drawIcon = null)
            : this(id, name, description, UIApi.VanillaCurrencyId(currency), price, color, grant, canPurchase, drawIcon)
        {
            Currency = currency;
        }

        public MerchantOfferDefinition(
            string id,
            string name,
            string description,
            string currencyId,
            int price,
            Color color,
            Action grant,
            Func<bool> canPurchase = null,
            Action<Rectangle> drawIcon = null)
            : this(
                id,
                name,
                description,
                currencyId,
                FixedPrice(price),
                color,
                grant,
                canPurchase,
                null,
                drawIcon)
        {
        }

        public MerchantOfferDefinition(
            string id,
            string name,
            string description,
            string currencyId,
            Func<int> getPrice,
            Color color,
            Action grant,
            Func<bool> canPurchase,
            Func<bool> isOwned,
            Action<Rectangle> drawIcon,
            Action revoke = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Offer id is required", "id");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Offer name is required", "name");
            if (getPrice == null) throw new ArgumentNullException("getPrice");
            if (grant == null) throw new ArgumentNullException("grant");
            if (string.IsNullOrWhiteSpace(currencyId)) throw new ArgumentException("Currency id is required", "currencyId");
            Id = id;
            Name = name;
            Description = description ?? string.Empty;
            Currency = Items.NULL;
            CurrencyId = currencyId;
            GetPrice = getPrice;
            Color = color;
            Grant = grant;
            Revoke = revoke ?? delegate { };
            CanPurchase = canPurchase ?? delegate { return true; };
            IsOwned = isOwned ?? delegate { return false; };
            DrawIcon = drawIcon;
        }

        private static Func<int> FixedPrice(int price)
        {
            if (price <= 0) throw new ArgumentOutOfRangeException("price");
            return delegate { return price; };
        }
    }

    public sealed class MerchantExchangeDefinition
    {
        public string FromCurrencyId { get; private set; }
        public int FromAmount { get; private set; }
        public string ToCurrencyId { get; private set; }
        public int ToAmount { get; private set; }

        public MerchantExchangeDefinition(string fromCurrencyId, int fromAmount, string toCurrencyId, int toAmount)
        {
            if (string.IsNullOrWhiteSpace(fromCurrencyId)) throw new ArgumentException("Source currency is required", "fromCurrencyId");
            if (string.IsNullOrWhiteSpace(toCurrencyId)) throw new ArgumentException("Target currency is required", "toCurrencyId");
            if (fromAmount <= 0) throw new ArgumentOutOfRangeException("fromAmount");
            if (toAmount <= 0) throw new ArgumentOutOfRangeException("toAmount");
            FromCurrencyId = fromCurrencyId;
            FromAmount = fromAmount;
            ToCurrencyId = toCurrencyId;
            ToAmount = toAmount;
        }
    }

    public sealed class MerchantDefinition
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string InteractionLabel { get; private set; }
        public int Priority { get; private set; }
        public Func<bool> IsAvailable { get; private set; }
        public Func<IList<MerchantOfferDefinition>> GetOffers { get; private set; }
        public Func<IList<MerchantExchangeDefinition>> GetExchanges { get; private set; }

        public MerchantDefinition(
            string id,
            string title,
            string interactionLabel,
            int priority,
            Func<bool> isAvailable,
            Func<IList<MerchantOfferDefinition>> getOffers,
            Func<IList<MerchantExchangeDefinition>> getExchanges = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Merchant id is required", "id");
            if (isAvailable == null) throw new ArgumentNullException("isAvailable");
            if (getOffers == null) throw new ArgumentNullException("getOffers");
            Id = id;
            Title = string.IsNullOrWhiteSpace(title) ? "Merchant" : title;
            InteractionLabel = string.IsNullOrWhiteSpace(interactionLabel) ? "Trade" : interactionLabel;
            Priority = priority;
            IsAvailable = isAvailable;
            GetOffers = getOffers;
            GetExchanges = getExchanges ?? delegate { return new List<MerchantExchangeDefinition>(); };
        }
    }

    public enum UiMainMenuPlacement
    {
        BeforeExtras,
        Workshop
    }

    public sealed class UiMenuContext
    {
        private readonly object factory;

        internal UiMenuContext(object value)
        {
            factory = value;
        }

        public bool ContinueGame()
        {
            return UIApi.ContinueFromMainMenu(factory);
        }

        public bool Open(IUiPage page)
        {
            return UIApi.Open(page);
        }
    }

    public sealed class UiMainMenuItemDefinition
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public UiMainMenuPlacement Placement { get; private set; }
        public int Priority { get; private set; }
        internal Func<object, IBTnode> CreateNode { get; private set; }

        public UiMainMenuItemDefinition(
            string id,
            string label,
            UiMainMenuPlacement placement,
            Func<object, IBTnode> createNode)
            : this(id, label, placement, 0, createNode)
        {
        }

        public UiMainMenuItemDefinition(
            string id,
            string label,
            UiMainMenuPlacement placement,
            int priority,
            Func<object, IBTnode> createNode)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Main menu item id is required", "id");
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Main menu item label is required", "label");
            if (createNode == null) throw new ArgumentNullException("createNode");
            Id = id;
            Label = label;
            Placement = placement;
            Priority = priority;
            CreateNode = createNode;
        }

        public static UiMainMenuItemDefinition Page(
            string id,
            string label,
            UiMainMenuPlacement placement,
            int priority,
            Func<UiMenuContext, IUiPage> createPage)
        {
            if (createPage == null) throw new ArgumentNullException("createPage");
            return new UiMainMenuItemDefinition(
                id,
                label,
                placement,
                priority,
                delegate(object factory)
                {
                    IUiPage page = createPage(new UiMenuContext(factory));
                    return page == null ? null : UIApi.CreateMenuPage(factory, page);
                });
        }
    }

    public enum UiPauseMenuPlacement
    {
        BeforeSaveAndExit
    }

    public sealed class UiMenuActionResult
    {
        internal Func<UiMenuActionResult> Poll;
        public bool Succeeded { get; private set; }
        public string FeedbackLabel { get; private set; }
        public float FeedbackDurationSeconds { get; private set; }

        private UiMenuActionResult(
            bool succeeded,
            string feedbackLabel,
            float feedbackDurationSeconds)
        {
            if (feedbackDurationSeconds < 0f
                || float.IsNaN(feedbackDurationSeconds)
                || float.IsInfinity(feedbackDurationSeconds))
                throw new ArgumentOutOfRangeException(
                    "feedbackDurationSeconds");
            if (feedbackDurationSeconds > 0f
                && string.IsNullOrWhiteSpace(feedbackLabel))
                throw new ArgumentException(
                    "Timed feedback requires a label",
                    "feedbackLabel");
            Succeeded = succeeded;
            FeedbackLabel = feedbackLabel ?? string.Empty;
            FeedbackDurationSeconds = feedbackDurationSeconds;
        }

        public static UiMenuActionResult Completed()
        {
            return new UiMenuActionResult(true, string.Empty, 0f);
        }

        public static UiMenuActionResult Completed(
            string feedbackLabel,
            float feedbackDurationSeconds)
        {
            return new UiMenuActionResult(
                true,
                feedbackLabel,
                feedbackDurationSeconds);
        }

        public static UiMenuActionResult Rejected()
        {
            return new UiMenuActionResult(false, string.Empty, 0f);
        }
        /// <summary>Shows a pending label until a memory-only game-thread poll returns a completed/rejected result; null means still pending.</summary>
        public static UiMenuActionResult Pending(string label, Func<UiMenuActionResult> poll)
        {
            if (string.IsNullOrWhiteSpace(label) || poll == null) throw new ArgumentException("Pending feedback requires a label and poll");
            return new UiMenuActionResult(true, label, 0f) { Poll = poll };
        }
        public static UiMenuActionResult Rejected(string label, float durationSeconds)
        { return new UiMenuActionResult(false, label, durationSeconds); }
    }

    public sealed class UiPauseMenuItemDefinition
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public UiPauseMenuPlacement Placement { get; private set; }
        public int Priority { get; private set; }
        internal Func<object, IBTnode> CreateNode { get; private set; }

        public UiPauseMenuItemDefinition(
            string id,
            string label,
            UiPauseMenuPlacement placement,
            Func<object, IBTnode> createNode)
            : this(id, label, placement, 0, createNode)
        {
        }

        public UiPauseMenuItemDefinition(
            string id,
            string label,
            UiPauseMenuPlacement placement,
            int priority,
            Func<object, IBTnode> createNode)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Pause menu item id is required", "id");
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Pause menu item label is required", "label");
            if (createNode == null) throw new ArgumentNullException("createNode");
            Id = id;
            Label = label;
            Placement = placement;
            Priority = priority;
            CreateNode = createNode;
        }

        public static UiPauseMenuItemDefinition Action(
            string id,
            string label,
            UiPauseMenuPlacement placement,
            int priority,
            Func<bool> execute)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            return new UiPauseMenuItemDefinition(
                id,
                label,
                placement,
                priority,
                delegate { return new UiMenuActionNode(execute); });
        }

        public static UiPauseMenuItemDefinition FeedbackAction(
            string id,
            string label,
            UiPauseMenuPlacement placement,
            int priority,
            Func<UiMenuActionResult> execute)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            return new UiPauseMenuItemDefinition(
                id,
                label,
                placement,
                priority,
                delegate { return new UiMenuFeedbackActionNode(execute); });
        }
    }

    public enum UiAction
    {
        None,
        Cancel,
        Confirm,
        Up,
        Down,
        Left,
        Right,
        Reset,
        Secondary
    }

    public struct UiInput
    {
        public UiAction Action;
        public bool Up;
        public bool Down;
        public bool Left;
        public bool Right;
        public bool Confirm;
        public bool Cancel;
        public bool Secondary;
    }

    public interface IUiPage
    {
        void OnOpen();
        void OnClose();
        void Update(UiInput input, float delta);
        void Draw();
        bool WantsClose { get; }
    }

    public interface IUiPageInputPolicy
    {
        bool HandlesCancel { get; }
    }

    public sealed class UiModalOptions
    {
        public static readonly UiModalOptions Default =
            new UiModalOptions(true);

        public bool DimBackground { get; private set; }

        public UiModalOptions(bool dimBackground)
        {
            DimBackground = dimBackground;
        }
    }
}
