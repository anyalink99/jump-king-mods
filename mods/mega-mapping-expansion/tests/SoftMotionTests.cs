using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void SoftMotionChecks()
        {
            var serializer = new XmlSerializer(typeof(PropData));
            PropData prop;
            using (var reader = new StringReader("<PropData id='mist' asset='mist' sampling='linear' />"))
                prop = (PropData)serializer.Deserialize(reader);
            Require(new PreparedProp(prop).LinearSampling, "soft sampling survives XML loading and preparation");
            Require(!new PreparedProp(new PropData()).LinearSampling, "existing pixel art keeps point sampling by default");
            var scene = new SceneFile {
                VectorAssets = new[] { new VectorAssetData { Id = "mist" } },
                Nodes = new[] { prop }
            };
            SceneValidation.Validate(scene, ".", 1);
            prop.Sampling = "unknown";
            bool rejected = false;
            try { SceneValidation.Validate(scene, ".", 1); }
            catch (InvalidDataException error) { rejected = error.Message.Contains("sampling"); }
            Require(rejected, "unknown sampling mode fails authoring validation");

            foreach (float duration in new[] { 39f, 47f, 53f })
            {
                float dt = 1f / 60f;
                Vector2 before = SceneAnimation.OrbitPosition(SceneAnimation.Phase(duration-dt,duration,"loop"),8,1);
                Vector2 seam = SceneAnimation.OrbitPosition(SceneAnimation.Phase(duration,duration,"loop"),8,1);
                Vector2 after = SceneAnimation.OrbitPosition(SceneAnimation.Phase(duration+dt,duration,"loop"),8,1);
                Require(Vector2.Distance(before,after) < .01f, "slow drift has no position jump at the loop seam");
                Require(Vector2.Distance((seam-before)/dt,(after-seam)/dt) < .005f,
                    "slow drift velocity remains continuous across the loop seam");
            }
        }
    }
}
