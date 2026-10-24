using System;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;

namespace MultiplayerExpansion
{
    internal static class SessionLifecycleTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Run()
        {
            ManualStartup();
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
            Console.WriteLine("[OK] Session lifecycle: manual startup, duplicate start, cancellation, queued transport switch, readiness and retry");
        }
        private static void ManualStartup()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var debugField = typeof(LevelDebugState).GetField("_instance", flags);
            var pollField = typeof(NativeSession).GetField("lastPoll", flags);
            var lifecycle = (SessionLifecycle)typeof(NativeSession).GetField("lifecycle", flags).GetValue(null);
            var previousDebug = debugField.GetValue(null);
            var previousPoll = pollField.GetValue(null);
            string previousMode = Environment.GetEnvironmentVariable("MPEX_NATIVE_TRANSPORT");
            string previousStatus = NativeSession.Status;
            try
            {
                // enable the real debug gate without starting a game or touching its window
                debugField.SetValue(null, FormatterServices.GetUninitializedObject(typeof(LevelDebugState)));
                Check(NativeMod.DebugEnabled, "Debug startup fixture didn't enable the native gate");
                foreach (string mode in new[] { null, "", "Local", "Steam", "Off" })
                {
                    Environment.SetEnvironmentVariable("MPEX_NATIVE_TRANSPORT", mode);
                    pollField.SetValue(null, DateTime.MinValue);
                    NativeSession.Tick();
                    Check(lifecycle.Phase == SessionPhase.Off && NativeSession.Status == previousStatus,
                        "Debug tick started a session without a menu action: " + mode);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MPEX_NATIVE_TRANSPORT", previousMode);
                debugField.SetValue(null, previousDebug);
                pollField.SetValue(null, previousPoll);
                NativeSession.Status = previousStatus;
            }
        }
    }
}
