using System;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal struct ProjectedPoint
    {
        internal double U,V,Coverage,Facing,NormalX,NormalY;
    }
    internal static class ProjectionMath
    {
        internal static ProjectedPoint Project(string projection,double u,double v,SceneValue tilt,bool disc)
        {
            double x=u*2-1,y=1-v*2,r2=x*x+y*y;
            var result=new ProjectedPoint { U=u,V=v,Coverage=disc&&r2>1?0:1,Facing=1 };
            if(projection=="spherical")
            {
                double z=Math.Sqrt(Math.Max(0,1-r2));result.Facing=z;result.NormalX=x;result.NormalY=y;
                double a=tilt.X*Math.PI/180,b=tilt.Y*Math.PI/180,xx=x*Math.Cos(a)-y*Math.Sin(a),yy=x*Math.Sin(a)+y*Math.Cos(a);
                double localY=yy*Math.Cos(b)-z*Math.Sin(b),localZ=yy*Math.Sin(b)+z*Math.Cos(b);
                result.U=.5+Math.Atan2(xx,localZ)/(Math.PI*2);result.V=.5-Math.Asin(Math.Max(-1,Math.Min(1,localY)))/Math.PI;
            }
            else if(projection=="cylindrical")result.U=.5+Math.Atan2(x,Math.Sqrt(Math.Max(0,1-x*x)))/(Math.PI*2);
            else if(projection=="perspective")
            {
                double denominator=1+Math.Max(-.95,tilt.Y*.01)*v;
                result.U=.5+(u-.5)/denominator;result.V=v/denominator;
            }
            return result;
        }
    }
}
