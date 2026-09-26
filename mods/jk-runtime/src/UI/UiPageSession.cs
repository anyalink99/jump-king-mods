using System;
using JumpKing.Controller;
using Microsoft.Xna.Framework.Input;

namespace JKRuntime.UI
{
    // Every host shares page ownership, Cancel policy and release boundaries.
    internal sealed class UiPageSession : IDisposable
    {
        internal readonly IUiPage Page;
        internal bool Active { get; private set; }
        internal bool Faulted { get; private set; }
        internal bool ReadyToClose { get; private set; }
        private bool cleanupPending, entering, closing, disposing;

        internal UiPageSession(IUiPage page)
        { if (page == null) throw new ArgumentNullException("page"); Page = page; }
        internal void Open()
        {
            if (Active) throw new InvalidOperationException("Page is already open");
            Dispose();
            Faulted = ReadyToClose = closing = false;
            Active = cleanupPending = entering = true;
            try { Page.OnOpen(); }
            catch (Exception error)
            {
                Faulted = true;
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Page open and cleanup failed", error, cleanup); }
                throw;
            }
        }
        internal void Resume()
        { entering = true; UiPointer.CancelCapture(Page); }
        internal void Update(UiInput input, float delta, bool released)
        {
            if (!Active || Faulted) return;
            if (entering) { if (released) entering = false; return; }
            if (closing) { ReadyToClose = released; return; }
            try
            {
                input = UiPointer.Read(Page, input);
                Page.Update(input, delta);
                if (!Active) return;
                var policy = Page as IUiPageInputPolicy;
                closing = Page.WantsClose || (input.Cancel && (policy == null || !policy.HandlesCancel));
                ReadyToClose = closing && released;
            }
            catch { Faulted = true; throw; }
        }
        internal void Draw()
        {
            if (!Active || Faulted) return;
            try { UiPointer.BeginSurface(Page); Page.Draw(); }
            catch { Faulted = true; throw; }
        }
        public void Dispose()
        {
            Active = false;
            if (!cleanupPending) return;
            if (disposing) throw new InvalidOperationException("Reentrant page cleanup");
            disposing = true;
            try
            {
                UiPointer.CancelCapture(Page);
                Page.OnClose();
                cleanupPending = false;
            }
            finally { disposing = false; }
        }
    }
    internal static class UiPageInput
    {
        internal static Func<bool> ReleaseProbe { get; set; }
        internal static bool IsReleased()
        {
            if (ReleaseProbe != null) return ReleaseProbe();
            if (Keyboard.GetState().GetPressedKeys().Length != 0) return false;
            MouseState mouse = Mouse.GetState();
            if (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed) return false;
            if (ControllerManager.instance != null)
                foreach (PadInstance pad in ControllerManager.instance.GetConnectedPads())
                    if (pad.IsValid && pad.IsConnected && pad.GetPad().GetPressedButtons().Length != 0) return false;
            return true;
        }
    }
}
