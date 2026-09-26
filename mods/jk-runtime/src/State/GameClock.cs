using System;
using System.Reflection;
using JumpKing;

namespace JKRuntime.State
{
    /// <summary>Native achievement clock and attempt identity. Physics ticks, wall time and replay clock ownership are distinct contracts.</summary>
    public static class GameClock
    {
        public struct State
        {
            public int Ticks;
            public float Time;
        }

        private static readonly Contract Refs = new Contract();
        private static Lease active;

        public static State ReadCurrent()
        {
            RuntimeApi.Kernel.CheckThread();
            object manager = RequireManager();
            object current = Refs.GetCurrentStats.Invoke(manager, null);
            return new State
            {
                Ticks = Math.Max(0, (int)Refs.Ticks.GetValue(current)),
                Time = Math.Max(0f, (float)Refs.Time.GetValue(current))
            };
        }

        public static AttemptStamp ReadAttempt()
        {
            RuntimeApi.Kernel.CheckThread();
            object manager = RequireManager();
            object current = Refs.GetCurrentStats.Invoke(manager, null);
            return new AttemptStamp
            {
                Session = (int)Refs.Session.GetValue(current),
                Attempt = (int)Refs.Attempts.GetValue(current)
            };
        }

        public static int ReadWins()
        {
            RuntimeApi.Kernel.CheckThread();
            object manager = RequireManager();
            object allTime = Refs.AllTime.GetValue(manager);
            return (int)Refs.TimesWon.GetValue(allTime);
        }

        /// <summary>Acquire the exclusive replay clock override. Dispose restores the original native statistics objects.</summary>
        public static Lease Override(int initialTicks, float initialTime)
        {
            RuntimeApi.Kernel.CheckThread();
            if (active != null) throw new InvalidOperationException("The game clock already has an override owner");
            if (float.IsNaN(initialTime) || float.IsInfinity(initialTime)) throw new ArgumentOutOfRangeException("initialTime");
            var lease = new Lease(
                Refs,
                Math.Max(0, initialTicks),
                Math.Max(0f, initialTime));
            active = lease;
            try { lease.SetFrame(0); return lease; }
            catch (Exception failure)
            {
                try { lease.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Game clock override and rollback failed", failure, cleanup); }
                throw;
            }
        }

        public static void ValidateContract()
        {
            // Constructing Refs validates every reflected member. The singleton
            // itself is created later by Jump King's normal startup sequence.
            FieldInfo unused = Refs.ManagerInstance;
        }

        private static object RequireManager()
        {
            object manager = Refs.ManagerInstance.GetValue(null);
            if (manager == null)
                throw new InvalidOperationException(
                    "Jump King game clock is not initialized");
            return manager;
        }

        public sealed class Lease : IDisposable
        {
            private readonly Contract refs;
            private readonly object manager;
            private readonly object originalAllTime;
            private readonly object originalSnapshot;
            private readonly object originalWinStats;
            private readonly bool originalInGameLoop;
            private readonly int snapshotTicks;
            private readonly float snapshotTime;
            private readonly int initialTicks;
            private readonly float initialTime;
            private bool disposed;

            internal Lease(Contract value, int ticks, float time)
            {
                refs = value;
                manager = RequireManager();
                initialTicks = ticks;
                initialTime = time;
                originalAllTime = refs.AllTime.GetValue(manager);
                originalSnapshot = refs.Snapshot.GetValue(manager);
                originalWinStats = refs.WinStats.GetValue(manager);
                originalInGameLoop = (bool)refs.InGameLoop.GetValue(manager);
                snapshotTicks = (int)refs.Ticks.GetValue(originalSnapshot);
                snapshotTime = (float)refs.Time.GetValue(originalSnapshot);
            }

            public void SetFrame(int frame)
            {
                RuntimeApi.Kernel.CheckThread();
                if (disposed) throw new ObjectDisposedException("GameClock.Lease");
                object allTime = refs.AllTime.GetValue(manager);
                long target = (long)snapshotTicks + initialTicks + Math.Max(0, frame);
                refs.Ticks.SetValue(
                    allTime,
                    target > int.MaxValue ? int.MaxValue : (int)target);
                refs.Time.SetValue(allTime, snapshotTime + initialTime);
                refs.AllTime.SetValue(manager, allTime);
            }

            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (disposed) return;
                refs.AllTime.SetValue(manager, originalAllTime);
                refs.Snapshot.SetValue(manager, originalSnapshot);
                refs.WinStats.SetValue(manager, originalWinStats);
                refs.InGameLoop.SetValue(manager, originalInGameLoop);
                disposed = true;
                if (active == this) active = null;
            }
        }

        internal sealed class Contract
        {
            public readonly FieldInfo ManagerInstance;
            public readonly MethodInfo GetCurrentStats;
            public readonly FieldInfo AllTime;
            public readonly FieldInfo Snapshot;
            public readonly FieldInfo WinStats;
            public readonly FieldInfo InGameLoop;
            public readonly FieldInfo Ticks;
            public readonly FieldInfo Time;
            public readonly FieldInfo Session;
            public readonly FieldInfo Attempts;
            public readonly FieldInfo TimesWon;

            public Contract()
            {
                Assembly assembly = typeof(Game1).Assembly;
                Type manager = assembly.GetType(
                    "JumpKing.MiscSystems.Achievements.AchievementManager",
                    true);
                Type stats = assembly.GetType(
                    "JumpKing.MiscSystems.Achievements.PlayerStats",
                    true);
                BindingFlags instance = BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic;
                ManagerInstance = manager.GetField(
                    "instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                GetCurrentStats = manager.GetMethod(
                    "GetCurrentStats",
                    BindingFlags.Instance | BindingFlags.Public);
                AllTime = manager.GetField("m_all_time_stats", instance);
                Snapshot = manager.GetField("m_snapshot", instance);
                WinStats = manager.GetField("m_win_stats", instance);
                InGameLoop = manager.GetField("m_in_game_loop", instance);
                Ticks = stats.GetField("_ticks", instance);
                Time = stats.GetField("time", instance);
                Session = stats.GetField("session", instance);
                Attempts = stats.GetField("attempts", instance);
                TimesWon = stats.GetField("times_won", instance);
                if (ManagerInstance == null
                    || GetCurrentStats == null
                    || AllTime == null
                    || Snapshot == null
                    || WinStats == null
                    || InGameLoop == null
                    || Ticks == null
                    || Time == null
                    || Session == null
                    || Attempts == null
                    || TimesWon == null)
                {
                    throw new InvalidOperationException(
                        "Jump King game-clock contract is unavailable");
                }
            }
        }
    }
}
