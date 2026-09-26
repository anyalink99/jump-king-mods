using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using JKRuntime.Compatibility;
using JumpKing;
using JumpKing.MiscEntities.OldMan;
using JumpKing.Props.RattmanText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JKRuntime
{
    internal static class MoreTextOptionsTests
    {
        private static Type harmony, harmonyMethod;
        private static MethodInfo target, original;
        private static int peerCalls;
        private static object Member(object obj, string name)
        { var f = obj.GetType().GetField(name); return f != null ? f.GetValue(obj) : obj.GetType().GetProperty(name).GetValue(obj, null); }
        private static object[] Postfixes()
        {
            var info = harmony.GetMethod("GetPatchInfo").Invoke(null, new object[] { target });
            return info == null ? new object[0] : ((IEnumerable)Member(info, "Postfixes")).Cast<object>().ToArray();
        }
        private static MethodInfo Hook(string name) { return typeof(MoreTextOptionsTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic); }
        private static void Patch(string owner, MethodInfo method, int priority, bool prefix)
        {
            var hm = Activator.CreateInstance(harmonyMethod, new object[] { method });
            harmonyMethod.GetField("priority").SetValue(hm, priority);
            if (method == original)
            {
                harmonyMethod.GetField("before").SetValue(hm, new[] { "fixture.peer" });
                harmonyMethod.GetField("after").SetValue(hm, new[] { "fixture.earlier" });
            }
            harmony.GetMethod("Patch").Invoke(Activator.CreateInstance(harmony, new object[] { owner }),
                new object[] { target, prefix ? hm : null, prefix ? null : hm, null, null });
        }
        private static bool SkipNative(ref BTresult __result) { __result = BTresult.Success; return false; }
        private static void Peer() { peerCalls++; }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static Entity MakeEntity(string name, OldManFont font, int width, string text)
        {
            var type = typeof(TargetLine).Assembly.GetType(name, true);
            var entity = (Entity)FormatterServices.GetUninitializedObject(type);
            typeof(Entity).GetField("m_components", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, new List<Component>());
            object settings = name.Contains("Rattman") ? (object)new RattmanSettings { font = font, bubble_format = new SpeechBubbleFormat { width = width } }
                : new OldManSettings { font = font, bubble_format = new SpeechBubbleFormat { width = width } };
            type.GetField("m_settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, settings);
            var board = (Component)Activator.CreateInstance(typeof(Entity).Assembly.GetType("EntityComponent.BlackBoardComp"), true);
            entity.AddComponents(board);
            ((Dictionary<string, object>)board.GetType().GetField("m_values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board))["BB_LINE_KEY"] = text;
            return entity;
        }
        private static SpriteFont MakeFont(int advance)
        {
            var chars = Enumerable.Range(32, 95).Select(i => (char)i).ToList();
            return new SpriteFont(null, chars.Select(c => new Rectangle(0, 0, advance, 10)).ToList(),
                chars.Select(c => new Rectangle(0, 0, advance, 10)).ToList(), chars, 12, 0,
                chars.Select(c => new Vector3(0, advance, 0)).ToList(), null);
        }
        private static void ExerciseFormatting()
        {
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            typeof(Game1).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, game);
            game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
            game.contentManager.font = new JKContentManager.Font { StyleFont = MakeFont(5), GargoyleFont = MakeFont(9) };
            int cases = 0;
            foreach (string type in new[] { "JumpKing.MiscEntities.OldManEntity", "JumpKing.Props.RattmanText.RattmanEntity" })
            foreach (OldManFont choice in new[] { OldManFont.Default, OldManFont.Gargoyle })
            foreach (int width in new[] { 25, 75, 400 })
            foreach (string text in new[] { "", "alpha beta gamma delta", "long_unbroken_text", "multiple  spaces here" })
            {
                var entity = MakeEntity(type, choice, width, text);
                var node = new TargetLine(entity);
                var board = entity.GetComponents()[0];
                var values = (Dictionary<string, object>)board.GetType().GetField("m_values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
                var font = choice == OldManFont.Default ? game.contentManager.font.StyleFont : game.contentManager.font.GargoyleFont;
                string expected = string.Join("", SpeechBubbleFormat.ChopString(text, font, width));
                Check((BTresult)target.Invoke(node, new object[] { null }) == BTresult.Success, "Native result preserved");
                Check((string)values["BB_LINE_KEY"] == expected, "Dialogue parity: " + type + "/" + choice + "/" + width);
                cases++;
            }
            Check(peerCalls == cases, "Unrelated postfix still runs exactly once per dialogue");
            target.Invoke(new TargetLine(new Entity()), new object[] { null }); // Unrelated entity must be ignored.
            Check(peerCalls == cases + 1, "Unrelated entity path remains operational");
        }
        private static void Main(string[] args)
        {
            try
            {
                MoreTextOptionsCompatibility.TryInstall();
                Check(MoreTextOptionsCompatibility.Status.StartsWith("Not needed:"), "Absent mod is non-mutating");
                var engine = Assembly.LoadFrom(args[0]);
                harmony = engine.GetType("HarmonyLib.Harmony", true); harmonyMethod = engine.GetType("HarmonyLib.HarmonyMethod", true);
                string mode = args.Length > 2 ? args[2] : "normal";
                string modPath = args[1];
                if (mode == "unknown")
                {
                    // Valid assembly with an unreviewed file hash, generated
                    // only in the test output directory. Never edit Workshop.
                    string directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "moretext-unknown-fixture");
                    System.IO.Directory.CreateDirectory(directory);
                    modPath = System.IO.Path.Combine(directory, "MoreTextOptions.dll");
                    System.IO.File.WriteAllBytes(modPath, System.IO.File.ReadAllBytes(args[1]).Concat(new byte[] { 0 }).ToArray());
                }
                var mod = Assembly.LoadFrom(modPath);
                target = typeof(TargetLine).GetMethod("MyRun", BindingFlags.Instance | BindingFlags.NonPublic);
                original = mod.GetType("MoreTextOptions.Patches.PatchTargetLine").GetMethod("Postfix");
                bool compatible = engine.GetType("HarmonyLib.AccessTools").GetMethods().Any(m => m.Name == "MethodDelegate" && m.IsGenericMethodDefinition &&
                    m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(MethodInfo), typeof(object), typeof(bool) }));
                if (mode == "unknown")
                { MoreTextOptionsCompatibility.TryInstall(); Check(MoreTextOptionsCompatibility.Status.StartsWith("Unsupported:"), "Unreviewed build refused"); return; }
                MoreTextOptionsCompatibility.TryInstall(); // Runtime before foreign BeforeLevelLoad.
                if (!compatible) Check(MoreTextOptionsCompatibility.Status.StartsWith("Waiting:"), "Late patch not mistaken for installed adapter");
                Patch("Zebra.MoreTextOptions.Harmony", original, 400, false);
                Patch("fixture.native-skip", Hook("SkipNative"), 800, true);
                Patch("fixture.peer", Hook("Peer"), mode == "ordering" ? 400 : 100, false);
                if (mode == "mixed") Assembly.LoadFile(System.IO.Path.GetFullPath(args[3]));
                MoreTextOptionsCompatibility.TryInstall();
                if (mode == "ordering" || mode == "mixed")
                {
                    Check(!MoreTextOptionsCompatibility.Status.StartsWith("Active:"), "Ambiguous compatibility refused");
                    Check(Postfixes().Any(p => Equals(Member(p, "PatchMethod"), original)), "Original retained on refusal");
                    Check(Postfixes().Length == 2, "No leaked replacement on refusal");
                    Console.WriteLine("[OK] MoreTextOptions non-mutating refusal: " + mode); return;
                }
                Check(MoreTextOptionsCompatibility.Status.StartsWith(compatible ? "Not needed:" : "Active:"), MoreTextOptionsCompatibility.Status);
                Check(Postfixes().Length == 2, "Only original registration replaced");
                var owned = Postfixes().Single(p => Equals(Member(p, "owner"), "Zebra.MoreTextOptions.Harmony"));
                Check((int)Member(owned, "priority") == 400 &&
                    ((string[])Member(owned, "before")).SequenceEqual(new[] { "fixture.peer" }) &&
                    ((string[])Member(owned, "after")).SequenceEqual(new[] { "fixture.earlier" }), "Owner and ordering metadata preserved");
                Check(Postfixes().Any(p => Equals(Member(p, "PatchMethod"), original)) == compatible, "Correct ABI selection");
                MoreTextOptionsCompatibility.TryInstall(); Check(Postfixes().Length == 2, "Repeated installation is idempotent");
                ExerciseFormatting();
                if (!compatible)
                {
                    // MTO PatchAll executes again on every level load.
                    Patch("Zebra.MoreTextOptions.Harmony", original, 400, false);
                    MoreTextOptionsCompatibility.TryInstall();
                    Check(Postfixes().Length == 2 && !Postfixes().Any(p => Equals(Member(p, "PatchMethod"), original)), "Repatch after level reload");
                    peerCalls = 0; ExerciseFormatting();
                    // Prove this is the unmodified, still-incompatible DLL.
                    bool failed = false;
                    try { RuntimeHelpers.RunClassConstructor(original.DeclaringType.TypeHandle); }
                    catch (TypeInitializationException ex) { failed = ex.InnerException is MissingMethodException; }
                    Check(failed, "Original DLL remains unchanged and reproduces its initializer failure");
                    peerCalls = 0; ExerciseFormatting(); // No dependency on poisoned foreign static fields.
                }
                Console.WriteLine("[OK] MoreTextOptions Harmony " + engine.GetName().Version + ": native NPC/Rattman formatting, both fonts, peer coexistence, retry/reload, original DLL untouched");
            }
            catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
        }
    }
}
