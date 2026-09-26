using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;

namespace ScreenSolver
{
    internal static partial class Tests
    {
        private static void WorkshopParity()
        {
            var assemblies = WorkshopCoverage.Contracts.ToDictionary(c => c.File, c => Fixture(c.File));
            var owners = WorkshopCoverage.Contracts.ToDictionary(c => c.File, c => new Harmony(c.Owner.Length == 0 ? "fixture.unpatched." + c.File : c.Owner));
            var managerField = typeof(EntityManager).GetField("_instance", NativeWorld.Flags);
            var oldManager = managerField.GetValue(null); managerField.SetValue(null, null);
            var manager = new EntityManager();
            var expansion = assemblies["JumpKing-Expansion-Blocks"].GetType("JumpKing_Expansion_Blocks.ModEntry", true);
            var ghost = assemblies["JumpKing-GhostOfTheImmortalBabeBlocks"].GetType("JumpKing_GhostOfTheImmortalBabeBlocks.ModEntry", true);
            var oldPlayer = expansion.GetProperty("Player").GetValue(null, null);
            try
            {
                foreach (var pair in assemblies)
                {
                    if (pair.Key == "JumpKing-Expansion-Blocks") expansion.GetMethod("PatchWithHarmony", NativeWorld.Flags).Invoke(null, new object[] { owners[pair.Key] });
                    else if (pair.Key == "JumpKing-GhostOfTheImmortalBabeBlocks") ghost.GetMethod("PatchWithHarmony", NativeWorld.Flags).Invoke(null, null);
                    else if (pair.Key == "UpSideDownCore") pair.Value.GetType("UpsideDownCore.UpsideDownCore", true).GetMethod("BeforeLevelLoad").Invoke(null, null);
                    else foreach (var type in pair.Value.GetTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0))
                    {
                        if (type.FullName == "AntiBlocks.Patches.PatchGameLoop") continue; // Texture loading, no simulation relevance.
                        owners[pair.Key].CreateClassProcessor(type).Patch();
                    }
                }
                Console.WriteLine("[INFO] Full Workshop fixture patched: " + string.Join(", ", assemblies.Keys));
                SolveCapture.AuditPatches(typeof(BodyComp).Assembly, new VerticalWindAdapter());
                scenePlayer = player => {
                    var entities = (List<Entity>)typeof(EntityManager).GetField("entities", NativeWorld.Flags).GetValue(manager);
                    entities.Clear(); entities.Add(player); expansion.GetProperty("Player").SetValue(null, player, null);
                    ghost.GetMethod("OnLevelStart").Invoke(null, null);
                    assemblies["ConveyorBlockMod"].GetType("ConveyorBlockMod.ModEntry", true).GetMethod("OnLevelStart").Invoke(null, null);
                    foreach (var pair in new[] {
                        new[] { "HighGravityBlockMod", "HighGravityBlockMod.HighGravityBlock", "HighGravityBlockMod.HighGravityBlockBehaviour" },
                        new[] { "SampleJkMod", "SampleJkMod.LowGravityBlock", "JumpKingLowGravityBlockMod.LowGravityBlockBehaviour" },
                        new[] { "JumpKing-UpsideDownBlocks", "JumpKing_UpsideDownBlocks.Blocks.UpsideDown", "JumpKing_UpsideDownBlocks.Behaviors.UpsideDown" }
                    }) player.m_body.RegisterBlockBehaviour(assemblies[pair[0]].GetType(pair[1], true), (IBlockBehaviour)Activator.CreateInstance(assemblies[pair[0]].GetType(pair[2], true), true));
                    foreach (string name in new[] { "ThinSnow", "Warp", "LegacyWarp", "OneWay", "LowGravity" })
                    {
                        var type = assemblies["JumpKingPlus"].GetType("JumpKingPlus.BlockBehaviours." + name + "BlockBehaviour", true);
                        var behaviour = (IBlockBehaviour)(name == "ThinSnow" ? Activator.CreateInstance(type, new object[] { player }) : Activator.CreateInstance(type, true));
                        player.m_body.RegisterBlockBehaviour(assemblies["JumpKingPlus"].GetType("JumpKingPlus.Blocks." + name + "Block", true), behaviour);
                    }
                };
                var watchedTypes = new[] {
                    assemblies["ConveyorBlockMod"].GetType("ConveyorBlockMod.Patches.ResolveXCollisionBehaviourPatch", true),
                    assemblies["JumpKing-Expansion-Blocks"].GetType("JumpKing_Expansion_Blocks.Patches.PatchedResolveXCollisionBehaviour", false)
                        ?? assemblies["JumpKing-Expansion-Blocks"].GetTypes().Single(t => t.Name == "PatchedResolveXCollisionBehaviour"),
                    assemblies["UpSideDownCore"].GetType("UpsideDownCore.Models.Manager", true),
                    assemblies["AntiBlocks"].GetType("AntiBlocks.Patches.PatchInventoryManager", true)
                };
                var watched = watchedTypes.SelectMany(t => t.GetFields(NativeWorld.Flags).Where(f => f.IsStatic && !f.IsInitOnly && !f.IsLiteral)).ToArray();
                object[] snapshot = null;
                afterReferenceStep = () => snapshot = watched.Select(f => f.GetValue(null)).ToArray();
                afterModelStep = () => Check(watched.Select(f => f.GetValue(null)).SequenceEqual(snapshot), "Search mutated a Workshop static cache or modifier state");
                ControlParity();
                PassiveParity(PlayerIntegrationParity);
                scenePlayer = null; afterReferenceStep = afterModelStep = null;
                WorldParity();
                CustomWindParity(assemblies["CustomWindSwitch"]);
                NativeRoute();
                var core = assemblies["UpSideDownCore"].GetType("UpsideDownCore.Models.Manager");
                core.GetField("isUpsideDown").SetValue(null, true);
                ExpectPatchRefusal("UpsideDownCore");
                core.GetField("isUpsideDown").SetValue(null, false);
                Console.WriteLine("[OK] Combined Workshop set: native-map capture, 141120 exact controller ticks, world parity, route search/replay, unchanged foreign caches");
            }
            finally
            {
                scenePlayer = null; afterReferenceStep = afterModelStep = null;
                foreach (var h in owners.Values) h.UnpatchAll(h.Id);
                expansion.GetProperty("Player").SetValue(null, oldPlayer, null);
                managerField.SetValue(null, oldManager);
            }
        }
    }
}
