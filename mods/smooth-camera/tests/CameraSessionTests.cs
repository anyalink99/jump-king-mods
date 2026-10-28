using System;
using System.Linq;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static SubjectSample Subject(float x, float y, int screen)
    { return new SubjectSample { Center = new Vector2(x, y), Screen = screen }; }

    private static void CameraSessionTests()
    {
        SessionNeighbor();
        SessionRulesAndBounds();
        foreach (bool right in new[] { false, true }) SessionCrossing(right);
        SessionTimeline();
        SessionTimelineStop();
        SessionMixedCadence();
        SessionExpansion();
        PrimitivePreviewRoutes();
        Console.WriteLine("[OK] Complete camera sessions: early neighboring views, equal rules, bounded arrivals, Focus returns, shared presentation timeline and preview routes");
    }
    private static void SessionNeighbor()
    {
        var screens = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2, 1), PortalScreen(3) };
        var options = new CameraSettings { Horizontal = true };
        var rules = Rules("<SmoothCamera version='2'><Screens from='4' mode='native'/></SmoothCamera>", 4);
        var camera = new CameraSession(); var subject = Subject(430, -20, 1);
        camera.Observe(subject, screens, options, rules);
        camera.Rig.Vertical.Place(180); camera.Rig.View.Place(0, 180, subject.Center.Y);
        for (int i = 0; i < 240; i++) { camera.Observe(subject, screens, options, rules); camera.Advance(1f / 120, CameraMode.Normal); }
        Check(camera.Pose.X < -65, "Complete session anticipates an offset portal on the neighboring screen");
        Check(camera.Images.Any(image => image.Screen == 2 && image.Offset.X == 480), "Bounded destination contributes its valid visible fragment");
        Check(!camera.Images.Any(image => image.Screen == 3), "Render plan excludes forbidden destination neighbor");
        Near(camera.Pose.Y, 180 - subject.Center.Y - options.Vertical.NegativeBand, "Early horizontal anticipation preserves the ordinary vertical band");
    }
    private static void SessionRulesAndBounds()
    {
        var screens = new[] { PortalScreen(0, 4), PortalScreen(1), PortalScreen(2), PortalScreen(3) };
        var options = new CameraSettings { Horizontal = true };
        var rules = Rules("<SmoothCamera version='2'><Screens from='1' mode='smooth'/><Screens from='2' mode='smooth'/><Screens from='3' mode='native'/><Screens from='4' mode='smooth'/></SmoothCamera>", 4);
        var camera = new CameraSession();
        camera.Observe(Subject(240, 2, 0), screens, options, rules); camera.Advance(0, CameraMode.Normal);
        camera.Rig.Vertical.Place(100); camera.Rig.View.Place(0, 100, 2);
        camera.Observe(Subject(240, -2, 1), screens, options, rules); camera.Advance(0, CameraMode.Normal);
        Near(camera.Pose.Y, 100, "Equivalent XML rules do not cut a complete camera session");

        camera.Reset(); var before = Subject(479, 100, 0);
        camera.Observe(before, screens, options, rules);
        camera.Rig.Vertical.Place(100); camera.Rig.View.Place(-210, 100, 100);
        for (int i = 0; i < 240; i++) { camera.Observe(before, screens, options, rules); camera.Advance(1f / 120, CameraMode.Normal); }
        Vector2 visible = before.Center + camera.Pose;
        var after = Subject(1, -980, 3);
        camera.Observe(after, screens, options, rules); camera.Advance(0, CameraMode.Normal);
        Near(after.Center.Y + camera.Pose.Y, visible.Y, "Short destination stack preserves vertical pose at the crossing");
        Near(after.Center.X + camera.Pose.X, visible.X + 2, "Short destination stack preserves horizontal pose at the crossing");
        Check(camera.Images.Any(image => image.Screen == 0 && image.Offset.Y == -1080), "Departure fragment uses the crossing's coordinate chart");
        float last = camera.Pose.Y; camera.Advance(1f / 240, CameraMode.Normal);
        Check(Math.Abs(camera.Pose.Y - last) < 1, "New destination bounds settle instead of clamping in one frame");
        for (int i = 0; i < 480; i++) { camera.Observe(after, screens, options, rules); camera.Advance(1f / 240, CameraMode.Normal); }
        Near(camera.Pose.Y, 1080, "Arrival eventually respects the destination's native vertical bounds");

        foreach (var mode in new[] { CameraMode.Up, CameraMode.Down })
        {
            camera.Reset(); camera.Observe(before, screens, options, rules);
            camera.Rig.Vertical.Place(100); camera.Rig.View.Place(-210, 100, 100);
            for (int frame = 0; frame < 240; frame++) camera.Advance(1f / 120, mode);
            Vector2 viewport = before.Center + camera.Pose;
            camera.Observe(after, screens, options, rules); camera.Advance(1f / 240, mode);
            Check(Vector2.Distance(after.Center + camera.Pose, viewport + new Vector2(2, 0)) < 3,
                "Held Look respects the shorter stack without a first-frame clamp");
        }

        after.Cut = after.Paused = true; after.Center = new Vector2(200, -600); after.Screen = 2;
        camera.Observe(after, screens, options, MapRules.Parse(null, 4, false, false)); camera.Advance(0, CameraMode.Normal);
        Near(camera.Pose.Y, 780, "Explicit paused seek initializes its own framing");
    }
    private static void SessionCrossing(bool right)
    {
        var screens = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2), PortalScreen(3) };
        var options = new CameraSettings { Horizontal = true };
        var rules = MapRules.Parse(null, 4, false, false);
        foreach (CameraMode mode in new[] { CameraMode.Normal, CameraMode.Focus, CameraMode.Up, CameraMode.Down })
        {
            var camera = new CameraSession(); var before = Subject(right ? 479 : 1, 100, 0);
            for (int i = 0; i < 240; i++) { camera.Observe(before, screens, options, rules); camera.Advance(1f / 120, CameraMode.Normal); }
            if (mode != CameraMode.Normal)
            {
                for (int i = 0; i < 60; i++) camera.Advance(1f / 120, mode);
                if (mode == CameraMode.Focus) camera.Advance(1f / 240, CameraMode.Normal);
            }
            CameraMode crossingMode = mode == CameraMode.Focus ? CameraMode.Normal : mode;
            Vector2 visible = before.Center + camera.Pose;
            var after = Subject(right ? 1 : 479, -620, 2);
            camera.Observe(after, screens, options, rules); camera.Advance(0, crossingMode);
            Near(after.Center.X + camera.Pose.X - visible.X, right ? 2 : -2, "Whole session retains C0 continuity on a one-way exit during " + mode);
            Near(after.Center.Y + camera.Pose.Y, visible.Y, "Whole session rebases vertical pose during " + mode);
            Vector2 landmark = new Vector2(right ? 470 : 10, 150);
            Vector2 sourceProjection = landmark + visible - before.Center;
            var departure = camera.Images.Single(image => image.Screen == 0 && image.Offset.X != 0);
            Vector2 projected = landmark + departure.Offset + camera.Pose;
            Near(projected.X, sourceProjection.X, "Departure landmark remains fixed at crossing");
            Near(projected.Y, sourceProjection.Y, "Departure landmark keeps its vertical chart");
            Vector2 last = after.Center + camera.Pose; camera.Advance(1f / 240, crossingMode);
            Check(Vector2.Distance(after.Center + camera.Pose, last) < 3, "First post-crossing presentation has no Focus-return jump");
            if (mode == CameraMode.Normal)
            {
                for (int i = 0; i < 480; i++) { camera.Observe(after, screens, options, rules); camera.Advance(1f / 240, CameraMode.Normal); }
                Check(after.Center.X + camera.Pose.X > 210 && after.Center.X + camera.Pose.X < 270,
                    "One-way arrival keeps useful framing while the subject is stationary");
            }
        }
    }
    private static void SessionTimeline()
    {
        foreach (int subframes in new[] { 1, 2, 4 })
        {
            var screens = new[] { PortalScreen(0, 1) };
            var options = new CameraSettings { Horizontal = true };
            options.HorizontalAxis.Mode = FollowMode.Direct;
            options.HorizontalAxis.NegativeResponse = options.HorizontalAxis.PositiveResponse = 20;
            var rules = MapRules.Parse(null, 1, false, false);
            var camera = new CameraSession(); var timeline = new SubjectTimeline();
            float last = 0, largest = 0;
            for (int tick = 0; tick < 420; tick++)
            {
                var subject = Subject((100 + tick * 4) % 480, 180, 0); subject.Velocity.X = 240;
                timeline.Observe(subject, screens);
                camera.Observe(timeline.Sample(0, true), screens, options, rules);
                for (int frame = 0; frame < subframes; frame++)
                {
                    var sample = timeline.Sample(1f / (60 * subframes), true);
                    camera.Observe(sample, screens, options, rules); camera.Advance(1f / (60 * subframes), CameraMode.Normal);
                    float viewport = sample.Center.X + camera.Pose.X;
                    if (tick > 120) largest = Math.Max(largest, Math.Abs(viewport - last));
                    last = viewport;
                }
            }
            Console.WriteLine("[MOTION] Shared subject/camera at " + (60 * subframes) + " Hz: largest steady relative step " + largest);
            Check(largest < .08f, "Shared presentation timeline keeps relative framing stable through repeated self links");
        }
    }
    private static void SessionTimelineStop()
    {
        var screens = new[] { PortalScreen(0) }; var timeline = new SubjectTimeline();
        timeline.Observe(Subject(100, 180, 0), screens); timeline.Sample(0, true, 1);
        timeline.Observe(Subject(104, 180, 0), screens);
        Near(timeline.Sample(0, true, .5f).Center.X, 102, "Movement interpolates between native observations");
        timeline.Observe(Subject(104, 180, 0), screens, false);
        Near(timeline.Sample(0, true, .75f).Center.X, 103, "Repeated drawing keeps the current interpolation interval");
        timeline.Observe(Subject(104, 180, 0), screens);
        for (int frame = 0; frame < 4; frame++)
            Near(timeline.Sample(0, true, frame / 4f).Center.X, 104,
                "A stationary native tick consumes the last movement instead of replaying it");
    }
    private static void SessionExpansion()
    {
        if (expansionAssembly == null) return;
        foreach (bool right in new[] { false, true })
        {
            var screens = new[] { PortalScreen(0), PortalScreen(1), PortalScreen(2), PortalScreen(3) };
            SetWalls(screens[0], new[] { MultiWarp(new Rectangle(right ? 472 : 0, 0, 8, 360), 3) });
            var options = new CameraSettings { Horizontal = true };
            var rules = MapRules.Parse(null, 4, false, false); var camera = new CameraSession();
            var before = Subject(right ? 479 : 1, 100, 0);
            for (int i = 0; i < 240; i++) { camera.Observe(before, screens, options, rules); camera.Advance(1f / 120, CameraMode.Normal); }
            Vector2 pose = camera.Pose;
            var arrival = camera.Images.Single(image => image.Screen == 2);
            var after = Subject(before.Center.X + (right ? -479.8f : 479.8f), -620, 2);
            camera.Observe(after, screens, options, rules); camera.Advance(0, CameraMode.Normal);
            Near(after.Center.X + camera.Pose.X, before.Center.X + pose.X, "MultiWarp subject has no fractional crossing residual");
            Near(arrival.Offset.X + pose.X, camera.Pose.X, "MultiWarp destination scenery uses the exact body chart before crossing");
            Check(camera.Images.Count(image => image.Screen == 0 && image.Offset.X != 0) == 1,
                "MultiWarp departure has one image without a second rounded copy");
        }
    }
    private static void SessionMixedCadence()
    {
        foreach (int rate in new[] { 60, 120, 144, 240 })
        {
            var screens = new[] { PortalScreen(0, 1) }; var options = new CameraSettings { Horizontal = true };
            options.HorizontalAxis.Mode = FollowMode.Direct;
            var rules = MapRules.Parse(null, 1, false, false); var camera = new CameraSession(); var timeline = new SubjectTimeline();
            int native = -1; double time = 0; float last = 0, largest = 0;
            for (int frame = 0; frame < rate * 8; frame++)
            {
                float dt = 1f / rate; time += dt;
                int tick = (int)Math.Floor(time * 60 + .000001);
                if (tick != native)
                {
                    var subject = Subject((100 + tick * 4) % 480, 180, 0); subject.Velocity.X = 240;
                    timeline.Observe(subject, screens); native = tick;
                }
                var sample = timeline.Sample(dt, true, (float)(time * 60 - tick));
                camera.Observe(sample, screens, options, rules); camera.Advance(dt, CameraMode.Normal);
                float viewport = sample.Center.X + camera.Pose.X;
                if (time > 3) largest = Math.Max(largest, Math.Abs(viewport - last));
                last = viewport;
            }
            Check(largest < .08f, "Scheduler phase keeps full-session framing stable at nondivisible cadence " + rate);
        }
    }
    private static void PrimitivePreviewRoutes()
    {
        object nativeScreens = Renderer.Screens.GetValue(null);
        int nativeIndex = (int)RenderContext.NativeScreen.GetValue(null);
        using (var preview = new CameraPreview())
        {
            var session = (CameraSession)typeof(CameraPreview).GetField("session", Flags).GetValue(preview);
            var profile = new CameraProfile { Horizontal = true };
            for (int scene = 0; scene < PracticeWorld.Names.Length; scene++)
            {
                int crossings = 0; float highest = 360, lowest = -1440, previousX = 0; int previousScreen = -1;
                for (int tick = 0; tick < 580; tick++)
                {
                    preview.Update(profile, 1f / 60);
                    var sample = session.Subject;
                    if (tick > 0 && (sample.Screen != previousScreen || Math.Abs(sample.Center.X - previousX) > 400)) crossings++;
                    highest = Math.Min(highest, sample.Center.Y); lowest = Math.Max(lowest, sample.Center.Y);
                    previousScreen = sample.Screen; previousX = sample.Center.X;
                    Check(!float.IsNaN(session.Pose.X + session.Pose.Y), "Primitive route keeps a finite camera pose");
                }
                Check(crossings > 0, "Primitive preview actually crosses a screen seam in " + preview.ScenarioName);
                if (scene == 1) Check(highest < -360, "Vertical preview climbs across multiple screen boundaries");
                if (scene == 4) Check(lowest - highest > 900, "Long-fall preview falls through the world instead of landing immediately");
                preview.NextScenario();
            }
            preview.Paused = true; var frozen = session.Subject;
            preview.Update(profile, 1); Check(session.Subject.Center == frozen.Center, "Pause freezes primitive preview");
            preview.Restart(); Check(session.Images.Length > 0, "Restart while paused prepares a visible preview immediately");
        }
        Check(ReferenceEquals(nativeScreens, Renderer.Screens.GetValue(null)) && nativeIndex == (int)RenderContext.NativeScreen.GetValue(null),
            "Primitive preview never swaps native world globals");
    }
}
