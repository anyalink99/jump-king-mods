using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using EntityComponent;
using JumpKing.Player;

namespace JKRuntime
{
    internal static class RuntimeDiagnostics
    {
        internal static string OutputDirectory = null;
        internal static Dictionary<string, object> Capture(RuntimeKernel kernel, GameContract contract)
        {
            var assemblies = new List<object>();
            var harmony = new List<object>();
            ModuleStatus[] modules = kernel.GetModules();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName, StringComparer.Ordinal))
            {
                try
                {
                    var item = new Dictionary<string, object>();
                    item["assembly"] = assembly.FullName;
                    item["moduleId"] = assembly.ManifestModule.ModuleVersionId.ToString();
                    item["location"] = Location(assembly);
                    item["integration"] = assembly == typeof(RuntimeApi).Assembly ? "runtime-with-bundled-ui"
                        : modules.Any(m => m.Origin == assembly.FullName) ? "runtime-registered" : "observed-only";
                    item["harmonyReferences"] = assembly.GetReferencedAssemblies().Where(a => a.Name == "0Harmony").Select(a => a.FullName).ToArray();
                    assemblies.Add(item);
                    if (assembly.GetName().Name == "0Harmony") harmony.Add(InspectHarmony(assembly));
                }
                catch (Exception error) { assemblies.Add(new { assembly = assembly.FullName, error = error.Message }); }
            }
            var report = new Dictionary<string, object>();
            report["performanceStatus"] = PerformanceDiagnostics.Status;
            report["performanceReport"] = PerformanceDiagnostics.LastReport;
            report["diagnosticMode"] = PerformanceDiagnostics.Enabled;
            report["runtime"] = RuntimeApi.Version; report["uiApi"] = JKRuntime.UI.UIApi.Version;
            report["generatedUtc"] = DateTime.UtcNow.ToString("o");
            report["state"] = kernel.State; report["modules"] = kernel.GetModules();
            report["order"] = kernel.Order; report["errors"] = kernel.Errors;
            report["packageErrors"] = PackageHost.Errors;
            report["preparationHook"] = PreparationHooks.Status;
            report["preparationScopes"] = PackageHost.InspectPreparation();
            report["commandError"] = JKRuntime.Settings.Commands.LastError;
            report["mechanics"] = RuntimeApi.Mechanics.Inspect();
            report["motionObservation"] = Gameplay.MotionObservation.Diagnostics();
            report["movementObservation"] = Gameplay.MovementObservation.Diagnostics();
            report["blockDeclarations"] = Geometry.BlockCatalog.Inspect();
            report["blockCatalogAudit"] = Geometry.BlockCatalog.AuditDate;
            report["physicalActionStreams"] = Input.SharedActionSampler.ActiveStreams;
            report["recentEvents"] = RuntimeJournal.Read();
            report["ownedResources"] = RuntimeResources.Inspect();
            report["runModifierContributors"] = Gameplay.RunModifiers.GetContributors();
            report["runModifierUnknownReasons"] = Gameplay.RunModifiers.GetUnknownReasons();
            report["runModifierInheritedPeak"] = Gameplay.RunModifiers.InheritedNativePeak;
            report["runModifierInheritedSources"] = Gameplay.RunModifiers.InheritedNames();
            report["foreignModifierObserver"] = Gameplay.ModifierRegistrationObserver.Status;
            report["moreTextOptionsCompatibility"] = Compatibility.MoreTextOptionsCompatibility.Status;
            report["conveyorCompatibility"] = Compatibility.ConveyorCompatibility.Status;
            report["jumpKingManagerCompatibility"] = Compatibility.JumpKingManagerCompatibility.Status;
            report["bundledCapabilities"] = contract.Available
                ? new[] { "jk.ui:1:0", "jk.game.readonly:1:0" } : new[] { "jk.ui:1:0" };
            report["gameSha256"] = contract.Fingerprint; report["knownGame"] = contract.KnownFingerprint;
            report["contracts"] = contract.Findings; report["assemblies"] = assemblies;
            report["harmony"] = harmony;
            report["policy"] = "Only registered modules are ordered. Scoped motion observation instruments supported arithmetic without replaying foreign callbacks. Reviewed compatibility adapters have separate coverage; unknown foreign behavior is not certified.";
            try
            {
                PlayerEntity player = EntityManager.instance == null ? null : EntityManager.instance.Find<PlayerEntity>();
                if (player != null && contract.Available)
                {
                    report["jumpOwner"] = contract.GetJumpState(player).GetType().AssemblyQualifiedName;
                    report["bodyPipeline"] = contract.DescribeBody(player.m_body);
                    report["presentationOwners"] = Gameplay.PresentationActivity.GetOwners(player.m_body);
                }
            }
            catch (Exception error) { report["playerInspectionError"] = error.Message; }
            return report;
        }

        internal static Dictionary<string, object> InspectHarmony(Assembly assembly)
        {
            var result = new Dictionary<string, object>();
            result["assembly"] = assembly.FullName; result["location"] = Location(assembly);
            try
            {
                if (!string.IsNullOrEmpty(assembly.Location))
                    using (var stream = File.OpenRead(assembly.Location))
                    using (var sha = SHA256.Create())
                        result["sha256"] = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                Type type = assembly.GetType("HarmonyLib.Harmony", true);
                MethodInfo all = type.GetMethod("GetAllPatchedMethods", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo info = type.GetMethod("GetPatchInfo", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(MethodBase) }, null);
                if (all == null || info == null) throw new MissingMethodException("Harmony inspection ABI (GetAllPatchedMethods/GetPatchInfo)");
                var patches = new List<object>();
                var targets = new List<MethodBase>();
                foreach (MethodBase method in (IEnumerable)all.Invoke(null, null)) targets.Add(method);
                foreach (MethodBase method in targets.OrderBy(Describe, StringComparer.Ordinal))
                {
                    object data;
                    try { data = info.Invoke(null, new object[] { method }); }
                    catch (Exception error) { patches.Add(new { target = Describe(method), error = error.Message }); continue; }
                    if (data == null) continue;
                    var owners = new SortedSet<string>(StringComparer.Ordinal);
                    foreach (string kind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                    {
                        IEnumerable list = Member(data, kind) as IEnumerable;
                        if (list == null) continue;
                        foreach (object patch in list)
                        {
                            string owner = Convert.ToString(Member(patch, "owner")); owners.Add(owner);
                            patches.Add(new { target = Describe(method), kind = kind, owner = owner,
                                priority = Member(patch, "priority"), before = Member(patch, "before"), after = Member(patch, "after") });
                        }
                    }
                    if (owners.Count > 1) patches.Add(new { target = Describe(method), note = "Multiple patch owners; observation, not proof of incompatibility.", owners = owners.ToArray() });
                }
                result["patches"] = patches;
            }
            catch (Exception error) { result["inspectionError"] = error.ToString(); }
            return result;
        }
        private static object Member(object target, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = target.GetType().GetField(name, flags);
            if (field != null) return field.GetValue(target);
            PropertyInfo property = target.GetType().GetProperty(name, flags);
            return property == null ? null : property.GetValue(target, null);
        }
        private static string Describe(MethodBase method)
        { return (method.DeclaringType == null ? "<global>" : method.DeclaringType.FullName) + "." + method; }
        private static string Location(Assembly assembly)
        {
            if (assembly.IsDynamic) return "<dynamic>";
            string path = assembly.Location;
            if (string.IsNullOrEmpty(path)) return "<memory>";
            // Retain Workshop association, not unrelated personal directories.
            string normalized = path.Replace('\\', '/');
            int workshop = normalized.IndexOf("/workshop/content/", StringComparison.OrdinalIgnoreCase);
            return workshop >= 0 ? normalized.Substring(workshop + 1) : Path.GetFileName(path);
        }
        internal static string Export(RuntimeKernel kernel, GameContract contract)
        {
            string directory = OutputDirectory ?? Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Runtime report directory unavailable.");
            PerformanceDiagnostics.ExportWindow();
            var report = Capture(kernel, contract);
            string json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(report);
            var text = new StringBuilder();
            text.AppendLine("JK Runtime " + RuntimeApi.Version + " / built-in UIApi+ " + JKRuntime.UI.UIApi.Version);
            text.AppendLine("State: " + kernel.State + "; game SHA256: " + contract.Fingerprint);
            foreach (string line in contract.Findings) text.AppendLine(line);
            text.AppendLine("Module order: " + string.Join(" -> ", kernel.Order));
            foreach (ModuleStatus module in kernel.GetModules()) text.AppendLine(module.Id + " " + module.Version + ": " + module.State + " " + module.Detail);
            foreach (string error in kernel.Errors) text.AppendLine(error);
            text.AppendLine("\nDetailed assembly/Harmony inventory (JSON):"); text.AppendLine(json);
            string path = Path.Combine(directory, "JKRuntime.report.txt");
            AtomicWrite(Path.Combine(directory, "JKRuntime.report.json"), json);
            AtomicWrite(path, text.ToString());
            return path;
        }
        internal static void AtomicWrite(string path, string value)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, value, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
