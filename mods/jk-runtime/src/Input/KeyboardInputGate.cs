using JKRuntime.UI;

namespace JKRuntime.Input
{
    /// <summary>per-producer boundary between physical UI capture and bound actions
    /// use one instance per keyboard stream, under that stream's existing lock</summary>
    public sealed class KeyboardInputGate
    {
        private int epoch = TextInputCapture.Epoch;
        private bool draining = TextInputCapture.Active;
        private readonly bool modal;
        private int modalEpoch;

        public KeyboardInputGate() : this(true) { }
        // the desktop pad must leave raw keys available to the modal's reader
        internal KeyboardInputGate(bool includeModal)
        { modal = includeModal; modalEpoch = InputContextLease.Epoch; }

        /// <summary>call before publishing held actions AND before delivering queued edges
        /// Pass whether the unfiltered source still holds any keys/buttons. when true,
        /// discard queued actions as well as the current frame. A release sample is
        /// itself suppressed, later fresh presses work normally. Safe on workers;
        /// doesn't read game objects. independent producers must observe their own release</summary>
        public bool Suppress(bool anyHeld)
        {
            int current = TextInputCapture.Epoch;
            if (modal)
            {
                int next = InputContextLease.Epoch;
                if (modalEpoch != next) { modalEpoch = next; draining = true; }
                if (InputContextLease.Active) { draining = true; return true; }
            }
            if (current != epoch) { epoch = current; draining = true; }
            if (TextInputCapture.Active) { draining = true; return true; }
            if (!draining) return false;
            if (!anyHeld) draining = false;
            return true;
        }
    }
}
