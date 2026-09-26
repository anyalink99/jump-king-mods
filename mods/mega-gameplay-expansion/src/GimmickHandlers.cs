using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;

namespace MegaGameplayExpansion
{
    // Recover explicit native registration pairs from IL. Names of mods, blocks
    // and handlers never participate in matching. No startup callback is replayed.
    internal static class GimmickHandlers
    {
        private static readonly Dictionary<Assembly, Dictionary<Type, List<ConstructorInfo>>> cache = new Dictionary<Assembly, Dictionary<Type, List<ConstructorInfo>>>();
        private static readonly HashSet<Type> published = new HashSet<Type>();
        private static readonly FieldInfo Lookup = typeof(BodyComp).GetField("m_blockBehaviourLookup", Gimmicks.Members);
        private static readonly FieldInfo Behaviours = typeof(BodyComp).GetField("m_blockBehaviours", Gimmicks.Members);
        internal static void Prepare(Type block) { Inspect(block.Assembly); }
        internal static bool HasAdditionalContact(Type block)
        {
            List<ConstructorInfo> recipes;
            if (!Inspect(block.Assembly).TryGetValue(block, out recipes)) return false;
            foreach (var type in recipes.Select(c => c.DeclaringType).Distinct())
                foreach (var method in type.GetMethods(Gimmicks.Members).Where(m => m.Name == "AdditionalXCollisionCheck" || m.Name == "AdditionalYCollisionCheck"))
                    if (!GimmickClassification.ConstantBoolean(method).HasValue) return true;
            return false;
        }
        private static Dictionary<Type, List<ConstructorInfo>> Inspect(Assembly assembly)
        {
            Dictionary<Type, List<ConstructorInfo>> result; if (cache.TryGetValue(assembly, out result)) return result;
            result = new Dictionary<Type, List<ConstructorInfo>>();
            Type[] types; try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException error) { types = error.Types.Where(t => t != null).ToArray(); }
            foreach (var type in types) foreach (var method in type.GetMethods(Gimmicks.Members | BindingFlags.DeclaredOnly))
            {
                if (method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                GimmickInstruction[] code; try { code = GimmickIl.Read(method); } catch { continue; }
                for (int i = 0; i < code.Length; i++)
                {
                    var call = code[i].Operand as MethodInfo;
                    if (call == null || call.DeclaringType != typeof(BodyComp) || call.Name != "RegisterBlockBehaviour") continue;
                    Type block = call.IsGenericMethod ? call.GetGenericArguments()[0] : null;
                    ConstructorInfo constructor = null;
                    bool hasPublication = false;
                    for (int j = i - 1; j >= 0 && i - j < 24; j--)
                    {
                        if (code[j].Code.FlowControl == System.Reflection.Emit.FlowControl.Branch || code[j].Code.FlowControl == System.Reflection.Emit.FlowControl.Cond_Branch) break;
                        var candidate = code[j].Operand as ConstructorInfo;
                        if (constructor == null && (code[j].Code.Name == "stsfld" || code[j].Code.Name == "stfld")) hasPublication = true;
                        var intervening = code[j].Operand as MethodInfo;
                        if (constructor == null && intervening != null && intervening.Name.StartsWith("set_", StringComparison.Ordinal)) hasPublication = true;
                        if (code[j].Code.Name == "newobj" && candidate != null && typeof(IBlockBehaviour).IsAssignableFrom(candidate.DeclaringType))
                        { if (constructor != null) break; constructor = candidate; }
                        if (block == null && code[j].Code.Name == "ldtoken" && code[j].Operand is Type && typeof(IBlock).IsAssignableFrom((Type)code[j].Operand)) block = (Type)code[j].Operand;
                        if (block != null && constructor != null)
                        {
                            List<ConstructorInfo> constructors; if (!result.TryGetValue(block, out constructors)) result.Add(block, constructors = new List<ConstructorInfo>());
                            if (hasPublication) published.Add(block);
                            if (!constructors.Contains(constructor)) constructors.Add(constructor); break;
                        }
                    }
                }
            }
            cache[assembly] = result; return result;
        }
        internal sealed class Lease : IDisposable
        {
            private readonly BodyComp body;
            internal readonly Type Block;
            private readonly IBlockBehaviour behaviour;
            private bool disposed;
            internal Lease(BodyComp value, Type block, IBlockBehaviour handler) { body = value; Block = block; behaviour = handler; }
            public void Dispose()
            {
                if (disposed) return;
                var lookup = (Dictionary<Type, IBlockBehaviour>)Lookup.GetValue(body); IBlockBehaviour current;
                if (lookup.TryGetValue(Block, out current) && ReferenceEquals(current, behaviour)) lookup.Remove(Block);
                // Keep a handler if another registration has adopted the same instance.
                if (!lookup.Values.Any(b => ReferenceEquals(b, behaviour))) ((LinkedList<IBlockBehaviour>)Behaviours.GetValue(body)).Remove(behaviour);
                disposed = true;
            }
        }
        internal static Lease Acquire(PlayerEntity player, Type block)
        {
            var lookup = (Dictionary<Type, IBlockBehaviour>)Lookup.GetValue(player.m_body);
            if (lookup.ContainsKey(block)) return null;
            List<ConstructorInfo> recipes;
            if (!Inspect(block.Assembly).TryGetValue(block, out recipes)) return null;
            if (published.Contains(block)) throw new InvalidOperationException("Handler registration also publishes external state; source initialization is required");
            if (recipes.Select(c => c.DeclaringType).Distinct().Count() != 1)
                throw new InvalidOperationException("Multiple native handlers declared for " + block.Name);
            var constructor = recipes.OrderBy(c => c.GetParameters().Length).First();
            object[] services = { player, player.m_body, LevelManager.Instance, player.GetComponent<InputComponent>() };
            var args = constructor.GetParameters().Select(p => {
                object value = services.FirstOrDefault(s => s != null && p.ParameterType.IsInstanceOfType(s));
                if (value == null) throw new InvalidOperationException("Handler requires unavailable dependency: " + p.ParameterType.FullName);
                return value;
            }).ToArray();
            var construction = new GimmickConstruction(constructor.DeclaringType.Assembly);
            var handler = (IBlockBehaviour)construction.Construct(constructor, args);
            var lease = new Lease(player.m_body, block, handler);
            try { if (!player.m_body.RegisterBlockBehaviour(block, handler)) return null; return lease; }
            catch { lease.Dispose(); throw; }
        }
    }
}
