using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.SaveThread;
using JumpKing.SaveThread.SaveComponents;

namespace WardrobePlus
{
    internal static class EquipmentService
    {
        private static readonly MethodInfo SetEnabled = NativeAppearance.Manager.GetMethod("SetSkinEnabled", BindingFlags.Public | BindingFlags.Static);
        private static readonly PropertyInfo General = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube", true).GetProperty("generalSettings", BindingFlags.Public | BindingFlags.Static);
        internal static JumpKing.Player.Skins.SkinSettings Settings()
        { return Controller.Active != null && Controller.ActiveMap == NativeAppearance.MapId() ? Controller.Active.Settings : NativeAppearance.Settings(); }
        internal static bool Available(int item)
        {
            if (item == NativeAppearance.BaseItem) return false;
            return Settings().skins.Any(s => (int)s.item == item) && InventoryManager.HasItem((Items)item);
        }
        internal static EquipmentSelection Capture() { return new EquipmentSelection { Items = NativeAppearance.Worn().Distinct().OrderBy(x => x).ToList() }; }
        internal static ItemEquipOptions SnapshotOptions()
        {
            var value = ((GeneralSettings)General.GetValue(null, null)).item_options.Save;
            value.options = new List<ItemEquipOptions.ItemOption>(value.options); return value;
        }
        internal static void RestoreOptions(ItemEquipOptions value)
        {
            var settings = (GeneralSettings)General.GetValue(null, null); settings.item_options.Save = value; General.SetValue(null, settings, null);
        }
        internal static EquipmentSelection Toggle(int item)
        {
            if (!Available(item)) throw new InvalidOperationException("This item is not in your inventory.");
            var next = Capture();
            if (!next.Items.Remove(item))
            {
                var settings = Settings(); var skin = settings.skins.First(s => (int)s.item == item);
                next.Items.RemoveAll(id => settings.skins.Any(s => (int)s.item == id && s.layers.Intersect(skin.layers).Any()));
                next.Items.Add(item);
            }
            return next;
        }
        internal static string Restore(EquipmentSelection desired)
        {
            if (desired == null) return "";
            var settings = Settings(); var selected = new List<int>(); var missing = new List<int>();
            foreach (int item in desired.Items)
            {
                if (!Available(item)) { missing.Add(item); continue; }
                var skin = settings.skins.First(s => (int)s.item == item);
                selected.RemoveAll(id => settings.skins.First(s => (int)s.item == id).layers.Intersect(skin.layers).Any());
                selected.Add(item);
            }
            // Keep the current occupant of a slot whose requested item is unavailable.
            foreach (int item in Capture().Items)
                if (missing.Any(id => settings.skins.Any(s => (int)s.item == id && s.layers.Intersect(settings.GetSkin((Items)item).layers).Any()))
                    && !selected.Any(id => settings.GetSkin((Items)id).layers.Intersect(settings.GetSkin((Items)item).layers).Any())) selected.Add(item);
            foreach (int item in Capture().Items.Where(id => !selected.Contains(id)).ToArray()) SetEnabled.Invoke(null, new object[] { (Items)item, false });
            foreach (int item in selected.Where(id => !NativeAppearance.Worn().Contains(id))) SetEnabled.Invoke(null, new object[] { (Items)item, true });
            return missing.Count == 0 ? "" : "Unavailable items skipped: " + string.Join(", ",missing.Select(x => ((Items)x).ToString()));
        }
    }
}
