using System;
using JumpKing;

namespace JKRuntime.UI
{
    /// <summary>Semantic feedback using the game's existing menu SFX and volume settings.</summary>
    public enum UiSound { Move, Confirm, Change, Back, Error }

    /// <summary>Play after a successful UI transition, never from Draw. Missing optional audio cannot fail the action.</summary>
    public static class UiSounds
    {
        internal static Action<UiSound> TestPlayback = null;
        private static bool reportedFailure;

        public static void Play(UiSound sound)
        {
            if (sound < UiSound.Move || sound > UiSound.Error) throw new ArgumentOutOfRangeException("sound");
            // Native OnMoveCursor and OnBack intentionally do nothing. Keep the
            // semantic values for consumers, but preserve that silence everywhere.
            if (sound == UiSound.Move || sound == UiSound.Back) return;
            if (TestPlayback != null) { TestPlayback(sound); return; }
            var game = Game1.instance;
            if (game == null || game.contentManager == null || game.contentManager.audio == null || game.contentManager.audio.menu == null) return;
            var menu = game.contentManager.audio.menu;
            // Native MenuSelector calls OnSelect for both activation and an
            // accepted slider/option edit. Select belongs to gameplay item toggles.
            var effect = sound == UiSound.Error ? menu.MenuFail : menu.CursorMove;
            if (effect == null) return;
            try { effect.Play(); }
            catch (Exception error)
            {
                if (reportedFailure) return;
                reportedFailure = true;
                Console.WriteLine("[JK Runtime UI] Menu sound unavailable: " + error.GetBaseException().Message);
            }
        }

        internal static void Select(ref int index, int next)
        { if (index != next) { index = next; Play(UiSound.Move); } }
    }
}
