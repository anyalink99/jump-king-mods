using System;
using System.Collections.Generic;

namespace MorphBallMod
{
    internal static class JumpKingContract
    {
        internal static void ValidateLevelLoading()
        {
            ValidateAll(
                new ContractCheck(
                    "slope topology",
                    BallKingBlockFactory.ValidateContract));
        }

        internal static void ValidateRuntime()
        {
            ValidateAll(
                new ContractCheck(
                    "body physics",
                    MorphController.ValidateContract),
                new ContractCheck(
                    "area transitions",
                    JKRuntime.Gameplay.AreaEntryObserver.ValidateContract),
                new ContractCheck(
                    "landing effects",
                    MorphLandingEffects.ValidateContract),
                new ContractCheck(
                    "jump effects",
                    MorphJumpEffects.ValidateContract),
                new ContractCheck(
                    "save state",
                    MorphStateStore.ValidateContract),
                new ContractCheck(
                    "player sprite",
                    MorphVisualComponent.ValidateContract));
        }

        private static void ValidateAll(params ContractCheck[] checks)
        {
            List<string> failures = new List<string>();
            for (int index = 0; index < checks.Length; index++)
            {
                try
                {
                    checks[index].Validate();
                }
                catch (Exception exception)
                {
                    failures.Add(
                        checks[index].Name + ": " + exception.Message);
                }
            }
            if (failures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Ball King is incompatible with this Jump King build: "
                        + string.Join("; ", failures.ToArray()));
            }
        }

        private sealed class ContractCheck
        {
            internal readonly string Name;
            internal readonly Action Validate;

            internal ContractCheck(string name, Action validate)
            {
                Name = name;
                Validate = validate;
            }
        }
    }
}
