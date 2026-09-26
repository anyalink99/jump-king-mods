using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    // A small, lazy pixel collision grid around ONE endpoint, independent of the
    // real camera. Both neighbouring screens therefore work during assembly too.
    internal sealed class PixelCollision
    {
        private static readonly FieldInfo Hitboxes = typeof(LevelScreen).GetField("m_hitboxes", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly Rectangle bounds;
        private readonly List<IBlock> blocks = new List<IBlock>();
        private readonly byte[] cells;
        internal PixelCollision(LevelScreen[] screens, Rectangle region)
        {
            if (Hitboxes == null || screens == null) throw new InvalidOperationException("Native particle collision contract unavailable");
            bounds = region;
            cells = new byte[checked(region.Width * region.Height)];
            var seen = new HashSet<IBlock>();
            foreach (var screen in screens)
            {
                var source = Hitboxes.GetValue(screen) as IBlock[];
                if (source == null) throw new InvalidOperationException("Native screen hitboxes unavailable");
                foreach (var block in source)
                    if (block != null && block.GetRect().Intersects(region) && seen.Add(block)) blocks.Add(block);
            }
        }
        internal bool Blocked(Vector2 position)
        {
            int x = (int)Math.Floor(position.X), y = (int)Math.Floor(position.Y);
            if (!bounds.Contains(x,y)) return true; // visual particles stay bounded
            int index = (y-bounds.Y)*bounds.Width+x-bounds.X;
            if (cells[index] == 0)
            {
                var pixel = new Rectangle(x,y,1,1);
                cells[index] = 1;
                foreach (var block in blocks)
                {
                    Rectangle overlap;
                    if (block.Intersects(pixel,out overlap) == BlockCollisionType.Collision_Blocking)
                    { cells[index] = 2; break; }
                }
            }
            return cells[index] == 2;
        }
        internal bool FindFree(Vector2 origin, out Vector2 free)
        {
            free = origin;
            if (!Blocked(origin)) return true;
            // Native art can overlap terrain outside the player's narrow hitbox.
            // Eject these texels to the nearest exposed cell, never deep through a wall.
            float best = float.MaxValue;
            for (int y=-8;y<=8;y++) for (int x=-8;x<=8;x++)
            {
                int distance=x*x+y*y;
                if (distance>=best || Blocked(origin+new Vector2(x,y))) continue;
                best=distance; free=origin+new Vector2(x,y);
            }
            return best != float.MaxValue;
        }
    }

    // Immutable outward paths. Assembly replays them backwards, so collisions
    // cannot cause reconstruction drift or depend on draw rate / snapshot order.
    internal sealed class PixelPath
    {
        private const int Steps=40;
        private readonly Vector2[] points=new Vector2[Steps+1];
        internal readonly bool Visible;
        internal PixelPath(PixelCollision space,Vector2 origin,Vector2 displacement)
        {
            Vector2 position;
            Visible=space.FindFree(origin,out position);
            Vector2 velocity=displacement/Steps;
            points[0]=position-origin;
            for(int i=1;i<=Steps;i++)
            {
                // Same independent-axis restitution/damping as Jetpack's trail.
                // Substeps are <1px, so even a one-pixel wall cannot be tunnelled.
                Vector2 horizontal=position+new Vector2(velocity.X,0);
                if(space.Blocked(horizontal)) { velocity.X*=-.28f; velocity.Y*=.9f; }
                else position=horizontal;
                Vector2 vertical=position+new Vector2(0,velocity.Y);
                if(space.Blocked(vertical)) { velocity.Y*=-.24f; velocity.X*=.72f; }
                else position=vertical;
                points[i]=position-origin;
            }
        }
        internal Vector2 At(float scatter)
        {
            // Discrete cached substeps remain collision-free even at concave corners.
            int index=Math.Min(Steps,Math.Max(0,(int)(scatter*Steps)));
            return points[index];
        }
    }
}
