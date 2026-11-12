using System;

namespace SmoothCamera
{
    internal enum CameraMode { Normal, Up, Down, Focus }

    // one gesture owns the camera until release. Latches are session state only
    internal sealed class CameraGestures
    {
        internal const double HoldSeconds = .25;
        internal double HoldThreshold = HoldSeconds;
        internal CameraMode Current { get; private set; }
        internal CameraMode Latched { get; private set; }
        private CameraTrigger upTrigger, downTrigger, focusTrigger, activeTrigger;
        private CameraMode pressed, before, tapped;
        private double started;
        private bool armed;
        private bool allowFocus = true, allowLook = true;

        internal void SetTriggers(CameraTrigger up, CameraTrigger down, CameraTrigger focus)
        {
            if (upTrigger == up && downTrigger == down && focusTrigger == focus) return;
            upTrigger = up; downTrigger = down; focusTrigger = focus;
            Reset();
        }

        internal void SetPermissions(bool focus, bool look)
        {
            if (allowFocus == focus && allowLook == look) return;
            allowFocus = focus; allowLook = look;
            if ((Latched == CameraMode.Focus && !focus) || (IsDirection(Latched) && !look)) Latched = CameraMode.Normal;
            // discard unfinished presses too; leaving a restricted area needs a fresh press
            Suspend();
        }

        internal void Reset() { Latched = CameraMode.Normal; Suspend(); }
        internal void Suspend() { pressed = CameraMode.Normal; Current = Latched; armed = false; }
        internal void Update(bool up, bool down, bool focus, double now, bool available)
        {
            if (!available || double.IsNaN(now) || double.IsInfinity(now)) { Suspend(); return; }
            int count = (up ? 1 : 0) + (down ? 1 : 0) + (focus ? 1 : 0);
            if (!armed) { if (count == 0) armed = true; return; }
            up &= allowLook; down &= allowLook; focus &= allowFocus;
            count = (up ? 1 : 0) + (down ? 1 : 0) + (focus ? 1 : 0);
            CameraMode key = focus ? CameraMode.Focus : up ? CameraMode.Up : down ? CameraMode.Down : CameraMode.Normal;
            if (count > 1 || (pressed != CameraMode.Normal && key != CameraMode.Normal && key != pressed))
            { Suspend(); return; }
            if (pressed == CameraMode.Normal)
            {
                if (key == CameraMode.Normal) return;
                pressed = key; before = Latched; started = now;
                activeTrigger = key == CameraMode.Up ? upTrigger : key == CameraMode.Down ? downTrigger : focusTrigger;
                bool cancel = key == Latched || (IsDirection(key) && IsDirection(Latched));
                tapped = cancel ? CameraMode.Normal : key;
                // cancel a latch on tap release
                // otherwise holding an already latched view briefly flashes the normal camera
                if (activeTrigger == CameraTrigger.Press) Current = Latched = tapped;
                else Current = activeTrigger == CameraTrigger.Hold ? key : cancel ? before : key;
            }
            else if (key == CameraMode.Normal)
            {
                // Classify on release as well: a delayed update must not turn a hold into a tap
                if (activeTrigger != CameraTrigger.Press)
                    Latched = activeTrigger == CameraTrigger.Both && now >= started && now - started < HoldThreshold ? tapped : before;
                Current = Latched; pressed = CameraMode.Normal;
            }
            else if (activeTrigger == CameraTrigger.Both && now - started >= HoldThreshold) Current = pressed;
        }
        private static bool IsDirection(CameraMode mode) { return mode == CameraMode.Up || mode == CameraMode.Down; }
    }
}
