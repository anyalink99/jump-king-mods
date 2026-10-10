using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using BehaviorTree;
using EntityComponent;
using JKRuntime.Gameplay;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime
{
    internal static class DebugJumpPromptTests
    {
        private static InputComponent.State raw;
        private static JumpState node;
        private static PlayerEntity player;
        private static float multiplier, intensity;
        private static int direction, launches;
        private static JumpResult result;
        private static bool Yes(ref bool __result) { __result = true; return false; }
        private static bool Input(ref InputComponent.State __result) { __result = raw; return false; }
        private static bool Node(ref JumpState __result) { __result = node; return false; }
        private static bool Multiplier(ref float __result) { __result = multiplier; return false; }
        private static bool NoBlock(ref bool __result) { __result = false; return false; }
        private static bool Start() { return false; }
        private static bool Launch(float p_intensity)
        {
            intensity = p_intensity; launches++;
            direction = player.GetComponent<InputComponent>().GetState().dpad.X;
            player.m_body.Velocity.Y = -10 * p_intensity;
            typeof(JumpState).GetMethod("Reset", OwnedPatches.Members).Invoke(node, null);
            return false;
        }
        private static MethodInfo Method(Type type, string name) { return type.GetMethod(name, OwnedPatches.Members); }
        private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            var model = new DebugJumpPrompt.Model();
            model.Pointer(true, false, true, 100, 100, false, 1); Check(!model.Armed, "Outside click must not arm");
            model.Pointer(false, true, true, 100, 100, false, 1);
            model.Pointer(true, true, true, 100, 100, false, 1);
            model.Pointer(true, true, true, 100, 84, false, 1); Check(model.Armed && model.Frames == 16, "Drag up increases frame count");
            model.Pointer(false, true, true, 100, 84, false, 1); Check(model.Armed && !model.Dragging, "Release keeps prompt");
            model.Pointer(true, true, true, 100, 84, false, 1); Check(model.Armed, "Second press waits for click or drag");
            model.Pointer(true, true, true, 100, 100, false, 1); Check(model.Armed && model.Frames == 12, "Another drag adjusts the existing prompt");
            model.Pointer(false, true, true, 100, 100, false, 1); Check(model.Armed, "Repeated drag release keeps prompt");
            model.Pointer(true, true, true, 100, 100, false, 1);
            model.Pointer(false, true, true, 100, 100, false, 1); Check(!model.Armed, "Stationary click disables on release");
            model.Pointer(true, true, true, 100, 100, true, 1);
            model.Pointer(false, true, true, 100, 100, true, 1); Check(model.Armed, "Initial stationary click enables instead of immediately disabling");
            model.Pointer(true, true, true, 100, 100, true, 1);
            model.Pointer(false, true, true, 100, 99, true, 1); Check(model.Armed && model.Frames == 12.25f, "Release position counts as a quarter-step drag");
            model.Pointer(true, true, true, 100, 99, true, 1);
            model.Pointer(true, true, true, 101, 99, true, 1);
            model.Pointer(false, true, true, 100, 99, true, 1); Check(model.Armed && model.Frames == 12.25f, "Horizontal motion and returning to the origin still count as a drag");
            model.Pointer(true, true, true, 100, 99, true, 1);
            model.Pointer(true, true, false, 100, 0, true, 1); Check(!model.Dragging, "Focus loss releases capture");
            model.Pointer(true, true, true, 100, 0, true, 1); Check(model.Frames == 12.25f, "Held button cannot resume stale drag");
            model.Pointer(false, true, true, 100, 0, true, 1); Check(model.Armed, "Interrupted gesture cannot become a disable click");
            model.Pointer(true, false, true, 100, 0, true, 1);
            model.Pointer(false, true, true, 100, 0, true, 1); Check(model.Armed, "Click starting outside cannot disable");
            Check(DebugJumpPrompt.ClampFrames(-5, true, 1) == 1 && DebugJumpPrompt.ClampFrames(1000, true, 1) == 35, "Dry bounds");
            Check(DebugJumpPrompt.ClampFrames(1000, true, .5f) == 71, "Water bounds");
            Check(DebugJumpPrompt.ClampFrames(12.25f, false, 1) == 12, "Disabling quarters requantizes");
            foreach (double interval in new[] { .5, .25, .125, 1.0 / 17 })
            {
                var fine = new DebugJumpPrompt.Model();
                fine.Pointer(true, true, true, 100, 100, interval, 1);
                fine.Pointer(false, true, true, 100, 99, interval, 1);
                Check(Math.Abs(fine.Frames - (12 + interval)) < .000002, "One pixel reaches the next selected substep");
                Check(DebugJumpPrompt.ClampFrames(1000, interval, 1) == 35, "Substep dry maximum");
                Check(DebugJumpPrompt.ClampFrames(1000, interval, .5f) == 71, "Substep water maximum");
                Check(DebugJumpPrompt.ClampFrames(-1, interval, 1) == 1, "Substep minimum");
            }
            foreach (double invalid in new[] { 0.0, -.25, 1.1, double.NaN, double.PositiveInfinity })
            {
                bool rejected = false;
                try { DebugJumpPrompt.ClampFrames(12, invalid, 1); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "Invalid prompt interval rejected");
            }
            using (DebugJumpPrompt.RegisterQuarterSteps(delegate { return true; }))
            {
                var read = (Func<double>)typeof(DebugJumpPrompt).GetField("chargeStep", OwnedPatches.Members).GetValue(null);
                Check(read() == .25, "Legacy quarter registration keeps its interval");
                bool duplicate = false;
                try { DebugJumpPrompt.RegisterChargeStep(delegate { return .5; }); }
                catch (InvalidOperationException) { duplicate = true; }
                Check(duplicate, "Two charge step providers cannot own the same prompt");
            }
            using (DebugJumpPrompt.RegisterChargeStep(delegate { return 1.0 / 17; }))
                Check(typeof(DebugJumpPrompt).GetField("chargeStep", OwnedPatches.Members).GetValue(null) != null, "Disposed legacy registration releases the slot");

            player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            typeof(Entity).GetField("m_components", OwnedPatches.Members).SetValue(player, new List<Component>());
            player.m_body = new BodyComp(Vector2.Zero, 18, 26);
            var input = new InputComponent(); player.AddComponents(player.m_body, input);
            node = new JumpState(player);
            var prompt = (DebugJumpPrompt.Prompt)FormatterServices.GetUninitializedObject(typeof(DebugJumpPrompt.Prompt));
            typeof(DebugJumpPrompt.Prompt).GetField("Player", OwnedPatches.Members).SetValue(prompt, player);
            typeof(DebugJumpPrompt.Prompt).GetField("Model", OwnedPatches.Members).SetValue(prompt, model);
            typeof(DebugJumpPrompt).GetField("current", OwnedPatches.Members).SetValue(null, prompt);
            using (var hooks = new OwnedPatches("test.prompt"))
            using (JumpEvents.Subscribe(value => result = value))
            {
                hooks.Add(typeof(DebugJumpPrompt.Prompt).GetProperty("Available", OwnedPatches.Members).GetGetMethod(true), prefix: Method(typeof(DebugJumpPromptTests), "Yes"));
                hooks.Add(typeof(DebugJumpPrompt.Prompt).GetProperty("Node", OwnedPatches.Members).GetGetMethod(true), prefix: Method(typeof(DebugJumpPromptTests), "Node"));
                hooks.Add(Method(typeof(DebugJumpPrompt.Prompt), "Multiplier"), prefix: Method(typeof(DebugJumpPromptTests), "Multiplier"));
                hooks.Add(Method(typeof(BodyComp), "GetMultipliers"), prefix: Method(typeof(DebugJumpPromptTests), "Multiplier"));
                hooks.Add(typeof(BodyComp).GetMethod("IsOnBlock", new[] { typeof(Type) }), prefix: Method(typeof(DebugJumpPromptTests), "NoBlock"));
                hooks.Add(Method(typeof(JumpState), "Start"), prefix: Method(typeof(DebugJumpPromptTests), "Start"));
                hooks.Add(Method(typeof(JumpState), "DoJump"), prefix: Method(typeof(DebugJumpPromptTests), "Launch"));
                hooks.Add(Method(typeof(InputComponent), "GetState"), prefix: Method(typeof(DebugJumpPromptTests), "Input"), postfix: Method(typeof(DebugJumpPrompt), "InputState"));
                hooks.Add(Method(typeof(JumpState), "MyRun"), prefix: Method(typeof(DebugJumpPrompt), "BeforeRun"), finalizer: Method(typeof(DebugJumpPrompt), "AfterRun"));
                foreach (float scale in new[] { 1f, .5f })
                foreach (int divisions in new[] { 2, 4, 8, 17 })
                {
                    multiplier = scale;
                    for (int step = divisions; step <= (scale == 1 ? 35 : 71) * divisions; step++)
                    {
                        float frames = (float)step / divisions;
                        model.Armed = true; model.Frames = frames; model.Charging = model.WaitRelease = false; model.Direction = -1;
                        raw = new InputComponent.State { jump = true, left = true };
                        typeof(InputComponent).GetField("_can_jump", OwnedPatches.Members).SetValue(input, true);
                        result = null; intensity = 0; int previous = launches, ticks = 0;
                        BTresult status;
                        do
                        {
                            status = (BTresult)Method(typeof(JumpState), "MyRun").Invoke(node, new object[] { new TickData(1f / 60, 0) });
                            ticks++;
                            // Release jump immediately, tap the opposite direction halfway through.
                            raw = new InputComponent.State { right = ticks == 2 };
                            Check(ticks < 80, "Prompt must finish");
                        } while (status == BTresult.Running);
                        Check(status == BTresult.Success && launches == previous + 1, "One tap gives one launch");
                        Check(Math.Abs(intensity - Math.Min(1, (frames + 1) * scale / 36)) < .00001f, "Exact native launch intensity at " + frames);
                        Check(result != null && result.CorrectedFrameCount == frames && !result.HoldMilliseconds.HasValue, "Exact frames without invented physical measurement");
                        Check(direction == (ticks >= 3 ? 1 : -1), "Direction tap remains latched");
                        Check(model.Direction == 0, "Takeoff clears the direction arrow");
                        raw.jump = true;
                        Check((BTresult)Method(typeof(JumpState), "MyRun").Invoke(node, new object[] { new TickData(1f / 60, 0) }) == BTresult.Failure, "Held jump cannot retrigger");
                    }
                }
                multiplier = 1;
                model.Armed = true; model.Frames = 12.25f; model.WaitRelease = false;
                raw = new InputComponent.State { left = true };
                Check(Run() == BTresult.Failure && model.Direction == 0, "Walking while armed must not select a direction");
                // Follow the previous directed jump with a fresh neutral charge.
                raw = new InputComponent.State { jump = true };
                typeof(InputComponent).GetField("_can_jump", OwnedPatches.Members).SetValue(input, true);
                Check(Run() == BTresult.Running && model.Direction == 0, "Neutral charge starts without the previous jump's direction");
                raw = new InputComponent.State();
                while (Run() == BTresult.Running) Check(model.Direction == 0, "Neutral charge stays neutral");
                Check(direction == 0 && model.Direction == 0, "Neutral prompt launches vertically after a directed jump and earlier movement");

                model.WaitRelease = false;
                raw = new InputComponent.State { jump = true, right = true };
                typeof(InputComponent).GetField("_can_jump", OwnedPatches.Members).SetValue(input, true);
                Check(Run() == BTresult.Running && model.Direction == 1, "A direction held at charge entry counts");
                raw = new InputComponent.State();
                Check(Run() == BTresult.Running && model.Direction == 1, "Releasing direction during charge retains the tap");
                prompt.Cancel();
                Check(model.Direction == 0 && (float)DebugJumpPrompt.Timer.GetValue(node) == 0, "Cancellation clears direction and native charge");

                model.WaitRelease = false;
                raw = new InputComponent.State { jump = true };
                typeof(InputComponent).GetField("_can_jump", OwnedPatches.Members).SetValue(input, true);
                Check(Run() == BTresult.Running && model.Direction == 0, "Charge after cancellation starts neutral");
                raw = new InputComponent.State { left = true };
                Check(Run() == BTresult.Running && model.Direction == -1, "Tap during a neutral charge selects its direction");
                raw = new InputComponent.State();
                while (Run() == BTresult.Running) Check(model.Direction == -1, "Direction remains only for the active charge");
                Check(direction == -1 && model.Direction == 0, "Tapped direction is applied and then cleared");
                prompt.Cancel(); Check(!model.Charging && !prompt.Executing && prompt.RunningNode == null, "Cancellation drops native execution");
                model.Armed = false; Check(!DebugJumpPrompt.IsControlling(node), "Disarming restores decorators");
            }
            typeof(DebugJumpPrompt).GetField("current", OwnedPatches.Members).SetValue(null, null);
            Check(!DebugJumpPrompt.IsControlling(node), "No control outside debug lifetime");
            Console.WriteLine("[OK] Debug prompt: pointer edges/focus, all substep intervals, real native dry/water releases, direction latching and exact evidence");
        }
        private static BTresult Run()
        { return (BTresult)Method(typeof(JumpState), "MyRun").Invoke(node, new object[] { new TickData(1f / 60, 0) }); }
    }
}
