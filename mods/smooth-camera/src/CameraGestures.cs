using System;

namespace SmoothCamera
{
    internal enum CameraMode { Normal, Up, Down, Focus }

    // One gesture owns the camera until release. Latches are session state only.
    internal sealed class CameraGestures
    {
        internal const double HoldSeconds = .25;
        internal CameraMode Current { get; private set; }
        internal CameraMode Latched { get; private set; }
        private CameraMode pressed, before, tapped;
        private double started;
        private bool armed;

        internal void Reset() { Latched = CameraMode.Normal; Suspend(); }
        internal void Suspend() { pressed = CameraMode.Normal; Current = Latched; armed = false; }
        internal void Update(bool up, bool down, bool focus, double now, bool available)
        {
            if (!available || double.IsNaN(now) || double.IsInfinity(now)) { Suspend(); return; }
            int count = (up ? 1 : 0) + (down ? 1 : 0) + (focus ? 1 : 0);
            if (!armed) { if (count == 0) armed = true; return; }
            CameraMode key = focus ? CameraMode.Focus : up ? CameraMode.Up : down ? CameraMode.Down : CameraMode.Normal;
            if (count > 1 || (pressed != CameraMode.Normal && key != CameraMode.Normal && key != pressed))
            { Suspend(); return; }
            if (pressed == CameraMode.Normal)
            {
                if (key == CameraMode.Normal) return;
                pressed = key; before = Latched; started = now;
                bool cancel = key == Latched || (IsDirection(key) && IsDirection(Latched));
                tapped = cancel ? CameraMode.Normal : key;
                // A cancelling tap commits on release. Otherwise a hold on an
                // already latched view would briefly flash the normal camera.
                Current = cancel ? before : key;
            }
            else if (key == CameraMode.Normal)
            {
                // Classify on release as well: a delayed update must not turn a hold into a tap.
                Latched = now >= started && now - started < HoldSeconds ? tapped : before;
                Current = Latched; pressed = CameraMode.Normal;
            }
            else if (now - started >= HoldSeconds) Current = pressed;
        }
        private static bool IsDirection(CameraMode mode) { return mode == CameraMode.Up || mode == CameraMode.Down; }
    }
}
