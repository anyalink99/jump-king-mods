using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.Mods;
using JumpKing.Controller;

internal static class PackageTests
{
    public static int Main(string[] args)
    {
        try
        {
            // No JKRuntime reference in this executable: reproduce GetTypes
            // while the hard dependency has not yet been loaded by the game.
            string runtime = args[0];
            foreach (string path in args.Skip(1))
            {
                Assembly shell = Assembly.LoadFrom(path);
                if (shell.GetReferencedAssemblies().Any(a => a.Name == "JKRuntime" || a.Name == "UIApiPlus"))
                    throw new Exception("Native discovery shell leaked an implementation dependency: " + path);
                Type[] types = shell.GetTypes();
                var attribute = types.Select(t => (JumpKingModAttribute)Attribute.GetCustomAttribute(t, typeof(JumpKingModAttribute))).Single(a => a != null);
                ModLoader.Instance.LoadedMods.Add(new ModAssembly(shell, attribute));
            }
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "JKRuntime"))
                throw new Exception("Runtime was pulled in during native discovery");
            Assembly loaded = Assembly.LoadFrom(runtime);
            Type host = loaded.GetType("JKRuntime.PackageHost", true);
            // Open Controls+ before another feature has discovered SDK packages.
            var manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager,
                new List<PadInstance> { (PadInstance)FormatterServices.GetUninitializedObject(typeof(PadInstance)) });
            ControllerManager.instance = manager;
            var automatic = loaded.GetType("JKRuntime.UI.AutomaticBindings", true);
            var refresh = automatic.GetMethod("Refresh", BindingFlags.Static | BindingFlags.NonPublic);
            refresh.Invoke(null, null);
            host.GetMethod("Discover").Invoke(null, null);
            host.GetMethod("Discover").Invoke(null, null);
            string[] errors = (string[])host.GetProperty("Errors").GetValue(null, null);
            if (errors.Length != 0) throw new Exception(string.Join("\n", errors));
            var modules = (Array)loaded.GetType("JKRuntime.RuntimeApi").GetMethod("GetModules").Invoke(null, null);
            if (modules.Length != args.Length - 1) throw new Exception("Missing/duplicate discovered modules");
            TestPackagedBindings(loaded, refresh);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.EndsWith(".Module")))
            foreach (var type in assembly.GetTypes())
            {
                var create = type.GetMethod("CreateStateParticipant", BindingFlags.Instance | BindingFlags.NonPublic);
                if (create == null) continue;
                var instance = FormatterServices.GetUninitializedObject(type);
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    if (field.FieldType.Assembly == assembly && field.FieldType.IsClass && !field.FieldType.IsAbstract)
                        field.SetValue(instance, FormatterServices.GetUninitializedObject(field.FieldType));
                var participant = create.Invoke(instance, null);
                var snapshot = participant.GetType().GetMethod("Capture").Invoke(participant, null);
                participant.GetType().GetMethod("Validate").Invoke(participant, new[] { snapshot });
                Console.WriteLine("[OK] Actual module state schema: " + type.FullName);
            }
            Console.WriteLine("[OK] Adverse native discovery + idempotent runtime package load: " + (args.Length - 1) + " modules");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void TestPackagedBindings(Assembly runtime, MethodInfo refresh)
    {
        Assembly ball = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "MorphBall.Module");
        if (ball == null) return;
        // Supply in-memory settings: this test must never read/write player settings.
        var holder = ball.GetType("MorphBallMod.SettingsStore", true);
        var current = holder.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic);
        var settingsType = ball.GetType("MorphBallMod.MorphBallSettings", true);
        current.SetValue(null, Activator.CreateInstance(settingsType), null);
        string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "binding-fixture-" + Guid.NewGuid().ToString("N") + ".xml");
        var defaults = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(settingsType),
            typeof(PackageTests).GetMethod("Defaults", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(settingsType));
        var file = Activator.CreateInstance(runtime.GetType("JKRuntime.Settings.SettingsFile`1", true).MakeGenericType(settingsType),
            new object[] { settingsPath, defaults, null });
        holder.GetField("file", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, file);
        holder.GetField("loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        var uiSettings = runtime.GetType("JKRuntime.UI.SettingsStore", true);
        var uiValue = Activator.CreateInstance(runtime.GetType("JKRuntime.UI.UIApiSettings", true));
        uiSettings.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, uiValue, null);
        uiSettings.GetField("loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        refresh.Invoke(null, null);
        var api = runtime.GetType("JKRuntime.UI.UIApi", true);
        Func<object[]> bindings = delegate { return ((IEnumerable)api.GetMethod("GetBindings").Invoke(null, null)).Cast<object>().ToArray(); };
        Func<object, string> id = b => (string)b.GetType().GetProperty("Id").GetValue(b, null);
        object binding = bindings().Single(b => id(b) == "auto.MorphBall.Morph");
        var read = (Delegate)binding.GetType().GetProperty("GetBindings").GetValue(binding, null);
        if (!((int[])read.DynamicInvoke()).SequenceEqual(new[] { 77 })) throw new Exception("Packaged Morph default binding missing");
        var replacement = Activator.CreateInstance(settingsType);
        var dictionary = (IDictionary)settingsType.GetProperty("KeyBindings").GetValue(replacement, null);
        dictionary[dictionary.Keys.Cast<object>().Single()] = new[] { 78 };
        current.SetValue(null, replacement, null);
        for (int i = 0; i < 3; i++) refresh.Invoke(null, null);
        if (!((int[])read.DynamicInvoke()).SequenceEqual(new[] { 78 })) throw new Exception("Packaged binding kept stale settings");
        if (bindings().Count(b => id(b) == "auto.MorphBall.Morph") != 1 || bindings().Any(b => id(b) == "auto.MorphBall.Module.Morph"))
            throw new Exception("Packaged binding duplicated or changed its saved identity");
        // A saved Controls+ assignment must still be found by the pre-SDK ID.
        var entry = Activator.CreateInstance(runtime.GetType("JKRuntime.UI.UiChordSettingsEntry", true));
        entry.GetType().GetProperty("Id").SetValue(entry, "auto.MorphBall.Morph", null);
        entry.GetType().GetProperty("Chords").SetValue(entry, new[] { new[] { 79 } }, null);
        var entries = Array.CreateInstance(entry.GetType(), 1); entries.SetValue(entry, 0);
        uiValue.GetType().GetProperty("BindingChords").SetValue(uiValue, entries, null);
        if (!((int[])read.DynamicInvoke()).SequenceEqual(new[] { 79 })) throw new Exception("Saved Morph binding was lost");
        runtime.GetType("JKRuntime.UI.AutomaticBindings").GetMethod("Disable", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        if (File.Exists(settingsPath)) throw new Exception("Discovery-only shutdown rewrote native bindings without an active device");
        Console.WriteLine("[OK] Packaged Morph binding: late settings, stable saved ID, live replacement, no duplicates");
    }
    private static T Defaults<T>() where T : class, new() { return new T(); }
}
