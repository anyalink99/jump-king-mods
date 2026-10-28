using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

internal static class ScrollingLoading
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static void Run(Assembly native, string temporary)
    {
        string folder = Path.Combine(temporary, "scrolling-settings");
        Directory.CreateDirectory(Path.Combine(folder, "textures"));
        using (var image = new Bitmap(8, 8)) image.Save(Path.Combine(folder, "textures", "sky.png"));
        var type = native.GetType("JKWorldsmith.Models.PageModels.Screen.ScrollingImagesModel");
        object model = FormatterServices.GetUninitializedObject(type);
        type.GetProperty("Folder").SetValue(model, folder, null);
        var scrolling = type.GetProperty("Scrollings");
        scrolling.SetValue(model, Activator.CreateInstance(scrolling.PropertyType), null);
        string path = Path.Combine(folder, "scroll1.xml");
        foreach (string animation in new[] { "", "<animation><fps>12</fps><sheet_cells><X>2</X><Y>3</Y></sheet_cells></animation>",
            "<animation><frames /></animation>", "<animation><fps>12</fps><frames><float>2</float><float>0</float><float>1</float></frames></animation>" })
        {
            string xml = "<ScrollingBGdata><scroll_speed>1.5</scroll_speed><layers><Layer><texture>sky</texture><position>-17</position>"
                + "<scroll_multiplier>0.25</scroll_multiplier>" + animation + "</Layer></layers></ScrollingBGdata>";
            File.WriteAllText(path, xml);
            type.GetMethod("Load", All).Invoke(model, null);
            var screens = (IList)scrolling.GetValue(model, null);
            if (screens.Count != 1) throw new Exception("Scrolling screen lost during loading");
            object screen = Get(screens[0], "Value");
            var layers = (IList)Get(screen, "Layers");
            if (layers.Count != 1 || (int)Get(layers[0], "Position") != -17 || (float)Get(layers[0], "ScrollMultiplier") != 0.25f
                || (float)Get(screen, "ScrollSpeed") != 1.5f)
                throw new Exception("Loading changed scrolling layer placement or speed");
            object settings = Get(layers[0], "Animation");
            var frames = (IList)Get(settings, "Frames");
            bool animated = animation.Contains("<float>");
            if (frames.Count != (animated ? 3 : 0) || (animated && ((float)frames[0] != 2 || (float)frames[1] != 0 || (float)frames[2] != 1)))
                throw new Exception("Loading changed animation frame order");
            if (animation.Contains("<sheet_cells>") && ((float)Get(settings, "Fps") != 12
                || (int)Get(Get(settings, "SheetCells"), "X") != 2 || (int)Get(Get(settings, "SheetCells"), "Y") != 3))
                throw new Exception("Missing frames discarded the animation settings");
            if (File.ReadAllText(path) != xml || File.Exists(path + ".worldsmith-backup"))
                throw new Exception("Loading scrolling settings wrote project files");
        }
        Console.WriteLine("[OK] Scrolling layers load missing/empty animation frames without changing placement, frame order or source files.");
    }

    static object Get(object value, string name) { return value.GetType().GetProperty(name).GetValue(value, null); }
}
