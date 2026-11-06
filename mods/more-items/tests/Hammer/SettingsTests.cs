using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.Settings;
using JKRuntime.UI;
using MoreItems;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static void ReloadItemSettings()
        {
            typeof(SettingsStore).GetField("loaded", Flags).SetValue(null, false);
            typeof(SettingsStore).GetProperty("Current", Flags).SetValue(null, null, null);
            SettingsStore.EnsureLoaded();
        }

        private static void StrengthSettings()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoreItems.Settings.xml");
            byte[] previousSettings = File.Exists(path) ? File.ReadAllBytes(path) : null;
            string previousDirectory = Environment.CurrentDirectory;
            string sandbox = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hammer-item-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(sandbox, "Content", "Saves"));
            Environment.CurrentDirectory = sandbox;
            var counts = (Dictionary<string, int>)typeof(ItemInventory).GetField("Counts", Flags).GetValue(null);
            try
            {
                File.WriteAllText(path, "<MoreItemsSettings><JetpackEquipped>true</JetpackEquipped><JetpackVolume>0.4</JetpackVolume></MoreItemsSettings>");
                ReloadItemSettings();
                Check(Settings.Strength.Value == 100 && SettingsStore.Current.JetpackEquipped
                    && SettingsStore.Current.JetpackVolume == .4f && !SettingsStore.Current.HammerEquipped,
                    "Existing More Items preferences gain default Hammer settings without auto-equipping or losing Jetpack settings");
                Check(Math.Abs(Settings.PhysicsStrength(100) - 1.2f) < .00001f,
                    "New 100 percent exactly selects the accepted former 120 percent force");
                Check(Math.Abs(Settings.PhysicsStrength(Settings.MaximumStrength) - 1.5f) < .00001f, "Rebased upper endpoint retains the previous maximum force");

                typeof(ItemInventory).GetField("loaded", Flags).SetValue(null, true);
                typeof(ItemInventory).GetField("available", Flags).SetValue(null, true);
                typeof(ItemInventory).GetField("currentKey", Flags).SetValue(null, "hammer-fixture");
                counts.Clear();
                HammerDefinition.RegisterModule(); HammerDefinition.Register(); HammerDefinition.Register();
                ConsumableDefinition item;
                Check(MoreItemsApi.TryGet("hammer", out item) && item.IsEquipment && !item.IsUsable, "Hammer registers as permanent equipment");
                var offer = UIApi.GetMerchantOffers().Single(o => o.Id == "more-items.hammer");
                var grant = UIApi.GetDebugActions().Single(a => a.Id == "more-items.add-hammer");
                var strength = UIApi.GetDebugActions().Single(a => a.Id == "more-items.hammer-strength");
                Check(offer.CurrencyId == UIApi.VanillaCurrencyId(JumpKing.MiscEntities.WorldItems.Items.Silver)
                    && offer.GetPrice() == 3, "Merchant sells Hammer for exactly three silver coins");
                Check(!item.SetEquipped(true) && !HammerDefinition.IsEnabledForPlayer(), "Unowned Hammer cannot activate its controller");
                grant.Execute(); grant.Execute();
                Check(MoreItemsApi.GetCount("hammer") == 1 && !item.IsEquipped(), "Debug grant persists one Hammer without equipping or duplicating it");
                Check(File.ReadAllText(Path.Combine(sandbox, "Content", "Saves", "more_items_inventory.sav")).Contains("hammer"),
                    "Debug grant passes through the real inventory save pipeline");
                Check(item.SetEquipped(true) && HammerDefinition.IsEnabledForPlayer(), "Owned equipment can enable Hammer");
                ReloadItemSettings(); Check(item.IsEquipped(), "Equipment choice survives settings reload");
                strength.Adjust(1);
                Check(Settings.Strength.Value == 105 && strength.GetLabel() == "Hammer: 105%" && item.IsEquipped(),
                    "Debug force adjustment updates its live label while retaining equipment state");
                ReloadItemSettings(); Check(Settings.Strength.Value == 105, "Debug strength persists");
                for (int i = 0; i < 40; i++) strength.Adjust(-1);
                Check(Settings.Strength.Value == 50, "Debug strength clamps at the lower endpoint");
                for (int i = 0; i < 40; i++) strength.Adjust(1);
                Check(Settings.Strength.Value == Settings.MaximumStrength, "Debug strength clamps at the upper endpoint");
                strength.Execute(); Check(Settings.Strength.Value == 100, "Confirm resets force to the new default");
                foreach (int bad in new[] { 0, 49, 101, 126, int.MaxValue })
                {
                    bool rejected = false;
                    try { Settings.Strength.Set(bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
                    Check(rejected && Settings.Strength.Value == 100, "Invalid force commands leave settings intact");
                }
                Check(item.SetEquipped(false) && !HammerDefinition.IsEnabledForPlayer(), "Unequipping disables Hammer independently of ownership");
                SettingsStore.SetHammerEnabled(false);
                Check(!grant.IsAvailable() && !item.IsEnabled(), "Disabled module hides its item and grant action");
                SettingsStore.SetHammerEnabled(true);
                Check(MoreItemsApi.GetCount("hammer") == 1 && SettingsStore.Current.JetpackEquipped && SettingsStore.Current.JetpackVolume == .4f,
                    "Hammer changes preserve item ownership and other equipment preferences");

                using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var before = SettingsStore.Current; bool failed = false;
                    try { SettingsStore.SetHammerStrength(105); } catch (IOException) { failed = true; }
                    Check(failed && ReferenceEquals(before, SettingsStore.Current), "Failed atomic Hammer save leaves the in-memory settings unchanged");
                }

                File.WriteAllText(path, "<MoreItemsSettings><HammerStrength>broken");
                ReloadItemSettings();
                int recoveredStrength = Settings.Strength.Value;
                bool recoveryRejected = false;
                try { strength.Adjust(1); } catch (Exception) { recoveryRejected = true; }
                Check(recoveryRejected && Settings.Strength.Value == recoveredStrength && File.ReadAllText(path).EndsWith("broken"),
                    "Read-only recovery rejects explicit edits visibly and preserves both active values and original data");

                var world = new World(); world.Blocks.Add(new Rectangle(-100, 22, 200, 20));
                float previous = 0;
                for (int percent = Settings.MinimumStrength; percent <= Settings.MaximumStrength; percent += Settings.StrengthStep)
                {
                    var state = Pose(Vector2.Zero, new Vector2(0, 20)); state.Drive = new Vector2(0, 8);
                    float response = -state.Step(Vector2.Zero, Vector2.Zero, new Vector2(0, 8), world, Settings.PhysicsStrength(percent)).Y;
                    Check(response > previous && response < 7f, "Every displayed force step has a distinct bounded physical response");
                    previous = response;
                }
                var anchor = Pose(Vector2.Zero, new Vector2(0, 20)); anchor.Contact = true;
                for (int frame = 0; frame < 120; frame++)
                    Near(anchor.Step(Vector2.Zero, new Vector2(0, .35f), Vector2.Zero, world, Settings.PhysicsStrength(frame % 2 == 0 ? Settings.MinimumStrength : Settings.MaximumStrength)),
                        Vector2.Zero, "Changing force on a stationary anchor never creates a powered impulse");
                float fullLaunch = LaunchHeight(10, 6, Settings.PhysicsStrength(Settings.MaximumStrength));
                Check(fullLaunch > LaunchHeight(10, 6, Settings.PhysicsStrength(100)) && fullLaunch < 320f,
                    "Rebased maximum remains stronger than default and bounded by native speed and gravity: " + fullLaunch);
            }
            finally
            {
                ItemModuleRegistry.StopRuntime();
                MoreItemsApi.Unregister("hammer"); MoreItemsApi.UnregisterModule("hammer");
                UIApi.UnregisterDebugAction("more-items.add-hammer"); UIApi.UnregisterDebugAction("more-items.hammer-strength");
                UIApi.UnregisterMerchantOffer("more-items.hammer");
                Environment.CurrentDirectory = previousDirectory;
                if (previousSettings == null) File.Delete(path); else File.WriteAllBytes(path, previousSettings);
                ReloadItemSettings();
            }
            Console.WriteLine("[OK] Hammer equipment, three-silver offer, persistent debug grant, normalized force, live debug value and preserved settings");
        }
    }
}
