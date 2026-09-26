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

namespace MegaGameplayExpansion
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
                // In-memory inventory only: never call native save setters.
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
            // Omit graphics/audio side effects, not movement or collision. This
            // headless fixture has no particle renderer or native sound assets.
            foreach (var behaviour in behaviours.ToArray())
                if (behaviour.GetType().Name == "WaterParticleSpawningBehaviour"
                    || behaviour.GetType().Name == "PlayBumpSFXBehaviour") behaviours.Remove(behaviour);
            return player;
        }
        private static void NoWalkOffRegression()
        {
            bool global = Settings.Current.NoWalkOff;
            var savedScreens = MapPixels.NoWalkOff.Screens.ToArray();
            try
            {
                Settings.Current.NoWalkOff = false;
                using (var input = new WalkInputFixture())
                {
                    NoWalkOffInactiveParity(input);
                    foreach (bool slope in new[] { false, true })
                    foreach (bool water in new[] { false, true })
                    foreach (int direction in new[] { -1, 1 })
                    foreach (string scope in new[] { "solid", "zone", "screen" })
                    {
                        input.Snake(false);
                        MapPixels.NoWalkOff.Screens.Clear();
                        var floor = scope == "solid" ? (IBlock)new NoWalkOffSurfaceBlock(new Rectangle(80, 320, 160, 40))
                            : new BoxBlock(new Rectangle(80, 320, 160, 40));
                        var blocks = new List<IBlock> { floor };
                        if (scope == "zone") blocks.Add(new NoWalkOffZoneBlock(new Rectangle(32, 240, 240, 80)));
                        if (scope == "screen") MapPixels.NoWalkOff.Screens.Add(0);
                        if (water) blocks.Add(new WaterBlock(new Rectangle(0, 0, 480, 360)));
                        if (slope) blocks.Add(new SlopeBlock(new Rectangle(direction > 0 ? 240 : 48, 320, 32, 32),
                            direction > 0 ? SlopeType.TopRight : SlopeType.TopLeft));
                        WalkScene(blocks.ToArray());
                        var player = NoWalkPlayer(); player.m_body.Position.X = 150;
                        int before = player.m_body.GetBehaviourList().Count;
                        using (var controller = new NoWalkOffController(player))
                        {
                            WalkTicks(player, input, direction, 350);
                            float edge = direction > 0 ? 239 : 63;
                            Require(player.m_body.IsOnGround && player.m_body.Position.X == edge,
                                "Missed exact edge: " + scope + " slope=" + slope + " water=" + water + " dir=" + direction + " pos=" + player.m_body.Position);
                            var snapshot = controller.Capture(); controller.Validate(snapshot);
                            WalkTicks(player, input, direction, 60);
                            Require(player.m_body.Position.X == edge && player.m_body.IsOnGround, "Held direction walked off");
                            for (int tick = 0; tick < 5; tick++)
                            { input.Buttons(true, true); player.UpdateComponents(1f / 60f); }
                            WalkTicks(player, input, direction, 15);
                            Require(player.m_body.Position.X == edge && player.m_body.IsOnGround, "Opposing held directions counted as an outward release");
                            WalkTicks(player, input, 0, 5);
                            Require(player.m_body.Position.X == edge && player.m_body.IsOnGround, "Neutral drift after edge stop");
                            WalkTicks(player, input, direction, 20);
                            Require(!player.m_body.IsOnGround, "Second press did not permit walk-off");

                            player.m_body.Position = new Vector2(edge, 294); player.m_body.Velocity = Vector2.Zero;
                            NativeFlight.Set(player.m_body, "_is_on_ground", true);
                            controller.Restore(snapshot);
                            WalkTicks(player, input, direction, 30);
                            Require(player.m_body.IsOnGround && player.m_body.Position.X == edge, "Restore leaked a walk-off permit");
                            player.m_body.Velocity = new Vector2(direction * 3.5f, -6);
                            player.UpdateComponents(1f / 60f);
                            Require(player.m_body.Position.Y < 294 && player.m_body.Position.X != edge, "Edge guard blocked jump takeoff");
                        }
                        Require(player.m_body.GetBehaviourList().Count == before, "No Walk Off leaked pipeline entries");
                    }
                    input.Snake(false);
                    NoWalkOffSlipperyParity(input);
                    NoWalkOffExternalMotion(input);
                    NoWalkOffWarp(input);
                    NoWalkOffMapPermission(input);
                    NoWalkOffBoundaries(input);
                }
            }
            finally
            {
                Settings.Current.NoWalkOff = global;
                MapPixels.NoWalkOff.Screens.Clear(); foreach (int value in savedScreens) MapPixels.NoWalkOff.Screens.Add(value);
            }
            Console.WriteLine("[OK] No Walk Off: 24 native entity cases, both edges, descending slope seams, water, Solid/Zone/Screen, hold/release/repress, snapshots, jumps and cleanup");
        }

        private static void NoWalkOffInactiveParity(WalkInputFixture input)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Preferences));
            using (var xml = new System.IO.StringReader("<Preferences><WarpJump>true</WarpJump></Preferences>"))
            {
                var preferences = (Preferences)serializer.Deserialize(xml);
                Require(preferences.WarpJump && !preferences.NoWalkOff, "Existing settings must retain Warp and default No Walk Off to off");
            }
            MapPixels.NoWalkOff.Screens.Clear();
            WalkScene(new BoxBlock(new Rectangle(80, 320, 80, 40)));
            var expected = NoWalkPlayer(); var actual = NoWalkPlayer();
            expected.m_body.Position.X = actual.m_body.Position.X = 100;
            using (var controller = new NoWalkOffController(actual))
            {
                for (int tick = 0; tick < 60; tick++)
                {
                    input.Direction(1); expected.UpdateComponents(1f / 60f); actual.UpdateComponents(1f / 60f);
                    Require(actual.m_body.Position == expected.m_body.Position && actual.m_body.Velocity == expected.m_body.Velocity
                        && actual.m_body.IsOnGround == expected.m_body.IsOnGround, "Inactive No Walk Off changed native walking/falling");
                }
            }
            Console.WriteLine("[OK] No Walk Off: old settings preserved; disabled native walk/fall parity");
        }

        private static void NoWalkOffSlipperyParity(WalkInputFixture input)
        {
            MapPixels.NoWalkOff.Screens.Clear(); MapPixels.NoWalkOff.Screens.Add(0);
            foreach (bool snake in new[] { false, true })
            foreach (bool water in new[] { false, true })
            foreach (int direction in new[] { -1, 1 })
            {
                input.Snake(snake);
                var blocks = new List<IBlock> { snake ? (IBlock)new NoWalkOffSurfaceBlock(new Rectangle(80, 320, 160, 40))
                    : new IceBlock(new Rectangle(80, 320, 160, 40)) };
                if (water) blocks.Add(new WaterBlock(new Rectangle(0, 0, 480, 360)));
                WalkScene(blocks.ToArray());
                var expected = NoWalkPlayer(); var actual = NoWalkPlayer();
                expected.m_body.Position.X = actual.m_body.Position.X = 150;
                using (var controller = new NoWalkOffController(actual))
                {
                    bool leftPlatform = false;
                    for (int tick = 0; tick < 350; tick++)
                    {
                        input.Direction(direction); expected.UpdateComponents(1f / 60f); actual.UpdateComponents(1f / 60f);
                        Require(actual.m_body.Position == expected.m_body.Position && actual.m_body.Velocity == expected.m_body.Velocity
                            && actual.m_body.IsOnGround == expected.m_body.IsOnGround,
                            "Slippery movement changed: snake=" + snake + " water=" + water + " dir=" + direction);
                        if (actual.m_body.Position.Y > 295) { leftPlatform = true; break; }
                    }
                    Require(leftPlatform, "Ice / Snake Ring did not allow continuous walk-off");
                }
            }
            input.Snake(false);
            Console.WriteLine("[OK] No Walk Off: 8 ice/Snake Ring native walk-off parity cases, dry/water, both directions");
        }

        private static void NoWalkOffWarp(WalkInputFixture input)
        {
            MapPixels.NoWalkOff.Screens.Clear(); MapPixels.NoWalkOff.Screens.Add(0);
            bool added = MapPixels.Warp.Screens.Add(0);
            try
            {
                WalkScene(new NoWalkOffSurfaceBlock(new Rectangle(80, 280, 80, 16)),
                    new BoxBlock(new Rectangle(0, 340, 480, 20)),
                    new NoWalkOffZoneBlock(new Rectangle(80, 220, 80, 60)));
                var player = NoWalkPlayer(); player.m_body.Position = new Vector2(100, 254);
                using (var warp = new WarpController(player))
                using (var edge = new NoWalkOffController(player))
                {
                    // Settle first: a freshly constructed test body has Vy=0,
                    // unlike the steady native grounded state after gravity.
                    player.m_body.Velocity.Y = 1;
                    WalkTicks(player, input, 1, 100);
                    Require(player.m_body.Enabled && player.m_body.IsOnGround && player.m_body.Position.X == 159,
                        "Warp ran before the protected walk-off");
                    WalkTicks(player, input, 0, 3);
                    for (int i = 0; i < 10 && player.m_body.Enabled; i++) WalkTicks(player, input, 1, 1);
                    Require(!player.m_body.Enabled, "Permitted walk-off did not start Warp");
                    for (int i = 0; i < 40 && !player.m_body.Enabled; i++) WalkTicks(player, input, 1, 1);
                    Require(player.m_body.Enabled && player.m_body.IsOnGround && player.m_body.Position.Y == 314,
                        "Combined Warp did not land on lower platform");
                    WalkTicks(player, input, 1, 250);
                    Require(player.m_body.IsOnGround, "Combined controller lost ground after landing");
                }
            }
            finally { if (added) MapPixels.Warp.Screens.Remove(0); }
            Console.WriteLine("[OK] No Walk Off: Warp walk-off/landing integration");
        }

        private static void NoWalkOffMapPermission(WalkInputFixture input)
        {
            MapPixels.NoWalkOff.Screens.Clear(); Settings.Current.NoWalkOff = true;
            var previous = Game1.instance.contentManager.level;
            var level = (JumpKing.Workshop.Level)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Workshop.Level));
            typeof(JumpKing.Workshop.Level).GetField("_level", Flags).SetValue(level,
                new JumpKing.Workshop.Level.LevelSettings { Tags = new[] { "AllowMegaGameplayExpansion" } });
            Game1.instance.contentManager.level = level;
            try
            {
                WalkScene(new BoxBlock(new Rectangle(80, 320, 80, 40)));
                var player = NoWalkPlayer(); player.m_body.Position.X = 100;
                using (var controller = new NoWalkOffController(player))
                {
                    WalkTicks(player, input, 1, 100);
                    Require(player.m_body.IsOnGround && player.m_body.Position.X == 159, "Map-permitted global mode inactive");
                    Require(FullRunSave.fullRunSave.CurrentBodyCompModifiers == 0, "Authored/allowed use marked the run");
                }
            }
            finally { Game1.instance.contentManager.level = previous; Settings.Current.NoWalkOff = false; }
        }

        private static void NoWalkOffBoundaries(WalkInputFixture input)
        {
            MapPixels.NoWalkOff.Screens.Clear();
            // A local zone is not sticky after leaving it. Neither is Solid
            // activation inherited by a neighbouring ordinary platform.
            foreach (bool surface in new[] { false, true })
            {
                WalkScene(surface ? (IBlock)new NoWalkOffSurfaceBlock(new Rectangle(80, 320, 40, 40))
                    : new NoWalkOffZoneBlock(new Rectangle(80, 260, 40, 60)),
                    new BoxBlock(new Rectangle(surface ? 120 : 80, 320, surface ? 120 : 160, 40)));
                var player = NoWalkPlayer(); player.m_body.Position.X = 90;
                using (var controller = new NoWalkOffController(player))
                {
                    WalkTicks(player, input, 1, 115);
                    Require(!player.m_body.IsOnGround, "Activation persisted outside its authored scope");
                }
            }
            MapPixels.NoWalkOff.Screens.Add(0);
            // Joined rectangles are not separate edges. A lower platform is not
            // support at the current elevation. Foreign impulses remain native.
            WalkScene(new BoxBlock(new Rectangle(80, 320, 40, 40)), new BoxBlock(new Rectangle(120, 320, 120, 40)),
                new BoxBlock(new Rectangle(245, 336, 50, 24)));
            var walker = NoWalkPlayer(); walker.m_body.Position.X = 100;
            using (var controller = new NoWalkOffController(walker))
            {
                WalkTicks(walker, input, 1, 130);
                Require(walker.m_body.Position.X == 239, "Joined/lower platforms misidentified as support");
                WalkTicks(walker, input, -1, 10);
                Require(walker.m_body.Position.X < 239, "Retreat from edge blocked");
                WalkTicks(walker, input, 1, 30);
                Require(walker.m_body.Position.X == 239 && walker.m_body.IsOnGround, "Returning approach bypassed edge");
            }
            WalkScene(new BoxBlock(new Rectangle(80, 320, 40, 40)), new BoxBlock(new Rectangle(152, 320, 100, 40)));
            walker = NoWalkPlayer(); walker.m_body.Position.X = 118; walker.m_body.Velocity = new Vector2(45, 1);
            NativeFlight.Set(walker.m_body, "_is_on_ground", true);
            using (var controller = new NoWalkOffController(walker))
            {
                WalkTicks(walker, input, 1, 1);
                Require(walker.m_body.Position.X == 163 && walker.m_body.IsOnGround,
                    "No Walk Off intercepted a 45px inherited impulse instead of yielding");
            }
            // Global mode uses the same controller even with no authored pixels.
            MapPixels.NoWalkOff.Screens.Clear(); Settings.Current.NoWalkOff = true;
            walker = NoWalkPlayer(); walker.m_body.Position.X = 100;
            using (var controller = new NoWalkOffController(walker))
            {
                WalkTicks(walker, input, 1, 40);
                Require(walker.m_body.Position.X == 119 && walker.m_body.IsOnGround, "Global mode inactive");
                Require(FullRunSave.fullRunSave.CurrentBodyCompModifiers == 1
                    && NativeFlight.Get<uint>(walker.m_body, "m_externalBehavioursCount") == 0,
                    "Global use must mark the run after BodyComp, without retaining a marker");
                Settings.Current.NoWalkOff = false;
                WalkTicks(walker, input, 1, 10);
                Require(!walker.m_body.IsOnGround, "Disabling global mode retained edge guard");
            }
            Console.WriteLine("[OK] No Walk Off scopes, joined terrain, lower ledges, swept gaps, retreat/re-approach and global toggle");
        }
    }
}
