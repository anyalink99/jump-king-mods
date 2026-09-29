using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace WorldsmithExtension
{
    internal static class FileSafety
    {
        static readonly BindingFlags Methods = BindingFlags.Static | BindingFlags.NonPublic;
        internal static void Install(Harmony harmony)
        {
            Patch(harmony, "JKWorldsmith.Extensions.IOWrappers", "DeleteTempAndMakeCurrentTemp", "RemoveWithBackup");
            Patch(harmony, "JKWorldsmith.Models.PageModels.Screen.ScreensModel", "Delete", "DeleteScreen");
            Patch(harmony, "JKWorldsmith.Models.PageModels.Screen.PropsModel", "ReplaceProp", "ReplaceProp");
            Patch(harmony, "JKWorldsmith.Models.PageModels.Screen.Tilesets.Layer", "SaveBitmap", "SaveLayer");
            Patch(harmony, "JKWorldsmith.Models.Projects.CurrentProjectManager", "GetCompiledFromRelative", "CompiledPath");
            Patch(harmony, "JKWorldsmith.Models.Projects.CurrentProjectManager", "PackOnDeleted", "DeleteCompiled");
            Patch(harmony, "JKWorldsmith.Models.Projects.CurrentProjectManager", "PackOnRenamed", "RenameCompiled");
            var replacements = new[]{Tuple.Create("JKWorldsmith.Extensions.IOWrappers", "OverridePreviousFile"), Tuple.Create("JKWorldsmith.Models.PageModels.Screen.ScreensModel", "Replace"), Tuple.Create("JKWorldsmith.Models.PageModels.Screen.MasksModel", "Replace"), Tuple.Create("JKWorldsmith.Models.PageModels.Screen.NPCsModel", "ChangeImage"), Tuple.Create("JKWorldsmith.ViewModels.Level.WardrobeViewModel", "ChangeBase"), Tuple.Create("JKWorldsmith.ViewModels.Level.WardrobeViewModel", "ReplaceFile"), Tuple.Create("JKWorldsmith.ViewModels.Level.WardrobeViewModel", "ReplaceWorlditem"), Tuple.Create("JKWorldsmith.Models.PageModels.Screen.PropsModel", "AddNewProp")};
            foreach (var replacement in replacements)
            {
                var method = AccessTools.DeclaredMethod(Engine.Type(replacement.Item1), replacement.Item2);
                if (method == null)
                    throw new MissingMethodException(replacement.Item1, replacement.Item2);
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(FileSafety).GetMethod("AtomicCopies", Methods)));
            }
        }

        static void Patch(Harmony harmony, string type, string method, string prefix)
        {
            var target = AccessTools.Method(Engine.Type(type), method);
            if (target == null)
                throw new MissingMethodException(type, method);
            harmony.Patch(target, new HarmonyMethod(typeof(FileSafety).GetMethod(prefix, Methods)));
        }

        static IEnumerable<CodeInstruction> AtomicCopies(IEnumerable<CodeInstruction> instructions)
        {
            int copies = 0;
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(File) && method.Name == "Copy")
                {
                    string replacement = method.GetParameters().Length == 2 ? "Copy" : "CopyOverwrite";
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(FileSafety).GetMethod(replacement, Methods);
                    copies++;
                }
                else if (method != null && method.DeclaringType == Engine.Type("JKWorldsmith.Extensions.IOWrappers") && method.Name == "DeleteTempAndMakeCurrentTemp")
                {
                    instruction.operand = typeof(FileSafety).GetMethod("KeepUntilReplacement", Methods);
                }

                yield return instruction;
            }

            if (copies == 0)
                throw new InvalidOperationException("The supported image replacement contract changed.");
        }

        static void KeepUntilReplacement(string path)
        {
        }

        internal static void Copy(string source, string destination)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("Replacement file is missing.", source);
            if (String.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                return;
            using (var input = File.OpenRead(source))
                Files.Atomic(destination, output => input.CopyTo(output));
            Panel.Status("Saved " + Path.GetFileName(destination));
        }

        static void CopyOverwrite(string source, string destination, bool overwrite)
        {
            Copy(source, destination);
        }

        internal static string CheckedPath(string root, string path)
        {
            root = Path.GetFullPath(root);
            path = Path.GetFullPath(path);
            string relative = Files.Relative(root, path);
            if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked project roots are not supported.");
            string current = root;
            foreach (string part in relative.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked project paths are not supported: " + current);
            }

            return path;
        }

        internal static string Archive(string root, string path)
        {
            path = CheckedPath(root, path);
            if (!File.Exists(path) && !Directory.Exists(path))
                return null;
            string relative = Files.Relative(root, path);
            string destination = Path.Combine(root, ".worldsmith-extension", "deleted", Guid.NewGuid().ToString("N"), relative);
            CheckedPath(root, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (Directory.Exists(path))
                Directory.Move(path, destination);
            else
                File.Move(path, destination);
            return destination;
        }

        static bool RemoveWithBackup(string path)
        {
            string root = Engine.ProjectRoot;
            if (String.IsNullOrEmpty(root))
                throw new IOException("No project is open for this file operation.");
            string archived = Archive(root, path);
            if (archived != null)
                Panel.Status("Removed " + Path.GetFileName(path) + "; recover from " + archived);
            return false;
        }

        static bool SaveLayer(object __instance)
        {
            string path = (string)Engine.Get(__instance, "filePath");
            var bitmap = (Bitmap)Engine.Get(__instance, "Bitmap");
            Files.Atomic(path, stream => bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png));
            Panel.Status("Saved " + Path.GetFileName(path));
            return false;
        }

        static bool DeleteScreen(object __instance, object possible_screen)
        {
            int screen = (int)possible_screen;
            string folder = (string)Engine.Get(__instance, "Folder");
            // Choose the complete filename instead of a partial regular expression
            string path = (string)Engine.Call(__instance, "GetImagePath", screen);
            if (__instance.GetType().Name == "MasksModel")
            {
                object weather = Engine.Get(Engine.Service("JKWorldsmith.ViewModels.Level.ScreenViewModel"), "CurrentWeather");
                string name = weather == null ? "" : (string)Engine.Get(weather, "Name");
                path = Path.Combine(folder, name + "mask" + screen + ".png");
            }

            string archived = Archive(folder, path);
            ((IDictionary)Engine.Get(__instance, "Dictionary")).Remove(screen);
            Engine.Call(__instance, "OnDictionaryChanged", __instance, EventArgs.Empty);
            if (archived != null)
                Panel.Status("Removed screen image; recover from " + archived);
            return false;
        }

        static bool ReplaceProp(object __instance, object possible_prop_name)
        {
            string name = (string)possible_prop_name;
            string folder = Path.Combine((string)Engine.Get(__instance, "Folder"), "textures");
            string path = CheckedPath(folder, Path.Combine(folder, name + ".png"));
            var type = Assembly.Load("Ookii.Dialogs.Wpf").GetType("Ookii.Dialogs.Wpf.VistaOpenFileDialog", true);
            object dialog = Activator.CreateInstance(type);
            Engine.Set(dialog, "Filter", "PNG (*.png)|*.png");
            Engine.Set(dialog, "CheckFileExists", true);
            if (!Object.Equals(type.GetMethod("ShowDialog", Type.EmptyTypes).Invoke(dialog, null), true))
                return false;
            string selected = (string)Engine.Get(dialog, "FileName");
            object image = Engine.Call(Engine.Type("JKWorldsmith.Extensions.DisposableImage"), "Get", selected);
            Copy(selected, path);
            object settings = Engine.Get(__instance, "Settings");
            object setting = settings.GetType().GetProperty("Item", new[]{typeof(string)}).GetValue(settings, new object[]{name});
            Engine.Set(setting, "Texture", image);
            Engine.Call(setting, "UpdateSheet");
            return false;
        }

        static string OutputPath(object project, string source)
        {
            string root = (string)Engine.Get(project, "Directory");
            string relative = Files.Relative(root, CheckedPath(root, source));
            string first = relative.Split(Path.DirectorySeparatorChar)[0];
            if (Files.IgnoredDirectory(first))
                throw new IOException("Generated files cannot be mapped as source assets.");
            if (new[]{".png", ".bmp", ".wav", ".mp3"}.Contains(Path.GetExtension(relative).ToLowerInvariant()))
                relative = Path.ChangeExtension(relative, ".xnb");
            string output = Path.Combine(root, "bin");
            return CheckedPath(output, Path.Combine(output, relative));
        }

        static bool CompiledPath(object __instance, string file, ref string newFile, ref bool __result)
        {
            newFile = OutputPath(__instance, file);
            __result = File.Exists(newFile) || Directory.Exists(newFile);
            return false;
        }

        static bool DeleteCompiled(object __instance, object e)
        {
            if ((bool)Engine.Get(e, "Handled"))
                return false;
            string path = OutputPath(__instance, (string)Engine.Get(e, "FullPath"));
            Archive((string)Engine.Get(__instance, "Directory"), path);
            NotifyPacked((string)Engine.Get(e, "FullPath"), "Deleted");
            return false;
        }

        static bool RenameCompiled(object __instance, object e)
        {
            if ((bool)Engine.Get(e, "Handled"))
                return false;
            string source = (string)Engine.Get(e, "FullPath");
            string old = OutputPath(__instance, (string)Engine.Get(e, "OldFullPath"));
            // Recompile after a rename because the extension may have changed
            if (File.Exists(source))
            {
                object[] arguments = {source, null};
                bool ok = (bool)__instance.GetType().GetMethod("PackFile", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, arguments);
                if (!ok)
                    throw new IOException((string)arguments[1]);
            }

            if (!String.Equals(old, OutputPath(__instance, source), StringComparison.OrdinalIgnoreCase))
                Archive((string)Engine.Get(__instance, "Directory"), old);
            NotifyPacked(source, "Renamed");
            return false;
        }

        static void NotifyPacked(string path, string kind)
        {
            var handler = Engine.Get(Engine.Type("JKWorldsmith.Models.Projects.CurrentProjectManager"), "OnItemPacked") as Delegate;
            if (handler == null)
                return;
            object type = Enum.Parse(Engine.Type("JKWorldsmith.Models.FileWatcher.UpdateType"), kind);
            object args = Activator.CreateInstance(Engine.Type("JKWorldsmith.Models.FileWatcher.ItemUpdatedEventArgs"), new[]{type});
            handler.DynamicInvoke(path, args);
        }
    }
}
