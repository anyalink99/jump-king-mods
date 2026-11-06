using System;
using EntityComponent;
using JumpKing;

namespace Replays
{
    internal static class ReplayRuntime
    {
        private static ReplayRepository repository;
        private static ReplayRecorder recorder;
        private static ReplayGhostEntity ghost;
        private static IReplayViewer viewer;
        private static ReplayWorld world;
        private static ReplayWorld preparedWorld;
        private static bool levelActive;
        private static string pendingWatchId;
        private static int winsAtLevelStart;

        internal static ReplayRepository Repository
        {
            get
            {
                if (repository == null)
                    repository = ReplayRepository.CreateDefault();
                return repository;
            }
        }

        internal static bool ViewerActive { get { return viewer != null; } }
        internal static void BindVerification() { if (recorder != null) recorder.BindVerification(); }
        internal static bool RecordingActive { get { return recorder != null && recorder.IsAlive; } }
        internal static string RecordingStatus { get { if (levelActive && recorder == null && !Repository.CanStartRecording) return "RECORDING PAUSED - RETRY FAILED SAVES"; return recorder != null && recorder.AtRecordingLimit ? "24-HOUR RECORDING LIMIT - SAVE THIS REPLAY" : ""; } }

        internal static void Prepare(JKRuntime.RuntimeScope scope)
        {
            scope.Defer(delegate { preparedWorld = null; });
            using (JKRuntime.RuntimeApi.MeasureStartup("replays.world-identity"))
                preparedWorld = ReplayWorldIdentity.Current();
        }

        internal static void Install()
        {
            Uninstall();
            levelActive = true;
            world = WorldForStart();
            winsAtLevelStart = ReplayVictoryClock.Read();
            if (!string.IsNullOrWhiteSpace(pendingWatchId))
            {
                ReplayData replay = SafeLoad(pendingWatchId);
                if (MatchesCurrentWorld(replay))
                {
                    BeginViewer(replay);
                    return;
                }
                pendingWatchId = null;
            }
            StartNormalRuntime();
        }
        internal static ReplayWorld WorldForStart() { return preparedWorld ?? ReplayWorldIdentity.Current(); }

        internal static void Uninstall()
        {
            levelActive = false;
            AbortViewer();
            StopRecorder(false);
            if (ghost != null && ghost.IsAlive) ghost.Destroy();
            ghost = null;
            world = null;
        }

        internal static void ApplyRecordingSetting()
        {
            ReplaySettingsStore.EnsureLoaded();
            if (!levelActive || ViewerActive) return;
            if (ReplaySettingsStore.Current.RecordingEnabled)
            {
                if ((recorder == null || !recorder.IsAlive) && Repository.CanStartRecording)
                    recorder = new ReplayRecorder(Repository, world);
            }
            else StopRecorder(false);
        }

        internal static void RefreshGhost()
        {
            if (ghost != null && ghost.IsAlive) ghost.Destroy();
            ghost = null;
            if (!levelActive || ViewerActive) return;
            ReplaySettingsStore.EnsureLoaded();
            ReplayData replay = SafeLoad(
                ReplaySettingsStore.Current.GhostReplayId);
            if (MatchesCurrentWorld(replay))
                ghost = new ReplayGhostEntity(replay);
        }

        internal static bool RequestWatch(string id)
        {
            pendingWatchId = id;
            ReplayData replay = SafeLoad(id);
            if (!levelActive)
            {
                if (replay != null && CanWatch(replay.Header)) return true;
                pendingWatchId = null;
                return false;
            }
            if (!MatchesCurrentWorld(replay))
            {
                pendingWatchId = null;
                return false;
            }
            return BeginViewer(replay);
        }

        internal static void ViewerClosed(IReplayViewer page)
        {
            if (!ReferenceEquals(viewer, page)) return;
            viewer = null;
            pendingWatchId = null;
            if (!levelActive) return;
            if (!ReplayUI.ReturnToMainMenu())
                Console.WriteLine("[Replays] Could not return to the main menu");
        }

        internal static bool SaveCurrentReplay()
        {
            if (!levelActive || ViewerActive || recorder == null
                || !recorder.IsAlive)
            {
                return false;
            }
            return recorder.SaveSnapshot();
        }

        internal static void EndLevel()
        {
            bool completedRun = levelActive
                && ReplayVictoryClock.Read() > winsAtLevelStart;
            levelActive = false;
            AbortViewer();
            StopRecorder(completedRun);
            if (ghost != null && ghost.IsAlive) ghost.Destroy();
            ghost = null;
            world = null;
        }

        private static bool IsCurrentWorld(ReplayHeader header)
        {
            return header != null
                && world != null
                && string.Equals(
                    header.WorldKey,
                    world.Key,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    header.WorldRevision,
                    world.Revision,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool CanWatch(ReplayHeader header)
        {
            if (header == null) return false;
            ReplayWorld current = world ?? ReplayWorldIdentity.Current();
            return current != null
                && string.Equals(
                    header.WorldKey,
                    current.Key,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    header.WorldRevision,
                    current.Revision,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static void Deleted(string id)
        {
            ReplaySettingsStore.EnsureLoaded();
            if (string.Equals(
                ReplaySettingsStore.Current.GhostReplayId,
                id,
                StringComparison.OrdinalIgnoreCase))
            {
                ReplaySettingsStore.SetGhost(string.Empty);
                RefreshGhost();
            }
            if (string.Equals(pendingWatchId, id, StringComparison.OrdinalIgnoreCase))
                pendingWatchId = null;
        }

        private static void StartNormalRuntime()
        {
            if (!levelActive || ViewerActive || world == null) return;
            ApplyRecordingSetting();
            RefreshGhost();
        }

        private static void StopRecorder(bool saveCompletedRun)
        {
            if (recorder == null) return;
            if (saveCompletedRun)
            {
                if (!recorder.SaveCompletedRun()) Console.WriteLine("[Replays] Completed recording could not be queued; inspect pending/failed saves in the replay library.");
            }
            else recorder.Discard();
            if (recorder.IsAlive) recorder.Destroy();
            recorder = null;
        }

        private static bool BeginViewer(ReplayData replay)
        {
            if (!levelActive || replay == null || ViewerActive) return false;
            StopRecorder(false);
            if (ghost != null && ghost.IsAlive) ghost.Destroy();
            ghost = null;
            viewer = ReplayUI.CreateViewer(replay);
            ReplayUI.ClosePauseMenu();
            if (OpenViewer()) return true;
            ScheduleViewer();
            return true;
        }

        private static bool OpenViewer()
        {
            return viewer != null
                && !viewer.IsOpen
                && ReplayUI.OpenViewer(viewer);
        }

        private static void ScheduleViewer()
        {
            if (!levelActive || viewer == null) return;
            Game1.callbackManager.CreateRoutine(
                1,
                delegate
                {
                    if (!levelActive || viewer == null) return;
                    if (OpenViewer()) return;
                    Console.WriteLine(
                        "[Replays] UIApi+ did not provide a modal host");
                    pendingWatchId = null;
                    AbortViewer();
                    StartNormalRuntime();
                });
        }

        private static void AbortViewer()
        {
            IReplayViewer current = viewer;
            viewer = null;
            if (current != null) current.Abort();
        }

        private static ReplayData SafeLoad(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            try { return Repository.Load(id); }
            catch (Exception error)
            {
                Console.WriteLine(
                    "[Replays] Could not load replay "
                    + id
                    + ": "
                    + error.Message);
                return null;
            }
        }

        private static bool MatchesCurrentWorld(ReplayData replay)
        {
            return replay != null && IsCurrentWorld(replay.Header);
        }
    }
}
