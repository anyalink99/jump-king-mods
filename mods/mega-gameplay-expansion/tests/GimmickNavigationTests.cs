using System;
using System.Linq;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static object PageCall(GimmickPage page, string method, params object[] args)
        { return typeof(GimmickPage).GetMethod(method, Flags).Invoke(page, args); }
        private static T PageField<T>(GimmickPage page, string name)
        { return (T)typeof(GimmickPage).GetField(name, Flags).GetValue(page); }
        private static void PageChoose(GimmickPage page, string id)
        {
            var list = PageField<UiList>(page, "list"); list.Select(id);
            list.Update(new UiInput { Action = UiAction.Confirm, Confirm = true });
        }
        private static void GimmickNavigationRegression()
        {
            var previous = Settings.Current; var previousMaps = GimmickMaps.Maps;
            var field = typeof(Settings).GetField("current", Flags);
            var entry = new GimmickEntry { Id = "navigation:water", Label = "Water", Owner = "Navigation provider", Kind = "Block", Colour = Color.Blue, Family = "Medium / zone" };
            var map = new GimmickMap { Id = "navigation:map", Title = "Navigation map", Regions = new[] { new GimmickRegion { Number = 1, Name = "Lower", First = 1, Last = 3 } } };
            map.Colours[Color.Blue.PackedValue] = new System.Collections.Generic.HashSet<int> { 2 };
            bool toggled = true; int writes = 0;
            var setting = new GimmickEntry { Id = "navigation:setting", Label = "Fixture setting", Owner = "Navigation provider", Kind = "Setting", Read = () => toggled, Write = value => { writes++; toggled = value; } };
            var refusing = new GimmickEntry { Id = "navigation:refusal", Label = "Refusing setting", Owner = "Navigation provider", Kind = "Setting", Read = () => true, Write = value => { writes++; throw new InvalidOperationException("Owner refused"); } };
            try {
                field.SetValue(null, new Preferences { WarpJump = true, NoWalkOff = true, AirDash = true, GimmickSearch = new GimmickSearchPreferences { Text = "no match", Colour = "#FF0000" },
                    GimmickPins = new[] { entry.Id }, GimmickRules = new[] { new GimmickRule { Id = "navigation:missing", Name = "Saved unavailable", Enabled = true, Screens = new[] { 2, 5 }, Application = GimmickApplication.FillEmpty } } });
                Gimmicks.Add(entry); Gimmicks.Add(setting); Gimmicks.Add(refusing); GimmickMaps.Maps = new[] { map };
                Gimmicks.Initialize(); GimmickClassification.Inspect(setting);
                Require(setting.Family == "Mod settings", "A discovered toggle is not proof of a player mechanic");
                var page = new GimmickPage(null, new JumpKing.PauseMenu.GuiFormat(), false);
                PageCall(page, "GoHome");
                Require(!PageField<bool>(page, "browsing"), "Library opens a navigation menu independent of old search filters");
                PageChoose(page, "maps"); PageChoose(page, map.Id); PageChoose(page, "1");
                Require(PageField<GimmickSearchRecord[]>(page, "results").Any(r => r.Entry.Id == entry.Id), "Map-region route ignores incompatible previous text and colour filters");
                PageField<GimmickSearchPreferences>(page, "query").Text = "water"; PageCall(page, "SaveQuery");
                Require(Settings.Current.GimmickSearch.Text == "no match" && Settings.Current.GimmickSearch.Colour == "#FF0000", "Map browsing never overwrites the user's saved search");
                PageCall(page, "Back"); Require(PageField<UiList>(page, "list").SelectedId == "1", "Back restores selected region");
                PageCall(page, "Back"); Require(PageField<UiList>(page, "list").SelectedId == map.Id, "Back restores selected map");
                PageCall(page, "Back"); Require(PageField<UiList>(page, "list").SelectedId == "maps", "Back restores main menu focus");
                PageChoose(page, "search");
                Require(PageField<GimmickSearchPreferences>(page, "query").Text == "no match", "Search resumes its previous independent query");
                PageCall(page, "Navigate", new Action(() => PageCall(page, "Filters")));
                PageChoose(page, "clear"); PageCall(page, "Back");
                Require(PageField<GimmickSearchPreferences>(page, "query").Colour == null, "Clearing filters survives return from filter page");
                PageCall(page, "GoHome"); PageChoose(page, "active");
                Require(((string[])PageCall(page, "ActiveIds")).Contains("navigation:missing"), "Unavailable enabled rules remain discoverable in Active & reset");
                Require(!((string[])PageCall(page, "ActiveIds")).Contains(setting.Id) && !((string[])PageCall(page, "ActiveIds")).Contains(refusing.Id), "Already enabled external settings are not MGE-owned effects");
                Require(Gimmicks.DefaultPins.All(id => ((string[])PageCall(page, "ActiveIds")).Contains(id)), "MGE's three enabled built-in modes remain visible");
                PageCall(page, "GoHome"); PageChoose(page, "restore");
                Require(!PageField<bool>(page, "browsing") && PageField<UiList>(page, "list").SelectedId == "restore", "Restore is directly accessible on the library home and returns there");
                Require(toggled && Settings.Current.GimmickRules.Length == 1 && !Settings.Current.GimmickRules[0].Enabled
                    && Settings.Current.GimmickRules[0].Screens.SequenceEqual(new[] { 2, 5 }) && Settings.Current.GimmickPins.Single() == entry.Id,
                    "Release-only preserves configuration and pins without changing global settings");
                Require(Settings.Current.WarpJump && Settings.Current.NoWalkOff && Settings.Current.AirDash && writes == 0, "Restore leaves native modes and all third-party settings unchanged");
                PageCall(page, "ResetActive", true);
                Require(toggled && writes == 0, "Bulk reset must never call third-party persistence callbacks, even when they are enabled");
                Require(!Settings.Current.WarpJump && !Settings.Current.NoWalkOff && !Settings.Current.AirDash, "Bulk reset disables the three MGE-owned global modes");
                Require(((string[])PageCall(page, "ActiveIds")).Length == 0, "External settings do not keep the active-effects counter nonzero");
                PageCall(page, "ActiveItem", setting.Id);
                Require(writes == 0, "A stale external-setting route cannot use active-effect disable controls");
                PageCall(page, "Details", setting.Id);
                PageChoose(page, "enabled"); PageChoose(page, "apply");
                Require(!toggled && writes == 1, "An explicit individual mod-setting edit still uses its original persistence callback");
                PageCall(page, "ResetActive", false); PageCall(page, "ResetActive", true);
                Require(!toggled && writes == 1, "Repeated Restore and bulk reset leave explicitly edited mod settings unchanged");
            }
            finally {
                Gimmicks.Entries.Remove(entry.Id); Gimmicks.Entries.Remove(setting.Id); Gimmicks.Entries.Remove(refusing.Id);
                GimmickMaps.Maps = previousMaps; field.SetValue(null, previous); Settings.Save();
            }
            Console.WriteLine("[OK] Library navigation: independent search, nested Back, unavailable rules, MGE-owned reset and zero foreign persistence calls from reset");
        }
    }
}
