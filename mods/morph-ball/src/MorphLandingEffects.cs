using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Player;

namespace MorphBallMod
{
    internal static class MorphLandingEffects
    {
        private static readonly MethodInfo HandleParticlesMethod =
            typeof(IsOnGround).GetMethod(
                "HandleParticles",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnBlocksMethod =
            typeof(BodyComp).GetMethod(
                "OnBlocks",
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        private static readonly FieldInfo LastResultField =
            typeof(IBTnode).GetField(
                "m_last_result",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ValidateContract()
        {
            if (HandleParticlesMethod == null
                || OnBlocksMethod == null
                || LastResultField == null)
            {
                throw new MissingMemberException(
                    "Jump King's landing effect contract is unavailable");
            }
        }

        internal static void Play()
        {
            Game1.instance.contentManager.audio.player.Land.PlayOneShot();
        }

        internal static void CompleteWithoutSound(
            BodyComp body,
            IsOnGround landingState)
        {
            if (body == null || landingState == null)
            {
                throw new ArgumentNullException(
                    body == null ? "body" : "landingState");
            }
            ValidateContract();
            BTresult previousResult = landingState.last_result;
            if (previousResult == BTresult.Success)
            {
                return;
            }
            if (previousResult != BTresult.Running)
            {
                Func<IEnumerable<Type>> onBlocks =
                    (Func<IEnumerable<Type>>)Delegate.CreateDelegate(
                        typeof(Func<IEnumerable<Type>>),
                        body,
                        OnBlocksMethod);
                HandleParticlesMethod.Invoke(
                    landingState,
                    new object[] { body, onBlocks });
            }
            LastResultField.SetValue(landingState, BTresult.Success);
        }
    }
}
