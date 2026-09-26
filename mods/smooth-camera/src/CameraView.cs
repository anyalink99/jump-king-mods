using System;

namespace SmoothCamera
{
    // Keep automatic tracking alive underneath temporary views so a release can
    // return to the framing the regular camera would have retained.
    internal sealed class CameraView
    {
        internal float X { get; private set; }
        internal float Y { get; private set; }
        private float focusWeight, focusVelocity, vy, previousPlayerY;
        private bool initialized, returning;
        private CameraMode previousMode;
        private int previousScreen;
        internal void Reset() { initialized = returning = false; focusWeight = focusVelocity = vy = 0; }
        internal void Rebase(float x, float y)
        { if (initialized) { X += x; Y += y; previousPlayerY -= y; } }

        internal void Advance(CameraMode mode, float normalX, float normalY, float playerY, float speed,
            int screen, int count, float delta, bool paused)
        {
            if (count <= 0 || float.IsNaN(playerY) || float.IsInfinity(playerY)) return;
            float maximum = (count - 1) * 360f;
            float targetY = mode == CameraMode.Focus ? screen * 360f : mode == CameraMode.Up ? 312 - playerY
                : mode == CameraMode.Down ? 48 - playerY : normalY;
            targetY = CameraMotion.Clamp(targetY, 0, maximum);
            float targetX = mode == CameraMode.Focus ? 0 : normalX;
            if (!initialized)
            { X = normalX; Y = normalY; previousPlayerY = playerY; previousScreen = screen; previousMode = CameraMode.Normal; initialized = true; }
            if (paused) return;
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
                // topology: never expose a side after its portal view disappears.
                X = normalX * (1 - focusWeight);
                Y = CameraMotion.Clamp(Spring(Y, targetY, ref vy, dt, omega), 0, maximum);
                if (mode == CameraMode.Up || mode == CameraMode.Down)
                {
                    float visible = CameraMotion.Clamp(CameraMotion.Clamp(Y, 16 - playerY, 344 - playerY), 0, maximum);
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
