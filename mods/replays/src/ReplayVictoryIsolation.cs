using System;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace Replays
{
    internal sealed class ReplayVictoryIsolation : IDisposable
    {
        private static readonly FieldInfo IsOnGroundField =
            typeof(BodyComp).GetField(
                "_is_on_ground",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private PlayerEntity player;
        private bool originalIsOnGround;
        private bool active;

        internal static void ValidateContract()
        {
            if (IsOnGroundField == null)
            {
                throw new InvalidOperationException(
                    "Jump King ground-state contract is unavailable");
            }
        }

        internal void Begin()
        {
            if (active) return;
            ValidateContract();
            player = EntityManager.instance.Find<PlayerEntity>();
            if (player == null || player.m_body == null)
                throw new InvalidOperationException(
                    "Jump King player is not ready for replay playback");
            originalIsOnGround = (bool)IsOnGroundField.GetValue(player.m_body);
            active = true;
            Maintain();
        }

        internal void Maintain()
        {
            if (!active || player == null || !player.IsAlive
                || player.m_body == null)
            {
                return;
            }
            IsOnGroundField.SetValue(player.m_body, false);
        }

        public void Dispose()
        {
            if (active && player != null && player.IsAlive
                && player.m_body != null)
            {
                IsOnGroundField.SetValue(
                    player.m_body,
                    originalIsOnGround);
            }
            active = false;
            player = null;
        }
    }
}
