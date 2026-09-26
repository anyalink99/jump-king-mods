using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void WeatherChecks()
        {
            var polygon=SceneValidation.ParsePath("10,280;90,280;90,300;60,300;60,290;40,290;40,300;10,300","shore");
            var geometry=new PuddleGeometry(polygon);
            Require(geometry.Contains(20,295) && !geometry.Contains(50,295),"concave puddle keeps the dry inlet dry");
            bool clipped=true;
            foreach(Rectangle span in geometry.Spans)
                for(int x=span.X;x<span.Right;x++)if(!geometry.Contains(x+.5f,span.Y+.5f))clipped=false;
            Require(clipped,"every reflected scanline pixel lies inside its shoreline");
            Require(PuddleGeometry.ReflectedY(280,280,.4f)==280 && PuddleGeometry.ReflectedY(288,280,.4f)==260,
                "puddle projection mirrors and compresses height around the contact plane");
            Require(PuddleGeometry.ReflectedY(300,280,.4f,.004f)<PuddleGeometry.ReflectedY(300,280,.4f),
                "perspective brings taller distant reflections into a shallow roof plane");
            var scene=new SceneFile {
                Rains=new[]{new RainData{Id="rain",Screen=3,Collide=true}},
                Puddles=new[]{new PuddleData{Id="pool",Screen=3,PlaneY=280,Outline="10,280;90,280;90,300;10,300"}}
            };
            SceneValidation.Validate(scene,".",4);
            var serializer=new XmlSerializer(typeof(SceneFile));
            using(var text=new StringWriter())
            {
                serializer.Serialize(text,scene);
                using(var reader=new StringReader(text.ToString()))
                {
                    var restored=(SceneFile)serializer.Deserialize(reader);
                    Require(restored.Rains[0].Collide && restored.Puddles[0].PlaneY==280,
                        "weather authoring survives XML round trip without losing collision or projection");
                }
            }
            scene.Puddles[0].ReflectionScaleY=float.NaN;
            bool rejected=false;try{SceneValidation.Validate(scene,".",4);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"invalid puddle projection fails validation");
            scene.Puddles[0].ReflectionScaleY=.4f;scene.Rains[0].Count=513;
            rejected=false;try{SceneValidation.Validate(scene,".",4);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"unbounded rain batches fail validation");
            Require(new SceneFile().Rains.Length==0 && new SceneFile().Puddles.Length==0,
                "existing maps do not acquire weather effects by default");
        }
    }
}
