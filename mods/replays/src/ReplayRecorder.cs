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
        private readonly ReplayWorldRecording worldTrack=new ReplayWorldRecording();
        private ReplayCaptureComponent captureComponent;
        private IDisposable captureAttachment;
        private bool completed;
        private bool hasAttemptStamp;
        private JKRuntime.State.AttemptStamp attemptStamp;
        private int appearanceVersion;
        private long cosmeticSequence = ReplayCosmetics.Sequence;
        private readonly List<ReplaySound> pendingSounds = new List<ReplaySound>();
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
            ReplayAudio.Played += CaptureSound;
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
            var clock = JKRuntime.State.GameClock.ReadVictory();
            replay.Header.CompletionTime = TimeSpan.FromSeconds(clock.Time
                + clock.Ticks * (replay.Header.TickDuration / (double)TimeSpan.TicksPerSecond)).Ticks;
            // completion transfers an immutable recording without another full
            // frame-buffer copy. the repository keeps failed writes for retry
            return repository.QueueCompleted(replay);
        }

        internal void Discard()
        {
            completed = true;
            ReplayAudio.Played -= CaptureSound;
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
            {
                pendingSounds.Clear();
                return;
            }

            JKRuntime.State.AttemptStamp currentStamp = ReplayAttemptClock.Read();
            if (hasAttemptStamp && !attemptStamp.Equals(currentStamp))
            {
                VerificationBridge.Forget(replay);
                replay = CreateReplay();
                worldTrack.Reset();
                pendingSounds.Clear();
                cosmeticSequence = ReplayCosmetics.Sequence;
                RecordAppearance(0);
            }
            attemptStamp = currentStamp;
            hasAttemptStamp = true;
            if (AtRecordingLimit) return;
            if (replay.Frames.Count == 0)
            {
                var clock = JKRuntime.State.GameClock.ReadCurrent();
                replay.Header.InitialGameTicks = clock.Ticks;
                replay.Header.InitialGameTime = clock.Time;
            }
            int currentAppearanceVersion =
                ReplayAppearanceTrack.CurrentVersion();
            if (currentAppearanceVersion != appearanceVersion)
                RecordAppearance(replay.Frames.Count);
            AppendFrame(replay.Frames,
                ReplayFrameCapture.Capture(
                    current,
                    replay.Frames.Count), ReplayCodec.MaximumFrames);
            worldTrack.Capture(replay,replay.Frames.Count-1);
            ReplayCosmetics.Capture(replay, replay.Frames.Count - 1, ref cosmeticSequence);
            foreach (var sound in pendingSounds)
                if (replay.SoundEvents.Count < 1000000)
                    replay.SoundEvents.Add(new ReplaySoundEvent { Frame = replay.Frames.Count - 1, Sound = sound });
            pendingSounds.Clear();
        }

        private void CaptureSound(ReplaySound sound)
        {
            if (!completed && !ReplayRuntime.ViewerActive && player != null
                && ShouldRecordBody(player.m_body) && pendingSounds.Count < 32)
                pendingSounds.Add(sound);
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
            // presentation transitions still use game ticks
            // skipping those frames shifts all later poses and the replay wind clock
            return body != null && (body.Enabled || JKRuntime.Gameplay.PresentationActivity.IsActive(body));
        }

        protected override void OnDestroy()
        {
            ReplayAudio.Played -= CaptureSound;
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
                    TickDuration = Game1.instance.TargetElapsedTime.Ticks,
                    CreatedUtc = DateTime.UtcNow,
                    GameVersion = typeof(Game1).Assembly.GetName()
                        .Version.ToString(),
                    ReplaysVersion = typeof(ReplayRecorder).Assembly
                        .GetName().Version.ToString()
                },
                AppearanceEvents = new List<ReplayAppearanceEvent>(),
                Frames = new List<ReplayFrame>(3600),
                SoundEvents = new List<ReplaySoundEvent>()
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
