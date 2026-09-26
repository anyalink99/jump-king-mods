using System;
using System.IO;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void AuthoringChecks(string temporary)
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                bool finiteRejected = false;
                try { FiniteNumbers.Validate(new SceneFile { Lights = new[] { new LightData { Id = "invalid-light", Radius = invalid } } }, "scene.xml"); }
                catch (InvalidDataException error) { finiteRejected = error.Message.Contains("invalid-light") && error.Message.Contains("radius"); }
                Require(finiteRejected, "non-finite nested fields report object and XML attribute");
            }
            string root = Path.Combine(temporary, "authoring-contracts");
            string folder = Path.Combine(root, "props/mega-mapping-expansion");
            Directory.CreateDirectory(folder);
            string entry = Path.Combine(folder, "scene.xml");
            File.WriteAllText(Path.Combine(folder, "nodes.xml"), "<MegaMapping><Nodes><Node id='test' asset='a' template='t' group='g' material='m' x='12'/></Nodes></MegaMapping>");
            File.WriteAllText(entry, "<MegaMapping version='1'><Templates><Template id='t' x='3' y='4'><Track property='x' keys='0:0;1:3'/><Track property='y' keys='0:1;1:4'/></Template></Templates><Materials><Material id='m' opacity='.4'/></Materials><LayerGroups><LayerGroup id='g' layer='background' z='-2'/></LayerGroups><Include src='nodes.xml'/></MegaMapping>");
            SceneFile scene = SceneValidation.Read(root);
            Require(scene.Nodes[0].X == 12 && scene.Nodes[0].Y == 4 && scene.Nodes[0].Opacity == .4f && scene.Nodes[0].Layer == "background", "module/default expansion preserves instance overrides");
            Require(scene.Nodes[0].Tracks.Length == 2, "template retains the complete repeated track collection");
            string before = CompiledSceneCache.SourceHash(root, scene);
            File.WriteAllText(Path.Combine(folder, "nodes.xml"), "<MegaMapping><Nodes><Node id='test' asset='a' x='13'/></Nodes></MegaMapping>");
            Require(before != CompiledSceneCache.SourceHash(root, scene), "included module changes invalidate cache");
            File.WriteAllText(entry, "<MegaMapping><Include src='scene.xml'/></MegaMapping>");
            bool rejected = false;
            try { SceneValidation.Read(root); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "cyclic include rejected");
            File.WriteAllText(entry, "<MegaMapping><Options timre='hidden'/></MegaMapping>");
            rejected = false;
            try { SceneValidation.Read(root); } catch (InvalidOperationException error) { rejected = error.ToString().Contains("timre"); }
            Require(rejected, "unknown attribute reports its spelling");
            scene = new SceneFile { VectorAssets = new[] { new VectorAssetData { Id = "a", ClipPath = "M0 0 L1 0 L1 1 Z" } } };
            rejected = false;
            try { SceneValidation.Validate(scene, root, 1); } catch (InvalidDataException error) { rejected = error.Message.Contains("clipMode"); }
            Require(rejected, "clipPath without a mode is actionable rather than silently ignored");
        }
    }
}
