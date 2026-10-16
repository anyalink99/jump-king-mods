using System;

namespace SmoothCamera
{
    // shared tracking law, topology and native screen selection belong to the callers
    internal sealed class AxisMotion
    {
        internal AxisSettings Options = new AxisSettings();
        internal float Value, Target;
        private float velocity, player, speed, length, minimum, maximum, native, influence = 1;
        private float stillTime, directionTime, look, lastPlayer;
        private int direction, pendingDirection;
        private bool initialized, frozen, following = true, constrain = true;
        internal void Reset() { initialized = false; Value = Target = velocity = stillTime = directionTime = look = 0; direction = pendingDirection = 0; }
        internal void Place(float value) { Value = Target = value; velocity = stillTime = directionTime = look = 0; direction = pendingDirection = 0; }
        internal void Rebase(float shift) { if (initialized) { Value += shift; Target += shift; player -= shift; lastPlayer -= shift; } }
        internal void KeepVisible(float margin, float min, float max)
        {
            if (!initialized || frozen || Options.Mode == FollowMode.Screen) return;
            float next = Clamp(Clamp(Value, margin - player, length - margin - player), min, max);
            if (next != Value) { Value = next; velocity = 0; }
        }
        internal void Observe(float position, float playerSpeed, float size, float min, float max, float nativePosition,
            bool paused, bool snap, bool follow = true, float weight = 1, bool constrainValue = true)
        {
            if (float.IsNaN(position) || float.IsInfinity(position)) return;
            player = position; speed = float.IsNaN(playerSpeed) || float.IsInfinity(playerSpeed) ? 0 : playerSpeed;
            length = size; minimum = min; maximum = max; native = nativePosition; frozen = paused; following = follow; influence = weight; constrain = constrainValue;
            if (!initialized || snap)
            {
                Value = Target = Clamp(following && constrain ? Options.Focus * length - player : native, min, max);
                velocity = stillTime = directionTime = look = 0; direction = pendingDirection = 0; initialized = true;
            }
            if (Math.Abs(position - lastPlayer) > .05f) stillTime = 0;
            lastPlayer = player;
            UpdateTarget();
            if (constrain) Value = Clamp(Value, min, max);
        }
        private void UpdateTarget()
        {
            if (Options.Mode == FollowMode.Screen) { Value = Target = Clamp(native, minimum, maximum); velocity = 0; return; }
            if (frozen) return;
            float desired = (Options.Focus * length - player - look) * influence;
            if (!following) Target = native;
            else if (Options.Mode == FollowMode.Direct || (Options.Recenter && stillTime >= Options.RecenterDelay)) Target = desired;
            else
            {
                float negative = Options.Mode == FollowMode.Window ? Options.Window * .5f : Options.NegativeBand;
                float positive = Options.Mode == FollowMode.Window ? Options.Window * .5f : Options.PositiveBand;
                // Window follows the actual viewport. JumpKing keeps the target
                // band through apex/landing, independently of spring lag
                float origin = Options.Mode == FollowMode.Window ? Value : Target;
                Target = Clamp(origin, desired - negative, desired + positive);
                if (Options.Mode == FollowMode.Window && Target == Value) velocity = 0;
            }
            Target = Clamp(Target, minimum, maximum);
        }
        internal void Advance(float delta, float visibilityMargin = -1)
        {
            if (!initialized || frozen || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float dt = Math.Min(delta, .1f);
            if (Math.Abs(speed) < 1) stillTime += dt; else stillTime = 0;
            int nextDirection = Math.Abs(speed) < 1 ? 0 : Math.Sign(speed);
            if (pendingDirection != nextDirection) { pendingDirection = nextDirection; directionTime = 0; }
            directionTime += dt;
            if (directionTime >= Options.LookDelay) direction = pendingDirection;
            float lookTarget = direction * Options.LookAhead;
            look += (lookTarget - look) * (1 - (float)Math.Exp(-12 * dt));
            UpdateTarget();
            if (Options.Mode == FollowMode.Screen) return;
            float response = speed < 0 || (Math.Abs(speed) < 1 && Target > Value) ? Options.NegativeResponse : Options.PositiveResponse;
            if (Options.FastAssist) response = Math.Max(response, Math.Min(60, Math.Abs(speed) / 24));
            float displacement = Value - Target;
            float impulse = (velocity + response * displacement) * dt;
            float decay = (float)Math.Exp(-response * dt);
            float next = Target + (displacement + impulse) * decay;
            velocity = (velocity - response * impulse) * decay;
            if ((displacement < 0 && next > Target) || (displacement > 0 && next < Target)) { next = Target; velocity = 0; }
            float visible = visibilityMargin < 0 ? next : Clamp(next, visibilityMargin - player, length - visibilityMargin - player);
            Value = constrain ? Clamp(visible, minimum, maximum) : visible;
            if (Value != next) velocity = 0;
            if (Math.Abs(Value - Target) < .0001f && Math.Abs(velocity) < .001f) { Value = Target; velocity = 0; }
        }
        private static float Clamp(float value, float min, float max) { return CameraMotion.Clamp(value, min, max); }
    }
}
