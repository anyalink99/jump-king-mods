using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JKRuntime.Simulation;
using JumpKing;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void NativeParity()
        {
            var fields = new[] { typeof(LevelManager).GetField("m_screens", NativeWorld.Flags), typeof(LevelManager).GetField("_total_screens", NativeWorld.Flags),
                typeof(Camera).GetField("_current_screen", NativeWorld.Flags) };
            var old = fields.Select(f => f.GetValue(null)).ToArray();
            var save = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true);
            var cache = (Dictionary<string, object>)save.GetField("loaded_objects", NativeWorld.Flags).GetValue(null);
            var oldCache = new Dictionary<string, object>(cache);
            string folder = (string)save.GetField("PERMANENT_FOLDER", NativeWorld.Flags).GetValue(null);
            cache[folder + "inventory.inv"] = new JumpKing.MiscEntities.WorldItems.Inventory.Inventory().GetDefault();
            cache[folder + "general_settings.set"] = new JumpKing.SaveThread.GeneralSettings().GetDefault();
            try
            {
                InstalledMapRoute();
                ControlParity();
                WorldParity();
                VerticalWindParity();
                PassiveParity();
                WorkshopParity();
                NativeRoute();
                int cases = 0, ticks = 0;
                foreach (Type floor in new[] { typeof(BoxBlock), typeof(IceBlock), typeof(SnowBlock), typeof(SandBlock) })
                foreach (bool water in new[] { false, true })
                foreach (int direction in new[] { -1, 0, 1 })
                foreach (float impulse in new[] { 0f, -1f, -4f, -8.742858f })
                {
                    var blocks = new List<IBlock> { (IBlock)Activator.CreateInstance(floor, new Rectangle(0, 320, 480, 40)),
                        new BoxBlock(new Rectangle(270, 210, 20, 110)), new BoxBlock(new Rectangle(90, 150, 90, 8)) };
                    if (floor == typeof(SandBlock)) blocks.Add(new BoxBlock(new Rectangle(0, 352, 480, 8)));
                    if (water) blocks.Add(new WaterBlock(new Rectangle(0, -360, 480, 720)));
                    var screens = new[] { new LevelScreen(0, blocks.ToArray(), new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) };
                    fields[0].SetValue(null, screens); fields[1].SetValue(null, 1); fields[2].SetValue(null, 0);
                    var reference = new BodyComp(new Vector2(180, 294), 18, 26) { Velocity = new Vector2(direction * 3.5f, impulse) };
                    NativePlayer.Set(reference, "_is_on_ground", false);
                    var behaviours = NativePlayer.Field<LinkedList<IBodyCompBehaviour>>(reference, "m_behaviours");
                    foreach (var b in behaviours.ToArray())
                        if (b.GetType().Name == "WaterParticleSpawningBehaviour" || b.GetType().Name == "PlayBumpSFXBehaviour") behaviours.Remove(b);
                    var memory = NativePlayer.Capture(reference);
                    var model = new NativePlayer(screens, memory, false) { ControlsEnabled = false };
                    var registry = new SimulationRegistry();
                    using (registry.Register(model.Provider()))
                    using (var session = registry.Open(new SimulationSeed(new SimulationPose { Position = reference.Position, Velocity = reference.Velocity,
                        Width = 18, Height = 26 }, 0, 1.0 / 60, NativeWind.AuditedGameSha256, new byte[0], new[] { NativePlayer.Requirement })))
                    {
                        var state = session.Initial;
                        for (int t = 0; t < 350; t++)
                        {
                            typeof(BodyComp).GetMethod("UpdateInternal", NativeWorld.Flags).Invoke(reference, new object[] { 1f / 60 });
                            state = session.Step(state, new SimulationInput()).State; ticks++;
                            Check(state.Pose.Position == reference.Position && state.Pose.Velocity == reference.Velocity && state.Pose.Grounded == reference.IsOnGround,
                                "Native body mismatch " + floor.Name + " water=" + water + " t=" + t + " expected=" + reference.Position + "/" + reference.Velocity + " actual=" + state.Pose.Position + "/" + state.Pose.Velocity);
                            if (reference.IsOnGround) break;
                        }
                    }
                    cases++;
                }
                // Side links can lead to lower-index screens. Goal topology
                // comes from the links, never from an assumed upward offset.
                var linked = new[] { new LevelScreen(0, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null),
                    new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), false, new[] { new TeleportLink(1) }, 0, null) };
                Check(new NativeWorld(linked, 1).Targets().SequenceEqual(new[] { 0 }), "Downward teleport target lost");
                var exits = new NativeWorld(linked, 1).ExitTargets();
                Check(exits.Select(t => t.Direction).SequenceEqual(new[] { ExitDirection.Left, ExitDirection.Right, ExitDirection.Down }),
                    "Shared side/down destination collapsed distinct exits");
                linked = new[] { linked[0], linked[1], new LevelScreen(2, new IBlock[0], new LevelScreen.Graphics(), false, new TeleportLink[0], 0, null) };
                linked[1] = new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), false, new[] { new TeleportLink(1), new TeleportLink(3) }, 0, null);
                exits = new NativeWorld(linked, 1).ExitTargets();
                Check(exits.Select(t => t.Direction).SequenceEqual(new[] { ExitDirection.Up, ExitDirection.Left, ExitDirection.Right, ExitDirection.Down }) &&
                    exits.Select(t => t.Screen).SequenceEqual(new[] { 2, 0, 2, 0 }), "Native paired teleport ordering");
                linked[1] = new LevelScreen(1, new IBlock[0], new LevelScreen.Graphics(), false, new[] { new TeleportLink(-1), new TeleportLink(3) }, 0, null);
                exits = new NativeWorld(linked, 1).ExitTargets();
                Check(exits[1].Screen == 2 && exits[2].Screen == 2, "Single enabled link must serve both sides, as in native gameplay");
                Console.WriteLine("[OK] Native solver body: " + cases + " trajectories / " + ticks + " exact ticks; materials, water, walls, ceilings and teleport target discovery");
            }
            finally
            {
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, old[i]);
                cache.Clear(); foreach (var pair in oldCache) cache.Add(pair.Key, pair.Value);
            }
        }
    }
}
