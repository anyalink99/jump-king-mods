using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using JKRuntime.World;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    public sealed class ControlTreeData
    {
        public ControlTreeData() { Retrigger = "ignore"; Nodes = new ControlNodeData[0]; Clock=SceneClock.Gameplay; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("event")] public string Event { get; set; }
        [XmlAttribute("stopEvent")] public string StopEvent { get; set; }
        [XmlAttribute("condition")] public string Condition { get; set; }
        [XmlAttribute("retrigger")] public string Retrigger { get; set; }
        [XmlAttribute("once")] public bool Once { get; set; }
        [XmlAttribute("clock")] public SceneClock Clock { get; set; }
        [XmlElement("Node")] public ControlNodeData[] Nodes { get; set; }
    }
    public sealed class ControlNodeData
    {
        public ControlNodeData() { Nodes = new ControlNodeData[0]; }
        [XmlAttribute("op")] public string Operation { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
        [XmlAttribute("target")] public string Target { get; set; }
        [XmlAttribute("seconds")] public double Seconds { get; set; }
        [XmlAttribute("count")] public int Count { get; set; }
        [XmlElement("Node")] public ControlNodeData[] Nodes { get; set; }
        [XmlElement("Action")] public ActionData Action { get; set; }
    }
    internal sealed class ControlInstruction
    {
        internal ControlNodeData Data;
        internal int[] Children;
    }
    internal sealed class ControlProgram
    {
        internal ControlTreeData Definition;
        internal ControlInstruction[] Code;
        internal WorldTreeProgram Execution;
        internal WorldTreeState State = new WorldTreeState();
        internal SceneEventSample Cause;
    }
    internal sealed partial class SceneProgram
    {
        private ControlProgram[] trees = new ControlProgram[0];
        private void PrepareTrees()
        {
            if (Definition.Trees == null || Definition.Trees.Length > 128) throw new InvalidDataException("Scene supports at most 128 control trees");
            var definitions = new Dictionary<string, ControlTreeData>(StringComparer.Ordinal);
            foreach (var tree in Definition.Trees)
            {
                if (tree == null || string.IsNullOrWhiteSpace(tree.Id) || definitions.ContainsKey(tree.Id) || tree.Nodes == null || tree.Nodes.Length != 1
                    || !Enum.IsDefined(typeof(SceneClock),tree.Clock) || (tree.Retrigger != "ignore" && tree.Retrigger != "restart") || (tree.Event != null && tree.Event == tree.StopEvent)) throw new InvalidDataException("Control tree requires a unique ID, one root and valid retrigger policy");
                if (!string.IsNullOrEmpty(tree.Condition) && Graph.Describe(tree.Condition).Kind != SceneValueKind.Boolean) throw new InvalidDataException(tree.Id + ": condition must be boolean");
                definitions.Add(tree.Id, tree);
            }
            int total = 0; var compiled = new List<ControlProgram>();
            foreach (var definition in definitions.Values.OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                var code = new List<ControlInstruction>(); CompileControl(definition.Nodes[0], definitions, code, new HashSet<string> { definition.Id }, 0);
                total += code.Count; if (total > 8192) throw new InvalidDataException("Scene control trees exceed 8192 expanded nodes");
                var program = new ControlProgram { Definition=definition, Code=code.ToArray() };
                program.Execution = new WorldTreeProgram(program.Code.Select(n => {
                    WorldTreeOp op = n.Data.Operation == "sequence" ? WorldTreeOp.Sequence : n.Data.Operation == "selector" ? WorldTreeOp.Selector : n.Data.Operation == "parallel" ? WorldTreeOp.Parallel
                        : n.Data.Operation == "repeat" ? WorldTreeOp.Repeat : n.Data.Operation == "invert" ? WorldTreeOp.Invert : WorldTreeOp.Leaf;
                    return new WorldTreeInstruction(op, n.Children, n.Data.Count);
                }).ToArray());
                compiled.Add(program);
                if (!string.IsNullOrEmpty(definition.Event)) ruleLeases.Add(Signals.SubscribeEvent(definition.Event, sample => StartTree(program, sample)));
                if (!string.IsNullOrEmpty(definition.StopEvent)) ruleLeases.Add(Signals.SubscribeEvent(definition.StopEvent, sample => FinishTree(program, WorldTreeResult.Cancelled)));
                foreach (var group in program.Code.Select((n, i) => new { Node=n, Index=i }).Where(n => n.Node.Data.Operation == "waitEvent").GroupBy(n => n.Node.Data.Target))
                {
                    var indices = group.Select(n => n.Index).ToArray();
                    ruleLeases.Add(Signals.SubscribeEvent(group.Key, sample => {
                        if (program.State.Result == WorldTreeResult.Running) foreach (int index in indices) if (program.State.Nodes[index].Armed) program.State.Nodes[index].Signalled = true;
                    }));
                }
            }
            trees = compiled.ToArray();
        }
        private int CompileControl(ControlNodeData source, Dictionary<string, ControlTreeData> definitions, List<ControlInstruction> code, HashSet<string> calls, int depth)
        {
            if (source == null || depth > 32 || code.Count >= 1024) throw new InvalidDataException("Control tree exceeds depth 32 or 1024 nodes");
            if (source.Operation == "call")
            {
                ControlTreeData target; if (source.Target == null || !definitions.TryGetValue(source.Target, out target) || !calls.Add(source.Target)) throw new InvalidDataException("Unknown or recursive control subtree");
                int root = CompileControl(target.Nodes[0], definitions, code, calls, depth + 1); calls.Remove(source.Target); return root;
            }
            var instruction = new ControlInstruction { Data=source, Children=new int[0] }; int index=code.Count; code.Add(instruction);
            if (new[] { "sequence", "selector", "parallel", "repeat", "invert" }.Contains(source.Operation))
            {
                if (source.Nodes == null || source.Nodes.Length < 1 || source.Nodes.Length > 64 || ((source.Operation == "repeat" || source.Operation == "invert") && source.Nodes.Length != 1)
                    || source.Count < 0 || source.Count > 1000000 || source.Action != null) throw new InvalidDataException("Invalid control composite");
                instruction.Children = source.Nodes.Select(n => CompileControl(n, definitions, code, calls, depth + 1)).ToArray();
            }
            else
            {
                if (source.Nodes != null && source.Nodes.Length != 0) throw new InvalidDataException("Control leaf cannot contain nodes");
                switch (source.Operation)
                {
                    case "wait": if (!SceneValue.Finite(source.Seconds) || source.Seconds < 0 || source.Seconds > 86400) throw new InvalidDataException("Wait needs finite seconds in 0..86400"); break;
                    case "waitEvent": if (string.IsNullOrWhiteSpace(source.Target) || source.Target.Length > 128) throw new InvalidDataException("WaitEvent needs an event ID"); break;
                    case "check": case "waitCondition": if (Graph.Describe(source.Source).Kind != SceneValueKind.Boolean) throw new InvalidDataException("Control condition must be boolean"); break;
                    case "action": ValidateAction(source.Action); break;
                    case "clear": break;
                    default: throw new InvalidDataException("Unknown control operation: " + source.Operation);
                }
            }
            return index;
        }
        private void StartTree(ControlProgram tree, SceneEventSample cause)
        {
            RefreshGraph();
            if (tree.State.Result == WorldTreeResult.Faulted || (tree.Definition.Once && tree.State.Fired) || (!string.IsNullOrEmpty(tree.Definition.Condition) && (!Graph.Available(tree.Definition.Condition)||Graph.Read(tree.Definition.Condition).X == 0))) return;
            if (tree.State.Result == WorldTreeResult.Running)
            { if (tree.Definition.Retrigger == "ignore") return; FinishTree(tree, WorldTreeResult.Cancelled); }
            tree.Cause=cause; tree.State.Nodes=new WorldTreeNodeState[tree.Code.Length]; tree.State.Result=WorldTreeResult.Running; tree.State.Fired=true;
        }
        private void FinishTree(ControlProgram tree, WorldTreeResult result)
        {
            for (int i=0;i<tree.Code.Length;i++) CancelOwner(TreeOwner(tree,i));
            tree.State.Result=result; tree.State.Nodes=null;
        }
        private static string TreeOwner(ControlProgram tree, int node) { return "tree:" + tree.Definition.Id + ":" + node; }
        private void ResetBranch(ControlProgram tree, int node)
        {
            CancelOwner(TreeOwner(tree,node)); tree.State.Nodes[node]=new WorldTreeNodeState(); foreach (int child in tree.Code[node].Children) ResetBranch(tree,child);
        }
        private void TickControlTrees(double gameplayDelta,double presentationDelta)
        {
            int budget=8192;
            foreach (var tree in trees)
            {
                if (tree.State.Result != WorldTreeResult.Running || (tree.Definition.Clock==SceneClock.Gameplay?gameplayDelta:presentationDelta)<=0) continue;
                try
                {
                    WorldTreeLeaf leaf=delegate(int index, ref WorldTreeNodeState state, ref int remaining) { return EvaluateControl(tree,index,ref state); };
                    var result=tree.Execution.Step(tree.State,0,ref budget,leaf,index=>ResetBranch(tree,index));
                    if (result != WorldTreeResult.Running) FinishTree(tree,result);
                }
                catch (Exception error)
                {
                    FinishTree(tree,WorldTreeResult.Faulted); if (logicErrors.Count==64) logicErrors.RemoveAt(0); logicErrors.Add(tree.Definition.Id + ": " + error.GetBaseException().Message);
                }
            }
        }
        private WorldTreeResult EvaluateControl(ControlProgram tree, int index, ref WorldTreeNodeState state)
        {
            RefreshGraph();
            var node=tree.Code[index].Data;
            switch (node.Operation)
            {
                case "wait": double time=Clock(tree.Definition.Clock);if (state.Result==WorldTreeResult.Idle) state.Started=time; return time-state.Started+1e-9>=node.Seconds ? WorldTreeResult.Success : WorldTreeResult.Running;
                case "waitEvent": state.Armed=true; return state.Signalled ? WorldTreeResult.Success : WorldTreeResult.Running;
                case "check": return Graph.Available(node.Source)&&Graph.Read(node.Source).X!=0 ? WorldTreeResult.Success : WorldTreeResult.Failure;
                case "waitCondition": return Graph.Available(node.Source)&&Graph.Read(node.Source).X!=0 ? WorldTreeResult.Success : WorldTreeResult.Running;
                case "action": Execute(node.Action,TreeOwner(tree,index),tree.Cause); break;
                case "clear": for(int i=0;i<tree.Code.Length;i++)CancelOwner(TreeOwner(tree,i)); break;
                default: throw new InvalidOperationException("Unknown control leaf");
            }
            return WorldTreeResult.Success;
        }
        internal string[] InspectTrees() { return trees.Select(t=>t.Definition.Id + ": " + t.State.Result).ToArray(); }
    }
}
