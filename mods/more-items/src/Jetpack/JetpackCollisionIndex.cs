using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JumpKingJetpack
{
    internal sealed class JetpackCollisionIndex
    {
        private const int BucketSize = 16;

        private static readonly FieldInfo HitboxesField =
            typeof(LevelScreen).GetField(
                "m_hitboxes",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Dictionary<long, List<IBlock>> buckets =
            new Dictionary<long, List<IBlock>>();
        private readonly HashSet<IBlock> candidates =
            new HashSet<IBlock>();

        private LevelScreen screen;

        internal bool Collides(Rectangle bounds)
        {
            Refresh();
            if (screen == null)
            {
                return false;
            }

            candidates.Clear();
            int left = Cell(bounds.Left);
            int top = Cell(bounds.Top);
            int right = Cell(bounds.Right - 1);
            int bottom = Cell(bounds.Bottom - 1);
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    List<IBlock> blocks;
                    if (!buckets.TryGetValue(Key(x, y), out blocks))
                    {
                        continue;
                    }
                    for (int index = 0; index < blocks.Count; index++)
                    {
                        candidates.Add(blocks[index]);
                    }
                }
            }

            foreach (IBlock block in candidates)
            {
                Rectangle overlap;
                if (block.Intersects(bounds, out overlap)
                    == BlockCollisionType.Collision_Blocking)
                {
                    return true;
                }
            }
            return false;
        }

        internal void Reset()
        {
            screen = null;
            buckets.Clear();
            candidates.Clear();
        }

        private void Refresh()
        {
            LevelScreen current = LevelManager.CurrentScreen;
            if (ReferenceEquals(screen, current))
            {
                return;
            }

            screen = current;
            buckets.Clear();
            candidates.Clear();
            if (screen == null || HitboxesField == null)
            {
                return;
            }

            IBlock[] blocks = HitboxesField.GetValue(screen) as IBlock[];
            if (blocks == null)
            {
                return;
            }

            for (int index = 0; index < blocks.Length; index++)
            {
                Add(blocks[index]);
            }
        }

        private void Add(IBlock block)
        {
            if (block == null)
            {
                return;
            }

            Rectangle bounds = block.GetRect();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            int left = Cell(bounds.Left);
            int top = Cell(bounds.Top);
            int right = Cell(bounds.Right - 1);
            int bottom = Cell(bounds.Bottom - 1);
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    long key = Key(x, y);
                    List<IBlock> blocks;
                    if (!buckets.TryGetValue(key, out blocks))
                    {
                        blocks = new List<IBlock>();
                        buckets.Add(key, blocks);
                    }
                    blocks.Add(block);
                }
            }
        }

        private static int Cell(int value)
        {
            return (int)Math.Floor(value / (double)BucketSize);
        }

        private static long Key(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }
    }
}
