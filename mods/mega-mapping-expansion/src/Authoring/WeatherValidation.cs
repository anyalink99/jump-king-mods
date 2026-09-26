using System;
using System.Collections.Generic;
using System.IO;

namespace MegaMappingExpansion
{
    internal static partial class SceneValidation
    {
        private static int ValidateWeather(SceneFile scene, int screens, HashSet<string> ids)
        {
            foreach (RainData rain in scene.Rains)
            {
                NeedId(rain.Id,ids,"rain"); Screen(rain.Screen,screens,rain.Id);
                ValidateChoice(rain.Layer,rain.Id+".layer","background","world","foreground");
                Unit(rain.Opacity,rain.Id+".opacity"); ParseColor(rain.Color,rain.Id+".color");
                Range(rain.Speed,1,900,rain.Id+".speed"); Range(rain.Wind,-300,300,rain.Id+".wind");
                Range(rain.Length,.5f,32,rain.Id+".length");
                Range(rain.Drift,0,80,rain.Id+".drift");
                if(rain.Count<1 || rain.Count>512) throw new InvalidDataException("Rain count must be 1..512: "+rain.Id);
            }
            foreach (PuddleData puddle in scene.Puddles)
            {
                NeedId(puddle.Id,ids,"puddle"); Screen(puddle.Screen,screens,puddle.Id);
                Unit(puddle.Opacity,puddle.Id+".opacity"); ParseColor(puddle.Color,puddle.Id+".color");
                Range(puddle.PlaneY,0,359,puddle.Id+".planeY");
                Range(puddle.ReflectionScaleY,.08f,1,puddle.Id+".reflectionScaleY");
                Range(puddle.Perspective,0,.02f,puddle.Id+".perspective");
                Range(puddle.Ripple,0,4,puddle.Id+".ripple");
                if(puddle.RainRings<0 || puddle.RainRings>64) throw new InvalidDataException("Rain rings must be 0..64: "+puddle.Id);
                var outline=ParsePath(puddle.Outline,puddle.Id+".outline");
                if(outline.Length<3 || outline.Length>64) throw new InvalidDataException("Puddle outline needs 3..64 vertices: "+puddle.Id);
                foreach(var point in outline)
                {
                    Range(point.X,0,480,puddle.Id+".outline.x");
                    Range(point.Y,puddle.PlaneY,360,puddle.Id+".outline.y");
                }
                if(new PuddleGeometry(outline).Spans.Length==0) throw new InvalidDataException("Empty puddle outline: "+puddle.Id);
            }
            foreach(var surf in scene.Surfs)
            {
                NeedId(surf.Id,ids,"surf");Screen(surf.Screen,screens,surf.Id);
                ValidateChoice(surf.Layer,surf.Id+".layer","background","world","foreground");
                Range(surf.X,-960,960,surf.Id+".x");Range(surf.Y,-360,720,surf.Id+".y");
                Range(surf.Width,1,1920,surf.Id+".width");Range(surf.Height,0,180,surf.Id+".height");
                Range(surf.Depth,1,720,surf.Id+".depth");Range(surf.Period,.5f,300,surf.Id+".period");
                Range(surf.Wavelength,20,1000,surf.Id+".wavelength");Range(surf.Phase,0,1,surf.Id+".phase");
                Range(surf.Break,0,1.6f,surf.Id+".break");Range(surf.Wind,-300,300,surf.Id+".wind");
                Unit(surf.Variation,surf.Id+".variation");
                Range(surf.Chop,0,12,surf.Id+".chop");Unit(surf.FoamDetail,surf.Id+".foamDetail");
                if(surf.Impacts.Length>16)throw new InvalidDataException("Surf supports at most 16 rock contacts: "+surf.Id);
                foreach(var contact in surf.Impacts)
                {
                    Range(contact.ArrivalPhase,-1,1,surf.Id+".impact.arrivalPhase");
                    Range(contact.X,-480,960,surf.Id+".impact.x");Range(contact.Y,-360,720,surf.Id+".impact.y");
                    Range(contact.Angle,-360,360,surf.Id+".impact.angle");Range(contact.Reach,1,200,surf.Id+".impact.reach");
                    Range(contact.Width,1,120,surf.Id+".impact.width");Range(contact.Z,-10000,10000,surf.Id+".impact.z");
                    ValidateChoice(contact.Layer,surf.Id+".impact.layer","background","world","foreground");
                    if(contact.Particles<0||contact.Particles>256)throw new InvalidDataException("Impact particles must be 0..256: "+surf.Id);
                }
                Range(surf.Z,-10000,10000,surf.Id+".z");
                if(surf.Spray<0||surf.Spray>512)throw new InvalidDataException("Surf spray must be 0..512: "+surf.Id);
                ParseColor(surf.Color,surf.Id+".color");ParseColor(surf.CrestColor,surf.Id+".crestColor");
            }
            return scene.Rains.Length+scene.Puddles.Length+scene.Surfs.Length;
        }
        private static void Range(float value,float min,float max,string name)
        { if(float.IsNaN(value) || value<min || value>max) throw new InvalidDataException(name+" must be "+min+".."+max); }
    }
}
