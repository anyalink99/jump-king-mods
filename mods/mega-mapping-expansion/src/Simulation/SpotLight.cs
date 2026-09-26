using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal static class SpotLight
    {
        internal static Vector2 Direction(LightData light,float seconds)
        {
            float angle=light.Angle+(light.SweepPeriod>0?light.SweepAngle*(float)Math.Sin(seconds/light.SweepPeriod*Math.PI*2):0);
            double rad=angle*Math.PI/180;return new Vector2((float)Math.Cos(rad),(float)Math.Sin(rad));
        }
        internal static float Angular(Vector2 receiverDirection,Vector2 beamDirection,float width)
        {
            if(width<=0)return 1;
            float outer=(float)Math.Cos(width*Math.PI/360),inner=(float)Math.Cos(width*Math.PI/480);
            return Angular(receiverDirection,beamDirection,outer,1/Math.Max(.000001f,inner-outer));
        }
        internal static float Angular(Vector2 receiverDirection,Vector2 beamDirection,float outer,float inverseFeather)
        {
            float t=MathHelper.Clamp((Vector2.Dot(receiverDirection,beamDirection)-outer)*inverseFeather,0,1);
            return t*t*(3-2*t);
        }
    }
}
