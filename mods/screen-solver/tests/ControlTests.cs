using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using HarmonyLib;
using JKRuntime.Simulation;
using JumpKing;
using JumpKing.API;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static bool SkipPresentation() { return false; }
        private static bool SkipShake(ref BTresult __result) { __result = BTresult.Success; return false; }
        private static object Empty(Type type) { return FormatterServices.GetUninitializedObject(type); }
        private static Action afterReferenceStep, afterModelStep;
        private static Action<PlayerEntity> scenePlayer;
        private static Action<PlayerEntity> capturePlayer;
        private static void ControlParity(bool muteFixture = false, bool subframeFixture = false)
        {
            var harmony = new Harmony("screen-solver.headless-tests-only");
            var skip = new HarmonyMethod(typeof(Tests).GetMethod("SkipPresentation", NativeWorld.Flags));
            var gameField = typeof(Game1).GetField("_instance", NativeWorld.Flags);
            object oldGame = gameField.GetValue(null); var oldController = ControllerManager.instance;
            var fixtureSave = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            var fixtureCache = (Dictionary<string, object>)fixtureSave.GetField("loaded_objects", NativeWorld.Flags).GetValue(null);
            string inventoryKey = (string)fixtureSave.GetField("PERMANENT_FOLDER", NativeWorld.Flags).GetValue(null) + "inventory.inv";
            object oldInventory = fixtureCache[inventoryKey];
            try
            {
                // Suppress only audiovisual endpoints. Never patch physics,
                // input, charge, walking, timers or behaviour-tree decisions.
                foreach (var t in new[] { typeof(JumpState), typeof(IsOnGround) })
                    harmony.Patch(t.GetMethod("HandleParticles", NativeWorld.Flags), prefix: skip);
                harmony.Patch(typeof(JumpKing.MiscSystems.SetScreenShakeNode).GetMethod("MyRun", NativeWorld.Flags),
                    prefix: new HarmonyMethod(typeof(Tests).GetMethod("SkipShake", NativeWorld.Flags)));
                var game = (Game1)Empty(typeof(Game1));
                GC.SuppressFinalize(game); // No graphics platform was constructed in this fixture.
                game.contentManager = (JKContentManager)Empty(typeof(JKContentManager));
                game.contentManager.playerSprites = new JKContentManager.PlayerSprites();
                foreach (var p in typeof(JKContentManager.PlayerSprites).GetProperties())
                    if (p.PropertyType.Name == "Sprite") harmony.Patch(p.GetGetMethod(), prefix: skip);
                gameField.SetValue(null, game);
                var manager = (ControllerManager)Empty(typeof(ControllerManager)); ControllerManager.instance = manager;
                var pad = (PadInstance)Empty(typeof(PadInstance));
                NativePlayer.Set(manager, "m_pads", new List<PadInstance> { pad });
                int count = 0;
                foreach (Type floor in new[] { typeof(BoxBlock), typeof(IceBlock), typeof(SnowBlock), typeof(SandBlock) })
                foreach (bool water in new[] { false, true })
                foreach (int hold in new[] { 1, 2, 10, 35, 36, 60, 72 })
                foreach (int direction in new[] { -1, 0, 1 })
                foreach (bool ledge in new[] { false, true })
                foreach (bool snake in new[] { false, true })
                {
                    var saveType = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
                    var cache = (Dictionary<string, object>)saveType.GetField("loaded_objects", NativeWorld.Flags).GetValue(null);
                    string folder = (string)saveType.GetField("PERMANENT_FOLDER", NativeWorld.Flags).GetValue(null);
                    var inventory = new JumpKing.MiscEntities.WorldItems.Inventory.Inventory().GetDefault();
                    if (snake) inventory.items.Add(new JumpKing.MiscEntities.WorldItems.Inventory.InventoryItem {
                        item = JumpKing.MiscEntities.WorldItems.Items.SnakeRing, count = 1 });
                    cache[folder + "inventory.inv"] = inventory;
                    var blocks = new List<IBlock> { (IBlock)Activator.CreateInstance(floor, new Rectangle(0, 320, ledge ? 260 : 480, 30)),
                        new BoxBlock(new Rectangle(0, 355, 480, 5)) };
                    if (water) blocks.Add(new WaterBlock(new Rectangle(0, -360, 480, 720)));
                    Type muteBlock = muteFixture ? AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MuteJumpSfxBlock")
                        .GetType("MuteJumpSfxBlock.Blocks.BlockMuteJumpSfx", true) : null;
                    if (muteFixture) blocks.Add((IBlock)Activator.CreateInstance(muteBlock, new Rectangle(0, 0, 480, 360)));
                    var screens = new[] { new LevelScreen(0, blocks.ToArray(), new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) };
                    typeof(LevelManager).GetField("m_screens", NativeWorld.Flags).SetValue(null, screens);
                    typeof(LevelManager).GetField("_total_screens", NativeWorld.Flags).SetValue(null, 1);
                    typeof(Camera).GetField("_current_screen", NativeWorld.Flags).SetValue(null, 0);
                    var body = new BodyComp(new Vector2(ledge ? 256 : 240, 294), 18, 26);
                    if (muteFixture) body.RegisterBlockBehaviour(muteBlock, (IBlockBehaviour)Activator.CreateInstance(
                        muteBlock.Assembly.GetType("MuteJumpSfxBlock.Behaviours.BehaviourMuteJumpSfx", true)));
                    var player = (PlayerEntity)Empty(typeof(PlayerEntity)); player.m_body = body;
                    typeof(Entity).GetField("m_components", NativeWorld.Flags).SetValue(player, new List<Component>());
                    var input = new InputComponent(); player.AddComponents(body, input);
                    var tree = (BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT", NativeWorld.Flags).Invoke(player, null);
                    player.AddComponents(tree);
                    if (scenePlayer != null) scenePlayer(player);
                    var behaviours = NativePlayer.Field<LinkedList<IBodyCompBehaviour>>(body, "m_behaviours");
                    foreach (var b in behaviours.ToArray())
                        if (b.GetType().Name == "WaterParticleSpawningBehaviour" || b.GetType().Name == "PlayBumpSFXBehaviour") behaviours.Remove(b);
                    var memory = NativePlayer.Capture(body); memory.Subframe = subframeFixture;
                    var model = new NativePlayer(muteFixture ? NativeWorld.Capture() : screens, memory, snake);
                    var registry = new SimulationRegistry();
                    using (registry.Register(model.Provider()))
                    using (var session = registry.Open(new SimulationSeed(new SimulationPose { Position = body.Position, Width = 18, Height = 26 },
                        0, 1.0 / 60, NativeWind.AuditedGameSha256, new byte[0], new[] { NativePlayer.Requirement })))
                    {
                        var state = session.Initial; PadState last = new PadState();
                        int pressTick = 0; bool buffered = false;
                        for (int t = 0; t < 210; t++)
                        {
                            bool jump = (t >= 3 && t < 3 + hold) || (ledge && t >= 100 && t < 100 + hold);
                            int dir = ledge && t < 3 ? 1 : t < 3 || t > hold + 3 ? 0 : direction;
                            var current = new PadState { left = dir < 0, right = dir > 0, jump = jump };
                            if (current.jump && !last.jump) pressTick = t;
                            NativePlayer.Set(pad, "last_state", last); NativePlayer.Set(pad, "current_state", current); last = current;
                            typeof(BodyComp).GetMethod("UpdateInternal", NativeWorld.Flags).Invoke(body, new object[] { 1f / 60 });
                            typeof(InputComponent).GetMethod("Update", NativeWorld.Flags).Invoke(input, new object[] { 1f / 60 });
                            var charge = NativePlayer.Field<JumpState>(player, "m_jump_state"); bool wasCharging = charge.IsRunning();
                            if (subframeFixture && wasCharging && !current.jump && !buffered)
                            {
                                float multiplier = ((JumpKing.BlockBehaviours.WaterBlockBehaviour)NativePlayer.Block(body, typeof(WaterBlock))).GetWaterMultiplier();
                                NativePlayer.Set(charge, "m_timer", InstalledSubframeTimer((t - pressTick) * .017, multiplier));
                            }
                            tree.GetRaw().Run(1f / 60);
                            if (!wasCharging && charge.IsRunning()) buffered = t > pressTick;
                            if (afterReferenceStep != null) afterReferenceStep();
                            state = session.Step(state, new SimulationInput(dir, jump)).State;
                            if (afterModelStep != null) afterModelStep();
                            Check(state.Pose.Position == body.Position && state.Pose.Velocity == body.Velocity,
                                "Controller parity " + floor.Name + " water=" + water + " ledge=" + ledge + " ring=" + snake + " hold=" + hold + " dir=" + dir + " tick=" + t +
                                " native=" + body.Position + "/" + body.Velocity + " model=" + state.Pose.Position + "/" + state.Pose.Velocity);
                            count++;
                        }
                    }
                }
                Console.WriteLine("[OK] Native full control tree: " + count + " exact ticks (charge, release, auto-jump, walking, water, ice, snow, sand)");
                CaptureParity(harmony, game);
            }
            finally { harmony.UnpatchAll(harmony.Id); gameField.SetValue(null, oldGame); ControllerManager.instance = oldController; fixtureCache[inventoryKey] = oldInventory; }
        }
        private static void CaptureParity(Harmony harmony, Game1 game)
        {
            foreach (var type in new[] { typeof(JumpState), typeof(IsOnGround) })
                harmony.Unpatch(type.GetMethod("HandleParticles", NativeWorld.Flags), HarmonyPatchType.All, harmony.Id);
            var managerType = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
            var statsType = typeof(BodyComp).Assembly.GetType("JumpKing.MiscSystems.Achievements.PlayerStats", true);
            var managerField = managerType.GetField("instance", NativeWorld.Flags); object previous = managerField.GetValue(null);
            try
            {
                var manager = Empty(managerType); managerField.SetValue(null, manager);
                managerType.GetField("m_snapshot", NativeWorld.Flags).SetValue(manager, Activator.CreateInstance(statsType));
                managerType.GetField("m_all_time_stats", NativeWorld.Flags).SetValue(manager, Activator.CreateInstance(statsType));
                typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime", NativeWorld.Flags).SetValue(game, TimeSpan.FromTicks(166667));
                var body = new BodyComp(new Vector2(240, 294), 18, 26); NativePlayer.Set(body, "_is_on_ground", true);
                var player = (PlayerEntity)Empty(typeof(PlayerEntity)); player.m_body = body;
                typeof(Entity).GetField("m_components", NativeWorld.Flags).SetValue(player, new List<Component>());
                player.AddComponents(body, new InputComponent());
                player.AddComponents((BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT", NativeWorld.Flags).Invoke(player, null));
                if (scenePlayer != null) scenePlayer(player);
                if (capturePlayer != null) capturePlayer(player);
                var before = body.Position;
                if (afterReferenceStep != null) afterReferenceStep();
                using (var captured = SolveCapture.Capture(player))
                {
                    Check(captured.Seed.Pose.Position == before && captured.Seed.TickSeconds == 1.0 / 60, "Live capture shifted player or used wind-clock delta for charge");
                    Check(JKRuntime.RuntimeApi.Simulation.ActiveSessions == 0, "Capture advanced search before Solve's first batch");
                    captured.Search.Advance(1, 1);
                }
                Check(body.Position == before && body.Enabled && JKRuntime.RuntimeApi.Simulation.ActiveSessions == 0, "Capture/cancel touched live body or leaked a session");
                if (afterModelStep != null) afterModelStep();
                Console.WriteLine("[OK] Live capture contract: native tree audit, distinct clocks, lazy search, cancellation and unchanged player");
            }
            finally { managerField.SetValue(null, previous); }
        }
    }
}
