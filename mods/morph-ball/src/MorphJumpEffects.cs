using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using JumpKing.Player;

namespace MorphBallMod
{
    internal static class MorphJumpEffects
    {
        private static readonly MethodInfo HandleParticlesMethod =
            typeof(JumpState).GetMethod(
                "HandleParticles",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(BodyComp),
                    typeof(Func<IEnumerable<Type>>)
                },
                null);
        private static readonly MethodInfo OnBlocksMethod =
            typeof(BodyComp).GetMethod(
                "OnBlocks",
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

        internal static void ValidateContract()
        {
            if (HandleParticlesMethod == null || OnBlocksMethod == null)
            {
                throw new MissingMethodException(
                    "Jump King's jump particle API is unavailable");
            }
        }

        internal static void Play(BodyComp body, JumpState jumpState)
        {
            Game1.instance.contentManager.audio.player.Jump.PlayOneShot();
            // A custom controller may remove JumpState altogether. Sounds and
            // OnJump remain valid; only its optional particle handler is absent.
            if (jumpState != null) SpawnParticles(body, jumpState);
            if (PlayerEntity.OnJumpCall != null)
            {
                PlayerEntity.OnJumpCall();
            }
        }

        private static void SpawnParticles(BodyComp body, JumpState jumpState)
        {
            ValidateContract();
            Func<IEnumerable<Type>> onBlocks =
                (Func<IEnumerable<Type>>)Delegate.CreateDelegate(
                    typeof(Func<IEnumerable<Type>>),
                    body,
                    OnBlocksMethod);
            HandleParticlesMethod.Invoke(
                jumpState,
                new object[] { body, onBlocks });
        }
    }
}
