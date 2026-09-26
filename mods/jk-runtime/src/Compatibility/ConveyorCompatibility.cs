using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Security.Cryptography;
using BehaviorTree;
using JumpKing.Player;

namespace JKRuntime.Compatibility
{
    // Patch the reviewed caller, never a shared generic FindNode<T> body.
    // Keep every conveyor condition, ResetResult and velocity update intact.
    internal static class ConveyorCompatibility
    {
        private const string Hash = "9ED9CAF95217D6D44F5AB580A5D118E4EC6A79AE2B3CA048E97ACE13D1709725";
        private const string Owner = "jk-runtime.conveyor-discovery";
        internal static string Status = "Not checked";
        private static Assembly installed;
        private static object patchOwner;
        private static MethodInfo target, transpiler, getInfo;
        private static bool rollbackFailed;

        internal static void TryInstall()
        {
            try { Ensure(); }
            catch (Exception error) { Status = "Unavailable: " + error.GetBaseException().Message; }
        }

        internal static void Ensure()
        {
            try { EnsureCore(); }
            catch (Exception error)
            {
                Status = "Unavailable: " + error.GetBaseException().Message;
                throw;
            }
        }

        private static void EnsureCore()
        {
            if (rollbackFailed) throw new InvalidOperationException("Conveyor rollback incomplete; restart required");
            var mods = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "ConveyorBlockMod").ToArray();
            if (mods.Length == 0) { Status = "Not needed: ConveyorBlockMod not loaded"; return; }
            if (mods.Length != 1) throw new InvalidOperationException("Multiple ConveyorBlockMod assemblies");
            var engines = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            if (engines.Length != 1) throw new InvalidOperationException("Conveyor compatibility requires one loaded Harmony engine");
            if (installed == mods[0])
            {
                if (!HasPatch()) throw new InvalidOperationException("Conveyor discovery hook was removed; restart required");
                return;
            }
            using (var stream = File.OpenRead(mods[0].Location))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != Hash)
                    throw new InvalidOperationException("Unreviewed ConveyorBlockMod build; jump replacement refused");
            var engine = engines[0];
            var harmony = engine.GetType("HarmonyLib.Harmony", true);
            var metadata = engine.GetType("HarmonyLib.HarmonyMethod", true);
            target = mods[0].GetType("ConveyorBlockMod.BlocksBehaviour.ConveyorBlockBehaviour", true)
                .GetMethod("UpdateXVelocityIfExitingTheBlock", BindingFlags.NonPublic | BindingFlags.Instance);
            if (target == null) throw new MissingMethodException("Conveyor exit behaviour");
            transpiler = CreateTranspiler(engine.GetType("HarmonyLib.CodeInstruction", true));
            patchOwner = Activator.CreateInstance(harmony, new object[] { Owner });
            getInfo = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) });
            try
            {
                harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5)
                    .Invoke(patchOwner, new object[] { target, null, null,
                        Activator.CreateInstance(metadata, new object[] { transpiler }), null });
                if (!HasPatch()) throw new InvalidOperationException("Conveyor discovery hook verification failed");
                installed = mods[0];
                Status = "Active: reviewed conveyor jump discovery; Harmony " + engine.GetName().Version;
            }
            catch (Exception failure)
            {
                try
                {
                    harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) })
                        .Invoke(patchOwner, new object[] { target, transpiler });
                }
                catch (Exception rollback)
                {
                    rollbackFailed = true;
                    throw new AggregateException("Conveyor patch rollback failed; restart required", failure, rollback);
                }
                throw;
            }
        }

        private static MethodInfo CreateTranspiler(Type instruction)
        {
            // Harmony 2.2 serializes a MethodInfo by token and loses closed
            // generic arguments. Give all engines a concrete non-generic bridge,
            // typed against their already loaded CodeInstruction assembly.
            var sequence = typeof(IEnumerable<>).MakeGenericType(instruction);
            var callback = typeof(Func<,>).MakeGenericType(sequence, sequence);
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("JKRuntime.ConveyorBridge." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Bridge").DefineType("ConveyorTranspiler", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var field = type.DefineField("Callback", callback, FieldAttributes.Public | FieldAttributes.Static);
            var method = type.DefineMethod("Rewrite", MethodAttributes.Public | MethodAttributes.Static, sequence, new[] { sequence });
            method.DefineParameter(1, ParameterAttributes.None, "instructions");
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, callback.GetMethod("Invoke"));
            il.Emit(OpCodes.Ret);
            var built = type.CreateType();
            built.GetField("Callback").SetValue(null, Delegate.CreateDelegate(callback,
                typeof(ConveyorCompatibility).GetMethod("Rewrite", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(instruction)));
            return built.GetMethod("Rewrite");
        }

        private static bool HasPatch()
        {
            object info = getInfo.Invoke(null, new object[] { target });
            if (info == null) return false;
            var entries = (System.Collections.IEnumerable)info.GetType().GetField("Transpilers").GetValue(info);
            int count = 0;
            foreach (object entry in entries)
                if (Equals(entry.GetType().GetProperty("PatchMethod").GetValue(entry, null), transpiler)
                    && Equals(entry.GetType().GetField("owner").GetValue(entry), Owner)) count++;
            return count == 1;
        }

        private static IEnumerable<T> Rewrite<T>(IEnumerable<T> instructions)
        {
            var list = instructions.ToList();
            var opcode = typeof(T).GetField("opcode");
            var operand = typeof(T).GetField("operand");
            var original = typeof(BTmanager).GetMethod("FindNode").MakeGenericMethod(typeof(JumpState));
            var replacement = typeof(Gameplay.JumpNodeBindings).GetMethod("FindActive", BindingFlags.Static | BindingFlags.NonPublic);
            int count = 0;
            foreach (T instruction in list)
            {
                if (!Equals(operand.GetValue(instruction), original)) continue;
                if (!Equals(opcode.GetValue(instruction), OpCodes.Callvirt) && !Equals(opcode.GetValue(instruction), OpCodes.Call))
                    throw new InvalidOperationException("Unexpected conveyor discovery opcode");
                // Mutate in place to preserve branch labels and exception blocks.
                opcode.SetValue(instruction, OpCodes.Call);
                operand.SetValue(instruction, replacement);
                count++;
            }
            if (count != 1) throw new InvalidOperationException("Expected exactly one conveyor jump lookup, found " + count);
            return list;
        }
    }
}
