using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BehaviorTree;
using MegaMappingExpansion.Endings;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static BehaviorSequence Sequence(params BehaviorNodeData[] children) { return new BehaviorSequence { Children = children }; }
        private static BehaviorTreeData Tree(string id, string trigger, BehaviorNodeData root)
        { return new BehaviorTreeData { Id = id, Event = trigger, Children = new[] { root } }; }
        private static SceneFile TreeScene(params BehaviorTreeData[] trees)
        {
            SceneFile scene = BehaviorScene(); scene.Regions = new RegionData[0]; scene.BehaviorTrees = trees;
            scene.Flags = new[] { new FlagData { Id = "powered" }, new FlagData { Id = "count", Type = "integer", Value = "0" } };
            return scene;
        }
        private static void RejectTree(SceneFile scene, string description)
        {
            bool failed = false;
            try { using (var engine = new SceneBehaviorEngine(scene, null)) { } } catch (InvalidDataException) { failed = true; }
            Require(failed, description);
        }
        private static void BehaviorTreeChecks(string scratch)
        {
            SceneFile scene = TreeScene(Tree("lamp", "start", Sequence(
                new BehaviorEffect { Effect = "lantern" }, new BehaviorWait { Seconds = 1 }, new BehaviorSetFlag { Flag = "powered", Value = "true" })));
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Tick(.5, false, true, At(0, 0));
                Require(scene.Lights.Length == 0 && engine.InspectTrees()[0].Contains("Idle"), "pause does not dispatch start or execute actions");
                engine.Tick(0, true, true, At(0, 0));
                Require(scene.Lights.Length == 1 && engine.InspectTrees()[0].Contains("BehaviorWait"), "start executes a sequence up to its wait");
                engine.Tick(.5, true, true, At(0, 0)); object snapshot = engine.Capture();
                engine.Tick(1, false, true, At(0, 0));
                Require(engine.GetFlag("powered") == "false", "paused trees keep their cursor and gameplay wait");
                engine.Tick(.5, true, true, At(0, 0));
                Require(engine.GetFlag("powered") == "true" && scene.Lights.Length == 0 && engine.InspectTrees()[0].Contains("Success"), "completion releases owned effects");
                engine.Restore(snapshot); engine.Tick(.49, true, true, At(0, 0));
                Require(scene.Lights.Length == 1 && engine.GetFlag("powered") == "false", "snapshot restores a running wait and its effect");
                engine.Tick(.01, true, true, At(0, 0));
                Require(scene.Lights.Length == 0 && engine.GetFlag("powered") == "true", "restored wait completes at the original deadline");
                engine.Restore(snapshot); engine.Restore(snapshot); engine.ResetRun(); engine.Tick(0, true, true, At(0, 0));
                Require(scene.Lights.Length == 1, "snapshot is reusable and restart starts exactly one fresh tree");
            }
            var waitTree = Tree("edge", "go", Sequence(new BehaviorWaitEvent { Event = "go" }, new BehaviorIncrement { Flag = "count", Amount = 1 }));
            waitTree.Screen = 2; waitTree.StopEvent = "stop";
            scene = TreeScene(waitTree);
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Emit("go", 1); engine.Tick(0, true, true, At(0, 0, 2));
                Require(engine.InspectTrees()[0].Contains("Idle"), "explicit event screen controls subscriptions");
                engine.Emit("go", 2); engine.Tick(0, true, true, At(0, 0, 1));
                Require(engine.GetFlag("count") == "0", "trigger cannot retroactively satisfy a not-yet-armed wait");
                object pending = engine.Capture(); engine.Emit("go", 2); engine.Tick(0, true, true, At(0, 0, 1));
                Require(engine.GetFlag("count") == "1", "an armed wait consumes the next matching edge despite camera screen");
                engine.Restore(pending); engine.Emit("stop", 2); engine.Tick(0, true, true, At(0, 0, 1));
                Require(engine.InspectTrees()[0].Contains("Cancelled"), "stop event cancels a running tree");
            }
            scene = TreeScene(Tree("repeat", "start", new BehaviorRepeat { Count = 3, Children = new BehaviorNodeData[] { new BehaviorIncrement { Flag = "count", Amount = 1 } } }));
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                for (int i = 1; i <= 3; i++) { engine.Tick(0, true, true, At(0, 0)); Require(engine.GetFlag("count") == i.ToString(), "Repeat yields after each instant iteration"); }
                engine.Tick(0, true, true, At(0, 0)); Require(engine.GetFlag("count") == "3", "completed roots do not automatically repeat");
            }
            scene = TreeScene(Tree("parallel", "start", new BehaviorParallel { Children = new BehaviorNodeData[] {
                new BehaviorIncrement { Flag = "count", Amount = 1 }, new BehaviorWaitFlag { Flag = "powered", Value = "true" } } }));
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                for (int i = 0; i < 10; i++) engine.Tick(.1, true, true, At(0, 0));
                Require(engine.GetFlag("count") == "1", "ParallelAll latches completed actions while other children wait");
                engine.SetFlag("powered", "true"); engine.Tick(0, true, true, At(0, 0));
                Require(engine.InspectTrees()[0].Contains("Success"), "ParallelAll requires every child to succeed");
            }
            scene = TreeScene(Tree("priority", "start", new BehaviorSelector { Children = new BehaviorNodeData[] {
                Sequence(new BehaviorCheckFlag { Flag = "powered", Value = "true" }, new BehaviorWait { Seconds = 10 }),
                Sequence(new BehaviorEffect { Effect = "lantern" }, new BehaviorWait { Seconds = 10 }) } }));
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                var external = engine.Activate("outside", "dim");
                engine.Tick(0, true, true, At(0, 0)); Require(scene.Lights.Length == 1, "selector enters lower branch when condition fails");
                engine.SetFlag("powered", "true"); engine.Tick(0, true, true, At(0, 0));
                Require(scene.Lights.Length == 0 && external.Active, "priority switch cancels only the losing branch's effects");
            }
            var reusable = Tree("body", null, Sequence(new BehaviorIncrement { Flag = "count", Amount = 1 }, new BehaviorWait { Seconds = .1 }));
            var entry = Tree("entry", "go", Sequence(new BehaviorCall { Tree = "body" }, new BehaviorCall { Tree = "body" })); entry.Once = true;
            scene = TreeScene(reusable, entry);
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Emit("go"); engine.Tick(0, true, true, At(0, 0)); engine.Emit("go"); engine.Tick(.1, true, true, At(0, 0)); engine.Tick(.1, true, true, At(0, 0));
                Require(engine.GetFlag("count") == "2", "subtree call sites have independent cursors and root once ignores retriggers");
                engine.Emit("go"); engine.Tick(0, true, true, At(0, 0)); Require(engine.GetFlag("count") == "2", "once remains consumed after completion");
            }
            var restarted = Tree("restart", "go", Sequence(new BehaviorEffect { Effect = "lantern" }, new BehaviorWait { Seconds = 1 })); restarted.Retrigger = "restart";
            scene = TreeScene(restarted);
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Emit("go"); engine.Tick(0, true, true, At(0, 0)); engine.Tick(.9, true, true, At(0, 0)); engine.Emit("go"); engine.Tick(0, true, true, At(0, 0)); engine.Tick(.2, true, true, At(0, 0));
                Require(scene.Lights.Length == 1 && engine.InspectEffects().Length == 1, "restart retrigger releases the old execution and resets the deadline");
            }
            scene = TreeScene(Tree("a-fault", "start", Sequence(new BehaviorEffect { Effect = "lantern" }, new BehaviorIncrement { Flag = "count", Amount = 1 })),
                Tree("b-ok", "start", new BehaviorSetFlag { Flag = "powered", Value = "true" })); scene.Flags[1].Value = int.MaxValue.ToString();
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Tick(0, true, true, At(0, 0));
                Require(scene.Lights.Length == 0 && engine.GetFlag("powered") == "true" && engine.InspectTrees()[0].Contains("Faulted") && engine.InspectErrors().Length == 1,
                    "overflow isolates the failing tree, unwinds its leases and preserves unrelated execution");
            }
            RejectTree(TreeScene(Tree("bad", "start", new BehaviorCall { Tree = "missing" })), "unknown subtrees fail preparation");
            scene = TreeScene(Tree("callback", "start", Sequence(new BehaviorEffect { Effect = "lantern" }, new BehaviorWait { Seconds = 1 })));
            using (var engine = new SceneBehaviorEngine(scene, (target, property) => { if (scene.Nodes[0].Asset == "gold") throw new InvalidOperationException("fixture callback"); }))
            {
                engine.Tick(0, true, true, At(0, 0));
                Require(scene.Lights.Length == 0 && scene.Nodes[0].Asset == "blue" && engine.InspectEffects().Length == 0,
                    "tree fault releases an effect whose change callback threw before the lease returned");
                Require(engine.InspectErrors()[0].Contains("BehaviorEffect"), "runtime faults retain the failing leaf path");
            }
            RejectTree(TreeScene(Tree("bad", "start", new BehaviorCall { Tree = "bad" })), "recursive subtrees fail preparation");
            RejectTree(TreeScene(Tree("bad", "start", new BehaviorSetFlag { Flag = "count", Value = "NaN" })), "typed flag operands fail preparation");
            RejectTree(TreeScene(Tree("bad", "start", new BehaviorWait { Seconds = double.NaN })), "non-finite waits fail preparation");
            RejectTree(TreeScene(Tree("bad", "start", new BehaviorEffect { Effect = "missing" })), "unknown effects fail preparation");
            RejectTree(TreeScene(Tree("bad", "start", new BehaviorInvert())), "empty decorators fail preparation");
            BehaviorNodeData deep = new BehaviorWait(); for (int i = 0; i < 34; i++) deep = Sequence(deep);
            RejectTree(TreeScene(Tree("bad", "start", deep)), "deep trees fail preparation");
            string longFlag = new string('f', 120);
            scene = TreeScene(Tree("long-event", "flag:" + longFlag, new BehaviorSetFlag { Flag = "powered", Value = "true" }));
            scene.Flags = scene.Flags.Concat(new[] { new FlagData { Id = longFlag } }).ToArray();
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.SetFlag(longFlag, "true"); engine.Tick(0, true, true, At(0, 0));
                Require(engine.GetFlag("powered") == "true", "tree subscriptions accept generated events from maximum-length native scene flags");
            }
            scene = TreeScene(Tree("sound", "start", Sequence(
                new BehaviorInvert { Children = new BehaviorNodeData[] { new BehaviorCheckFlag { Flag = "powered", Value = "true" } } },
                new BehaviorSound { Sound = "bell" }, new BehaviorWait { Seconds = 1 })));
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                int sounds = 0; engine.PlaySound = key => { Require(key == "bell", "sound action uses its native event key"); sounds++; };
                for (int i = 0; i < 5; i++) engine.Tick(.1, true, true, At(0, 0));
                Require(sounds == 1, "invert and sound are one-shot sequence steps, not per-frame actions");
            }
            scene = TreeScene(Tree("hidden", "hiddenwallenter:gate", Sequence(new BehaviorWaitEvent { Event = "landed" })));
            Require(BehaviorTreeCompiler.Events(scene).Contains("landed") && BehaviorTreeCompiler.Events(scene).Contains("hiddenwallenter:gate"), "native subscriptions include triggers and awaited events without Rules");
            try
            {
                NativeHiddenWalls.Configure(scene);
                var type = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.Props.RaymanWall.RaymanWallEntity", true);
                var method = type.GetMethod("TouchPlayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Require(HarmonyLib.Harmony.GetPatchInfo(method).Owners.Contains("mega-mapping-expansion.hidden-walls"), "tree-only Hidden Kingdom contact installs the verified native hook");
            }
            finally { NativeHiddenWalls.Release(); }
            var settingsField = typeof(MappingSettings).GetField("store", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            object previousSettings = settingsField.GetValue(null); settingsField.SetValue(null, null);
            try
            {
                using (var engine = new SceneBehaviorEngine(TreeScene(Tree("bridge", "ending:cue", new BehaviorSetFlag { Flag = "powered", Value = "true" })), null))
                {
                    var cue = new SceneBridgeNode("EmitSceneEvent", "ending:cue", null, () => engine);
                    var wait = new SceneBridgeNode("WaitSceneFlag", "powered", "true", () => engine);
                    Require(wait.Run(new TickData(0, 1)) == BTresult.Running, "native ending bridge waits on the scene's actual flag");
                    cue.Run(new TickData(0, 1)); engine.Tick(0, true, true, At(0, 0));
                    Require(wait.Run(new TickData(0, 2)) == BTresult.Success, "native ending cue and scene tree share events and flags");
                }
                XElement bridge = XElement.Parse("<Sequencer><EmitSceneEvent>ending:cue</EmitSceneEvent><WaitSceneFlag><Key>powered</Key><Value>true</Value></WaitSceneFlag></Sequencer>");
                EndingDocument.Validate(bridge, "main_ending"); EndingDocument.ValidateScene(new[] { bridge }, TreeScene());
                bool failed = false; try { EndingDocument.ValidateScene(new[] { bridge }, null); } catch (InvalidDataException) { failed = true; }
                Require(failed, "ending bridge requires an authored scene instead of silently skipping");
            }
            finally { settingsField.SetValue(null, previousSettings); }
            string path = Path.Combine(scratch, "tree-roundtrip-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(path, "<MegaMapping><Flags><Flag id='done'/></Flags><BehaviorTrees><Tree id='test' event='start'><Sequencer><PauseNode seconds='0.25'/><SetFlag flag='done' value='yes'/></Sequencer></Tree></BehaviorTrees></MegaMapping>");
                SceneFile loaded = SceneAuthoring.Read(path); SceneFile roundtrip = SceneBehaviorEngine.Copy(loaded);
                using (var engine = new SceneBehaviorEngine(roundtrip, null)) { engine.Tick(0, true, true, At(0, 0)); engine.Tick(.25, true, true, At(0, 0)); Require(engine.GetFlag("done") == "yes", "polymorphic trees survive the scene XML/cache serialization contract"); }
                File.WriteAllText(path, "<MegaMapping><BehaviorTrees><Tree id='bad'><PauseNode second='1'/></Tree></BehaviorTrees></MegaMapping>");
                bool failed = false; try { SceneAuthoring.Read(path); } catch (InvalidOperationException) { failed = true; }
                Require(failed, "misspelled node attributes are not ignored");
                File.WriteAllText(path, "<MegaMapping><BehaviorTrees><Tree id='bad'><PauseNode>2</PauseNode></Tree></BehaviorTrees></MegaMapping>");
                failed = false; try { SceneAuthoring.Read(path); } catch (InvalidOperationException) { failed = true; }
                Require(failed, "native ending scalar syntax cannot silently turn a scene wait into zero seconds");
                File.WriteAllText(path, "<MegaMapping><ObjectDefinitions><Object id='counter'><Flags><Flag id='count' type='integer' value='0'/></Flags>" +
                    "<BehaviorTrees><Tree id='body'><Sequencer><PauseNode seconds='1'/><IncrementFlag flag='@count' amount='1'/></Sequencer></Tree>" +
                    "<Tree id='run' event='enter:@zone' stopEvent='exit:@zone'><Subtree tree='@body'/></Tree></BehaviorTrees></Object></ObjectDefinitions>" +
                    "<Objects><Instance id='left' object='counter' x='20' screen='2'/><Instance id='right' object='counter' x='200' screen='3'/></Objects></MegaMapping>");
                loaded = SceneAuthoring.Read(path);
                Require(loaded.BehaviorTrees.All(t => t.Screen == 0) && loaded.BehaviorTrees.Single(t => t.Id == "left.run").StopEvent == "exit:left.zone",
                    "object trees retain non-spatial metadata and namespace stop events");
                using (var engine = new SceneBehaviorEngine(loaded, null))
                {
                    engine.Emit("enter:left.zone"); engine.Tick(0, true, true, At(400, 300)); engine.Tick(1, true, true, At(400, 300));
                    Require(engine.GetFlag("left.count") == "1" && engine.GetFlag("right.count") == "0", "object-local subtree and flag references stay isolated");
                }
            }
            finally { File.Delete(path); }
            // Report a bounded CPU fixture, not a promise about rendering FPS on other machines.
            var dormant = Enumerable.Range(0, 128).Select(i => Tree("idle-" + i, "never", new BehaviorWait { Seconds = 86400 })).ToArray();
            var waiting = Enumerable.Range(0, 128).Select(i => Tree("wait-" + i, "start", new BehaviorWait { Seconds = 86400 })).ToArray();
            foreach (var fixture in new[] { dormant, waiting })
                using (var engine = new SceneBehaviorEngine(TreeScene(fixture), null))
                {
                    engine.Tick(0, true, true, At(0, 0));
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 10000; i++) engine.Tick(.001, true, true, At(0, 0));
                    timer.Stop();
                    Console.WriteLine("[PERF] 128 " + (fixture == dormant ? "dormant" : "waiting") + " scene trees: " + (timer.Elapsed.TotalMilliseconds / 10000).ToString("F5", System.Globalization.CultureInfo.InvariantCulture) + " ms/tick (CPU fixture)");
                }
        }
    }
}
