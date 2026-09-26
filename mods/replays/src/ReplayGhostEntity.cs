using System;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace Replays
{
    internal sealed class ReplayGhostEntity : Entity
    {
        private readonly ReplayData replay;
        private PlayerEntity player;
        private int frameIndex = -1;
        private bool hasAttemptStamp;
        private JKRuntime.State.AttemptStamp attemptStamp;

        internal ReplayGhostEntity(ReplayData value)
        {
            replay = value;
            player = Find<PlayerEntity>();
            if (player == null)
                throw new InvalidOperationException(
                    "Jump King player is not ready for the racing ghost");
            ReplayEntityDrawOrder.PlaceImmediatelyBefore(this, player);
        }

        protected override void Update(float delta)
        {
            if (replay == null || replay.Frames.Count == 0) return;
            if (player == null || !player.IsAlive)
                player = Find<PlayerEntity>();
            if (player == null || !player.m_body.Enabled || ReplayUI.IsOpen)
                return;
            JKRuntime.State.AttemptStamp current = ReplayAttemptClock.Read();
            if (hasAttemptStamp && !attemptStamp.Equals(current))
                frameIndex = -1;
            attemptStamp = current;
            hasAttemptStamp = true;
            if (frameIndex < replay.Frames.Count - 1) frameIndex++;
        }

        public override void Draw()
        {
            if (replay == null || replay.Frames.Count == 0) return;
            int index = Math.Max(0, frameIndex);
            ReplayGhostRenderer.Draw(
                replay.Frames[index],
                ReplayAppearanceTrack.AtFrame(replay, index),
                new Color(90, 225, 240),
                0.55f,
                true);
        }
    }
}
