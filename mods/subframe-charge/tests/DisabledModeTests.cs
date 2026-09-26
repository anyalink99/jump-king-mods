using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using EntityComponent.BT;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Player;
using Microsoft.Xna.Framework;

internal static class DisabledModeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool NoPhysicalInput(ref bool __result) { __result = false; return false; }
    private static bool held, pressed;
    private static bool HeldInput(ref InputComponent.State __result) { __result = new InputComponent.State { jump = held }; return false; }
    private static bool PressedInput(ref InputComponent.State __result) { __result = new InputComponent.State { jump = pressed }; return false; }
    private static void BufferPoses(PlayerEntity player, BehaviorTreeComp tree, MethodInfo apply, object charge)
    {
        var ground = typeof(BodyComp).GetField("_is_on_ground", Flags);
        var sprite = typeof(PlayerEntity).GetField("m_sprite", Flags);
        var input = player.GetComponent<InputComponent>();
        var updateInput = typeof(InputComponent).GetMethod("Update", Flags);
        tree.Reset();
        for (int cycle = 0; cycle < 20; cycle++)
        {
            ground.SetValue(player.m_body, false); player.m_body.Velocity = new Vector2(0, 2);
            held = pressed = false; updateInput.Invoke(input, new object[] { 1f / 60f }); tree.GetRaw().Run(1f / 60f);
            held = pressed = true; updateInput.Invoke(input, new object[] { 1f / 60f }); tree.GetRaw().Run(1f / 60f);
            pressed = false;
            ground.SetValue(player.m_body, true); player.m_body.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 8; frame++)
            {
                apply.Invoke(null, null);
                updateInput.Invoke(input, new object[] { 1f / 60f }); tree.GetRaw().Run(1f / 60f);
                Check(ReferenceEquals(sprite.GetValue(player), charge), "Buffered charge showed another pose at cycle " + cycle + ", frame " + frame);
            }
            held = false; updateInput.Invoke(input, new object[] { 1f / 60f }); tree.GetRaw().Run(1f / 60f);
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            var mod = Assembly.LoadFrom(args[0]);
            var engine = Assembly.LoadFrom(args[1]);
            AppDomain.CurrentDomain.SetData("SubframeCharge.LogDirectory", AppDomain.CurrentDomain.BaseDirectory);
            var state = mod.GetType("SubframeCharge.SubframeChargeState", true);
            var harmony = engine.GetType("HarmonyLib.Harmony", true);
            var metadata = engine.GetType("HarmonyLib.HarmonyMethod", true);
            var owner = Activator.CreateInstance(harmony, new object[] { "sfc.tests.disabled" });
            var patch = harmony.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            foreach (string method in new[] { "ConfigureSampler", "IsGameActive" })
            {
                var prefix = Activator.CreateInstance(metadata, new object[] { typeof(DisabledModeTests).GetMethod("NoPhysicalInput", Flags) });
                patch.Invoke(owner, new object[] { state.GetMethod(method, Flags), prefix, null, null, null });
            }
            var settings = mod.GetType("SubframeCharge.SettingsStore", true);
            object current = Activator.CreateInstance(mod.GetType("SubframeCharge.SubframeChargeSettings", true));
            settings.GetProperty("Current", Flags).SetValue(null, current, null);
            settings.GetField("loaded", Flags).SetValue(null, true);
            current.GetType().GetProperty("Enabled").SetValue(current, false, null);
            current.GetType().GetProperty("ShowMeasurement").SetValue(current, false, null);
            var installer = mod.GetType("SubframeCharge.SubframeChargeInstaller", true);
            var apply = installer.GetMethod("ApplyCurrentMode", Flags);
            apply.Invoke(null, null); // Fully off also works without a player/window.
            var manager = new EntityManager();
            var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            typeof(Game1).GetField("_instance", Flags).SetValue(null, game);
            game.contentManager = (JKContentManager)FormatterServices.GetUninitializedObject(typeof(JKContentManager));
            game.contentManager.playerSprites = new JKContentManager.PlayerSprites { _CurrentSprites = new JumpKing.JKMemory.LayeredKingSprites(null) };
            game.contentManager.playerSprites.AddLayer(new JumpKing.JKMemory.KingSprites(null));
            ControllerManager.instance = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", Flags).SetValue(ControllerManager.instance, new List<PadInstance>());
            var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            typeof(Entity).GetField("m_components", Flags).SetValue(player, new List<Component>());
            manager.AddObject(player);
            player.m_body = new BodyComp(new Vector2(150, 268), 18, 26);
            typeof(PlayerEntity).GetField("m_screen_shake", Flags).SetValue(player,
                FormatterServices.GetUninitializedObject(typeof(JumpKing.MiscSystems.ScreenShakeController)));
            var tree = (BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT", Flags).Invoke(player, null);
            player.AddComponents(player.m_body, new InputComponent(), tree);
            var originalComponents = player.GetComponents();
            var jump = tree.GetRaw().FindNode<JumpState>();
            var timer = typeof(JumpState).GetField("m_timer", Flags);
            var sprite = typeof(PlayerEntity).GetField("m_sprite", Flags);
            var charge = game.contentManager.playerSprites.jump_charge;
            player.SetSprite(charge); timer.SetValue(jump, 0.125f);
            for (int i = 0; i < 100; i++) apply.Invoke(null, null);
            Check(ReferenceEquals(jump, tree.GetRaw().FindNode<JumpState>()) && (float)timer.GetValue(jump) == 0.125f,
                "Fully disabled refresh changed the native charge node/timer");
            Check(ReferenceEquals(sprite.GetValue(player), charge) && player.GetComponents().SequenceEqual(originalComponents),
                "Fully disabled refresh changed the charge sprite/component order");
            for (int i = 0; i < 3; i++)
            {
                current.GetType().GetProperty("ShowMeasurement").SetValue(current, true, null);
                apply.Invoke(null, null);
                Check(installer.GetField("replacementJumpState", Flags).GetValue(null) != null && JKRuntime.Input.SharedActionSampler.ActiveStreams == 1,
                    "Explicit measurement did not activate the observer");
                current.GetType().GetProperty("ShowMeasurement").SetValue(current, false, null);
                apply.Invoke(null, null);
                Check(ReferenceEquals(jump, tree.GetRaw().FindNode<JumpState>()) && player.GetComponents().SequenceEqual(originalComponents)
                    && JKRuntime.Input.SharedActionSampler.ActiveStreams == 0, "Disabling both settings leaked a replacement, sampler or player component");
            }
            installer.GetMethod("Uninstall", Flags).Invoke(null, null);
            foreach (var pair in new[] { new[] { "GetState", "HeldInput" }, new[] { "GetPressedState", "PressedInput" } })
            {
                var prefix = Activator.CreateInstance(metadata, new object[] { typeof(DisabledModeTests).GetMethod(pair[1], Flags) });
                patch.Invoke(owner, new object[] { typeof(InputComponent).GetMethod(pair[0]), prefix, null, null, null });
            }
            BufferPoses(player, tree, apply, charge);
            Console.WriteLine("[OK] Fully disabled SFC: original native charge node/timer/sprite/order; explicit measurement toggles restore every component and sampler");
            Console.WriteLine("[OK] Complete native behavior tree: 20 held-landing buffers retain the charge sprite on every charging frame with SFC fully off");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
