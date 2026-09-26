using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace SubframeCharge
{
    internal sealed class PositionHistory
    {
        internal Vector2 Current, Velocity;
        private bool initialized;
        private bool wasGrounded;
        internal bool CanPredict { get; private set; }
        private int screen;
        internal void Reset() { initialized = false; CanPredict=false; }
        internal void Observe(Vector2 position, Vector2 velocity, int nextScreen, bool snap, bool grounded=false, Vector2? nativeBefore=null)
        {
            bool discontinuity = !initialized || snap || screen != nextScreen || Vector2.DistanceSquared(Current, position) > 64*64
                || float.IsNaN(velocity.X) || float.IsNaN(velocity.Y) || float.IsInfinity(velocity.X) || float.IsInfinity(velocity.Y);
            // Water/material behaviours modify displacement without necessarily
            // writing BodyComp.Velocity. The completed tick already includes all
            // those effects, including foreign handlers; do not simulate them.
            Vector2 movedBy=position-Current;
            Velocity = discontinuity ? Vector2.Zero : Vector2.Clamp(movedBy, new Vector2(-64), new Vector2(64));
            if(!discontinuity)
            {
                // The player's behaviour tree runs after BodyComp. A fresh walk
                // or takeoff must be visible before the next body integration.
                var before=nativeBefore ?? velocity;
                if(Math.Abs(movedBy.X)<.001f && before.X==0) Velocity.X=velocity.X;
                if(Math.Abs(movedBy.Y)<.001f && velocity.Y<0 && before.Y>=0) Velocity.Y=velocity.Y;
                // A native stop or bounce supersedes the pre-impact displacement.
                if(velocity.X==0) Velocity.X=0;
                else if(velocity.X*Velocity.X<0) Velocity.X=velocity.X;
                if(velocity.Y==0) Velocity.Y=0;
                else if(velocity.Y*Velocity.Y<0) Velocity.Y=velocity.Y;
                // The next draw starts from the completed tick. Keep observed
                // material scaling, but use its outgoing speed, not the speed
                // before gravity, drag or a same-direction collision response.
                if(nativeBefore.HasValue)
                {
                    if(before.X*velocity.X>0 && movedBy.X*before.X>0)
                        Velocity.X=Math.Abs(before.X)<.01f ? velocity.X : movedBy.X*(velocity.X/before.X);
                    if(before.Y*velocity.Y>0 && movedBy.Y*before.Y>0)
                        Velocity.Y=Math.Abs(before.Y)<.01f ? velocity.Y : movedBy.Y*(velocity.Y/before.Y);
                }
                // The native sand behaviour adds one positional pixel on the
                // first downward tick off support, even outside sand. That
                // one-time offset is not a persistent displacement multiplier.
                if(wasGrounded && !grounded && before.Y>0 && velocity.Y>0)
                    Velocity.Y=velocity.Y;
            }
            if(grounded)
            {
                if(Velocity.Y>0) Velocity.Y=0; // native gravity remains positive on support
                float moved=position.X-Current.X;
                if(Math.Abs(moved)<.001f && (!nativeBefore.HasValue || nativeBefore.Value.X!=0)
                    || moved*Velocity.X<0) Velocity.X=0;
            }
            Velocity=Vector2.Clamp(Velocity,new Vector2(-64),new Vector2(64));
            Current = position; screen = nextScreen; initialized = true;
            wasGrounded=grounded; CanPredict=!discontinuity;
        }
        // Observed motion is in pixels per physics tick. Predict at most one
        // tick; no speculative body update, collision callback or save mutation.
        internal Vector2 At(float alpha) { return Current + Velocity * Math.Max(0, Math.Min(1, alpha)); }
    }
    internal static class PlayerPresentation
    {
        private static readonly PositionHistory history = new PositionHistory();
        private static readonly PredictionPath path = new PredictionPath();
        private static BodyComp body;
        internal static void Reset() { body = null; history.Reset(); path.Begin(Vector2.Zero,Vector2.Zero,0,0,false); }
        internal static void AfterUpdate()
        {
            var player = GameLoop.m_player;
            if (!FeatureClock.Active || !FeatureClock.HighRefresh || player == null || !player.IsAlive || !JumpGame.instance.IsPlaying()) { Reset(); return; }
            if (!ReferenceEquals(body, player.m_body)) { history.Reset(); body = player.m_body; }
            bool customMotion=JKRuntime.Gameplay.JumpSlot.ChargePolicySuspended || JKRuntime.Gameplay.GameFeatures.IsMorphed
                || JKRuntime.Gameplay.GameFeatures.IsAttached || JKRuntime.Gameplay.GameFeatures.ThrustActive
                || JKRuntime.Gameplay.GameFeatures.Movement==JKRuntime.Gameplay.MovementMode.VariableJump;
            history.Observe(body.Position, body.Velocity, Camera.CurrentScreenIndex1,
                customMotion || JKRuntime.Gameplay.NativePause.IsPaused || !Game1.instance.IsActive,body.IsOnGround,body.LastVelocity);
            using(JKRuntime.RuntimeApi.MeasurePerformance("subframe-presentation.collision-path"))
                path.Prepare(history.Current,history.Velocity,body.GetHitbox(),Camera.CurrentScreen,
                    history.CanPredict ? (Vector2?)body.Velocity : null,body.IsOnGround);
        }
        internal static Vector2 Position(BodyComp value)
        {
            return FeatureClock.Active && FeatureClock.HighRefresh && ReferenceEquals(body, value)
                && value.Position==history.Current && !JKRuntime.Gameplay.PresentationActivity.IsActive(value)
                ? path.At(FeatureClock.Alpha) : value.Position;
        }
        internal static IEnumerable<CodeInstruction> RewritePlayer(IEnumerable<CodeInstruction> source)
        {
            int count = 0;
            var position = AccessTools.Field(typeof(BodyComp), "Position");
            var getter = AccessTools.PropertyGetter(typeof(BodyComp), "Position");
            foreach (var instruction in source)
            {
                if (position != null && instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, position)
                    || getter != null && instruction.Calls(getter))
                { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(PlayerPresentation), "Position"); count++; }
                yield return instruction;
            }
            if (count == 0) throw new NotSupportedException("Unsupported native player drawing position");
        }
    }
}
