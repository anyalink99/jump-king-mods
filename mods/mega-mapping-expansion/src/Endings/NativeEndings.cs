using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BehaviorTree;
using EntityComponent.BT;
using JKRuntime;
using JKRuntime.Compatibility;
using JumpKing;
using JumpKing.Util.DrawBT;

namespace MegaMappingExpansion.Endings
{
    internal static class NativeEndings
    {
        internal sealed class Binding
        {
            internal string Role, TypeName;
            internal EndingXmlParser.Ending Ending;
            internal MethodInfo Method;
            internal bool Actor { get { return !Role.EndsWith("_ending", StringComparison.Ordinal); } }
        }
        internal static readonly Binding[] Bindings = CreateBindings();
        private static Dictionary<string, XElement> documents = new Dictionary<string, XElement>();
        private static OwnedPatches patches;
        private static string preparedRoot;
        private static readonly Dictionary<MethodBase, string> foreignRoles = new Dictionary<MethodBase, string>();
        private static Binding[] CreateBindings()
        {
            string prefix = "JumpKing.GameManager.MultiEnding.";
            var result = new List<Binding>();
            string[] roles = { "main_ending", "main_king", "main_babe", "main_cherub", "nbp_ending", "nbp_king", "nbp_babe", "nbp_hanging_babe", "nbp_cherubs", "nbp_crow", "owl_ending", "owl_king", "owl_babe", "owl_gargoyle", "owl_bird" };
            string[] types = { "NormalEnding.NormalEnding", "NormalEnding.EndingKing", "NormalEnding.EndingBabe", "NormalEnding.EndingCherub", "NewBabePlusEnding.NewBabePlusEnding", "NewBabePlusEnding.Actors.NBPKingEntity", "NewBabePlusEnding.Actors.NBPBabeEntity", "NewBabePlusEnding.Actors.HangingBabe", "NewBabePlusEnding.Actors.NBPCherubs", "NewBabePlusEnding.Actors.NBPCrow", "OwlEnding.OwlEnding", "OwlEnding.OwlKingEntity", "OwlEnding.OwlBabeEntity", "OwlEnding.OwlGargoyleEntity", "OwlEnding.OwlBirdEntity" };
            for (int i = 0; i < roles.Length; i++) result.Add(new Binding { Role = roles[i], TypeName = prefix + types[i], Ending = i < 4 ? EndingXmlParser.Ending.MainBabe : i < 10 ? EndingXmlParser.Ending.NewBabePlus : EndingXmlParser.Ending.GhostOfTheBabe });
            return result.ToArray();
        }
        internal static Dictionary<string, XElement> Read(string root)
        {
            var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (Binding binding in Bindings)
            {
                string path = Path.Combine(root, "ending", "custom_" + binding.Role + ".xml");
                if (!File.Exists(path)) continue;
                try { result.Add(binding.TypeName, EndingDocument.Read(path, binding.Role)); }
                catch (Exception error) { throw new InvalidDataException(path + ": " + error.Message, error); }
            }
            return result;
        }
        internal static void Prepare(string root)
        {
            // parse the whole candidate before replacing a working set
            // no file IO from MakeBT, including actors made during loading and native endings
            Dictionary<string, XElement> candidate;
            try { candidate = Read(root); }
            catch { Release(); throw; }
            if (candidate.Count == 0) { Release(); return; }
            AssertContracts();
            if (patches != null && !new HashSet<string>(candidate.Keys).SetEquals(documents.Keys)) Release();
            if (patches == null)
            {
                var owned = new OwnedPatches("mega-mapping-expansion.endings");
                try
                {
                    foreach (Binding binding in Bindings)
                    {
                        if (!candidate.ContainsKey(binding.TypeName)) continue;
                        owned.Add(binding.Method, prefix: typeof(NativeEndings).GetMethod(binding.Actor ? "ActorPrefix" : "ControllerPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                    }
                    InstallForeignGuards(owned, candidate);
                    var patchInfo=typeof(HarmonyLib.Harmony).Assembly.GetType("HarmonyLib.PatchInfo",true);
                    var update=typeof(HarmonyLib.Harmony).Assembly.GetType("HarmonyLib.PatchFunctions",true).GetMethod("UpdateWrapper",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(MethodBase),patchInfo},null);
                    if(update==null||update.ReturnType!=typeof(MethodInfo)||patchInfo.GetField("postfixes")==null)throw new NotSupportedException("Ending adapter requires the reviewed Harmony wrapper contract");
                    owned.Add(update,prefix:typeof(NativeEndings).GetMethod("BeforeForeignWrapper",BindingFlags.Static|BindingFlags.NonPublic));
                    patches = owned;
                }
                catch { owned.Dispose(); foreignRoles.Clear(); throw; }
            }
            try{InstallForeignGuards(patches,candidate);}catch{Release();throw;}
            documents = candidate; preparedRoot = Path.GetFullPath(root);
        }
        internal static void ValidateScene(MegaMappingExpansion.Construction.SceneDocument scene) { EndingDocument.ValidateScene(documents.Values, scene); }
        internal static void AssertContracts()
        {
            foreach (Binding binding in Bindings)
            {
                Type type = typeof(Game1).Assembly.GetType(binding.TypeName, true);
                binding.Method = type.GetMethod("MakeBT", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (binding.Method == null || binding.Method.ReturnType != (binding.Actor ? typeof(BehaviorTreeComp) : typeof(BTmanager)))
                    throw new NotSupportedException("Unsupported native ending tree: " + binding.TypeName);
            }
        }
        private static bool TryBuild(object actor, out IBTnode tree)
        {
            tree = null; XElement document;
            if (preparedRoot == null) throw new InvalidOperationException("Ending adapter has no prepared map");
            if (Game1.instance == null || Game1.instance.contentManager == null) throw new InvalidOperationException("Native ending creation requires loaded game content");
            if (!Owns(actor.GetType().FullName) || !documents.TryGetValue(actor.GetType().FullName, out document)) return false;
            // ending construction is a lifecycle boundary, not a frame poll
            InstallForeignGuards(patches,documents);
            Binding binding = Array.Find(Bindings, b => b.TypeName == actor.GetType().FullName);
            try { tree = EndingXmlParser.GetBtTree(actor as ISpriteEntity, document, binding.Ending); return true; }
            catch (Exception error)
            { throw new InvalidDataException("ending/custom_" + binding.Role + ".xml: native tree binding failed", error); }
        }
        private static bool ActorPrefix(ISpriteEntity __instance, ref BehaviorTreeComp __result)
        { IBTnode tree; if (!TryBuild(__instance, out tree)) return true; __result = new BehaviorTreeComp(tree); return false; }
        private static bool ControllerPrefix(object __instance, ref BTmanager __result)
        { IBTnode tree; if (!TryBuild(__instance, out tree)) return true; __result = new BTmanager(tree); return false; }
        internal static void Release()
        {
            if (patches != null) { patches.Dispose(); patches = null; }
            documents.Clear(); preparedRoot = null; foreignRoles.Clear();
        }
        private static bool Owns(string typeName)
        {
            if (preparedRoot == null || !documents.ContainsKey(typeName) || Game1.instance == null || Game1.instance.contentManager == null) return false;
            string root = MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root);
            return string.Equals(Path.GetFullPath(root), preparedRoot, StringComparison.OrdinalIgnoreCase);
        }
        private static bool ForeignTreePrefix(MethodBase __originalMethod)
        {
            string typeName;
            return !foreignRoles.TryGetValue(__originalMethod, out typeName) || !Owns(typeName);
        }
        private static void BeforeForeignWrapper(MethodBase original,object patchInfo)
        {
            if(patches==null||preparedRoot==null)return;
            var binding=Bindings.FirstOrDefault(b=>b.Method==original&&documents.ContainsKey(b.TypeName));
            if(binding==null)return;
            foreach(string kind in new[]{"prefixes","transpilers","finalizers"})
                RejectForeignExecution(binding,(HarmonyLib.Patch[])patchInfo.GetType().GetField(kind).GetValue(patchInfo));
            // guard before Harmony emits calls; a compiled wrapper may inline an unguarded callback
            foreach(var patch in (HarmonyLib.Patch[])patchInfo.GetType().GetField("postfixes").GetValue(patchInfo))
                if(patch.PatchMethod.Module.Assembly.GetName().Name=="MoreEndingOptions")GuardCallback(patches,binding,patch.PatchMethod);
        }
        private static Type[] LoadableTypes(Assembly assembly)
        {
            try{return assembly.GetTypes();}
            catch(ReflectionTypeLoadException error){
                CompatibilityEvidence.Record("mme.endings.discovery",assembly.GetName().Name,"ending-callback-v2",CompatibilityOutcome.Unknown,
                    CompatibilityEvidence.Identity(assembly)+"; partial type load: "+string.Join("; ",error.LoaderExceptions.Where(e=>e!=null).Select(e=>e.Message).Take(3)),"Only observed targets can be guarded; revalidate on Patch and ending creation");
                return error.Types.Where(t=>t!=null).ToArray();
            }
        }
        internal static MethodInfo[] ForeignCallbacks(Assembly assembly, Binding binding)
        {
            var result = new HashSet<MethodInfo>();
            // installed targets also cover providers that use TargetMethod or Patch directly
            var installed = HarmonyLib.Harmony.GetPatchInfo(binding.Method);
            if (installed != null) {
                RejectForeignExecution(binding,installed.Prefixes.Concat(installed.Transpilers).Concat(installed.Finalizers));
                foreach (var patch in installed.Postfixes)
                    if (patch.PatchMethod.Module.Assembly == assembly) result.Add(patch.PatchMethod);
            }
            // preparation can run before PatchAll; combine class and method metadata
            foreach (Type type in LoadableTypes(assembly))
            {
                try {
                var defaults = type.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Cast<HarmonyLib.HarmonyPatch>();
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (method.Name != "Postfix" && !method.IsDefined(typeof(HarmonyLib.HarmonyPostfix), false)) continue;
                    var attributes = defaults.Concat(method.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Cast<HarmonyLib.HarmonyPatch>());
                    var target = HarmonyLib.HarmonyMethod.Merge(attributes.Select(a => a.info).ToList());
                    if (target.declaringType == binding.Method.DeclaringType && target.methodName == binding.Method.Name
                        && (!target.methodType.HasValue || target.methodType == HarmonyLib.MethodType.Normal)
                        && (target.argumentTypes == null || target.argumentTypes.Length == 0)) result.Add(method);
                }
                } catch(Exception error) {
                    if(!(error is TypeLoadException || error is FileNotFoundException || error is FileLoadException))throw;
                    CompatibilityEvidence.Record("mme.endings.discovery",assembly.GetName().Name,"ending-callback-v2",CompatibilityOutcome.Unknown,
                        CompatibilityEvidence.Identity(assembly)+"; metadata unavailable for "+type.FullName+": "+error.Message,"Guard actual targets at wrapper construction");
                }
            }
            return result.ToArray();
        }
        private static void RejectForeignExecution(Binding binding,IEnumerable<HarmonyLib.Patch> foreign)
        {
            foreach(var patch in foreign) if(patch.PatchMethod.Module.Assembly.GetName().Name=="MoreEndingOptions") {
                string reason="Unreviewed non-postfix ending hook: "+patch.PatchMethod;
                CompatibilityEvidence.Record("mme.endings."+binding.Role,"MoreEndingOptions","ending-callback-v2",CompatibilityOutcome.Unsupported,reason,"Refuse the required custom ending");
                throw new NotSupportedException(reason);
            }
        }
        private static void InstallForeignGuards(OwnedPatches owned, Dictionary<string, XElement> candidate)
        {
            // native discovery loads mod assemblies before calling their startup
            // callbacks. Guard the callbacks themselves, so either PatchAll order
            // works, without removing or rebuilding another provider's patches
            var providers=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="MoreEndingOptions").ToArray();
            if(providers.Length==0)CompatibilityEvidence.Record("mme.endings","MoreEndingOptions","ending-callback-v2",CompatibilityOutcome.Absent,"Provider is not loaded","Native MME ending adapter");
            foreach (Assembly assembly in providers)
            {
                foreach (Binding binding in Bindings.Where(b => candidate.ContainsKey(b.TypeName)))
                {
                    var callbacks=ForeignCallbacks(assembly,binding);
                    if(callbacks.Length==0)CompatibilityEvidence.Record("mme.endings."+binding.Role,assembly.GetName().Name,"ending-callback-v2",CompatibilityOutcome.Unknown,
                        CompatibilityEvidence.Identity(assembly)+"; no observed callback for this role","Reconcile at wrapper construction; do not infer permanent absence");
                    foreach (MethodInfo callback in callbacks)
                    {
                        GuardCallback(owned,binding,callback);
                    }
                }
            }
        }
        private static void GuardCallback(OwnedPatches owned,Binding binding,MethodInfo callback)
        {
            var assembly=callback.DeclaringType.Assembly;
            // the guard doesn't consume arguments, so added/reordered parameters are fine
            // a return-value postfix needs a different adapter; don't silently erase its result
            if (!callback.IsStatic || callback.ReturnType != typeof(void)){
                CompatibilityEvidence.Record("mme.endings."+binding.Role,assembly.GetName().Name,"ending-callback-v2",CompatibilityOutcome.Unsupported,"Expected a static void postfix: "+callback,"Refuse the required custom ending");
                throw new NotSupportedException("Cannot guard ending callback " + callback.DeclaringType.FullName + "." + callback.Name
                    + " from " + assembly.FullName + " for " + binding.Role + ": expected a static void postfix");
            }
            string existing;
            if (foreignRoles.TryGetValue(callback, out existing))
            {
                if (existing != binding.TypeName) throw new NotSupportedException("Ending callback serves multiple roles: " + callback);
                return;
            }
            try{owned.Add(callback, prefix: typeof(NativeEndings).GetMethod("ForeignTreePrefix", BindingFlags.Static | BindingFlags.NonPublic));}
            catch(Exception error){CompatibilityEvidence.Record("mme.endings."+binding.Role,assembly.GetName().Name,"ending-callback-v2",CompatibilityOutcome.Failed,error.Message,"Refuse the required custom ending");throw;}
            foreignRoles.Add(callback, binding.TypeName);
            CompatibilityEvidence.Record("mme.endings."+binding.Role,assembly.GetName().Name,"ending-callback-v2",CompatibilityOutcome.Supported,
                CompatibilityEvidence.Identity(assembly)+"; native="+CompatibilityEvidence.Identity(typeof(Game1).Assembly)+"; engine="+CompatibilityEvidence.Identity(typeof(HarmonyLib.Harmony).Assembly)+"; static void callback="+callback.DeclaringType.FullName+"."+callback.Name,
                "Guard only this authored role while its map owns the ending");
        }

    }
}
