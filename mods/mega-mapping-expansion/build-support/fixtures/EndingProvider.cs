using System;
using System.Reflection;
using HarmonyLib;
using BehaviorTree;
using JumpKing.GameManager.MultiEnding.NormalEnding;

namespace MoreEndingOptions
{
    public static class Probe
    {
        public static int Calls,TargetCalls;
        public static void Prefix(){Calls++;}
        public static void LatePostfix(ref BTmanager __result){Calls++;}
        public static BTmanager Unsupported(BTmanager __result){Calls++;return __result;}
    }
    [HarmonyPatch]
    public static class DynamicTarget
    {
        [HarmonyTargetMethod] public static MethodBase TargetMethod(){Probe.TargetCalls++;return AccessTools.Method(typeof(NormalEnding),"MakeBT");}
        [HarmonyPostfix] public static void Renamed(ref BTmanager __result){Probe.Calls++;}
    }
}

namespace MoreEndingOptions { public class UnloadableOptionalFeature : EndingFixtureDependency.OptionalBase {} }

namespace MoreEndingOptions { public static class UnloadableSignature { public static void Ignore(EndingFixtureDependency.OptionalBase value) {} } }
