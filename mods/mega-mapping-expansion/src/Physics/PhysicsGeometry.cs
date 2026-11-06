using System;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal struct PhysicsBox
    {
        internal long Left,Top,Right,Bottom;
        internal PhysicsBox(long x,long y,int width,int height){Left=x;Top=y;Right=x+width*PhysicsCoordinates.Unit;Bottom=y+height*PhysicsCoordinates.Unit;}
        internal PhysicsBox(Rectangle box):this(box.X*PhysicsCoordinates.Unit,box.Y*PhysicsCoordinates.Unit,box.Width,box.Height){}
        internal Rectangle Cover{get{return new Rectangle(Floor(Left),Floor(Top),Ceiling(Right)-Floor(Left),Ceiling(Bottom)-Floor(Top));}}
        internal static int Floor(long value){return checked((int)Math.Floor((double)value/PhysicsCoordinates.Unit));}
        internal static int Ceiling(long value){return checked((int)Math.Ceiling((double)value/PhysicsCoordinates.Unit));}
    }
    internal static class PhysicsGeometry
    {
        // strict interior overlap makes edge contact identical in both directions
        internal static bool Intersects(PhysicsBox body,Rectangle rect,out Rectangle overlap)
        {
            long unit=PhysicsCoordinates.Unit;
            long l=Math.Max(body.Left,(long)rect.Left*unit)-(long)rect.Left*unit;
            long r=Math.Min(body.Right,(long)rect.Right*unit)-(long)rect.Left*unit;
            long t=Math.Max(body.Top,(long)rect.Top*unit)-(long)rect.Top*unit;
            long b=Math.Min(body.Bottom,(long)rect.Bottom*unit)-(long)rect.Top*unit;
            overlap=Rectangle.Empty;
            if(l>=r||t>=b)return false;
            overlap=Rectangle.Intersect(body.Cover,rect);
            return true;
        }
    }
}
