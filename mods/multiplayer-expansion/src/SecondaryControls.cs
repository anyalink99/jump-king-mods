using JumpKing.Controller;

namespace MultiplayerExpansion
{
    internal sealed class SecondaryControls
    {
        internal const int Left = 0xDB, Right = 0xDD, Jump = 0xDC;
        private bool leftReady, rightReady, jumpReady;
        internal static bool Reserved(int key) { return key == Left || key == Right || key == Jump; }
        // Runtime layers keep the device's profile, not its concrete class
        internal static bool IsKeyboard(IPad pad) { return pad != null && pad.GetSaveIdentifier() == "pc_keyboard_jump_king"; }
        internal static bool Accepts(int role, int selected) { return (role == 1 || role == 2) && selected == 3 - role; }
        private static bool Held(bool enabled, bool down, ref bool ready)
        {
            // focus and menu transitions must see a release before accepting another hold
            if (!enabled) { ready = false; return false; }
            if (!down) ready = true;
            return down && ready;
        }
        internal void Apply(ref PadState state, bool enabled, bool left, bool right, bool jump)
        {
            state.left |= Held(enabled, left, ref leftReady);
            state.right |= Held(enabled, right, ref rightReady);
            state.jump |= Held(enabled, jump, ref jumpReady);
        }
    }
}
