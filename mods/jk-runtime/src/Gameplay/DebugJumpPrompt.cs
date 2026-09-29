using System;
using System.Globalization;
using System.Reflection;
using BehaviorTree;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime.Gameplay
{
    /// <summary>Debug-only prompted native jumps. Charge decorators must yield to IsControlling before interpreting physical release timing.</summary>
    public static class DebugJumpPrompt
    {
        private static Prompt current;
        private static Func<bool> quarterSteps;
        internal static readonly FieldInfo Timer = typeof(JumpState).GetField("m_timer", OwnedPatches.Members);
        private static readonly MethodInfo Reset = typeof(JumpState).GetMethod("Reset", OwnedPatches.Members);

        /// <summary>Register the effective quarter-step setting, including map policy. The callback is read on the game thread.</summary>
        public static IDisposable RegisterQuarterSteps(Func<bool> enabled)
        {
            RuntimeApi.Kernel.CheckThread();
            if (enabled == null || quarterSteps != null) throw new InvalidOperationException("Debug charge subdivisions already registered or invalid");
            quarterSteps = enabled;
            return new ActionLease(delegate { RuntimeApi.Kernel.CheckThread(); quarterSteps = null; });
        }

        /// <summary>True only for the active player's native charge node while the debug prompt is armed.</summary>
        public static bool IsControlling(JumpState state)
        {
            RuntimeApi.Kernel.CheckThread();
            return current != null && current.Model.Armed && current.Available
                && ReferenceEquals(current.Node, state);
        }

        internal static IDisposable Install(PlayerEntity player)
        {
            if (LevelDebugState.instance == null) return new ActionLease(delegate { });
            return current = new Prompt(player);
        }

        internal static float ClampFrames(float frames, bool quarters, float multiplier)
        {
            float divisions = quarters ? 4 : 1;
            float maximum = Math.Max(1, (float)Math.Ceiling((PlayerValues.JUMP_TIME * 60.0 / multiplier - 1 - 0.00001) * divisions) / divisions);
            return Math.Max(1, Math.Min(maximum, (float)Math.Round(frames * divisions, MidpointRounding.AwayFromZero) / divisions));
        }

        // Keep pointer state separate so focus, capture and click edges can be tested without a window.
        internal sealed class Model
        {
            internal bool Armed, Dragging, Right, Charging, WaitRelease;
            internal float Frames = 12, Held, Target, AnchorFrames, AnchorX, AnchorY;
            internal int Direction;
            private bool disableOnClick, moved;
            internal void Pointer(bool right, bool inside, bool usable, float x, float y, bool quarters, float multiplier)
            {
                if (!usable) { Dragging = false; Right = right; return; }
                if (right && !Right && inside)
                {
                    disableOnClick = Armed;
                    Armed = Dragging = true;
                    moved = false;
                    AnchorX = x; AnchorY = y; AnchorFrames = Frames;
                }
                if (Dragging)
                {
                    // Remember the whole gesture, including horizontal motion and a
                    // drag that comes back to its starting point before release.
                    moved |= x != AnchorX || y != AnchorY;
                    Frames = ClampFrames(AnchorFrames + (AnchorY - y) / 4f, quarters, multiplier);
                    if (!right)
                    {
                        if (disableOnClick && !moved && inside) Armed = false;
                        Dragging = false;
                    }
                }
                Frames = ClampFrames(Frames, quarters, multiplier);
                Right = right;
            }
            internal void Cancel() { Charging = false; Held = 0; Direction = 0; WaitRelease = true; }
        }

        internal sealed class Prompt : Entity, IDisposable
        {
            internal readonly Model Model = new Model();
            internal readonly PlayerEntity Player;
            private readonly InputComponent input;
            private readonly OwnedPatches hooks;
            private readonly IDisposable pause;
            private readonly IDisposable updates;
            internal bool Executing, Release;
            internal float Before, LaunchTimer;
            internal JumpState RunningNode;
            internal JumpState Node { get { return RuntimeHost.Contract.GetJumpState(Player); } }
            internal bool Available { get { return Player.IsAlive && Game1.instance.IsActive && !NativePause.IsPaused
                && !JumpSlot.ChargePolicySuspended && !GameFeatures.IsMorphed && GameFeatures.Movement != MovementMode.VariableJump
                && PlayerControl.Available(Player.m_body, "jk-runtime.debug-jump"); } }

            internal Prompt(PlayerEntity player)
            {
                Player = player; input = player.GetComponent<InputComponent>();
                Model.Right = Mouse.GetState().RightButton == ButtonState.Pressed;
                hooks = new OwnedPatches("jk-runtime.debug-jump");
                try
                {
                    if (Timer == null || Reset == null) throw new NotSupportedException("Native debug charge contract unavailable");
                    hooks.Add(typeof(JumpState).GetMethod("MyRun", OwnedPatches.Members),
                        prefix: Hook("BeforeRun"), finalizer: Hook("AfterRun"));
                    hooks.Add(typeof(InputComponent).GetMethod("GetState"), postfix: Hook("InputState"), priority: 0);
                    pause = NativePause.Subscribe("jk-runtime.debug-jump", delegate(bool paused, long stamp) { if (paused) Cancel(); });
                    updates = PlayerUpdates.Register(player, "jk-runtime.debug-jump", PlayerUpdatePhase.BeforeInput, Update);
                }
                catch { if (pause != null) pause.Dispose(); hooks.Dispose(); Destroy(); throw; }
            }
            protected override void Update(float delta)
            {
                if (!Player.IsAlive) { Dispose(); return; }
                var mouse = Mouse.GetState();
                var game = Game1.instance;
                var client = game.Window.ClientBounds; var screen = game.GetScreenSize();
                var pixel = new Point(mouse.X * screen.X / Math.Max(1, client.Width), mouse.Y * screen.Y / Math.Max(1, client.Height));
                var rect = game.GetGameRect();
                bool usable = Available;
                bool armed = Model.Armed;
                Model.Pointer(mouse.RightButton == ButtonState.Pressed, rect.Contains(pixel), usable,
                    (pixel.X - rect.X) * 480f / Math.Max(1, rect.Width),
                    (pixel.Y - rect.Y) * 360f / Math.Max(1, rect.Height), quarterSteps != null && quarterSteps(), Multiplier());
                if (!armed && Model.Armed) { Reset.Invoke(Node, null); Model.Cancel(); }
                if (!usable || (armed && !Model.Armed) || mouse.LeftButton == ButtonState.Pressed || mouse.ScrollWheelValue != lastScroll)
                    Cancel();
                lastScroll = mouse.ScrollWheelValue;
                if (!input.GetState().jump) Model.WaitRelease = false;
                if (!usable) { Model.Armed = false; return; }
            }
            private int lastScroll = Mouse.GetState().ScrollWheelValue;
            internal float Multiplier() { return Math.Max(0.0001f, Player.m_body.GetMultipliers()); }
            internal void Cancel()
            {
                if (RunningNode != null) Reset.Invoke(RunningNode, null);
                RunningNode = null; Model.Cancel(); Executing = false;
            }
            public override void Draw()
            {
                if (!Model.Armed || !Available) return;
                var font = Game1.instance.contentManager.font.MenuFont;
                string arrow = Model.Direction < 0 ? (font.Characters.Contains('\u2190') ? " \u2190" : " <-")
                    : Model.Direction > 0 ? (font.Characters.Contains('\u2192') ? " \u2192" : " ->") : "";
                string text = "P: " + Model.Frames.ToString("0.##", CultureInfo.InvariantCulture) + arrow;
                Vector2 size = font.MeasureString(text);
                var bounds = Player.m_body.GetHitbox();
                Vector2 position = Camera.TransformVector2(new Vector2(bounds.Center.X, bounds.Top - size.Y - 5));
                position.X = (float)Math.Round(position.X - size.X / 2);
                position.Y = (float)Math.Round(position.Y);
                TextHelper.DrawString(font, text, position, Color.White, Vector2.Zero, true);
            }
            public void Dispose()
            {
                Cancel();
                if (pause != null) pause.Dispose();
                if (updates != null) updates.Dispose();
                hooks.Dispose();
                if (current == this) current = null;
                if (IsAlive) Destroy();
            }
        }

        private static MethodInfo Hook(string name) { return typeof(DebugJumpPrompt).GetMethod(name, OwnedPatches.Members); }
        private static bool BeforeRun(JumpState __instance, TickData p_data, ref BTresult __result, out Prompt __state)
        {
            __state = null;
            if (!IsControlling(__instance)) return true;
            var prompt = current; var model = prompt.Model;
            var raw = prompt.Player.GetComponent<InputComponent>().GetState();
            if (model.Charging && (!ReferenceEquals(prompt.RunningNode, __instance) || (float)Timer.GetValue(__instance) == 0)) prompt.Cancel();
            if (!model.Charging && (!raw.jump || model.WaitRelease)) { __result = BTresult.Failure; return false; }
            // Direction belongs to this charge. Movement before it and the previous
            // jump must not turn a neutral prompt into a sideways jump.
            if (!model.Charging) { model.Target = model.Frames; model.Held = 0; model.Direction = 0; }
            if (raw.dpad.X != 0) model.Direction = raw.dpad.X;
            prompt.Release = model.Charging && model.Held + 0.0001f >= model.Target;
            prompt.LaunchTimer = Math.Min(PlayerValues.JUMP_TIME, (model.Target + 1) * prompt.Multiplier() / 60f);
            if (prompt.Release) Timer.SetValue(__instance, Math.Max(0, prompt.LaunchTimer - p_data.delta_time * prompt.Multiplier()));
            prompt.Before = prompt.Player.m_body.Velocity.Y;
            prompt.RunningNode = __instance;
            prompt.Executing = true; __state = prompt;
            model.Held += p_data.delta_time * 60;
            return true;
        }
        private static void InputState(InputComponent __instance, ref InputComponent.State __result)
        {
            var prompt = current;
            if (prompt == null || !prompt.Executing || !ReferenceEquals(__instance.gameObject, prompt.Player)) return;
            __result.jump = !prompt.Release;
            __result.left = prompt.Model.Direction < 0; __result.right = prompt.Model.Direction > 0;
        }
        private static Exception AfterRun(Exception __exception, BTresult __result, Prompt __state)
        {
            if (__state == null) return __exception;
            var prompt = __state; var model = prompt.Model;
            prompt.Executing = false;
            if (__exception != null) { prompt.Cancel(); return __exception; }
            model.Charging = __result == BTresult.Running;
            if (__result == BTresult.Success)
            {
                model.WaitRelease = true; model.Direction = 0; prompt.RunningNode = null;
                int? whole = model.Target == Math.Floor(model.Target) ? (int?)model.Target : null;
                JumpEvents.Publish(new JumpResult("jk-runtime.debug-jump", JumpEvidence.Unavailable, null,
                    whole, null, prompt.LaunchTimer, false, false, prompt.Before, prompt.Player.m_body.Velocity.Y, model.Target, null));
            }
            else if (!model.Charging) prompt.Cancel();
            return null;
        }
    }
}
