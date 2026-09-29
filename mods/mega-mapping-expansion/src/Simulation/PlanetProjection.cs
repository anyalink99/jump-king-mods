using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal static class PlanetProjection
    {
        internal static Vector2 UV(PlanetData p,Vector2 screen,out float coverage,out float facing)
        {
            float x=(screen.X-p.X)/p.Radius,y=-(screen.Y-p.Y)/p.Radius;
            float r2=x*x+y*y;
            coverage=MathHelper.Clamp((1-(float)Math.Sqrt(r2))*p.Radius/2,0,1);
            float z=(float)Math.Sqrt(Math.Max(0,1-r2));facing=z;
            float a=MathHelper.ToRadians(p.AxisTilt),b=MathHelper.ToRadians(p.ViewTilt);
            float xx=x*(float)Math.Cos(a)-y*(float)Math.Sin(a),yy=x*(float)Math.Sin(a)+y*(float)Math.Cos(a);
            float localY=yy*(float)Math.Cos(b)-z*(float)Math.Sin(b);
            float localZ=yy*(float)Math.Sin(b)+z*(float)Math.Cos(b);
            return new Vector2(.5f+(float)Math.Atan2(xx,localZ)/MathHelper.TwoPi+p.Longitude/360,
                .5f-(float)Math.Asin(MathHelper.Clamp(localY,-1,1))/(float)Math.PI);
        }
        internal static float Turn(float time,float period)
        {return (float)((time/(double)period)%1);}
    }
}
