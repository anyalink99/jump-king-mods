using System;
using System.IO;
using System.Xml.Linq;
using JKRuntime.Gameplay;
using Microsoft.Xna.Framework;
namespace JKRuntime
{
    internal static class MapMechanicTests
    {
        private const string Id = "test.mechanic";
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        private static void Invalid(string xml) { bool rejected = false; try { MapMechanics.Parse(XElement.Parse(xml), null, 3); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Invalid authoring rejected: " + xml); }
        public static void Main(string[] args)
        {
            System.Reflection.Assembly.LoadFrom(args[0]);
            PreparationHooks.Install();
            Check(PreparationHooks.TickAvailable, "Installed native safe player boundary exists");
            int observed = 0, applied = 0;
            using (MapMechanics.Watch("test", delegate { return observed; }, delegate { applied++; }))
            {
                MapMechanics.BeforePlayer(); Check(applied == 0, "Unchanged scope does not recompose controllers");
                observed++; MapMechanics.BeforePlayer(); MapMechanics.BeforePlayer();
                Check(applied == 1, "Changed scope recomposes once before component iteration");
            }
            observed++; MapMechanics.BeforePlayer(); Check(applied == 1, "Disposed scope releases transition observer");
            using (MapMechanics.Watch("test", delegate { return observed; }, delegate { applied++; }))
            { observed++; JKRuntime.Settings.Commands.Drain(); Check(applied == 2, "Command entity safely reconciles without relying on a Harmony callback"); }
            bool saved = true; int writes = 0;
            string historyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "map-mechanic-history.xml");
            var historyField = typeof(MapMechanics).GetField("preferences", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            historyField.SetValue(null, new MapMechanicPreferences(historyPath));
            MapMechanics.InitializePreference(Id, false);
            MapMechanics.Register("test", Id, delegate { saved = false; writes++; }, new MapMechanicParameter("strength", 0.0, 2.0));
            bool mappingMarkerRejected = false;
            try { MapMechanics.ScreenColor("mega-mapping.scene", MapMechanicMode.On); }
            catch (ArgumentException) { mappingMarkerRejected = true; }
            Check(mappingMarkerRejected, "Mapping presentation has no common screen marker");
            var markerFactory = (JumpKing.API.IBlockFactory)typeof(MapMechanics).GetField("factory",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            for (int green = 110; green <= 112; green++)
                Check(!markerFactory.CanMakeBlock(new Color(173, green, 211, 255), null), "Removed Mapping colour is not claimed by Runtime: " + green);
            Check(markerFactory.CanMakeBlock(MapMechanics.ScreenColor("subframe-charge.timing", MapMechanicMode.Local), null),
                "Existing gameplay markers remain registered");
            var hit = new Rectangle(20, 20, 8, 8);
            var tags = new[] { MapMechanics.ControlledTagPrefix + Id };
            MapMechanics.Parse(XElement.Parse("<Mechanics version='1'><Mechanic id='test.mechanic'><Screens from='1' mode='on'><Parameter name='strength' value='0.5'/></Screens><Screens from='2' mode='local'/><Zone id='a' screen='2' x='10' y='10' width='100' height='100'><Parameter name='strength' value='1.5'/></Zone></Mechanic></Mechanics>"), tags, 3);
            Check(saved && writes == 0, "Preparation does not mutate settings");
            MapMechanics.RecordUse(Id);
            Check(!File.Exists(historyPath), "Preparation and cancelled intro do not write history or establish use");
            MapMechanics.CommitEntry(); MapMechanics.CommitEntry();
            Check(!saved && writes == 1 && !MapMechanics.CanConfigure(Id), "Controlled entry durably disables once and locks user enabling");
            Check(MapMechanics.Resolve(Id, 0, hit, false).Enabled && MapMechanics.Resolve(Id, 0, hit, false).Authored, "On grants map activation independently of preference");
            Check(MapMechanics.Resolve(Id, 0, hit, true).Number("strength", 1) == 0.5f, "Validated screen parameters are applied");
            Check(MapMechanics.Resolve(Id, 1, hit, false).Number("strength", 1) == 1.5f && MapMechanics.Resolve(Id, 1, hit, false).Enabled, "Local XML area supplies activation and parameters");
            Check(!MapMechanics.Resolve(Id, 1, new Rectangle(200, 200, 5, 5), true).Enabled, "Local denies outside areas even with enabled preference");
            Check(!MapMechanics.Resolve(Id, 1, Rectangle.Empty, false).Enabled, "Missing player is not inside a zone");
            Check(!MapMechanics.Resolve(Id, 2, hit, true, true).Enabled, "Unmarked controlled screen rejects saved enable and local surface");
            MapPolicy.Parse(XElement.Parse("<MapPolicy version='1'><Mechanic id='test.mechanic' reason='Denied'/></MapPolicy>"));
            Check(!MapMechanics.Resolve(Id, 0, hit, true, true).Enabled, "Hard policy denies authored screen and surface"); MapPolicy.Clear();
            MapMechanics.ClearWorld();
            Check(!saved && MapMechanics.CanConfigure(Id), "Exit unlocks controls without restoring old preference");
            // neither controlled gameplay nor a diagnostic permission query establishes outside use
            MapMechanics.Parse(null, null, 3);
            MapMechanics.Resolve(Id, 0, hit, true);
            MapMechanics.Parse(null, tags, 3); saved = true; MapMechanics.CommitEntry();
            MapMechanics.RecordUse(Id);
            Check(!saved && writes == 2, "Queries and tagged-map use leave map-only preferences eligible for disabling");
            MapMechanics.ClearWorld(); MapMechanics.Parse(null, null, 3); MapMechanics.CommitEntry();
            MapMechanics.RecordUse(Id);
            historyField.SetValue(null, new MapMechanicPreferences(historyPath));
            MapMechanics.ClearWorld(); MapMechanics.Parse(null, tags, 3); saved = true; MapMechanics.CommitEntry();
            Check(saved && writes == 2 && !MapMechanics.CanConfigure(Id), "Outside use survives reload and preserves preference while author controls the map");
            Check(!MapMechanics.Resolve(Id, 0, hit, saved).Enabled, "Protected global enable cannot bypass an unmarked controlled screen");
            MapMechanics.ClearWorld();
            Check(saved, "Leaving a controlled map retains the protected enable value");
            PreferenceHistoryTests(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "map-preferences"));
            MapMechanics.ClearWorld();
            MapMechanics.DeclareScreen(Id, 0, MapMechanicMode.On, "pixel");
            Invalid("<Mechanics version='1'><Mechanic id='test.mechanic'><Screens from='1' mode='off'/></Mechanic></Mechanics>");
            MapMechanics.ClearWorld();
            Invalid("<Mechanics version='1'><Mechanic id='missing'/></Mechanics>");
            Invalid("<Mechanics version='1'><Mechanic id='test.mechanic'><Screens from='4' mode='on'/></Mechanic></Mechanics>");
            Invalid("<Mechanics version='1'><Mechanic id='test.mechanic'><Screens from='1' mode='on'><Parameter name='strength' value='NaN'/></Screens></Mechanic></Mechanics>");
            Invalid("<Mechanics version='1'><Mechanic id='test.mechanic'><Zone id='a' screen='1' x='0' y='0' width='50' height='50'/><Zone id='b' screen='1' x='10' y='10' width='50' height='50'/></Mechanic></Mechanics>");
            MapMechanics.Parse(XElement.Parse("<Mechanics version='1'><Mechanic id='test.mechanic'><Zone id='a' screen='1' x='0' y='0' width='50' height='50'/></Mechanic></Mechanics>"), null, 3);
            Check(MapMechanics.NeedsController(Id, false), "Zone-only maps prepare passive controllers");
            Check(MapMechanics.Resolve(Id, 0, hit, false).Enabled, "Unrestricted legacy map permits explicit authored area");
            MapMechanics.ClearWorld();
            string root = Path.GetFullPath("map-marker-fixture");
            MapMechanics.RecordDecode(root, new object());
            MapMechanics.DeclareScreen(Id, 0, MapMechanicMode.On, "late native decode");
            var decoded = MapMechanics.CaptureDecoded(root);
            Check(MapMechanics.CaptureDecoded(root + "-other").Length == 0, "Decoded pixels never transfer to another world");
            MapMechanics.ClearWorld(); MapMechanics.RestoreDecoded(root, decoded); MapMechanics.Parse(null, tags, 3);
            Check(MapMechanics.Resolve(Id, 0, hit, false).Enabled, "Synchronous fallback retains already decoded native marker metadata");
            MapMechanics.RecordDecode(root, new object()); MapMechanics.Parse(null, tags, 3);
            Check(!MapMechanics.Resolve(Id, 0, hit, false).Enabled, "Fresh geometry decode discards old markers, including on the same map");
            MapMechanics.ClearWorld();
        }

        private static void PreferenceHistoryTests(string root)
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "preferences.xml");
            var store = new MapMechanicPreferences(path);
            store.Initialize("new", false); store.Initialize("existing", true);
            Check(!File.Exists(path) && !store.Protected("new") && store.Protected("existing"), "Migration stages fresh defaults separately from existing enabled settings");
            store.Flush(); store = new MapMechanicPreferences(path);
            store.Initialize("new", true);
            Check(!store.Protected("new"), "Later default-setting writes do not overwrite known map-only history");
            store.Protect("new"); store.Flush(); store = new MapMechanicPreferences(path);
            Check(store.Protected("new") && store.Protected("existing") && store.Protected("unknown"), "Protection is durable and missing provider evidence is conservative");

            var field = typeof(MapMechanics).GetField("preferences", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(null, store);
            MapMechanics.InitializePreference("test.manual", false);
            MapMechanics.InitializePreference("test.other", false);
            MapMechanics.Register("test", "test.manual", () => { });
            MapMechanics.Register("test", "test.other", () => { });
            MapMechanics.Parse(null, new[] { MapMechanics.ControlledTagPrefix + "test.other" }, 1);
            MapMechanics.RecordUserEnable("test.manual", true);
            MapMechanics.RecordUserEnable("test.other", true);
            Check(store.Protected("test.manual") && !store.Protected("test.other"), "Explicit enable protects only that mechanic and cannot bypass its own tag");
            MapMechanics.RecordUserEnable("test.manual", false);
            Check(store.Protected("test.manual"), "Turning a preference off never forgets earlier outside use");
            MapMechanics.ClearWorld(); MapMechanics.Parse(null, null, 1);
            MapMechanics.RecordUse("test.other");
            Check(!store.Protected("test.other"), "A prepared but unstarted world cannot establish actual use");
            MapMechanics.CommitEntry(); MapMechanics.RecordUse("test.other");
            Check(store.Protected("test.other"), "Actual outside use establishes independent protection");
            MapMechanics.ClearWorld();

            File.WriteAllText(path, "broken history");
            var damaged = new MapMechanicPreferences(path);
            damaged.Initialize("fresh", false); damaged.Flush();
            Check(damaged.Protected("fresh") && File.ReadAllText(path) == "broken history", "Unreadable history preserves preferences and the damaged file");
            string conflictPath = Path.Combine(root, "conflict.xml");
            var conflict = new MapMechanicPreferences(conflictPath); conflict.Initialize("fresh", false);
            File.WriteAllText(conflictPath, "external edit"); conflict.Flush();
            Check(conflict.Protected("fresh") && File.ReadAllText(conflictPath) == "external edit", "Failed history commit cannot authorize destructive preference writes");
        }
    }
}
