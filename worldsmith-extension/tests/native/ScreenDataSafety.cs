using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Threading;
using HarmonyLib;

internal static class ScreenDataSafety
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int errors;
    static bool SkipStartup() { return false; }
    static bool Report(Exception e, string title) { errors++; return false; }
    static object Get(object value, string name) { return value.GetType().GetProperty(name).GetValue(value, null); }
    static void Set(object value, string name, object data) { value.GetType().GetProperty(name).SetValue(value, data, null); }
    static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, All).Invoke(value, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static string Xml(float speed) { return "<ScrollingBGdata><scroll_speed>" + speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</scroll_speed><layers /></ScrollingBGdata>"; }

    internal static void Run(Assembly native, string temporary)
    {
        if (Application.Current == null) new Application();
        var harmony = new Harmony("worldsmith.tests.screen-data-errors");
        var report = native.GetType("JKWorldsmith.Models.DialogManager").GetMethod("ShowError", new[] { typeof(Exception), typeof(string) });
        // Typography creates the native App; pumping audio callbacks mustn't start Steam and the real UI
        var startup = native.GetType("JKWorldsmith.App").GetMethod("OnStartup", All, null,
            new[] { typeof(object), typeof(StartupEventArgs) }, null);
        harmony.Patch(startup, new HarmonyMethod(typeof(ScreenDataSafety).GetMethod("SkipStartup", All)));
        harmony.Patch(report, new HarmonyMethod(typeof(ScreenDataSafety).GetMethod("Report", All)));
        try { Scrolls(native, temporary); Audio(native, temporary); }
        finally
        {
            harmony.Unpatch(report, HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(startup, HarmonyPatchType.Prefix, harmony.Id);
        }
        Console.WriteLine("[OK] Scrolling edits keep their own files after bad XML, new screens and reloads; audio appends sections and clears metadata across project loads.");
    }

    static void Scrolls(Assembly native, string temporary)
    {
        string folder = Path.Combine(temporary, "scrolling-save-safety");
        Directory.CreateDirectory(folder);
        string bad = Path.Combine(folder, "scroll1.xml"), good = Path.Combine(folder, "scroll02.xml");
        File.WriteAllText(bad, "<broken");
        File.WriteAllText(good, Xml(2));
        File.WriteAllText(Path.Combine(folder, "backup-scroll3.xml"), "<not-a-screen />");
        var type = native.GetType("JKWorldsmith.Models.PageModels.Screen.ScrollingImagesModel");
        object model = FormatterServices.GetUninitializedObject(type);
        Set(model, "Folder", folder);
        Set(model, "Scrollings", Activator.CreateInstance(type.GetProperty("Scrollings").PropertyType));
        int before = errors;
        Call(model, "Load");
        var screens = (IList)Get(model, "Scrollings");
        Check(errors == before + 1 && screens.Count == 1 && ((IList)Get(model, "ScrollingFiles")).Count == 1, "Bad scrolling XML did not isolate its entry");
        Check(File.ReadAllText(good) == Xml(2) && File.ReadAllText(bad) == "<broken", "Scrolling load wrote source files");
        Set(Get(screens[0], "Value"), "ScrollSpeed", 9f);
        Check(File.ReadAllText(good).Contains("<scroll_speed>9</scroll_speed>") && File.ReadAllText(bad) == "<broken", "Scrolling edit wrote another screen's XML");
        Check(!File.Exists(Path.Combine(folder, "scroll2.xml")), "Saving changed the original scrolling filename");

        object value = Get(screens[0], "Value");
        object added = Activator.CreateInstance(type.GetNestedType("ScrollingWrapper"), new[] { (object)4, value });
        ((IList)Get(model, "ScrollingFiles")).Add(Path.Combine(folder, "scroll4.xml"));
        screens.Add(added);
        Check(File.Exists(Path.Combine(folder, "scroll4.xml")), "New scrolling screen was not saved");
        Set(value, "ScrollSpeed", 7f);
        Check(File.ReadAllText(good).Contains("<scroll_speed>7</scroll_speed>") && File.ReadAllText(Path.Combine(folder, "scroll4.xml")).Contains("<scroll_speed>7</scroll_speed>"), "New wrapper lost its save binding");
        string saved = File.ReadAllText(good);
        Call(model, "Load");
        Set(value, "ScrollSpeed", 6f);
        Check(File.ReadAllText(good) == saved, "Reload left a stale list subscribed to saving");
        screens = (IList)Get(model, "Scrollings");
        object rejected = Activator.CreateInstance(type.GetNestedType("ScrollingWrapper"), new[] { (object)1, Get(screens[0], "Value") });
        bool blocked = false;
        try { screens.Add(rejected); } catch (IOException) { blocked = true; }
        Check(blocked && File.ReadAllText(bad) == "<broken", "Adding a layer overwrote an unreadable screen");
        screens.Remove(rejected);
        Call(model, "Save", model, new ListChangedEventArgs(ListChangedType.Reset, -1));
        Call(model, "Save", model, new ListChangedEventArgs(ListChangedType.ItemDeleted, 0));
        Check(File.ReadAllText(good) == saved, "List reset/deletion wrote a neighboring screen");

        File.WriteAllText(Path.Combine(folder, "scroll2.xml"), Xml(3));
        before = errors;
        Call(model, "Load");
        Check(errors == before + 2 && ((IList)Get(model, "Scrollings")).Count == 1, "Duplicate screen filenames were not rejected");
        File.Delete(Path.Combine(folder, "scroll2.xml"));
        File.WriteAllText(bad, "<ScrollingBGdata><scroll_speed>1</scroll_speed></ScrollingBGdata>");
        Call(model, "Load");
        Check(((IList)Get(model, "Scrollings")).Count == 3, "Repaired screen or omitted layers did not reload");
    }

    static object Ambience(Assembly native, string name)
    {
        object value = Activator.CreateInstance(native.GetType("JKWorldsmith.Shared.Structs.AmbienceObject"));
        Set(value, "Name", name); Set(value, "Volume", 0.7f); return value;
    }

    static void Audio(Assembly native, string temporary)
    {
        var type = native.GetType("JKWorldsmith.Models.PageModels.Screen.AudioModel");
        object model = FormatterServices.GetUninitializedObject(type);
        var sections = (IList)Activator.CreateInstance(type.GetProperty("Sections").PropertyType);
        Set(model, "Sections", sections);
        object oldSound = Ambience(native, "old-track"), newSound = Ambience(native, "new-track");
        Call(model, "SetCurrentAudio", oldSound, 1);
        object first = sections[0]; Set(first, "Screens", 5);
        Call(model, "SetCurrentAudio", newSound, 10);
        Check(sections.Count == 3 && Object.ReferenceEquals(first, sections[0]) && (int)Get(sections[1], "Screens") == 4, "Appending audio shifted existing sections");
        Check(Object.ReferenceEquals(Call(model, "GetCurrentAudio", 5), first) && Object.ReferenceEquals(Call(model, "GetCurrentAudio", 6), sections[1])
            && Object.ReferenceEquals(Call(model, "GetCurrentAudio", 10), sections[2]) && Call(model, "GetCurrentAudio", 0) == null, "Audio section boundaries are wrong");
        Check(Object.ReferenceEquals(((IList)Get(sections[2], "Ambience"))[0], newSound), "New audio lost its object binding");
        object extra = Ambience(native, "extra");
        Call(model, "SetCurrentAudio", extra, 5);
        Check(sections.Count == 3 && ((IList)Get(first, "Ambience")).Count == 2, "Adding inside an existing section changed its coverage");
        Call(model, "SetCurrentAudio", extra, 11);
        Check(sections.Count == 4 && (int)Get(sections[3], "Screens") == 1, "Adjacent audio insertion added a gap");
        Set(model, "Sections", Activator.CreateInstance(type.GetProperty("Sections").PropertyType));
        Call(model, "SetCurrentAudio", extra, 3);
        sections = (IList)Get(model, "Sections");
        Check(sections.Count == 2 && (int)Get(sections[0], "Screens") == 2, "Empty audio layout did not retain its initial silent screens");
        bool invalid = false;
        try { Call(model, "SetCurrentAudio", extra, 0); } catch (TargetInvocationException error) { invalid = error.InnerException is ArgumentOutOfRangeException; }
        Check(invalid && sections.Count == 2, "Invalid audio screen modified the layout");

        var special = type.GetField("SpecialInfo", All); var files = type.GetField("Files", All);
        object oldSpecial = special.GetValue(null), oldFiles = files.GetValue(null);
        special.SetValue(null, Activator.CreateInstance(special.FieldType));
        files.SetValue(null, Activator.CreateInstance(files.FieldType));
        try
        {
            foreach (string category in new[] { "Music", "SFX", "SFX", "Ambience" })
            {
                string folder = Path.Combine(temporary, "audio-" + category), background = Path.Combine(folder, "background");
                Directory.CreateDirectory(Path.Combine(background, "data"));
                File.WriteAllBytes(Path.Combine(background, "shared.wav"), new byte[] { 0 });
                string metadata = category == "Ambience" ? "" : "<AmbienceInfo><name>shared</name><type>" + category + "</type><restart>true</restart><fade_in_length>2</fade_in_length></AmbienceInfo>";
                string source = "<AmbienceSaveValues><special_info>" + metadata + "</special_info><sections><AmbienceSave><screens>3</screens><ambience><Ambience><name>shared</name><volume>0.5</volume></Ambience></ambience></AmbienceSave></sections></AmbienceSaveValues>";
                string path = Path.Combine(background, "data", "values.xml"); File.WriteAllText(path, source);
                Set(model, "Folder", folder); Call(model, "Load");
                // let native Load publish its new lists before inspecting them
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { }));
                var all = (IList)Get(model, "AllAudio");
                Check(all.Count == 1, "Audio fixture failed to load");
                object info = Get(all[0], "Info");
                Check(category == "Ambience" ? info == null : info != null && Get(info, "Type").ToString() == category, "Audio metadata leaked from a previous project");
                Check(((IList)special.GetValue(null)).Count == (category == "Ambience" ? 0 : 1), "Audio reload accumulated duplicate metadata");
                Check(File.ReadAllText(path) == source, "Loading audio rewrote its settings");
            }
        }
        finally { special.SetValue(null, oldSpecial); files.SetValue(null, oldFiles); }
    }
}
