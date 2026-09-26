using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using BehaviorTree;
using JumpKing;
using JumpKing.MiscEntities.WorldItems;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion.Endings
{
    // Files are parsed and checked during preparation, never from a draw or BT tick.
    // This is the existing MoreEndingOptions vocabulary, not a new scripting language.
    internal static class EndingDocument
    {
        private static readonly HashSet<string> Composites = new HashSet<string>(new[] {
            "RestartSequencer", "Selector", "SequenceOnce", "Sequencer", "Simultaneous", "RandomSelector", "RunAllAnySuccess" });
        private static readonly HashSet<string> Empty = new HashSet<string>(new[] {
            "CherubsDeliver", "CherubsEscape", "PlayMusic", "EndNode", "SuicideNode", "JumpUp", "FallDown",
            "Jump", "IdleAnim", "PutOnCrown", "CherubsDeliverAnim", "CherubsEscapeAnim", "BabeJump", "GiveCrownNBP", "IsBirdDone", "SpawnLightning" });
        internal static XElement Read(string path, string role)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 }))
            {
                XElement root = XElement.Load(reader, LoadOptions.SetLineInfo);
                Validate(root, role); return root;
            }
        }
        internal static void Validate(XElement root, string role)
        {
            foreach (XElement element in root.DescendantsAndSelf())
            {
                if (element.Name.NamespaceName != "" || element.HasAttributes) Fail(element, "attributes/namespaces are not part of this format");
                if (element.HasElements && element.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) Fail(element, "unexpected text between child elements");
                if (!element.HasElements) element.Value = element.Value.Trim();
            }
            int count = 0; Visit(root, role, 0, ref count);
        }
        private static void Visit(XElement node, string role, int depth, ref int count)
        {
            if (++count > 4096 || depth > 64) Fail(node, "tree exceeds 4096 nodes or 64 levels");
            if (node.Name.NamespaceName != "" || node.Attributes().Any(a => !a.IsNamespaceDeclaration)) Fail(node, "node attributes/namespaces are not part of this format");
            string name = node.Name.LocalName;
            if (Composites.Contains(name))
            {
                if (!node.Elements().Any()) Fail(node, "composite requires a child");
                foreach (XElement child in node.Elements()) Visit(child, role, depth + 1, ref count);
                return;
            }
            bool actor = !role.EndsWith("_ending", StringComparison.Ordinal);
            if (!actor && !new[] { "Evaluator", "StaticNode", "EndingScroll", "PauseNode", "BroadcastNode", "ReceiverWaitNode", "PlaySFX", "PlayMusic", "EndNode", "PlayEventSFX", "SpawnLightning", "EmitSceneEvent", "SetSceneFlag", "CheckSceneFlag", "WaitSceneFlag" }.Contains(name))
                Fail(node, "requires an actor file, not an ending controller");
            if (name == "IsBirdDone" && role != "owl_bird") Fail(node, "IsBirdDone requires owl_bird");
            if ((name == "CherubsDeliver" || name == "CherubsEscape") && role.StartsWith("owl_")) Fail(node, "the owl ending has no cherubs");
            if (Empty.Contains(name)) { Fields(node); ScalarEmpty(node); return; }
            switch (name)
            {
                case "EmitSceneEvent": BehaviorTreeCompiler.Key(Text(node), name); break;
                case "SetSceneFlag": case "CheckSceneFlag": case "WaitSceneFlag":
                    Fields(node, "Key", "Value"); BehaviorTreeCompiler.Key(Text(node.Element("Key")), name); Text(node.Element("Value")); break;
                case "Evaluator":
                    Fields(node, "Condition", "Func");
                    foreach (string field in new[] { "Condition", "Func" })
                    { XElement wrapper = node.Element(field); if (wrapper.Elements().Count() != 1) Fail(wrapper, "requires exactly one BT node"); Visit(wrapper.Elements().Single(), role, depth + 1, ref count); }
                    break;
                case "StaticNode":
                    var children = node.Elements().Where(e => e.Name != "Result").ToArray();
                    if (node.Elements("Result").Count() != 1 || children.Length != 1) Fail(node, "requires Result and exactly one BT node");
                    Choice<BTresult>(Text(node.Element("Result"))); Visit(children[0], role, depth + 1, ref count); break;
                case "PauseNode": Number(node, 0, 86400); break;
                case "SetSpriteNode": case "GetSpriteNode": ValidateSprite(Text(node)); break;
                case "SetSpriteEffectNode": Choice<SpriteEffects>(Text(node)); break;
                case "BroadcastNode": case "ReceiverWaitNode": case "PlayEventSFX": Text(node); break;
                case "PlaySFX": ValidateSound(Text(node)); break;
                case "GiveWearableItemNode":
                    string item = Text(node); Choice<Items>(item);
                    if (!new[] { "Crown", "Shoes", "CrownNBP", "SnakeRing", "GiantBoots", "Cap", "GnomeHat", "Tunic", "YellowShoes", "CrownOwl", "CapeOwl" }.Contains(item)) Fail(node, "item is not wearable");
                    break;
                case "EndingScroll": case "MoveNode":
                    string vector = name == "EndingScroll" ? "Step" : "DeltaMove";
                    Fields(node, vector, "Repetitions"); Integer(node.Element("Repetitions"), 0, 1000000);
                    Fields(node.Element(vector), "X", "Y"); Number(node.Element(vector).Element("X"), -1000000, 1000000); Number(node.Element(vector).Element("Y"), -1000000, 1000000); break;
                case "CoupleAnim":
                    FieldsOptional(node, new[] { "WalkOne", "WalkTwo", "WalkSmear" }, "Speed");
                    foreach (string field in new[] { "WalkOne", "WalkTwo", "WalkSmear" }) ValidateSprite(Text(node.Element(field)));
                    if (node.Element("Speed") != null) Integer(node.Element("Speed"), 1, 1000000); break;
                case "CheckBBKey": case "SetBBKeyNode":
                    Fields(node, "Key", "Value"); Text(node.Element("Key")); Integer(node.Element("Value"), int.MinValue, int.MaxValue); break;
                case "LoopingAnimNode":
                    Fields(node, "Step", "Sprites"); Number(node.Element("Step"), .000001, 86400);
                    XElement sprites = node.Element("Sprites");
                    if (!sprites.Elements().Any() || sprites.Elements().Count() > 1024 || sprites.Elements().Any(e => e.Name != "Sprite")) Fail(sprites, "requires 1..1024 Sprite values");
                    foreach (XElement sprite in sprites.Elements()) ValidateSprite(Text(sprite)); break;
                case "JumpToTargetHeight":
                    Fields(node, "Velocity", "TargetMove"); Number(node.Element("Velocity"), -1000000, 1000000); Number(node.Element("TargetMove"), -1000000, 1000000); break;
                case "JumpInPlace": Fields(node, "Speed"); Integer(node.Element("Speed"), -1000000, 1000000); break;
                default: Fail(node, "unknown behavior-tree node"); break;
            }
        }
        internal static void ValidateScene(IEnumerable<XElement> documents, SceneFile scene)
        {
            foreach (XElement node in documents.SelectMany(d => d.DescendantsAndSelf()))
            {
                string name = node.Name.LocalName;
                if (name != "EmitSceneEvent" && name != "SetSceneFlag" && name != "CheckSceneFlag" && name != "WaitSceneFlag") continue;
                if (scene == null) Fail(node, "scene bridge requires props/mega-mapping-expansion/scene.xml");
                if (name == "EmitSceneEvent") continue;
                FlagData flag = scene.Flags.FirstOrDefault(f => f.Id == node.Element("Key").Value);
                if (flag == null) Fail(node, "unknown scene flag: " + node.Element("Key").Value);
                NarrativeValidation.FlagValue(flag, node.Element("Value").Value);
            }
        }
        private static void Fields(XElement node, params string[] names) { FieldsOptional(node, names); }
        private static void FieldsOptional(XElement node, string[] required, params string[] optional)
        {
            if (required.Any(n => node.Elements(n).Count() != 1) || optional.Any(n => node.Elements(n).Count() > 1)
                || node.Elements().Any(e => !required.Concat(optional).Contains(e.Name.ToString()))) Fail(node, "expected " + string.Join(", ", required.Concat(optional)));
        }
        private static void ScalarEmpty(XElement node) { if (!string.IsNullOrWhiteSpace(node.Value)) Fail(node, "expected an empty node"); }
        private static string Text(XElement node)
        {
            if (node == null) throw new InvalidDataException("Missing ending value");
            string text = node.Value.Trim();
            if (node.HasElements || text.Length == 0 || text.Length > 256) Fail(node, "expected a scalar value of 1..256 characters");
            return text;
        }
        private static void Number(XElement node, double min, double max)
        { double number; if (!double.TryParse(Text(node), NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number) || number < min || number > max) Fail(node, "number out of range " + min + ".." + max); }
        private static void Integer(XElement node, int min, int max)
        { int number; if (!int.TryParse(Text(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < min || number > max) Fail(node, "integer out of range"); }
        private static void Choice<T>(string value)
        { if (!Enum.GetNames(typeof(T)).Contains(value)) throw new InvalidDataException("Unknown " + typeof(T).Name + ": " + value); }
        internal static void ValidateSprite(string value)
        {
            string[] parts = value.Split('.');
            var names = new Dictionary<string, string> { { "Regular", "Regular" }, { "Babe", "Babe" }, { "BabeCouple", "BabeCouple" }, { "Ending1Misc", "Ending1Misc" }, { "NBPKing", "NBPKing" }, { "NBPBabe", "NBPBabe" }, { "OWLKing", "OwlKing" }, { "OWLBabe", "OwlBabe" }, { "OWLGargoyle", "OwlGargoyle" }, { "OWLBird", "OwlBird" } };
            string type;
            if (parts.Length != 2 || !names.TryGetValue(parts[0], out type)) throw new InvalidDataException("Unknown ending sprite: " + value);
            Type keys = typeof(Game1).Assembly.GetType("JumpKing.JKMemory.KingSpriteLayers." + type + "+SpriteKey", true);
            if (!Enum.GetNames(keys).Contains(parts[1])) throw new InvalidDataException("Unknown ending sprite: " + value);
        }
        internal static void ValidateSound(string value)
        {
            if (!SoundNames.Contains(value)) throw new InvalidDataException("Unknown ending sound: " + value);
            string[] parts = value.Split('.');
            var fields = new Dictionary<string, string> { { "Babe", "babe" }, { "Menu", "menu" }, { "Music", "music" }, { "Player", "player" } };
            Type type = typeof(JKContentManager.Audio); string group;
            if (parts.Length != 2) throw new InvalidDataException("Expected sound Category.Name: " + value);
            if (parts[0] != "Audio")
            { if (!fields.TryGetValue(parts[0], out group)) throw new InvalidDataException("Unknown sound category: " + value); type = type.GetField(group).FieldType; }
            var field = type.GetField(parts[1]);
            if (field == null || !typeof(JumpKing.XnaWrappers.IJKSound).IsAssignableFrom(field.FieldType)) throw new InvalidDataException("Unknown ending sound: " + value);
        }
        private static readonly HashSet<string> SoundNames = new HashSet<string>((
            "Audio.Plink Audio.PressStart Audio.NewLocation Audio.Talking Audio.RaymanSFX Audio.WaterSplashEnter Audio.WaterSplashExit " +
            "Babe.Jump Babe.Kiss Babe.Mou Babe.Pickup Babe.Scream Babe.Surprised Menu.CursorMove Menu.Select Menu.MenuOpen Menu.MenuFail Menu.TitleHit " +
            "Music.TitleScreen Music.Opening Music.Ending Music.Ending2 Music.Ending3 Player.Jump Player.Land Player.Bump Player.Splat Player.IceJump " +
            "Player.IceLand Player.SnowJump Player.SnowLand Player.SnowSplat Player.IronLand Player.IronSplat Player.WaterJump Player.WaterLand " +
            "Player.WaterBump Player.WaterSplat Player.SandLand Player.EndingParasol").Split(' '), StringComparer.Ordinal);
        private static void Fail(XElement node, string error)
        { var line = (IXmlLineInfo)node; throw new InvalidDataException(node.Name + " at line " + line.LineNumber + ": " + error); }
    }
}
