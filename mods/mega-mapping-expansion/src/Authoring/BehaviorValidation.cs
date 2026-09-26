using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion
{
    internal static class BehaviorValidation
    {
        private static void Choice(string value, string field, params string[] choices)
        { if (!choices.Contains(value)) throw new InvalidDataException(field + ": expected " + string.Join(", ", choices)); }
        private static void Id(string id)
        { if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.StartsWith("@")) throw new InvalidDataException("Invalid scene behavior ID: " + id); }
        internal static void Effect(EffectDefinition effect, IEnumerable<string> templates)
        {
            if (effect == null) throw new InvalidDataException("Null effect definition");
            if (effect.Changes == null) effect.Changes = new SceneChange[0];
            if (effect.Lights == null) effect.Lights = new LightSpawn[0];
            FiniteNumbers.Validate(effect, "Effect"); Id(effect.Id);
            if (effect.Group != null && effect.Group.Length > 128) throw new InvalidDataException(effect.Id + ": group exceeds 128 characters");
            Choice(effect.Clock, effect.Id + ".clock", "gameplay", "presentation");
            Choice(effect.Repeat, effect.Id + ".repeat", "refresh", "replace", "ignore", "stack");
            if (effect.Duration < 0 || effect.Duration > 86400 || effect.FadeIn < 0 || effect.FadeOut < 0
                || effect.FadeIn > 86400 || effect.FadeOut > effect.Duration || (effect.Duration > 0 && effect.FadeIn + effect.FadeOut > effect.Duration))
                throw new InvalidDataException(effect.Id + ": duration/fades must fit in 0..86400 seconds; fades are included in duration");
            if (effect.MaxStacks < 1 || effect.MaxStacks > 32) throw new InvalidDataException(effect.Id + ": maxStacks must be 1..32");
            if (effect.Changes == null || effect.Lights == null || effect.Changes.Length > 64 || effect.Lights.Length > 8)
                throw new InvalidDataException(effect.Id + ": maximum 64 changes and 8 light spawns");
            foreach (SceneChange change in effect.Changes)
            { if (change == null) throw new InvalidDataException("Null effect action"); Choice(change.Mode, effect.Id + ".mode", "set", "add", "multiply"); }
            foreach (LightSpawn light in effect.Lights)
            {
                if (light == null || !templates.Contains(light.Template)) throw new InvalidDataException(effect.Id + ": unknown light template");
                if (Math.Abs(light.OffsetX) > 4096 || Math.Abs(light.OffsetY) > 4096) throw new InvalidDataException(effect.Id + ": spawn offsets must be within +/-4096 pixels");
            }
        }
        internal static void Attachment(string target, SceneFile scene)
        {
            if (string.IsNullOrEmpty(target) || target == "player" || target == "player.center" || target == "player.feet") return;
            if (!scene.Nodes.Concat(scene.Props).Any(p => p.Id == target) && !scene.NativeActors.Any(a => a.Id == target)) throw new InvalidDataException("Unknown attachment target: " + target);
        }
        internal static void Validate(SceneFile scene, int screens)
        {
            if (scene.Effects.Length > 512 || scene.Regions.Length > 512 || scene.Rules.Length > 512 || scene.Flags.Length > 256 || scene.LightTemplates.Length > 128)
                throw new InvalidDataException("Behavior budget: 512 effects/regions/rules, 256 flags, 128 light templates");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EffectDefinition effect in scene.Effects) { Id(effect.Id); if (!ids.Add(effect.Id)) throw new InvalidDataException("Duplicate effect: " + effect.Id); }
            var flagIds = new HashSet<string>(StringComparer.Ordinal);
            if (scene.Flags.Any(f => f.Scope == "save")) Id(scene.Options.SaveId);
            foreach (FlagData flag in scene.Flags)
            { Id(flag.Id); if (flag.Id.Length > 120) throw new InvalidDataException("Flag ID exceeds 120 characters"); if (!flagIds.Add(flag.Id)) throw new InvalidDataException("Duplicate flag: " + flag.Id); Choice(flag.Scope, flag.Id + ".scope", "run", "save"); if (flag.Value == null || flag.Value.Length > 256) throw new InvalidDataException("Invalid flag value: " + flag.Id); }
            Action<string, string> effectRef = (name, source) => { if (!string.IsNullOrEmpty(name) && !ids.Contains(name)) throw new InvalidDataException(source + ": unknown effect " + name); };
            Action<string, string> flagRef = (name, source) => { if (!string.IsNullOrEmpty(name) && !flagIds.Contains(name)) throw new InvalidDataException(source + ": unknown flag " + name); };
            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RegionData region in scene.Regions)
            {
                Id(region.Id); if (!regionIds.Add(region.Id)) throw new InvalidDataException("Duplicate region: " + region.Id);
                if (region.Id.Length > 120) throw new InvalidDataException("Region ID exceeds 120 characters");
                if (!string.IsNullOrEmpty(region.Owner)) { Id(region.Owner); if (region.Owner.Length > 120 || region.Lifetime == "inside") throw new InvalidDataException(region.Id + ": shared owner is limited to 120 characters and lifetime=effect"); }
                if (!string.IsNullOrEmpty(region.Anchor))
                {
                    SceneAnchor anchor = scene.Anchors.FirstOrDefault(a => a.Id == region.Anchor);
                    if (anchor == null) throw new InvalidDataException(region.Id + ": unknown anchor " + region.Anchor);
                    if (region.X != 0 || region.Y != 0 || region.Width != 0 || region.Height != 0 || region.Screen != 1)
                        throw new InvalidDataException(region.Id + ": anchor supplies the screen and rectangle; omit screen/x/y/width/height");
                    if (region.Test == "standing" && anchor.Kind != "solid") throw new InvalidDataException(region.Id + ": standing requires a solid anchor");
                }
                else if (region.Screen < 1 || region.Screen > screens || region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 || region.X + region.Width > 480 || region.Y + region.Height > 360)
                    throw new InvalidDataException(region.Id + ": region must fit its existing 480x360 screen");
                Choice(region.Test, region.Id + ".test", "hitbox", "center", "feet", "contained", "standing");
                if (region.Test == "standing" && region.Hysteresis != 0) throw new InvalidDataException(region.Id + ": standing uses exact native support, not hysteresis");
                Choice(region.SpawnInside, region.Id + ".spawnInside", "fire", "baseline"); Choice(region.Lifetime, region.Id + ".lifetime", "effect", "inside");
                if (region.Hysteresis < 0 || region.Hysteresis > 32 || region.Dwell < 0 || region.Dwell > 60) throw new InvalidDataException(region.Id + ": hysteresis 0..32 px; dwell 0..60 seconds");
                effectRef(region.Enter, region.Id); effectRef(region.Exit, region.Id); flagRef(region.RequiresFlag, region.Id);
                if (region.Lifetime == "inside" && !string.IsNullOrEmpty(region.Enter) && scene.Effects.First(e => e.Id == region.Enter).Repeat == "stack")
                    throw new InvalidDataException(region.Id + ": inside ownership does not support stacked enter effects");
            }
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuleData rule in scene.Rules)
            {
                Id(rule.Id); if (!ruleIds.Add(rule.Id)) throw new InvalidDataException("Duplicate rule: " + rule.Id);
                if (rule.Id.Length > 120) throw new InvalidDataException("Rule ID exceeds 120 characters");
                if (!string.IsNullOrEmpty(rule.Owner)) { Id(rule.Owner); if (rule.Owner.Length > 120) throw new InvalidDataException(rule.Id + ": shared owner exceeds 120 characters"); }
                Id(rule.Event); effectRef(rule.Effect, rule.Id); flagRef(rule.RequiresFlag, rule.Id); flagRef(rule.SetFlag, rule.Id);
                if (rule.Sound != null && (rule.Sound.Length == 0 || rule.Sound.Length > 128 || rule.Sound.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0))
                    throw new InvalidDataException(rule.Id + ": sound must be a native event_music key, not a file path");
                if (rule.Cooldown < 0 || rule.Cooldown > 86400 || rule.Screen < 0 || rule.Screen > screens) throw new InvalidDataException(rule.Id + ": invalid cooldown/screen");
                if (!string.IsNullOrEmpty(rule.SetFlag) && (rule.Value == null || rule.Value.Length > 256)) throw new InvalidDataException(rule.Id + ": flag value required");
            }
            var props = scene.Props.Concat(scene.Nodes).ToDictionary(p => p.Id, StringComparer.Ordinal);
            foreach (string objectId in props.Keys.Concat(scene.Lights.Select(l => l.Id)).Concat(scene.Fogs.Select(f => f.Id)).Concat(scene.Rains.Select(r => r.Id)).Concat(scene.Emitters.Select(e => e.Id))
                .Concat(scene.Waters.Select(w => w.Id)).Concat(scene.Puddles.Select(p => p.Id)).Concat(scene.Bushes.Select(b => b.Id))
                .Concat(scene.Surfs.Select(s => s.Id)).Concat(scene.Planets.Select(p => p.Id)).Concat(scene.ShadowSurfaces.Select(s => s.Id)))
                if (objectId == "options" || objectId.StartsWith("screen:") || objectId.StartsWith("anchor:") || objectId.StartsWith("@") || objectId.Contains("/")) throw new InvalidDataException("Reserved scene object ID: " + objectId);
            foreach (PropData prop in props.Values)
            {
                if (Math.Abs(prop.OffsetX) > 4096 || Math.Abs(prop.OffsetY) > 4096) throw new InvalidDataException(prop.Id + ": attachment offsets must be within +/-4096 pixels");
                var chain = new HashSet<string>(StringComparer.Ordinal); PropData next = prop;
                while (next != null && !string.IsNullOrEmpty(next.Attach))
                {
                    Attachment(next.Attach, scene);
                    if (!chain.Add(next.Id)) throw new InvalidDataException("Attachment cycle at " + next.Id);
                    if (chain.Count > 32) throw new InvalidDataException("Attachment chains may contain at most 32 parents");
                    props.TryGetValue(next.Attach, out next);
                }
            }
            foreach (LightData light in scene.Lights.Concat(scene.LightTemplates))
            { Attachment(light.Attach, scene); if (Math.Abs(light.OffsetX) > 4096 || Math.Abs(light.OffsetY) > 4096) throw new InvalidDataException(light.Id + ": attachment offsets must be within +/-4096 pixels"); }
            // Same bounded validation of all action operands without GPU resources or disk IO.
            foreach (BehaviorTreeData tree in scene.BehaviorTrees)
                if (tree.Screen > screens) throw new InvalidDataException(tree.Id + ": tree screen exceeds map size");
            using (var engine = new SceneBehaviorEngine(scene, null)) { }
        }
    }
}
