using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MegaMappingExpansion
{
    internal enum BehaviorOp { Sequence, Selector, Parallel, Repeat, Invert, Wait, WaitEvent, Emit, CheckFlag, WaitFlag, SetFlag, Increment, Effect, Clear, Sound }
    internal sealed class BehaviorInstruction
    {
        internal BehaviorOp Op;
        internal int[] Children;
        internal string Key, Value, Path;
        internal double Seconds;
        internal int Count;
    }
    internal sealed class BehaviorProgram
    {
        internal BehaviorTreeData Definition;
        internal BehaviorInstruction[] Code;
    }
    internal static class BehaviorTreeCompiler
    {
        internal const int MaxNodes = 1024, MaxDepth = 32, MaxTrees = 128;
        internal static void Key(string value, string source)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.StartsWith("@"))
                throw new InvalidDataException(source + ": expected an ID/event of 1..128 characters, not starting with @");
        }
        internal static BehaviorProgram[] Compile(SceneFile scene)
        {
            if (scene.BehaviorTrees == null || scene.BehaviorTrees.Length > MaxTrees) throw new InvalidDataException("Maximum 128 behavior trees");
            var definitions = new Dictionary<string, BehaviorTreeData>(StringComparer.Ordinal);
            foreach (BehaviorTreeData tree in scene.BehaviorTrees)
            {
                if (tree == null) throw new InvalidDataException("Null behavior tree");
                Key(tree.Id, "Tree");
                if (definitions.ContainsKey(tree.Id)) throw new InvalidDataException("Duplicate behavior tree: " + tree.Id);
                definitions.Add(tree.Id, tree);
                if (tree.Children == null || tree.Children.Length != 1) throw new InvalidDataException(tree.Id + ": Tree requires exactly one root node");
                if (tree.Event != null) Key(tree.Event, tree.Id + ".event");
                if (tree.StopEvent != null) Key(tree.StopEvent, tree.Id + ".stopEvent");
                if (tree.Event != null && tree.Event == tree.StopEvent) throw new InvalidDataException(tree.Id + ": event and stopEvent must differ");
                if (tree.Retrigger != "ignore" && tree.Retrigger != "restart") throw new InvalidDataException(tree.Id + ": retrigger must be ignore or restart");
                if (tree.Screen < 0) throw new InvalidDataException(tree.Id + ": screen cannot be negative");
                if (tree.RequiresFlag != null) Flag(scene, tree.RequiresFlag, tree.EqualsValue, tree.Id);
                else if (tree.EqualsValue != null) throw new InvalidDataException(tree.Id + ": equals requires requiresFlag");
            }
            var result = new List<BehaviorProgram>();
            int total = 0;
            foreach (BehaviorTreeData tree in scene.BehaviorTrees.OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                var code = new List<BehaviorInstruction>();
                try { Visit(tree.Children[0], scene, definitions, code, new HashSet<string> { tree.Id }, tree.Id, 0); }
                catch (InvalidDataException error)
                {
                    string origin;
                    if (scene.SourceOrigins == null || !scene.SourceOrigins.TryGetValue(tree.Id, out origin)) throw;
                    throw new InvalidDataException(origin + ": " + error.Message, error);
                }
                total += code.Count;
                if (total > 8192) throw new InvalidDataException("Behavior trees exceed 8192 expanded nodes in this scene");
                result.Add(new BehaviorProgram { Definition = tree, Code = code.ToArray() });
            }
            return result.ToArray();
        }
        private static void Flag(SceneFile scene, string key, string value, string path)
        {
            FlagData flag = scene.Flags.FirstOrDefault(f => f.Id == key);
            if (flag == null) throw new InvalidDataException(path + ": unknown flag " + key);
            if (value == null || value.Length > 256) throw new InvalidDataException(path + ": value required (maximum 256 characters)");
            NarrativeValidation.FlagValue(flag, value);
        }
        private static int Visit(BehaviorNodeData data, SceneFile scene, Dictionary<string, BehaviorTreeData> definitions,
            List<BehaviorInstruction> code, HashSet<string> calls, string path, int depth)
        {
            if (data == null || depth > MaxDepth || code.Count >= MaxNodes) throw new InvalidDataException(path + ": maximum 32 levels / 1024 expanded nodes per tree");
            var call = data as BehaviorCall;
            if (call != null)
            {
                BehaviorTreeData target;
                if (call.Tree == null || !definitions.TryGetValue(call.Tree, out target)) throw new InvalidDataException(path + ": unknown subtree " + call.Tree);
                if (!calls.Add(call.Tree)) throw new InvalidDataException(path + ": recursive subtree " + call.Tree);
                int entry = Visit(target.Children[0], scene, definitions, code, calls, path + "/" + call.Tree, depth + 1);
                calls.Remove(call.Tree); return entry;
            }
            var node = new BehaviorInstruction { Path = path + "/" + data.GetType().Name, Children = new int[0] };
            int index = code.Count; code.Add(node);
            var branch = data as BehaviorBranchData;
            if (branch != null)
            {
                if (data is BehaviorSequence) node.Op = BehaviorOp.Sequence;
                else if (data is BehaviorSelector) node.Op = BehaviorOp.Selector;
                else if (data is BehaviorParallel) node.Op = BehaviorOp.Parallel;
                else if (data is BehaviorInvert) node.Op = BehaviorOp.Invert;
                else if (data is BehaviorRepeat) { node.Op = BehaviorOp.Repeat; node.Count = ((BehaviorRepeat)data).Count; }
                else throw new InvalidDataException(path + ": unknown composite");
                if (branch.Children == null || branch.Children.Length == 0 || branch.Children.Length > 64)
                    throw new InvalidDataException(path + ": composite requires 1..64 children");
                if ((node.Op == BehaviorOp.Invert || node.Op == BehaviorOp.Repeat) && branch.Children.Length != 1)
                    throw new InvalidDataException(path + ": decorator requires one child");
                if (node.Count < 0 || node.Count > 1000000) throw new InvalidDataException(path + ": repeat count must be 0..1000000");
                node.Children = branch.Children.Select((child, i) => Visit(child, scene, definitions, code, calls, node.Path + "[" + i + "]", depth + 1)).ToArray();
                return index;
            }
            var flag = data as BehaviorFlag;
            if (flag != null)
            {
                Flag(scene, flag.Flag, flag.Value, node.Path); node.Key = flag.Flag; node.Value = flag.Value;
                node.Op = data is BehaviorCheckFlag ? BehaviorOp.CheckFlag : data is BehaviorWaitFlag ? BehaviorOp.WaitFlag : BehaviorOp.SetFlag;
            }
            else if (data is BehaviorWait)
            {
                node.Op = BehaviorOp.Wait; node.Seconds = ((BehaviorWait)data).Seconds;
                if (double.IsNaN(node.Seconds) || double.IsInfinity(node.Seconds) || node.Seconds < 0 || node.Seconds > 86400)
                    throw new InvalidDataException(node.Path + ": seconds must be finite, 0..86400");
            }
            else if (data is BehaviorWaitEvent) { node.Op = BehaviorOp.WaitEvent; node.Key = ((BehaviorWaitEvent)data).Event; Key(node.Key, node.Path); }
            else if (data is BehaviorEmit) { node.Op = BehaviorOp.Emit; node.Key = ((BehaviorEmit)data).Event; Key(node.Key, node.Path); }
            else if (data is BehaviorClear) node.Op = BehaviorOp.Clear;
            else if (data is BehaviorEffect)
            {
                node.Op = BehaviorOp.Effect; node.Key = ((BehaviorEffect)data).Effect;
                if (!scene.Effects.Any(e => e.Id == node.Key)) throw new InvalidDataException(node.Path + ": unknown effect " + node.Key);
            }
            else if (data is BehaviorSound)
            {
                node.Op = BehaviorOp.Sound; node.Key = ((BehaviorSound)data).Sound; Key(node.Key, node.Path);
                if (node.Key.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0) throw new InvalidDataException(node.Path + ": sound must be an event_music key");
            }
            else if (data is BehaviorIncrement)
            {
                node.Op = BehaviorOp.Increment; var increment = (BehaviorIncrement)data; node.Key = increment.Flag; node.Count = increment.Amount;
                if (!scene.Flags.Any(f => f.Id == node.Key && f.Type == "integer")) throw new InvalidDataException(node.Path + ": IncrementFlag requires an integer flag");
            }
            else throw new InvalidDataException(node.Path + ": unknown leaf");
            return index;
        }
        internal static IEnumerable<string> Events(SceneFile scene)
        {
            foreach (RuleData rule in scene.Rules) yield return rule.Event;
            foreach (BehaviorProgram tree in Compile(scene))
            {
                if (tree.Definition.Event != null) yield return tree.Definition.Event;
                if (tree.Definition.StopEvent != null) yield return tree.Definition.StopEvent;
                foreach (BehaviorInstruction node in tree.Code) if (node.Op == BehaviorOp.WaitEvent) yield return node.Key;
            }
        }
    }
}
