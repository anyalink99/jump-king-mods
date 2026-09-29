using System;
using System.Reflection;

internal static class ContractSmokeTests
{
    private static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            Console.Error.WriteLine("Expected the Ball King assembly path");
            return 1;
        }
        try
        {
            Assembly assembly = Assembly.LoadFrom(args[0]);
            InvokeStatic(assembly, "MorphBallMod.JumpKingContract", "ValidateLevelLoading");
            InvokeStatic(assembly, "MorphBallMod.JumpKingContract", "ValidateRuntime");
            InvokeStatic(assembly, "MorphBallMod.MorphPipelineContract", "Validate");
            TestJumpReplacement(assembly);
            if (args.Length == 2)
            {
                Assembly sfc = Assembly.LoadFrom(args[1]);
                BehaviorTree.IBTnode wrapped = (BehaviorTree.IBTnode)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
                    sfc.GetType("SubframeCharge.SubframeChargeState", true));
                BehaviorTree.BTmanager tree = new BehaviorTree.BTmanager(new BehaviorTree.BTselector(wrapped));
                object found = assembly.GetType("MorphBallMod.MorphJumpNodeResolver", true).GetMethod(
                    "Find", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { tree });
                if (!ReferenceEquals(found, wrapped)) throw new Exception("Ball King failed to find built SFC replacement");
                Console.WriteLine("[OK] Ball King discovery against actual built SubframeChargeState");
            }
            Console.WriteLine("[OK] Ball King installed-game contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Exception cause = exception is TargetInvocationException
                && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
            Console.Error.WriteLine(cause);
            return 1;
        }
    }

    private sealed class WrappedJump : JumpKing.Player.JumpState
    { internal WrappedJump() : base(null) { } }

    private static void TestJumpReplacement(Assembly assembly)
    {
        MethodInfo find = assembly.GetType("MorphBallMod.MorphJumpNodeResolver", true).GetMethod(
            "Find", BindingFlags.Static | BindingFlags.NonPublic);
        JumpKing.Player.JumpState original = new JumpKing.Player.JumpState(null);
        WrappedJump wrapped = new WrappedJump();
        BehaviorTree.BTselector root = new BehaviorTree.BTselector(original);
        BehaviorTree.BTmanager tree = new BehaviorTree.BTmanager(root);
        if (!ReferenceEquals(find.Invoke(null, new object[] { tree }), original)) throw new Exception("native jump lookup");
        FieldInfo children = typeof(BehaviorTree.IBTcomposite).GetField("m_children", BindingFlags.Instance | BindingFlags.NonPublic);
        if (children == null) throw new Exception("native composite children contract");
        BehaviorTree.IBTnode[] nodes = (BehaviorTree.IBTnode[])children.GetValue(root);
        nodes[0] = wrapped;
        if (tree.FindNode<JumpKing.Player.JumpState>() != null) throw new Exception("native exact-type contract changed");
        if (!ReferenceEquals(find.Invoke(null, new object[] { tree }), wrapped)) throw new Exception("replacement-first lookup");
        nodes[0] = original;
        if (!ReferenceEquals(find.Invoke(null, new object[] { tree }), original)) throw new Exception("toggle restoration reused stale node");
        Console.WriteLine("[OK] Ball King jump lookup: native, subclass replacement, restoration; no cached old node");
    }

    private static void InvokeStatic(
        Assembly assembly,
        string typeName,
        string methodName)
    {
        Type type = assembly.GetType(typeName, true);
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new MissingMethodException(typeName, methodName);
        }
        method.Invoke(null, null);
    }
}
