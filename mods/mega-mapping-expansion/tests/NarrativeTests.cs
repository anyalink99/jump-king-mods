using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void NarrativeChecks()
        {
            var xml = new XmlDocument();
            xml.LoadXml("<MegaMapping><ObjectDefinitions><Object id='lamp'><Parameters><Param name='power' type='number' default='1' min='0' max='4'/></Parameters><Lights><Light id='bulb' x='2' y='3' intensity='${power}'/></Lights><Rules><Rule id='switch' event='start' effect='@on'/></Rules><Effects><Effect id='on'><Set target='@bulb' property='intensity' value='2'/></Effect></Effects></Object></ObjectDefinitions><Objects><Instance id='a' object='lamp' x='10' y='20' screen='2'/><Instance id='b' object='lamp' x='90'><Arg name='power' value='3'/></Instance></Objects></MegaMapping>");
            SceneObjects.Expand(xml, new Dictionary<string, string>());
            Require(xml.SelectSingleNode("//Light[@id='a.bulb']/@x").Value == "12" && xml.SelectSingleNode("//Light[@id='a.bulb']/@screen").Value == "2", "Object assembly translates local components onto its screen");
            Require(xml.SelectSingleNode("//Light[@id='b.bulb']/@intensity").Value == "3" && xml.SelectSingleNode("//Effect[@id='b.on']/Set/@target").Value == "b.bulb", "Instances own independent parameters and effect references");
            xml.LoadXml("<MegaMapping><ObjectDefinitions><Object id='weather'><Rains><Rain id='rain'/></Rains><Surfs><Surf id='wave'><Impact x='2' y='3'/></Surf></Surfs><Puddles><Puddle id='water' planeY='20' outline='0,20;10,20;10,30' reflectionObjects='player;@shape'/></Puddles><Rules><Rule id='local' event='enter:@region'/></Rules></Object></ObjectDefinitions><Objects><Instance id='room' object='weather' x='10' y='20'/></Objects></MegaMapping>");
            SceneObjects.Expand(xml, new Dictionary<string, string>());
            Require(xml.SelectSingleNode("//Rain/@x") == null && xml.SelectSingleNode("//Surf/@y").Value == "260" && xml.SelectSingleNode("//Impact/@y").Value == "23", "Object placement preserves screen-wide rain and translates component defaults and impact points");
            Require(xml.SelectSingleNode("//Puddle/@planeY").Value == "40" && xml.SelectSingleNode("//Puddle/@reflectionObjects").Value == "player;room.shape" && xml.SelectSingleNode("//Rule/@event").Value == "enter:room.region", "Object-local outlines, reflection selection and events expand consistently");
            var scene = new SceneFile { Flags = new[] { new FlagData { Id = "secrets", Type = "integer", Value = "0", Scope = "save" } },
                Strings = new[] { new MapString { Id = "found", Value = "Found: {flag:secrets}" }, new MapString { Id = "localized", Translations = new[] { new Translation { Culture = "en-US", Value = "Hello" }, new Translation { Culture = "ru-RU", Value = "Привет" } } } },
                Rules = new[] { new RuleData { Id = "discover", Event = "secret", Increment = "secrets", Amount = 1, Once = true } } };
            scene.Options.SaveId = "fixture";
            NarrativeValidation.Validate(scene, 1);
            var texts = new SceneTextService(scene, "ru-RU");
            Require(texts.Initial("localized") == "Привет", "Translations select the exact declared culture");
            bool rejected = false; try { new SceneTextService(scene, "de-DE"); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Missing translation is not silently substituted");
            using (var engine = new SceneBehaviorEngine(scene, null))
            {
                engine.Emit("secret"); engine.Tick(.01, true, true, At(0, 0, 1));
                Require(texts.Resolve("found", engine.GetFlag) == "Found: 1", "Rules update typed counters used by text");
                object snapshot = engine.Capture(); engine.SetFlag("secrets", "5"); engine.Restore(snapshot);
                engine.Emit("secret"); engine.Tick(.01, true, true, At(0, 0, 1));
                Require(engine.GetFlag("secrets") == "1", "Counter snapshots preserve once-rule ownership");
                rejected = false; try { engine.SetFlag("secrets", "bad"); } catch (InvalidDataException) { rejected = true; }
                Require(rejected && engine.GetFlag("secrets") == "1", "Invalid counter assignment does not mutate state");
            }
            Require(SceneTextService.Wrap("alpha beta\nabcdef", 5, s => s.Length).SequenceEqual(new[] { "alpha", "beta", "abcde", "f" }), "Text wraps words and long tokens while preserving explicit line breaks");
            var overflow = new SceneFile { Flags = new[] { new FlagData { Id = "count", Type = "integer", Value = int.MaxValue.ToString() } },
                Rules = new[] { new RuleData { Id = "overflow", Event = "count", Increment = "count", Amount = 1, Effect = "dark" } },
                Effects = new[] { new Api.EffectDefinition { Id = "dark", Changes = new[] { new Api.SceneChange { Target = "options", Property = "ambientIntensity", Value = "0" } } } } };
            using (var engine = new SceneBehaviorEngine(overflow, null)) { engine.Emit("count"); engine.Tick(.01, true, true, At(0, 0, 1)); Require(overflow.Options.AmbientIntensity == 1 && engine.GetFlag("count") == int.MaxValue.ToString(), "Counter overflow cannot partially apply the rule effect"); }
        }
    }
}
