using System.Collections.Generic;
using System.Linq;
using JumpKing.Controller;

namespace MorphBallMod
{
    internal static class MorphInput
    {
        internal static bool IsMorphHeld()
        {
            SettingsStore.EnsureLoaded();
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected)
            {
                return false;
            }
            int[] pressed = main.GetPad().GetPressedButtons();
            int[] bindings = SettingsStore.Current
                .KeyBindings[MorphBinding.Morph];
            return pressed.Any(
                delegate(int button)
                {
                    return ((IEnumerable<int>)bindings).Contains(button);
                });
        }
    }
}
