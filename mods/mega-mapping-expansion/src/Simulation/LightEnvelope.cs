using System;
namespace MegaMappingExpansion
{
    internal static class LightEnvelope
    {
        internal static float Rim(float distance,LightData light)
        {
            float radius=light.RimRadius>0?light.RimRadius:Math.Min(light.Radius,96);
            if(light.RimIntensity<=0 || distance>=radius)return 0;
            return LightVisibility.Attenuation(distance,radius,Math.Max(1.5f,light.Falloff))*light.RimIntensity;
        }
        internal static float Pulse(float time,float period,float amount,float phase)
        {
            if(period<=0 || amount<=0)return 1;
            return 1-amount*(.5f+.5f*(float)Math.Cos((time/period+phase)*Math.PI*2));
        }
    }
}
