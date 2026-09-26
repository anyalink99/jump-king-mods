using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using EntityComponent;
using EntityComponent.BT;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static bool compositionMorphed;
        private static bool CompositionLoad(ref bool __result) { __result = compositionMorphed; return false; }
        private static bool CompositionSave() { return false; }
        private static bool CompositionMorphKey(ref bool __result) { __result = false; return false; }
        private static void MovementComposition(string ballPath, string itemsPath)
        {
            var ball = Assembly.LoadFrom(ballPath); var items = Assembly.LoadFrom(itemsPath);
            Assembly.LoadFrom(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"));
            var store = ball.GetType("MorphBallMod.MorphStateStore", true);
            var ballType = ball.GetType("MorphBallMod.MorphController", true);
            var jetpackType = items.GetType("JumpKingJetpack.JetpackController", true);
            ball.GetType("MorphBallMod.SettingsStore", true).GetProperty("Current", Flags).SetValue(null,
                Activator.CreateInstance(ball.GetType("MorphBallMod.MorphBallSettings", true)), null);
            var soundType = typeof(PlayerEntity).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefsRuntime", true);
            var soundInstance = soundType.BaseType.GetField("instance", Flags); object oldSound = soundInstance.GetValue(null);
            var soundManager = FormatterServices.GetUninitializedObject(soundType);
            soundType.BaseType.GetField("m_settings", Flags).SetValue(soundManager,
                Activator.CreateInstance(typeof(PlayerEntity).Assembly.GetType("JumpKing.PlayerPreferences.SoundPrefs", true)));
            soundInstance.SetValue(null, soundManager);
            bool oldDash = Settings.Current.AirDash; Settings.Current.AirDash = true;
            try
            {
                using (var patches = new OwnedPatches("fixture.movement-composition"))
                using (var world = new RuntimeScope())
                using (var pad = new WalkInputFixture())
                {
                    // Only persistence/physical input are isolated. All movement,
                    // native tree, audio preparation and ownership code is real.
                    patches.Add(store.GetMethod("Load", Flags), prefix: typeof(Tests).GetMethod("CompositionLoad", Flags));
                    patches.Add(store.GetMethod("Save", Flags), prefix: typeof(Tests).GetMethod("CompositionSave", Flags));
                    patches.Add(ball.GetType("MorphBallMod.MorphInput").GetMethod("IsMorphHeld", Flags), prefix: typeof(Tests).GetMethod("CompositionMorphKey", Flags));
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    ball.GetType("MorphBallMod.ModEntry").GetMethod("PrepareWorld").Invoke(null, new object[] { world });
                    items.GetType("MoreItems.ModEntry").GetMethod("PrepareWorld").Invoke(null, new object[] { world });
                    timer.Stop(); Console.WriteLine("[COST] Ball and More Items world audio preparation: " + timer.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
                    foreach (bool ballFirst in new[] { false, true })
                    {
                        DashWorld(new JumpKing.Level.BoxBlock(new Rectangle(0, 320, 480, 40)));
                        var player = DashPlayer();
                        var components = (List<Component>)typeof(Entity).GetField("m_components", Flags).GetValue(player);
                        components.Remove(player.GetComponent<BehaviorTreeComp>());
                        var tree = (BehaviorTreeComp)typeof(PlayerEntity).GetMethod("MakeBT", Flags).Invoke(player, null);
                        player.AddComponents(tree); typeof(PlayerEntity).GetField("m_bt", Flags).SetValue(player, tree);
                        var context = NativeFlight.Get<BehaviourContext>(player.m_body, "m_behaviourContext");
                        var thrust = (IBodyCompBehaviour)Activator.CreateInstance(jetpackType, Flags, null,
                            new object[] { player, (Action)delegate { } }, null);
                        object morph = null;
                        using (var dash = new AirDashController(player))
                        try
                        {
                            compositionMorphed = true;
                            if (!ballFirst) DashPress(dash, player, pad);
                            morph = Activator.CreateInstance(ballType, Flags, null, new object[] { player }, null);
                            if (ballFirst)
                            {
                                Require(PlayerControl.Owner(player.m_body) == "morph-ball", "Restored Ball acquires movement before Dash");
                                DashPress(dash, player, pad);
                                Require(!dash.Active, "Dash cannot preempt restored Ball");
                                DashPad(pad, false, true); typeof(Component).GetMethod("LowUpdate", Flags).Invoke(player.GetComponent<InputComponent>(), new object[] { 1f / 60f });
                                thrust.ExecuteBehaviour(context);
                                DashPad(pad, true, false); typeof(Component).GetMethod("LowUpdate", Flags).Invoke(player.GetComponent<InputComponent>(), new object[] { 1f / 60f });
                                float before = player.m_body.Velocity.Y; thrust.ExecuteBehaviour(context);
                                Require(player.m_body.Velocity.Y < before, "Real Jetpack adds thrust to free-flight Ball");
                                ballType.GetMethod("ForceUnmorph", Flags).Invoke(morph, null);
                            }
                            else
                            {
                                Require(dash.Active && PlayerControl.Owner(player.m_body) == dash.Id, "Saved Ball cannot preempt an active Dash");
                                var before = player.m_body.Velocity;
                                ((IBodyCompBehaviour)morph).ExecuteBehaviour(context); thrust.ExecuteBehaviour(context);
                                Require(player.m_body.Velocity == before, "Ball and Jetpack preserve Dash velocity while denied");
                            }
                        }
                        finally
                        {
                            if (morph != null) { ballType.GetMethod("ForceUnmorph", Flags).Invoke(morph, null); ballType.GetMethod("Dispose", Flags).Invoke(morph, null); }
                        }
                        Require(PlayerControl.Owner(player.m_body) == null && player.GetComponent<AirDashVisual>() == null,
                            "Both orders release body ownership and Dash component");
                    }
                }
            }
            finally { Settings.Current.AirDash = oldDash; soundInstance.SetValue(null, oldSound); }
            Console.WriteLine("[OK] Real Ball/Dash/Jetpack: both ownership orders, restored-form denial, free-flight additive thrust and cleanup");
        }
    }
}
