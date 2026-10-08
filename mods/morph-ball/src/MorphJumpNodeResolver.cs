using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;

namespace MorphBallMod
{
    internal static class MorphJumpNodeResolver
    {
        private static readonly FieldInfo Root = typeof(BTmanager).GetField(
            "m_root_node", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static JumpKing.Player.JumpState Find(BTmanager tree)
        {
            if (tree == null) return null;
            if (Root == null) throw new MissingFieldException("BTmanager.m_root_node");
            Queue<IBTnode> pending = new Queue<IBTnode>();
            HashSet<IBTnode> visited = new HashSet<IBTnode>();
            pending.Enqueue((IBTnode)Root.GetValue(tree));
            while (pending.Count != 0)
            {
                IBTnode node = pending.Dequeue();
                if (node == null || !visited.Add(node)) continue;
                JumpKing.Player.JumpState jump = node as JumpKing.Player.JumpState;
                if (jump != null) return jump;
                foreach (IBTnode child in node.GetRelatedNodes()) pending.Enqueue(child);
            }
            return null;
        }
    }
}
