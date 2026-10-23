using System;
using System.Reflection;
using JumpKing;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    // Render queries are local to the drawing thread. never write the native
    // screen index: physics, save workers and screen events keep native state
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
        private readonly JKRuntime.Presentation.NativeScreenView.DrawScope native;
        [ThreadStatic] private static RenderContext current;
        private readonly RenderContext previous;

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
            previous=current;current=this;native=JKRuntime.Presentation.NativeScreenView.Enter(screen,new Vector2(0,extraTranslation));
        }

        public void Dispose() { native.Dispose();current=previous;Active = oldActive; Screen = oldScreen; Correction = oldCorrection; Column = oldColumn; BaseScreen = oldBaseScreen; HorizontalCorrection = oldHorizontal; VerticalTranslation = oldVertical; }
        internal static void SetHorizontal(int value){HorizontalCorrection=value;if(current!=null)current.native.ExtraTranslation=new Vector2(value,VerticalTranslation-Screen*CameraMotion.Height);}
    }
}
