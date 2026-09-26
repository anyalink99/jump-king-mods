using System;
using System.IO;
using System.Reflection;

namespace JKRuntime.Settings
{
    /// <summary>Resolves mod-owned save files against the installed game's current save prefix without using its lossy XML reader/cache.</summary>
    public static class NativeSaveFiles
    {
        private static readonly PropertyInfo Prefix = typeof(JumpKing.SaveThread.SaveManager).Assembly
            .GetType("JumpKing.SaveThread.SaveHelper", true).GetProperty("PREFIX", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>Returns an absolute path in a relative native save folder. Resolve again after native context changes.</summary>
        public static string GetPath(string folder, string file)
        {
            if (Prefix == null || Prefix.PropertyType != typeof(string)) throw new NotSupportedException("Native save prefix is unavailable");
            if (string.IsNullOrWhiteSpace(folder) || Path.IsPathRooted(folder) || string.IsNullOrWhiteSpace(file) || file == "." || file == ".." || Path.GetFileName(file) != file)
                throw new ArgumentException("A relative save folder and a single filename are required");
            string root = Path.GetFullPath((string)Prefix.GetValue(null, null)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, folder, file));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Save path escapes the native content root");
            return path;
        }
    }
}
