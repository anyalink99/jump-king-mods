using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Gameplay;
using JKRuntime.Settings;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.Mods;
using JumpKing.Player;
using JumpKing.SaveThread.SaveComponents;

namespace JKRuntime
{
    internal static class ForeignModifierTests
    {
        private static Type fixture, harmony, harmonyMethod;
        private static object foreignOwner;
        private static int index;
        private static string directory;
        private sealed class Behaviour : IBodyCompBehaviour
        { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void SetNativeSave()
        {
            var loop = new GameLoop();
            var completion = typeof(GameLoop).GetField("m_ending_body_modifiers", BindingFlags.Instance | BindingFlags.NonPublic);
            completion.SetValue(loop, Activator.CreateInstance(completion.FieldType, true));
            typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube").GetProperty("CombinedSave").SetValue(null,
                new JumpKing.SaveThread.CombinedSaveFile { full_run = new SaveCompCushion<FullRunSave> { initialized = true } }, null);
        }
        private static BodyComp NewBody()
        {
            RunModifiers.Finish(); SetNativeSave();
            var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
            var list = new LinkedList<IBodyCompBehaviour>(); list.AddLast(new Behaviour());
            typeof(BodyComp).GetField("m_behaviours", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(body, list);
            return body;
        }
        private static void Begin(BodyComp body)
        { RunModifiers.Begin(body, "run-" + (++index), RunModifiers.ReadNativePeak(), Path.Combine(directory, index + ".xml")); }
        private static IBodyCompBehaviour Create() { return (IBodyCompBehaviour)fixture.GetMethod("Create").Invoke(null, null); }
        private static bool Call(string method, params object[] args) { return (bool)fixture.GetMethod(method).Invoke(null, args); }
        private static void Patch(string method, string prefix, string postfix)
        {
            var patch = harmony.GetMethod("Patch");
            object pre = prefix == null ? null : Activator.CreateInstance(harmonyMethod, new object[] { fixture.GetMethod(prefix) });
            object post = postfix == null ? null : Activator.CreateInstance(harmonyMethod, new object[] { fixture.GetMethod(postfix) });
            patch.Invoke(foreignOwner, new object[] { typeof(BodyComp).GetMethod(method), pre, post, null, null });
        }
        private static void Unpatch(string target, string hook)
        { harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(foreignOwner, new object[] { typeof(BodyComp).GetMethod(target), fixture.GetMethod(hook) }); }
        private static Assembly PatchSecondEngine(string path)
        {
            var second = Assembly.LoadFile(Path.GetFullPath(path));
            var secondType = second.GetType("HarmonyLib.Harmony", true);
            var secondMethod = second.GetType("HarmonyLib.HarmonyMethod", true);
            var secondOwner = Activator.CreateInstance(secondType, new object[] { "foreign.second-engine" });
            var hook = Activator.CreateInstance(secondMethod, new object[] { fixture.GetMethod("Touch") });
            secondType.GetMethod("Patch").Invoke(secondOwner, new object[] { typeof(BodyComp).GetMethod("RegisterBehaviourAfter"), hook, null, null, null });
            return second;
        }
        private static string Signature(Assembly assembly)
        { return (string)typeof(ModifierRegistrationObserver).GetMethod("ExistingPatches", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { assembly }); }
        private static void ExpectForeignThrow(Action call)
        {
            try { call(); } catch (Exception e) { Check(e.GetBaseException().Message == "Foreign fixture failure", "Original exception preserved"); return; }
            throw new Exception("Foreign exception swallowed");
        }
        private static void NativeResetOnWorker(MethodInfo reset)
        {
            string fixtureRoot = Path.Combine(directory, "reset-fixture");
            string content = Path.Combine(fixtureRoot, "Content", "props", "worlditems");
            Directory.CreateDirectory(content);
            using (var writer = new StreamWriter(Path.Combine(content, "worlditems.xml")))
                new System.Xml.Serialization.XmlSerializer(typeof(WorldItemsSave)).Serialize(writer, new WorldItemsSave());
            string prior = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(fixtureRoot);
                Exception failure = null;
                var worker = new System.Threading.Thread(delegate() { try { reset.Invoke(null, null); } catch (Exception error) { failure = error; } });
                worker.Start();
                Check(worker.Join(5000), "Native save worker completed");
                if (failure != null) throw failure;
            }
            finally { Directory.SetCurrentDirectory(prior); }
        }
        private static void Exercise()
        {
            var body = NewBody(); var marker = Create();
            Call("Add", body, marker); Call("Remove", body, marker); // before Runtime's OnLevelStart
            Begin(body);
            Check(RunModifiers.GetContributors().SequenceEqual(new[] { "Third-party fixture mod" }), "Early same-tick marker uses ModLoader name, not DLL title, with no false unknown");
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 0, "Removed early marker no longer active");
            var own = new Behaviour();
            RunModifiers.Register(body, own);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 1, "Explicit API and Harmony don't double count");
            RunModifiers.Remove(body, own);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 0 && !RunModifiers.GetContributors().Any(n => n.StartsWith("Unknown")), "Paired explicit remove doesn't produce false unknown");
            RunModifiers.Finish();
            Check(RunModifiers.GetContributors().Contains("Third-party fixture mod"), "End preserves foreign cause");

            foreach (string method in new[] { "Before", "After", "Add" })
            {
                body = NewBody(); Begin(body); marker = Create(); var anchor = body.GetBehaviourList().First();
                Check(method == "Add" ? Call(method, body, marker) : Call(method, body, marker, anchor), "Native registration successful");
                Check(RunModifiers.GetContributors().Single() == "Third-party fixture mod", "Direct DLL call attributed for " + method);
                Call("Remove", body, marker);
                Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 0 && body.GetBehaviourList().Count == 1, "Native list and removal preserved");
                Check(RunModifiers.WaitForWrites(5000), "Foreign evidence background write completed");
                var saved = AtomicXmlFile.Load<RunModifierRecord>(Path.Combine(directory, index + ".xml"));
                Check(saved.Sources.Count == 1 && !saved.Unknown && saved.NativePeak == 1, "Foreign evidence persisted");
            }
            body = NewBody(); Begin(body); marker = Create();
            Check(!Call("Before", body, marker, Create()) && RunModifiers.GetContributors().Length == 0, "Failed anchor adds no contributor");
            var allowed = new BodyPipeline(body, false);
            allowed.RegisterBefore(marker, body.GetBehaviourList().First());
            RunModifiers.Observe();
            Check(RunModifiers.GetContributors().Length == 0, "Map-allowed foreign behaviour is not accused");
            // Same object in an unmarked slot must not be counted twice by the two observers.
            RunModifiers.Register(body, marker);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 1, "Mixed marked/unmarked duplicate identity only tracks marked registration");
            RunModifiers.Remove(body, marker);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 0, "Duplicate-identity removal does not leave a phantom tracked count");

            body = NewBody(); Begin(body); marker = Create();
            Call("Add", body, marker); Call("Add", body, marker);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 2, "Native permits duplicate registrations; multiplicity tracked");
            Call("Remove", body, marker); Call("Remove", body, marker);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 0 && RunModifiers.GetContributors().Length == 1, "Duplicates removed; historical source once");

            body = NewBody(); Begin(body);
            Call("Add", body, new Behaviour());
            Check(RunModifiers.GetContributors().Single().StartsWith("Unknown mod (behaviour DLL:"), "Shared/unmapped DLL is not invented as a mod author");

            body = NewBody(); Begin(body); marker = Create();
            Patch("RegisterBehaviourBefore", null, "Nested");
            RunModifiers.RegisterBefore(body, marker, body.GetBehaviourList().First());
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 2 && !RunModifiers.GetContributors().Any(n => n.StartsWith("Unknown")), "Nested foreign registration and explicit wrapper compose without double counting");
            Unpatch("RegisterBehaviourBefore", "Nested");

            body = NewBody(); Begin(body); marker = Create();
            Patch("RegisterBehaviourBefore", "Skip", null);
            Check(Call("Before", body, marker, body.GetBehaviourList().First()), "Competing prefix result preserved");
            Check(RunModifiers.GetContributors().Length == 0 && body.GetBehaviourList().Count == 1, "Skipped original returning true is not evidence");
            ModifierRegistrationObserver.TryInstall();
            Check(Call("Before", body, marker, body.GetBehaviourList().First()) && body.GetBehaviourList().Count == 1, "Retry leaves foreign prefix intact");
            Unpatch("RegisterBehaviourBefore", "Skip");
            Patch("RegisterBehaviourBefore", "Throw", null);
            ExpectForeignThrow(() => Call("Before", body, marker, body.GetBehaviourList().First()));
            Check(ModifierRegistrationEvidence.Get(body).Depth == 0, "Prefix failure closes observation");
            Unpatch("RegisterBehaviourBefore", "Throw");
            Check(Call("Before", body, marker, body.GetBehaviourList().First()), "Observer survives foreign failure");
            Check(RunModifiers.GetContributors().Single() == "Third-party fixture mod", "Direct registration remains observed after foreign repatching");

            body = NewBody(); Begin(body); marker = Create();
            Patch("RegisterBehaviourBefore", null, "Throw");
            ExpectForeignThrow(() => Call("Before", body, marker, body.GetBehaviourList().First()));
            Check(ModifierRegistrationEvidence.Get(body).Depth == 0 && body.GetBehaviourList().Count == 2, "Throwing postfix preserves native side effect and closes observation");
            Unpatch("RegisterBehaviourBefore", "Throw");
            RunModifiers.Observe();
            Check(RunModifiers.GetContributors().Any(n => n.StartsWith("Unknown")), "Aborted registration metadata stays conservative");

            body = NewBody(); Begin(body);
            Check(RunModifiers.GetContributors().Length == 0, "New body/attempt has no old source leakage");
            var survivingOne = Create(); var survivingTwo = Create();
            Call("Add", body, survivingOne); Call("Add", body, survivingTwo);
            var removed = new List<IBodyCompBehaviour>();
            for (int i = 0; i < 9; i++) { var item = new Behaviour(); removed.Add(item); RunModifiers.Register(body, item); }
            Check(FullRunSave.fullRunSave.CurrentBodyCompModifiers == 11, "Reproduce captured run's 11 native registrations");
            RunModifiers.Finish();
            foreach (var item in removed) RunModifiers.Remove(body, item);
            Check(ModifierRegistrationEvidence.Get(body).ActiveCount == 2, "Teardown after Finish retains only the two surviving handlers");
            Check(RunModifiers.GetContributors().Length == 2, "Teardown cannot erase completed result sources");
            var reset = typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube").GetMethod("DeleteSaves");
            NativeResetOnWorker(reset);
            var inherited = FullRunSave.fullRunSave;
            Check(inherited.CurrentBodyCompModifiers == 2, "Real native save reset carries two registrations");
            Check(RunModifiers.WaitForWrites(5000), "Save-worker reset provenance reaches disk before a new attempt");
            var afterReset = AtomicXmlFile.Load<RunModifierRecord>(Path.Combine(directory, index + ".xml"));
            Check(afterReset.ResetNativePeak == 2 && afterReset.ResetSources.Single().Name == "Third-party fixture mod", "Reset metadata survives closing on the results screen");
            body = NewBody(); FullRunSave.fullRunSave = inherited;
            Call("Add", body, Create()); // foreign OnLevelStart before Runtime
            Begin(body);
            Check(RunModifiers.GetContributors().SequenceEqual(new[] { "Third-party fixture mod" }) && RunModifiers.GetUnknownReasons().Length == 0,
                "Real reset hook preserves foreign source without guessing or carrying removed mods");
            Call("Add", body, Create());
            Check(RunModifiers.GetContributors().Length == 1, "New registrations of inherited source remain deduplicated");
            Check(RunModifiers.WaitForWrites(5000), "Inherited evidence persisted");
            var carried = AtomicXmlFile.Load<RunModifierRecord>(Path.Combine(directory, index + ".xml"));
            Check(carried.InheritedNativePeak == 2 && carried.InheritedSources.Count == 1 && !carried.Unknown, "Disk retains reset provenance");
            RunModifiers.Finish();
            // A foreign prefix can suppress DeleteSaves. Equal counters do not
            // establish a reset if the original did not run.
            object skip = Activator.CreateInstance(harmonyMethod, new object[] { fixture.GetMethod("SkipReset") });
            harmony.GetMethod("Patch").Invoke(foreignOwner, new object[] { reset, skip, null, null, null });
            NativeResetOnWorker(reset);
            harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(foreignOwner,
                new object[] { reset, fixture.GetMethod("SkipReset") });
            inherited = FullRunSave.fullRunSave;
            body = NewBody(); FullRunSave.fullRunSave = inherited; Begin(body);
            Check(RunModifiers.GetUnknownReasons().Any(r => r.StartsWith("initial-native-flag:")), "Skipped native reset cannot manufacture provenance");
            RunModifiers.Unload();
        }
        public static int Main(string[] args)
        {
            try
            {
                directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foreign-attribution-" + Guid.NewGuid().ToString("N"));
                ModifierRegistrationObserver.TryInstall();
                Check(ModifierRegistrationObserver.Status.StartsWith("Unavailable:"), "No-Harmony runtime still works without loading a DLL");
                var asm = Assembly.LoadFrom(args[0]);
                fixture = Assembly.LoadFrom(args[1]).GetType("ForeignModifierFixture", true);
                Check(!fixture.Assembly.GetReferencedAssemblies().Any(a => a.Name == "JKRuntime"), "Foreign fixture has no Runtime dependency");
                ModLoader.Instance.LoadedMods.Add(new ModAssembly(fixture.Assembly, new JumpKingModAttribute("Third-party fixture mod")));
                harmony = asm.GetType("HarmonyLib.Harmony", true); harmonyMethod = asm.GetType("HarmonyLib.HarmonyMethod", true);
                foreignOwner = Activator.CreateInstance(harmony, new object[] { "foreign.fixture" });
                bool before = args.Length > 2;
                if (before)
                {
                    Patch("RegisterBehaviourBefore", "Touch", null);
                    var warm = NewBody();
                    Call("Before", warm, Create(), warm.GetBehaviourList().First());
                    Check((int)fixture.GetField("Touches").GetValue(null) == 1, "Foreign patch is active before observer install");
                }
                bool conflict = args.Length > 2 && args[2] == "conflict";
                bool lateConflict = args.Length > 2 && args[2] == "late-conflict";
                Assembly second = null;
                if (args.Length > 3 && !lateConflict) second = conflict ? PatchSecondEngine(args[3]) : Assembly.LoadFile(Path.GetFullPath(args[3]));
                string beforeFirst = Signature(asm), beforeSecond = second == null ? "" : Signature(second);
                ModifierRegistrationObserver.TryInstall();
                if (conflict)
                {
                    Check(ModifierRegistrationObserver.Status.StartsWith("Unavailable: multiple"), "Independent engines are rejected before patching");
                    Check(Signature(asm) == beforeFirst && Signature(second) == beforeSecond, "Conflict preflight leaves all foreign patches unchanged");
                    var explicitBody = NewBody(); Begin(explicitBody);
                    RunModifiers.RegisterBefore(explicitBody, Create(), explicitBody.GetBehaviourList().First());
                    Check(RunModifiers.GetContributors().Single() == "Third-party fixture mod", "Explicit API works even when automatic observer cannot install");
                    Console.WriteLine("[OK] Independent Harmony engine conflict is non-mutating; explicit attribution remains operational");
                    return 0;
                }
                Check(ModifierRegistrationObserver.Status.StartsWith("Active:"), ModifierRegistrationObserver.Status);
                Exercise();
                if (before) Check((int)fixture.GetField("Touches").GetValue(null) > 1, "Pre-existing foreign patch survives observer and other patch changes");
                ModifierRegistrationObserver.CheckCoverage();
                Check(ModifierRegistrationObserver.Status.StartsWith("Active:"), "All owned hooks remain after coexistence tests");
                if (lateConflict)
                {
                    second = PatchSecondEngine(args[3]);
                    string afterFirst = Signature(asm), afterSecond = Signature(second);
                    ModifierRegistrationObserver.CheckCoverage();
                    Check(ModifierRegistrationObserver.Status.StartsWith("Degraded:"), "Late engine conflict invalidates automatic coverage");
                    ModifierRegistrationObserver.TryInstall();
                    Check(Signature(asm) == afterFirst && Signature(second) == afterSecond, "No re-patching war after a late engine conflict");
                    Console.WriteLine("[OK] Late independent Harmony engine detected; no foreign patch changes or automatic reinstall");
                    return 0;
                }
                var observerPostfix = typeof(ModifierRegistrationObserver).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                // Remove through the ABI that actually owns the observer; two
                // LoadFile Harmony copies need not share their metadata store.
                var observerInfo = (MethodInfo)typeof(ModifierRegistrationObserver).GetField("patchInfo", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var observerHarmony = observerInfo.DeclaringType;
                var remover = Activator.CreateInstance(observerHarmony, new object[] { "foreign.fixture-removal" });
                observerHarmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(remover,
                    new object[] { typeof(BodyComp).GetMethod("RegisterBehaviour"), observerPostfix });
                ModifierRegistrationObserver.CheckCoverage();
                Check(ModifierRegistrationObserver.Status.StartsWith("Degraded:"), "External removal of observer is reported, not silently repaired");
                Console.WriteLine("[OK] Foreign modifier observation with Harmony " + asm.GetName().Version + (before ? " (foreign patch first)" : " (observer first)") + ": early/direct/transient calls, native counters, deduplication, names, permissions, skip/throw coexistence");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
