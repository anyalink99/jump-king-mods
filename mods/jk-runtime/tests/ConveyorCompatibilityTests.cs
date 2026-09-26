using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using JKRuntime.Gameplay;
using Microsoft.Xna.Framework;

internal static class ConveyorCompatibilityTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private sealed class CustomJump : JumpState { public CustomJump() : base(null) { } }
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static readonly FieldInfo Result = typeof(IBTnode).GetField("m_last_result", Flags);
    private static Type behaviourType, blockType;
    private static PlayerEntity player;
    private static BehaviourContext Context(float y, IBlock block)
    {
        player.m_body.Velocity = new Vector2(3, y);
        var context = new BehaviourContext(player.m_body);
        var collision = new AdvCollisionInfo(block == null ? new List<IBlock>() : new List<IBlock> { block }, false, SlopeType.None, Vector2.Zero);
        typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo", Flags).Invoke(context.CollisionInfo, new object[] { collision });
        return context;
    }
    private static object Behaviour(float speed, int history)
    {
        var behaviour = Activator.CreateInstance(behaviourType);
        var block = Activator.CreateInstance(blockType, new object[] { new Rectangle(0, 0, 50, 10), (byte)2, (byte)100 });
        blockType.GetProperty("Speed").SetValue(block, speed, null);
        behaviourType.GetField("_collidedConveyorBlock").SetValue(behaviour, block);
        var fields = new[] { "IsPlayerOnBlockLastFrame", "IsPlayerOnBlock2FramesBefore", "IsPlayerOnBlock3FramesBefore" };
        for (int i = 0; i < fields.Length; i++) behaviourType.GetField(fields[i]).SetValue(behaviour, (history & (1 << i)) != 0);
        return behaviour;
    }
    private static void Exit(object behaviour, BehaviourContext context)
    { ((IBlockBehaviour)behaviour).ExecuteBlockBehaviour(context); }
    private static void Cases(JumpState active)
    {
        foreach (float speed in new[] { -4f, -0.5f, 0f, 0.5f, 4f })
        foreach (float y in new[] { -1f, 0f, 1f })
        foreach (int history in new[] { 0, 1, 3, 5, 6, 7 })
        foreach (bool box in new[] { false, true })
        {
            Result.SetValue(active, BTresult.Running);
            var context = Context(y, box ? new BoxBlock(new Rectangle(0, 0, 20, 10)) : null);
            Exit(Behaviour(speed, history), context);
            Check(player.m_body.Velocity == new Vector2(3 + (history == 7 ? speed : 0), y), "Conveyor changed native exit velocity");
            Check(active.last_result == (history == 7 && y >= 0 && !box ? BTresult.NULL : BTresult.Running), "Wrong live jump reset condition");
        }
        var onBlock = Behaviour(4, 7);
        Result.SetValue(active, BTresult.Running);
        Exit(onBlock, Context(0, (IBlock)behaviourType.GetField("_collidedConveyorBlock").GetValue(onBlock)));
        Check(active.last_result == BTresult.Running && player.m_body.Velocity.X == 3, "Remaining on belt is not an exit");
    }
    public static int Main(string[] args)
    {
        try
        {
            var adapterType = typeof(JumpNodeBindings).Assembly.GetType("JKRuntime.Compatibility.ConveyorCompatibility", true);
            adapterType.GetMethod("TryInstall", Flags).Invoke(null, null); // No conveyor yet; late native discovery must work.
            var engine = args[0] == "none" ? null : Assembly.LoadFrom(args[0]);
            var conveyor = Assembly.LoadFrom(args[1]);
            behaviourType = conveyor.GetType("ConveyorBlockMod.BlocksBehaviour.ConveyorBlockBehaviour", true);
            blockType = conveyor.GetType("ConveyorBlockMod.Blocks.ConveyorBlock", true);
            var manager = new EntityManager();
            player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            typeof(Entity).GetField("m_components", Flags).SetValue(player, new List<Component>());
            player.m_body = new BodyComp(Vector2.Zero, 18, 26);
            manager.AddObject(player);
            var native = new JumpState(player);
            bool refusal = args.Length > 2 && args[2] == "refuse";
            var replacement = args.Length > 2 && !refusal
                ? (JumpState)FormatterServices.GetUninitializedObject(Assembly.LoadFrom(args[2]).GetType("SubframeCharge.SubframeChargeState", true))
                : new CustomJump();
            var root = new BTsequencor(native);
            var tree = new BehaviorTreeComp(root);
            player.AddComponents(player.m_body, tree);
            typeof(PlayerEntity).GetField("m_jump_state", Flags).SetValue(player, native);
            if (refusal)
            {
                try { JumpNodeBindings.Replace(tree.GetRaw(), player, native, replacement); throw new Exception("Unsupported compatibility accepted"); }
                catch (InvalidOperationException)
                {
                    Check(ReferenceEquals(root.Children[0], native)
                        && ReferenceEquals(typeof(PlayerEntity).GetField("m_jump_state", Flags).GetValue(player), native), "Refusal mutated the native graph");
                    Check(((string)adapterType.GetField("Status", Flags).GetValue(null)).StartsWith("Unavailable:"), "Refusal missing from diagnostics");
                }
                Console.WriteLine("[OK] Unsupported conveyor/engine refused without graph mutation");
                return 0;
            }
            Cases(native);
            // Reproduce the exact crash before any Runtime intervention.
            root.Children[0] = replacement;
            Check(tree.GetRaw().FindNode<JumpState>() == null, "Native lookup unexpectedly supports subclasses");
            try { Exit(Behaviour(4, 7), Context(0, null)); throw new Exception("Reported crash did not reproduce"); }
            catch (NullReferenceException error) { Check(error.StackTrace.Contains("UpdateXVelocityIfExitingTheBlock"), "Unexpected null reference source"); }
            Console.WriteLine("[OK] Original installed ConveyorBlockMod reproduces the reported exit NullReferenceException");
            root.Children[0] = native;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                var bindings = JumpNodeBindings.Replace(tree.GetRaw(), player, native, replacement);
                Check(tree.GetRaw().FindNode<JumpState>() == null, "Compatibility changed native generic lookup globally");
                Check(tree.GetRaw().FindNode<BTsequencor>() == root, "Unrelated generic lookup changed");
                Check(tree.GetRaw().FindNode<CustomJump>() == (replacement as CustomJump), "Shared generic instantiation corrupted");
                Cases(replacement);
                bindings.Restore(); bindings.Restore();
                Check(ReferenceEquals(tree.GetRaw().FindNode<JumpState>(), native), "Restore lost original jump");
                Cases(native);
            }
            // Missing/ambiguous graphs must not manufacture a node or swallow errors.
            var find = typeof(JumpNodeBindings).GetMethod("FindActive", Flags);
            foreach (var invalid in new IBTnode[] { new BTsequencor(), new BTsequencor(native, replacement) })
            {
                try { find.Invoke(null, new object[] { new BTmanager(invalid) }); throw new Exception("Malformed graph accepted"); }
                catch (TargetInvocationException error) { Check(error.InnerException is InvalidOperationException, "Wrong graph rejection"); }
            }
            var shared = new BTmanager(new BTsequencor(replacement, new BTIsNodeRunning(replacement), new StaticNode(replacement, BTresult.Success)));
            Check(ReferenceEquals(find.Invoke(null, new object[] { shared }), replacement), "Shared graph references counted as multiple jumps");
            // Do not silently overwrite an external removal or permit unsafe replacement.
            var adapter = typeof(JumpNodeBindings).Assembly.GetType("JKRuntime.Compatibility.ConveyorCompatibility", true);
            var target = (MethodInfo)adapter.GetField("target", Flags).GetValue(null);
            var hook = (MethodInfo)adapter.GetField("transpiler", Flags).GetValue(null);
            var harmony = engine.GetType("HarmonyLib.Harmony", true);
            var owner = Activator.CreateInstance(harmony, new object[] { "conveyor.tests" });
            harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) }).Invoke(owner, new object[] { target, hook });
            try { JumpNodeBindings.Replace(tree.GetRaw(), player, native, replacement); throw new Exception("Missing protection accepted"); }
            catch (InvalidOperationException) { Check(ReferenceEquals(tree.GetRaw().FindNode<JumpState>(), native), "Failed protection changed the graph"); }
            Console.WriteLine("[OK] Conveyor compatibility: " + checks + " checks; " + replacement.GetType().FullName + "; Harmony " + engine.GetName().Version);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
