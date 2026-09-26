using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void Reject(Action action, string message)
        { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Require(rejected, message); }
        private static void GimmickRegression(string game)
        {
            Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"));
            GimmickBlocks.Install();
            var factory = new UnknownProvider.Factory();
            LevelManager.RegisterBlockFactory(factory);
            GimmickBlocks.FactoryBoundary(); // also catches inlined native registration
            var material = factory.GetBlock(UnknownProvider.Factory.Code, new Rectangle(0, 320, 480, 40), null, null, 0, 0, 0);
            string id = Gimmicks.BlockId(typeof(UnknownProvider.Factory), UnknownProvider.Factory.Code);
            Require(Gimmicks.Entries.ContainsKey(id), "A newly registered unknown factory is observed through the real Harmony hook: " + GimmickBlocks.HookStatus);
            GimmickBlocks.DiscoverPalette(new[] { UnknownProvider.Factory.Code });
            Require(factory.Calls == 1, "Discovery must not replay GetBlock");
            var entry = Gimmicks.Entries[id];
            var copy = (UnknownProvider.Material)GimmickBlocks.Copy(entry.Template, new Rectangle(24, -350, 16, 24));
            Require(copy.Parameter == 42 && ((IBlock)copy).GetRect() == new Rectangle(24, -350, 16, 24), "Native clone preserves unknown type and scalar parameters");
            Require(material.GetRect() == new Rectangle(0, 320, 480, 40), "Clone leaves the original geometry untouched");
            string reason;
            Require(!GimmickBlocks.CanCopy(new UnknownProvider.OwnedMaterial(Rectangle.Empty), out reason), "Reference-owning blocks cannot be transplanted");
            Require(!GimmickBlocks.CanCopy(null, out reason), "Screen markers do not become colliders");
            IBlock solid = new BoxBlock(new Rectangle(0, 320, 480, 40));
            IBlock ice = new IceBlock(new Rectangle(16, 280, 16, 8));
            IBlock zone = new SandBlock(new Rectangle(80, 280, 16, 40));
            var source = new[] { solid, ice, zone };
            var rule = new GimmickRule { Id = id, Enabled = true };
            var replaced = GimmickSession.Build(source, 0, entry, rule);
            Require(replaced[0] is UnknownProvider.Material && ReferenceEquals(replaced[1], ice) && ReferenceEquals(replaced[2], zone), "Ordinary solid selection");
            rule.Application = GimmickApplication.SolidTerrain;
            replaced = GimmickSession.Build(source, 0, entry, rule);
            Require(replaced[0] is UnknownProvider.Material && replaced[1] is UnknownProvider.Material && ReferenceEquals(replaced[2], zone), "Blocking terrain selection uses native collision, not factory IsSolid");
            rule.Application = GimmickApplication.ExistingBlocks;
            Require(GimmickSession.Build(source, 0, entry, rule).All(b => b is UnknownProvider.Material), "Existing blocks includes nonblocking zones");
            var slope = new SlopeBlock(new Rectangle(10, 10, 20, 20), SlopeType.TopLeft);
            Reject(() => GimmickSession.Build(new IBlock[] { slope }, 0, entry, rule), "Slope conversion requires explicit opt-in");
            rule.ConvertSlopes = true;
            Require(GimmickSession.Build(new IBlock[] { slope }, 0, entry, rule)[0] is UnknownProvider.Material, "Explicit slope conversion");
            rule.Application = GimmickApplication.FillSpace;
            Require(GimmickSession.Build(new IBlock[0], 2, entry, rule)[0].GetRect() == new Rectangle(0, -720, 480, 360), "Fill includes air in native world coordinates");
            rule.Application = GimmickApplication.Overlay;
            Reject(() => GimmickSession.Build(source, 0, entry, rule), "Solid overlay refused");
            var water = new GimmickEntry { Template = new WaterBlock(Rectangle.Empty) };
            replaced = GimmickSession.Build(source, 0, water, rule);
            Require(replaced.Length == 4 && ReferenceEquals(replaced[0], solid), "Nonblocking overlay retains native support terrain");
            rule.FirstScreen = 2; rule.LastScreen = 3;
            Require(ReferenceEquals(GimmickSession.Build(source, 0, entry, rule), source) && GimmickSession.InRange(rule, 2) && !GimmickSession.InRange(rule, 3), "Inclusive one-based region ranges");

            GimmickStateRegression();
            GimmickContactStateRegression();
            GimmickSessionRegression(entry);
            GimmickMapRegression();
            GimmickConstructionRegression();
            GimmickStartupRegression();
            GimmickRedesignRegression();
            GimmickNavigationRegression();
            var old = Settings.Current.GimmickPins;
            try
            {
                Settings.Current.GimmickPins = null; Require(Gimmicks.Pins.Length == 3, "Legacy preferences seed three default pins");
                Settings.Current.GimmickPins = new string[0]; Require(Gimmicks.Pins.Length == 0, "An explicitly empty pin set stays empty");
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Preferences));
                using (var text = new StringWriter())
                {
                    serializer.Serialize(text, new Preferences { GimmickPins = new string[0], GimmickRules = new[] { new GimmickRule { Id = id, ConvertSlopes = true, FirstScreen = 3, LastScreen = 7, Contract = "fixture", Value = "Heavy" } } });
                    var saved = (Preferences)serializer.Deserialize(new StringReader(text.ToString()));
                    Require(saved.GimmickPins.Length == 0 && saved.GimmickRules[0].ConvertSlopes && saved.GimmickRules[0].LastScreen == 7 && saved.GimmickRules[0].Value == "Heavy", "Preferences roundtrip preserves pins, state value and shape/range policy");
                }
            }
            finally { Settings.Current.GimmickPins = old; }
            Console.WriteLine("[OK] Universal gimmicks: unknown provider observation, no factory replay, typed copies, five modes, slope consent, state leases/getters, transactional geometry, XNB regions and preferences");
        }
        private static void GimmickStateRegression()
        {
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            {
                var player = ResumePlayer(); var behaviour = new UnknownProvider.Behaviour();
                player.m_body.RegisterBehaviour(behaviour);
                GimmickStates.Discover(player);
                Require(Gimmicks.Entries.Values.Any(e => e.Slot != null && e.Label.Contains("transition") && e.Label.Contains("target")), "Nested unknown behavior state discovered");
                Require(!Gimmicks.Entries.Keys.Any(k => k.Contains("DormantSubsystem")), "Static constructors are not run during discovery");
                var slot = Gimmicks.Entries.Values.Single(e => e.Slot != null && e.Slot.Field != null && e.Slot.Field.DeclaringType == typeof(UnknownProvider.Control) && e.Slot.Field.Name.Contains("Held")).Slot;
                UnknownProvider.Control.Held = false;
                using (var state = new StateOverride(slot, new GimmickRule { Value = "True" }, 3))
                {
                    state.Tick(); UnknownProvider.Control.Held = false;
                    Require(UnknownProvider.Control.ReadHeld(), "Generic getter stays forced after provider resets backing field later in the tick");
                }
                Require(!UnknownProvider.Control.ReadHeld(), "Getter is passive after release");
                var enumSlot = Gimmicks.Entries.Values.Single(e => e.Slot != null && e.Slot.Field != null && e.Slot.Field.DeclaringType == typeof(UnknownProvider.Control) && e.Slot.ValueType == typeof(UnknownProvider.Form)).Slot;
                using (var state = new StateOverride(enumSlot, new GimmickRule { Value = "Heavy" }, 3))
                { state.Tick(); UnknownProvider.Control.Mode = UnknownProvider.Form.Travel; Require(UnknownProvider.Control.ReadMode() == UnknownProvider.Form.Heavy, "Generic enum getter forcing"); }
                Require(UnknownProvider.Control.ReadMode() == UnknownProvider.Form.Travel, "Release does not overwrite a distinct value last written by the owner");
                var nested = Gimmicks.Entries.Values.Single(e => e.Slot != null && ReferenceEquals(e.Slot.Target, behaviour.transition)).Slot;
                using (var state = new StateOverride(nested, new GimmickRule { Value = "True" }, 3)) { state.Tick(); Require(behaviour.transition.target, "Raw nested field forced"); }
                Require(!behaviour.transition.target, "Raw nested field restored");
                var set = Gimmicks.Entries.Values.Single(e => e.Slot != null && e.Slot.ScreenSet && e.Slot.Field.DeclaringType == typeof(UnknownProvider.Factory)).Slot;
                UnknownProvider.Factory.ScreenIndices.Clear(); UnknownProvider.Factory.ScreenIndices.Add(1);
                using (var state = new StateOverride(set, new GimmickRule { FirstScreen = 2, LastScreen = 3, Value = "True" }, 4))
                { state.Tick(); Require(UnknownProvider.Factory.ScreenIndices.SetEquals(new[] { 1, 2 }), "Index set range uses zero-based screen indices"); }
                Require(UnknownProvider.Factory.ScreenIndices.SetEquals(new[] { 1 }), "Set restores pre-existing membership");
                using (var state = new StateOverride(set, new GimmickRule { FirstScreen = 2, LastScreen = 3, Value = "False" }, 4)) { state.Tick(); Require(UnknownProvider.Factory.ScreenIndices.Count == 0, "Index set can force absence"); }
                Require(UnknownProvider.Factory.ScreenIndices.SetEquals(new[] { 1 }), "Force-absence restores membership");
                var nativeScreens = new[] {
                    new LevelScreen(0, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
                    new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null)
                };
                var native = new StateSlot { Target = nativeScreens[0], ScreenTargets = nativeScreens.Cast<object>().ToArray(),
                    Field = typeof(LevelScreen).GetField("m_wind_enabled", Flags), ValueType = typeof(bool) };
                using (var state = new StateOverride(native, new GimmickRule { FirstScreen = 2, LastScreen = 2, Value = "True" }, 2))
                { state.Tick(); Require(!nativeScreens[0].WindEndabled && nativeScreens[1].WindEndabled, "Native screen state respects selected scope"); }
                Require(!nativeScreens[1].WindEndabled, "Native screen state restores authored flag");
            }
        }
        private static void GimmickContactStateRegression()
        {
            var saved = Settings.Current.GimmickRules;
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            try {
                IBlock[] original = { new BoxBlock(new Rectangle(0, 320, 480, 40)) };
                var screens = Scene(original); GimmickBlocks.Screens.SetValue(null, screens);
                typeof(JumpKing.Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                var player = ResumePlayer(); player.m_body.Position = new Vector2(180, 200);
                var handler = new UnknownProvider.PortableHandler(LevelManager.Instance);
                player.m_body.RegisterBlockBehaviour(typeof(UnknownProvider.PortableZone), handler);
                UnknownProvider.Registration.Published = handler;
                GimmickStates.Discover(player);
                var entries = Gimmicks.Entries.Values.Where(e => e.Slot != null &&
                    (ReferenceEquals(e.Slot.Target, handler) || ReferenceEquals(e.Slot.Target, handler.contact)
                    || e.Slot.Field.DeclaringType == typeof(UnknownProvider.PortableHandler))).ToArray();
                Require(entries.Any(e => ReferenceEquals(e.Slot.Target, handler.contact)) && entries.Any(e => e.Slot.ScreenSet)
                    && entries.Any(e => e.Id.StartsWith("state:static:")), "Contact diagnostics include nested, collection and static alias paths");
                using (var session = new GimmickSession(player))
                foreach (var entry in entries) {
                    Require(entry.Slot.ReadOnlyReason != null, "Every handler-owned slot is read-only: " + entry.Id);
                    var rule = new GimmickRule { Id = entry.Id, Enabled = true, Value = "True", Contract = entry.Slot.Contract };
                    Reject(() => new StateOverride(entry.Slot, rule, 1), "Direct contact override must not acquire a getter or write state");
                    Reject(() => session.Apply(new[] { rule }), "Saved contact rules are rejected before application");
                    Reject(() => Gimmicks.Configure(rule), "Pinned/direct configuration cannot force contact");
                    Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Rejected contact rule leaves geometry unchanged");
                }
                var chosen = entries.First(e => ReferenceEquals(e.Slot.Target, handler) && e.Slot.Getter != null);
                GimmickStates.Discover(player); // Session teardown above deliberately invalidates live catalogue slots.
                var page = new GimmickPage(null, new JumpKing.PauseMenu.GuiFormat(), false);
                PageCall(page, "Details", chosen.Id);
                var diagnosticRows = (JKRuntime.UI.UiListItem[])typeof(JKRuntime.UI.UiList).GetField("items", Flags).GetValue(PageField<JKRuntime.UI.UiList>(page, "list"));
                Require(diagnosticRows.Any(row => row.Id == "materials") && !diagnosticRows.Any(row => row.Id == "apply" || row.Id == "enabled" || row.Id == "value" || row.Id == "pin"),
                    "Contact diagnostic UI routes to real materials without activation controls");
                Require(GimmickSearch.Ready(chosen) == "Read-only", "Contact diagnostics cannot advertise Ready in search");
                PageChoose(page, "materials");
                var materialRows = (JKRuntime.UI.UiListItem[])typeof(JKRuntime.UI.UiList).GetField("items", Flags).GetValue(PageField<JKRuntime.UI.UiList>(page, "list"));
                Require(materialRows.Length > 0 && materialRows.All(row => Gimmicks.Entries.ContainsKey(row.Id) && Gimmicks.Entries[row.Id].Kind == "Block"
                    && Gimmicks.Entries[row.Id].Owner == chosen.Owner), "Material shortcut shows only the selected provider's blocks");
                Settings.Current.GimmickRules = new[] { new GimmickRule { Id = chosen.Id, Enabled = true, Value = "True", Contract = chosen.Slot.Contract } };
                using (var scope = new JKRuntime.RuntimeScope()) {
                    ModEntry.PrepareAttempt(scope);
                    Require(!((System.Collections.Generic.HashSet<MethodInfo>)typeof(GimmickGetters).GetField("prepared", Flags).GetValue(null)).Contains(chosen.Slot.Getter),
                        "Legacy contact rule does not install a getter hook during preparation");
                    using (var session = new GimmickSession(player, PreparedGimmicks())) {
                        GimmickTick(session)(1f / 60f);
                        Require(!session.IsEnabled(chosen.Id) && Gimmicks.Status.Contains("Read-only collision"), "Legacy saved contact override is rejected on startup");
                    }
                }
                Require(!handler.IsPlayerOnBlock && !handler.contact.target && handler.contactScreens.Count == 0
                    && !UnknownProvider.PortableHandler.PreviousContact, "Rejected contact overrides leave all provider state untouched");
            }
            finally { Settings.Current.GimmickRules = saved; UnknownProvider.Registration.Published = null; }
        }
        private static void GimmickSessionRegression(GimmickEntry entry)
        {
            using (new SpriteGameFixture())
            using (new WalkInputFixture())
            {
                IBlock[] original = { new BoxBlock(new Rectangle(0, 320, 480, 40)) };
                var screens = Scene(original); GimmickBlocks.Screens.SetValue(null, screens);
                typeof(JumpKing.Camera).GetField("_current_screen", Flags).SetValue(null, 0);
                var player = ResumePlayer(); player.m_body.Position = new Vector2(180, 200);
                using (var session = new GimmickSession(player))
                {
                    var rule = new GimmickRule { Id = entry.Id, Enabled = true };
                    session.Apply(new[] { rule });
                    var applied = (IBlock[])GimmickBlocks.Hitboxes.GetValue(screens[0]);
                    Require(applied[0] is UnknownProvider.Material && original[0].GetType() == typeof(BoxBlock), "Session installs generic replacement without editing source blocks");
                    object snapshot = session.Capture();
                    var bad = Gimmicks.Copy(rule); bad.Application = GimmickApplication.FillSpace;
                    Reject(() => session.Apply(new[] { bad }), "Player-overlapping fill refused atomically");
                    Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), applied), "Failed application leaves the previous world intact");
                    session.Validate(snapshot);
                    session.Apply(new GimmickRule[0]);
                    Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Disable restores exact original collision array");
                    Reject(() => session.Validate(snapshot), "Old snapshot cannot cross configuration changes");
                    session.Apply(new[] { rule });
                }
                Require(ReferenceEquals(GimmickBlocks.Hitboxes.GetValue(screens[0]), original), "Unload restores exact original geometry");
            }
        }
        private static void GimmickMapRegression()
        {
            var regions = GimmickMaps.ParseRegions(XDocument.Parse("<LocationSettings><locations><Location><name>{color}Upper</name><start>3</start><end>7</end></Location><Location><name>Lower</name><start>1</start><end>4</end></Location></locations></LocationSettings>"));
            Require(regions[0].Number == 1 && regions[0].First == 3 && regions[1].Last == 4 && regions[0].Name == "Upper", "Region author order and overlap preserved");
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream); writer.Write(new[] { (byte)'X', (byte)'N', (byte)'B', (byte)'w', (byte)5, (byte)0 }); writer.Write(0);
                writer.Write((byte)1); writer.Write("Microsoft.Xna.Framework.Content.Texture2DReader"); writer.Write(0); writer.Write((byte)0); writer.Write((byte)1);
                writer.Write(0); writer.Write(120); writer.Write(90); writer.Write(1); writer.Write(120 * 90 * 4);
                for (int y = 0; y < 90; y++) for (int x = 0; x < 120; x++) writer.Write(0xff000000u | (uint)(x / 60 * 2 + y / 45 + 1));
                stream.Position = 6; writer.Write((int)stream.Length); stream.Position = 0;
                var colours = GimmickMaps.ReadAtlas(new BinaryReader(stream));
                Require(colours[0xff000002].SetEquals(new[] { 2 }) && colours[0xff000003].SetEquals(new[] { 3 }), "XNB atlas reads native column-major screen ordering");
                stream.SetLength(stream.Length - 4); stream.Position = 6; writer.Write((int)stream.Length); stream.Position = 0;
                bool rejected = false; try { GimmickMaps.ReadAtlas(new BinaryReader(stream)); } catch (EndOfStreamException) { rejected = true; }
                Require(rejected, "Truncated atlas rejected");
            }
        }
    }
}
