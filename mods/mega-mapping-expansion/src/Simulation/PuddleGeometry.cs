using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Scan conversion is performed once at scene preparation. Even/odd spans
    // retain concave shores and keep reflected pixels inside their actual pool.
    internal sealed class PuddleGeometry
    {
        internal readonly Rectangle[] Spans;
        private readonly Vector2[] points;
        internal PuddleGeometry(Vector2[] outline)
        {
            points=outline;
            var spans=new List<Rectangle>();
            var crossings=new List<float>();
            for(int y=0;y<360;y++)
            {
                crossings.Clear(); float sy=y+.5f;
                for(int i=0,j=points.Length-1;i<points.Length;j=i++)
                {
                    Vector2 a=points[j],b=points[i];
                    if((a.Y<=sy && b.Y>sy)||(b.Y<=sy && a.Y>sy))
                        crossings.Add(a.X+(sy-a.Y)*(b.X-a.X)/(b.Y-a.Y));
                }
                crossings.Sort();
                for(int i=0;i+1<crossings.Count;i+=2)
                {
                    int left=Math.Max(0,(int)Math.Ceiling(crossings[i]-.5f));
                    int right=Math.Min(480,(int)Math.Ceiling(crossings[i+1]-.5f));
                    if(right>left) spans.Add(new Rectangle(left,y,right-left,1));
                }
            }
            Spans=spans.ToArray();
        }
        internal bool Contains(float x,float y)
        {
            bool inside=false;
            for(int i=0,j=points.Length-1;i<points.Length;j=i++)
            {
                Vector2 a=points[j],b=points[i];
                if((a.Y>y)!=(b.Y>y) && x<(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
            }
            return inside;
        }
        internal static float ReflectedY(float y,float plane,float scale,float perspective=0)
        {
            float depth=y-plane;
            // Inverse of d = scale*h/(1 + perspective*h). Tall distant
            // reflections compress more than the King's nearby contact image.
            float divisor=scale-perspective*depth;
            return divisor<=0 ? -1 : plane-depth/divisor;
        }
    }
}
