using System;
using Microsoft.Xna.Framework;
namespace MegaMappingExpansion
{
    // Deep bands decay towards the undisturbed body. Gentle swells use an
    // elliptical parcel orbit; breakers use an asymmetric, open-lipped profile.
    // An oversteep trochoid self-intersects into a closed bubble, which is not a
    // convincing breaking crest. The authored cubic profile avoids that loop.
    internal static class SurfMotion
    {
        internal static float Travel(SurfData surf,float seconds)
        {return seconds/surf.Period+surf.Variation*(.17f*(float)Math.Sin(seconds/(surf.Period*2.73f))+.09f*(float)Math.Sin(seconds/(surf.Period*1.17f)));}
        internal static float Phase(SurfData surf,float coordinate,float seconds)
        {return coordinate/surf.Wavelength-Travel(surf,seconds)+surf.Phase;}
        internal static float Impact(SurfData surf,SurfImpactData contact,float seconds)
        {
            float phase=(contact.ArrivalPhase>=0 ? seconds/surf.Period+contact.ArrivalPhase : Phase(surf,contact.X-surf.X,seconds))*MathHelper.TwoPi;
            return (float)Math.Pow(Math.Max(0,Math.Cos(phase)),10);
        }
        internal static Vector2 Point(SurfData surf,float coordinate,float depth,float seconds)
        {
            double phase=Phase(surf,coordinate,seconds)*Math.PI*2;
            float decay=(float)Math.Exp(-depth*3.2f),crest=(float)Math.Cos(phase);
            // The envelope is spatial, not half-frequency temporal, so the period
            // is identical for geometry and spray across every loop boundary.
            float height=surf.Height*(.83f+.17f*(float)Math.Sin(coordinate*.013));
            height*=1+surf.Variation*(.19f*(float)Math.Sin(seconds/(surf.Period*1.71f)+coordinate*.004f)+.09f*(float)Math.Sin(seconds/(surf.Period*.63f)-coordinate*.009f));
            float chop=surf.Chop*(.63f*(float)Math.Sin(coordinate*.083f-seconds*1.71f)+.37f*(float)Math.Sin(coordinate*.173f+seconds*2.13f));
            if(surf.Break>1)
            {
                float cycle=Phase(surf,coordinate,seconds);
                float local=cycle-(float)Math.Floor(cycle+.5f);
                float steepness=surf.Break+surf.Variation*.16f*(float)Math.Sin(seconds*.47f+coordinate*.005f);
                float spill=surf.Variation*(.5f+.5f*(float)Math.Sin(seconds/(surf.Period*.71f)+coordinate*.004f));
                height*=1-spill*.3f;
                Vector2 profile=Breaker(local+.5f,steepness,spill);
                return new Vector2(surf.X+coordinate+(profile.X-local)*surf.Wavelength*decay,
                    surf.Y+depth*surf.Depth+(profile.Y*height+chop)*decay);
            }
            return new Vector2(surf.X+coordinate-(float)Math.Sin(phase)*height*surf.Break*decay,
                surf.Y+depth*surf.Depth+(-crest*height+chop)*decay);
        }
        private static Vector2 Cubic(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        {float u=1-t;return a*(u*u*u)+b*(3*u*u*t)+c*(3*u*t*t)+d*(t*t*t);}
        private static Vector2 Breaker(float t,float steepness,float spill)
        {
            float curl=.06f+(steepness-1)*.08f+spill*.09f;
            float shoulder=-.04f-spill*.045f,lip=-.72f+spill*.26f;
            // Control distances include each segment's parameter duration; joins
            // and the period seam therefore agree in velocity, not just position.
            if(t<.45f)return Cubic(new Vector2(-.5f,.05f),new Vector2(-.35f,.05f),new Vector2(shoulder-.135f,-1),new Vector2(shoulder,-1),t/.45f);
            if(t<.6f)return Cubic(new Vector2(shoulder,-1),new Vector2(shoulder+.045f,-1),new Vector2(curl+.05f,lip),new Vector2(curl,lip),(t-.45f)/.15f);
            if(t<.7f)return Cubic(new Vector2(curl,lip),new Vector2(curl-.1f/3,lip),new Vector2(.09f,-.18f-.1f/3),new Vector2(.11f,-.18f),(t-.6f)/.1f);
            return Cubic(new Vector2(.11f,-.18f),new Vector2(.17f,-.08f),new Vector2(.4f,.05f),new Vector2(.5f,.05f),(t-.7f)/.3f);
        }
        internal static float CrestCoordinate(SurfData surf,float seconds,int index)
        {return (Travel(surf,seconds)-surf.Phase+index)*surf.Wavelength;}
    }
}
