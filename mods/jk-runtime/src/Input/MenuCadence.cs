using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager;
using JumpKing.GameManager.TitleScreen;

namespace JKRuntime.Input
{
    internal static class MenuCadence
    {
        internal static readonly Type Pause = typeof(Game1).Assembly.GetType("JumpKing.PauseMenu.PauseManager", true);
        private static readonly FieldInfo PauseInstance = InputContract.Field(Pause, "instance");
        private static readonly PropertyInfo IsPaused = InputContract.Property(Pause, "IsPaused");
        private static readonly MethodInfo PauseUpdate = InputContract.Method(Pause, "PauseUpdate");
        private static readonly MethodInfo OnPause = InputContract.Method(typeof(GameLoop), "OnPause");
        private static readonly MethodInfo OnResume = InputContract.Method(typeof(GameLoop), "OnResume");
        private static readonly Action PointerUpdate = (Action)Delegate.CreateDelegate(typeof(Action), InputContract.Method(typeof(JKRuntime.UI.UiPointer), "Update"));
        private static GameTitleScreen title;
        private static JKRuntime.UI.MenuTreeSession titleSession;
        private static readonly FieldInfo TitleMenu = InputContract.Field(typeof(GameTitleScreen), "m_menu");
        // Intro entities read the native aggregate pad frame on the simulation
        // tick. only the actual menu may consume a presentation input frame
        private static bool TitleMenuRunning
        { get { var node=title==null ? null : TitleMenu.GetValue(title) as IBTnode; return node!=null && node.IsRunning(); } }
        internal static bool Pumping;
        internal static bool Fast { get { return ResponsiveControls.Active && ResponsiveControls.Requested; } }
        internal static void Reset() { title = null; titleSession = null; Pumping = false; }
        internal static void TitleStart(GameTitleScreen __instance) { title = __instance; titleSession = null; }
        internal static bool NativePauseUpdate() { return !Fast || Pumping; }
        internal static bool NativePointerUpdate() { return !Fast || Pumping; }
        internal static BTresult RunTitle(BTmanager tree, float delta)
        {
            if (title == null && JumpGame.instance != null)
                title = (GameTitleScreen)InputContract.Field(typeof(JumpGame), "m_title_screen").GetValue(JumpGame.instance);
            if(titleSession==null || !ReferenceEquals(titleSession.Tree,tree))
                titleSession=new JKRuntime.UI.MenuTreeSession(tree);
            return titleSession.RunNative(delta,Fast && TitleMenuRunning);
        }
        internal static void Pump(float delta)
        {
            if (!Fast || ControllerManager.instance == null || JumpGame.instance == null) return;
            bool playing = JumpGame.instance.IsPlaying() && GameLoop.m_player != null && GameLoop.m_player.IsAlive;
            var session=titleSession;
            bool titleActive = title != null && title.IsRunning() && TitleMenuRunning && session!=null && session.CanPump;
            object pause = playing ? PauseInstance.GetValue(null) : null;
            if (pause == null && !titleActive) return;
            var menu = ControllerManager.instance.MenuController;
            bool wasPaused = pause != null && (bool)IsPaused.GetValue(pause, null);
            Pumping = true;
            try
            {
              using (JKRuntime.Input.NativeInputFrames.BeginMenu(menu, ResponsiveInput.MenuEdges()))
              {
                PointerUpdate();
                if (pause != null) PauseUpdate.Invoke(pause, new object[] { delta });
                else session.Pump(delta);
                bool nowPaused = pause != null && (bool)IsPaused.GetValue(pause, null);
                if (wasPaused != nowPaused)
                {
                    // the native GameLoop sees the already-applied state on its
                    // next tick, so dispatch its sound transition exactly once
                    (nowPaused ? OnPause : OnResume).Invoke(GameLoop.instance, null);
                    // pause consumes menu edges, held gameplay input stays native
                    JKRuntime.Presentation.SmoothRendering.Reset();
                }
                if (wasPaused || nowPaused || titleActive) ResponsiveInput.MenuConsumed();
              }
            }
            finally
            { Pumping = false; }
        }
    }
}
