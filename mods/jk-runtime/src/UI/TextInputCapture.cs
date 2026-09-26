using System;
using System.Threading;
using JumpKing.Controller;
using JKRuntime.Input;

namespace JKRuntime.UI
{
    // The permanent keyboard pad stays installed across page transitions. Keep
    // suppressing held keys after a forced close until their physical release.
    internal sealed class TextInputCapture : IDisposable
    {
        private static int owners, epoch;
        private bool disposed;
        internal static int Epoch { get { return Volatile.Read(ref epoch); } }
        internal TextInputCapture()
        {
            RuntimeApi.Kernel.CheckThread();
            Interlocked.Increment(ref owners);
            Interlocked.Increment(ref epoch);
            try { ClearPublishedInput(); }
            catch { Interlocked.Decrement(ref owners); Interlocked.Increment(ref epoch); throw; }
        }
        internal static bool Active { get { return Volatile.Read(ref owners) > 0; } }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread();
            if (disposed) return;
            disposed = true;
            Interlocked.Decrement(ref owners);
            Interlocked.Increment(ref epoch);
            ClearPublishedInput();
        }
        private static void ClearPublishedInput()
        {
            var manager = ControllerManager.instance;
            if (manager == null) return;
            foreach (var pad in manager.GetConnectedPads())
                if (pad.IsValid && pad.GetPad().GetSaveIdentifier() == "pc_keyboard_jump_king")
                {
                    KeyboardMousePad.Ensure(pad);
                    NativeInputFrames.Publish(pad, new PadState(), new PadState());
                }
            // The opening/closing gesture already belongs to this page. Never
            // leave an earlier aggregate frame available to its parent tree.
            if (manager.MenuController != null) manager.MenuController.ConsumePadPresses();
        }
    }
}
