using System;
using System.Collections.Generic;
using BehaviorTree;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using JKRuntime.UI;

namespace MoreItems
{
    public sealed class ItemModuleDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public Func<bool> IsEnabled { get; private set; }
        public Action<bool> SetEnabled { get; private set; }
        public JKRuntime.Settings.Setting<bool> EnabledSetting { get; private set; }
        public Action<MenuSelector> AddSettingsItems { get; private set; }
        public Action InstallRuntime { get; private set; }
        public Action UninstallRuntime { get; private set; }

        public ItemModuleDefinition(
            string id,
            string name,
            Func<bool> isEnabled,
            Action<bool> setEnabled,
            Action<MenuSelector> addSettingsItems = null,
            Action installRuntime = null,
            Action uninstallRuntime = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Item module id is required", "id");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Item module name is required", "name");
            if (isEnabled == null) throw new ArgumentNullException("isEnabled");
            if (setEnabled == null) throw new ArgumentNullException("setEnabled");
            Id = id.Trim();
            Name = name;
            IsEnabled = isEnabled;
            SetEnabled = setEnabled;
            AddSettingsItems = addSettingsItems;
            InstallRuntime = installRuntime ?? delegate { };
            UninstallRuntime = uninstallRuntime ?? delegate { };
            EnabledSetting = new JKRuntime.Settings.Setting<bool>("more-items." + Id + ".enabled", "Enable " + Name, IsEnabled, setEnabled,
                delegate { ItemModuleRegistry.RefreshRuntime(Id); UIApi.NotifyInventoryItemChanged(string.Empty); });
            SetEnabled = EnabledSetting.Set;
        }
    }

    public static partial class MoreItemsApi
    {
        public static void RegisterModule(ItemModuleDefinition definition)
        {
            ItemModuleRegistry.Register(definition);
        }

        public static void UnregisterModule(string id)
        {
            ItemModuleRegistry.Unregister(id);
        }

        public static bool TryGetModule(
            string id,
            out ItemModuleDefinition definition)
        {
            return ItemModuleRegistry.TryGet(id, out definition);
        }

        public static IList<ItemModuleDefinition> GetModules()
        {
            return ItemModuleRegistry.GetDefinitions();
        }

        public static bool IsModuleEnabled(string id)
        {
            return ItemModuleRegistry.IsEnabled(id);
        }

        public static ItemModuleToggleOption CreateModuleToggle(string id)
        {
            return ItemModuleRegistry.CreateToggle(id);
        }
    }

    internal static class ItemModuleRegistry
    {
        private static readonly Dictionary<string, ItemModuleDefinition> Definitions =
            new Dictionary<string, ItemModuleDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> Order = new List<string>();
        private static readonly HashSet<string> Installed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool runtimeActive;

        internal static void Register(ItemModuleDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            StopModule(definition.Id);
            if (!Definitions.ContainsKey(definition.Id)) Order.Add(definition.Id);
            Definitions[definition.Id] = definition;
            RefreshRuntime(definition.Id);
        }

        internal static void Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            StopModule(id);
            Definitions.Remove(id);
            Order.RemoveAll(delegate(string value)
            {
                return string.Equals(value, id, StringComparison.OrdinalIgnoreCase);
            });
        }

        internal static bool TryGet(string id, out ItemModuleDefinition definition)
        {
            return Definitions.TryGetValue(id ?? string.Empty, out definition);
        }

        internal static IList<ItemModuleDefinition> GetDefinitions()
        {
            List<ItemModuleDefinition> result = new List<ItemModuleDefinition>();
            foreach (string id in Order)
                if (Definitions.ContainsKey(id)) result.Add(Definitions[id]);
            return result;
        }

        internal static bool IsEnabled(string id)
        {
            ItemModuleDefinition definition;
            return TryGet(id, out definition) && definition.IsEnabled();
        }

        internal static ItemModuleToggleOption CreateToggle(string id)
        {
            ItemModuleDefinition definition;
            if (!TryGet(id, out definition))
                throw new ArgumentException("Item module is not registered", "id");
            return new ItemModuleToggleOption(definition);
        }

        internal static void StartRuntime()
        {
            StopRuntime();
            runtimeActive = true;
            foreach (string id in Order) RefreshRuntime(id);
        }

        internal static void StopRuntime()
        {
            var errors = new List<Exception>();
            for (int index = Order.Count - 1; index >= 0; index--)
                try { StopModule(Order[index]); } catch (Exception error) { errors.Add(error); }
            runtimeActive = false;
            if (errors.Count != 0) throw new AggregateException("Item module cleanup incomplete", errors);
        }

        internal static void RefreshRuntime(string id)
        {
            StopModule(id);
            ItemModuleDefinition definition;
            if (!runtimeActive
                || !TryGet(id, out definition)
                || !definition.IsEnabled()) return;
            Installed.Add(definition.Id);
            try { definition.InstallRuntime(); }
            catch (Exception failure)
            {
                try { StopModule(id); }
                catch (Exception cleanup) { throw new AggregateException("Item module startup and cleanup failed", failure, cleanup); }
                throw;
            }
        }

        private static void StopModule(string id)
        {
            ItemModuleDefinition definition;
            if (!Installed.Contains(id)
                || !TryGet(id, out definition)) return;
            definition.UninstallRuntime();
            Installed.Remove(id);
        }

        internal static IBTnode CreateSettingsGrid(object factory, GuiFormat format)
        {
            List<UiCompactGridItemDefinition> cards =
                new List<UiCompactGridItemDefinition>();
            foreach (ItemModuleDefinition definition in GetDefinitions())
            {
                if (definition.AddSettingsItems == null) continue;
                MenuSelector page = new MenuSelector(format);
                definition.AddSettingsItems(page);
                page.Initialize();
                cards.Add(new UiCompactGridItemDefinition(
                    definition.Name,
                    page,
                    null,
                    definition.IsEnabled));
            }
            return UIApi.CreateCompactGrid(factory, cards);
        }
    }

    public class ItemModuleToggleOption : IToggle, IMenuItem
    {
        private const int Padding = 2;
        private readonly ItemModuleDefinition definition;
        private readonly string label;
        private Point labelSize;

        public ItemModuleToggleOption(ItemModuleDefinition item)
            : this(item, "Enable " + (item == null ? string.Empty : item.Name))
        {
        }

        protected ItemModuleToggleOption(ItemModuleDefinition item, string text)
            : base(item != null && item.IsEnabled())
        {
            if (item == null) throw new ArgumentNullException("item");
            definition = item;
            label = text;
        }

        protected override void OnToggle()
        {
            definition.EnabledSetting.Set(toggle);
        }

        public override void Draw(int x, int y, bool selected)
        {
            OverrideToggle(definition.IsEnabled());
            JKRuntime.UI.UiTheme.DrawText(
                Game1.instance.contentManager.font.MenuFont,
                label,
                new Vector2(x, y),
                JKRuntime.UI.UiTheme.Text);
            DrawCheckBox(
                new Vector2(x + labelSize.X + Padding, y + labelSize.Y / 2),
                toggle);
        }

        public override Point GetSize()
        {
            Vector2 measured = Game1.instance.contentManager.font.MenuFont.MeasureString(label);
            labelSize = new Point((int)measured.X, (int)measured.Y);
            Point checkbox = GetCheckBoxSize();
            return new Point(
                labelSize.X + Padding + checkbox.X,
                Math.Max(labelSize.Y, checkbox.Y));
        }
    }

}
