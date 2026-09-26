using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using JumpKing.API;
using JumpKing.Player;

namespace JKRuntime.Gameplay
{
    // No Harmony type/reference escapes this optional reflection adapter. It
    // never loads a Harmony DLL, edits foreign patches or suppresses game calls.
    internal static class ModifierRegistrationObserver
    {
        private const string Owner = "jk.runtime.run-modifiers";
        private static readonly HashSet<Assembly> attempted = new HashSet<Assembly>();
        private static int gameThread;
        private static long nextPoll;
        private static bool installed, hookError;
        private static volatile bool blocked;
        private static MethodInfo patchInfo;
        private static MethodInfo[] installedTargets;
        private static Assembly selected;
        private static readonly string[] TargetNames = { "RegisterBehaviour", "RegisterBehaviourBefore", "RegisterBehaviourAfter", "RemoveBehaviour", "DeleteSaves" };
        internal static string Status = "Not initialized";
        internal static void Poll()
        {
            if (blocked || Stopwatch.GetTimestamp() < nextPoll) return;
            nextPoll = Stopwatch.GetTimestamp() + Stopwatch.Frequency * (installed ? 5 : 1);
            if (installed) CheckCoverage(); else TryInstall();
        }
        internal static void TryInstall()
        {
            RunModifierTrace.Initialize();
            if (installed || blocked) return;
            RuntimeApi.Kernel.CheckThread();
            gameThread = Thread.CurrentThread.ManagedThreadId;
            var candidates = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony")
                .OrderByDescending(a => ExistingPatches(a).Length != 0).ThenByDescending(a => a.GetName().Version).ToArray();
            if (candidates.Length == 0) { Status = "Unavailable: Harmony is not loaded; explicit API attribution only"; return; }
            if (candidates.Select(ExistingPatches).Where(s => s.Length != 0).Distinct().Count() > 1)
            {
                Status = "Unavailable: multiple Harmony engines have different registration patches; explicit API attribution only";
                return;
            }
            foreach (var assembly in candidates)
            {
                if (!attempted.Add(assembly)) continue;
                var targets = new List<MethodInfo>();
                object owner = null; MethodInfo unpatch = null;
                try
                {
                    if (assembly.GetName().Version.Major != 2) throw new NotSupportedException("Only the Harmony 2 ABI is supported");
                    RunModifiers.ValidateContract(); ModifierRegistrationEvidence.Validate();
                    var harmony = assembly.GetType("HarmonyLib.Harmony", true);
                    var method = assembly.GetType("HarmonyLib.HarmonyMethod", true);
                    var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
                    unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) });
                    if (unpatch == null) throw new MissingMethodException("Harmony.Unpatch(MethodBase, MethodInfo)");
                    owner = Activator.CreateInstance(harmony, new object[] { Owner });
                    // Observe before other prefixes where possible and after
                    // their postfixes. Physical mutations are checked as well
                    // as __result; a skipped original returning true isn't proof.
                    var priority = method.GetField("priority");
                    if (priority == null) throw new MissingFieldException("HarmonyMethod.priority");
                    foreach (string name in TargetNames)
                    {
                        var target = Target(name);
                        if (target == null || target.IsGenericMethod || target.ReturnType != (name == "DeleteSaves" ? typeof(void) : typeof(bool)))
                            throw new MissingMethodException("Required modifier observation method: " + name);
                        object prefix = Activator.CreateInstance(method, new object[] { Hook(Callback(target, "Prefix")) });
                        object postfix = Activator.CreateInstance(method, new object[] { Hook(Callback(target, "Postfix")) });
                        object finalizer = Activator.CreateInstance(method, new object[] { Hook(Callback(target, "Finalizer")) });
                        priority.SetValue(prefix, int.MaxValue); priority.SetValue(postfix, int.MinValue);
                        targets.Add(target); // Include a partially patched target in rollback.
                        patch.Invoke(owner, new[] { target, prefix, postfix, null, finalizer });
                    }
                    installed = true;
                    selected = assembly;
                    patchInfo = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
                    installedTargets = targets.ToArray();
                    Status = "Active: " + assembly.FullName + "; native registration evidence (not all foreign mutations)";
                    CheckCoverage();
                    Console.WriteLine("[JK Runtime] Modifier observer: " + Status);
                    return;
                }
                catch (Exception error)
                {
                    bool rollbackFailed = false;
                    if (owner != null && unpatch != null)
                        foreach (var target in targets)
                        foreach (string name in new[] { "Prefix", "Postfix", "Finalizer" })
                            try { unpatch.Invoke(owner, new object[] { target, Hook(Callback(target, name)) }); }
                            catch { rollbackFailed = true; }
                    Status = "Unavailable: " + assembly.FullName + ": " + error.GetBaseException().Message
                        + (rollbackFailed ? "; observer cleanup incomplete, restart required" : "");
                    Console.WriteLine("[JK Runtime] Modifier observer: " + Status);
                    if (rollbackFailed) { blocked = true; return; }
                }
            }
        }
        private static MethodInfo Hook(string name) { return typeof(ModifierRegistrationObserver).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic); }
        private static MethodInfo Target(string name)
        {
            return name == "DeleteSaves" ? typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube", true)
                .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : typeof(BodyComp).GetMethod(name);
        }
        private static string Callback(MethodInfo target, string kind)
        { return target.Name == "DeleteSaves" ? "Reset" + kind : kind; }
        internal static void CheckCoverage()
        {
            if (!installed || blocked) return;
            try
            {
                string selectedPatches = ExistingPatches(selected);
                foreach (var other in AppDomain.CurrentDomain.GetAssemblies().Where(a => a != selected && a.GetName().Name == "0Harmony"))
                {
                    string patches = ExistingPatches(other);
                    if (patches.Length != 0 && patches != selectedPatches)
                        throw new InvalidOperationException("Another Harmony engine has independent registration patches");
                }
                foreach (var target in installedTargets)
                {
                    object info = patchInfo.Invoke(null, new object[] { target });
                    foreach (var pair in new[] { new[] { "Prefixes", "Prefix" }, new[] { "Postfixes", "Postfix" }, new[] { "Finalizers", "Finalizer" } })
                    {
                        var patches = info == null ? null : ReadMember(info, pair[0]) as IEnumerable;
                        bool found = patches != null && patches.Cast<object>().Any(p => Equals(ReadMember(p, "PatchMethod"), Hook(Callback(target, pair[1]))) && Equals(ReadMember(p, "owner"), Owner));
                        if (!found) throw new InvalidOperationException("Missing observer " + target.Name + "/" + pair[1]);
                    }
                }
            }
            catch (Exception error)
            {
                blocked = true;
                Status = "Degraded: " + error.GetBaseException().Message + "; foreign coverage unavailable, restart required";
                Console.WriteLine("[JK Runtime] Modifier observer: " + Status);
            }
        }
        private static string ExistingPatches(Assembly assembly)
        {
            try
            {
                var type = assembly.GetType("HarmonyLib.Harmony");
                var get = type == null ? null : type.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
                if (get == null) return "unreadable:" + assembly.FullName;
                var items = new List<string>();
                foreach (string name in TargetNames)
                {
                    object info = get.Invoke(null, new object[] { Target(name) });
                    if (info == null) continue;
                    foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                    {
                        var patches = ReadMember(info, kind) as IEnumerable;
                        if (patches == null) continue;
                        foreach (object patch in patches)
                        {
                            var method = ReadMember(patch, "PatchMethod") as MethodInfo;
                            items.Add(name + "/" + kind + "/" + ReadMember(patch, "owner") + "/"
                                + (method == null ? "unknown" : method.Module.ModuleVersionId + "/" + method.MetadataToken));
                        }
                    }
                }
                return string.Join("|", items.OrderBy(s => s, StringComparer.Ordinal));
            }
            catch { return "unreadable:" + assembly.FullName; } // Never mistake inaccessible metadata for no patches.
        }
        private static object ReadMember(object value, string name)
        {
            var type = value.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(value);
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property == null ? null : property.GetValue(value, null);
        }
        private static void Prefix(BodyComp __instance, IBodyCompBehaviour __0, MethodBase __originalMethod, out ModifierRegistrationObservation __state)
        {
            __state = null;
            if (Thread.CurrentThread.ManagedThreadId != gameThread) return;
            try { __state = ModifierRegistrationEvidence.Capture(__instance, __0, __originalMethod.Name == "RemoveBehaviour", false); }
            catch (Exception error) { ReportError(error); }
        }
        private static void ResetPrefix(out ModifierResetEvidence __state)
        {
            __state = null;
            if (blocked) { RunModifiers.CancelReset(); return; }
            try { __state = RunModifiers.CaptureReset(); }
            catch (Exception error) { ReportError(error); }
        }
        private static void ResetPostfix(ModifierResetEvidence __state, bool __runOriginal)
        {
            try { RunModifiers.CompleteReset(__runOriginal && !blocked ? __state : null); }
            catch (Exception error) { RunModifiers.CancelReset(); ReportError(error); }
        }
        private static void ResetFinalizer(Exception __exception)
        {
            if (__exception != null) RunModifiers.CancelReset();
        }
        private static void Postfix(bool __result, ModifierRegistrationObservation __state)
        {
            try { ModifierRegistrationEvidence.Complete(__state, __result); }
            catch (Exception error) { ReportError(error); }
        }
        private static void Finalizer(ModifierRegistrationObservation __state)
        {
            // Void finalizer preserves any original exception unchanged. Also
            // closes evidence when another prefix/original/postfix throws.
            try { ModifierRegistrationEvidence.Complete(__state, false); }
            catch (Exception error) { ReportError(error); }
        }
        private static void ReportError(Exception error)
        {
            if (hookError) return;
            hookError = true; Status += "; evidence error: " + error.GetBaseException().Message;
            Console.WriteLine("[JK Runtime] Modifier observer evidence error (game call left intact): " + error);
        }
    }
}
