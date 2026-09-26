using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MegaMappingExpansion
{
    internal static class ReflectionSelection
    {
        internal static string[] Parse(string text)
        {
            if (text == null) return null; // Omission explicitly selects the whole composed world.
            var values = text.Split(';').Select(v => v.Trim()).ToArray();
            if (values.Length == 0 || values.Length > 128 || values.Any(string.IsNullOrEmpty) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new InvalidDataException("reflectionObjects requires 1..128 unique Prop/Node IDs or player, separated by semicolons");
            return values;
        }
        internal static void Validate(SceneFile scene)
        {
            var props = scene.Props.Concat(scene.Nodes).ToDictionary(p => p.Id, StringComparer.Ordinal);
            var sets = new HashSet<string>(StringComparer.Ordinal);
            Action<string, string, int> check = (id, text, screen) => {
                string[] selection = Parse(text); if (selection == null) return;
                foreach (string selected in selection)
                {
                    if (selected == "player")
                    {
                        if (props.ContainsKey(selected)) throw new InvalidDataException(id + ": reflection token player is reserved for the King; rename the Prop/Node named player");
                        continue;
                    }
                    PropData prop;
                    if (scene.NativeActors.Any(a => a.Id == selected)) continue;
                    if (!props.TryGetValue(selected, out prop)) throw new InvalidDataException(id + ": unknown reflection object " + selected);
                    if (prop.Screen != screen && string.IsNullOrEmpty(prop.Attach)) throw new InvalidDataException(id + ": reflection object is on another screen: " + selected);
                }
                sets.Add(screen + "/" + text);
            };
            foreach (WaterData water in scene.Waters)
            {
                if (water.ReflectionObjects != null && !water.CompositeReflection) throw new InvalidDataException(water.Id + ": reflectionObjects requires compositeReflection");
                check(water.Id, water.ReflectionObjects, water.Screen);
            }
            foreach (PuddleData puddle in scene.Puddles) check(puddle.Id, puddle.ReflectionObjects, puddle.Screen);
            if (sets.Count > 64) throw new InvalidDataException("At most 64 distinct reflection selections per map (about 43 MiB at native resolution)");
        }
    }
}
