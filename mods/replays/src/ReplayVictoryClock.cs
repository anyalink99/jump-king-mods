namespace Replays
{
    internal static class ReplayVictoryClock
    {
        internal static int Read()
        {
            return JKRuntime.State.GameClock.ReadWins();
        }
    }
}
