using System;
using System.Collections.Generic;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace JKRuntime.Geometry
{
    internal static class TopologyOpenings
    {
        private struct Edges { internal byte Sides; internal ulong Top,Bottom; }
        private static readonly Dictionary<LevelScreen, Edges> cache = new Dictionary<LevelScreen, Edges>();
        internal static void Reset() { cache.Clear(); ExpansionPortals.Reset(); }

        internal static int Destination(LevelScreen[] screens, int source, bool left, float y = float.NaN)
        {
            int destination = MapTopology.Destination(screens, source, left, y);
            if (destination < 0) return -1;
            return OpenSide(screens,source,left) ? destination : -1;
        }

        private static Edges Read(LevelScreen[] screens,int index)
        {
            Edges edges;
            if(!cache.TryGetValue(screens[index],out edges)) {edges=Scan(screens[index],index);cache.Add(screens[index],edges);}
            return edges;
        }
        internal static bool OpenSide(LevelScreen[] screens,int index,bool left)
        { return (Read(screens,index).Sides & (left ? 1 : 2))!=0; }
        internal static bool Vertical(LevelScreen[] screens,int lower)
        {
            if(lower<0 || lower+1>=screens.Length || screens[lower]==null || screens[lower+1]==null) return false;
            ulong free=~(Read(screens,lower).Top|Read(screens,lower+1).Bottom)&((1UL<<60)-1);
            return (free & (free>>1))!=0;
        }

        private static Edges Scan(LevelScreen screen, int index)
        {
            ulong left = 0, right = 0;
            ulong upper=0,lower=0;
            int top = -index * 360;
            foreach (var block in JKRuntime.Geometry.NativeWorldGeometry.BlocksView(screen))
            {
                if (block == null) continue;
                Type type = block.GetType();
                // only audited solid shapes can prove that an edge is a wall
                // never call arbitrary mod collision callbacks while rendering
                bool box = type == typeof(BoxBlock) || type == typeof(IceBlock) || type == typeof(SnowBlock);
                bool slope = type == typeof(SlopeBlock);
                if (!box && !slope) continue;
                Rectangle bounds = block.GetRect();
                bool touchesLeft = bounds.Left <= 0 && bounds.Right > 0;
                bool touchesRight = bounds.Left <= 479 && bounds.Right > 479;
                bool touchesTop=bounds.Top<=top && bounds.Bottom>top;
                bool touchesBottom=bounds.Top<top+360 && bounds.Bottom>=top+360;
                if (!touchesLeft && !touchesRight && !touchesTop && !touchesBottom) continue;
                if (slope)
                {
                    var vertices = JKRuntime.Geometry.NativeWorldGeometry.ReadSlopeVertices((SlopeBlock)block);
                    if (touchesLeft) left |= SlopeEdge(vertices, .5f, top);
                    if (touchesRight) right |= SlopeEdge(vertices, 479.5f, top);
                    if (touchesTop) upper |= Boundary(vertices,top+.5f,0,false,60);
                    if (touchesBottom) lower |= Boundary(vertices,top+359.5f,0,false,60);
                    continue;
                }
                int first = Math.Max(0, (int)Math.Floor((bounds.Top - top) / 8.0));
                int last = Math.Min(44, (int)Math.Floor((bounds.Bottom - 1 - top) / 8.0));
                for (int row = first; row <= last; row++)
                {
                    if (touchesLeft) left |= 1UL << row;
                    if (touchesRight) right |= 1UL << row;
                }
                first=Math.Max(0,(int)Math.Floor(bounds.Left/8.0));
                last=Math.Min(59,(int)Math.Floor((bounds.Right-1)/8.0));
                for(int column=first;column<=last;column++) {
                    if(touchesTop) upper|=1UL<<column;
                    if(touchesBottom) lower|=1UL<<column;
                }
            }
            return new Edges {Sides=(byte)((HasGap(left) ? 1 : 0) | (HasGap(right) ? 2 : 0)),Top=upper,Bottom=lower};
        }

        private static bool HasGap(ulong blocked)
        {
            ulong free = ~blocked & ((1UL << 45) - 1);
            return (free & (free >> 1)) != 0;
        }

        private static ulong SlopeEdge(Vector2[] vertices, float x, int top)
        { return Boundary(vertices,x,top,true,45); }
        private static ulong Boundary(Vector2[] vertices,float plane,int origin,bool vertical,int count)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 a = vertices[i], b = vertices[(i + 1) % vertices.Length];
                float an=vertical ? a.X : a.Y,bn=vertical ? b.X : b.Y;
                float av=vertical ? a.Y : a.X,bv=vertical ? b.Y : b.X;
                if (plane < Math.Min(an,bn) || plane > Math.Max(an,bn)) continue;
                if (an==bn) { low=Math.Min(low,Math.Min(av,bv));high=Math.Max(high,Math.Max(av,bv)); }
                else
                {
                    float y=av+(bv-av)*(plane-an)/(bn-an);
                    low = Math.Min(low, y); high = Math.Max(high, y);
                }
            }
            ulong mask = 0;
            if (high <= low) return mask;
            for (int row = 0; row < count; row++)
                if (origin+row*8<high && origin+(row+1)*8>low) mask |= 1UL<<row;
            return mask;
        }
    }
}
