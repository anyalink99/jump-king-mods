using System;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing.Controller;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework;

namespace JKRuntime.UI
{
    public static class UIApi
    {
        public const string Version = RuntimeApi.Version;
        /// <summary>True while a shared text page owns keyboard input. Custom input consumers should defer shortcuts during this scope.</summary>
        public static bool IsTextInputActive { get { return TextInputCapture.Active; } }
        /// <summary>Own physical keyboard actions for a custom text editor. Game-thread only.
        /// Dispose on every close/failure path; nested leases coexist. Text/command reading
        /// remains the editor's responsibility. Bound actions drain through release.</summary>
        public static IDisposable AcquireTextInput() { return new TextInputCapture(); }
        private static readonly Dictionary<string, UiBindingDefinition> Bindings =
            new Dictionary<string, UiBindingDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> BindingOrder = new List<string>();
        private static readonly Dictionary<string, WorldInteraction> Interactions =
            new Dictionary<string, WorldInteraction>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> InteractionOrder = new List<string>();
        private static readonly Dictionary<string, UiInputActionDefinition> InputActions =
            new Dictionary<string, UiInputActionDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> InputActionOrder = new List<string>();
        private static readonly Dictionary<string, UiDebugActionDefinition> DebugActions =
            new Dictionary<string, UiDebugActionDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> DebugActionOrder = new List<string>();
        private static readonly Dictionary<string, MerchantOfferDefinition> MerchantOffers =
            new Dictionary<string, MerchantOfferDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> MerchantOfferOrder = new List<string>();
        private static readonly Dictionary<string, UiCurrencyDefinition> Currencies =
            new Dictionary<string, UiCurrencyDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> CurrencyOrder = new List<string>();
        private static readonly Dictionary<string, UiMainMenuItemDefinition> MainMenuItems =
            new Dictionary<string, UiMainMenuItemDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> MainMenuItemOrder = new List<string>();
        private static readonly Dictionary<string, UiPauseMenuItemDefinition> PauseMenuItems =
            new Dictionary<string, UiPauseMenuItemDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> PauseMenuItemOrder = new List<string>();
        private static readonly Dictionary<string, UiInventoryItemDefinition> InventoryItems =
            new Dictionary<string, UiInventoryItemDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> InventoryItemOrder = new List<string>();
        private static readonly Dictionary<string, object> RegistrationOwners =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static WorldInteraction[] interactionSnapshot = new WorldInteraction[0];
        private static readonly WorldInteraction[] NoInteractions = new WorldInteraction[0];
        private static WorldInteraction[] globalInteractionSnapshot = new WorldInteraction[0];
        private static readonly Dictionary<int, WorldInteraction[]> ScreenInteractionSnapshots =
            new Dictionary<int, WorldInteraction[]>();
        private static UiInputActionDefinition[] inputActionSnapshot = new UiInputActionDefinition[0];

        public static string VanillaCurrencyId(Items item) { return "jumpking.item." + (int)item; }

        public static bool Supports(string capability)
        {
            if (string.Equals(capability, "ui-feedback-v1", StringComparison.OrdinalIgnoreCase)) return true;
            if (capability == "window-cursor-v1") return true;
            if (capability == "text-entry-v1") return true;
            if (capability == "text-input-capture-v1") return true;
            if (string.Equals(capability, "binding-pages-v1", StringComparison.OrdinalIgnoreCase)) return true;
            if (capability == "page-tools-v1" || capability == "pointer-drag-v1") return true;
            return string.Equals(capability, "bindings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "binding-chords", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "physical-binding-resolution", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "input-routing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "world-interactions-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "mouse-buttons-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "pointer-navigation-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "modal-pages", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "page-stack-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "transparent-modal-pages", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "embedded-menu-pages", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "secondary-modal-action", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "action-button-hints", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "workshop-menu-items", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "main-menu-control", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "main-menu-return", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "root-main-menu-items", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "root-pause-menu-items", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "menu-items-v2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "menu-action-feedback", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "modal-input-policy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "pause-menu-control", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "vanilla-ui-theme", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "merchant-trading", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "inventory-items-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "inventory-equipment-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "compact-inventory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "compact-grid-pages-v1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "compact-workshop-grids", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "compact-mod-grid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "pinned-mod-settings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "mod-setting-labels", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "mod-toggle-bindings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(capability, "registration-scopes", StringComparison.OrdinalIgnoreCase);
        }

        public static int[][] ResolvePhysicalBinding(
            PadInstance pad,
            int[] runtimeButtons)
        {
            if (pad == null) throw new ArgumentNullException("pad");
            return ChordVirtualizer.ResolvePhysicalBinding(
                pad,
                runtimeButtons);
        }

        public static void RegisterCurrency(UiCurrencyDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!Currencies.ContainsKey(definition.Id)) CurrencyOrder.Add(definition.Id);
            Currencies[definition.Id] = definition;
            ClearRegistrationOwner("currency", definition.Id);
        }

        public static void UnregisterCurrency(string id)
        {
            Remove(Currencies, CurrencyOrder, "currency", id);
        }

        public static bool TryGetCurrency(string id, out UiCurrencyDefinition definition)
        {
            return Currencies.TryGetValue(id ?? string.Empty, out definition);
        }

        public static IList<UiCurrencyDefinition> GetCurrencies()
        {
            List<UiCurrencyDefinition> result = new List<UiCurrencyDefinition>();
            foreach (string id in CurrencyOrder)
                if (Currencies.ContainsKey(id)) result.Add(Currencies[id]);
            return result;
        }

        public static void RegisterBinding(UiBindingDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!Bindings.ContainsKey(definition.Id)) BindingOrder.Add(definition.Id);
            Bindings[definition.Id] = definition;
            ClearRegistrationOwner("binding", definition.Id);
        }

        public static void UnregisterBinding(string id)
        {
            Remove(Bindings, BindingOrder, "binding", id);
        }

        public static IList<UiBindingDefinition> GetBindings()
        {
            List<UiBindingDefinition> result = new List<UiBindingDefinition>();
            foreach (string id in BindingOrder)
                if (Bindings.ContainsKey(id)) result.Add(Bindings[id]);
            result.Sort(CompareBindings);
            return result;
        }

        public static void RegisterInteraction(WorldInteraction interaction)
        {
            if (interaction == null) throw new ArgumentNullException("interaction");
            if (!Interactions.ContainsKey(interaction.Id)) InteractionOrder.Add(interaction.Id);
            Interactions[interaction.Id] = interaction;
            ClearRegistrationOwner("interaction", interaction.Id);
            RebuildInteractionSnapshot();
        }

        public static void UnregisterInteraction(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                Remove(Interactions, InteractionOrder, "interaction", id);
                RebuildInteractionSnapshot();
            }
        }

        public static IList<WorldInteraction> GetInteractions()
        {
            return new List<WorldInteraction>(interactionSnapshot);
        }

        internal static WorldInteraction[] GetInteractionSnapshot()
        {
            return interactionSnapshot;
        }

        internal static WorldInteraction[] GetGlobalInteractionSnapshot()
        {
            return globalInteractionSnapshot;
        }

        internal static WorldInteraction[] GetScreenInteractionSnapshot(int screen)
        {
            WorldInteraction[] result;
            return ScreenInteractionSnapshots.TryGetValue(screen, out result)
                ? result
                : NoInteractions;
        }

        public static void RegisterInputAction(UiInputActionDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!InputActions.ContainsKey(definition.Id)) InputActionOrder.Add(definition.Id);
            InputActions[definition.Id] = definition;
            ClearRegistrationOwner("input", definition.Id);
            RebuildInputActionSnapshot();
        }

        public static void UnregisterInputAction(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            Remove(InputActions, InputActionOrder, "input", id);
            RebuildInputActionSnapshot();
        }

        public static IList<UiInputActionDefinition> GetInputActions()
        {
            return new List<UiInputActionDefinition>(inputActionSnapshot);
        }

        internal static UiInputActionDefinition[] GetInputActionSnapshot()
        {
            return inputActionSnapshot;
        }

        public static void RegisterDebugAction(UiDebugActionDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!DebugActions.ContainsKey(definition.Id)) DebugActionOrder.Add(definition.Id);
            DebugActions[definition.Id] = definition;
            ClearRegistrationOwner("debug", definition.Id);
        }

        public static void UnregisterDebugAction(string id)
        {
            Remove(DebugActions, DebugActionOrder, "debug", id);
        }

        public static IList<UiDebugActionDefinition> GetDebugActions()
        {
            List<UiDebugActionDefinition> result = new List<UiDebugActionDefinition>();
            foreach (string id in DebugActionOrder)
                if (DebugActions.ContainsKey(id)) result.Add(DebugActions[id]);
            return result;
        }

        public static void RegisterInventoryItem(UiInventoryItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!InventoryItems.ContainsKey(definition.Id)) InventoryItemOrder.Add(definition.Id);
            InventoryItems[definition.Id] = definition;
            ClearRegistrationOwner("inventory-item", definition.Id);
            InventoryMenuIntegration.RefreshDefinitions();
        }

        public static void UnregisterInventoryItem(string id)
        {
            Remove(InventoryItems, InventoryItemOrder, "inventory-item", id);
            InventoryMenuIntegration.RefreshDefinitions();
        }

        public static IList<UiInventoryItemDefinition> GetInventoryItems()
        {
            List<UiInventoryItemDefinition> result = new List<UiInventoryItemDefinition>();
            foreach (string id in InventoryItemOrder)
                if (InventoryItems.ContainsKey(id)) result.Add(InventoryItems[id]);
            return result;
        }

        public static void NotifyInventoryItemChanged(string id)
        {
            InventoryMenuIntegration.UpdateVisibility(id);
        }

        public static void RegisterMerchantOffer(MerchantOfferDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!MerchantOffers.ContainsKey(definition.Id)) MerchantOfferOrder.Add(definition.Id);
            MerchantOffers[definition.Id] = definition;
            ClearRegistrationOwner("offer", definition.Id);
        }

        public static void UnregisterMerchantOffer(string id)
        {
            Remove(MerchantOffers, MerchantOfferOrder, "offer", id);
        }

        public static IList<MerchantOfferDefinition> GetMerchantOffers()
        {
            List<MerchantOfferDefinition> result = new List<MerchantOfferDefinition>();
            foreach (string id in MerchantOfferOrder)
                if (MerchantOffers.ContainsKey(id)) result.Add(MerchantOffers[id]);
            return result;
        }

        public static void RegisterMerchant(MerchantDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            RegisterInteraction(WorldInteraction.Page(
                definition.Id,
                definition.InteractionLabel,
                definition.Priority,
                delegate
                {
                    return definition.IsAvailable();
                },
                delegate
                {
                    return new MerchantPage(
                        definition.Title,
                        definition.GetOffers(),
                        definition.GetExchanges());
                }));
        }

        public static void UnregisterMerchant(string id)
        {
            UnregisterInteraction(id);
        }

        public static void RegisterMainMenuItem(
            UiMainMenuItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!MainMenuItems.ContainsKey(definition.Id))
                MainMenuItemOrder.Add(definition.Id);
            MainMenuItems[definition.Id] = definition;
            ClearRegistrationOwner("main-menu-item", definition.Id);
            ModMenuIntegration.Refresh();
        }

        public static void UnregisterMainMenuItem(string id)
        {
            Remove(
                MainMenuItems,
                MainMenuItemOrder,
                "main-menu-item",
                id);
            ModMenuIntegration.Refresh();
        }

        public static IList<UiMainMenuItemDefinition> GetMainMenuItems()
        {
            List<UiMainMenuItemDefinition> result =
                new List<UiMainMenuItemDefinition>();
            foreach (string id in MainMenuItemOrder)
                if (MainMenuItems.ContainsKey(id)) result.Add(MainMenuItems[id]);
            result.Sort(
                delegate(UiMainMenuItemDefinition left, UiMainMenuItemDefinition right)
                {
                    int priority = right.Priority.CompareTo(left.Priority);
                    return priority != 0
                        ? priority
                        : MainMenuItemOrder.IndexOf(left.Id).CompareTo(
                            MainMenuItemOrder.IndexOf(right.Id));
                });
            return result;
        }

        public static void RegisterPauseMenuItem(
            UiPauseMenuItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (!PauseMenuItems.ContainsKey(definition.Id))
                PauseMenuItemOrder.Add(definition.Id);
            PauseMenuItems[definition.Id] = definition;
            ClearRegistrationOwner("pause-menu-item", definition.Id);
            ModMenuIntegration.Refresh();
        }

        public static void UnregisterPauseMenuItem(string id)
        {
            Remove(
                PauseMenuItems,
                PauseMenuItemOrder,
                "pause-menu-item",
                id);
            ModMenuIntegration.Refresh();
        }

        public static IList<UiPauseMenuItemDefinition> GetPauseMenuItems()
        {
            List<UiPauseMenuItemDefinition> result =
                new List<UiPauseMenuItemDefinition>();
            foreach (string id in PauseMenuItemOrder)
                if (PauseMenuItems.ContainsKey(id)) result.Add(PauseMenuItems[id]);
            result.Sort(
                delegate(UiPauseMenuItemDefinition left, UiPauseMenuItemDefinition right)
                {
                    int priority = right.Priority.CompareTo(left.Priority);
                    return priority != 0
                        ? priority
                        : PauseMenuItemOrder.IndexOf(left.Id).CompareTo(
                            PauseMenuItemOrder.IndexOf(right.Id));
                });
            return result;
        }

        public static bool Open(IUiPage page)
        {
            return Open(page, UiModalOptions.Default);
        }

        public static bool Open(IUiPage page, UiModalOptions options)
        {
            return ModalHost.Instance != null
                && ModalHost.Instance.Open(
                    page,
                    options ?? UiModalOptions.Default);
        }

        public static IBTnode CreateMenuPage(object factory, IUiPage page)
        {
            return new EmbeddedMenuPageNode(factory, page);
        }

        public static IBTnode CreateCompactGrid(
            object factory,
            IEnumerable<UiCompactGridItemDefinition> definitions)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            if (definitions == null) throw new ArgumentNullException("definitions");
            List<GridMenuItem> items = new List<GridMenuItem>();
            List<JumpKing.Util.IDrawable> pageDrawables =
                new List<JumpKing.Util.IDrawable>();
            HashSet<IBTnode> visited = new HashSet<IBTnode>();
            foreach (UiCompactGridItemDefinition definition in definitions)
            {
                if (definition == null) continue;
                CollectDrawableTree(
                    definition.Child,
                    visited,
                    pageDrawables);
                items.Add(new GridMenuItem(
                    definition.Label,
                    definition.Child,
                    GridSubtitle.ForText(definition.Subtitle),
                    definition.IsVisible));
            }
            SquareGridSelector grid = new SquareGridSelector(items);
            CompactGridDrawableHost host =
                new CompactGridDrawableHost(grid, pageDrawables);
            VanillaMenuAdapter.AddDrawable(
                factory,
                host);
            return host;
        }

        private static void CollectDrawableTree(
            IBTnode node,
            HashSet<IBTnode> visited,
            IList<JumpKing.Util.IDrawable> drawables)
        {
            if (node == null || !visited.Add(node)) return;
            JumpKing.Util.IDrawable drawable =
                node as JumpKing.Util.IDrawable;
            if (drawable != null) drawables.Add(drawable);
            foreach (IBTnode child in node.GetRelatedNodes() ?? new IBTnode[0])
                CollectDrawableTree(child, visited, drawables);
        }

        public static JumpKing.PauseMenu.BT.TextButton CreateFeedbackButton(
            string label,
            Func<UiMenuActionResult> execute)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Button label is required", "label");
            if (execute == null) throw new ArgumentNullException("execute");
            UiMenuFeedbackActionNode action =
                new UiMenuFeedbackActionNode(execute);
            return new UiFeedbackTextButton(label, action);
        }

        public static void ClosePauseMenu()
        {
            ModMenuIntegration.ClosePause();
        }

        public static bool ContinueFromMainMenu(object factory)
        {
            return VanillaMenuAdapter.ContinueFromMainMenu(factory);
        }

        public static bool ReturnToMainMenu()
        {
            return VanillaMenuAdapter.ReturnToMainMenu();
        }

        public static bool IsOpen { get { return ModalHost.Instance != null && ModalHost.Instance.IsOpen; } }

        public static int ModalDepth
        {
            get { return ModalHost.Instance == null ? 0 : ModalHost.Instance.Depth; }
        }

        public static IList<UiModSettingInfo> GetModSettings()
        {
            return ModSettingsCatalog.GetInfos();
        }

        public static void SetModSettingPinned(string id, bool pinned)
        {
            ModSettingsCatalog.SetPinned(id, pinned);
        }

        public static void SetModSettingBindable(string id, bool bindable)
        {
            ModSettingsCatalog.SetBindable(id, bindable);
        }

        public static void RegisterModSettingLabel(string id, string label)
        {
            ModSettingsCatalog.RegisterLabel(id, label);
            ClearRegistrationOwner("setting-label", id);
        }

        public static void UnregisterModSettingLabel(string id)
        {
            ModSettingsCatalog.UnregisterLabel(id);
            RegistrationOwners.Remove(OwnerKey("setting-label", id));
        }

        public static bool CompactWorkshopGridsEnabled
        {
            get
            {
                SettingsStore.EnsureLoaded();
                return SettingsStore.Current.UseCompactWorkshopGrids;
            }
        }

        [Obsolete("Use CompactWorkshopGridsEnabled instead")]
        public static bool CompactModGridEnabled
        {
            get { return CompactWorkshopGridsEnabled; }
        }

        private static void RebuildInteractionSnapshot()
        {
            List<WorldInteraction> result = new List<WorldInteraction>();
            foreach (string id in InteractionOrder)
                if (Interactions.ContainsKey(id)) result.Add(Interactions[id]);
            interactionSnapshot = result.ToArray();
            List<WorldInteraction> global = new List<WorldInteraction>();
            Dictionary<int, List<WorldInteraction>> screens =
                new Dictionary<int, List<WorldInteraction>>();
            foreach (WorldInteraction interaction in interactionSnapshot)
            {
                if (interaction.Screen <= 0)
                {
                    global.Add(interaction);
                    continue;
                }
                List<WorldInteraction> values;
                if (!screens.TryGetValue(interaction.Screen, out values))
                {
                    values = new List<WorldInteraction>();
                    screens[interaction.Screen] = values;
                }
                values.Add(interaction);
            }
            globalInteractionSnapshot = global.ToArray();
            ScreenInteractionSnapshots.Clear();
            foreach (KeyValuePair<int, List<WorldInteraction>> pair in screens)
                ScreenInteractionSnapshots[pair.Key] = pair.Value.ToArray();
        }

        private static void RebuildInputActionSnapshot()
        {
            List<UiInputActionDefinition> result = new List<UiInputActionDefinition>();
            foreach (string id in InputActionOrder)
                if (InputActions.ContainsKey(id)) result.Add(InputActions[id]);
            inputActionSnapshot = result.ToArray();
        }

        private static void Remove<T>(
            Dictionary<string, T> values,
            List<string> order,
            string category,
            string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            values.Remove(id);
            RegistrationOwners.Remove(OwnerKey(category, id));
            order.RemoveAll(
                delegate(string value)
                {
                    return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
                });
        }

        internal static object SetRegistrationOwner(string category, string id, string owner)
        {
            object token = new object();
            RegistrationOwners[OwnerKey(category, id)] = token;
            return token;
        }

        internal static bool IsRegistrationOwner(string category, string id, object token)
        {
            object current;
            return RegistrationOwners.TryGetValue(OwnerKey(category, id), out current)
                && ReferenceEquals(current, token);
        }

        private static void ClearRegistrationOwner(string category, string id)
        {
            RegistrationOwners.Remove(OwnerKey(category, id));
        }

        private static string OwnerKey(string category, string id)
        {
            return category + "|" + id;
        }

        private static int CompareBindings(UiBindingDefinition left, UiBindingDefinition right)
        {
            int group = GroupOrder(left.Group).CompareTo(GroupOrder(right.Group));
            if (group != 0) return group;
            group = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
            if (group != 0) return group;
            return BindingOrder.IndexOf(left.Id).CompareTo(BindingOrder.IndexOf(right.Id));
        }

        private static int GroupOrder(string group)
        {
            if (string.Equals(group, "Jump King", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(group, "JK Runtime", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }
    }
}
