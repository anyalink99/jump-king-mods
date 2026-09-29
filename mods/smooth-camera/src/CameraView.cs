using System;

namespace SmoothCamera
{
    // keep automatic tracking alive underneath temporary views so a release can
    // return to the framing the regular camera would have retained
    internal sealed class CameraView
    {
        internal float X { get; private set; }
        internal float Y { get; private set; }
        private float focusWeight, focusVelocity, vy, previousPlayerY;
        private bool initialized, returning, transitionPending;
        private CameraMode previousMode;
        private int previousScreen;
        internal int FirstScreen, LastScreen = int.MaxValue;
        internal float LookMargin = 48;
        internal void TransitionFrom(float x, float y) { X = x; Y = y; initialized = returning = transitionPending = true; vy = 0; }
        internal void Constrain(float min, float max) { Y = CameraMotion.Clamp(Y, min, max); }
        internal void Reset() { initialized = returning = transitionPending = false; focusWeight = focusVelocity = vy = 0; }
        internal void Rebase(float x, float y)
        { if (initialized) { X += x; Y += y; previousPlayerY -= y; } }

        internal void Advance(CameraMode mode, float normalX, float normalY, float playerY, float speed,
            int screen, int count, float delta, bool paused)
        {
            if (count <= 0 || float.IsNaN(playerY) || float.IsInfinity(playerY)) return;
            float minimum = Math.Min(FirstScreen, count - 1) * 360f, maximum = Math.Min(LastScreen, count - 1) * 360f;
            float targetY = mode == CameraMode.Focus ? screen * 360f : mode == CameraMode.Up ? 360 - LookMargin - playerY
                : mode == CameraMode.Down ? LookMargin - playerY : normalY;
            targetY = CameraMotion.Clamp(targetY, minimum, maximum);
            float targetX = mode == CameraMode.Focus ? 0 : normalX;
            if (!initialized)
            { X = normalX; Y = normalY; previousPlayerY = playerY; previousScreen = screen; previousMode = CameraMode.Normal; initialized = true; }
            if (paused) return;
            if (transitionPending) { previousPlayerY = playerY; previousScreen = screen; previousMode = mode; transitionPending = false; }
            bool teleport = Math.Abs(playerY - previousPlayerY) > 270;
            bool focusScreenChange = mode == CameraMode.Focus && previousMode == mode && screen != previousScreen;
            if (mode != previousMode) returning = true;
            if (teleport || focusScreenChange)
            { X = targetX; Y = targetY; focusWeight = mode == CameraMode.Focus ? 1 : 0; focusVelocity = vy = 0; }
            else if (mode == CameraMode.Normal && !returning) { X = normalX; Y = normalY; focusWeight = focusVelocity = vy = 0; }
            else if (delta > 0 && !float.IsNaN(delta) && !float.IsInfinity(delta))
            {
                if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0;
                float omega = mode == CameraMode.Focus ? 12 : Math.Max(12, Math.Min(90, Math.Abs(speed) / 12));
                float dt = Math.Min(.1f, delta);
                focusWeight = Spring(focusWeight, mode == CameraMode.Focus ? 1 : 0, ref focusVelocity, dt, 12);
                // Scale the live portal offset instead of lagging behind its
                // topology: never expose a side after its portal view disappears
                X = normalX * (1 - focusWeight);
                Y = CameraMotion.Clamp(Spring(Y, targetY, ref vy, dt, omega), minimum, maximum);
                if (mode == CameraMode.Up || mode == CameraMode.Down)
                {
                    float visible = CameraMotion.Clamp(CameraMotion.Clamp(Y, 16 - playerY, 344 - playerY), minimum, maximum);
                    if (Y != visible) vy = 0;
                    Y = visible;
                }
                if (mode == CameraMode.Normal && Math.Abs(X - normalX) < .01f && Math.Abs(Y - normalY) < .01f
                    && Math.Abs(focusVelocity) < .001f && Math.Abs(vy) < .1f)
                { returning = false; X = normalX; Y = normalY; focusWeight = focusVelocity = vy = 0; }
            }
            previousMode = mode; previousScreen = screen; previousPlayerY = playerY;
        }
        private static float Spring(float value, float target, ref float velocity, float dt, float omega)
        {
            float d = value - target, impulse = (velocity + omega * d) * dt, decay = (float)Math.Exp(-omega * dt);
            float next = target + (d + impulse) * decay;
            velocity = (velocity - omega * impulse) * decay;
            if ((d < 0 && next > target) || (d > 0 && next < target)
                || (Math.Abs(next - target) < .0001f && Math.Abs(velocity) < .001f))
            { velocity = 0; return target; }
            return next;
        }
    }
}
