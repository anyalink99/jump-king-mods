using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BehaviorTree;
using EntityComponent.BT;
using JKRuntime;
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
            // Parse the whole candidate before replacing a working set; no file IO
            // from MakeBT, including actors created during loading and native endings.
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
                    patches = owned;
                }
                catch { owned.Dispose(); foreignRoles.Clear(); throw; }
            }
            documents = candidate; preparedRoot = Path.GetFullPath(root);
        }
        internal static void ValidateScene(SceneFile scene) { EndingDocument.ValidateScene(documents.Values, scene); }
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
            string root = SceneValidation.ResolveLevelRoot(Game1.instance.contentManager.root);
            return string.Equals(Path.GetFullPath(root), preparedRoot, StringComparison.OrdinalIgnoreCase);
        }
        private static bool ForeignTreePrefix(MethodBase __originalMethod)
        {
            string typeName;
            return !foreignRoles.TryGetValue(__originalMethod, out typeName) || !Owns(typeName);
        }
        private static void InstallForeignGuards(OwnedPatches owned, Dictionary<string, XElement> candidate)
        {
            // Native discovery loads mod assemblies before invoking their startup
            // callbacks. Guard the callbacks themselves, so either PatchAll order
            // works, without removing or rebuilding another provider's patches.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "MoreEndingOptions"))
            {
                foreach (Binding binding in Bindings.Where(b => candidate.ContainsKey(b.TypeName)))
                {
                    var callbacks = assembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false)
                        .Cast<HarmonyLib.HarmonyPatch>().Any(a => a.info.methodName == "MakeBT"
                            && a.info.declaringType == binding.Method.DeclaringType))
                        .Select(t => t.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static)).ToArray();
                    if (callbacks.Length != 1 || callbacks[0] == null || callbacks[0].ReturnType != typeof(void))
                        throw new NotSupportedException("Unsupported MoreEndingOptions callback for " + binding.Role);
                    MethodInfo callback = callbacks[0];
                    Type[] expected = binding.Actor
                        ? new[] { typeof(ISpriteEntity), typeof(BehaviorTreeComp).MakeByRefType() }
                        : new[] { typeof(BTmanager).MakeByRefType() };
                    if (!callback.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected))
                        throw new NotSupportedException("Unsupported MoreEndingOptions signature for " + binding.Role);
                    foreignRoles.Add(callback, binding.TypeName);
                    owned.Add(callback, prefix: typeof(NativeEndings).GetMethod("ForeignTreePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                }
            }
        }
    }
}
