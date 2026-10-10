using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

internal static class RightGrowing
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static void Run(Assembly engine, Assembly native, string fixtures)
    {
        string root = Path.Combine(fixtures, "right-growing"), output = Path.Combine(fixtures, "right-growing-build", "content");
        Directory.CreateDirectory(root);
        foreach (string folder in new[] { "audio", "ending", "king", "particles", "props", "screens", "gui", "props/textures", "props/mega-mapping-expansion" })
            Directory.CreateDirectory(Path.Combine(root, folder));
        foreach (string file in new[] { "level_settings.xml", "gui/location_settings.xml", "gui/earthquake_settings.xml", "props/textures/prop_settings.xml", "particles/weather.xml" })
            File.WriteAllText(Path.Combine(root, file), "<Settings/>");
        File.WriteAllText(Path.Combine(root, "worldsmith-extension.xml"), "<WorldsmithExtension collisionSource='atlas'/>");
        File.WriteAllText(Path.Combine(root, "props/mega-mapping-expansion/map.xml"), "<MapLayout version='1' screens='200' atlasSide='15'/>");
        string source = Path.Combine(root, "level.png");
        using (var atlas = new Bitmap(960, 585))
        {
            atlas.SetPixel(0, 0, Color.Blue);
            atlas.SetPixel((199 / 13) * 60, (199 % 13) * 45, Color.Magenta);
            atlas.SetPixel(959, 584, Color.Lime);
            atlas.Save(source);
        }
        // an old strip lying around shouldn't override the selected source
        using (var strip = new Bitmap(60, 169 * 45)) strip.Save(Path.Combine(root, "visual_level.png"));
        byte[] before = File.ReadAllBytes(source);
        var type = native.GetType("JKWorldsmith.ViewModels.Level.HitboxViewModel");
        object vm = FormatterServices.GetUninitializedObject(type);
        type.GetField("folder", All).SetValue(vm, root);
        type.GetMethod("InitializeHitbox", All).Invoke(vm, null);
        var frames = (Array)type.GetProperty("HitboxFrames").GetValue(vm, null);
        using (var image = (Bitmap)type.GetProperty("GdiImage").GetValue(vm, null))
        {
            if (frames.Length != 200 || image.GetPixel(0, 0).ToArgb() != Color.Magenta.ToArgb() || image.GetPixel(0, 199 * 45).ToArgb() != Color.Blue.ToArgb())
                throw new Exception("Right-growing preview has wrong screen order or source");
            type.GetMethod("GenerateInGameLevelHitbox", All).Invoke(vm, null);
        }
        if (!System.Linq.Enumerable.SequenceEqual(before, File.ReadAllBytes(source)))
            throw new Exception("Native regeneration overwrote right-growing source or padding");
        string atlasOnly = Path.Combine(fixtures, "atlas-only");
        Directory.CreateDirectory(Path.Combine(atlasOnly, "props/mega-mapping-expansion"));
        File.Copy(source, Path.Combine(atlasOnly, "level.png"));
        File.Copy(Path.Combine(root, "props/mega-mapping-expansion/map.xml"), Path.Combine(atlasOnly, "props/mega-mapping-expansion/map.xml"));
        object atlasVm = FormatterServices.GetUninitializedObject(type);
        type.GetField("folder", All).SetValue(atlasVm, atlasOnly);
        type.GetMethod("InitializeHitbox", All).Invoke(atlasVm, null);
        using (var image = (Bitmap)type.GetProperty("GdiImage").GetValue(atlasVm, null))
        {
            if (image.GetPixel(0, 0).ToArgb() != Color.Magenta.ToArgb())
                throw new Exception("Atlas-only source lost screen order without authoring settings");
            type.GetMethod("GenerateInGameLevelHitbox", All).Invoke(atlasVm, null);
        }
        if (!System.Linq.Enumerable.SequenceEqual(before, File.ReadAllBytes(Path.Combine(atlasOnly, "level.png"))))
            throw new Exception("Native regeneration overwrote atlas-only source without authoring settings");
        engine.GetType("WorldsmithExtension.Builder").GetMethod("Build", All).Invoke(null, new object[] { root, output, new Action<string>(text => {}) });
        string recovered = Path.Combine(fixtures, "right-growing-compiled.png");
        engine.GetType("WorldsmithExtension.PackedAssets").GetMethod("Extract", All).Invoke(null, new object[] { Path.Combine(output, "level.xnb"), recovered, true });
        using (var atlas = new Bitmap(recovered))
            if (atlas.Width != 900 || atlas.Height != 675 || atlas.GetPixel((199 / 15) * 60, (199 % 15) * 45).ToArgb() != Color.Magenta.ToArgb())
                throw new Exception("Checked build did not repack right-growing source");
        if (File.Exists(Path.Combine(output, "worldsmith-extension.xml")) || File.Exists(Path.Combine(output, "level.png")))
            throw new Exception("Authoring layout leaked into playable output");
        Console.WriteLine("[OK] Right-growing atlas: native preview, source preservation and checked build.");
    }
}
