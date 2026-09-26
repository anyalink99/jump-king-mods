using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace JKRuntime
{
    /// <summary>A retryable group of patches in the already loaded shared Harmony 2 engine. Never loads an engine or removes foreign callbacks.</summary>
    public sealed class OwnedPatches : IDisposable
    {
        public const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private readonly object owner;
        private readonly Type metadata;
        private readonly MethodInfo patch, unpatch;
        private readonly List<Tuple<MethodBase, MethodInfo>> applied = new List<Tuple<MethodBase, MethodInfo>>();
        private readonly Assembly engine;
        private bool closing;
        private static ModuleBuilder adapters;
        private static int sequence;
        private static readonly Dictionary<Tuple<MethodInfo, MethodInfo, int, bool, Assembly>, MethodInfo> callAdapters = new Dictionary<Tuple<MethodInfo, MethodInfo, int, bool, Assembly>, MethodInfo>();

        public static Assembly SharedEngine()
        {
            var candidates = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            if (candidates.Length != 1 || candidates[0].GetName().Version.Major != 2)
                throw new InvalidOperationException("Expected one loaded Harmony 2 engine; found " + candidates.Length);
            return candidates[0];
        }

        public OwnedPatches(string id)
        {
            RuntimeApi.Kernel.CheckThread();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Any(char.IsControl)) throw new ArgumentException("A nonempty Harmony owner ID is required", "id");
            engine = SharedEngine();
            var harmony = engine.GetType("HarmonyLib.Harmony", true);
            metadata = engine.GetType("HarmonyLib.HarmonyMethod", true);
            owner = Activator.CreateInstance(harmony, new object[] { id });
            patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) });
            if (unpatch == null) throw new MissingMethodException("Harmony.Unpatch(MethodBase, MethodInfo)");
        }

        public void Add(MethodBase target, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null, int priority = 400)
        {
            RuntimeApi.Kernel.CheckThread();
            if (closing) throw new ObjectDisposedException("OwnedPatches");
            if (target == null || target.ContainsGenericParameters) throw new ArgumentException("A concrete native method is required", "target");
            var entries = new object[] { target, null, null, null, null };
            var callbacks = new[] { prefix, postfix, transpiler, finalizer };
            for (int i = 0; i < callbacks.Length; i++)
            {
                var callback = callbacks[i];
                if (callback == null) continue;
                if (!callback.IsStatic) throw new ArgumentException("Patch callbacks must be static");
                var entry = Activator.CreateInstance(metadata, new object[] { callback });
                metadata.GetField("priority").SetValue(entry, priority);
                entries[i + 1] = entry;
                applied.Add(Tuple.Create(target, callback));
            }
            patch.Invoke(owner, entries);
        }

        /// <summary>Replace an exact number of call sites. Labels and exception boundaries are retained; an unexpected native body rejects installation.</summary>
        public void ReplaceCalls(MethodInfo target, MethodInfo original, MethodInfo replacement, int expected, bool immediateLength = false)
        {
            RuntimeApi.Kernel.CheckThread();
            if (original == null || replacement == null || !replacement.IsStatic || expected <= 0) throw new ArgumentException("Invalid call replacement");
            var parameters = original.GetParameters().Select(p => p.ParameterType).ToList();
            if (!original.IsStatic) parameters.Insert(0, original.DeclaringType);
            if (replacement.ReturnType != original.ReturnType || !parameters.SequenceEqual(replacement.GetParameters().Select(p => p.ParameterType)))
                throw new ArgumentException("Call replacement must preserve the native stack signature");
            var key = Tuple.Create(original, replacement, expected, immediateLength, engine);
            MethodInfo existing;
            if (callAdapters.TryGetValue(key, out existing)) { Add(target, transpiler: existing); return; }
            var instruction = engine.GetType("HarmonyLib.CodeInstruction", true);
            var opcode = instruction.GetField("opcode"); var operand = instruction.GetField("operand");
            Func<IEnumerable, IEnumerable> rewrite = source => {
                var list = source.Cast<object>().ToArray(); int count = 0;
                for (int i = 0; i < list.Length; i++)
                {
                    OpCode code = (OpCode)opcode.GetValue(list[i]);
                    if ((code != OpCodes.Call && code != OpCodes.Callvirt) || !Equals(operand.GetValue(list[i]), original)) continue;
                    if (immediateLength && (i + 1 >= list.Length || (OpCode)opcode.GetValue(list[i + 1]) != OpCodes.Ldlen))
                        throw new NotSupportedException("Native result is no longer an immediate length query");
                    opcode.SetValue(list[i], OpCodes.Call); operand.SetValue(list[i], replacement); count++;
                }
                if (count != expected) throw new NotSupportedException("Native call count changed: expected " + expected + ", found " + count);
                Array result = Array.CreateInstance(instruction, list.Length); Array.Copy(list, result, list.Length); return result;
            };
            // Emit only a typed ABI adapter; the rewrite logic stays in Runtime.
            // No compiler process, source generation or Harmony dependency in the SDK.
            if (adapters == null) adapters = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("JKRuntime.PatchAdapters"), AssemblyBuilderAccess.Run).DefineDynamicModule("Adapters");
            var type = adapters.DefineType("CallSites" + (++sequence), TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var callbackField = type.DefineField("Callback", typeof(Func<IEnumerable, IEnumerable>), FieldAttributes.Public | FieldAttributes.Static);
            Type enumerable = typeof(IEnumerable<>).MakeGenericType(instruction);
            var method = type.DefineMethod("Rewrite", MethodAttributes.Public | MethodAttributes.Static, enumerable, new[] { enumerable });
            method.DefineParameter(1, ParameterAttributes.None, "instructions");
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldsfld, callbackField); il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, typeof(Func<IEnumerable, IEnumerable>).GetMethod("Invoke")); il.Emit(OpCodes.Castclass, enumerable); il.Emit(OpCodes.Ret);
            var created = type.CreateType(); created.GetField("Callback").SetValue(null, rewrite);
            var adapter = created.GetMethod("Rewrite"); callAdapters.Add(key, adapter);
            Add(target, transpiler: adapter);
        }

        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread(); closing = true;
            var errors = new List<Exception>();
            for (int i = applied.Count - 1; i >= 0; i--)
                try { unpatch.Invoke(owner, new object[] { applied[i].Item1, applied[i].Item2 }); applied.RemoveAt(i); }
                catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Patch removal remains incomplete", errors);
        }
    }
}
