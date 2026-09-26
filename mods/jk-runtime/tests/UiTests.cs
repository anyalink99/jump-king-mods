using System;
using System.IO;
using System.Xml.Serialization;
using BehaviorTree;
using JumpKing.Controller;
using JumpKing.PauseMenu.BT;
using JumpKing.PauseMenu;
using JumpKing.Util;
using System.Collections;

namespace JKRuntime.UI
{
    internal static class UiTests
    {
        private static int failures;

        private static void Main()
        {
            CapturesAChordOnFullRelease();
            LegacyCaptureKeepsOneButton();
            DoesNotFinishBetweenChordButtons();
            IgnoresOneEmptyPollBetweenButtons();
            VirtualButtonNeedsWholeChord();
            ResolvesVirtualBindingToPhysicalChord();
            SerializesChordSettings();
            SerializesMenuPreferences();
            GridNavigationUsesBothAxes();
            AdaptiveGridRows();
            InterruptedEmbeddedPageReleasesResources();
            CompactGridCancelReturnsOneLayer();
            CompactGridRequiresExplicitConfirmation();
            PinnedSettingsUsesVanillaMenuLayering();
            MenuMutationPreservesOpenChild();
            NativeMenuFramesDoNotRestoreConsumedInput();
            ReportsIndependentUiCapabilities();
            EquipmentDefinitionsKeepToggleStateSeparateFromActivation();
            CompactGridOwnsItsDrawableLifecycle();
            CompactGridHonorsCardVisibility();
            TimedMenuFeedbackRestoresItsLabel();
            ActionHintsFollowPhysicalBindings();
            CommandLayoutUsesContentWidths();
            FooterRowsRespectTheirFrame();
            OwnershipSurvivesReplacement();
            ModalSuspensionCooperates();
            PageTools();
            AdjustableDebugActions();
            SemanticFeedback();
            if (failures > 0) Environment.Exit(1);
            Console.WriteLine("[OK] UIApi+ tests");
        }

        private sealed class OwnedComponent : EntityComponent.Component { }
        private static void SemanticFeedback()
        {
            var cues = new System.Collections.Generic.List<UiSound>();
            UiSounds.TestPlayback = cues.Add;
            try
            {
                int selection = 0;
                UiSounds.Select(ref selection, 0); Assert(cues.Count == 0, "Repeated hover of the same selection is silent");
                UiSounds.Select(ref selection, 1); Assert(selection == 1 && cues.Count == 0, "Selection changes silently, matching native menus");
                UiSounds.Play(UiSound.Move); UiSounds.Play(UiSound.Back);
                Assert(cues.Count == 0, "Explicit navigation and back cues preserve native silence for existing consumers");
                cues.Clear(); int runs = 0;
                var action = new UiDebugActionDefinition("test.audio", "Tests", "Run", () => runs++);
                Assert(!DebugActionsPageNode.InvokeAction(action, UiAction.Left) && cues.Count == 0, "Irrelevant debug arrows are silent");
                DebugActionsPageNode.InvokeAction(action, UiAction.Confirm);
                Assert(runs == 1 && cues.Count == 1 && cues[0] == UiSound.Confirm, "Debug confirmation emits its missing success cue exactly once");
                cues.Clear();
                var disabled = new UiDebugActionDefinition("test.audio.disabled", "Tests", "No", () => runs++, () => false);
                Assert(!DebugActionsPageNode.InvokeAction(disabled, UiAction.Confirm) && runs == 1 && cues.Count == 0, "Disabled debug actions stay silent without execution, like native unavailable options");
                cues.Clear(); double value = 0;
                var number = new UiNumberControl(0, 10, 1, () => value, v => value = v);
                number.Set(-10); Assert(cues.Count == 0, "Clamped unchanged numeric controls do not repeat a sound");
                number.Set(3); Assert(value == 3 && cues.Count == 1 && cues[0] == UiSound.Change, "Numeric feedback follows a committed value change");
                cues.Clear();
                var list = new UiList(); list.SetItems(new[] { new UiListItem("only", "Only", () => runs++) });
                list.Update(UiInputRouter.FromAction(UiAction.Down)); Assert(cues.Count == 0, "Single-item list navigation is silent");
                list.Update(UiInputRouter.FromAction(UiAction.Confirm)); Assert(cues.Count == 1 && cues[0] == UiSound.Confirm, "Shared list activation supplies confirmation");
                cues.Clear();
                var command = new UiPageCommand(UiAction.Confirm, "Fail", () => { throw new InvalidOperationException("fixture"); });
                bool failed = false;
                try { command.Handle(UiInputRouter.FromAction(UiAction.Confirm)); } catch (InvalidOperationException) { failed = true; }
                Assert(failed && cues.Count == 1 && cues[0] == UiSound.Error, "Failed shared commands emit error instead of confirmation and preserve the exception");
                Assert(UIApi.Supports("ui-feedback-v1"), "Consumers can discover native feedback support");
                Assert(UiTheme.Text == Microsoft.Xna.Framework.Color.White && UiTheme.Disabled == Microsoft.Xna.Framework.Color.Gray && UiTheme.Muted != UiTheme.Disabled, "Normal, secondary and unavailable colors have separate roles");
            }
            finally { UiSounds.TestPlayback = null; }
        }
        private static void AdjustableDebugActions()
        {
            int count = 0, value = 100; bool available = true;
            var legacy = new UiDebugActionDefinition("test.debug.old", "Tests", "Legacy", delegate { count++; });
            DebugActionsPageNode.InvokeAction(legacy, UiAction.Left);
            DebugActionsPageNode.InvokeAction(legacy, UiAction.Confirm);
            Assert(count == 1 && legacy.GetLabel() == "Legacy" && legacy.Adjust == null && legacy.ConfirmLabel == "RUN", "Old debug actions retain their behavior and constructor");
            var option = new UiDebugActionDefinition("test.debug.value", "Tests", "Value", delegate { value = 100; },
                () => available, () => "Value: " + value, direction => value += direction * 5, "RESET");
            DebugActionsPageNode.InvokeAction(option, UiAction.Right);
            Assert(value == 105 && option.GetLabel() == "Value: 105", "Debug Right updates the displayed value");
            DebugActionsPageNode.InvokeAction(option, UiAction.Left);
            DebugActionsPageNode.InvokeAction(option, UiAction.Left);
            Assert(value == 95, "Debug Left decreases the value");
            available = false;
            Assert(!DebugActionsPageNode.InvokeAction(option, UiAction.Right) && value == 95, "Unavailable debug values cannot change");
            available = true; DebugActionsPageNode.InvokeAction(option, UiAction.Confirm);
            Assert(value == 100 && option.ConfirmLabel == "RESET", "Confirm retains the value's reset action");
        }
        private sealed class RecursiveComponent : EntityComponent.Component
        {
            internal bool Recurse, FailEnable;
            protected override void OnDisable() { if (Recurse) Gameplay.ComponentSuspension.Acquire("ui.tests.nested", this); }
            protected override void OnEnable() { if (FailEnable) throw new InvalidOperationException("enable failure"); }
        }
        private sealed class FailingPage : ScopedUiPage
        {
            internal int Released;
            protected override void OpenPage(RuntimeScope resources) { resources.Defer(() => Released++); throw new InvalidOperationException("open failure"); }
            public override void Update(UiInput input, float delta) { }
            public override void Draw() { }
        }
        private static void PageTools()
        {
            var page = new FailingPage(); try { page.OnOpen(); } catch (InvalidOperationException) { }
            page.OnClose(); Assert(page.Released == 1, "partial page open releases owned work once");
            int[][] wrapped = UiPageLayout.WrapCommands(100, new[] { 80, 60, 220, 40 });
            Assert(wrapped.Length == 4, "oversized command rows wrap instead of hiding commands");
            var layout = new UiPageLayout(new Microsoft.Xna.Framework.Rectangle(12, 12, 456, 336), 3, 2);
            Assert(layout.Content.Bottom < layout.Status.Top && layout.Status.Bottom < layout.Footers[0].Top, "content, status and all footer rows are disjoint");
            string[] lines = UiTheme.WrapLines("abcdefgh\nhi there", 4, s => s.Length);
            Assert(string.Join("|", lines) == "abcd|efgh|hi|ther|e", "long word starts on first line; explicit newline retained");
            double value = 1;
            var number = new UiNumberControl(0, 2, .25, () => value, v => value = v);
            number.Set(100); Assert(value == 2, "numeric clamp"); number.Set(.6); Assert(value == .5, "numeric step snapping");
            var list = new UiList(); var a = new UiListItem("a", "A"); var b = new UiListItem("b", "B");
            list.SetItems(new[] { a, b }); list.Select("b"); list.SetItems(new[] { b, a }); Assert(list.SelectedId == "b", "list selection follows stable IDs across reorder");
        }
        private static void ModalSuspensionCooperates()
        {
            foreach (bool modalFirst in new[] { true, false })
            {
                var component = new OwnedComponent(); component.Enabled = true;
                IDisposable other = null; RuntimeScope modal = null;
                if (modalFirst) modal = ModalHost.SuspendComponents(new[] { component });
                other = Gameplay.ComponentSuspension.Acquire("ui.tests.other", component);
                if (!modalFirst) modal = ModalHost.SuspendComponents(new[] { component });
                modal.Dispose();
                Assert(!component.Enabled && Gameplay.ComponentSuspension.IsSuspended(component), "modal leaves other owner's suspension intact");
                other.Dispose(); Assert(component.Enabled, "last owner restores original enabled state");
            }
            var disabled = new OwnedComponent();
            ModalHost.SuspendComponents(new[] { disabled }).Dispose();
            Assert(!disabled.Enabled, "initially disabled component stays disabled");
            var recursive = new RecursiveComponent(); recursive.Enabled = true; recursive.Recurse = true;
            bool rejected = false;
            try { Gameplay.ComponentSuspension.Acquire("ui.tests", recursive); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && recursive.Enabled && !Gameplay.ComponentSuspension.IsSuspended(recursive), "recursive disable is rejected without orphaning suspension");
            recursive.Recurse = false; var lease = Gameplay.ComponentSuspension.Acquire("ui.tests", recursive); recursive.FailEnable = true;
            try { lease.Dispose(); } catch (InvalidOperationException) { }
            Assert(!recursive.Enabled && Gameplay.ComponentSuspension.IsSuspended(recursive), "failed restore retains retryable suspension");
            recursive.FailEnable = false; lease.Dispose(); Assert(recursive.Enabled, "retry restores component after enable failure");
        }

        private static void OwnershipSurvivesReplacement()
        {
            var first = new UiRegistrationScope("ui.tests");
            var second = new UiRegistrationScope("ui.tests");
            Func<int[]> read = delegate { return new int[0]; };
            first.RegisterBinding(new UiBindingDefinition("ui.tests.binding", "Tests", "Old", read, delegate(int[] v) { }, null));
            var replacement = new UiBindingDefinition("ui.tests.binding", "Tests", "New", read, delegate(int[] v) { }, null);
            second.RegisterBinding(replacement); first.Dispose();
            Assert(UIApi.GetBindings().Contains(replacement), "old scope cannot delete same-owner replacement");
            second.Dispose(); Assert(!UIApi.GetBindings().Contains(replacement), "replacement scope releases its own generation");
        }

        private static void ReportsIndependentUiCapabilities()
        {
            Assert(
                UIApi.Supports("embedded-menu-pages"),
                "embedded menu pages are public API");
            Assert(
                UIApi.Supports("physical-binding-resolution"),
                "physical binding resolution is public API");
            Assert(
                UIApi.Supports("transparent-modal-pages"),
                "transparent modal pages are public API");
            Assert(
                UIApi.Supports("pause-menu-control"),
                "embedded pages can return to the active world");
            Assert(
                UIApi.Supports("main-menu-control"),
                "embedded pages can start the current world");
            Assert(
                UIApi.Supports("main-menu-return"),
                "transparent playback can return to the main menu");
            Assert(
                UIApi.Supports("root-main-menu-items"),
                "mods can register root main-menu entries");
            Assert(
                UIApi.Supports("root-pause-menu-items"),
                "mods can register root pause-menu entries");
            Assert(
                UIApi.Supports("menu-items-v2"),
                "mods can use high-level ordered root-menu entries");
            Assert(
                UIApi.Supports("menu-action-feedback"),
                "menu actions can provide temporary feedback labels");
            Assert(
                UIApi.Supports("modal-input-policy"),
                "nested pages can own Cancel navigation");
            Assert(
                UIApi.Supports("secondary-modal-action"),
                "modal pages expose an exclusive secondary action");
            Assert(
                UIApi.Supports("inventory-items-v1"),
                "mods can integrate custom inventory equipment");
            Assert(
                UIApi.Supports("inventory-equipment-v1"),
                "inventory equipment uses a dedicated toggle contract");
            Assert(
                UIApi.Supports("compact-grid-pages-v1"),
                "compact card grids are reusable outside Workshop");
            Assert(
                UiInputRouter.Read(
                    new PadState { boots = true },
                    false).Secondary,
                "boots binding routes to the secondary modal action");
            Assert(
                !new UiModalOptions(false).DimBackground,
                "transparent overlays retain their presentation option");
        }

        private static void FooterRowsRespectTheirFrame()
        {
            foreach (var frame in new[] {
                new Microsoft.Xna.Framework.Rectangle(12, 12, 456, 336),
                new Microsoft.Xna.Framework.Rectangle(22, 18, 436, 324),
                new Microsoft.Xna.Framework.Rectangle(8, 268, 464, 88) })
            {
                var lower = UiTheme.FooterRow(frame);
                var upper = UiTheme.FooterRow(frame, 1);
                var slots = UiTheme.CommandSlots(lower, new[] { 50, 120, 70 });
                Assert(lower.Bottom == frame.Bottom - 16 && upper.Bottom + 4 == lower.Top,
                    "footer rows retain frame padding and inter-row clearance");
                foreach (var slot in slots) Assert(frame.Contains(slot) && slot.Bottom <= frame.Bottom - 16,
                    "rendered command badges stay inside their owning frame");
                var moved = frame; moved.Offset(17, -9);
                var expected = lower; expected.Offset(17, -9);
                Assert(UiTheme.FooterRow(moved) == expected, "footer follows frame movement instead of screen coordinates");
            }
            bool refused = false;
            try { UiTheme.FooterRow(new Microsoft.Xna.Framework.Rectangle(0, 0, 80, 40)); }
            catch (ArgumentException) { refused = true; }
            Assert(refused, "undersized frames cannot place commands across the border");
        }

        private static void CommandLayoutUsesContentWidths()
        {
            var bounds = new Microsoft.Xna.Framework.Rectangle(25, 304, 427, 20);
            var slots = UiTheme.CommandSlots(bounds, new[] { 44, 125, 68 });
            Assert(slots[0].Width == 44 && slots[1].X == 85 && slots[1].Width == 125,
                "command groups follow their content with a fixed gap instead of equal columns");
            foreach (int width in new[] { 40, 180, 427 })
            {
                bounds.Width = width;
                slots = UiTheme.CommandSlots(bounds, new[] { 165, 80, 250 });
                Assert(slots[0].Left >= bounds.Left && slots[2].Right <= bounds.Right
                    && slots[0].Right <= slots[1].Left && slots[1].Right <= slots[2].Left,
                    "long binding labels remain inside the command bar at width " + width);
            }
            Assert(UiTheme.SnapTextPosition(new Microsoft.Xna.Framework.Vector2(10.5f, -0.5f))
                == new Microsoft.Xna.Framework.Vector2(10, -1), "fractional text placement snaps consistently to the pixel grid");
            Assert(UiTheme.NormalizeKey("LeftControl+B/RightShift") == "LCTRL+B/RSHIFT", "compound button hints normalize each physical key");
        }

        private static void ActionHintsFollowPhysicalBindings()
        {
            var binding = new PadBinding { boots = new[] { 66 }, confirm = new[] { 13 }, jump = new[] { 32 }, pause = new[] { 27 } };
            var pad = new PadInstance(new FakePad(), binding);
            Assert(UiInputHints.Key(pad, UiAction.Secondary) == "66", "secondary hint resolves the actual Boots binding");
            binding.boots = new[] { 88 };
            Assert(UiInputHints.Key(pad, UiAction.Secondary) == "88", "hint changes immediately after rebinding");
            Assert(UiInputHints.Key(pad, UiAction.Confirm) == "13", "confirm hint prefers the native menu binding");
            binding.confirm = new int[0];
            Assert(UiInputHints.Key(pad, UiAction.Confirm) == "32", "confirm can use the gameplay Jump alternative");
            Assert(UiInputHints.Key(pad, UiAction.Cancel) == "27", "cancel can use the Pause alternative");
            var chordPad = new ChordPad(new FakePad());
            binding.boots = chordPad.SetChords("test.hints", new[] { new UiChord(17, 66) });
            Assert(UiInputHints.Key(new PadInstance(chordPad, binding), UiAction.Secondary) == "17+66", "hint expands a virtual Controls+ chord to physical buttons");
            Assert(UiInputHints.Key(new PadInstance(new FakePad(), new PadBinding()), UiAction.Secondary) == "-", "unbound actions do not invent a key");
            Assert(UIApi.Supports("action-button-hints") && UIApi.Supports("workshop-menu-items"), "new UI facilities are discoverable");
        }

        private static void EquipmentDefinitionsKeepToggleStateSeparateFromActivation()
        {
            bool equipped = false;
            UiInventoryItemDefinition definition =
                UiInventoryItemDefinition.Equipment(
                    "test.equipment",
                    "Equipment",
                    "Test equipment",
                    Microsoft.Xna.Framework.Color.White,
                    delegate { return 1; },
                    delegate { return equipped; },
                    delegate(bool value) { equipped = value; return true; });
            Assert(definition.IsEquipment, "equipment kind is explicit");
            Assert(definition.Activate == null,
                "equipment does not masquerade as a use action");
            Assert(definition.SetEquipped(true) && definition.IsEquipped(),
                "equipment setter stores the requested state");
        }

        private static void CompactGridOwnsItsDrawableLifecycle()
        {
            FakeMenuFactory factory = new FakeMenuFactory();
            FakeDrawableNode page = new FakeDrawableNode();
            IBTnode node = UIApi.CreateCompactGrid(
                factory,
                new[] { new UiCompactGridItemDefinition("Item", page) });
            Assert(factory.Drawables.Count == 1,
                "compact-grid contract registers one ordered drawable host");
            CompactGridDrawableHost host =
                factory.Drawables[0] as CompactGridDrawableHost;
            Assert(host != null && object.ReferenceEquals(host, node),
                "compact-grid host is the focused behavior-tree node");
            Assert(host != null && host.Pages.Contains(page),
                "compact-grid host owns its card pages");
        }

        private static void CompactGridHonorsCardVisibility()
        {
            FakeMenuFactory factory = new FakeMenuFactory();
            UIApi.CreateCompactGrid(
                factory,
                new[]
                {
                    new UiCompactGridItemDefinition(
                        "Hidden item",
                        new FakeDrawableNode(),
                        null,
                        delegate { return false; })
                });
            CompactGridDrawableHost host =
                factory.Drawables[0] as CompactGridDrawableHost;
            Assert(host != null && host.Grid.VisibleItemCount == 0,
                "compact-grid visibility removes unavailable cards");
        }

        private static void TimedMenuFeedbackRestoresItsLabel()
        {
            long now = 1000;
            UiMenuFeedbackActionNode node = new UiMenuFeedbackActionNode(
                delegate { return UiMenuActionResult.Completed(); },
                delegate { return now; });
            node.Apply(UiMenuActionResult.Completed("Saved!", 1f));
            Assert(node.Label("Save replay") == "Saved!",
                "feedback label is shown immediately");
            now += TimeSpan.TicksPerSecond - 1;
            Assert(node.Label("Save replay") == "Saved!",
                "feedback label remains for its duration");
            now++;
            Assert(node.Label("Save replay") == "Save replay",
                "ordinary label returns when feedback expires");
            UiMenuActionResult completion = null;
            node.Apply(UiMenuActionResult.Pending("Saving...", delegate { return completion; }));
            now += TimeSpan.TicksPerSecond * 20;
            Assert(node.Label("Save replay") == "Saving...", "Pending feedback cannot expire into false success");
            completion = UiMenuActionResult.Rejected("Save failed", 2f);
            Assert(node.Label("Save replay") == "Save failed", "Asynchronous failure replaces pending feedback");
            now += TimeSpan.TicksPerSecond * 2;
            Assert(node.Label("Save replay") == "Save replay", "Terminal feedback duration starts at completion");
        }

        private static void CapturesAChordOnFullRelease()
        {
            ChordCapture capture = new ChordCapture(2);
            int[] result;
            Assert(!capture.Update(new int[0], out result), "idle");
            Assert(!capture.Update(new[] { 10 }, out result), "modifier held");
            Assert(!capture.Update(new[] { 10, 20 }, out result), "second button held");
            Assert(!capture.Update(new int[0], out result), "first release frame waits");
            Assert(!capture.Update(new int[0], out result), "second release frame waits");
            Assert(capture.Update(new int[0], out result), "stable full release commits");
            Assert(result.Length == 2 && result[0] == 10 && result[1] == 20, "two-button chord");
        }

        private static void LegacyCaptureKeepsOneButton()
        {
            ChordCapture capture = new ChordCapture(1);
            int[] result;
            capture.Update(new[] { 30, 40 }, out result);
            capture.Update(new int[0], out result);
            capture.Update(new int[0], out result);
            Assert(capture.Update(new int[0], out result), "legacy release commits");
            Assert(result.Length == 1 && result[0] == 30, "legacy binding stays singular");
        }

        private static void DoesNotFinishBetweenChordButtons()
        {
            ChordCapture capture = new ChordCapture(2);
            int[] result;
            capture.Update(new[] { 50 }, out result);
            Assert(!capture.Update(new[] { 50 }, out result), "held modifier does not commit");
            capture.Update(new[] { 50, 60 }, out result);
            Assert(!capture.Update(new[] { 60 }, out result), "partial release does not commit");
            Assert(!capture.Update(new int[0], out result), "first empty frame does not commit");
            Assert(!capture.Update(new int[0], out result), "second empty frame does not commit");
            Assert(capture.Update(new int[0], out result), "last release commits");
        }

        private static void IgnoresOneEmptyPollBetweenButtons()
        {
            ChordCapture capture = new ChordCapture(2);
            int[] result;
            capture.Update(new[] { 70 }, out result);
            Assert(!capture.Update(new int[0], out result), "transient empty poll waits");
            Assert(!capture.Update(new[] { 80 }, out result), "second button joins capture");
            capture.Update(new int[0], out result);
            capture.Update(new int[0], out result);
            Assert(capture.Update(new int[0], out result), "grace capture commits");
            Assert(result.Length == 2 && result[0] == 70 && result[1] == 80, "grace keeps both buttons");
        }

        private static void VirtualButtonNeedsWholeChord()
        {
            FakePad source = new FakePad();
            ChordPad pad = new ChordPad(source);
            int[] binding = pad.SetChords("test", new[] { new UiChord(40, 88) });
            Assert(binding.Length == 1 && binding[0] < 0, "chord receives virtual button");
            source.Pressed = new[] { 40 };
            Assert(Array.IndexOf(pad.GetPressedButtons(), binding[0]) < 0, "partial chord stays inactive");
            source.Pressed = new[] { 40, 88 };
            Assert(Array.IndexOf(pad.GetPressedButtons(), binding[0]) >= 0, "complete chord activates");
            Assert(pad.ButtonToString(binding[0]) == "40+88", "virtual button label");
            pad.Remove("test");
            Assert(Array.IndexOf(pad.GetPressedButtons(), binding[0]) < 0, "removed chord stays inactive");
        }

        private static void ResolvesVirtualBindingToPhysicalChord()
        {
            FakePad source = new FakePad();
            PadInstance instance = new PadInstance(source);
            int[] runtime = ChordVirtualizer.Apply(
                instance,
                "test-resolution",
                new[] { new UiChord(40, 88), new UiChord(82) });
            int[][] physical = UIApi.ResolvePhysicalBinding(instance, runtime);
            Assert(
                physical.Length == 2
                    && physical[0].Length == 2
                    && physical[0][0] == 40
                    && physical[0][1] == 88
                    && physical[1].Length == 1
                    && physical[1][0] == 82,
                "public API resolves virtual alternatives to physical chords");
            ChordVirtualizer.Remove(instance, "test-resolution");
        }

        private static void SerializesChordSettings()
        {
            UIApiSettings settings = new UIApiSettings
            {
                BindingChords = new[]
                {
                    new UiChordSettingsEntry
                    {
                        Id = "jump-king.test.jump",
                        Chords = new[] { new[] { 40, 88 }, new[] { 82 } }
                    }
                }
            };
            XmlSerializer serializer = new XmlSerializer(typeof(UIApiSettings));
            string xml;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, settings);
                xml = writer.ToString();
            }
            UIApiSettings restored;
            using (StringReader reader = new StringReader(xml))
                restored = (UIApiSettings)serializer.Deserialize(reader);
            Assert(restored.BindingChords.Length == 1, "binding chord settings row");
            Assert(restored.BindingChords[0].Chords[0].Length == 2, "binding chord settings pair");
            Assert(restored.BindingChords[0].Chords[0][1] == 88, "binding chord settings value");
        }

        private static void SerializesMenuPreferences()
        {
            Assert(new UIApiSettings().UseCompactWorkshopGrids,
                "compact Workshop grids are enabled by default");
            Assert(new UIApiSettings().UseCompactInventory,"compact inventory is enabled by default");
            UIApiSettings settings = new UIApiSettings
            {
                UseCompactWorkshopGrids = true,
                UseCompactInventory = false,
                PinnedSettings = new[] { "mod.setting.one", "mod.setting.two" },
                BindableSettings = new[] { "mod.setting.toggle" }
            };
            XmlSerializer serializer = new XmlSerializer(typeof(UIApiSettings));
            string xml;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, settings);
                xml = writer.ToString();
            }
            Assert(xml.Contains("UseCompactWorkshopGrids"),
                "Workshop grid preference uses the current XML name");
            Assert(!xml.Contains("UseCompactModGrid"),
                "legacy compact Mods XML name is not written");
            UIApiSettings restored;
            using (StringReader reader = new StringReader(xml))
                restored = (UIApiSettings)serializer.Deserialize(reader);
            Assert(restored.UseCompactWorkshopGrids,
                "compact Workshop grid setting persists");
            Assert(!restored.UseCompactInventory,"disabled compact inventory preference persists");
            using(var reader=new StringReader("<UIApiSettings />"))
                Assert(((UIApiSettings)serializer.Deserialize(reader)).UseCompactInventory,"older settings inherit compact inventory without a migration write");
            Assert(restored.PinnedSettings.Length == 2, "pinned setting ids persist");
            Assert(restored.BindableSettings.Length == 1,
                "bindable toggle setting ids persist");

            const string legacy = "<UIApiSettings><UseCompactModGrid>false</UseCompactModGrid></UIApiSettings>";
            using (StringReader reader = new StringReader(JKRuntime.Settings.DataMigration.UpgradeUiXml(legacy)))
                restored = (UIApiSettings)serializer.Deserialize(reader);
            Assert(!restored.UseCompactWorkshopGrids,
                "legacy compact Mods preference migrates to Workshop grids");
        }

        private static void GridNavigationUsesBothAxes()
        {
            Assert(GridNavigation.MoveHorizontal(4, 10, 1, 4) == 5,
                "grid moves right inside a row");
            Assert(GridNavigation.MoveHorizontal(9, 10, 1, 4) == 8,
                "short final row wraps horizontally");
            Assert(GridNavigation.MoveVertical(1, 10, 1, 4) == 5,
                "grid moves down in a column");
            Assert(GridNavigation.MoveVertical(9, 10, 1, 4) == 1,
                "grid wraps vertically to the same column");
        }

        private static void AdaptiveGridRows()
        {
            var layout = new CompactGridLayout(new[] { 18, 64, 32, 18, 18, 18, 76, 32 }, 3, 120, 8);
            Assert(layout.Heights[0] == 64 && layout.Tops[2] == 0 && layout.Tops[3] == 72,
                "Each grid row uses its tallest item plus a consistent gap");
            Assert(layout.Pages.Length == 2 && layout.Pages[0].Last == 6 && layout.Pages[1].First == 6,
                "Grid pages break between complete rows without dropping a short final row");
            var shortItems = new int[60]; for (int i = 0; i < shortItems.Length; i++) shortItems[i] = 18;
            var compact = new CompactGridLayout(shortItems, 3, 300, 8);
            Assert(compact.Pages[0].Last > 12, "Short grid entries can use more than four rows per page");
            for (int i = 0; i < layout.PageOf.Length; i++)
                Assert(layout.Tops[i] + layout.Heights[i] <= 120, "Every adaptive cell stays inside its page");
        }

        private static void CompactGridCancelReturnsOneLayer()
        {
            WorkshopGridExitGate gate = new WorkshopGridExitGate();
            gate.RequestExit();
            Assert(
                gate.Poll(true) == WorkshopGridExitResult.Waiting,
                "compact grid holds its parent while Escape is pressed");
            Assert(
                gate.Poll(false) == WorkshopGridExitResult.Exit,
                "compact grid returns exactly one layer after release");

            gate.ChildClosed();
            Assert(
                gate.Poll(true) == WorkshopGridExitResult.Waiting,
                "returning child cannot leak held Escape into the grid");
            Assert(
                gate.Poll(false) == WorkshopGridExitResult.None,
                "returning child keeps the compact grid open");
        }

        private static void CompactGridRequiresExplicitConfirmation()
        {
            CompactGridFocusState focus = new CompactGridFocusState();
            Assert(!focus.CanReadGridInput(true),
                "opening confirmation is consumed until released");
            Assert(!focus.CanReadGridInput(false),
                "first released frame only arms grid input");
            Assert(focus.CanReadGridInput(false),
                "grid accepts navigation after entry release");
            Assert(!focus.ChildActive,
                "hovering a card never opens its child page");
            focus.OpenChild();
            Assert(focus.ChildActive,
                "explicit confirmation opens the selected card");
            focus.CloseChild();
            Assert(!focus.ChildActive,
                "closing a card returns focus to the grid");
        }


        private static void PinnedSettingsUsesVanillaMenuLayering()
        {
            Assert(typeof(PinnedSettingsBrowser).BaseType == typeof(MenuSelector),
                "pinned settings participates in vanilla parent-first menu layering");
        }

        private sealed class RunningMenuItem : IBTSimpleMenuItem
        {
            internal int Calls;
            internal bool Finish, BadLayout;
            protected override BTresult MyRun(TickData data) { Calls++; return Finish ? BTresult.Failure : BTresult.Running; }
            public override void Draw(int x, int y, bool selected) { }
            public override Microsoft.Xna.Framework.Point GetSize()
            { if(BadLayout) throw new InvalidOperationException("layout fixture"); return new Microsoft.Xna.Framework.Point(20,10); }
        }
        private sealed class CountingPage : IUiPage
        {
            internal int Opens, Closes;
            public bool WantsClose { get { return false; } }
            public void OnOpen() { Opens++; }
            public void OnClose() { Closes++; }
            public void Update(UiInput input, float delta) { }
            public void Draw() { }
        }
        private static void InterruptedEmbeddedPageReleasesResources()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var previous = ControllerManager.instance; var previousMenu = MenuController.instance;
            var manager = (ControllerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            ControllerManager.instance = manager;
            typeof(ControllerManager).GetField("_menu_controller", flags).SetValue(manager, new MenuController(manager));
            typeof(ControllerManager).GetField("m_pads", flags).SetValue(manager, new System.Collections.Generic.List<PadInstance> { new PadInstance(new FakePad()) });
            try
            {
                var page = new CountingPage(); var node = new EmbeddedMenuPageNode(new FakeMenuFactory(), page);
                node.Run(new TickData(.016f, 1)); node.ResetResult(); node.ResetResult();
                Assert(page.Opens == 1 && page.Closes == 1, "Interrupted embedded page releases its resources exactly once");
                node.Run(new TickData(.016f, 2)); node.ResetResult();
                Assert(page.Opens == 2 && page.Closes == 2, "Reopened embedded page starts a fresh lifetime");
                var item = new RunningMenuItem(); item.Run(new TickData(.016f, 3));
                VanillaMenuAdapter.RepairIdleSelection(new IMenuItem[] { item }, 0, BTresult.Running);
                Assert(item.last_result == BTresult.Running, "Idle repair never interrupts the parent's genuinely active row");
                VanillaMenuAdapter.RepairIdleSelection(new IMenuItem[] { item }, 0, BTresult.Failure);
                Assert(item.last_result != BTresult.Running, "Dormant row cannot retain an unowned running action");
            }
            finally { ControllerManager.instance = previous; MenuController.instance = previousMenu; }
        }
        private static void MenuMutationPreservesOpenChild()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var manager = (ControllerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            var previous = ControllerManager.instance; ControllerManager.instance = manager;
            typeof(ControllerManager).GetField("_menu_controller", flags).SetValue(manager, new MenuController(manager));
            try
            {
                var root = new MenuSelector(new JumpKing.PauseMenu.GuiFormat());
                var resume = new RunningMenuItem(); var browser = new RunningMenuItem(); var hidden = new RunningMenuItem(); var pin = new RunningMenuItem();
                root.AddChild(resume); root.AddChild(browser); root.AddChild(hidden); root.Initialize(false);
                var index = typeof(MenuSelector).GetField("_index", flags);
                var childResult = typeof(MenuSelector).GetField("m_last_child_result", flags);
                var run = typeof(MenuSelector).GetMethod("MyRun", flags);
                typeof(IBTnode).GetField("m_last_result", flags).SetValue(root, BTresult.Running);
                index.SetValue(root, 1); childResult.SetValue(root, BTresult.Running);
                // Native DisableMenuItem would play audio when correcting selection;
                // set the same visible-row projection for this headless fixture.
                typeof(MenuSelector).GetField("m_menu_items", flags).SetValue(root, new IMenuItem[]{resume,browser});
                for(int i=0;i<20;i++)
                {
                    VanillaMenuAdapter.SetChildren(root, new IBTnode[]{resume,pin,browser,hidden});
                    run.Invoke(root,new object[]{new TickData(1f/60,2*i+1)});
                    Assert((int)index.GetValue(root)==2 && browser.Calls==2*i+1 && resume.Calls==0 && pin.Calls==0,
                        "pin insertion retains the active browser by identity");
                    VanillaMenuAdapter.SetChildren(root, new IBTnode[]{resume,browser,hidden});
                    run.Invoke(root,new object[]{new TickData(1f/60,2*i+2)});
                    Assert((int)index.GetValue(root)==1 && browser.Calls==2*i+2,"unpin keeps the same running child");
                }
                var visible=(IMenuItem[])typeof(MenuSelector).GetField("m_menu_items",flags).GetValue(root);
                Assert(visible.Length==2,"pin refresh preserves native disabled rows");
                VanillaMenuAdapter.SetChildren(root,new IBTnode[]{resume,pin});
                Assert(root.Children.Length==3,"removing the running child is deferred");
                Assert(VanillaMenuAdapter.ChildrenForEdit(root).Length==2,"subsequent edits compose with the pending tree");
                browser.Finish=true; run.Invoke(root,new object[]{new TickData(1f/60,41)});
                VanillaMenuAdapter.ApplyPending(root);
                Assert(root.Children.Length==2 && (BTresult)childResult.GetValue(root)==BTresult.NULL,"pending removal commits only after child completion");
                var before=root.Children;
                bool failed=false;
                try { VanillaMenuAdapter.SetChildren(root,new IBTnode[]{new RunningMenuItem{BadLayout=true}}); }
                catch(InvalidOperationException) { failed=true; }
                Assert(failed && ReferenceEquals(root.Children,before),"failed layout leaves the published tree intact");
            }
            finally { ControllerManager.instance=previous; }
        }
        private static void NativeMenuFramesDoNotRestoreConsumedInput()
        {
            var menu=new MenuController(null);
            var field=typeof(MenuController).GetField("_menu_state",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            field.SetValue(menu,new PadState{confirm=true});
            using(Input.NativeInputFrames.BeginMenu(menu,new PadState{down=true}))
            {
                Assert(menu.GetPadState().down && !menu.GetPadState().confirm,"menu frame replaces stale input");
                bool nested=false;
                try { Input.NativeInputFrames.BeginMenu(menu,new PadState{confirm=true}); } catch(InvalidOperationException) { nested=true; }
                Assert(nested && menu.GetPadState().down,"nested dispatch is rejected without corrupting active input");
                menu.ConsumePadPresses();
            }
            Assert(!menu.GetPadState().confirm && !menu.GetPadState().down,"disposing cannot resurrect consumed confirmation");
            try { using(Input.NativeInputFrames.BeginMenu(menu,new PadState{confirm=true})) throw new InvalidOperationException(); }
            catch(InvalidOperationException) { }
            Assert(!menu.GetPadState().confirm,"exception cleanup clears the owned menu frame");
            var manager=(ControllerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            var previous=ControllerManager.instance; ControllerManager.instance=manager;
            typeof(ControllerManager).GetField("_menu_controller",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(manager,menu);
            try
            {
                var child=new RunningMenuItem();
                var button=new TextButton("Fixture",child,(Microsoft.Xna.Framework.Graphics.SpriteFont)null,Microsoft.Xna.Framework.Color.White);
                var pinned=new PinnedSettingMenuItem("fixture",button);
                using(Input.NativeInputFrames.BeginMenu(menu,new PadState{confirm=true})) pinned.Run(new TickData(1f/60,1));
                Assert(child.Calls==1,"pinned native button opens on explicit confirmation");
                pinned.ResetResult();
                pinned.Run(new TickData(1f/60,2));
                Assert(child.Calls==1 && pinned.last_result==BTresult.Failure,"resetting a pinned wrapper resets its native child; browsing cannot reopen it");
            }
            finally { ControllerManager.instance=previous; }
        }

        private sealed class FakePad : IPad
        {
            internal int[] Pressed = new int[0];

            public int[] GetPressedButtons() { return Pressed; }
            public string ButtonToString(int button) { return button.ToString(); }
            public PadBinding GetDefaultBind() { return new PadBinding(); }
            public string GetSaveIdentifier() { return "test"; }
            public string GetPrintName() { return "Test"; }
            public bool IsConnected() { return true; }
        }

        private sealed class FakeMenuFactory
        {
            public IList Drawables { get; private set; }

            internal FakeMenuFactory()
            {
                Drawables = new ArrayList();
            }

            public void AddDrawable(object drawable)
            {
                Drawables.Add(drawable);
            }
        }

        private sealed class FakeDrawableNode : IBTnode, IDrawable
        {
            protected override BTresult MyRun(TickData data)
            {
                return BTresult.Running;
            }

            public void Draw() { }
        }

        private static void Assert(bool condition, string name)
        {
            if (condition) return;
            Console.Error.WriteLine("[FAIL] " + name);
            failures++;
        }
    }
}
