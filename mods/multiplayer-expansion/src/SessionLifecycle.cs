using System;

namespace MultiplayerExpansion
{
    internal enum SessionPhase { Off, Preparing, Starting, Running, Stopping, Failed }
    internal enum StartDecision { Ignore, Prepare, Stop }

    internal sealed class SessionLifecycle
    {
        internal SessionPhase Phase { get; private set; }
        internal string Mode { get; private set; }
        internal string PendingMode { get; private set; }
        internal string Error { get; private set; }
        internal static string StartupMode(string value)
        { return string.IsNullOrEmpty(value) ? "Local" : value; }
        internal StartDecision Start(string mode)
        {
            if (mode != "Local" && mode != "Steam") throw new ArgumentException("Unknown connection mode", "mode");
            if (Phase == SessionPhase.Off || Phase == SessionPhase.Failed)
            {
                Mode = mode; PendingMode = Error = null; Phase = SessionPhase.Preparing;
                return StartDecision.Prepare;
            }
            if (Phase != SessionPhase.Stopping && Mode == mode) return StartDecision.Ignore;
            PendingMode = mode;
            if (Phase == SessionPhase.Stopping) return StartDecision.Ignore;
            Phase = SessionPhase.Stopping; return StartDecision.Stop;
        }
        internal bool Prepared()
        {
            if (Phase != SessionPhase.Preparing) return false;
            Phase = SessionPhase.Starting; return true;
        }
        internal void Ready() { if (Phase == SessionPhase.Starting) Phase = SessionPhase.Running; }
        internal void Cancel() { PendingMode = null; Phase = SessionPhase.Stopping; }
        internal void Fail(string error) { Error = error; PendingMode = null; Phase = SessionPhase.Stopping; }
        internal string Stopped()
        {
            string next = PendingMode; PendingMode = null; Mode = null;
            Phase = Error == null ? SessionPhase.Off : SessionPhase.Failed;
            return next;
        }
    }
}
