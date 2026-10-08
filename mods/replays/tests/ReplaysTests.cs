using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using JumpKing;
using Microsoft.Xna.Framework;
using Replays;

internal static class ReplaysTests
{
    private static int failures;

    private static int Main()
    {
        TestCodecRoundTrip();
        TestCosmeticCodec();
        TestVisualGeometryCodec();
        TestCodecRejectsInvalidFrame();
        TestPixelSnapping();
        TestLayeredPoseRecognition();
        TestRepositoryLifecycle();
        TestRepositoryInvalidationRace();
        TestBackgroundSaveResults();
        TestRecorderOwnershipAndLimit();
        TestTimeline();
        TestAttemptClockContract();
        TestVictoryClockContract();
        TestGameClockContract();
        TestVictoryIsolationContract();
        TestAppearanceContract();
        TestAttemptStamp();
        TestAppearanceTimeline();
        TestSnapshotIsolation();
        TestSettingsDefaultsAndSerialization();
        TestPresentationTimeline();
        TestPreparedWorld();
        TestCameraTarget();
        TestVerificationBridge();
        TestAudioAndTiming();
        TestNativeAudioHook();
        TestCompletionSaving();
        if (failures != 0)
        {
            Console.Error.WriteLine("Replays tests failed: " + failures);
            return 1;
        }
        Console.WriteLine("[OK] Replays tests");
        return 0;
    }
    private static void TestCosmeticCodec()
    {
        string directory=NewDirectory();
        try
        {
            var replay=SampleReplay("cosmetic");replay.CosmeticEvents.Add(new ReplayCosmeticEvent{Frame=1,Data="{\"Trigger\":\"jump\",\"Surface\":\"snow\"}"});
            string path=Path.Combine(directory,"v3.jkr");ReplayCodec.Write(path,replay);var restored=ReplayCodec.Read(path);
            Assert(restored.CosmeticEvents.Count==1&&restored.CosmeticEvents[0].Data==replay.CosmeticEvents[0].Data,"sparse cosmetic events round-trip");
            var snapshot=ReplaySnapshot.Create(replay);replay.CosmeticEvents[0].Data="changed";Assert(snapshot.CosmeticEvents[0].Data!=replay.CosmeticEvents[0].Data,"cosmetic save snapshot is independent");
            foreach(int legacyVersion in new[]{2,3,4})
            {
            string old=Path.Combine(directory,"v"+legacyVersion+".jkr");
            using(var file=File.Create(old))using(var gzip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionMode.Compress))using(var writer=new BinaryWriter(gzip))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("JKRP"));writer.Write(legacyVersion);
                var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
                typeof(ReplayCodec).GetMethod("WriteHeader",flags).Invoke(null,new object[]{writer,replay.Header});
                typeof(ReplayCodec).GetMethod("WriteAppearance",flags).Invoke(null,new object[]{writer,replay});
                if(legacyVersion>=3)typeof(ReplayCodec).GetMethod("WriteCosmetics",flags).Invoke(null,new object[]{writer,replay.CosmeticEvents,replay.Frames.Count});
                foreach(var frame in replay.Frames){writer.Write(frame.Position.X);writer.Write(frame.Position.Y);writer.Write(frame.VisualOffset.X);writer.Write(frame.VisualOffset.Y);writer.Write(frame.Screen);writer.Write((byte)frame.Flags);writer.Write((byte)frame.Pose);if(legacyVersion>=4){writer.Write(9f);writer.Write(26f);}}
            }
            var legacy=ReplayCodec.Read(old);
            Assert(legacy.CosmeticEvents.Count==(legacyVersion==2?0:1)&&legacy.Frames.Count==2&&legacy.Frames[0].AnchorOffset.HasValue==(legacyVersion>=4),"Legacy recording retains its frame layout and native anchor: "+legacyVersion);
            Assert(legacy.SoundEvents==null&&!legacy.Header.CompletionTime.HasValue&&legacy.Header.TickDuration==170000,"legacy timing and missing audio remain distinguishable");
            }
            replay.CosmeticEvents[0].Frame=99;bool rejected=false;try{ReplayCodec.Write(Path.Combine(directory,"invalid.jkr"),replay);}catch(InvalidDataException){rejected=true;}Assert(rejected,"cosmetic events outside recorded frames are rejected");
        }
        finally{Directory.Delete(directory,true);}
    }
    private static void TestVisualGeometryCodec()
    {
        string directory=NewDirectory();
        try {
            var replay=SampleReplay("jing");var frame=replay.Frames[0];frame.Position=new Vector2(180.25f,200.75f);frame.AnchorOffset=new Vector2(5,14);replay.Frames[0]=frame;
            string path=Path.Combine(directory,"geometry.jkr");ReplayCodec.Write(path,replay);var read=ReplayCodec.Read(path);
            Assert(read.Frames[0].DrawAnchor==new Vector2(185.25f,214.75f)+frame.VisualOffset,"New replay format preserves Jing's fractional draw anchor");
            frame.AnchorOffset=new Vector2(float.NaN,14);replay.Frames[0]=frame;bool refused=false;
            try{ReplayCodec.Write(Path.Combine(directory,"invalid.jkr"),replay);}catch(InvalidDataException){refused=true;}
            Assert(refused,"Replay geometry rejects invalid coordinates");
        } finally {Directory.Delete(directory,true);}
    }
    private static void TestVerificationBridge()
    {
        var source=SampleReplay("verification-source");string received=null;int calls=0;
        Action<string,string,string,double,double> handler=delegate(string binding,string id,string path,double start,double end){received=binding;calls++;Assert(end>=start,"verification coverage ordered");};
        VerificationBridge.Saved+=handler;
        try
        {
            VerificationBridge.BindRun("run-a:session-a");VerificationBridge.Associate(source);
            var snapshot=ReplaySnapshot.Create(source);VerificationBridge.BindRun("run-b:session-b");
            var summary=new ReplaySummary {Header=snapshot.Header,FilePath="fixture.jkr"};
            VerificationBridge.Published(summary);Assert(received=="run-a:session-a"&&calls==1,"snapshot retains capture association across a later run");
            VerificationBridge.Published(summary);Assert(calls==1,"save receipt published once");
            VerificationBridge.Forget(source);VerificationBridge.Published(new ReplaySummary {Header=source.Header,FilePath="fixture.jkr"});Assert(calls==1,"discarded recordings do not attach to a run");
        }
        finally { VerificationBridge.Saved-=handler;VerificationBridge.BindRun(null); }
    }

    private static void TestPreparedWorld()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        string root = Path.Combine(Path.GetTempPath(), "replays-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var instance = typeof(Game1).GetField("_instance", flags); object previousGame = instance.GetValue(null);
        var screens = typeof(JumpKing.Level.LevelManager).GetField("m_screens", flags); object previousScreens = screens.GetValue(null);
        var game = (Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Game1)); GC.SuppressFinalize(game);
        var contentField = typeof(Game1).GetField("contentManager", flags);
        var content = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(contentField.FieldType);
        contentField.SetValue(game, content); contentField.FieldType.GetField("root", flags).SetValue(content, root);
        try
        {
            instance.SetValue(null, game); screens.SetValue(null, new JumpKing.Level.LevelScreen[3]);
            File.WriteAllBytes(Path.Combine(root, "level.xnb"), new byte[] { 1, 2, 3 });
            string first;
            using (var attempt = new JKRuntime.RuntimeScope())
            {
                ReplayRuntime.Prepare(attempt); first = ReplayRuntime.WorldForStart().Revision;
                File.WriteAllBytes(Path.Combine(root, "level.xnb"), new byte[] { 4, 5, 6 });
                Assert(ReplayRuntime.WorldForStart().Revision == first, "handoff reuses the intro's exact world digest");
            }
            using (var attempt = new JKRuntime.RuntimeScope())
            {
                ReplayRuntime.Prepare(attempt);
                Assert(ReplayRuntime.WorldForStart().Revision != first, "next attempt rehashes changed map assets, even with equal lengths");
            }
            File.WriteAllBytes(Path.Combine(root, "level.xnb"), new byte[] { 1, 2, 3 });
            Assert(ReplayRuntime.WorldForStart().Revision == first, "cancelled preparation cannot supply a stale identity");
        }
        finally { instance.SetValue(null, previousGame); screens.SetValue(null, previousScreens); File.Delete(Path.Combine(root, "level.xnb")); Directory.Delete(root); }
    }

    private static void TestPresentationTimeline()
    {
        var body = (JumpKing.Player.BodyComp)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.BodyComp));
        var other = (JumpKing.Player.BodyComp)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.BodyComp));
        body.Enabled = false;
        Assert(!ReplayRecorder.ShouldRecordBody(body), "unowned disabled body is not recorded");
        var first = JKRuntime.Gameplay.PresentationActivity.Begin("test.warp", body);
        var second = JKRuntime.Gameplay.PresentationActivity.Begin("test.effect", body);
        int frames = 0;
        for (int tick = 0; tick < 15; tick++) if (ReplayRecorder.ShouldRecordBody(body)) frames++;
        Assert(frames == 15, "warp presentation ticks retain recording duration");
        Assert(!JKRuntime.Gameplay.PresentationActivity.IsActive(other), "activity is actor-local");
        first.Dispose(); first.Dispose();
        Assert(ReplayRecorder.ShouldRecordBody(body), "one owner cannot clear another presentation");
        second.Dispose();
        Assert(!ReplayRecorder.ShouldRecordBody(body) && !body.Enabled, "release neither leaks nor enables physics");
        body.Enabled = true;
        Assert(ReplayRecorder.ShouldRecordBody(body), "ordinary physics recording retained");
    }

    private static void TestCodecRoundTrip()
    {
        string directory = NewDirectory();
        try
        {
            string path = Path.Combine(directory, "roundtrip.jkr");
            ReplayData source = SampleReplay("roundtrip");
            ReplayCodec.Write(path, source);
            ReplayData loaded = ReplayCodec.Read(path);
            Assert(loaded.Header.Id == "roundtrip", "replay id round-trips");
            Assert(loaded.Header.WorldKey == "level:test|author|2", "world identity round-trips");
            Assert(loaded.Header.WorldRevision == "revision", "world revision round-trips");
            Assert(loaded.Header.InitialGameTicks == 4321, "initial game clock round-trips");
            Assert(
                Math.Abs(loaded.Header.InitialGameTime - 0.125f) < 0.0001f,
                "sub-frame game clock round-trips");
            Assert(loaded.Frames.Count == 2, "frame count round-trips");
            AssertNear(12.5f, loaded.Frames[1].Position.X, "position round-trips");
            Assert(
                loaded.Frames[1].VisualOffset == new Vector2(0.5f, -0.5f),
                "visual anchor correction round-trips");
            Assert(
                loaded.AppearanceEvents.Count == 2,
                "appearance events round-trip");
            Assert(
                loaded.AppearanceEvents[1].Items.Length == 2,
                "equipped item set round-trips");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void TestCodecRejectsInvalidFrame()
    {
        string directory = NewDirectory();
        try
        {
            ReplayData source = SampleReplay("invalid-frame");
            ReplayFrame frame = source.Frames[0];
            frame.Position.X = float.NaN;
            source.Frames[0] = frame;
            bool rejected = false;
            try { ReplayCodec.Write(Path.Combine(directory, "invalid.jkr"), source); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "codec rejects non-finite positions");

            source = SampleReplay("invalid-offset");
            frame = source.Frames[0];
            frame.VisualOffset.Y = float.NaN;
            source.Frames[0] = frame;
            rejected = false;
            try { ReplayCodec.Write(Path.Combine(directory, "invalid-offset.jkr"), source); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "codec rejects non-finite visual offsets");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void TestPixelSnapping()
    {
        Sprite sprite = (Sprite)Activator.CreateInstance(
            typeof(Sprite),
            true);
        sprite.source = new Rectangle(0, 0, 19, 27);
        sprite.center = new Vector2(0.5f, 0.5f);
        Vector2 topLeft = ReplayGhostRenderer.SnappedTopLeft(
            sprite,
            new Vector2(100.25f, 200.25f));
        Assert(
            topLeft == new Vector2(90f, 186f),
            "ghost rendering uses vanilla post-origin pixel snapping");
    }

    private static void TestLayeredPoseRecognition()
    {
        Sprite baseSprite = (Sprite)Activator.CreateInstance(
            typeof(Sprite),
            true);
        Sprite equipmentSprite = (Sprite)Activator.CreateInstance(
            typeof(Sprite),
            true);
        Type layeredType = typeof(Game1).Assembly.GetType(
            "JumpKing.XnaWrappers.LayeredSprite",
            true);
        Sprite layered = layeredType.GetConstructor(new[]
        {
            typeof(Sprite),
            typeof(Sprite[])
        }).Invoke(new object[]
        {
            baseSprite,
            new[] { equipmentSprite }
        }) as Sprite;
        Assert(
            ReplayFrameCapture.SamePoseVisual(baseSprite, layered),
            "pose recognition compares base layers symmetrically");
    }

    private static void TestRepositoryLifecycle()
    {
        string directory = NewDirectory();
        try
        {
            ReplayRepository repository = new ReplayRepository(directory);
            ReplaySummary saved = repository.Save(SampleReplay("stored"));
            Assert(saved != null, "repository saves replay");
            Assert(repository.List().Count == 1, "repository lists replay");
            Assert(repository.Load("stored") != null, "repository loads replay by id");
            Assert(repository.Delete("stored"), "repository deletes replay");
            Assert(repository.List().Count == 0, "deleted replay leaves library");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void TestRepositoryInvalidationRace()
    {
        string directory = NewDirectory();
        using (var scanned = new System.Threading.ManualResetEvent(false))
        using (var release = new System.Threading.ManualResetEvent(false))
        {
            int scans = 0;
            var repository = new ReplayRepository(directory, delegate {
                string[] files = Directory.GetFiles(directory, "*.jkr");
                if (System.Threading.Interlocked.Increment(ref scans) == 1) { scanned.Set(); release.WaitOne(); }
                return files;
            });
            IList<ReplaySummary> result = null;
            Exception error = null;
            var thread = new System.Threading.Thread(delegate() { try { result = repository.List(); } catch (Exception failure) { error = failure; } });
            try
            {
                thread.Start(); Assert(scanned.WaitOne(3000), "Directory snapshot is held before publication");
                repository.Save(SampleReplay("racing-save"));
                release.Set(); Assert(thread.Join(3000), "Invalidated scan finishes without deadlock");
                Assert(error == null && result != null && result.Count == 1 && repository.List().Count == 1 && scans == 2,
                    "A scan cannot republish stale data after a completed save invalidates it");
                ReplayCodec.Write(Path.Combine(directory, "import.jkr"), SampleReplay("imported"));
                repository.Refresh(); Assert(repository.List().Count == 2, "Explicit refresh discovers externally imported files");
            }
            finally { release.Set(); thread.Join(3000); Directory.Delete(directory, true); }
        }
    }

    private static void TestBackgroundSaveResults()
    {
        string directory = NewDirectory();
        using (var started = new System.Threading.ManualResetEvent(false))
        using (var release = new System.Threading.ManualResetEvent(false))
        {
            int writes = 0;
            var repository = new ReplayRepository(directory, null, delegate(string path, ReplayData data) {
                if (System.Threading.Interlocked.Increment(ref writes) == 1)
                { started.Set(); release.WaitOne(); throw new IOException("Injected write failure"); }
                ReplayCodec.Write(path, data);
            });
            try
            {
                Assert(repository.QueueSave(SampleReplay("failed-first")), "Queue accepts a recording");
                var first = repository.LastSave;
                Assert(started.WaitOne(3000) && !first.IsCompleted, "Queued saves are not reported as durable success");
                Assert(repository.QueueSave(SampleReplay("second")), "A second bounded save is accepted");
                int copies = 0;
                Assert(!repository.QueueSnapshot(delegate { copies++; return SampleReplay("rejected"); }) && copies == 0,
                    "A full save budget rejects before copying frame buffers");
                Assert(repository.QueueCompleted(SampleReplay("completed")) && !repository.CanStartRecording,
                    "A completed run retains its reserved slot while further recording is paused");
                Assert(!repository.QueueCompleted(SampleReplay("overflow")), "Completion backpressure remains bounded");
                release.Set(); Assert(repository.Drain(5000), "Background saves drain");
                Assert(first.State == JKRuntime.BackgroundWorkState.Failed && repository.SaveStatus.Contains("FAILED"), "Write errors remain visible and retryable");
                Assert(repository.RetryFailed() && repository.Drain(5000) && repository.LastSave.State == JKRuntime.BackgroundWorkState.Succeeded,
                    "The original failed snapshot can be retried without recapturing a different run");
                Assert(repository.CanStartRecording && repository.List().Count == 3 && Directory.GetFiles(directory, "*.tmp*").Length == 0, "Only complete durable files enter the library and temporary files are cleaned");
            }
            finally { release.Set(); repository.Drain(5000); Directory.Delete(directory, true); }
        }
    }

    private static void TestRecorderOwnershipAndLimit()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var player = (JumpKing.Player.PlayerEntity)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.Player.PlayerEntity));
        typeof(EntityComponent.Entity).GetField("m_components", flags).SetValue(player, new List<EntityComponent.Component>());
        for (int i = 0; i < 25; i++)
        {
            var recorder = (ReplayRecorder)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ReplayRecorder));
            typeof(ReplayRecorder).GetField("replay", flags).SetValue(recorder, SampleReplay("discarded"));
            recorder.Attach(player);
            Assert(player.GetComponents().Length == 1, "Capture component is owned by the active recorder");
            recorder.Discard();
            Assert(player.GetComponents().Length == 0 && typeof(ReplayRecorder).GetField("replay", flags).GetValue(recorder) == null,
                "Discard removes the native reference and releases the recording buffer");
        }
        var data = SampleReplay("bounded");
        var frames = new List<ReplayFrame>(2);
        Assert(ReplayRecorder.AppendFrame(frames, data.Frames[0], 2) && ReplayRecorder.AppendFrame(frames, data.Frames[1], 2)
            && !ReplayRecorder.AppendFrame(frames, data.Frames[0], 2) && frames.Count == 2 && frames.Capacity == 2,
            "Capture stops before exceeding its format/memory limit and retains the final valid frames");
    }

    private static void TestTimeline()
    {
        ReplayTimeline timeline = new ReplayTimeline(121);
        timeline.Update(1f);
        Assert(timeline.Index == 60, "normalized simulation seconds advance sixty frames");
        timeline.Seek(-600);
        Assert(timeline.Index == 0, "rewind clamps to first frame");
        timeline.Seek(600);
        Assert(timeline.Index == 120, "fast-forward clamps to last frame");
        Assert(!timeline.Playing, "timeline pauses at replay end");
        timeline.Toggle();
        Assert(timeline.Playing, "playback can resume after seeking");
    }

    private static void TestAttemptStamp()
    {
        JKRuntime.State.AttemptStamp first = new JKRuntime.State.AttemptStamp
        {
            Session = 7,
            Attempt = 2
        };
        JKRuntime.State.AttemptStamp same = new JKRuntime.State.AttemptStamp
        {
            Session = 7,
            Attempt = 2
        };
        JKRuntime.State.AttemptStamp next = new JKRuntime.State.AttemptStamp
        {
            Session = 7,
            Attempt = 3
        };
        Assert(first.Equals(same), "same attempt keeps one recording");
        Assert(!first.Equals(next), "new attempt rotates the recording");
    }

    private static void TestAttemptClockContract()
    {
        try
        {
            JKRuntime.State.GameClock.ValidateContract();
            Assert(true, "installed game exposes attempt state");
        }
        catch (Exception error)
        {
            Assert(
                false,
                "installed game exposes attempt state: "
                + error.GetType().Name);
        }
    }

    private static void TestVictoryClockContract()
    {
        try
        {
            JKRuntime.State.GameClock.ValidateContract();
            Assert(true, "installed game exposes victory state");
        }
        catch (Exception error)
        {
            Assert(
                false,
                "installed game exposes victory state: "
                + error.GetType().Name);
        }
    }

    private static void TestGameClockContract()
    {
        try
        {
            JKRuntime.State.GameClock.ValidateContract();
            Assert(true, "installed game exposes the wind-driving game clock");
        }
        catch (Exception error)
        {
            Assert(false, "installed game exposes the wind-driving game clock: "
                + error.GetType().Name);
        }
    }

    private static void TestVictoryIsolationContract()
    {
        try
        {
            ReplayVictoryIsolation.ValidateContract();
            Assert(true, "installed game exposes ground state for replay isolation");
        }
        catch (Exception error)
        {
            Assert(
                false,
                "installed game exposes ground state for replay isolation: "
                + error.GetType().Name);
        }
    }

    private static void TestAppearanceContract()
    {
        try
        {
            ReplayAppearanceTrack.ValidateContract();
            ReplayAppearanceSprites.ValidateContract();
            ReplayAppearanceTrack.CurrentVersion();
            Assert(
                ReplayAppearanceTrack.Capture() != null,
                "equipped appearance can be sampled");
            Assert(true, "installed game exposes equipped appearance state");
        }
        catch (Exception error)
        {
            Assert(
                false,
                "installed game exposes equipped appearance state: "
                + error.GetType().Name);
        }
    }

    private static void TestCameraTarget()
    {
        var data = SampleReplay("camera");
        data.Frames[0] = new ReplayFrame { Position = new Vector2(200, 300), Screen = 1 };
        data.Frames[1] = new ReplayFrame { Position = new Vector2(202, 299), Screen = 1 };
        JKRuntime.Presentation.CameraTarget target;
        Assert(!JKRuntime.Presentation.CameraTargets.TryRead(out target), "Recording data alone does not own the camera");
        using (var scope = new JKRuntime.RuntimeScope())
        {
            var camera = new ReplayCameraTarget(data, scope);
            camera.Present(0, true, false);
            JKRuntime.Presentation.CameraTargets.TryRead(out target);
            Assert(target.Center == new Vector2(209, 313) && target.Screen == 0 && !target.Paused,
                "Viewer publishes world center and zero-based recorded screen");
            long cut = target.Discontinuity;
            camera.Present(1, true, false);
            JKRuntime.Presentation.CameraTargets.TryRead(out target);
            Assert(target.Velocity == new Vector2(120, -60) && target.Discontinuity == cut,
                "Playback supplies camera velocity in pixels per second");
            camera.Present(1, true, false);
            JKRuntime.Presentation.CameraTargets.TryRead(out target);
            Assert(target.Velocity == new Vector2(120, -60), "Repeated presentation frames retain recorded velocity");
            camera.Present(1, false, false);
            JKRuntime.Presentation.CameraTargets.TryRead(out target);
            Assert(target.Paused && target.Discontinuity == cut, "Pausing the viewer preserves camera continuity");
            camera.Present(1, false, true);
            JKRuntime.Presentation.CameraTargets.TryRead(out target);
            Assert(target.Paused && target.Discontinuity > cut, "Seeking to the same paused frame resets camera framing");
            cut = target.Discontinuity;
            camera.Present(0, true, false);
            JKRuntime.Presentation.CameraTargets.TryRead(out target);
            Assert(target.Discontinuity > cut && target.Velocity == Vector2.Zero, "Backward playback cannot retain forward velocity");
        }
        Assert(!JKRuntime.Presentation.CameraTargets.TryRead(out target), "Closing a viewer releases its camera target");
        var timeline = new ReplayTimeline(200);
        long seek = timeline.SeekRevision;
        timeline.Update(1f / 60);
        Assert(timeline.SeekRevision == seek, "Ordinary playback does not mark a camera cut");
        timeline.Seek(1);
        Assert(timeline.SeekRevision != seek, "Even a one-frame seek marks a camera cut");
    }

    private static void TestAppearanceTimeline()
    {
        ReplayData replay = SampleReplay("appearance");
        Assert(
            ReplayAppearanceTrack.AtFrame(replay, 0).Length == 1,
            "initial equipped state applies at frame zero");
        Assert(
            ReplayAppearanceTrack.AtFrame(replay, 1).Length == 2,
            "later equipped state applies at its event frame");
    }

    private static void TestSettingsDefaultsAndSerialization()
    {
        ReplaySettings settings = new ReplaySettings();
        Assert(
            settings.ReplaysInMainMenu,
            "Replays section starts in the root main menu");
        Assert(
            settings.SaveReplayInPauseMenu,
            "manual replay saving starts in the pause menu");
        settings.ReplaysInMainMenu = false;
        settings.SaveReplayInPauseMenu = false;
        XmlSerializer serializer = new XmlSerializer(typeof(ReplaySettings));
        string xml;
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, settings);
            xml = writer.ToString();
        }
        ReplaySettings restored;
        using (StringReader reader = new StringReader(xml))
            restored = (ReplaySettings)serializer.Deserialize(reader);
        Assert(
            !restored.ReplaysInMainMenu,
            "main-menu placement setting persists");
        Assert(
            !restored.SaveReplayInPauseMenu,
            "pause-menu save setting persists");
    }

    private static void TestSnapshotIsolation()
    {
        ReplayData source = SampleReplay("live-recording");
        ReplayData snapshot = ReplaySnapshot.Create(source);
        Assert(snapshot != null, "recording snapshot is created");
        Assert(
            snapshot.Header.Id != source.Header.Id,
            "each saved snapshot receives a unique id");
        Assert(
            snapshot.Frames.Count == source.Frames.Count,
            "snapshot copies every recorded frame");
        source.Frames.Clear();
        source.AppearanceEvents[0].Items[0] = 99;
        Assert(
            snapshot.Frames.Count == 2,
            "saved snapshot does not share the live frame list");
        Assert(
            snapshot.AppearanceEvents[0].Items[0] == 0,
            "saved snapshot does not share equipped item arrays");
    }

    private sealed class AudioSink : IReplayAudioOutput
    {
        internal readonly List<ReplaySound> Sounds = new List<ReplaySound>();
        internal int Stops, Pauses, Resumes;
        public void Play(ReplaySound value) { Sounds.Add(value); }
        public void Stop() { Stops++; }
        public void Pause(bool paused) { if (paused) Pauses++; else Resumes++; }
    }

    private static void TestAudioAndTiming()
    {
        var header = new ReplayHeader { FrameCount = 10372 };
        Assert(new ReplaySummary { Header = header }.Duration == TimeSpan.FromMilliseconds(176324),
            "Restless Knight's 10372 native ticks display 2:56, not 2:52");
        Assert(ReplayTiming.SeekFrames(header, 1) == 59 && ReplayTiming.SeekFrames(header, -10) == -588,
            "seek distances use IGT seconds");
        var timeline = new ReplayTimeline(100);
        timeline.Update(1f / 60f);
        Assert(timeline.Index == 1, "IGT correction does not slow native playback");
        Assert(ReplayTiming.Format(TimeSpan.FromMilliseconds(176358), true) == "2:56.358", "completion preserves native milliseconds");
        Assert(ReplayAudio.Infer(ReplayPose.Charge, ReplayPose.JumpFall) == ReplaySound.Jump,
            "legacy short jumps under a ceiling still make a jump sound");

        string directory = NewDirectory();
        try
        {
            var data = SampleReplay("audio-clock");
            data.Header.TickDuration = 170000;
            data.Header.CompletionTime = TimeSpan.FromMilliseconds(176358).Ticks;
            data.SoundEvents = new List<ReplaySoundEvent> {
                new ReplaySoundEvent { Frame = 1, Sound = ReplaySound.IronLand },
                new ReplaySoundEvent { Frame = 1, Sound = ReplaySound.SnowLand }
            };
            string path = Path.Combine(directory, "audio.jkr");
            ReplayCodec.Write(path, data);
            var read = ReplayCodec.Read(path);
            var summary = ReplayCodec.ReadSummary(path);
            Assert(read.SoundEvents.Count == 2 && read.SoundEvents[1].Sound == ReplaySound.SnowLand, "simultaneous surface sounds round-trip");
            Assert(summary.Header.CompletionTime == data.Header.CompletionTime && read.Header.TickDuration == 170000, "summary retains authoritative clock metadata");
            var copy = ReplaySnapshot.Create(data);
            data.SoundEvents[0].Sound = ReplaySound.Jump;
            data.Header.TickDuration = 200000;
            Assert(copy.SoundEvents[0].Sound == ReplaySound.IronLand && copy.Header.TickDuration == 170000
                && copy.Header.CompletionTime == data.Header.CompletionTime, "manual snapshot owns sound and clock data");
            data.SoundEvents[0].Sound = (ReplaySound)255;
            bool rejected = false;
            try { ReplayCodec.Write(Path.Combine(directory, "bad-sound.jkr"), data); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "unknown audio IDs are rejected");
            data.SoundEvents.Clear(); data.Header.TickDuration = 0; rejected = false;
            try { ReplayCodec.Write(Path.Combine(directory, "bad-clock.jkr"), data); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "invalid clock cadence is rejected");

            var sink = new AudioSink();
            using (var player = new ReplayAudioPlayback(read, sink))
            {
                player.Present(0, true, true);
                player.Present(1, true, false);
                player.Present(1, true, false);
                Assert(sink.Sounds.Count == 2, "normal playback emits each sound exactly once");
                player.Present(1, false, false); player.Present(1, false, false); player.Present(1, true, false);
                Assert(sink.Pauses == 1 && sink.Resumes == 1 && sink.Sounds.Count == 2, "pause resumes voices without replaying events");
                player.Present(0, true, true); player.Present(1, true, true);
                Assert(sink.Sounds.Count == 2, "short forward and backward seeks are silent");
                player.Present(0, false, true); player.Present(0, true, false); player.Present(1, true, false);
                Assert(sink.Sounds.Count == 4, "playing again after a seek replays future events");
            }
            Assert(sink.Stops >= 5, "seek and disposal stop borrowed voices");
            sink = new AudioSink();
            using (var player = new ReplayAudioPlayback(read, sink))
            {
                player.Present(0, true, true); player.Present(1, false, false); player.Present(1, false, false);
                Assert(sink.Sounds.Count == 2 && sink.Pauses == 0, "natural completion plays final sounds and lets their tails finish");
            }
            read.SoundEvents = null;
            var charge = read.Frames[0]; charge.Pose = ReplayPose.Charge; read.Frames[0] = charge;
            sink = new AudioSink();
            using (var player = new ReplayAudioPlayback(read, sink)) { player.Present(0, true, true); player.Present(1, true, false); }
            Assert(sink.Sounds.Count == 1 && sink.Sounds[0] == ReplaySound.Jump, "legacy pose changes infer basic jump audio");
            read.SoundEvents = new List<ReplaySoundEvent>(); sink = new AudioSink();
            using (var player = new ReplayAudioPlayback(read, sink)) { player.Present(0, true, true); player.Present(1, true, false); }
            Assert(sink.Sounds.Count == 0, "recorded silence never invokes the legacy guess");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static bool SkipNativeSound() { return false; }

    private static void TestNativeAudioHook()
    {
        const System.Reflection.BindingFlags flags = JKRuntime.OwnedPatches.Members;
        System.Reflection.Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"));
        var instance = typeof(Game1).GetField("_instance", flags); object old = instance.GetValue(null);
        var game = (Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Game1)); GC.SuppressFinalize(game);
        var field = typeof(Game1).GetField("contentManager", flags);
        var content = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(field.FieldType); field.SetValue(game, content);
        var audioField = field.FieldType.GetField("audio", flags);
        var audio = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(audioField.FieldType); audioField.SetValue(content, audio);
        var playerField = audioField.FieldType.GetField("player", flags);
        var player = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(playerField.FieldType); playerField.SetValue(audio, player);
        var sound = (JumpKing.XnaWrappers.OneShotSound)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.XnaWrappers.OneShotSound));
        playerField.FieldType.GetField("WaterLand", flags).SetValue(player, sound);
        int calls = 0; ReplaySound received = ReplaySound.Jump;
        Action<ReplaySound> handler = value => { calls++; received = value; };
        ReplayAudio.Played += handler;
        try
        {
            instance.SetValue(null, game);
            using (var mute = new JKRuntime.OwnedPatches("tests.replay.mute"))
            {
                var target = typeof(JumpKing.XnaWrappers.OneShotSound).GetMethod("PlayOneShot");
                mute.Add(target, prefix: typeof(ReplaysTests).GetMethod("SkipNativeSound", flags));
                using (var world = new JKRuntime.RuntimeScope())
                {
                    ReplayAudio.Prepare(world); sound.PlayOneShot();
                    Assert(calls == 1 && received == ReplaySound.WaterLand, "real native method dispatch records the selected surface sound");
                    var foreign = (JumpKing.XnaWrappers.OneShotSound)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(JumpKing.XnaWrappers.OneShotSound));
                    foreign.PlayOneShot(); Assert(calls == 1, "unrelated menu and mod sounds are not recorded as player sounds");
                }
                sound.PlayOneShot(); Assert(calls == 1, "world teardown removes only the replay observer");
            }
        }
        finally { ReplayAudio.Played -= handler; instance.SetValue(null, old); }
    }

    private static void TestCompletionSaving()
    {
        const System.Reflection.BindingFlags flags = JKRuntime.OwnedPatches.Members;
        var managerType = typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager", true);
        var instance = managerType.GetField("instance", flags); object previous = instance.GetValue(null);
        var manager = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(managerType);
        var winField = managerType.GetField("m_win_stats", flags);
        var win = Activator.CreateInstance(winField.FieldType);
        winField.FieldType.GetField("_ticks", flags).SetValue(win, 10374); winField.SetValue(manager, win);
        string directory = NewDirectory();
        var repository = new ReplayRepository(directory);
        try
        {
            instance.SetValue(null, manager);
            var source = SampleReplay("completed-clock"); source.Header.InitialGameTicks = 1; source.Header.InitialGameTime = 0;
            var recorder = (ReplayRecorder)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ReplayRecorder));
            typeof(ReplayRecorder).GetField("repository", flags).SetValue(recorder, repository);
            typeof(ReplayRecorder).GetField("replay", flags).SetValue(recorder, source);
            Assert(recorder.SaveSnapshot() && repository.Drain(5000), "manual snapshot is durably saved before completion");
            Assert(!repository.List()[0].Header.CompletionTime.HasValue, "unfinished snapshot does not invent a result");
            Assert(recorder.SaveCompletedRun() && repository.Drain(5000), "completed recording is durably saved");
            Assert(repository.Load(source.Header.Id).Header.CompletionTime == TimeSpan.FromMilliseconds(176358).Ticks,
                "completion saving uses the victory clock after current attempt reset, not frame count");
            Assert(!recorder.SaveCompletedRun(), "completion transfers only once");
        }
        finally { repository.Drain(5000); instance.SetValue(null, previous); Directory.Delete(directory, true); }
    }

    private static ReplayData SampleReplay(string id)
    {
        return new ReplayData
        {
            Header = new ReplayHeader
            {
                Id = id,
                WorldKey = "level:test|author|2",
                WorldName = "Test World",
                WorldAuthor = "Author",
                WorldRevision = "revision",
                GameVersion = "1.0.0.0",
                ReplaysVersion = "1.0.0.0",
                CreatedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                TotalScreens = 2,
                InitialGameTicks = 4321,
                InitialGameTime = 0.125f
            },
            AppearanceEvents = new List<ReplayAppearanceEvent>
            {
                new ReplayAppearanceEvent
                {
                    Frame = 0,
                    Items = new[] { 0 }
                },
                new ReplayAppearanceEvent
                {
                    Frame = 1,
                    Items = new[] { 0, 10 }
                }
            },
            Frames = new List<ReplayFrame>
            {
                new ReplayFrame
                {
                    Position = new Vector2(10f, 20f),
                    VisualOffset = Vector2.Zero,
                    Screen = 1,
                    Flags = ReplayFrameFlags.None,
                    Pose = ReplayPose.Idle
                },
                new ReplayFrame
                {
                    Position = new Vector2(12.5f, 18f),
                    VisualOffset = new Vector2(0.5f, -0.5f),
                    Screen = 1,
                    Flags = ReplayFrameFlags.FacingLeft,
                    Pose = ReplayPose.JumpUp
                }
            }
        };
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "replays-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Assert(bool value, string name)
    {
        if (value) return;
        failures++;
        Console.Error.WriteLine("[FAIL] " + name);
    }

    private static void AssertNear(float expected, float actual, string name)
    {
        Assert(Math.Abs(expected - actual) < 0.0001f, name);
    }
}
