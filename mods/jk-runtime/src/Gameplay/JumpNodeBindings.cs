using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    // The native graph shares one JumpState between ground execution, the
    // airborne leniency StaticNode and BTIsNodeRunning. Replace references,
    // not just the first child found. Record ownership for safe restoration.
    public sealed class JumpNodeBindings
    {
        private readonly List<Action> restore = new List<Action>();

        // Native FindNode<T> uses exact type equality. Compatibility callers
        // need the live subclass, not a stale original or a global generic detour.
        internal static JumpState FindActive(BTmanager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            var root = typeof(BTmanager).GetField("m_root_node", BindingFlags.Instance | BindingFlags.NonPublic);
            if (root == null) throw new MissingFieldException("BTmanager.m_root_node");
            var pending = new Stack<IBTnode>();
            var visited = new HashSet<IBTnode>();
            pending.Push((IBTnode)root.GetValue(manager));
            JumpState found = null;
            while (pending.Count != 0)
            {
                var node = pending.Pop();
                if (node == null || !visited.Add(node)) continue;
                var jump = node as JumpState;
                if (jump != null)
                {
                    if (found != null && !ReferenceEquals(found, jump))
                        throw new InvalidOperationException("Ambiguous active jump graph");
                    found = jump;
                }
                foreach (var related in node.GetRelatedNodes()) pending.Push(related);
            }
            if (found == null) throw new InvalidOperationException("Active jump graph has no JumpState");
            return found;
        }

        public static JumpNodeBindings Replace(BTmanager manager, PlayerEntity player,
            JumpState original, JumpState replacement)
        {
            FieldInfo root = typeof(BTmanager).GetField("m_root_node", BindingFlags.Instance | BindingFlags.NonPublic);
            if (root == null) throw new MissingFieldException("BTmanager.m_root_node");
            return Replace((IBTnode)root.GetValue(manager), player, original, replacement);
        }

        public static JumpNodeBindings Replace(IBTnode root, PlayerEntity player,
            JumpState original, JumpState replacement)
        {
            if (root == null || original == null || replacement == null)
                throw new ArgumentNullException("jump graph");
            Compatibility.ConveyorCompatibility.Ensure();
            JumpNodeBindings bindings = new JumpNodeBindings();
            try
            {
                HashSet<IBTnode> visited = new HashSet<IBTnode>();
                Queue<IBTnode> pending = new Queue<IBTnode>();
                pending.Enqueue(root);
                while (pending.Count > 0)
                {
                    IBTnode node = pending.Dequeue();
                    if (node == null || !visited.Add(node)) continue;
                    // Traverse the original graph before mutating its edges.
                    foreach (IBTnode related in node.GetRelatedNodes()) pending.Enqueue(related);
                    for (Type type = node.GetType(); type != null && typeof(IBTnode).IsAssignableFrom(type); type = type.BaseType)
                    {
                        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public
                            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if (typeof(IBTnode).IsAssignableFrom(field.FieldType))
                                bindings.ReplaceField(node, field, original, replacement);
                            else if (field.FieldType == typeof(IBTnode[]))
                            {
                                IBTnode[] children = (IBTnode[])field.GetValue(node);
                                if (children == null) continue;
                                for (int index = 0; index < children.Length; index++)
                                {
                                    if (!object.ReferenceEquals(children[index], original)) continue;
                                    int slot = index;
                                    children[slot] = replacement;
                                    bindings.restore.Add(delegate {
                                        if (!object.ReferenceEquals(children[slot], replacement)) throw new InvalidOperationException("Native jump child ownership changed externally");
                                        children[slot] = original;
                                    });
                                }
                            }
                        }
                    }
                }
                if (bindings.restore.Count == 0)
                    throw new InvalidOperationException("Native jump graph has no replaceable references");
                FieldInfo playerJump = typeof(PlayerEntity).GetField("m_jump_state", BindingFlags.Instance | BindingFlags.NonPublic);
                if (playerJump == null) throw new MissingFieldException("PlayerEntity.m_jump_state");
                if (player != null) bindings.ReplaceField(player, playerJump, original, replacement);

                // Preserve automatic custom block sound/particle registration,
                // including registrations made while the replacement is active.
                foreach (string name in new[] { "customBlockSounds", "customBlockParticleSpawningActions" })
                {
                    FieldInfo field = typeof(JumpState).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field == null) throw new MissingFieldException("JumpState." + name);
                    field.SetValue(replacement, field.GetValue(original));
                }
                return bindings;
            }
            catch
            {
                bindings.Restore();
                throw;
            }
        }

        private void ReplaceField(object owner, FieldInfo field, JumpState original, JumpState replacement)
        {
            if (!object.ReferenceEquals(field.GetValue(owner), original)) return;
            field.SetValue(owner, replacement);
            restore.Add(delegate {
                if (!object.ReferenceEquals(field.GetValue(owner), replacement)) throw new InvalidOperationException("Native jump reference ownership changed externally: " + field.Name);
                field.SetValue(owner, original);
            });
        }

        public void Restore()
        {
            var errors = new List<Exception>();
            for (int index = restore.Count - 1; index >= 0; index--)
                try { restore[index](); restore.RemoveAt(index); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Native jump references were not completely restored", errors);
        }
    }
}
