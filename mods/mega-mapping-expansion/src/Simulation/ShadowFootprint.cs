using System;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Conservative row bounds for a soft-source shadow. Exact visibility is
    // still evaluated inside these bounds; this only rejects untouched pixels.
    internal sealed class ShadowFootprint
    {
        internal readonly int[] Left,Right;
        private readonly int width,height;
        private readonly Vector2[] points=new Vector2[8],hull=new Vector2[16];
        internal ShadowFootprint(int width,int height)
        {this.width=width;this.height=height;Left=new int[height];Right=new int[height];Clear();}
        private void Clear(){for(int y=0;y<height;y++){Left[y]=width;Right[y]=-1;}}
        private static float Cross(Vector2 a,Vector2 b,Vector2 c)
        {return (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);}
        internal void Build(Vector2 source,Rectangle blocker,float radius,float scaleX,float scaleY)
        {
            Clear();if(blocker.Width<=0||blocker.Height<=0)return;
            blocker.Inflate(3,3);
            if(blocker.Contains(source))
            {for(int y=0;y<height;y++){Left[y]=0;Right[y]=width-1;}return;}
            Vector2 nearest=new Vector2(MathHelper.Clamp(source.X,blocker.Left,blocker.Right),MathHelper.Clamp(source.Y,blocker.Top,blocker.Bottom));
            float distance=Vector2.Distance(source,nearest);
            if(distance>radius+4)return;
            float extend=1+(radius+width*scaleX+height*scaleY)/Math.Max(.01f,distance);
            points[0]=new Vector2(blocker.Left,blocker.Top);points[1]=new Vector2(blocker.Right,blocker.Top);
            points[2]=new Vector2(blocker.Right,blocker.Bottom);points[3]=new Vector2(blocker.Left,blocker.Bottom);
            for(int i=0;i<4;i++)points[i+4]=source+(points[i]-source)*extend;
            // Eight points: an allocation-free insertion sort is sufficient.
            for(int i=1;i<8;i++)
            {
                Vector2 p=points[i];int j=i-1;
                while(j>=0&&(points[j].X>p.X||(points[j].X==p.X&&points[j].Y>p.Y)))
                {points[j+1]=points[j];j--;}
                points[j+1]=p;
            }
            int n=0;
            for(int i=0;i<8;i++){while(n>=2&&Cross(hull[n-2],hull[n-1],points[i])<=0)n--;hull[n++]=points[i];}
            int lower=n+1;
            for(int i=6;i>=0;i--){while(n>=lower&&Cross(hull[n-2],hull[n-1],points[i])<=0)n--;hull[n++]=points[i];}
            n--;
            for(int y=0;y<height;y++)
            {
                float yy=(y+.5f)*scaleY,min=float.MaxValue,max=float.MinValue;
                for(int i=0;i<n;i++)
                {
                    Vector2 a=hull[i],b=hull[(i+1)%n];
                    if((a.Y<=yy&&b.Y>yy)||(b.Y<=yy&&a.Y>yy))
                    {float x=a.X+(yy-a.Y)*(b.X-a.X)/(b.Y-a.Y);min=Math.Min(min,x);max=Math.Max(max,x);}
                }
                if(min<=max){Left[y]=Math.Max(0,(int)Math.Floor(min/scaleX-.5f)-1);Right[y]=Math.Min(width-1,(int)Math.Ceiling(max/scaleX-.5f)+1);}
            }
        }
    }
}
