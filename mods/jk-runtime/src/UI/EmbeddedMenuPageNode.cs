using System;
using BehaviorTree;
using JumpKing.Controller;
using JumpKing.Util;

namespace JKRuntime.UI
{
    internal sealed class EmbeddedMenuPageNode : IBTnode, IDrawable
    {
        private readonly UiPageSession session;
        private BTresult? terminal;
        internal EmbeddedMenuPageNode(object factory, IUiPage page)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            session = new UiPageSession(page);
            VanillaMenuAdapter.AddDrawable(factory, this);
        }
        protected override BTresult MyRun(TickData data)
        {
            if (terminal.HasValue) return terminal.Value;
            try
            {
                if (!session.Active) session.Open();
                UiInput input = UiInputRouter.Read(ControllerManager.instance.MenuController.GetPadState(), false);
                UiInputRouter.Consume();
                session.Update(input, data.delta_time, UiPageInput.IsReleased());
                if (session.ReadyToClose) Finish(BTresult.Success);
            }
            catch (Exception error) { Fail(error); }
            return terminal ?? BTresult.Running;
        }
        public void Draw()
        {
            if (terminal.HasValue || last_result != BTresult.Running) return;
            try { session.Draw(); }
            catch (Exception error) { Fail(error); }
        }
        protected override void OnNewRun()
        {
            // TextButton starts its child again on a fresh Confirm without necessarily
            // resetting that child. Never turn a terminal result into an idle reopen.
            if (terminal.HasValue && ControllerManager.instance.MenuController.GetPadState().confirm)
                ResetResult();
        }
        public override void ResetResult()
        {
            // Only an explicit native reset permits another lifetime after failure.
            try { session.Dispose(); terminal = null; }
            catch (Exception error) { terminal = BTresult.Failure; Log(error); }
            ControllerManager.instance.MenuController.ConsumePadPresses();
            base.ResetResult();
        }
        private void Finish(BTresult result)
        {
            terminal = result;
            session.Dispose();
            ControllerManager.instance.MenuController.ConsumePadPresses();
        }
        private void Fail(Exception error)
        {
            Log(error);
            try { Finish(BTresult.Failure); }
            catch (Exception cleanup) { Log(cleanup); }
        }
        private static void Log(Exception error)
        { Console.WriteLine("[JK Runtime UI] Embedded page failed: " + error); }
    }
}
