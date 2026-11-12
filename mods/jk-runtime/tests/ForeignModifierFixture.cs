using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using JumpKing.Mods;

[assembly: AssemblyTitle("Fixture library title is NOT the native mod name")]

[JumpKingMod("Third-party fixture mod")]
public static class ForeignModifierFixture
{
    public sealed class Behaviour : IBodyCompBehaviour
    { public bool ExecuteBehaviour(BehaviourContext context) { return true; } }
    public static IBodyCompBehaviour Create() { return new Behaviour(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Add(BodyComp body, IBodyCompBehaviour behaviour) { return body.RegisterBehaviour(behaviour); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Before(BodyComp body, IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor) { return body.RegisterBehaviourBefore(behaviour, anchor); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool After(BodyComp body, IBodyCompBehaviour behaviour, IBodyCompBehaviour anchor) { return body.RegisterBehaviourAfter(behaviour, anchor); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Remove(BodyComp body, IBodyCompBehaviour behaviour) { return body.RemoveBehaviour(behaviour); }
    public static bool Skip(ref bool __result) { __result = true; return false; }
    public static bool SkipReset() { return false; }
    public static void Throw() { throw new InvalidOperationException("Foreign fixture failure"); }
    public static int Touches;
    public static void Touch() { Touches++; }
    public static void Nested(BodyComp __instance) { __instance.RegisterBehaviour(new Behaviour()); }
}
