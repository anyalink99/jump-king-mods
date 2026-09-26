using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using EntityComponent;
using JKRuntime.Compatibility;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Level;
using JumpKing.MiscSystems.LocationText;
using JumpKing.Player;

namespace JKRuntime
{
    internal static class ManagerCompatibilityTests
    {
        private static int checks;
        private static int peerCalls;
        private static bool SuppressKeyPolling() { return false; }
        private static void PeerCallback() { peerCalls++; }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        private static void World(int count, params Location[] locations)
        {
            var screens = Enumerable.Range(0, count).Select(i => new LevelScreen(i, new IBlock[0],
                new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null)).ToArray();
            typeof(LevelManager).GetField("m_screens", OwnedPatches.Members).SetValue(null, screens);
            typeof(LevelManager).GetField("_total_screens", OwnedPatches.Members).SetValue(null, count);
            typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.LocationText.LocationTextManager").GetMethod("SetSettingsData")
                .Invoke(null, new object[] { new LocationSettings { locations = locations } });
        }
        private static ComboBox Combo(Form form) { return (ComboBox)form.Controls.Find("cboSpecificLevel", true).Single(); }
        private static void Apply(bool enabled)
        { SettingsStore.Current.ModCompatibilityFixes = enabled; JumpKingManagerCompatibility.Apply(); }
        [STAThread]
        private static void Main(string[] args)
        {
            SettingsStore.EnsureLoaded();
            JumpKingManagerCompatibility.Prepare();
            Check(JumpKingManagerCompatibility.Status.StartsWith("Not needed:"), "Absent mod is not loaded by Runtime");
            if (args[0] != "none") Assembly.LoadFrom(args[0]);
            var foreign = Assembly.LoadFrom(args[1]);
            World(205, new Location { start = 1, end = 3, name = "Workshop entrance" },
                new Location { start = 4, end = 205, name = "Upper halls" });
            if (args.Length > 2 && args[2] == "refuse")
            {
                JumpKingManagerCompatibility.Prepare();
                Check(JumpKingManagerCompatibility.Status.StartsWith("Unavailable:") || JumpKingManagerCompatibility.Status.StartsWith("Unsupported:"), "Unknown mod/engine refused");
                Check(JumpKingManagerCompatibility.Map == null, "Refused adapter publishes no map");
                Console.WriteLine("[OK] Manager refusal: " + JumpKingManagerCompatibility.Status); return;
            }
            var type = foreign.GetType("JumpKingManager.Manager", true);
            var active = foreign.GetType("JumpKingManager.ModEntry", true).GetField("JKManager", OwnedPatches.Members);
            using (var peer = new OwnedPatches("test.manager-peer"))
            {
            // The real constructor starts a key-polling worker. Isolated tests
            // exercise its handler explicitly, never react to the user's keys.
            peer.Add(type.GetMethod("IsKeyDown", OwnedPatches.Members), prefix: typeof(ManagerCompatibilityTests).GetMethod("SuppressKeyPolling", OwnedPatches.Members));
            peer.Add(type.GetMethod("btnSpecificLevel_Click", OwnedPatches.Members), postfix: typeof(ManagerCompatibilityTests).GetMethod("PeerCallback", OwnedPatches.Members));
            using (var existing = (Form)Activator.CreateInstance(type))
            {
                var original = Combo(existing).Items.Cast<object>().ToArray();
                Check(original.Length > 100, "Original installed Manager exposes vanilla enum");
                active.SetValue(null, existing);
                JumpKingManagerCompatibility.Prepare();
                Check(JumpKingManagerCompatibility.Status.StartsWith("Active:"), JumpKingManagerCompatibility.Status);
                Check(Combo(existing).Items.Count == 205 && Combo(existing).Items[204].ToString().Contains("Upper halls"), "Existing Manager uses loaded map beyond enum limits");
                var panel = existing.Controls.OfType<FlowLayoutPanel>().Single();
                Check(panel.Controls.OfType<Button>().Count() == 2, "Actual loaded area buttons replace vanilla groups");
                // No player is loaded: navigation must return without an enum
                // cast, a foreign error dialog, or a stale player dereference.
                Combo(existing).SelectedIndex = 204;
                type.GetMethod("btnSpecificLevel_Click", OwnedPatches.Members).Invoke(existing, new object[] { null, EventArgs.Empty });
                Check(true, "High-index specific-screen handler safely reaches the adapter");
                var entities = new EntityManager();
                var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
                player.m_body = new BodyComp(Microsoft.Xna.Framework.Vector2.Zero, 18, 26);
                entities.MoveToFront(player);
                type.GetMethod("btnSpecificLevel_Click", OwnedPatches.Members).Invoke(existing, new object[] { null, EventArgs.Empty });
                Check(Camera.CurrentScreen == 204 && player.m_body.Position.Y < -73000, "Real click reaches a high loaded screen instead of vanilla coordinates");
                Check(player.m_body.Velocity == Microsoft.Xna.Framework.Vector2.Zero, "Navigation preserves Manager's velocity reset");
                var stale = JumpKingManagerCompatibility.Map;
                var handle = existing.Handle;
                Combo(existing).SelectedIndex = 3;
                var worker = new Thread(delegate() { type.GetMethod("btnSpecificLevel_Click", OwnedPatches.Members).Invoke(existing, new object[] { null, EventArgs.Empty }); });
                worker.Start(); worker.Join(); Application.DoEvents();
                Check(Camera.CurrentScreen == 3, "Hotkey worker marshals navigation to the form/game thread");
                using (var bitmap = new Bitmap(existing.Width, existing.Height))
                {
                    existing.StartPosition = FormStartPosition.Manual;
                    existing.Location = new Point(-20000, -20000);
                    existing.ShowInTaskbar = false;
                    existing.Show();
                    Check(panel.Visible && !existing.Controls.Find("btnRedcrownWoods", true).Single().Visible, "Loaded areas hide the original vanilla buttons");
                    existing.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manager-map-preview.png"));
                    existing.Hide();
                }
                Apply(false);
                Check(Combo(existing).Items.Cast<object>().SequenceEqual(original), "Disable restores exact foreign enum items");
                Check(panel.Controls.Count == 0 && !existing.Text.Contains("Loaded map"), "Disable restores original view");
                int beforePeer = peerCalls;
                type.GetMethod("btnSpecificLevel_Click", OwnedPatches.Members).Invoke(existing, new object[] { null, EventArgs.Empty });
                Check(peerCalls == beforePeer + 1, "Opt-out preserves another owner's callback");
                Apply(true); Apply(true);
                Check(existing.Controls.OfType<FlowLayoutPanel>().Count() == 1 && Combo(existing).Items.Count == 205, "Enable/repeated refresh are idempotent");
                using (var created = (Form)Activator.CreateInstance(type))
                {
                    Check(Combo(created).Items.Count == 205, "Constructor hook adapts later windows");
                    JumpKingManagerCompatibility.ClearWorld();
                    Check(Combo(created).Items.Count == 0 && Combo(existing).Items.Count == 0, "World exit clears every adapted window");
                    Apply(true);
                    Check(Combo(created).Items.Count == 0, "Menu toggle cannot revive unloaded native data");
                    World(2, new Location {start=1,end=2,name="Different map"});
                    JumpKingManagerCompatibility.Prepare();
                    Check(Combo(created).Items.Count == 2 && Combo(existing).Items[0].ToString().Contains("Different map"), "World switch replaces labels and screen limits");
                    var before = player.m_body.Position;
                    JumpKingManagerCompatibility.Navigate(existing, stale, 0);
                    Check(player.m_body.Position == before, "A stale map action cannot teleport into the new world");
                    World(40, Enumerable.Range(1, 40).Select(i => new Location {start=i,end=i,name="Custom area " + i}).ToArray());
                    JumpKingManagerCompatibility.Prepare();
                    existing.Show();
                    var lastArea = panel.Controls.OfType<Button>().Last();
                    panel.ScrollControlIntoView(lastArea);
                    lastArea.PerformClick();
                    Check(Camera.CurrentScreen == 39, "Scrollable area buttons navigate beyond the original fixed region count");
                    existing.Hide();
                    World(3); JumpKingManagerCompatibility.Prepare();
                    Check(Combo(created).Items.Count == 3 && Combo(created).Items[2].ToString() == "Screen 3", "No location definitions use actual unnamed screens");
                    Apply(false);
                    Check(Combo(created).Items.Cast<object>().SequenceEqual(original), "Later-created Manager also restores vanilla state");
                }
                active.SetValue(null, null);
            }
            }
            Console.WriteLine("[OK] Installed Manager: " + checks + " checks; existing/new windows, live opt-out, world switch, unnamed/high screens");
        }
    }
}
