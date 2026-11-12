using System.Runtime.CompilerServices;
public static class HarmonyFixture
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Target() { return 7; }
    public static void Postfix(ref int __result) { __result++; }
}
