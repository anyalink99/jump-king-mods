using System;
using System.Collections.Generic;
using BehaviorTree;
using EntityComponent.BT;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace CasualJumping
{
    internal static class CasualControllerInstaller
    {
        private static JKRuntime.RuntimeScope installation;
        private static IDisposable policy;
        private static BehaviorTreeComp installedTree;
        private static IBTcomposite jumpParent;
        private static JumpState vanillaJumpState;
        private static IBTnode vanillaJumpBlocker;
        private static int jumpChildIndex = -1;

        internal static void ApplyCurrentMode()
        {
            if (policy == null) policy = JKRuntime.Gameplay.JumpSlot.RegisterControllerPolicy(ApplyCore, RemoveCore,
                delegate { return SettingsStore.Current.Mode != ControlMode.CasualPlus; });
            else JKRuntime.Gameplay.JumpSlot.Refresh();
        }

        private static void ApplyCore()
        {
            RemoveCore();
            SettingsStore.EnsureLoaded();
            ControlMode mode = SettingsStore.Current.Mode;
            installation = new JKRuntime.RuntimeScope();
            try
            {
                if (mode == ControlMode.Vanilla)
                {
                    return;
                }

                PlayerEntity player =
                    EntityComponent.EntityManager.instance.Find<PlayerEntity>();
                if (player == null)
                {
                    return;
                }

                BodyComp body = player.m_body;
                JKRuntime.Gameplay.BodyPipeline newRegistry = installation.Own(
                    new JKRuntime.Gameplay.BodyPipeline(
                        body,
                        !LevelAllowsCurrentMode(), 200, "casual-jumping"));
                IBodyCompBehaviour wind =
                    FindBehaviour<WindVelocityUpdateBehaviour>(body);
                IBodyCompBehaviour beforeMovement =
                    FindBehaviour<UpdateXPositionFromVelocityBehaviour>(body);
                IBodyCompBehaviour xCollision =
                    FindBehaviour<ResolveXCollisionBehaviour>(body);
                IBodyCompBehaviour yCollision =
                    FindBehaviour<ResolveYCollisionBehaviour>(body);
                if (wind == null
                    || beforeMovement == null
                    || xCollision == null
                    || yCollision == null)
                {
                    throw new InvalidOperationException(
                        "Jump King body behaviour order is unavailable");
                }

                CasualController newController = new CasualController(player);
                PreMovementVelocityObserver newPreMovementObserver =
                    new PreMovementVelocityObserver(newController);
                XCollisionObserver newXObserver =
                    new XCollisionObserver(newController);
                YCollisionObserver newYObserver =
                    new YCollisionObserver(newController);
                RegisterBehaviours(
                    newRegistry,
                    newController,
                    newPreMovementObserver,
                    newXObserver,
                    newYObserver,
                    wind,
                    beforeMovement,
                    xCollision,
                    yCollision);

                if (mode == ControlMode.CasualPlus)
                {
                    installation.Defer(RestoreVanillaJump);
                    DisableVanillaJump(player);
                }
                installation.Own(JKRuntime.State.GameState.Snapshots.Register(newController.CreateStateParticipant()));

            }
            catch (Exception error)
            {
                try { RemoveCore(); }
                catch (Exception cleanup) { throw new AggregateException("Casual installation and rollback failed", error, cleanup); }
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (policy == null) { RemoveCore(); return; }
            policy.Dispose(); policy = null;
        }
        private static void RemoveCore()
        {
            if (installation == null) return;
            installation.Dispose();
            installation = null;
        }

        private static void RegisterBehaviours(
            JKRuntime.Gameplay.BodyPipeline registry,
            CasualController newController,
            PreMovementVelocityObserver newPreMovementObserver,
            XCollisionObserver newXObserver,
            YCollisionObserver newYObserver,
            IBodyCompBehaviour wind,
            IBodyCompBehaviour beforeMovement,
            IBodyCompBehaviour xCollision,
            IBodyCompBehaviour yCollision)
        {
            if (!registry.RegisterBefore(newController, wind))
            {
                throw new InvalidOperationException(
                    "Could not register Casual Jumping before wind");
            }
            if (!registry.RegisterBefore(
                newPreMovementObserver,
                beforeMovement))
            {
                registry.Remove(newController);
                throw new InvalidOperationException(
                    "Could not register Casual Jumping velocity observer");
            }
            if (!registry.RegisterAfter(newXObserver, xCollision))
            {
                registry.Remove(newPreMovementObserver);
                registry.Remove(newController);
                throw new InvalidOperationException(
                    "Could not register Casual Jumping X collision observer");
            }
            if (!registry.RegisterAfter(newYObserver, yCollision))
            {
                registry.Remove(newXObserver);
                registry.Remove(newPreMovementObserver);
                registry.Remove(newController);
                throw new InvalidOperationException(
                    "Could not register Casual Jumping Y collision observer");
            }
        }

        private static bool LevelAllowsCurrentMode()
        {
            string[] tags = Game1.instance.contentManager.level == null
                ? null
                : Game1.instance.contentManager.level.Info.Tags;
            return LevelPermission.AllowsCasual(tags);
        }

        private static void DisableVanillaJump(PlayerEntity player)
        {
            BehaviorTreeComp tree = player.GetComponent<BehaviorTreeComp>();
            if (tree == null)
            {
                throw new InvalidOperationException(
                    "Jump King behaviour tree is unavailable");
            }

            JumpState jumpState = tree.GetRaw().FindNode<JumpState>();
            IBTcomposite parent =
                tree.GetRaw().FindParentNodeOf<JumpState>() as IBTcomposite;
            if (jumpState == null || parent == null)
            {
                throw new InvalidOperationException(
                    "Jump King's vanilla jump node is unavailable");
            }

            int childIndex = FindChildIndex(parent, jumpState);
            if (childIndex < 0)
            {
                throw new InvalidOperationException(
                    "Jump King's vanilla jump parent is malformed");
            }

            IBTnode blocker = StaticNodeSimple.Failure;
            parent.Children[childIndex] = blocker;
            installedTree = tree;
            jumpParent = parent;
            vanillaJumpState = jumpState;
            vanillaJumpBlocker = blocker;
            jumpChildIndex = childIndex;
            tree.Reset();
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
            bool canRestore = jumpParent != null
                && vanillaJumpState != null
                && jumpChildIndex >= 0
                && jumpChildIndex < jumpParent.Children.Length
                && object.ReferenceEquals(
                    jumpParent.Children[jumpChildIndex],
                    vanillaJumpBlocker);
            if (jumpParent != null && !canRestore && (jumpChildIndex < 0 || jumpChildIndex >= jumpParent.Children.Length
                || !ReferenceEquals(jumpParent.Children[jumpChildIndex], vanillaJumpState)))
                throw new InvalidOperationException("Casual jump slot ownership changed externally; restoration remains pending");
            if (canRestore)
            {
                jumpParent.Children[jumpChildIndex] = vanillaJumpState;
            }
            // A previous Reset may have thrown after the slot was restored.
            // Retain its ownership record until the reset itself succeeds.
            if (installedTree != null && jumpParent != null && jumpChildIndex >= 0
                && jumpChildIndex < jumpParent.Children.Length
                && ReferenceEquals(jumpParent.Children[jumpChildIndex], vanillaJumpState)) installedTree.Reset();

            installedTree = null;
            jumpParent = null;
            vanillaJumpState = null;
            vanillaJumpBlocker = null;
            jumpChildIndex = -1;
        }

        private static IBodyCompBehaviour FindBehaviour<T>(BodyComp body)
            where T : IBodyCompBehaviour
        {
            IReadOnlyCollection<IBodyCompBehaviour> behaviours =
                body.GetBehaviourList();
            foreach (IBodyCompBehaviour behaviour in behaviours)
            {
                if (behaviour is T)
                {
                    return behaviour;
                }
            }
            return null;
        }
    }
}
