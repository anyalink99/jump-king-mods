using JKRuntime.UI;

namespace JKRuntime.Input
{
    /// <summary>Per-producer boundary between physical text editing and bound actions.
    /// Use one instance per keyboard stream, under that stream's existing lock.</summary>
    public sealed class KeyboardInputGate
    {
        private int epoch = TextInputCapture.Epoch;
        private bool draining = TextInputCapture.Active;

        /// <summary>Call before publishing held actions AND before delivering queued edges.
        /// Pass whether the unfiltered source still holds any keys/buttons. When true,
        /// discard queued actions as well as the current frame. A release sample is
        /// itself suppressed; subsequent fresh presses work normally. Safe on workers;
        /// does not read game objects. Independent producers must observe their own release.</summary>
        public bool Suppress(bool anyHeld)
        {
            int current = TextInputCapture.Epoch;
            if (current != epoch) { epoch = current; draining = true; }
            if (TextInputCapture.Active) { draining = true; return true; }
            if (!draining) return false;
            if (!anyHeld) draining = false;
            return true;
        }
    }
}
