using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    internal static class ClothMotion
    {
        internal static Vector2 Bend(Vector2 point,Vector2 pin,float height,float strength,float age,float duration)
        {
            float t=MathHelper.Clamp((point.Y-pin.Y)/Math.Max(1,height),0,1),phase=age/Math.Max(.01f,duration)*MathHelper.TwoPi;
            float x=point.X-pin.X;
            float swing=(float)Math.Sin(phase-t*1.7f)*t*t;
            float fold=(float)Math.Sin(x*.21f-phase*2+t*3)*t*.35f;
            return point+new Vector2((swing+fold)*strength,(float)Math.Cos(x*.17f+phase-t)*t*t*strength*.3f);
        }
    }
}
