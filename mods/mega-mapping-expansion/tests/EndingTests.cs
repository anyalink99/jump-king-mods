using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BehaviorTree;
using MegaMappingExpansion.Endings;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void EndingChecks(string scratch)
        {
            NativeEndings.AssertContracts();
            Require(NativeEndings.Bindings.All(b => b.Method != null), "Every supported native ending controller and actor has an exact MakeBT contract");
            Action<string, string> reject = (xml, role) => {
                bool failed = false;
                try { EndingDocument.Validate(XElement.Parse(xml), role); }
                catch (InvalidDataException) { failed = true; }
                Require(failed, "Malformed ending rejected: " + xml);
            };
            reject("<Unknown/>", "main_king");
            reject("<MoveNode/>", "main_ending");
            reject("<PauseNode>NaN</PauseNode>", "main_king");
            reject("<PauseNode>-1</PauseNode>", "main_king");
            reject("<PauseNode>1,5</PauseNode>", "main_king");
            reject("<IsBirdDone/>", "owl_king");
            reject("<CherubsDeliver/>", "owl_bird");
            reject("<PlaySFX>Player.DoesNotExist</PlaySFX>", "main_king");
            reject("<SetSpriteNode>Regular.DoesNotExist</SetSpriteNode>", "main_king");
            reject("<SequenceOnce>ignored<PauseNode>1</PauseNode></SequenceOnce>", "main_king");
            reject("<MoveNode><Repetitions>1</Repetitions><DeltaMove unit='pixels'><X>1</X><Y>0</Y></DeltaMove></MoveNode>", "main_king");
            reject("<StaticNode><Result>Success</Result><PauseNode>0</PauseNode><PauseNode>1</PauseNode></StaticNode>", "main_king");
            var culture = CultureInfo.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                var document = XElement.Parse("<Evaluator><Condition><SequenceOnce><PauseNode> 0.01 </PauseNode></SequenceOnce></Condition><Func><StaticNode><Result>Success</Result><SequenceOnce><PauseNode>0.01</PauseNode></SequenceOnce></StaticNode></Func></Evaluator>");
                EndingDocument.Validate(document, "main_ending");
                IBTnode tree = EndingXmlParser.GetBtTree(null, document, EndingXmlParser.Ending.MainBabe);
                var manager = new BTmanager(tree);
                for (int i = 0; i < 5; i++) manager.Run(.1f);
                Require(manager.FindNode<JumpKing.Util.PauseNode>() != null, "Nested composites inside decorators retain their native child nodes");
                Require(manager.LastResult == BTresult.Success, "Nested custom tree executes on the native behavior-tree engine with invariant numbers");
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = culture; }
            string root = Path.Combine(scratch, "ending-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "ending"));
            string file = Path.Combine(root, "ending", "custom_main_king.xml");
            // These files are isolated test fixtures, not authored map data.
            File.WriteAllText(file, "<PauseNode>0.1</PauseNode>");
            try
            {
                NativeEndings.Prepare(root);
                var binding = NativeEndings.Bindings.First(b => b.Role == "main_king");
                Require(HarmonyLib.Harmony.GetPatchInfo(binding.Method).Owners.Contains("mega-mapping-expansion.endings"), "An authored ending installs the native tree adapter");
                var gameField = typeof(JumpKing.Game1).GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                object previousGame = gameField.GetValue(null);
                try
                {
                    var game = (JumpKing.Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Game1));
                    game.contentManager = (JumpKing.JKContentManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.JKContentManager));
                    game.contentManager.root = root; gameField.SetValue(null, game);
                    foreach (var target in NativeEndings.Bindings)
                        File.WriteAllText(Path.Combine(root, "ending", "custom_" + target.Role + ".xml"), "<SequenceOnce><PauseNode>0.01</PauseNode></SequenceOnce>");
                    NativeEndings.Prepare(root);
                    foreach (var target in NativeEndings.Bindings)
                    {
                        object actor = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(target.Method.DeclaringType);
                        object result = target.Method.Invoke(actor, null);
                        BTmanager manager = target.Actor ? ((EntityComponent.BT.BehaviorTreeComp)result).GetRaw() : (BTmanager)result;
                        manager.Run(.1f); manager.Run(.1f);
                        Require(manager.LastResult == BTresult.Success, "The installed native MakeBT entry point executes the custom tree: " + target.Role);
                    }
                }
                finally { gameField.SetValue(null, previousGame); }
                File.WriteAllText(file, "<Unknown/>");
                bool failed = false;
                try { NativeEndings.Prepare(root); } catch (InvalidDataException error) { failed = error.Message.Contains(file); }
                Require(failed, "Invalid custom ending fails preparation with its path instead of selecting a vanilla tree");
                File.WriteAllText(file, "<!DOCTYPE tree [<!ENTITY value SYSTEM 'file:///missing'>]><PauseNode>&value;</PauseNode>");
                failed = false;
                try { NativeEndings.Read(root); } catch (InvalidDataException) { failed = true; }
                Require(failed, "Ending XML prohibits external entities");
            }
            finally
            {
                NativeEndings.Release();
                foreach (var target in NativeEndings.Bindings) File.Delete(Path.Combine(root, "ending", "custom_" + target.Role + ".xml"));
                Directory.Delete(Path.GetDirectoryName(file)); Directory.Delete(root);
            }
            Require(NativeEndings.Read(root).Count == 0, "No custom ending files means no ending overrides");
            foreach (var binding in NativeEndings.Bindings)
            {
                var patch = HarmonyLib.Harmony.GetPatchInfo(binding.Method);
                Require(patch == null || !patch.Owners.Contains("mega-mapping-expansion.endings"), "Ending adapters release with their resource owner: " + binding.Role);
            }
        }
    }
}
