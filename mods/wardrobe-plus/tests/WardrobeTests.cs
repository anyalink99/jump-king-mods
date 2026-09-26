using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using WardrobePlus;

internal static partial class WardrobeTests
{
    private static int checks;
    private static string output;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
    private static void Reject(Action action, string label)
    { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, label); }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            ResolutionTests(); EquipmentDataTests(); StoreTests(); FittingTests(); RecipeTests(); MaterialTests(); MenuOnlyAccess();
            NativeAppearance.Validate();
            Check(new Preview().Poses.Count > 13, "Native movement and ending pose contracts are available");
            if (args.Length >= 2 && args[0] == "--graphics") GraphicsTests(args[1], args.Length > 2 ? args[2] : null);
            Console.WriteLine("[OK] Wardrobe+ " + checks + " checks; artifacts: " + output);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void MenuOnlyAccess()
    {
        const string id = "wardrobe-plus.open";
        JKRuntime.UI.UIApi.RegisterBinding(new JKRuntime.UI.UiBindingDefinition(id, "Wardrobe+", "Open wardrobe", () => new[] { new JKRuntime.UI.UiChord(70) }, values => { }, () => { }));
        JKRuntime.UI.UIApi.RegisterInputAction(JKRuntime.UI.UiInputActionDefinition.FromChords(id, "Open wardrobe", 100, () => new[] { new JKRuntime.UI.UiChord(70) }, () => true, () => { throw new Exception("Removed hotkey fired"); }));
        Menus.Sync(); Menus.Sync();
        Check(!JKRuntime.UI.UIApi.GetBindings().Any(b => b.Id == id) && !JKRuntime.UI.UIApi.GetInputActions().Any(a => a.Id == id), "Old wardrobe hotkey registration is removed, including its input action");
        Check(JKRuntime.UI.UIApi.GetMainMenuItems().Count(m => m.Id == "wardrobe-plus.workshop") == 1, "Wardrobe remains accessible once in Workshop after removing its hotkey");
        JKRuntime.UI.UIApi.UnregisterMainMenuItem("wardrobe-plus.workshop");
    }
    private static Catalog SampleCatalog()
    {
        var catalog = new Catalog();
        catalog.Sets.Add(new SetSource { Id = "workshop:1", Name = "First" });
        catalog.Sets.Add(new SetSource { Id = "workshop:2", Name = "Second" });
        catalog.Sources.Add(new Source { Id = "workshop:1:set:4", ParentId = "workshop:1", Collection = true, Item = 4, Asset = "parent", Name = "Parent hat" });
        catalog.Sources.Add(new Source { Id = "workshop:2:set:4", ParentId = "workshop:2", Collection = true, Item = 4, Asset = "borrowed", Name = "Borrowed hat" });
        catalog.Sources.Add(new Source { Id = "workshop:3:skin:4", ParentId = "", Item = 4, Asset = "single", Name = "Single hat" });
        return catalog;
    }
    private static Resolution Default(int item, bool original)
    { return new Resolution { Id = original ? "original" : "map", Asset = original ? "original" : "map", Label = original ? "Original" : "Map" }; }
    private static void ResolutionTests()
    {
        var catalog = SampleCatalog(); var outfit = new Outfit { ParentId = "workshop:1" };
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => true).Asset == "parent", "Inherited item resolves from its parent");
        Check(Resolver.Resolve(outfit, 8, catalog, Default, x => true).Asset == "map", "Partial sets fall back per item");
        outfit.Set(new AppearanceChoice { Item = 4, Mode = ChoiceMode.Source, SourceId = "workshop:3:skin:4" });
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => true).Asset == "single", "Standalone override wins over a collection");
        outfit.Set(new AppearanceChoice { Item = 4, Mode = ChoiceMode.Source, SourceId = "workshop:2:set:4" });
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => true).Asset == "borrowed", "Another collection can donate an exact item");
        var missing = Resolver.Resolve(outfit, 4, catalog, Default, x => x != "borrowed");
        Check(missing.Asset == "parent" && missing.Warning.Contains("Missing"), "Missing explicit source visibly inherits without erasing the reference");
        Check(outfit.Choice(4).SourceId == "workshop:2:set:4", "Missing-source resolution preserves intended source identity");
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => true).Asset == "borrowed", "Returning subscriptions recover their original selection");
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => false).Asset == "map", "Missing explicit and parent sources reach the map");
        outfit.Set(new AppearanceChoice { Item = 4, Mode = ChoiceMode.LevelDefault });
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => true).Asset == "map", "Level default bypasses an active parent");
        outfit.Set(new AppearanceChoice { Item = 4, Mode = ChoiceMode.OriginalGame });
        Check(Resolver.Resolve(outfit, 4, catalog, Default, x => true).Asset == "original", "Original game bypasses map and parent");
        outfit.Set(new AppearanceChoice { Item = 8, Mode = ChoiceMode.Source, SourceId = "workshop:2:set:4" });
        Check(Resolver.Resolve(outfit, 8, catalog, Default, x => true).Asset == "map", "Cross-item source assignment is rejected by resolution");
        outfit.Set(new AppearanceChoice { Item = 4, Locked = true });
        var randomized = Resolver.Randomize(outfit, catalog, new[] { 4, 8 }, new[] { "workshop:2:set:4" }, new Random(1));
        Check(randomized.Choice(4).Mode == ChoiceMode.Inherit, "Randomizer respects item locks");
        outfit.Set(new AppearanceChoice { Item = 4 });
        randomized = Resolver.Randomize(outfit, catalog, new[] { 4, 8 }, new[] { "workshop:2:set:4" }, new Random(1));
        Check(randomized.Choice(4).SourceId == "workshop:2:set:4" && randomized.Choice(8).SourceId == outfit.Choice(8).SourceId, "Randomizer only uses matching favorite sources");
        Check(outfit.Choice(4).Mode == ChoiceMode.Inherit, "Randomization edits a copy");
        Reject(() => Catalog.AssetPath(output, "../outside"), "Catalog prevents source path traversal");
        Reject(() => Catalog.AssetPath(output, Path.GetFullPath(output)), "Catalog rejects absolute asset names");
    }
    private static void StoreTests()
    {
        var store = new Store(Path.Combine(output, "store")); var data = new WardrobeData();
        data.Current.Name = "First"; store.Save(data); data.Current.Name = "Second"; store.Save(data);
        Check(store.Load().Current.Name == "Second", "Current state round-trips");
        Check(Store.Read<WardrobeData>(store.FilePath + ".bak").Current.Name == "First", "Previous complete settings remain in the backup");
        var draft = data.Current.Copy(); draft.Set(new AppearanceChoice { Item = 9, Mode = ChoiceMode.OriginalGame });
        Check(data.Current.Choices.Count == 0, "Preview draft cannot mutate the applied outfit");
        File.WriteAllText(store.FilePath, "broken"); var recovering = new Store(store.DirectoryPath); var recovered = recovering.Load();
        Check(recovering.ReadOnly && recovered.Current.Name == "First", "Corrupt settings use backup read-only");
        Reject(() => recovering.Save(new WardrobeData()), "Recovery cannot overwrite corrupt user data");
        Check(File.ReadAllText(store.FilePath) == "broken", "Corrupt original is preserved for recovery");
        var invalid = new WardrobeData { Version = 999 };
        Reject(() => Store.Validate(invalid), "Unknown versions are rejected");
        invalid = new WardrobeData(); invalid.Current.Choices.Add(new AppearanceChoice { Item = 4 }); invalid.Current.Choices.Add(new AppearanceChoice { Item = 4 });
        Reject(() => Store.Validate(invalid), "Duplicate item overrides are rejected");
        invalid = new WardrobeData(); invalid.Current.Fits.Add(new FitAdjustment { X = int.MinValue });
        Reject(() => Store.Validate(invalid), "Out-of-range fit data is rejected without integer overflow");
        invalid = new WardrobeData(); invalid.Current.Fits.Add(new FitAdjustment()); invalid.Current.Fits.Add(new FitAdjustment());
        Reject(() => Store.Validate(invalid), "Ambiguous duplicate fitting records are rejected");
        invalid = new WardrobeData(); invalid.Current.Id = "../outside";
        Reject(() => Store.Validate(invalid), "Recipe filenames cannot escape their directory");
        string badXml = Path.Combine(output, "external.xml");
        File.WriteAllText(badXml, "<!DOCTYPE Recipe [<!ENTITY x SYSTEM 'file:///missing'>]><Recipe><Version>&x;</Version></Recipe>");
        Reject(() => Store.Read<Recipe>(badXml), "Recipe XML does not resolve external entities");
    }
    private static void FittingTests()
    {
        var outfit = new Outfit();
        outfit.SetFit(new FitAdjustment { BaseId = "a", SourceId = "hat", Item = 4, X = 3, Y = -2 });
        Check(outfit.Fit("a", "hat", 4, 0, 2).X == 3, "All-pose fit inherits into individual frames");
        outfit.SetFit(new FitAdjustment { BaseId = "a", SourceId = "hat", Item = 4, Group = 0, Frame = 2, X = -4 });
        Check(outfit.Fit("a", "hat", 4, 0, 2).X == -4 && outfit.Fit("a", "hat", 4, 0, 1).X == 3, "Pose fit replaces only its own pose");
        Check(outfit.Fit("b", "hat", 4, 0, 2).X == 0 && outfit.Fit("a", "other", 4, 0, 2).X == 0, "Fits do not leak across bodies or source items");
        var copy = outfit.Copy(); copy.Fits[0].X = 10;
        Check(outfit.Fits[0].X == 3, "Fitting cancel can restore independent data");
        Check(FitBaker.Nudge(Point.Zero, 1, 0, true) == new Point(-1, 0), "Screen-right arrows reverse local X when facing left");
        Check(FitBaker.Nudge(new Point(32, -32), 1, -1, false) == new Point(32, -32), "Arrow nudges clamp at safe padding limits");
        var pixels = new Color[16]; pixels[5] = Color.Red; pixels[6] = Color.Blue;
        var shifted = new Color[100]; FitBaker.CopyFrame(pixels, 4, new Rectangle(1, 1, 2, 2), shifted, 10, new Point(3, 4));
        Check(shifted[43] == Color.Red && shifted[44] == Color.Blue && shifted[42] == Color.Transparent, "Fitting translates source pixels without neighboring-frame contamination");
    }
    private static void RecipeTests()
    {
        var store = new Store(Path.Combine(output, "recipes"));
        var outfit = new Outfit { Name = "Shared look", ParentId = "workshop:42" };
        outfit.Set(new AppearanceChoice { Item = 4, Mode = ChoiceMode.Source, SourceId = "workshop:99:set:4", Label = "Hat" });
        outfit.SetFit(new FitAdjustment { BaseId = "base", SourceId = "workshop:99:set:4", Item = 4, X = 2 });
        string path = store.Export(outfit); var imported = store.Import(path);
        Check(imported.Id != outfit.Id && imported.Choice(4).SourceId == outfit.Choice(4).SourceId, "Recipe import creates an independent outfit with original source references");
        Check(imported.Fits[0].X == 2, "Recipes include local fitting preferences");
        var recipe = Store.Read<Recipe>(path);
        Check(recipe.WorkshopLinks.Count == 2 && recipe.WorkshopLinks.All(x => x.StartsWith("https://steamcommunity.com/")), "Recipe includes direct Workshop dependency links");
    }
}
