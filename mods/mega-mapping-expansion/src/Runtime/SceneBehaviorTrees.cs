using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneBehaviorEngine
    {
        private enum TreeResult { Idle, Running, Success, Failure, Cancelled, Faulted }
        private sealed class TreeFault : Exception
        { internal TreeFault(string path, Exception error) : base(path + ": " + error.GetBaseException().Message) { } }
        private struct TreeNodeState
        {
            internal TreeResult Result;
            internal int Cursor, Iterations;
            internal double Started;
            internal bool Armed, Signalled;
            internal long Effect;
        }
        private sealed class TreeState
        {
            internal TreeResult Result;
            internal bool Fired;
            internal TreeNodeState[] Nodes;
            internal TreeState Clone()
            { return new TreeState { Result = Result, Fired = Fired, Nodes = Nodes == null ? null : (TreeNodeState[])Nodes.Clone() }; }
        }
        private BehaviorProgram[] treePrograms;
        private TreeState[] treeStates;
        private readonly Dictionary<string, List<int>> treeStarts = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> treeStops = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private struct TreeWaitAddress { internal int Tree, Node; }
        private readonly Dictionary<string, List<TreeWaitAddress>> treeWaits = new Dictionary<string, List<TreeWaitAddress>>(StringComparer.Ordinal);
        private int runningTrees;
        private static void Subscribe(Dictionary<string, List<int>> subscriptions, string name, int tree)
        {
            if (name == null) return;
            List<int> values;
            if (!subscriptions.TryGetValue(name, out values)) subscriptions.Add(name, values = new List<int>());
            values.Add(tree);
        }
        private void InitializeTrees()
        {
            treePrograms = BehaviorTreeCompiler.Compile(scene);
            treeStates = new TreeState[treePrograms.Length];
            for (int i = 0; i < treePrograms.Length; i++)
            {
                treeStates[i] = new TreeState();
                Subscribe(treeStarts, treePrograms[i].Definition.Event, i);
                Subscribe(treeStops, treePrograms[i].Definition.StopEvent, i);
                for (int n = 0; n < treePrograms[i].Code.Length; n++)
                {
                    BehaviorInstruction node = treePrograms[i].Code[n];
                    if (node.Op != BehaviorOp.WaitEvent) continue;
                    List<TreeWaitAddress> waits;
                    if (!treeWaits.TryGetValue(node.Key, out waits)) treeWaits.Add(node.Key, waits = new List<TreeWaitAddress>());
                    waits.Add(new TreeWaitAddress { Tree = i, Node = n });
                }
            }
        }
        private bool TreeScreen(int index, int eventScreen)
        { int screen = treePrograms[index].Definition.Screen; return screen == 0 || screen == eventScreen; }
        private void DispatchTreeEvent(SceneEvent observed)
        {
            if (treePrograms.Length == 0) return;
            int eventScreen = observed.Screen > 0 ? observed.Screen : actor.Screen;
            // Deliver only to waits already reached. Events are edges, never sticky global mailboxes.
            List<TreeWaitAddress> waiting;
            if (runningTrees > 0 && treeWaits.TryGetValue(observed.Name, out waiting))
                foreach (TreeWaitAddress address in waiting)
                {
                    TreeState state = treeStates[address.Tree];
                    if (state.Result == TreeResult.Running && TreeScreen(address.Tree, eventScreen) && state.Nodes[address.Node].Armed)
                        state.Nodes[address.Node].Signalled = true;
                }
            List<int> subscribers;
            if (treeStops.TryGetValue(observed.Name, out subscribers))
                foreach (int i in subscribers) if (TreeScreen(i, eventScreen) && treeStates[i].Result == TreeResult.Running) FinishTree(i, TreeResult.Cancelled);
            if (treeStarts.TryGetValue(observed.Name, out subscribers))
                foreach (int i in subscribers)
                {
                    BehaviorTreeData definition = treePrograms[i].Definition; TreeState state = treeStates[i];
                    if (!TreeScreen(i, eventScreen) || state.Result == TreeResult.Faulted || (definition.Once && state.Fired)
                        || !Condition(definition.RequiresFlag, definition.EqualsValue)) continue;
                    if (state.Result == TreeResult.Running)
                    {
                        if (definition.Retrigger == "ignore") continue;
                        FinishTree(i, TreeResult.Cancelled);
                    }
                    state.Nodes = new TreeNodeState[treePrograms[i].Code.Length];
                    state.Result = TreeResult.Running; state.Fired = true; runningTrees++;
                }
        }
        private void FinishTree(int tree, TreeResult result)
        {
            TreeState state = treeStates[tree];
            if (state.Result == TreeResult.Running) runningTrees--;
            state.Result = result;
            state.Nodes = null;
            // Release by ownership even when a changed callback threw before Activate returned a lease.
            string prefix = "bt:" + tree + ":";
            if (active.RemoveAll(effect => effect.Owner.StartsWith(prefix, StringComparison.Ordinal)) > 0)
            { RefreshLights(); Recompose(); }
        }
        private void ResetTreeBranch(int tree, int index)
        {
            TreeState state = treeStates[tree];
            if (state.Nodes == null) return;
            if (treePrograms[tree].Code[index].Op == BehaviorOp.Effect)
            {
                string owner = "bt:" + tree + ":" + index;
                if (active.Exists(effect => effect.Owner == owner)) ClearOwner(owner);
            }
            state.Nodes[index] = new TreeNodeState();
            foreach (int child in treePrograms[tree].Code[index].Children) ResetTreeBranch(tree, child);
        }
        private void TickTrees()
        {
            if (runningTrees == 0) return;
            // Compilation bounds the total graph size; each node is visited at most once per tick.
            int budget = 8192;
            for (int tree = 0; tree < treeStates.Length; tree++)
            {
                if (treeStates[tree].Result != TreeResult.Running) continue;
                try
                {
                    TreeResult result = TickTreeNode(tree, 0, ref budget);
                    if (result != TreeResult.Running) FinishTree(tree, result);
                }
                catch (Exception error)
                {
                    FinishTree(tree, TreeResult.Faulted);
                    RecordError("tree:" + treePrograms[tree].Definition.Id, error);
                }
            }
        }
        private TreeResult TickTreeNode(int tree, int index, ref int budget)
        {
            if (--budget < 0) throw new InvalidOperationException("Behavior tree tick exceeded 8192 node visits");
            BehaviorInstruction instruction = treePrograms[tree].Code[index];
            TreeNodeState state = treeStates[tree].Nodes[index];
            TreeResult result;
            try { result = EvaluateTreeNode(tree, index, instruction, ref state, ref budget); }
            catch (TreeFault) { throw; }
            catch (Exception error) { throw new TreeFault(instruction.Path, error); }
            state.Result = result; treeStates[tree].Nodes[index] = state; return result;
        }
        private TreeResult EvaluateTreeNode(int tree, int index, BehaviorInstruction node, ref TreeNodeState state, ref int budget)
        {
            bool first = state.Result == TreeResult.Idle;
            switch (node.Op)
            {
                case BehaviorOp.Sequence:
                    while (state.Cursor < node.Children.Length)
                    {
                        TreeResult child = TickTreeNode(tree, node.Children[state.Cursor], ref budget);
                        if (child != TreeResult.Success) return child;
                        state.Cursor++;
                    }
                    return TreeResult.Success;
                case BehaviorOp.Selector:
                    for (int i = 0; i < node.Children.Length; i++)
                    {
                        int child = node.Children[i];
                        if (treeStates[tree].Nodes[child].Result == TreeResult.Failure) ResetTreeBranch(tree, child);
                        TreeResult result = TickTreeNode(tree, child, ref budget);
                        if (result == TreeResult.Failure) { ResetTreeBranch(tree, child); continue; }
                        // A higher-priority branch preempts the old running branch and its leases.
                        for (int j = i + 1; j < node.Children.Length; j++) ResetTreeBranch(tree, node.Children[j]);
                        state.Cursor = i; return result;
                    }
                    return TreeResult.Failure;
                case BehaviorOp.Parallel:
                    bool waiting = false;
                    foreach (int child in node.Children)
                    {
                        TreeResult result = treeStates[tree].Nodes[child].Result;
                        if (result != TreeResult.Success) result = TickTreeNode(tree, child, ref budget);
                        if (result == TreeResult.Failure) return result;
                        waiting |= result == TreeResult.Running;
                    }
                    return waiting ? TreeResult.Running : TreeResult.Success;
                case BehaviorOp.Repeat:
                    TreeResult iteration = TickTreeNode(tree, node.Children[0], ref budget);
                    if (iteration != TreeResult.Success) return iteration;
                    state.Iterations++;
                    if (node.Count > 0 && state.Iterations >= node.Count) return TreeResult.Success;
                    ResetTreeBranch(tree, node.Children[0]);
                    return TreeResult.Running; // Always yield; an infinite instant body cannot lock the game.
                case BehaviorOp.Invert:
                    TreeResult inverted = TickTreeNode(tree, node.Children[0], ref budget);
                    return inverted == TreeResult.Running ? inverted : inverted == TreeResult.Success ? TreeResult.Failure : TreeResult.Success;
                case BehaviorOp.Wait:
                    if (first) state.Started = gameplayTime;
                    return gameplayTime - state.Started + 1e-9 >= node.Seconds ? TreeResult.Success : TreeResult.Running;
                case BehaviorOp.WaitEvent:
                    state.Armed = true;
                    return state.Signalled ? TreeResult.Success : TreeResult.Running;
                case BehaviorOp.CheckFlag: return GetFlag(node.Key) == node.Value ? TreeResult.Success : TreeResult.Failure;
                case BehaviorOp.WaitFlag: return GetFlag(node.Key) == node.Value ? TreeResult.Success : TreeResult.Running;
                case BehaviorOp.SetFlag: SetFlag(node.Key, node.Value); break;
                case BehaviorOp.Increment:
                    SetFlag(node.Key, checked(int.Parse(GetFlag(node.Key), CultureInfo.InvariantCulture) + node.Count).ToString(CultureInfo.InvariantCulture)); break;
                case BehaviorOp.Emit: Emit(node.Key); break;
                case BehaviorOp.Effect:
                    state.Effect = Activate("bt:" + tree + ":" + index, node.Key).Id; break;
                case BehaviorOp.Clear:
                    for (int n = 0; n < treeStates[tree].Nodes.Length; n++)
                    {
                        Cancel(treeStates[tree].Nodes[n].Effect);
                        treeStates[tree].Nodes[n].Effect = 0;
                    }
                    break;
                case BehaviorOp.Sound:
                    if (PlaySound == null) throw new InvalidOperationException("Native scene sound service was not bound");
                    PlaySound(node.Key); break;
                default: throw new InvalidOperationException("Unknown behavior operation");
            }
            return TreeResult.Success;
        }
        internal string[] InspectTrees()
        {
            Check();
            return treePrograms.Select((p, i) => p.Definition.Id + ": " + treeStates[i].Result +
                (treeStates[i].Nodes == null ? "" : "; waiting: " + string.Join(", ", p.Code.Where((n, index) => treeStates[i].Nodes[index].Result == TreeResult.Running && n.Children.Length == 0).Select(n => n.Path)))).ToArray();
        }
        internal IEnumerable<string> TreeSounds
        { get { return treePrograms.SelectMany(p => p.Code).Where(n => n.Op == BehaviorOp.Sound).Select(n => n.Key).Distinct(StringComparer.Ordinal); } }
        private void ResetTrees()
        { runningTrees = 0; for (int i = 0; i < treeStates.Length; i++) treeStates[i] = new TreeState(); }
        private void RestoreTrees(TreeState[] states)
        { treeStates = Array.ConvertAll(states, s => s.Clone()); runningTrees = treeStates.Count(s => s.Result == TreeResult.Running); }
    }
}
