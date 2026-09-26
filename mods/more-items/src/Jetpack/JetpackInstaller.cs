using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using MoreItems;

namespace JumpKingJetpack
{
    internal static class JetpackInstaller
    {
        private static JKRuntime.RuntimeScope installation;
        private static JetpackItem item;

        internal static void Apply()
        {
            Uninstall();
            SettingsStore.EnsureLoaded();
            if (!SettingsStore.Current.JetpackEquipped)
            {
                return;
            }

            PlayerEntity player =
                EntityComponent.EntityManager.instance.Find<PlayerEntity>();
            if (player == null)
            {
                return;
            }

            installation = new JKRuntime.RuntimeScope();
            installation.Defer(delegate { ThrustState.SetActive(false); });
            try
            {
            BodyComp body = player.m_body;
            JKRuntime.Gameplay.BodyPipeline newRegistry =
                installation.Own(new JKRuntime.Gameplay.BodyPipeline(
                    body,
                    false, 100, "more-items.jetpack"));
            IBodyCompBehaviour wind =
                FindBehaviour<WindVelocityUpdateBehaviour>(body);
            IBodyCompBehaviour controllerTarget =
                wind;
            IBodyCompBehaviour beforeMovement =
                FindBehaviour<UpdateXPositionFromVelocityBehaviour>(body);
            IBodyCompBehaviour yCollision =
                FindBehaviour<ResolveYCollisionBehaviour>(body);
            if (wind == null || beforeMovement == null || yCollision == null)
            {
                throw new InvalidOperationException(
                    "Jump King body behaviour order is unavailable");
            }

            JetpackUsageMarker newUsageMarker = new JetpackUsageMarker(
                body,
                LevelAllowsJetpack());
            installation.Defer(delegate { if (newUsageMarker.IsAlive) newUsageMarker.Destroy(); });
            JetpackController newController = new JetpackController(
                player,
                newUsageMarker.MarkUsed);
            JetpackPreMovementObserver newPreMovementObserver =
                new JetpackPreMovementObserver(newController);
            JetpackYCollisionObserver newYCollisionObserver =
                new JetpackYCollisionObserver(newController);
                RegisterBehaviours(
                    newRegistry,
                    newController,
                    newPreMovementObserver,
                    newYCollisionObserver,
                    controllerTarget,
                    beforeMovement,
                    yCollision);
                var airSprite = new JetpackAirSpriteComponent(player);
                installation.Own(new JKRuntime.Gameplay.ComponentAttachment(player, airSprite));
                item = installation.Own(new JetpackItem(player));
                newController.AttachItem(item);
                            installation.Own(JKRuntime.State.GameState.Snapshots.Register(newController.CreateStateParticipant()));
            }
            catch (Exception error)
            {
                try { Uninstall(); }
                catch (Exception cleanup) { throw new AggregateException("Jetpack installation and rollback failed", error, cleanup); }
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (installation == null) return;
            installation.Dispose();
            installation = null;
            item = null;
        }

        internal static void RefreshSettings()
        {
            if (item != null)
            {
                item.ApplySettings();
            }
        }

        private static void RegisterBehaviours(
            JKRuntime.Gameplay.BodyPipeline newRegistry,
            JetpackController newController,
            JetpackPreMovementObserver newPreMovementObserver,
            JetpackYCollisionObserver newYCollisionObserver,
            IBodyCompBehaviour controllerTarget,
            IBodyCompBehaviour beforeMovement,
            IBodyCompBehaviour yCollision)
        {
            if (!newRegistry.RegisterBefore(
                newController,
                controllerTarget))
            {
                throw new InvalidOperationException(
                    "Could not register Jetpack movement");
            }
            if (!newRegistry.RegisterBefore(
                newPreMovementObserver,
                beforeMovement))
            {
                newRegistry.Remove(newController);
                throw new InvalidOperationException(
                    "Could not register Jetpack velocity observer");
            }
            if (!newRegistry.RegisterAfter(
                newYCollisionObserver,
                yCollision))
            {
                newRegistry.Remove(newPreMovementObserver);
                newRegistry.Remove(newController);
                throw new InvalidOperationException(
                    "Could not register Jetpack Y collision observer");
            }
        }

        private static bool LevelAllowsJetpack()
        {
            string[] tags = Game1.instance.contentManager.level == null
                ? null
                : Game1.instance.contentManager.level.Info.Tags;
            return LevelPermission.AllowsJetpack(tags);
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
