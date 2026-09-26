using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using EntityComponent.BT;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Controller;
using JumpKing.Player;

namespace SubframeCharge
{
    internal static class SubframeChargeInstaller
    {
        private static BodyComp installedBody;
        private static IBodyCompBehaviour lifecycleMarker;
        private static JKRuntime.Gameplay.BodyPipeline lifecycleRegistry;
        private static BehaviorTreeComp installedTree;
        private static SubframeChargeState replacementJumpState;
        private static JumpTrajectoryProbe trajectoryProbe;
        private static BodyComp trajectoryBody;
        private static JKRuntime.Gameplay.JumpNodeBindings jumpBindings;
        private static ChargeFrameComponents frameObservers;
        private static readonly FieldInfo PlayerJump = typeof(PlayerEntity).GetField("m_jump_state", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool OwnsCompletedTap(PadInstance pad)
        {
            var player=JumpKing.GameManager.GameLoop.m_player;
            return SettingsStore.Current.Enabled && replacementJumpState!=null && jumpBindings!=null
                && JumpKing.JumpGame.instance!=null && JumpKing.JumpGame.instance.IsPlaying()
                && player!=null && player.IsAlive && ReferenceEquals(installedBody,player.m_body)
                && !JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended
                && ReferenceEquals(PlayerJump.GetValue(player),replacementJumpState)
                && replacementJumpState.CanReplayTap(pad);
        }

        internal static void ApplyCurrentMode()
        {
            UninstallJumpReplacement();
            SettingsStore.EnsureLoaded();
            JumpPercentIntegration.ResetMeasurement(SettingsStore.Current.Enabled);
            if (!SettingsStore.Current.Enabled && !SettingsStore.Current.ShowMeasurement)
            {
                ReleaseTrajectoryProbe();
                return;
            }
            JumpPercentIntegration.EnsureDisplayHook();
            PlayerEntity player =
                EntityComponent.EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
            {
                return;
            }
            EnsureTrajectoryProbe(player);
            if (JKRuntime.Gameplay.GameFeatures.Movement == JKRuntime.Gameplay.MovementMode.VariableJump)
            {
                JumpPercentIntegration.ResetMeasurement(true);
                DiagnosticLog.Write(
                    "jump replacement suspended for Casual+");
                return;
            }
            Install(player, SettingsStore.Current.Enabled);
        }

        internal static void Uninstall()
        {
            UninstallJumpReplacement();
            JumpPercentIntegration.ResetMeasurement(true);
            ReleaseTrajectoryProbe();
        }

        private static void ReleaseTrajectoryProbe()
        {
            if (trajectoryProbe != null)
            {
                trajectoryProbe.Enabled = false;
                var components = typeof(EntityComponent.Entity).GetField("m_components", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(trajectoryProbe.gameObject) as List<EntityComponent.Component>;
                if (components != null) components.Remove(trajectoryProbe);
            }
            trajectoryProbe = null;
            trajectoryBody = null;
        }

        internal static void DetachChargePolicy()
        {
            UninstallJumpReplacement();
            JumpPercentIntegration.ResetMeasurement(true);
        }

        private static void UninstallJumpReplacement()
        {
            if (frameObservers != null) frameObservers.Dispose();
            frameObservers = null;
            if (lifecycleMarker != null && lifecycleRegistry != null)
            {
                lifecycleRegistry.Remove(lifecycleMarker);
            }
            lifecycleMarker = null;
            lifecycleRegistry = null;
            RestoreVanillaJump();
            if (replacementJumpState != null)
            {
                replacementJumpState.Dispose();
            }
            replacementJumpState = null;
            installedBody = null;
        }

        private static void Install(PlayerEntity player, bool correctionEnabled)
        {
            BehaviorTreeComp tree = player.GetComponent<BehaviorTreeComp>();
            JumpState original = tree == null
                ? null
                : tree.GetRaw().FindNode<JumpState>();
            IBTcomposite parent = tree == null
                ? null
                : tree.GetRaw().FindParentNodeOf<JumpState>() as IBTcomposite;
            if (tree != null && original == null && HasReplacementJump(tree))
            {
                // JumpSlot detaches us before a controller changes the graph.
                // A custom JumpState is intentional ownership, not a broken
                // native graph. Recomposition will resume us when it is restored.
                JumpPercentIntegration.ResetMeasurement(true);
                DiagnosticLog.Write("charge and observation suspended: another controller owns jumping");
                return;
            }
            if (!correctionEnabled && (original == null || parent == null || original.GetType() != typeof(JumpState)))
            {
                // A disabled gameplay mod must not replace another mod's custom
                // jump implementation merely to obtain a measurement.
                DiagnosticLog.Write("observation unavailable: native JumpState is owned/replaced by another controller");
                return;
            }
            if (tree == null || original == null || parent == null)
            {
                throw new InvalidOperationException(
                    "Jump King's native jump node is unavailable");
            }

            int childIndex = FindChildIndex(parent, original);
            if (childIndex < 0)
            {
                throw new InvalidOperationException(
                    "Jump King's native jump parent is malformed");
            }

            BodyComp body = player.m_body;
            bool levelAllowsSubframeCharge =
                LevelAllowsSubframeCharge();
            JKRuntime.Gameplay.BodyPipeline registry =
                new JKRuntime.Gameplay.BodyPipeline(
                    body,
                    correctionEnabled && (!levelAllowsSubframeCharge || SettingsStore.Current.QuarterStepCharge), 300, "subframe-charge");
            IBodyCompBehaviour wind = FindBehaviour<WindVelocityUpdateBehaviour>(
                body);
            if (wind == null)
            {
                throw new InvalidOperationException(
                    "Jump King's wind behaviour is unavailable");
            }

            if (trajectoryProbe == null
                || !object.ReferenceEquals(trajectoryBody, body))
            {
                throw new InvalidOperationException(
                    "Subframe Charge trajectory observer is unavailable");
            }
            SubframeChargeState replacement = new SubframeChargeState(
                    player,
                    trajectoryProbe,
                    correctionEnabled, correctionEnabled && SettingsStore.Current.QuarterStepCharge);
            JumpLifecycleMarker marker =
                new JumpLifecycleMarker(replacement);
            try
            {
                if (!registry.RegisterBefore(marker, wind))
                {
                    throw new InvalidOperationException(
                        "Could not register Subframe Charge lifecycle marker");
                }
                jumpBindings = JKRuntime.Gameplay.JumpNodeBindings.Replace(tree.GetRaw(), player, original, replacement);
                frameObservers = new ChargeFrameComponents(player, body, tree, replacement.Timeline);
                tree.Reset();
            }
            catch
            {
                if (frameObservers != null) frameObservers.Dispose();
                frameObservers = null;
                RestoreVanillaJump();
                registry.Remove(marker);
                replacement.Dispose();
                throw;
            }

            installedBody = body;
            lifecycleMarker = marker;
            lifecycleRegistry = registry;
            installedTree = tree;
            replacementJumpState = replacement;
            DiagnosticLog.Write(
                "installed native-surface JumpState replacement modified="
                + (correctionEnabled && !levelAllowsSubframeCharge)
                + " correction=" + correctionEnabled + " observation=True");
        }

        private static void EnsureTrajectoryProbe(PlayerEntity player)
        {
            BodyComp body = player.m_body;
            if (trajectoryProbe != null
                && object.ReferenceEquals(trajectoryBody, body)
                && trajectoryProbe.Enabled)
            {
                return;
            }
            if (trajectoryProbe != null)
            {
                trajectoryProbe.Enabled = false;
            }

            trajectoryProbe = new JumpTrajectoryProbe(body);
            trajectoryBody = body;
            player.AddComponents(trajectoryProbe);
            DiagnosticLog.Write("trajectory observer attached");
        }

        private static bool HasReplacementJump(BehaviorTreeComp tree)
        {
            FieldInfo root = typeof(BTmanager).GetField("m_root_node", BindingFlags.Instance | BindingFlags.NonPublic);
            if (root == null) throw new MissingFieldException("BTmanager.m_root_node");
            var pending = new Queue<IBTnode>();
            var visited = new HashSet<IBTnode>();
            pending.Enqueue((IBTnode)root.GetValue(tree.GetRaw()));
            while (pending.Count != 0)
            {
                IBTnode node = pending.Dequeue();
                if (node == null || !visited.Add(node)) continue;
                if (node is JumpState && node.GetType() != typeof(JumpState)) return true;
                foreach (IBTnode related in node.GetRelatedNodes()) pending.Enqueue(related);
            }
            return false;
        }

        private static int FindChildIndex(
            IBTcomposite parent,
            JumpState jumpState)
        {
            for (int index = 0; index < parent.Children.Length; index++)
            {
                if (object.ReferenceEquals(parent.Children[index], jumpState))
                {
                    return index;
                }
            }
            return -1;
        }

        private static void RestoreVanillaJump()
        {
            if (jumpBindings != null)
            {
                jumpBindings.Restore();
                jumpBindings = null;
                if (installedTree != null)
                {
                    installedTree.Reset();
                }
            }

            installedTree = null;
        }

        private static IBodyCompBehaviour FindBehaviour<T>(BodyComp body)
            where T : IBodyCompBehaviour
        {
            foreach (IBodyCompBehaviour behaviour in body.GetBehaviourList())
            {
                if (behaviour is T)
                {
                    return behaviour;
                }
            }
            return null;
        }

        private static bool LevelAllowsSubframeCharge()
        {
            string[] tags = Game1.instance == null
                || Game1.instance.contentManager.level == null
                    ? null
                    : Game1.instance.contentManager.level.Info.Tags;
            return LevelPermission.AllowsSubframeCharge(tags);
        }

        private sealed class JumpLifecycleMarker : IBodyCompBehaviour
        {
            private readonly SubframeChargeState state;

            internal JumpLifecycleMarker(SubframeChargeState jumpState)
            {
                state = jumpState;
            }

            public bool ExecuteBehaviour(BehaviourContext context)
            {
                state.ObserveFrameInput();
                return true;
            }
        }
    }
}
