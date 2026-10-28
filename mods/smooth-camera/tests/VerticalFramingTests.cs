using System;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void VerticalFramingTests()
    {
        foreach (int rate in new[] { 60, 144, 240 })
        foreach (FollowMode mode in new[] { FollowMode.JumpKing, FollowMode.Direct, FollowMode.Window })
        foreach (bool legacy in new[] { false, true })
        foreach (int direction in new[] { -1, 1 })
        {
            bool continuous = true;
            var options = AxisSettings.HorizontalDefault(); options.Mode = mode;
            options.ContinuousFraming = legacy; options.Focus = .65f; options.LookAhead = 16;
            var motion = new CameraMotion { Options = options };
            var reference = new AxisMotion { Options = options };
            float y = -3420, limit = continuous ? 100000 : 180;
            int screen = 10, tick = 0;
            motion.Observe(y, 30, false, 0, screen);
            reference.Observe(continuous ? y : y + screen * 360, 0, 360, -limit, limit, 0, false, false);
            for (int frame = 0; frame < rate * 6; frame++)
            {
                int nextTick = frame * 60 / rate;
                while (tick < nextTick)
                {
                    int heading = tick < 180 ? direction : -direction;
                    y += heading * 4; tick++;
                    int nextScreen = (int)Math.Ceiling(-y / 360);
                    if (!continuous) reference.Rebase((screen - nextScreen) * 360);
                    screen = nextScreen;
                    float before = motion.Translation;
                    motion.Observe(y, 30, false, heading * 240, screen);
                    Near(motion.Translation, before, "Vertical screen change never cuts the current camera position");
                    // the off mode uses the old horizontal law in screen-local coordinates
                    reference.Observe(continuous ? y : y + screen * 360, heading * 240, 360,
                        -limit, limit, 0, false, false, true, 1, false);
                }
                motion.Advance(1f / rate); reference.Advance(1f / rate, 48);
                float expected = reference.Value + (continuous ? 0 : screen * 360);
                Check(Math.Abs(motion.Translation - expected) < .02f,
                    "Vertical " + (continuous ? "continuous" : "per-screen") + " framing matches its tracking law: " + mode);
            }
        }
    }
}
