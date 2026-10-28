using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.MiscEntities.WorldItems;
using JumpKing.MiscEntities.WorldItems.Inventory;
using JumpKing.Player;
using JumpKing.SaveThread;
using JumpKing.SaveThread.SaveComponents;
using Microsoft.Xna.Framework;

namespace Powers
{
    internal static partial class Tests
    {
        private sealed class WalkInputFixture : IDisposable
        {
            private readonly ControllerManager previous = ControllerManager.instance;
            internal readonly PadInstance Pad;
            private readonly Dictionary<string, object> cache;
            private readonly Dictionary<string, object> oldCache;
            private readonly string folder;
            private readonly System.Reflection.FieldInfo savedLoopField, combinedField, modifierCountField;
            private readonly object savedLoop, savedCombined, savedModifierCount;
            private readonly bool achievementsDisabled;
            private readonly System.Reflection.PropertyInfo achievementsProperty;
            internal WalkInputFixture()
            {
                var manager = (ControllerManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ControllerManager));
                Pad = (PadInstance)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PadInstance));
                typeof(ControllerManager).GetField("m_pads", Flags).SetValue(manager, new List<PadInstance> { Pad });
                ControllerManager.instance = manager;
                var save = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
                cache = (Dictionary<string, object>)save.GetField("loaded_objects", Flags).GetValue(null);
                oldCache = new Dictionary<string, object>(cache);
                folder = (string)save.GetField("PERMANENT_FOLDER", Flags).GetValue(null);
                savedLoopField = typeof(JumpKing.GameManager.GameLoop).GetField("_instance", Flags);
                savedLoop = savedLoopField.GetValue(null);
                var loop = new JumpKing.GameManager.GameLoop();
                var completion = loop.GetType().GetField("m_ending_body_modifiers", Flags);
                completion.SetValue(loop, Activator.CreateInstance(completion.FieldType, true));
                combinedField = save.GetField("_COMBINED_SAVE", Flags);
                savedCombined = combinedField.GetValue(null);
                combinedField.SetValue(null, new CombinedSaveFile { full_run = new SaveCompCushion<FullRunSave> { initialized = true } });
                modifierCountField = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.FullRunManager").GetField("modifiers_count", Flags);
                savedModifierCount = modifierCountField.GetValue(null);
                achievementsProperty = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementRegister").GetProperty("AchievementsDisabled", Flags);
                achievementsDisabled = (bool)achievementsProperty.GetValue(null, null);
                // in-memory inventory only: never call native save setters
                cache[folder + "general_settings.set"] = new GeneralSettings().GetDefault();
                Snake(false);
            }
            internal void Snake(bool enabled)
            {
                var inventory = new Inventory().GetDefault();
                if (enabled) inventory.items.Add(new InventoryItem { item = Items.SnakeRing, count = 1 });
                cache[folder + "inventory.inv"] = inventory;
            }
            internal void Direction(int direction)
            { Buttons(direction < 0, direction > 0); }
            internal void Buttons(bool left, bool right)
            {
                var current = (PadState)typeof(PadInstance).GetField("current_state", Flags).GetValue(Pad);
                typeof(PadInstance).GetField("last_state", Flags).SetValue(Pad, current);
                typeof(PadInstance).GetField("current_state", Flags).SetValue(Pad, new PadState { left = left, right = right });
            }
            public void Dispose()
            {
                ControllerManager.instance = previous;
                cache.Clear(); foreach (var pair in oldCache) cache.Add(pair.Key, pair.Value);
                savedLoopField.SetValue(null, savedLoop); combinedField.SetValue(null, savedCombined);
                modifierCountField.SetValue(null, savedModifierCount);
                achievementsProperty.SetValue(null, achievementsDisabled, null);
            }
        }

        private static void WalkScene(params IBlock[] blocks)
        {
            typeof(LevelManager).GetField("m_screens", Flags).SetValue(null, Scene(blocks));
            typeof(LevelManager).GetField("_total_screens", Flags).SetValue(null, 1);
            typeof(Camera).GetField("_current_screen", Flags).SetValue(null, 0);
        }
        private static void WalkTicks(PlayerEntity player, WalkInputFixture input, int direction, int count)
        {
            for (int tick = 0; tick < count; tick++)
            { input.Direction(direction); player.UpdateComponents(1f / 60f); }
        }
        private static PlayerEntity NoWalkPlayer()
        {
            var player = ResumePlayer();
            var behaviours = NativeFlight.Get<LinkedList<JumpKing.API.IBodyCompBehaviour>>(player.m_body, "m_behaviours");
            // Omit graphics/audio side effects, not movement or collision. this
            // headless fixture has no particle renderer or native sound assets
            foreach (var behaviour in behaviours.ToArray())
                if (behaviour.GetType().Name == "WaterParticleSpawningBehaviour"
                    || behaviour.GetType().Name == "PlayBumpSFXBehaviour") behaviours.Remove(behaviour);
            return player;
        }
    }
}
