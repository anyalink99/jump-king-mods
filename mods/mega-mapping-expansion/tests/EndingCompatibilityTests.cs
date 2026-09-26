using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent.BT;
using HarmonyLib;
using JumpKing;
using MegaMappingExpansion.Endings;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void EndingCompatibilityChecks(string scratch, string providerPath)
        {
            var foreign = Assembly.LoadFrom(providerPath);
            var entry = foreign.GetType("MoreEndingOptions.ModEntry", true);
            NativeEndings.AssertContracts();
            string root = Path.Combine(scratch, "ending-coexistence-" + Guid.NewGuid().ToString("N"));
            string map = Path.Combine(root, "map"), vanilla = Path.Combine(root, "vanilla");
            string owned = Path.Combine(map, "ending");
            Directory.CreateDirectory(owned); Directory.CreateDirectory(vanilla);
            var gameField = typeof(Game1).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = gameField.GetValue(null);
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1)); GC.SuppressFinalize(game);
            game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager)); gameField.SetValue(null, game);
            try
            {
                foreach (var binding in NativeEndings.Bindings)
                    File.WriteAllText(Path.Combine(owned, "custom_" + binding.Role + ".xml"), "<PauseNode>0.01</PauseNode>");
                for (int visit = 0; visit < 3; visit++)
                {
                    game.contentManager.root = map;
                    entry.GetMethod("OnLevelStart").Invoke(null, null);
                    NativeEndings.Prepare(map);
                    // First visit: MME prepares before native PatchAll. Later
                    // visits: MoreEndingOptions already owns every target.
                    if (visit == 0) entry.GetMethod("BeforeLevelLoad").Invoke(null, null);
                    NativeEndings.Prepare(map); // Same-world attempt retains its adapters.
                    foreach (var binding in NativeEndings.Bindings)
                    {
                        var info = Harmony.GetPatchInfo(binding.Method);
                        Require(info.Owners.Contains("Zebra.MoreEndingOptions.Harmony")
                            && info.Owners.Contains("mega-mapping-expansion.endings"), "Both providers keep their native adapters: " + binding.Role);
                        object actor = FormatterServices.GetUninitializedObject(binding.Method.DeclaringType);
                        string document = Path.Combine(owned, "custom_" + binding.Role + ".xml");
                        File.WriteAllText(document, "<Unknown/>"); // Only a second parser would read this now.
                        object result = binding.Method.Invoke(actor, null);
                        File.WriteAllText(document, "<PauseNode>0.01</PauseNode>");
                        BTmanager tree = binding.Actor ? ((BehaviorTreeComp)result).GetRaw() : (BTmanager)result;
                        tree.Run(.1f); tree.Run(.1f);
                        Require(tree.LastResult == BTresult.Success, "Authored ending runs with the real foreign postfix guarded: " + binding.Role);
                    }
                    NativeEndings.Release();
                    game.contentManager.root = vanilla;
                    NativeEndings.Prepare(vanilla);
                    entry.GetMethod("OnLevelStart").Invoke(null, null);
                    foreach (var binding in NativeEndings.Bindings)
                    {
                        var info = Harmony.GetPatchInfo(binding.Method);
                        Require(!info.Owners.Contains("mega-mapping-expansion.endings")
                            && info.Owners.Contains("Zebra.MoreEndingOptions.Harmony"), "Vanilla retains only the foreign ending adapter: " + binding.Role);
                    }
                    // A legacy map still belongs to MoreEndingOptions after MME unloads.
                    Directory.CreateDirectory(Path.Combine(vanilla, "ending"));
                    string foreignDocument = Path.Combine(vanilla, "ending", "custom_main_ending.xml");
                    File.WriteAllText(foreignDocument, "<PauseNode>0.01</PauseNode>");
                    object[] resultArgs = { null };
                    foreign.GetType("MoreEndingOptions.Patches.PatchNormalEnding", true).GetMethod("Postfix").Invoke(null, resultArgs);
                    var foreignTree = (BTmanager)resultArgs[0]; foreignTree.Run(.1f); foreignTree.Run(.1f);
                    Require(foreignTree.LastResult == BTresult.Success, "MoreEndingOptions still reads its own map files after a world switch");
                    File.Delete(foreignDocument);
                }
                // A partial MME claim must not suppress a different actor role.
                foreach (var binding in NativeEndings.Bindings.Where(b => b.Role != "main_ending"))
                    File.Delete(Path.Combine(owned, "custom_" + binding.Role + ".xml"));
                game.contentManager.root = map;
                NativeEndings.Prepare(map);
                File.WriteAllText(Path.Combine(owned, "custom_main_king.xml"), "<PauseNode>0.01</PauseNode>");
                var king = NativeEndings.Bindings.Single(b => b.Role == "main_king");
                object[] actorArgs = { FormatterServices.GetUninitializedObject(king.Method.DeclaringType), null };
                foreign.GetType("MoreEndingOptions.Patches.PatchEndingKing", true).GetMethod("Postfix").Invoke(null, actorArgs);
                var actorTree = ((BehaviorTreeComp)actorArgs[1]).GetRaw(); actorTree.Run(.1f); actorTree.Run(.1f);
                Require(actorTree.LastResult == BTresult.Success, "Unclaimed actor role stays with MoreEndingOptions while MME owns the controller");
                // Root selection precedes world teardown in native loading.
                game.contentManager.root = vanilla;
                File.WriteAllText(Path.Combine(vanilla, "ending", "custom_main_ending.xml"), "<PauseNode>0.01</PauseNode>");
                object[] nextMapArgs = { null };
                foreign.GetType("MoreEndingOptions.Patches.PatchNormalEnding", true).GetMethod("Postfix").Invoke(null, nextMapArgs);
                Require(nextMapArgs[0] is BTmanager, "Previous map ownership cannot suppress another map before teardown");
                // Invalid replacement preparation also releases the foreign guards.
                File.WriteAllText(Path.Combine(owned, "custom_main_ending.xml"), "<Unknown/>");
                bool rejected = false;
                try { NativeEndings.Prepare(map); } catch (InvalidDataException) { rejected = true; }
                var callback = foreign.GetType("MoreEndingOptions.Patches.PatchNormalEnding", true).GetMethod("Postfix");
                var remaining = Harmony.GetPatchInfo(callback);
                Require(rejected && (remaining == null || !remaining.Owners.Contains("mega-mapping-expansion.endings")),
                    "Failed preparation releases the foreign callback guard");
            }
            finally { NativeEndings.Release(); gameField.SetValue(null, previous); }
            Console.WriteLine("[OK] Map / vanilla / map re-entry with installed MoreEndingOptions; fixture retained: " + root);
        }
    }
}
