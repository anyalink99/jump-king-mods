using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Serialization;

internal static class InstalledBindingModeTests
{
    private static void Main(string[] args)
    {
        var roots = new[] { Path.GetDirectoryName(args[0]), args[1] }.Concat(args.Skip(2).Select(Path.GetDirectoryName)).ToArray();
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string root in roots) { string path = Path.Combine(root, name); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        var game = Assembly.LoadFrom(Path.Combine(args[1], "JumpKing.exe"));
        var engine = Assembly.LoadFrom(args[2]); var harmony = engine.GetType("HarmonyLib.Harmony"); var metadata = engine.GetType("HarmonyLib.HarmonyMethod");
        var owner = Activator.CreateInstance(harmony, new object[] { "fixture.installed-binding-modes" });
        var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
        var update = game.GetType("JumpKing.Controller.ControllerManager").GetMethod("Update");
        string copy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed-context", "JKRuntime.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(args[0], copy, true);
        var runtime = Assembly.LoadFrom(copy);
        var discovery = runtime.GetType("JKRuntime.UI.BindingModeDiscovery", true);
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var analyze = discovery.GetMethod("Analyze", flags); var edges = discovery.GetMethod("ReadsOnlyEdges", flags);
        int holds = 0, presses = 0;
        foreach (string file in args.Skip(3))
        {
            var assembly = Assembly.LoadFrom(file); var types = assembly.GetTypes();
            // install only the polling patches; no mod initialization or gameplay callbacks run
            var writers = types.SelectMany(t => t.GetMethods(flags)).Where(m => m.IsStatic && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType.FullName == "JumpKing.Controller.ControllerManager").ToArray();
            foreach (var writer in writers) patch.Invoke(owner, new[] { (object)update, null,
                Activator.CreateInstance(metadata, new object[] { writer }), null, null });
            foreach (var type in types)
            foreach (var property in type.GetProperties(flags).Where(p => p.Name == "KeyBindings" && p.PropertyType.IsGenericType))
            {
                var key = property.PropertyType.GetGenericArguments()[0]; if (!key.IsEnum) continue;
                foreach (object value in Enum.GetValues(key))
                {
                    var field = analyze.Invoke(null, new object[] { assembly, property, value, writers });
                    bool edge = field == null && (bool)edges.Invoke(null, new object[] { assembly, property, value });
                    if (field != null) holds++; if (edge) presses++;
                    var mode = discovery.GetMethod("Discover", flags).Invoke(null, new object[] { "fixture." + value, assembly, property, value });
                    if ((field != null || edge) && mode == null) throw new Exception("Production discovery lost the inferred mode");
                    if (mode != null && (bool)mode.GetType().GetProperty("CanChange").GetValue(mode, null) != (field != null))
                        throw new Exception("Production discovery exposed the wrong alternatives");
                    if (field != null) {
                        var activation = runtime.GetType("JKRuntime.UI.BindingActivation");
                        var entries = (System.Collections.IDictionary)activation.GetField("foreign", flags).GetValue(null);
                        var entry = entries["fixture." + value];
                        var valid = (Func<bool>)entry.GetType().GetField("Valid", flags).GetValue(entry);
                        if (!valid()) throw new Exception("Unchanged live patch graph must remain valid");
                        patch.Invoke(owner, new[] { (object)update, null, Activator.CreateInstance(metadata,
                            new object[] { typeof(InstalledBindingModeTests).GetMethod("LaterPatch", flags) }), null, null });
                        if (valid()) throw new Exception("Changed live patch graph must invalidate conversion");
                    }
                    Console.WriteLine(assembly.GetName().Name + ": " + value + " = " + (field != null ? "Hold (Press supported)" : edge ? "Press (fixed)" : "Unknown (fixed)"));
                }
            }
        }
        if (holds == 0 || presses == 0) throw new Exception("Installed fixtures must cover both inferred Hold and fixed Press");
        Context(game, runtime, engine, args.Skip(3).Select(Assembly.LoadFrom).ToArray());
    }
    private static bool AlwaysAvailable(out bool __result) { __result = true; return false; }
    private static void Context(Assembly game, Assembly runtime, Assembly engine, Assembly[] providers)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var jump = game.GetType("JumpKing.Player.JumpState");
        var prefix = providers.SelectMany(a => a.GetTypes()).SelectMany(t => t.GetMethods(flags)).Single(m => m.ReturnType == typeof(bool)
            && m.GetParameters().Any(p => p.ParameterType == jump) && m.GetParameters().Any(p => p.ParameterType.FullName == "BehaviorTree.BTresult&"));
        var analyzer = runtime.GetType("JKRuntime.UI.BindingContextIl"); var parsed = new object[] { prefix, null };
        if (!(bool)analyzer.GetMethod("TryGuard", flags).Invoke(null, parsed)) throw new Exception("Installed conditional jump prefix wasn't covered: " + analyzer.GetField("LastRefusal", flags).GetValue(null));
        var block = ((Type[])parsed[1]).Single(t => t.Name == "AutoJumpCharge");
        var handlerType = block.Assembly.GetTypes().Single(t => t.Name == "AutoJumpCharge" && t.GetInterfaces().Any(i => i.Name == "IBlockBehaviour"));
        var handler = FormatterServices.GetUninitializedObject(handlerType);
        var activeFlag = handlerType.GetField("<IsPlayerOnBlock>k__BackingField", flags);
        var bodyType = game.GetType("JumpKing.Player.BodyComp"); var body = FormatterServices.GetUninitializedObject(bodyType);
        var lookupField = bodyType.GetField("m_blockBehaviourLookup", flags);
        var lookup = (System.Collections.IDictionary)Activator.CreateInstance(lookupField.FieldType); lookup.Add(block, handler); lookupField.SetValue(body, lookup);
        var playerType = game.GetType("JumpKing.Player.PlayerEntity"); var player = FormatterServices.GetUninitializedObject(playerType);
        playerType.GetField("m_body", flags).SetValue(player, body); game.GetType("JumpKing.GameManager.GameLoop").GetField("m_player", flags).SetValue(null, player);
        var harmony = engine.GetType("HarmonyLib.Harmony"); var metadata = engine.GetType("HarmonyLib.HarmonyMethod");
        var owner = Activator.CreateInstance(harmony, new object[] { "fixture.installed-context" });
        var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
        patch.Invoke(owner, new[] { (object)jump.GetMethod("MyRun", flags), Activator.CreateInstance(metadata, new object[] { prefix }), null, null, null });
        var walkPrefix = prefix.DeclaringType.Assembly.GetType("JumpKing_Expansion_Blocks.Patches.PatchedWalk").GetMethod("PrefixRun", flags);
        parsed = new object[] { walkPrefix, null };
        if (!(bool)analyzer.GetMethod("TryGuard", flags).Invoke(null, parsed)) throw new Exception("Installed conditional walking prefix wasn't covered");
        var walkBlock = ((Type[])parsed[1]).Single(t => t.Name == "RevokeWalking");
        var walkHandlerType = walkBlock.Assembly.GetTypes().Single(t => t.Name == "RevokeWalking" && t.GetInterfaces().Any(i => i.Name == "IBlockBehaviour"));
        var walkHandler = FormatterServices.GetUninitializedObject(walkHandlerType);
        var walkFlag = walkHandlerType.GetField("<IsPlayerOnBlock>k__BackingField", flags); lookup.Add(walkBlock, walkHandler);
        patch.Invoke(owner, new[] { (object)game.GetType("JumpKing.Player.Walk").GetMethod("MyRun", flags), Activator.CreateInstance(metadata, new object[] { walkPrefix }), null, null, null });
        var gravity = providers.Select(a => a.GetType("UpsideDownCore.Patching.Walk")).FirstOrDefault(t => t != null);
        if (gravity != null) Activator.CreateInstance(gravity, flags, null, new[] { owner }, null);
        var context = runtime.GetType("JKRuntime.UI.BindingConversionContext"); context.GetMethod("Prepare", flags).Invoke(null, null);
        var reason = context.GetMethod("Reason", flags);
        foreach (string id in new[] { "jump-king.left", "jump-king.right", "jump-king.jump" })
            if (reason.Invoke(null, new object[] { id }) != null) throw new Exception("Installed dormant patches disabled plain-ground input: " + id + ": " + reason.Invoke(null, new object[] { id }));
        patch.Invoke(owner, new[] { (object)jump.GetMethod("MyRun", flags), null, Activator.CreateInstance(metadata,
            new object[] { typeof(InstalledBindingModeTests).GetMethod("LaterPatch", flags) }), null, null });
        if (reason.Invoke(null, new object[] { "jump-king.jump" }) == null) throw new Exception("Patch mutation wasn't observed");
        context.GetMethod("FinalizeOwnHooks", flags).Invoke(null, null);
        if (reason.Invoke(null, new object[] { "jump-king.jump" }) != null) throw new Exception("Late native startup observer permanently disabled Jump");
        if (reason.Invoke(null, new object[] { "jump-king.jump" }) != null) throw new Exception("Dormant mechanic disabled Jump conversion: " + reason.Invoke(null, new object[] { "jump-king.jump" }));
        activeFlag.SetValue(handler, true);
        if (reason.Invoke(null, new object[] { "jump-king.jump" }) == null) throw new Exception("Active automatic-charge mechanic retained toggle Jump");
        foreach (string id in new[] { "jump-king.left", "jump-king.right", "jump-king.boots", "fixture.Sprint" })
            if (reason.Invoke(null, new object[] { id }) != null) throw new Exception("Jump mechanic disabled unrelated action: " + id);
        activeFlag.SetValue(handler, false);
        walkFlag.SetValue(walkHandler, true);
        if (reason.Invoke(null, new object[] { "jump-king.left" }) == null || reason.Invoke(null, new object[] { "jump-king.right" }) == null
            || reason.Invoke(null, new object[] { "jump-king.jump" }) != null) throw new Exception("Walking restriction wasn't isolated from Jump");
        walkFlag.SetValue(walkHandler, false);
        var activation = runtime.GetType("JKRuntime.UI.BindingActivation");
        patch.Invoke(owner, new[] { (object)activation.GetProperty("Available", flags).GetGetMethod(true),
            Activator.CreateInstance(metadata, new object[] { typeof(InstalledBindingModeTests).GetMethod("AlwaysAvailable", flags) }), null, null, null });
        var managerType = game.GetType("JumpKing.Controller.ControllerManager"); var manager = FormatterServices.GetUninitializedObject(managerType);
        managerType.GetField("instance", flags).SetValue(null, manager);
        var device = Activator.CreateInstance(game.GetType("JumpKing.Controller.KeyboardPad"), true);
        var padType = game.GetType("JumpKing.Controller.PadInstance"); var pad = Activator.CreateInstance(padType, new[] { device });
        var padsField = managerType.GetField("m_pads", flags); var pads = (System.Collections.IList)Activator.CreateInstance(padsField.FieldType); pads.Add(pad); padsField.SetValue(manager, pads);
        managerType.GetField("_current_main", flags).SetValue(manager, pad);
        var menu = Activator.CreateInstance(game.GetType("JumpKing.Controller.MenuController"), new[] { manager }); managerType.GetField("_menu_controller", flags).SetValue(manager, menu);
        var settings = runtime.GetType("JKRuntime.UI.SettingsStore"); var modeType = runtime.GetType("JKRuntime.UI.UiBindingMode");
        settings.GetMethod("SetBindingMode", flags).Invoke(null, new object[] { "jump-king.jump", Enum.Parse(modeType, "Press") });
        settings.GetMethod("SetBindingMode", flags).Invoke(null, new object[] { "jump-king.left", Enum.Parse(modeType, "Press") });
        var current = padType.GetField("current_state", flags); var previous = padType.GetField("last_state", flags);
        Action<bool, bool> tick = (down, surface) => {
            activation.GetMethod("BeforePoll", flags).Invoke(null, null);
            previous.SetValue(pad, current.GetValue(pad)); var state = Activator.CreateInstance(current.FieldType);
            current.FieldType.GetField("jump").SetValue(state, down); current.FieldType.GetField("left").SetValue(state, down); current.SetValue(pad, state);
            activation.GetMethod("AfterPoll", flags).Invoke(null, null);
            activeFlag.SetValue(handler, surface); activation.GetMethod("AfterBody", flags).Invoke(null, null);
        };
        Func<string, bool> held = name => (bool)current.FieldType.GetField(name).GetValue(current.GetValue(pad));
        tick(false, false); tick(true, false); tick(false, true);
        if (held("jump") || !held("left")) throw new Exception("First-contact handoff must clear Jump while retaining unrelated movement latch");
        tick(true, true); if (!held("jump")) throw new Exception("Automatic charge lost its physical release press");
        tick(false, true); if (held("jump")) throw new Exception("Automatic charge retained an obsolete toggle");
        tick(false, false); tick(false, false); tick(true, false); tick(false, false);
        if (!held("jump")) throw new Exception("Ordinary ground failed to re-arm toggle Jump");
        tick(true, false); tick(false, false);
        walkFlag.SetValue(walkHandler, true); activation.GetMethod("AfterBody", flags).Invoke(null, null);
        if (held("left")) throw new Exception("Current-frame walking restriction retained the movement latch");
        walkFlag.SetValue(walkHandler, false);
        patch.Invoke(owner, new[] { (object)jump.GetMethod("MyRun", flags), Activator.CreateInstance(metadata,
            new object[] { typeof(InstalledBindingModeTests).GetMethod("ReplaceInput", flags) }), null, null, null });
        context.GetMethod("FinalizeOwnHooks", flags).Invoke(null, null);
        if (reason.Invoke(null, new object[] { "jump-king.jump" }) == null || reason.Invoke(null, new object[] { "jump-king.left" }) != null)
            throw new Exception("Finalization trusted an uncovered input replacement or blocked unrelated movement");
        context.GetMethod("Clear", flags).Invoke(null, null);
        Console.WriteLine("[OK] Installed automatic charge and no-walking: dormant/active predicates, action isolation, same-frame first landing, physical presses and re-arm; no callback replay");
    }
    private static void LaterPatch() { }
    private static bool ReplaceInput() { return false; }
}
