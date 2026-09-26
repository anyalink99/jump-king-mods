using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using EntityComponent.BT;
using JKRuntime.Gameplay;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HammerKing
{
    // Keep the original landing/failure nodes and their registered callbacks alive.
    // Only the two sources of keyboard-driven movement are replaced.
    internal sealed class HammerNativeControl : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo JumpField = typeof(PlayerEntity).GetField("m_jump_state", Flags);
        private static readonly FieldInfo FailField = typeof(PlayerEntity).GetField("m_fail_state", Flags);
        private static readonly FieldInfo WaitDone = typeof(FailState).GetField("m_wait_done", Flags);
        private static readonly FieldInfo Root = typeof(BTmanager).GetField("m_root_node", Flags);
        private readonly List<Action> restore = new List<Action>();
        private readonly FailState failure;
        private sealed class NoJump : JumpState
        {
            internal NoJump(PlayerEntity player) : base(player) { }
            public override BTresult Run(TickData data) { return m_last_result = BTresult.Failure; }
        }
        private sealed class NoWalk : IBTnode
        { protected override BTresult MyRun(TickData data) { return BTresult.Failure; } }

        internal static void ValidateContract()
        {
            if (JumpField == null || FailField == null || WaitDone == null || WaitDone.FieldType != typeof(bool) || Root == null)
                throw new NotSupportedException("Jump King native movement/landing contract changed");
        }

        internal HammerNativeControl(PlayerEntity player, BehaviorTreeComp tree)
        {
            ValidateContract();
            failure = (FailState)FailField.GetValue(player);
            var jump = (JumpState)JumpField.GetValue(player);
            if (jump == null || failure == null) throw new InvalidOperationException("Native player control nodes are unavailable");
            try
            {
                var movement = PlayerControl.Acquire(player.m_body, "more-items.hammer");
                restore.Add(movement.Dispose);
                // Declares native charge unavailable independently of mod IDs
                // or graph shape. Release only after restoring every graph edge.
                var charge = JumpSlot.SuspendChargePolicy("more-items");
                restore.Add(charge.Dispose);
                var bindings = JumpNodeBindings.Replace(tree.GetRaw(), player, jump, new NoJump(player));
                restore.Add(bindings.Restore);
                var pending = new Queue<IBTnode>();
                var visited = new HashSet<IBTnode>();
                pending.Enqueue((IBTnode)Root.GetValue(tree.GetRaw()));
                int walks = 0;
                while (pending.Count > 0)
                {
                    var node = pending.Dequeue();
                    if (node == null || !visited.Add(node)) continue;
                    foreach (var child in node.GetRelatedNodes()) pending.Enqueue(child);
                    var composite = node as IBTcomposite;
                    if (composite == null) continue;
                    var children = composite.Children;
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (!(children[i] is Walk)) continue;
                        int slot = i;
                        var original = children[i];
                        var replacement = new NoWalk();
                        children[i] = replacement;
                        restore.Add(delegate {
                            if (!ReferenceEquals(children[slot], replacement)) throw new InvalidOperationException("Native walk ownership changed externally");
                            children[slot] = original;
                        });
                        walks++;
                    }
                }
                if (walks == 0) throw new InvalidOperationException("Native walking node is unavailable");
            }
            catch { Dispose(); throw; }
        }

        internal bool AllowsHammer(Vector2 mouse)
        {
            if (!failure.IsRunning()) return true;
            // Mouse movement can get up only after the original splat timer expires.
            if (!(bool)WaitDone.GetValue(failure) || mouse.LengthSquared() < 0.25f) return false;
            failure.ResetResult();
            return true;
        }

        public void Dispose()
        {
            var errors = new List<Exception>();
            for (int i = restore.Count - 1; i >= 0; i--)
            {
                // Keep charge reserved if any native edge could not be restored.
                // A cleanup failure must not activate another owner on that graph.
                if (i <= 1 && errors.Count != 0) break;
                try { restore[i](); restore.RemoveAt(i); } catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }
}
