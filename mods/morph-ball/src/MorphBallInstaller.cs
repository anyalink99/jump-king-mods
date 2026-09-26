using System;
using System.Collections.Generic;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;

namespace MorphBallMod
{
    internal static class MorphBallInstaller
    {
        private static JKRuntime.RuntimeScope installation;

        internal static void Apply()
        {
            Uninstall();
            SettingsStore.EnsureLoaded();
            if (!SettingsStore.Current.EnableMorphBall)
            {
                return;
            }
            PlayerEntity player = EntityComponent.EntityManager.instance
                .Find<PlayerEntity>();
            if (player == null)
            {
                return;
            }
            JumpKingContract.ValidateRuntime();
            MorphPipelineContract.Validate();

            installation = new JKRuntime.RuntimeScope();
            installation.Defer(BallFormState.Reset);
            try
            {
            BodyComp body = player.m_body;
            JKRuntime.Gameplay.BodyPipeline newRegistry =
                installation.Own(new JKRuntime.Gameplay.BodyPipeline(
                    body,
                    !LevelAllowsBallKing(), 0, "morph-ball"));
            IBodyCompBehaviour wind =
                FindBehaviour<WindVelocityUpdateBehaviour>(body);
            IBodyCompBehaviour controllerTarget =
                wind;
            IBodyCompBehaviour beforeMovement =
                FindBehaviour<CollisionCheckEarlyExitBehaviour>(body);
            IBodyCompBehaviour xCollision =
                FindBehaviour<ResolveXCollisionBehaviour>(body);
            IBodyCompBehaviour positionCap =
                FindBehaviour<CapPositionBehaviour>(body);
            IBodyCompBehaviour teleport =
                FindBehaviour<HandlePlayerTeleportBehaviour>(body);
            IBodyCompBehaviour yCollision =
                FindBehaviour<ResolveYCollisionBehaviour>(body);
            IBodyCompBehaviour bumpSound =
                FindBehaviour<PlayBumpSFXBehaviour>(body);
            if (wind == null
                || beforeMovement == null
                || positionCap == null
                || xCollision == null
                || teleport == null
                || yCollision == null
                || bumpSound == null)
            {
                throw new InvalidOperationException(
                    "Jump King body behaviour order is unavailable");
            }

            MorphController newController = new MorphController(player);
            installation.Defer(newController.Dispose);
            installation.Defer(newController.ForceUnmorph);
            MorphPreMovementObserver newPreMovementObserver =
                new MorphPreMovementObserver(newController);
            MorphXCollisionObserver newXObserver =
                new MorphXCollisionObserver(newController);
            MorphPreCapObserver newPreCapObserver =
                new MorphPreCapObserver(newController);
            MorphPostCapObserver newPostCapObserver =
                new MorphPostCapObserver(newController);
            MorphYCollisionObserver newYObserver =
                new MorphYCollisionObserver(newController);
            MorphTeleportObserver newTeleportObserver =
                new MorphTeleportObserver(newController);
            MorphBumpSuppressor newBumpSuppressor =
                new MorphBumpSuppressor(newController);
            MorphVisualComponent newVisual =
                player.GetComponent<MorphVisualComponent>();
            bool addVisual = newVisual == null;
            if (addVisual)
            {
                newVisual = new MorphVisualComponent(player, newController);
            }
            else
            {
                newVisual.AttachController(newController);
                newVisual.Enabled = true;
            }
            installation.Defer(delegate { newVisual.Enabled = false; });
            Dictionary<MorphPipelinePhase, IBodyCompBehaviour> behaviours =
                new Dictionary<MorphPipelinePhase, IBodyCompBehaviour>
                {
                    { MorphPipelinePhase.Controller, newController },
                    { MorphPipelinePhase.PreMovement, newPreMovementObserver },
                    { MorphPipelinePhase.XCollision, newXObserver },
                    { MorphPipelinePhase.Teleport, newTeleportObserver },
                    { MorphPipelinePhase.YCollision, newYObserver },
                    { MorphPipelinePhase.PrePositionCap, newPreCapObserver },
                    { MorphPipelinePhase.PostPositionCap, newPostCapObserver },
                    { MorphPipelinePhase.BumpSound, newBumpSuppressor }
                };
            Dictionary<MorphPipelinePhase, IBodyCompBehaviour> targets =
                new Dictionary<MorphPipelinePhase, IBodyCompBehaviour>
                {
                    { MorphPipelinePhase.Controller, controllerTarget },
                    { MorphPipelinePhase.PreMovement, beforeMovement },
                    { MorphPipelinePhase.XCollision, xCollision },
                    { MorphPipelinePhase.Teleport, teleport },
                    { MorphPipelinePhase.YCollision, yCollision },
                    { MorphPipelinePhase.PrePositionCap, positionCap },
                    { MorphPipelinePhase.PostPositionCap, positionCap },
                    { MorphPipelinePhase.BumpSound, bumpSound }
                };
                foreach (MorphPipelineStep step
                    in MorphPipelineContract.Steps)
                {
                    bool registered = step.Placement
                        == MorphPipelinePlacement.Before
                            ? newRegistry.RegisterBefore(
                                behaviours[step.Phase],
                                targets[step.Phase])
                            : newRegistry.RegisterAfter(
                                behaviours[step.Phase],
                                targets[step.Phase]);
                    if (!registered)
                    {
                        throw new InvalidOperationException(
                            step.FailureMessage);
                    }
                }
                if (addVisual)
                {
                    installation.Own(new JKRuntime.Gameplay.ComponentAttachment(player, newVisual));
                }
                BallFormState.Enabled = true;
                installation.Own(JKRuntime.State.GameState.Snapshots.Register(newController.CreateStateParticipant()));
            }
            catch (Exception error)
            {
                try { Uninstall(); }
                catch (Exception cleanup) { throw new AggregateException("Ball installation and rollback failed", error, cleanup); }
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (installation == null) return;
            installation.Dispose();
            installation = null;
        }

        private static bool LevelAllowsBallKing()
        {
            string[] tags = Game1.instance.contentManager.level == null
                ? null
                : Game1.instance.contentManager.level.Info.Tags;
            return LevelPermission.AllowsBallKing(tags);
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
