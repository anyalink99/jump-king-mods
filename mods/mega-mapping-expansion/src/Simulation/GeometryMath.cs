using System;
using System.Collections.Generic;
using System.IO;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal static class GeometryMath
    {
        internal static SceneValue[] Points(string text)
        {
            var parts=(text??"").Split(';');if(parts.Length<2||parts.Length>128)throw new InvalidDataException("Geometry needs 2..128 vector points");
            var values=Array.ConvertAll(parts,p=>SceneValue.Parse(SceneValueKind.Vector2,p));return values;
        }
        internal static bool Contains(SceneValue[] polygon,double x,double y)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                var a=polygon[j];var b=polygon[i];double cross=(x-a.X)*(b.Y-a.Y)-(y-a.Y)*(b.X-a.X);
                if(Math.Abs(cross)<1e-8&&x>=Math.Min(a.X,b.X)&&x<=Math.Max(a.X,b.X)&&y>=Math.Min(a.Y,b.Y)&&y<=Math.Max(a.Y,b.Y))return true;
                if((a.Y>y)!=(b.Y>y)&&x<(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
            }
            return inside;
        }
        private static double Cross(SceneValue a,SceneValue b,SceneValue c){return(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);}
        internal static bool Overlaps(SceneValue[] a,SceneValue[] b,double padding=0)
        {
            if(Contains(a,b[0].X,b[0].Y)||Contains(b,a[0].X,a[0].Y))return true;
            for(int i=0;i<a.Length;i++)for(int j=0;j<b.Length;j++)
            {
                var p=a[i];var q=a[(i+1)%a.Length];var r=b[j];var s=b[(j+1)%b.Length];
                double pq1=Cross(p,q,r),pq2=Cross(p,q,s),rs1=Cross(r,s,p),rs2=Cross(r,s,q);
                if(pq1*pq2<=0&&rs1*rs2<=0&&Math.Max(Math.Min(p.X,q.X),Math.Min(r.X,s.X))<=Math.Min(Math.Max(p.X,q.X),Math.Max(r.X,s.X))&&Math.Max(Math.Min(p.Y,q.Y),Math.Min(r.Y,s.Y))<=Math.Min(Math.Max(p.Y,q.Y),Math.Max(r.Y,s.Y)))return true;
                if(padding>0&&(Distance(p,r,s)<=padding||Distance(q,r,s)<=padding||Distance(r,p,q)<=padding||Distance(s,p,q)<=padding))return true;
            }
            return false;
        }
        internal static double Distance(SceneValue point,SceneValue a,SceneValue b)
        {double dx=b.X-a.X,dy=b.Y-a.Y,length=dx*dx+dy*dy,t=length<1e-12?0:Math.Max(0,Math.Min(1,((point.X-a.X)*dx+(point.Y-a.Y)*dy)/length));double x=point.X-a.X-dx*t,y=point.Y-a.Y-dy*t;return Math.Sqrt(x*x+y*y);}
        internal static short[] Triangulate(SceneValue[] polygon)
        {
            if(polygon.Length<3)throw new InvalidDataException("Polygon needs at least three points");
            double area=0;for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];area+=a.X*b.Y-b.X*a.Y;}
            if(Math.Abs(area)<1e-8)throw new InvalidDataException("Polygon has no area");
            double sign=area>0?1:-1;var remaining=new List<int>();for(int i=0;i<polygon.Length;i++)remaining.Add(i);var output=new List<short>();
            while(remaining.Count>3)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int ai=remaining[(i+remaining.Count-1)%remaining.Count],bi=remaining[i],ci=remaining[(i+1)%remaining.Count];var a=polygon[ai];var b=polygon[bi];var c=polygon[ci];
                    if(Cross(a,b,c)*sign<=1e-8)continue;bool blocked=false;
                    foreach(int p in remaining)if(p!=ai&&p!=bi&&p!=ci&&Cross(a,b,polygon[p])*sign>=0&&Cross(b,c,polygon[p])*sign>=0&&Cross(c,a,polygon[p])*sign>=0){blocked=true;break;}
                    if(blocked)continue;output.Add((short)ai);output.Add((short)bi);output.Add((short)ci);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)throw new InvalidDataException("Polygon is self-intersecting or has duplicate/degenerate edges");
            }
            output.Add((short)remaining[0]);output.Add((short)remaining[1]);output.Add((short)remaining[2]);return output.ToArray();
        }
        internal static SceneValue Curve(SceneValue[] points,double phase)
        {
            double scaled=Math.Max(0,Math.Min(1,phase))*(points.Length-1);int i=Math.Min(points.Length-2,(int)scaled);double t=scaled-i;
            return SceneValue.Vector(points[i].X+(points[i+1].X-points[i].X)*t,points[i].Y+(points[i+1].Y-points[i].Y)*t);
        }
    }
}
