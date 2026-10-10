using System;
using System.IO;
using System.Reflection;

namespace MultiplayerExpansion
{
    [Flags]
    internal enum InteractionRules : byte { Ghosts = 0, Platforms = 1, Sides = 2, Push = 4, Solid = 7 }

    internal static class InteractionSettings
    {
        internal static bool Valid(InteractionRules value)
        { return ((int)value & ~7) == 0 && ((value & InteractionRules.Push) == 0 || (value & InteractionRules.Sides) != 0); }
        internal static string Name(InteractionRules value)
        { return value == InteractionRules.Ghosts ? "Ghosts" : "Solid"; }
        internal static InteractionRules Normalize(InteractionRules value)
        { return value == InteractionRules.Ghosts ? value : InteractionRules.Platforms | InteractionRules.Sides | (value & InteractionRules.Push); }
        internal static InteractionRules Read(string path)
        {
            byte value;
            string[] fields = File.Exists(path) ? File.ReadAllText(path).Trim().Split(' ') : new string[0];
            return fields.Length > 0 && byte.TryParse(fields[0], out value) && Valid((InteractionRules)value)
                ? Normalize((InteractionRules)value) : InteractionRules.Ghosts;
        }
        internal static InteractionRules ReadSolid(string path)
        {
            string[] fields = File.Exists(path) ? File.ReadAllText(path).Trim().Split(' ') : new string[0];
            if (fields.Length == 2 && (fields[1] == "0" || fields[1] == "1"))
                return InteractionRules.Platforms | InteractionRules.Sides | (fields[1] == "1" ? InteractionRules.Push : 0);
            var saved = Read(path);
            return saved == InteractionRules.Ghosts ? InteractionRules.Solid : saved;
        }
        internal static void Write(string path, InteractionRules value)
        {
            if (!Valid(value)) throw new ArgumentException("Invalid interaction rules");
            value = Normalize(value);
            var solid = value == InteractionRules.Ghosts ? ReadSolid(path) : value;
            string temp = path + ".tmp";
            File.WriteAllText(temp, ((byte)value).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + ((solid & InteractionRules.Push) != 0 ? "1" : "0"));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
    }
}
