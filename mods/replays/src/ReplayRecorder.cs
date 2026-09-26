using System;
using System.Collections.Generic;
using EntityComponent;
using JumpKing;
using JumpKing.Player;

namespace Replays
{
    internal sealed class ReplayRecorder : Entity
    {
        private readonly ReplayRepository repository;
        private readonly ReplayWorld world;
        private readonly List<int> appearanceScratch = new List<int>(5);
        private ReplayData replay;
        private PlayerEntity player;
        private ReplayCaptureComponent captureComponent;
        private IDisposable captureAttachment;
        private bool completed;
        private bool hasAttemptStamp;
        private JKRuntime.State.AttemptStamp attemptStamp;
        private int appearanceVersion;
        internal bool AtRecordingLimit { get { return replay != null && replay.Frames.Count >= ReplayCodec.MaximumFrames; } }
        internal void BindVerification() { VerificationBridge.Associate(replay); }

        internal ReplayRecorder(
            ReplayRepository replayRepository,
            ReplayWorld world)
        {
            if (replayRepository == null)
                throw new ArgumentNullException("replayRepository");
            if (world == null)
                throw new ArgumentNullException("world");
            repository = replayRepository;
            this.world = world;
            replay = CreateReplay();
            VerificationBridge.Associate(replay);
            RecordAppearance(0);
        }

        internal bool SaveSnapshot()
        {
            if (completed || replay == null || replay.Frames.Count < 2) return false;
            return repository.QueueSnapshot(delegate { return ReplaySnapshot.Create(replay); });
        }

        internal bool SaveCompletedRun()
        {
            if (completed) return false;
            completed = true;
            if (replay == null || replay.Frames.Count < 2) return false;
            // Completion transfers an immutable recording without another full
            // frame-buffer copy. The repository retains failed writes for retry.
            return repository.QueueCompleted(replay);
        }

        internal void Discard()
        {
            completed = true;
            VerificationBridge.Forget(replay);
            replay = null;
            DetachCaptureComponent();
        }

        protected override void Update(float delta)
        {
            if (completed || ReplayRuntime.ViewerActive) return;
            PlayerEntity current = player != null && player.IsAlive
                ? player
                : Find<PlayerEntity>();
            if (current == null || current.m_body == null) return;
            if (ReferenceEquals(current, player)
                && captureComponent != null
                && captureComponent.Enabled)
                return;

            Attach(current);
        }

        internal void Attach(PlayerEntity target)
        {
            DetachCaptureComponent();
            player = target;
            captureComponent = new ReplayCaptureComponent(this, target);
            captureAttachment = new JKRuntime.Gameplay.ComponentAttachment(target, captureComponent);
        }

        internal void Capture(PlayerEntity current)
        {
            if (completed || ReplayRuntime.ViewerActive
                || current == null
                || !ReferenceEquals(current, player)
                || current.m_body == null
                || !ShouldRecordBody(current.m_body))
                return;

            JKRuntime.State.AttemptStamp currentStamp = ReplayAttemptClock.Read();
            if (hasAttemptStamp && !attemptStamp.Equals(currentStamp))
            {
                VerificationBridge.Forget(replay);
                replay = CreateReplay();
                RecordAppearance(0);
            }
            attemptStamp = currentStamp;
            hasAttemptStamp = true;
            if (AtRecordingLimit) return;
            int currentAppearanceVersion =
                ReplayAppearanceTrack.CurrentVersion();
            if (currentAppearanceVersion != appearanceVersion)
                RecordAppearance(replay.Frames.Count);
            AppendFrame(replay.Frames,
                ReplayFrameCapture.Capture(
                    current,
                    replay.Frames.Count), ReplayCodec.MaximumFrames);
        }

        internal static bool AppendFrame(List<ReplayFrame> frames, ReplayFrame value, int limit)
        {
            if (frames.Count >= limit) return false;
            if (frames.Count == frames.Capacity)
                frames.Capacity = Math.Min(limit, Math.Max(4, frames.Capacity * 2));
            frames.Add(value);
            return true;
        }

        internal static bool ShouldRecordBody(BodyComp body)
        {
            // A presentation transition still consumes game ticks. Omitting its
            // frames would shift every later pose and the replay's wind clock.
            return body != null && (body.Enabled || JKRuntime.Gameplay.PresentationActivity.IsActive(body));
        }

        protected override void OnDestroy()
        {
            replay = null;
            DetachCaptureComponent();
            base.OnDestroy();
        }

        private void DetachCaptureComponent()
        {
            if (captureComponent != null)
                captureComponent.Enabled = false;
            if (captureAttachment != null) { captureAttachment.Dispose(); captureAttachment = null; }
            captureComponent = null;
            player = null;
        }

        private ReplayData CreateReplay()
        {
            JKRuntime.State.GameClock.State clock = JKRuntime.State.GameClock.ReadCurrent();
            return new ReplayData
            {
                Header = new ReplayHeader
                {
                    Id = Guid.NewGuid().ToString("N"),
                    WorldKey = world.Key,
                    WorldName = world.Name,
                    WorldAuthor = world.Author,
                    WorldRevision = world.Revision,
                    TotalScreens = world.TotalScreens,
                    InitialGameTicks = clock.Ticks,
                    InitialGameTime = clock.Time,
                    CreatedUtc = DateTime.UtcNow,
                    GameVersion = typeof(Game1).Assembly.GetName()
                        .Version.ToString(),
                    ReplaysVersion = typeof(ReplayRecorder).Assembly
                        .GetName().Version.ToString()
                },
                AppearanceEvents = new List<ReplayAppearanceEvent>(),
                Frames = new List<ReplayFrame>(3600)
            };
        }

        private void RecordAppearance(int frame)
        {
            ReplayAppearanceTrack.RecordIfChanged(
                replay,
                frame,
                appearanceScratch);
            appearanceVersion = ReplayAppearanceTrack.CurrentVersion();
        }

    }

    internal sealed class ReplayCaptureComponent : Component
    {
        private readonly ReplayRecorder recorder;
        private readonly PlayerEntity player;

        internal ReplayCaptureComponent(
            ReplayRecorder owner,
            PlayerEntity playerEntity)
        {
            recorder = owner;
            player = playerEntity;
        }

        protected override void LateUpdate(float delta)
        {
            recorder.Capture(player);
        }
    }
}
