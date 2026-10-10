using System;

namespace MultiplayerExpansion
{
    internal static class SessionLifecycleTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Run()
        {
            Check(SessionLifecycle.StartupMode(null) == "Local" && SessionLifecycle.StartupMode("Off") == "Off", "Default/opt-out startup changed");
            var session = new SessionLifecycle();
            Check(session.Start("Local") == StartDecision.Prepare, "Initial start didn't prepare");
            Check(session.Start("Local") == StartDecision.Ignore, "Double start wasn't coalesced");
            Check(session.Start("Steam") == StartDecision.Stop, "Transport switch didn't stop the old session");
            Check(!session.Prepared(), "Cancelled preparation still launched a child");
            Check(session.Start("Local") == StartDecision.Ignore && session.PendingMode == "Local", "Queued switch didn't keep the last request");
            session.Cancel();
            Check(session.Stopped() == null && session.Phase == SessionPhase.Off, "Cancel relaunched a queued session");
            session.Start("Local"); Check(session.Prepared(), "Fresh preparation wasn't accepted");
            Check(session.Phase == SessionPhase.Starting, "Child became ready before it drew a frame");
            session.Ready(); Check(session.Phase == SessionPhase.Running, "Ready child wasn't enabled");
            session.Start("Steam"); Check(session.Stopped() == "Steam", "Transport restart was lost");
            session.Start("Steam"); session.Fail("load failed");
            Check(session.Stopped() == null && session.Phase == SessionPhase.Failed, "Failure triggered an automatic restart loop");
            Check(session.Start("Local") == StartDecision.Prepare && session.Error == null, "Retry retained the failed state");
            Console.WriteLine("[OK] Session lifecycle: local default, duplicate start, cancellation, queued transport switch, readiness and retry");
        }
    }
}
