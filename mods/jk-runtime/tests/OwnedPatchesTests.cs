using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace JKRuntime
{
    internal static class OwnedPatchesTests
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Value(int x) { return x + 1; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Replacement(int x) { return x + 10; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int Subject(int x) { return Value(x) * 2; }
        private static void After(ref int __result) { __result += 3; }
        private static MethodInfo Method(string name) { return typeof(OwnedPatchesTests).GetMethod(name, OwnedPatches.Members); }
        private static void Require(bool ok, string text) { if (!ok) throw new Exception(text); }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            Require(Subject(1) == 4, "Native baseline");
            using (var foreign = new OwnedPatches("LegacyMod.Appearance"))
            {
                foreign.Add(Method("Subject"), postfix: Method("After"));
                using (var patches = new OwnedPatches("test.call-sites"))
                {
                    patches.ReplaceCalls(Method("Subject"), Method("Value"), Method("Replacement"), 1);
                    Require(Subject(1) == 25, "Typed call replacement composes with a foreign callback");
                }
                Require(Subject(1) == 7, "Disposal restores native calls while retaining foreign patches");
                var failed = new OwnedPatches("test.rollback"); bool rejected = false;
                try { failed.ReplaceCalls(Method("Subject"), Method("Value"), Method("Replacement"), 2); }
                catch { rejected = true; }
                finally { failed.Dispose(); }
                Require(rejected && Subject(1) == 7, "Unexpected call shape rejects and rolls back only its own patch");
            }
            Require(Subject(1) == 4, "Last owner restores native behaviour");
            Console.WriteLine("[OK] Owned patch groups: typed adapters, native call checks, foreign composition and rollback");
        }
    }
}
