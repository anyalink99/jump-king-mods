using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.Level;
using JumpKing.BodyCompBehaviours;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void GimmickRedesignRegression()
        {
            IBlock terrain = new BoxBlock(new Rectangle(0, 320, 480, 40));
            IBlock slope = new SlopeBlock(new Rectangle(30, 280, 40, 40), SlopeType.TopLeft);
            var zone = new SandBlock(new Rectangle(80, 280, 20, 40));
            var source = new IBlock[] { terrain, slope, zone };
            var ice = new GimmickEntry { Id = "test:ice", Kind = "Block", Template = new IceBlock(Rectangle.Empty) };
            var water = new GimmickEntry { Id = "test:water", Kind = "Block", Template = new WaterBlock(Rectangle.Empty) };
            var rule = new GimmickRule { Id = ice.Id, Enabled = true, Application = GimmickApplication.Auto };
            var surface = GimmickSession.Build(source, 0, ice, rule);
            Require(surface[0].GetType() == typeof(IceBlock) && surface[0].GetRect() == terrain.GetRect() && ReferenceEquals(surface[1], slope) && ReferenceEquals(surface[2], zone), "Auto surface preserves shape, slopes and zones");
            var filled = GimmickSession.Build(surface, 0, water, rule);
            Require(filled.Take(source.Length).SequenceEqual(surface) && filled.Skip(source.Length).All(b => b.GetType() == typeof(WaterBlock)), "Auto medium retains exact existing objects and concrete water type");
            var occupancy = new int[480 * 360];
            foreach (var block in filled.Skip(source.Length)) {
                var r = block.GetRect(); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) occupancy[y * 480 + x]++;
            }
            for (int y = 0; y < 360; y++) for (int x = 0; x < 480; x++) {
                Rectangle overlap; var pixel = new Rectangle(x, y, 1, 1);
                // Independent analytic interior for the fixture triangle (30,320),
                // (70,280), (70,320); native edge queries alone omit its interior.
                bool inside = x >= 30 && x < 70 && y >= 280 && y < 320 && x + y + 1 >= 350;
                bool solid = inside || terrain.Intersects(pixel, out overlap) == BlockCollisionType.Collision_Blocking || slope.Intersects(pixel, out overlap) == BlockCollisionType.Collision_Blocking;
                Require(occupancy[y * 480 + x] == (solid ? 0 : 1), "Empty fill exactly partitions native solid/slope occupancy without gaps or overlap at " + x + "," + y);
            }
            rule.Application = GimmickApplication.FillEmpty;
            Require(GimmickSession.Build(new IBlock[0], 2, water, rule).Single().GetRect() == new Rectangle(0, -720, 480, 360), "Empty-space world coordinates across screens");
            Reject(() => GimmickSession.Build(source, 0, ice, rule), "Blocking material cannot fill empty space as a medium");
            rule.Application = GimmickApplication.ReplaceMedium;
            var medium = GimmickSession.Build(source, 0, water, rule);
            Require(ReferenceEquals(medium[0], terrain) && ReferenceEquals(medium[1], slope) && medium[2] is WaterBlock, "Replace-medium leaves blocking geometry intact");
            rule.Screens = new[] { 1, 3 };
            Require(GimmickSession.InRange(rule, 0) && !GimmickSession.InRange(rule, 1) && GimmickSession.InRange(rule, 2), "Disjoint target screen union");
            var copied = Gimmicks.Copy(rule); copied.Screens[0] = 8; Require(rule.Screens[0] == 1, "Draft copy owns its screen selection");
            rule.Screens = new int[0]; Require(!GimmickSession.InRange(rule, 0), "Deselecting all regions does not target the entire map");
            var slopeEntry = new GimmickEntry { Kind = "Block", Template = slope };
            rule.Screens = null; rule.Application = GimmickApplication.Auto;
            Reject(() => GimmickSession.Build(source, 0, slopeEntry, rule), "Auto never turns rectangular terrain into slope polygons");
            GimmickSearchRegression(); GimmickWindRegression(); GimmickDraftRegression(ice);
            Console.WriteLine("[OK] Semantic placement: exact 172800-pixel fill/slope partition, surface identity, typed media, screen union, wind ownership, search facets and draft isolation");
        }
        private static void GimmickSearchRegression()
        {
            Color? colour;
            Require(GimmickSearch.Colour("#12abef", out colour) && colour == new Color(18, 171, 239), "HEX parser");
            Require(GimmickSearch.Colour("rgb(18, 171, 239)", out colour) && colour == new Color(18, 171, 239), "RGB parser");
            Require(!GimmickSearch.Colour("256,1,2", out colour) && !GimmickSearch.Colour("#123", out colour), "Invalid colour is not interpreted as an empty filter");
            var blue = new Color(1, 2, 3); var red = Color.Red;
            var records = new[] {
                new GimmickSearchRecord { Entry = new GimmickEntry { Id = "a", Label = "Deep Water", Owner = "A", Family = "Medium", Geometry = "Nonblocking", Colour = blue }, Text = "deep water a", Readiness = "Ready" },
                new GimmickSearchRecord { Entry = new GimmickEntry { Id = "b", Label = "Water", Owner = "B", Family = "Medium", Geometry = "Nonblocking", Colour = red }, Text = "water b", Readiness = "Ready" },
                new GimmickSearchRecord { Entry = new GimmickEntry { Id = "c", Label = "Unknown", Owner = "Unresolved", Kind = "Colour", Colour = blue }, Text = "unknown", Readiness = "Unknown" }
            };
            var mapA = new GimmickMap { Id = "a", Title = "A", Regions = new[] { new GimmickRegion { Number = 1, First = 1, Last = 2 } } };
            var mapB = new GimmickMap { Id = "b", Title = "B", Regions = new[] { new GimmickRegion { Number = 1, First = 2, Last = 3 } } };
            mapA.Colours[blue.PackedValue] = new HashSet<int> { 1 }; mapB.Colours[red.PackedValue] = new HashSet<int> { 2 };
            string error; var maps = new[] { mapA, mapB };
            var q = new GimmickSearchPreferences { Text = "water", Providers = new[] { "A", "B" }, Regions = new[] { "a|1" } };
            Require(GimmickSearch.Filter(records, q, maps, out error).Single().Entry.Id == "a", "OR within providers, AND with map-qualified region; equal region numbers are distinct");
            q.Regions = null; q.Text = null; q.Providers = null; q.Colour = "1,2,3";
            Require(GimmickSearch.Filter(records, q, maps, out error).Length == 2, "Unknown colours participate in exact colour filtering");
            q.Colour = "bad"; Require(GimmickSearch.Filter(records, q, maps, out error).Length == 0 && error != null, "Invalid filter produces explicit error");
            var text = new GimmickText(); text.Open("wind water"); text.Move(0,false); text.Move(4,true);
            text.Insert("ice"); Require(text.Text == "ice water" && text.Caret == 3, "Text selection replacement");
            text.Move(text.Text.Length, false); text.Delete(true); Require(text.Text == "ice wate", "Text backspace");
            text.Move(0, false); text.Delete(false); Require(text.Text == "ce wate", "Text forward delete");
            text.Insert(new string('x', 257)); Require(text.Text == "ce wate" && text.Error != null, "Bounded paste preserves text on failure");
            text.Open("search");
            Require(JKRuntime.UI.UIApi.IsTextInputActive, "Custom search editor acquires shared keyboard ownership");
            text.Open("renamed");
            Require(JKRuntime.UI.UIApi.IsTextInputActive, "Reopening search keeps exactly one capture");
            text.Dispose(); text.Dispose();
            Require(!JKRuntime.UI.UIApi.IsTextInputActive, "Custom editor teardown releases shared keyboard ownership exactly once");
            Require(GimmickSearch.ScreenSelection("1,3-5,4").SequenceEqual(new[] { 1, 3, 4, 5 }), "Explicit target ranges merge duplicates");
            Reject(() => GimmickSearch.ScreenSelection("5-3"), "Descending ranges rejected");
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Preferences));
            using (var writer = new System.IO.StringWriter()) {
                var savedQuery = new GimmickSearchPreferences { Text = "water", Colour = "#010203", Regions = new[] { "a|1", "b|1" } };
                serializer.Serialize(writer, new Preferences { GimmickSearch = savedQuery, SavedSearches = new[] { savedQuery }, GimmickRules = new[] {
                    new GimmickRule { Id = "config:wind", SourceId = GimmickWind.Id, Name = "Upper wind", WindStrength = 2, WindImmediate = true, Screens = new[] { 1, 3 } }
                } });
                var saved = (Preferences)serializer.Deserialize(new System.IO.StringReader(writer.ToString()));
                Require(saved.GimmickRules[0].SourceId == GimmickWind.Id && saved.GimmickRules[0].Screens.SequenceEqual(new[] { 1, 3 }) && saved.GimmickRules[0].WindStrength == 2 && saved.GimmickSearch.Regions.Length == 2 && saved.SavedSearches.Length == 1, "Configured instances, wind controls, union scopes and saved queries roundtrip XML");
            }
        }
        private static void GimmickWindRegression()
        {
            using (new SpriteGameFixture()) using (new WalkInputFixture()) {
                var player = ResumePlayer(); var behaviour = player.m_body.GetBehaviourList().OfType<WindVelocityUpdateBehaviour>().First();
                var screens = new[] {
                    new LevelScreen(0, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
                    new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), true, new TeleportLink[0], 4, true)
                };
                var latch = typeof(WindVelocityUpdateBehaviour).GetField("m_wind_enabled", Flags);
                var intensity = typeof(LevelScreen).GetField("m_wind_intensity", Flags);
                var direction = typeof(LevelScreen).GetField("m_wind_direction", Flags);
                var rule = new GimmickRule { Id = GimmickWind.Id, Enabled = true, Value = "Right", WindStrength = 2, WindImmediate = true, Screens = new[] { 1 } };
                typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                using (var wind = new GimmickWind(screens, player, rule)) {
                    Require(screens[0].WindEndabled && (float)intensity.GetValue(screens[0]) == 16 && Equals(direction.GetValue(screens[0]), false), "Wind applies native strength/direction in no-wind map");
                    Require((float)intensity.GetValue(screens[1]) == 4 && Equals(direction.GetValue(screens[1]), true), "Wind respects scope");
                    wind.Tick(); Require((bool)latch.GetValue(behaviour), "Immediate wind arms native midair latch");
                    typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 1); wind.Tick(); Require(!(bool)latch.GetValue(behaviour), "Leaving wind scope releases held latch");
                }
                Require(!screens[0].WindEndabled && (float)intensity.GetValue(screens[0]) == 0 && direction.GetValue(screens[0]) == null, "Wind restores authored metadata");
                rule.WindStrength = 0;
                using (var wind = new GimmickWind(screens, player, rule)) Require(!screens[0].WindEndabled, "Zero strength is disabled, not native default intensity");
                rule.WindStrength = float.NaN; Reject(() => GimmickWind.Validate(rule), "Wind rejects NaN");
                Gimmicks.Initialize(); GimmickBlocks.Screens.SetValue(null, screens);
                var copy = new GimmickRule { Id = "config:wind-test", SourceId = GimmickWind.Id, Name = "Second wind", Enabled = true, Value = "Left", WindStrength = 3, Screens = new[] { 2 } };
                Gimmicks.AddConfiguration(copy); rule.WindStrength = 1;
                using (var session = new GimmickSession(player)) {
                    session.Apply(new[] { rule, copy });
                    Require(screens[0].WindIntensity == 8 && screens[1].WindIntensity == 24, "Independent wind configurations apply to separate regions");
                    var overlap = Gimmicks.Copy(copy); overlap.Screens = new[] { 1 };
                    Reject(() => session.Apply(new[] { rule, overlap }), "Overlapping wind configurations rejected atomically");
                    Require(screens[0].WindIntensity == 8 && screens[1].WindIntensity == 24, "Rejected wind edit preserves both active configurations");
                    session.Apply(new GimmickRule[0]);
                    Require(!screens[0].WindEndabled && screens[1].WindIntensity == 4, "Releasing configured wind copies restores both authored scopes");
                }
                Gimmicks.Entries.Remove(copy.Id);
            }
        }
        private static void GimmickDraftRegression(GimmickEntry entry)
        {
            Gimmicks.Add(entry); var old = Settings.Current.GimmickRules;
            try {
                Settings.Current.GimmickRules = new[] { new GimmickRule { Id = entry.Id, Application = GimmickApplication.Auto } };
                var page = new GimmickPage(null, new JumpKing.PauseMenu.GuiFormat(), false);
                typeof(GimmickPage).GetMethod("Details", Flags).Invoke(page, new object[] { entry.Id });
                typeof(GimmickPage).GetMethod("Change", Flags).Invoke(page, new object[] { entry.Id, new Action<GimmickRule>(r => { r.Enabled = true; r.Application = GimmickApplication.FillSpace; }) });
                Require(!Settings.Current.GimmickRules[0].Enabled && Settings.Current.GimmickRules[0].Application == GimmickApplication.Auto, "Editing enabled/mode never commits draft");
            } finally { Settings.Current.GimmickRules = old; Gimmicks.Entries.Remove(entry.Id); }
        }
    }
}
