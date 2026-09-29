using System;

namespace SmoothCamera
{
    internal sealed class CameraMotion
    {
        internal const int Width = 480, Height = 360;
        private readonly AxisMotion axis = new AxisMotion();
        private bool initialized;
        private float previousPlayerY;
        internal AxisSettings Options { get { return axis.Options; } set { axis.Options = value; } }
        internal int FirstScreen, LastScreen = int.MaxValue;
        internal float Translation { get { return axis.Value; } }
        internal int PixelTranslation { get { return (int)Math.Round(Translation); } }
        internal void Reset() { initialized = false; axis.Reset(); }
        internal void Rebase(float translation) { axis.Rebase(translation); if (initialized) previousPlayerY -= translation; }
        internal void Step(float centerY, int screenCount, float delta, bool paused)
        { Observe(centerY, screenCount, paused); Advance(delta); }
        internal void Observe(float centerY, int screenCount, bool paused, float verticalSpeed = 0, int nativeScreen = -1)
        {
            if (screenCount <= 0 || float.IsNaN(centerY) || float.IsInfinity(centerY)) return;
            bool snap = !initialized || Math.Abs(centerY - previousPlayerY) > Height * .75f;
            if (nativeScreen < 0) nativeScreen = Math.Max(0, Math.Min(screenCount - 1, (int)Math.Ceiling(-centerY / Height)));
            float min = Math.Min(FirstScreen, screenCount - 1) * Height, max = Math.Min(LastScreen, screenCount - 1) * Height;
            axis.Observe(centerY, verticalSpeed, Height, min, max, nativeScreen * Height, paused, snap);
            previousPlayerY = centerY; initialized = true;
        }
        internal void Advance(float delta) { axis.Advance(delta, 48); }
        internal static float Clamp(float value, float minimum, float maximum)
        { return Math.Max(minimum, Math.Min(maximum, value)); }
        internal static int[] VisibleScreens(int translation, int screenCount)
        {
            if (screenCount <= 0) return new int[0];
            translation = Math.Max(0, Math.Min((screenCount - 1) * Height, translation));
            int lower = translation / Height;
            return translation % Height == 0 || lower + 1 >= screenCount ? new[] { lower } : new[] { lower, lower + 1 };
        }
    }
}
