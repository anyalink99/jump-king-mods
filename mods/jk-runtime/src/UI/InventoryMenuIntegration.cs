using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorTree;
using BehaviorTree.Util;
using JumpKing;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions.BindController;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using LanguageJK;

namespace JKRuntime.UI
{
    internal static class InventoryMenuIntegration
    {
        private static readonly Dictionary<string, EnableableMenuItem> Items =
            new Dictionary<string, EnableableMenuItem>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<IBTnode> InjectedNodes = new List<IBTnode>();
        private static readonly List<object> InjectedDrawables = new List<object>();
        private static MenuSelector installedInventory;
        private static object installedFactory;
        private static bool installed;

        internal static void Install()
        {
            installed = true;
            Rebuild();
        }

        internal static void Uninstall()
        {
            installed = false;
            RemoveInjected();
        }

        internal static void RefreshDefinitions()
        {
            if (installed) Rebuild();
        }

        internal static void UpdateVisibility()
        {
            foreach (KeyValuePair<string, EnableableMenuItem> pair in Items)
            {
                UiInventoryItemDefinition definition = Find(pair.Key);
                if (definition != null && SafeVisible(definition)) pair.Value.Enable();
                else pair.Value.Disable();
            }
        }

        internal static void UpdateVisibility(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                UpdateVisibility();
                return;
            }
            EnableableMenuItem item;
            if (!Items.TryGetValue(id, out item)) return;
            UiInventoryItemDefinition definition = Find(id);
            if (definition != null && SafeVisible(definition)) item.Enable();
            else item.Disable();
        }

        private static void Rebuild()
        {
            object pause;
            object factory;
            if (!VanillaMenuAdapter.TryGetPauseContext(out pause, out factory)) return;
            IList drawables = VanillaMenuAdapter.GetDrawables(factory);
            if (drawables == null) return;
            MenuSelector inventory = FindInventory(drawables);
            if (inventory == null) return;
            RemoveInjected();
            installedInventory = inventory;
            installedFactory = factory;

            GuiFormat itemFormat = VanillaMenuAdapter.GetFormat(inventory);
            itemFormat.anchor_bounds.Width -= 60;
            itemFormat.element_margin = 8;
            GuiFormat inspectFormat = itemFormat;
            inspectFormat.anchor_bounds.Width -= 60;
            List<IBTnode> nodes = new List<IBTnode>();
            foreach (UiInventoryItemDefinition definition in UIApi.GetInventoryItems())
            {
                MenuSelector inspect = new MenuSelector(inspectFormat);
                foreach (TextInfo line in TextInfo.CreateFittedInfo(
                    inspectFormat,
                    definition.Description,
                    Color.Gray,
                    Game1.instance.contentManager.font.MenuFont))
                    inspect.AddChild(line);
                inspect.Initialize();

                MenuSelector actions = new MenuSelector(itemFormat);
                actions.AddChild(new TextButton(language.MENUFACTORY_INSPECT, inspect));
                if (definition.IsEquipment)
                    actions.AddChild(new InventoryEquipmentToggle(definition));
                else if (definition.Activate != null)
                    actions.AddChild(new InventoryActionButton(definition, new ActivateInventoryItemNode(definition)));
                actions.Initialize();
                VanillaMenuAdapter.AddDrawable(factory, actions);
                VanillaMenuAdapter.AddDrawable(factory, inspect);
                InjectedDrawables.Add(actions);
                InjectedDrawables.Add(inspect);

                EnableableMenuItem item = EnableableMenuItem.CreateEnableableMenuItem(
                    inventory,
                    new RegisteredInventoryButton(
                        definition,
                        new BTsequencor(new WaitUntilNoMenuInput(), actions)));
                Items[definition.Id] = item;
                nodes.Add(item);
                InjectedNodes.Add(item);
            }
            InsertAtTop(inventory, nodes);
            AddInventoryCondition(pause);
            UpdateVisibility();
            CompactInventory.Attach(inventory);
        }

        private static UiInventoryItemDefinition Find(string id)
        {
            foreach (UiInventoryItemDefinition definition in UIApi.GetInventoryItems())
                if (string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase)) return definition;
            return null;
        }

        private static MenuSelector FindInventory(IList drawables)
        {
            foreach (object drawable in drawables)
            {
                MenuSelector selector = drawable as MenuSelector;
                if (selector != null && HasDirectInventoryItem(selector)) return selector;
            }
            return null;
        }

        private static void RemoveInjected()
        {
            CompactInventory.Detach();
            if (installedInventory != null && InjectedNodes.Count > 0)
            {
                List<IBTnode> keep = new List<IBTnode>();
                foreach (IBTnode child in VanillaMenuAdapter.ChildrenForEdit(installedInventory))
                    if (!InjectedNodes.Contains(child)) keep.Add(child);
                VanillaMenuAdapter.SetChildren(installedInventory, keep.ToArray());
            }
            if (installedFactory != null && InjectedDrawables.Count > 0)
            {
                IList drawables = VanillaMenuAdapter.GetDrawables(installedFactory);
                if (drawables != null)
                    foreach (object drawable in InjectedDrawables) drawables.Remove(drawable);
            }
            InjectedNodes.Clear();
            InjectedDrawables.Clear();
            Items.Clear();
            installedInventory = null;
            installedFactory = null;
        }

        private static void InsertAtTop(MenuSelector inventory, IList<IBTnode> items)
        {
            IBTnode[] original = VanillaMenuAdapter.ChildrenForEdit(inventory);
            IBTnode[] combined = new IBTnode[items.Count + original.Length];
            for (int i = 0; i < items.Count; i++) combined[i] = items[i];
            Array.Copy(original, 0, combined, items.Count, original.Length);
            VanillaMenuAdapter.SetChildren(inventory, combined);
        }

        private static bool HasDirectInventoryItem(MenuSelector selector)
        {
            foreach (IBTnode child in selector.Children)
                if (ContainsInventoryButton(child, 2)) return true;
            return false;
        }

        private static bool ContainsInventoryButton(IBTnode node, int remainingDepth)
        {
            if (node == null) return false;
            if (node is InventoryButton) return true;
            if (remainingDepth <= 0) return false;
            foreach (IBTnode child in node.GetRelatedNodes() ?? new IBTnode[0])
                if (ContainsInventoryButton(child, remainingDepth - 1)) return true;
            return false;
        }

        private static void AddInventoryCondition(object pause)
        {
            AddConditionRecursive(
                VanillaMenuAdapter.GetBehaviourTreeRoot(pause),
                new HashSet<IBTnode>());
        }

        private static void AddConditionRecursive(IBTnode node, HashSet<IBTnode> visited)
        {
            if (node == null || !visited.Add(node)) return;
            RunAllAnySuccess conditions = node as RunAllAnySuccess;
            if (conditions != null
                && ContainsTypeName(conditions, "BTOwnsItem", new HashSet<IBTnode>())
                && !ContainsTypeName(conditions, "HasRegisteredInventoryItemsNode", new HashSet<IBTnode>()))
                conditions.AddChild(new HasRegisteredInventoryItemsNode());
            foreach (IBTnode child in node.GetRelatedNodes() ?? new IBTnode[0])
                AddConditionRecursive(child, visited);
        }

        private static bool ContainsTypeName(IBTnode node, string name, HashSet<IBTnode> visited)
        {
            if (node == null || !visited.Add(node)) return false;
            if (node.GetType().Name == name) return true;
            foreach (IBTnode child in node.GetRelatedNodes() ?? new IBTnode[0])
                if (ContainsTypeName(child, name, visited)) return true;
            return false;
        }

        private static bool SafeVisible(UiInventoryItemDefinition definition)
        {
            try { return definition.IsVisible() && definition.GetCount() > 0; }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Inventory visibility failed for " + definition.Id + ": " + error.Message);
                return false;
            }
        }
    }

    internal sealed class RegisteredInventoryButton : IMenuPressable
    {
        private readonly UiInventoryItemDefinition definition;
        internal UiInventoryItemDefinition Definition { get { return definition; } }
        internal RegisteredInventoryButton(UiInventoryItemDefinition item, IBTnode child) : base(child)
        {
            definition = item;
        }
        public override void Draw(int x, int y, bool selected)
        {
            int count = Math.Max(0, definition.GetCount());
            string label = count > 1 ? count + " " + definition.PluralName : definition.Name;
            MenuItemHelper.Draw(x, y, label, definition.Color, Game1.instance.contentManager.font.MenuFontSmall);
        }
        public override Point GetSize()
        {
            return MenuItemHelper.GetSize("999 " + definition.PluralName, Game1.instance.contentManager.font.MenuFontSmall);
        }
    }

    internal sealed class InventoryActionButton : IMenuPressable
    {
        private readonly UiInventoryItemDefinition definition;
        internal InventoryActionButton(UiInventoryItemDefinition item, IBTnode child) : base(child)
        {
            definition = item;
        }
        public override void Draw(int x, int y, bool selected)
        {
            MenuItemHelper.Draw(x, y, definition.GetActionLabel(), Color.White, Game1.instance.contentManager.font.MenuFont);
        }
        public override Point GetSize()
        {
            return MenuItemHelper.GetSize(definition.GetActionLabel(), Game1.instance.contentManager.font.MenuFont);
        }
    }

    internal sealed class InventoryEquipmentToggle : IToggle, IMenuItem
    {
        private const int Padding = 2;
        private readonly UiInventoryItemDefinition definition;
        private Point labelSize;

        internal InventoryEquipmentToggle(UiInventoryItemDefinition item)
            : base(SafeEquipped(item))
        {
            definition = item;
        }

        protected override void OnToggle()
        {
            bool accepted = false;
            try { accepted = definition.SetEquipped(toggle); }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Equipment toggle failed for "
                    + definition.Id + ": " + error.Message);
            }
            if (!accepted) OverrideToggle(SafeEquipped(definition));
        }

        protected override bool CanChange()
        {
            try
            {
                return definition.GetCount() > 0 && definition.CanActivate();
            }
            catch { return false; }
        }

        public override void Draw(int x, int y, bool selected)
        {
            OverrideToggle(SafeEquipped(definition));
            string label = definition.GetActionLabel();
            UiTheme.DrawText(
                Game1.instance.contentManager.font.MenuFont,
                label,
                new Vector2(x, y),
                CanChange() ? UiTheme.Text : UiTheme.Disabled);
            DrawCheckBox(
                new Vector2(x + labelSize.X + Padding, y + labelSize.Y / 2),
                toggle);
        }

        public override Point GetSize()
        {
            Vector2 measured = Game1.instance.contentManager.font.MenuFont
                .MeasureString(definition.GetActionLabel());
            labelSize = new Point((int)measured.X, (int)measured.Y);
            Point checkbox = GetCheckBoxSize();
            return new Point(
                labelSize.X + Padding + checkbox.X,
                Math.Max(labelSize.Y, checkbox.Y));
        }

        private static bool SafeEquipped(UiInventoryItemDefinition item)
        {
            try { return item != null && item.IsEquipped != null && item.IsEquipped(); }
            catch { return false; }
        }
    }

    internal sealed class ActivateInventoryItemNode : IBTnode
    {
        private readonly UiInventoryItemDefinition definition;
        internal ActivateInventoryItemNode(UiInventoryItemDefinition item) { definition = item; }
        protected override BTresult MyRun(TickData data)
        {
            return definition.CanActivate() && definition.Activate()
                ? BTresult.Success
                : BTresult.Failure;
        }
    }

    internal sealed class HasRegisteredInventoryItemsNode : IBTnode
    {
        protected override BTresult MyRun(TickData data)
        {
            foreach (UiInventoryItemDefinition definition in UIApi.GetInventoryItems())
                if (definition.IsVisible() && definition.GetCount() > 0) return BTresult.Success;
            return BTresult.Failure;
        }
    }
}
