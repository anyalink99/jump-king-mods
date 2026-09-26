using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static int checks;
    private static string mappingImplementation;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected, string message)
    { Check(Math.Abs(actual - expected) < 0.002f, message + ": " + actual + " != " + expected); }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            MotionTests();
            HorizontalTests();
            PortalOpeningTests();
            GestureTests();
            CameraViewTests();
            CameraBindingTests();
            StartupLifecycleTests();
            FastFallPresentation();
            int mappingArgument = Array.IndexOf(args, "--mapping");
            if (mappingArgument >= 0) mappingImplementation = args[mappingArgument + 1];
            Check(typeof(JumpKing.PauseMenu.BT.Actions.IToggle).IsAssignableFrom(typeof(CameraOption)), "Smooth Camera uses the native checkbox");
            Hooks.Install();
            try
            {
                NativeTests();
                CadenceTests();
                if (args.Contains("--graphics")) GraphicsTests(args[1]);
            }
            finally { Renderer.Stop(); Hooks.Uninstall(); }
            Check(!Hooks.Installed, "Hooks uninstalled");
            Check(!Harmony.GetAllPatchedMethods().Any(x => Harmony.GetPatchInfo(x).Owners.Contains(Hooks.Id)), "No camera patches survive unload");
            Settings.Load(); Settings.Current.Smooth = false;
            ModEntry.Start();
            Check(!Hooks.Installed, "Starting an attempt with camera disabled installs no camera patches");
            ModEntry.End(); Settings.Current.Smooth = true;
            Console.WriteLine("[OK] Smooth Camera: " + checks + " checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void MotionTests()
    {
        var motion = new CameraMotion();
        motion.Step(330, 5, 1f / 60, false); Near(motion.Translation, 0, "Bottom framing");
        motion.Step(100, 5, 1f / 60, false);
        Check(motion.Translation > 0 && motion.Translation < 80, "Smooth following has a finite delay");
        float before = motion.Translation;
        motion.Step(100, 5, 1f / 60, true); Near(motion.Translation, before, "Pause freezes motion");
        for (int i = 0; i < 100; i++) motion.Step(100, 5, 1f / 60, false);
        Near(motion.Translation, 68, "Stationary target converges without recentering the dead zone");
        motion.Step(-800, 5, 1f / 60, false); Near(motion.Translation, 980, "Distant teleport snaps");
        motion.Step(-3000, 5, 1f / 60, false); Near(motion.Translation, 1440, "Top framing");
        motion.Step(float.NaN, 5, 1, false); Near(motion.Translation, 1440, "Invalid position ignored");
        motion.Reset(); motion.Step(-700, 5, 0, true); Near(motion.Translation, 880, "Restart initializes even while paused");
        motion.Step(-700, 1, 0, true); Near(motion.Translation, 0, "Single-screen map never scrolls");
        var a = new CameraMotion(); var b = new CameraMotion();
        a.Step(-100, 10, 0, false); b.Step(-100, 10, 0, false);
        a.Step(-150, 10, 1f / 30, false);
        b.Step(-150, 10, 1f / 60, false); b.Step(-150, 10, 1f / 60, false);
        Near(a.Translation, b.Translation, "Frame subdivision invariant smoothing");
        foreach (int rate in new[] { 60, 144, 240 })
        {
            var comfort = new CameraMotion();
            comfort.Step(-400, 10, 0, false);
            for (int i = 1; i <= rate; i++) comfort.Step(-400 - 120f * i / rate, 10, 1f / rate, false);
            float apex = comfort.Translation;
            for (int i = 1; i <= rate * 2; i++)
            {
                float last = comfort.Translation;
                comfort.Step(-520 + Math.Min(35f, 35f * i / rate), 10, 1f / rate, false);
                Check(comfort.Translation >= last - .0001f, "Small descent never reverses the camera at " + rate + " Hz");
            }
            Check(comfort.Translation >= apex, "Apex framing is retained");
            float settled = comfort.Translation;
            for (int i = 0; i < rate * 3; i++) comfort.Step(-485, 10, 1f / rate, false);
            Near(comfort.Translation, settled, "Landing has no delayed downward recentering");
            for (int i = 0; i < rate; i++) comfort.Step(-400, 10, 1f / rate, false);
            Check(comfort.Translation < settled - 20, "Sustained descent moves the camera down");
        }
        motion.Reset(); motion.Step(-1500, 10, 0, false);
        for (int i = 1; i < 25; i++)
        {
            float y = -1500 + i * 35;
            motion.Step(y, 10, 1f / 60, false);
            Check(y + motion.Translation <= 312.01f, "Fast fall keeps the player in view");
        }
        for (int shift = 0; shift <= 1440; shift++)
        {
            var visible = CameraMotion.VisibleScreens(shift, 5);
            Check(visible.Length <= 2 && visible.All(x => x >= 0 && x < 5), "Only valid adjacent screens drawn");
            for (int y = 0; y < 360; y++)
            {
                int covered = visible.Count(screen => y >= shift - screen * 360 && y < shift - screen * 360 + 360);
                if (covered != 1) throw new Exception("Gap/overlap at shift " + shift + ", y " + y);
            }
        }
        Check(CameraMotion.VisibleScreens(0, 0).Length == 0, "Empty map is safe");
    }

    private static void FastFallPresentation()
    {
        var camera = new CameraMotion(); float y = -6000, largest = 0;
        camera.Observe(y, 30, false);
        for (int tick = 0; tick < 180; tick++)
        {
            y += 15; camera.Observe(y, 30, false, 900);
            for (int frame = 0; frame < 4; frame++)
            {
                float before = camera.Translation;
                camera.Advance(1f / 240);
                if (tick > 30) largest = Math.Max(largest, Math.Abs(camera.Translation - before));
            }
        }
        Console.WriteLine("[PERF] Fast-fall maximum camera step at 240 Hz: " + largest.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " logical pixels");
        Check(largest < 6, "Fast fall remains smooth between simulation ticks instead of hitting the visibility clamp");
        var landing = new CameraMotion(); landing.Observe(-400, 10, false);
        for (int tick = 1; tick <= 12; tick++)
        { landing.Observe(-400 - tick * 10, 10, false, -600); landing.Advance(1f / 60); }
        landing.Observe(-520, 10, false, 0);
        for (int frame = 0; frame < 720; frame++)
        {
            float before = landing.Translation; landing.Advance(1f / 240);
            Check(landing.Translation >= before, "Fast ascent followed by landing never overshoots and reverses framing");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NativeTests()
    {
        Renderer.InvalidateCompatibility();
        long scanTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Renderer.RefreshCompatibility(scanTime); int scans = Renderer.CompatibilityScans;
        for (int tick = 0; tick < 240; tick++)
        { Renderer.AfterUpdate(); Renderer.RefreshCompatibility(scanTime + tick); }
        Check(Renderer.CompatibilityScans == scans, "Simulation updates do not repeatedly deserialize Harmony patches");
        Renderer.RefreshCompatibility(scanTime + System.Diagnostics.Stopwatch.Frequency);
        Check(Renderer.CompatibilityScans == scans + 1, "Independent hook changes are periodically rechecked");
        var count = typeof(LevelManager).GetField("_total_screens", Flags);
        count.SetValue(null, 5);
        RenderContext.NativeScreen.SetValue(null, 1);
        Camera.Offset = new Vector2(3, 4);
        Near(Camera.TransformVector2(new Vector2(20, -100)).Y, 264, "Native transform unchanged outside rendering");
        using (new RenderContext(3))
        {
            Check(Camera.CurrentScreen == 3 && Camera.CurrentScreenIndex1 == 4, "Render screen queries match selected screen");
            Check((int)RenderContext.NativeScreen.GetValue(null) == 1, "Native camera field is never changed");
            Near(Camera.TransformVector2(new Vector2(20, -100)).Y, 984, "World vector follows render screen");
            var rect = new Rectangle(20, -100, 10, 20);
            Check(Camera.TransformRect(rect).Y == 984, "Value rectangle transform");
            Camera.TransformRect(ref rect); Check(rect.Y == 984, "Ref rectangle transform");
            Check(Camera.OnlyOffsetRect(new Rectangle(0, 0, 480, 360)).Y == 4, "Screen-local layers keep their native coordinates");
            using (new RenderContext(0)) Check(Camera.CurrentScreen == 0, "Nested render scope");
            Check(Camera.CurrentScreen == 3, "Nested scope restores previous view");
            int workerScreen = -1; float workerY = 0;
            var thread = new Thread(delegate() { workerScreen = Camera.CurrentScreen; workerY = Camera.TransformVector2(new Vector2(0, -100)).Y; });
            thread.Start(); thread.Join();
            Check(workerScreen == 1 && workerY == 264, "Save/worker threads observe logical camera and transforms");
        }
        Check(Camera.CurrentScreen == 1 && !RenderContext.Active, "Render exit restores native queries");
        try { using (new RenderContext(4)) throw new InvalidOperationException("fixture"); }
        catch (InvalidOperationException) { }
        Check(!RenderContext.Active && Camera.CurrentScreen == 1, "Exception restores render scope");
        Camera.Offset = Vector2.Zero;
        Camera.UpdateCamera(new Point(100, -725));
        Check(Camera.CurrentScreen == 3, "Native screen transition remains functional");
        Camera.UpdateCameraWithVelocity(new Point(100, -1079), Vector2.Zero);
        Check(Camera.CurrentScreen == 3, "Native velocity gating unchanged");
    }
}
