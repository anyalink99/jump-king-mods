using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Media.Imaging;
using HarmonyLib;

internal static class Compatibility
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly engine, Assembly native, string fixtures, string host)
    {
        var run = engine.GetType("WorldsmithExtension.BuildJob").GetMethod("Run", All);
        var files = engine.GetType("WorldsmithExtension.Files");
        var status = new Action<string>(text => {});
        string originalTemplate = Path.Combine(host, "Resources", "Templates");
        foreach (string category in new[] { "Skin", "Set", "Tileset" })
        {
            string source = Path.Combine(fixtures, "template-" + category), output = Path.Combine(fixtures, "template-build-" + category, "content");
            Copy(Path.Combine(originalTemplate, category), source, false);
            if (category == "Tileset")
            {
                // the native new-project flow writes this after copying its PNG-only template
                object settings = Activator.CreateInstance(native.GetType("JKWorldsmith.Models.PageModels.Screen.Tilesets.TilesetSettings"));
                object data = settings.GetType().GetMethod("ToStruct").Invoke(settings, null);
                using (var stream = File.Create(Path.Combine(source, "tileset_settings.xml")))
                    new System.Xml.Serialization.XmlSerializer(data.GetType()).Serialize(stream, data);
            }
            string before = (string)files.GetMethod("Fingerprint", All).Invoke(null, new object[] { source });
            object detected = engine.GetType("WorldsmithExtension.NativeProjects").GetMethod("Category", All).Invoke(null, new object[] { source });
            if (detected.ToString() != category) throw new Exception("Stock template category changed: " + category);
            run.Invoke(null, new object[] { source, category, output, status });
            if (Directory.GetFiles(output, "*.xnb", SearchOption.AllDirectories).Length == 0 ||
                before != (string)files.GetMethod("Fingerprint", All).Invoke(null, new object[] { source }))
                throw new Exception("Stock template didn't compile without changing its source: " + category);
        }

        string compiled = Path.Combine(fixtures, "partial-package"), rebuilt = Path.Combine(fixtures, "partial-build", "content");
        Copy(Path.Combine(fixtures, "stock-template-build", "content"), compiled, true);
        string picture = Path.Combine(compiled, "screens", "foreground", "preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(picture));
        using (var bitmap = new Bitmap(3, 3))
        {
            bitmap.SetPixel(0, 0, Color.Lime);
            bitmap.Save(picture, System.Drawing.Imaging.ImageFormat.Png);
        }
        File.WriteAllText(Path.ChangeExtension(picture, ".xnb"), "stale screen texture");
        string collisionHash = (string)files.GetMethod("Hash", All).Invoke(null, new object[] { Path.Combine(compiled, "level.xnb") });
        run.Invoke(null, new object[] { compiled, "Level", rebuilt, status });
        if (collisionHash != (string)files.GetMethod("Hash", All).Invoke(null, new object[] { Path.Combine(rebuilt, "level.xnb") }))
            throw new Exception("Compiling partial sources changed compiled-only collision");
        string screen = Path.Combine(rebuilt, "screens", "foreground", "preview.xnb");
        string decoded = Path.Combine(fixtures, "compiled-preview.xnb");
        engine.GetType("WorldsmithExtension.PackedAssets").GetMethod("Extract", All).Invoke(null, new object[] { screen, decoded, false });
        using (var bitmap = new Bitmap(Path.ChangeExtension(decoded, ".png")))
            if (bitmap.GetPixel(0, 0).ToArgb() != Color.Lime.ToArgb()) throw new Exception("Partial package retained stale screen pixels");

        CollisionChoice(engine, native, fixtures);
        CompactAtlases(engine, native, fixtures);
        var manager = native.GetType("JKWorldsmith.Models.Projects.CurrentProjectManager");
        object project = FormatterServices.GetUninitializedObject(manager);
        manager.BaseType.GetField("directory", All).SetValue(project, compiled);
        object[] scan = { null };
        manager.GetMethod("CheckForNewFiles", All).Invoke(project, scan);
        var pending = (string[])scan[0];
        if (!pending.Contains(picture) || pending.Contains(Path.ChangeExtension(picture, ".xnb")) || !pending.Contains(Path.Combine(compiled, "level.xnb")))
            throw new Exception("Native background scan let stale compiled assets override available sources");
        File.WriteAllText(Path.Combine(compiled, "visual_level.png"), "scan fixture");
        scan = new object[] { null };
        manager.GetMethod("CheckForNewFiles", All).Invoke(project, scan);
        if (((string[])scan[0]).Contains(Path.Combine(compiled, "level.xnb")))
            throw new Exception("Compiled collision overrode an available strip source");
        File.Delete(Path.Combine(compiled, "visual_level.png"));
        foreach (string relative in new[] { "visual_level.png", "props/textures/temp/sprite.png" })
        {
            object[] args = { Path.Combine(compiled, relative), null };
            manager.GetMethod("GetCompiledFromRelative", All).Invoke(project, args);
            string expected = Path.Combine(compiled, "bin", relative == "visual_level.png" ? "level.xnb" : "props/textures/temp/sprite.xnb");
            if ((string)args[1] != Path.GetFullPath(expected)) throw new Exception("Native compiled path changed: " + relative);
        }
        Watcher(engine, native, compiled, project);
        string removed = Path.Combine(compiled, "screens", "foreground", "removed.png");
        File.WriteAllText(removed, "image");
        File.WriteAllText(Path.ChangeExtension(removed, ".xnb"), "compiled image");
        engine.GetType("WorldsmithExtension.FileSafety").GetMethod("ArchiveAsset", All).Invoke(null, new object[] { compiled, removed });
        if (File.Exists(removed) || File.Exists(Path.ChangeExtension(removed, ".xnb")) ||
            !Directory.GetFiles(Path.Combine(compiled, ".worldsmith-extension", "deleted"), "removed.xnb", SearchOption.AllDirectories).Any())
            throw new Exception("Deleted recovered asset survived as an active XNB or lost its backup");
        Console.WriteLine("[OK] Stock Skin/Set/Tileset templates, partial-source maps, ordinary collision source selection and native compiled paths.");
    }

    static void CompactAtlases(Assembly engine, Assembly native, string fixtures)
    {
        var type = native.GetType("JKWorldsmith.ViewModels.Level.HitboxViewModel");
        foreach (var shape in new[] { new Size(3, 3), new Size(7, 5), new Size(12, 13) })
        {
            string root = Path.Combine(fixtures, "compact-" + shape.Width + "-" + shape.Height);
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "level.png");
            using (var bitmap = new Bitmap(shape.Width * 60, shape.Height * 45))
            {
                bitmap.SetPixel(0, 0, Color.Lime);
                bitmap.Save(source);
            }
            string before = (string)engine.GetType("WorldsmithExtension.Files").GetMethod("Fingerprint", All).Invoke(null, new object[] { root });
            object model = FormatterServices.GetUninitializedObject(type);
            type.GetField("folder", All).SetValue(model, root);
            type.GetMethod("InitializeHitbox", All).Invoke(model, null);
            using (var image = (Bitmap)type.GetProperty("GdiImage").GetValue(model, null))
            {
                int count = shape.Width * shape.Height;
                if (((Array)type.GetProperty("HitboxFrames").GetValue(model, null)).Length != count || image.GetPixel(0, (count - 1) * 45).ToArgb() != Color.Lime.ToArgb())
                    throw new Exception("Compact native atlas lost its screens or order");
                type.GetMethod("GenerateInGameLevelHitbox", All).Invoke(model, null);
            }
            if (before != (string)engine.GetType("WorldsmithExtension.Files").GetMethod("Fingerprint", All).Invoke(null, new object[] { root }))
                throw new Exception("Opening a compact atlas changed its source");
        }
    }

    static int notifications;
    static void Changed(object sender, object args) { notifications++; }
    static void Watcher(Assembly engine, Assembly native, string root, object project)
    {
        var manager = native.GetType("JKWorldsmith.Models.Projects.CurrentProjectManager");
        var changed = manager.GetEvent("FileChanged");
        var handler = Delegate.CreateDelegate(changed.EventHandlerType, typeof(Compatibility).GetMethod("Changed", All));
        var type = native.GetType("JKWorldsmith.Models.FileWatcher.ProperFileWatcher");
        var argsType = native.GetType("JKWorldsmith.Models.FileWatcher.ProperFileSystemEventArgs");
        var wait = type.GetMethod("WaitForChanged", All, null, new[] { typeof(object), argsType }, null);
        using (var watcher = (FileSystemWatcher)Activator.CreateInstance(type, new object[] { root }))
        {
            changed.AddEventHandler(null, handler);
            try
            {
                notifications = 0;
                foreach (string relative in new[] { "props/textures/temp/a.png", "props/textures/bin/b.png", "bin/ignored.png" })
                {
                    string path = Path.Combine(root, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, "watch fixture");
                    object args = Activator.CreateInstance(argsType, new object[] { new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(path), Path.GetFileName(path)) });
                    wait.Invoke(watcher, new[] { watcher, args });
                }
                if (notifications != 2) throw new Exception("Watcher dropped nested resource folders or included generated output");
                string output = Path.Combine(root, "bin", "level.xnb");
                File.WriteAllText(output, "compiled collision");
                object deleted = Activator.CreateInstance(argsType, new object[] { new FileSystemEventArgs(WatcherChangeTypes.Deleted, root, "visual_level.png") });
                manager.GetMethod("PackOnDeleted", All).Invoke(project, new[] { watcher, deleted });
                if (File.Exists(output) || !Directory.GetFiles(Path.Combine(root, ".worldsmith-extension", "deleted"), "level.xnb", SearchOption.AllDirectories)
                    .Any(p => File.ReadAllText(p) == "compiled collision"))
                    throw new Exception("Deleting a collision strip didn't archive its actual compiled output");
            }
            finally { changed.RemoveEventHandler(null, handler); }
        }
    }

    static void CollisionChoice(Assembly engine, Assembly native, string fixtures)
    {
        var type = native.GetType("JKWorldsmith.ViewModels.Level.HitboxViewModel");
        var initialize = type.GetMethod("InitializeHitbox", All);
        var harmony = new Harmony("worldsmith.extension");
        foreach (bool atlasNewer in new[] { true, false })
        {
            foreach (bool stock in new[] { true, false })
            {
                string root = Path.Combine(fixtures, "collision-" + atlasNewer + "-" + stock);
                Directory.CreateDirectory(root);
                string atlas = Path.Combine(root, "level.png"), strip = Path.Combine(root, "visual_level.png");
                using (var bitmap = new Bitmap(780, 585))
                {
                    bitmap.SetPixel(0, 0, Color.Blue);
                    bitmap.Save(atlas);
                }
                using (var bitmap = new Bitmap(60, 169 * 45))
                {
                    bitmap.SetPixel(0, 168 * 45, Color.Lime);
                    bitmap.Save(strip);
                }
                DateTime stamp = DateTime.UtcNow.AddHours(-1);
                File.SetLastWriteTimeUtc(atlas, stamp.AddMinutes(atlasNewer ? 1 : 0));
                File.SetLastWriteTimeUtc(strip, stamp.AddMinutes(atlasNewer ? 0 : 1));
                object model = FormatterServices.GetUninitializedObject(type);
                type.GetField("folder", All).SetValue(model, root);
                type.GetField("filter", All).SetValue(model, "visual_level.png");
                if (stock) harmony.Unpatch(initialize, HarmonyPatchType.Prefix, "worldsmith.extension");
                try
                {
                    initialize.Invoke(model, null);
                    var frame = (BitmapSource)((Array)type.GetProperty("HitboxFrames").GetValue(model, null)).GetValue(0);
                    byte[] pixel = new byte[4];
                    frame.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
                    if (pixel[atlasNewer ? 0 : 1] != 255 || pixel[atlasNewer ? 1 : 0] != 0)
                        throw new Exception("Ordinary collision selected the wrong source; stock=" + stock + ", atlasNewer=" + atlasNewer);
                }
                finally
                {
                    var image = type.GetProperty("GdiImage").GetValue(model, null) as Bitmap;
                    if (image != null) image.Dispose();
                    if (stock) harmony.Patch(initialize, new HarmonyMethod(engine.GetType("WorldsmithExtension.CollisionPatches").GetMethod("InitializeCollision", All)));
                }
            }
        }
    }

    static void Copy(string source, string destination, bool compiledOnly)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            if (!compiledOnly || !new[] { ".png", ".bmp", ".wav", ".mp3" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string child in Directory.GetDirectories(source)) Copy(child, Path.Combine(destination, Path.GetFileName(child)), compiledOnly);
    }
}
