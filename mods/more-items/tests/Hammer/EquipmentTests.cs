using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using EntityComponent;
using EntityComponent.BT;
using JumpKing.Player;
using MoreItems;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    internal static partial class Tests
    {
        private static void EquippedLifecycle(PlayerEntity player, BehaviorTreeComp tree)
        {
            var loop = new JumpKing.GameManager.GameLoop();
            var completion = loop.GetType().GetField("m_ending_body_modifiers", Flags);
            completion.SetValue(loop, Activator.CreateInstance(completion.FieldType, true));
            var save = typeof(PlayerEntity).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            save.GetProperty("CombinedSave").SetValue(null, new JumpKing.SaveThread.CombinedSaveFile {
                full_run = new JumpKing.SaveThread.SaveComponents.SaveCompCushion<JumpKing.SaveThread.SaveComponents.FullRunSave> { initialized = true }
            }, null);
            JumpKing.SaveThread.SaveComponents.FullRunSave.fullRunSave = new JumpKing.SaveThread.SaveComponents.FullRunSave();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoreItems.Settings.xml");
            byte[] oldSettings = File.Exists(path) ? File.ReadAllBytes(path) : null;
            var managerField = typeof(EntityManager).GetField("_instance", Flags);
            object oldManager = managerField.GetValue(null);
            var counts = (Dictionary<string, int>)typeof(ItemInventory).GetField("Counts", Flags).GetValue(null);
            var baseline = Nodes(tree);
            var behaviours = player.m_body.GetBehaviourList().ToArray();
            var soundManager = typeof(PlayerEntity).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime", true);
            var soundInstance = soundManager.BaseType.GetField("instance", Flags);
            object oldSound = soundInstance.GetValue(null);
            try
            {
                object managerSound = FormatterServices.GetUninitializedObject(soundManager);
                soundManager.BaseType.GetField("m_settings", Flags).SetValue(managerSound,
                    Activator.CreateInstance(typeof(PlayerEntity).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefs", true)));
                soundInstance.SetValue(null, managerSound);
                managerField.SetValue(null, null);
                var manager = new EntityManager();
                ((List<Entity>)typeof(EntityManager).GetField("entities", Flags).GetValue(manager)).Add(player);
                File.WriteAllText(path, "<MoreItemsSettings />"); ReloadItemSettings();
                typeof(ItemInventory).GetField("loaded", Flags).SetValue(null, true);
                typeof(ItemInventory).GetField("available", Flags).SetValue(null, true);
                counts["hammer"] = 1;
                HammerDefinition.RegisterModule(); HammerDefinition.Register();
                ConsumableDefinition item; MoreItemsApi.TryGet("hammer", out item);
                ItemModuleRegistry.StartRuntime();
                Check(baseline.SetEquals(Nodes(tree)), "Owned but unequipped Hammer leaves the native graph intact");
                HammerMapPermissions(player, tree, item);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    Check(item.SetEquipped(true) && JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended,
                        "Inventory equip installs the real controller and reserves native jump");
                    var current = typeof(HammerInstaller).GetField("controller", Flags).GetValue(null);
                    var physics = (HammerPhysics)typeof(HammerController).GetField("physics", Flags).GetValue(current);
                    physics.Head = new Vector2(170, 280); physics.Contact = true;
                    Settings.Strength.Set(105);
                    Check(ReferenceEquals(current, typeof(HammerInstaller).GetField("controller", Flags).GetValue(null))
                        && physics.Head == new Vector2(170, 280), "Live debug strength does not recreate or move a planted hammer");
                    var suspend = typeof(JKRuntime.UI.UIApi).Assembly.GetType("JKRuntime.UI.ModalHost", true)
                        .GetMethod("SuspendComponents", Flags);
                    using ((IDisposable)suspend.Invoke(null, new object[] { player.GetComponents() }))
                        Check(item.SetEquipped(false) && baseline.SetEquals(Nodes(tree)) && !JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended,
                            "Inventory unequip during a modal restores native graph edges and releases charge ownership");
                    player.GetComponent<HammerVisual>().Refresh();
                    Check(!(typeof(PlayerEntity).GetField("m_sprite", Flags).GetValue(player) is HammerSprite),
                        "Closing a modal cannot restore a controller's detached Hammer sprite");
                    Check(player.m_body.GetBehaviourList().SequenceEqual(behaviours), "Unequip removes all added body hooks");
                }
                item.SetEquipped(true);
                ItemModuleDefinition module; MoreItemsApi.TryGetModule("hammer", out module);
                module.SetEnabled(false);
                Check(item.IsEquipped() && baseline.SetEquals(Nodes(tree)), "Module disable suspends equipped Hammer without losing equipment choice");
                module.SetEnabled(true);
                Check(JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended, "Re-enabling restores the equipped controller");
                ItemModuleRegistry.StopRuntime();
                Check(baseline.SetEquals(Nodes(tree)) && player.m_body.GetBehaviourList().SequenceEqual(behaviours), "Level unload releases the complete Hammer runtime");
                ItemModuleRegistry.StartRuntime();
                Check(JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended, "Next level reinstalls persistent equipment");
                ItemModuleRegistry.StopRuntime();
            }
            finally
            {
                ItemModuleRegistry.StopRuntime(); HammerInstaller.Uninstall();
                MoreItemsApi.Unregister("hammer"); MoreItemsApi.UnregisterModule("hammer");
                managerField.SetValue(null, oldManager);
                soundInstance.SetValue(null, oldSound);
                if (oldSettings == null) File.Delete(path); else File.WriteAllBytes(path, oldSettings);
                ReloadItemSettings();
            }
            Console.WriteLine("[OK] Actual Hammer inventory equip/unequip, live force, module disable/re-enable and level lifecycle");
        }

        private sealed class PermissionPeer : JumpKing.API.IBodyCompBehaviour
        {
            public bool ExecuteBehaviour(JumpKing.BodyCompBehaviours.BehaviourContext context) { return true; }
        }

        private static uint ExternalCount(BodyComp body)
        { return (uint)typeof(BodyComp).GetField("m_externalBehavioursCount", Flags).GetValue(body); }

        private static uint ModifierPeak()
        { return JumpKing.SaveThread.SaveComponents.FullRunSave.fullRunSave.CurrentBodyCompModifiers; }

        private static void HammerMapPermissions(PlayerEntity player, BehaviorTreeComp tree, ConsumableDefinition item)
        {
            var content = JumpKing.Game1.instance.contentManager;
            var oldLevel = content.level;
            var nativeNodes = Nodes(tree);
            var nativeBehaviours = player.m_body.GetBehaviourList().ToArray();
            var peer = new PermissionPeer();
            bool peerInstalled = false;
            try
            {
                ItemModuleRegistry.StopRuntime();
                Check(item.SetEquipped(true), "Equipment choice can persist while a world is unloaded");
                // A fresh allowed map must not mark an otherwise clean run. Later
                // transitions must preserve an unrelated modifier and prior history.
                Check(ExternalCount(player.m_body) == 0 && ModifierPeak() == 0, "Permission fixture starts with a clean native run");
                string[][] maps = { new[] { "AllowHammer" }, null, new[] { "AllowHammer" }, new[] { "AllowJetpack" } };
                for (int i = 0; i < maps.Length; i++)
                {
                    if (i == 1)
                    {
                        Check(player.m_body.RegisterBehaviour(peer), "Foreign modifier registration succeeds");
                        peerInstalled = true;
                    }
                    var level = (JumpKing.Workshop.Level)FormatterServices.GetUninitializedObject(typeof(JumpKing.Workshop.Level));
                    typeof(JumpKing.Workshop.Level).GetField("_level", Flags).SetValue(level,
                        new JumpKing.Workshop.Level.LevelSettings { Tags = maps[i] });
                    content.level = maps[i] == null ? null : level;
                    uint before = ExternalCount(player.m_body), peakBefore = ModifierPeak();
                    ItemModuleRegistry.StartRuntime();
                    Check(item.IsEquipped() && JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended,
                        "Persistent Hammer remains usable on both authorized and unauthorized worlds");
                    bool allowed = i == 0 || i == 2;
                    Check(allowed ? ExternalCount(player.m_body) == before && ModifierPeak() == peakBefore
                        : ExternalCount(player.m_body) > before && ModifierPeak() >= ExternalCount(player.m_body),
                        "Current world's AllowHammer controls actual native modifier registration");
                    uint peak = ModifierPeak();
                    ItemModuleRegistry.StopRuntime();
                    Check(ExternalCount(player.m_body) == before && ModifierPeak() == peak && nativeNodes.SetEquals(Nodes(tree)),
                        "World unload restores native control and preserves run history");
                    var remaining = peerInstalled ? nativeBehaviours.Concat(new[] { peer }) : nativeBehaviours;
                    Check(player.m_body.GetBehaviourList().SequenceEqual(remaining), "Hammer cleanup preserves every foreign and native behaviour");
                }
                Check(MoreItemsApi.GetCount("hammer") == 1, "Changing world permissions preserves item ownership");
            }
            finally
            {
                ItemModuleRegistry.StopRuntime();
                item.SetEquipped(false);
                if (peerInstalled) player.m_body.RemoveBehaviour(peer);
                content.level = oldLevel;
                ItemModuleRegistry.StartRuntime();
            }
            Console.WriteLine("[OK] AllowHammer native clean-run accounting, foreign markers and persistent equipment across worlds");
        }
    }
}
