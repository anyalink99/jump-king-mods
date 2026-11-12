using System;
using System.Collections.Generic;

namespace MorphBallMod
{
    internal enum MorphPipelinePhase
    {
        Controller,
        PreMovement,
        XCollision,
        Teleport,
        YCollision,
        PrePositionCap,
        PostPositionCap,
        BumpSound
    }

    internal enum MorphPipelinePlacement
    {
        Before,
        After
    }

    internal struct MorphPipelineStep
    {
        internal MorphPipelinePhase Phase;
        internal MorphPipelinePlacement Placement;
        internal string FailureMessage;

        internal MorphPipelineStep(
            MorphPipelinePhase phase,
            MorphPipelinePlacement placement,
            string failureMessage)
        {
            Phase = phase;
            Placement = placement;
            FailureMessage = failureMessage;
        }
    }

    internal static class MorphPipelineContract
    {
        private static readonly MorphPipelineStep[] OrderedSteps =
        {
            Step(
                MorphPipelinePhase.Controller,
                MorphPipelinePlacement.Before,
                "Could not register Ball King movement"),
            Step(
                MorphPipelinePhase.PreMovement,
                MorphPipelinePlacement.Before,
                "Could not register Ball King velocity observer"),
            Step(
                MorphPipelinePhase.XCollision,
                MorphPipelinePlacement.After,
                "Could not register Ball King X collision observer"),
            Step(
                MorphPipelinePhase.Teleport,
                MorphPipelinePlacement.After,
                "Could not register Ball King teleport observer"),
            Step(
                MorphPipelinePhase.YCollision,
                MorphPipelinePlacement.After,
                "Could not register Ball King Y collision observer"),
            Step(
                MorphPipelinePhase.PrePositionCap,
                MorphPipelinePlacement.Before,
                "Could not observe Jump King's native position cap"),
            Step(
                MorphPipelinePhase.PostPositionCap,
                MorphPipelinePlacement.After,
                "Could not observe Jump King's native position cap"),
            Step(
                MorphPipelinePhase.BumpSound,
                MorphPipelinePlacement.Before,
                "Could not register Ball King bump suppressor")
        };

        internal static IList<MorphPipelineStep> Steps
        {
            get { return Array.AsReadOnly(OrderedSteps); }
        }

        internal static void Validate()
        {
            Array values = Enum.GetValues(typeof(MorphPipelinePhase));
            if (OrderedSteps.Length != values.Length)
            {
                throw new InvalidOperationException(
                    "Ball King pipeline does not cover every phase");
            }
            HashSet<MorphPipelinePhase> phases =
                new HashSet<MorphPipelinePhase>();
            for (int index = 0; index < OrderedSteps.Length; index++)
            {
                if (!phases.Add(OrderedSteps[index].Phase))
                {
                    throw new InvalidOperationException(
                        "Ball King pipeline contains a duplicate phase");
                }
            }
        }

        private static MorphPipelineStep Step(
            MorphPipelinePhase phase,
            MorphPipelinePlacement placement,
            string failureMessage)
        {
            return new MorphPipelineStep(
                phase,
                placement,
                failureMessage);
        }
    }
}
