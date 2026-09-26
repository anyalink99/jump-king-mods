using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.PauseMenu.BT.Actions.Possessions;
using JumpKing.Workshop;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace WardrobePlus
{
    internal static class Hooks
    {
        internal static bool Installed;
        internal static string Error = "";
        private static readonly BindingFlags Flags = NativeAppearance.Flags;
        private static JKRuntime.OwnedPatches patches;
        private static readonly ConditionalWeakTable<SwitchSkinOption, object> overriddenSelectors = new ConditionalWeakTable<SwitchSkinOption, object>();
        internal static void Install()
        {
            if (Installed) return;
            try
            {
                if (patches != null) { patches.Dispose(); patches = null; }
                patches = new JKRuntime.OwnedPatches("WardrobePlus.Appearance");
                Action<Type, string, string, bool> add = (type, name, replacement, postfix) =>
                {
                    var original = type.GetMethod(name, Flags);
                    var handler = typeof(Hooks).GetMethod(replacement, Flags);
                    if (original == null || handler == null) throw new MissingMethodException(type.FullName, name);
                    patches.Add(original, prefix: postfix ? null : handler, postfix: postfix ? handler : null);
                };
                add(typeof(PlayerSpritesManager), "LoadSpritesInternal", "LoadPrefix", false);
                add(typeof(Game1), "Update", "Pump", true);
                add(typeof(Game1), "Draw", "AfterDraw", true);
                add(typeof(WorkshopManager), "UpdateMenu", "ContentChanged", true);
                add(typeof(WorkshopManager), "InvokeOnItemParsed", "ContentParsed", false);
                add(typeof(ToggleReskin), "OnToggle", "ReskinToggle", false);
                add(typeof(ToggleCollection), "OnToggle", "CollectionToggle", false);
                add(typeof(ToggleReskin), "Draw", "ReskinDraw", false);
                add(typeof(ToggleCollection), "Draw", "CollectionDraw", false);
                add(typeof(SwitchSkinOption), "CanChange", "CanChange", false);
                add(typeof(SwitchSkinOption), "OnOptionChange", "OptionChange", false);
                add(typeof(SwitchSkinOption), "CurrentOptionName", "OptionName", false);
                add(typeof(SwitchSkinOption), "GetSize", "OptionSize", false);
                add(typeof(IOptions), "MyRun", "OptionRun", false);
                add(typeof(JumpKing.Workshop.UGC.Utils), "AnySkinEnabledFromItem", "HasAlternatives", false);
                Installed = true; Error = "";
            }
            catch (Exception error)
            {
                Error = error.GetBaseException().Message;
                if (patches != null)
                    try { patches.Dispose(); patches = null; }
                    catch (Exception cleanup) { Error += "; patch cleanup pending: " + cleanup.GetBaseException().Message; }
                Console.WriteLine("[Wardrobe+] " + Error);
            }
        }
        private static bool LoadPrefix(bool isReload)
        {
            if (!Controller.Enabled) return true;
            // A required base/map failure remains visible; do not silently invoke a second resolver.
            return !Controller.Load(isReload);
        }
        private static void Pump() { Controller.Pump(); }
        private static void AfterDraw() { Controller.AfterDraw(); }
        private static void ContentChanged() { Controller.ContentChanged(); }
        private static void ContentParsed() { Controller.ContentParsed(); }
        private static bool ReskinToggle(Reskin ___m_item)
        {
            if (!Controller.Enabled) return true;
            int item = (int)___m_item.Info.skin;
            string id = Catalog.SourceId(___m_item, item, false);
            var outfit = Controller.Data.Current.Copy(); var old = outfit.Choice(item);
            outfit.Set(old.Mode == ChoiceMode.Source && old.SourceId == id ? new AppearanceChoice { Item = item, Locked = old.Locked }
                : new AppearanceChoice { Item = item, Mode = ChoiceMode.Source, SourceId = id, Label = ___m_item.Name ?? ___m_item.Info.name, Locked = old.Locked });
            Controller.Apply(outfit); return false;
        }
        private static bool CollectionToggle(Collection ___m_collection)
        {
            if (!Controller.Enabled) return true;
            string id = Catalog.PackageId(___m_collection);
            var outfit = Controller.Data.Current.Copy();
            bool remove = outfit.ParentId == id;
            outfit.ParentId = remove ? "" : id; outfit.ParentName = remove ? "" : ((IUGC)___m_collection).Name ?? id;
            if (!remove && !Controller.Data.KeepCustomizations) outfit.Choices.Clear();
            Controller.Apply(outfit); return false;
        }
        private static bool ReskinDraw(Reskin ___m_item, int x, int y)
        {
            if (!Controller.Enabled) return true;
            var choice = Controller.Data.Current.Choice((int)___m_item.Info.skin);
            string label = choice.Mode == ChoiceMode.Source && choice.SourceId == Catalog.SourceId(___m_item, (int)___m_item.Info.skin, false) ? "[x] Use appearance" : "[ ] Use appearance";
            UiTheme.TextLine(label, new Vector2(x, y), UiTheme.Text, false); return false;
        }
        private static bool CollectionDraw(Collection ___m_collection, int x, int y)
        {
            if (!Controller.Enabled) return true;
            UiTheme.TextLine(Controller.CollectionStatus(___m_collection), new Vector2(x, y), UiTheme.Text, false); return false;
        }
        private static bool CanChange(Items ___ITEM, ref bool __result)
        {
            if (!Controller.Enabled) return true;
            __result = NativeAppearance.Worn().Contains((int)___ITEM); return false;
        }
        private static void Sync(SwitchSkinOption option)
        {
            overriddenSelectors.GetValue(option, key => new object());
            int item = (int)(Items)typeof(SwitchSkinOption).GetField("ITEM", Flags).GetValue(option);
            var values = Controller.Choices(item); var selected = Controller.Data.Current.Choice(item);
            int index = values.FindIndex(x => x.Mode == selected.Mode && x.SourceId == selected.SourceId);
            typeof(IOptions).GetField("m_option_count", Flags).SetValue(option, values.Count);
            typeof(IOptions).GetField("m_current", Flags).SetValue(option, Math.Max(0, index));
        }
        private static void RestoreSelector(SwitchSkinOption option)
        {
            object marker;
            if (!overriddenSelectors.TryGetValue(option, out marker)) return;
            var item = (Items)typeof(SwitchSkinOption).GetField("ITEM", Flags).GetValue(option);
            var skins = WorkshopManager.instance.GetSkinsFromItem(item);
            int selected = WorkshopManager.instance.GetEnabledSkin(item) + 1;
            typeof(SwitchSkinOption).GetField("reskins", Flags).SetValue(option, skins);
            typeof(SwitchSkinOption).GetField("SIZE", Flags).SetValue(option, skins.Count + 1);
            typeof(SwitchSkinOption).GetField("last_option", Flags).SetValue(option, selected);
            typeof(IOptions).GetField("m_option_count", Flags).SetValue(option, skins.Count + 1);
            typeof(IOptions).GetField("m_current", Flags).SetValue(option, selected);
            overriddenSelectors.Remove(option);
        }
        private static void OptionRun(IOptions __instance)
        {
            var option = __instance as SwitchSkinOption;
            if (option == null) return;
            if (Controller.Enabled) Sync(option); else RestoreSelector(option);
        }
        private static bool OptionChange(Items ___ITEM, int option)
        {
            if (!Controller.Enabled) return true;
            var values = Controller.Choices((int)___ITEM);
            var outfit = Controller.Data.Current.Copy(); var selected = values[Math.Max(0, Math.Min(values.Count - 1, option))];
            selected.Locked = outfit.Choice((int)___ITEM).Locked; outfit.Set(selected);
            Controller.Apply(outfit); return false;
        }
        private static bool OptionName(SwitchSkinOption __instance, Items ___ITEM, ref string __result)
        {
            if (!Controller.Enabled) { RestoreSelector(__instance); return true; }
            Sync(__instance);
            var choice = Controller.Data.Current.Choice((int)___ITEM);
            string label = choice.Mode == ChoiceMode.Inherit ? "Inherit" : choice.Label;
            __result = UiTheme.FitText(label, 170, false); return false;
        }
        private static bool OptionSize(SwitchSkinOption __instance, ref Point __result)
        { if (!Controller.Enabled) { RestoreSelector(__instance); return true; } __result = new Point(190, 20); return false; }
        private static bool HasAlternatives(ref bool __result)
        { if (!Controller.Enabled) return true; __result = true; return false; }
    }
}
