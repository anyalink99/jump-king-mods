using System;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    // Render queries are local to the drawing thread. Never write the native
    // screen index: physics, save workers and screen events retain native state.
    internal sealed class RenderContext : IDisposable
    {
        internal static readonly FieldInfo NativeScreen = typeof(Camera).GetField("_current_screen", BindingFlags.Static | BindingFlags.NonPublic);
        [ThreadStatic] internal static bool Active;
        [ThreadStatic] internal static int Screen;
        [ThreadStatic] internal static int Correction;
        [ThreadStatic] internal static int Column, BaseScreen, HorizontalCorrection, VerticalTranslation;
        private readonly bool oldActive;
        private readonly int oldScreen, oldCorrection;
        private readonly int oldColumn, oldBaseScreen, oldHorizontal, oldVertical;

        internal RenderContext(int screen, int extraTranslation = 0, int column = 0, int baseScreen = -1)
        {
            oldActive = Active; oldScreen = Screen; oldCorrection = Correction;
            oldColumn = Column; oldBaseScreen = BaseScreen; oldHorizontal = HorizontalCorrection;
            oldVertical = VerticalTranslation; VerticalTranslation = screen * CameraMotion.Height + extraTranslation;
            Column = column; BaseScreen = baseScreen < 0 ? screen : baseScreen; HorizontalCorrection = 0;
            int logical = (int)NativeScreen.GetValue(null);
            Screen = screen;
            Correction = (screen - logical) * CameraMotion.Height + extraTranslation;
            Active = true;
        }

        public void Dispose() { Active = oldActive; Screen = oldScreen; Correction = oldCorrection; Column = oldColumn; BaseScreen = oldBaseScreen; HorizontalCorrection = oldHorizontal; VerticalTranslation = oldVertical; }
        internal static bool CurrentScreen(ref int __result)
        { if (!Active) return true; __result = Screen; return false; }
        internal static bool CurrentScreenIndex1(ref int __result)
        { if (!Active) return true; __result = Screen + 1; return false; }
        internal static void Vector(ref Vector2 __result) { if (Active) { __result.Y += Correction; __result.X += HorizontalCorrection; } }
        internal static void RectangleValue(ref Rectangle __result) { if (Active) { __result.Y += Correction; __result.X += HorizontalCorrection; } }
        internal static void RectangleRef(ref Rectangle draw_dst) { if (Active) { draw_dst.Y += Correction; draw_dst.X += HorizontalCorrection; } }
    }
}
