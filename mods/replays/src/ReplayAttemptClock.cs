namespace Replays
{
    internal static class ReplayAttemptClock
    {
        internal static JKRuntime.State.AttemptStamp Read()
        {
            return JKRuntime.State.GameClock.ReadAttempt();
        }
    }
}
