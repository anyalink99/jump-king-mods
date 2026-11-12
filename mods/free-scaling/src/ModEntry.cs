using System;
using System.Reflection;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Settings;
using JumpKing;

[assembly: AssemblyTitle("Graphics+")]
[assembly: AssemblyVersion("1.0.4.0")]
[assembly: AssemblyFileVersion("1.0.4.0")]

namespace FreeScaling
{
    [RuntimeModule("free-scaling", "Graphics+")]
    public static class ModEntry
    {
        private static RuntimeScope process;
        private static WindowSession session;
        internal static bool Enabled { get { return session != null; } }
        internal static readonly Setting<bool> EnableSetting = new Setting<bool>("free-scaling.enabled", "Free scaling", () => Enabled, SetEnabled);

        [BeforeLevelLoad]
        public static void Initialize()
        {
            if (process != null) return;
            Settings.Load(); var scope = new RuntimeScope();
            try {
                var hooks = scope.Own(new OwnedPatches("niotid.free-scaling"));
                Menu.Install(hooks);
                scope.Own(JKRuntime.Presentation.SmoothRendering.Register("free-scaling"));
                Game1.instance.Exiting += Exiting; scope.Defer(() => Game1.instance.Exiting -= Exiting);
                // BeforeLevelLoad runs inside ContentInit; the window/device already exist
                // a postfix installed on that running call would miss startup entirely
                if (Settings.Current.Enabled) session = new WindowSession(Settings.Current);
                process = scope;
            } catch { scope.Dispose(); throw; }
        }
        [BeforeAttempt]
        public static void Prepare(RuntimeScope scope)
        {
            if(JKRuntime.Presentation.SmoothRendering.Enabled)JKRuntime.Presentation.SmoothRendering.Prepare();
            scope.Defer(JKRuntime.Presentation.SmoothRendering.Reset);
        }
        internal static void SetEnabled(bool value)
        {
            if (value == Enabled) return;
            var next = Settings.Current.Copy(); next.Enabled = value;
            if (value) {
                var acquired = new WindowSession(next);
                try { Settings.File.Save(next); session = acquired; }
                catch { acquired.Dispose(); throw; }
            } else {
                Settings.File.Save(next); session.Dispose(); session = null;
            }
        }
        internal static void SetSampling(PixelSampling value)
        {
            if (!Enabled) return;
            var next = Settings.Current.Copy(); next.Sampling = value;
            Settings.File.Save(next); session.Sampling = value;
        }
        internal static void RememberWindow(int width, int height, int x, int y)
        {
            if (!Enabled || !Settings.File.CanSave || (Settings.Current.Width == width && Settings.Current.Height == height
                && Settings.Current.HasPosition && Settings.Current.X == x && Settings.Current.Y == y)) return;
            var next = Settings.Current.Copy(); next.Width = width; next.Height = height;
            next.HasPosition = true; next.X = x; next.Y = y;
            try { Settings.File.Save(next); }
            catch (Exception error) { Console.WriteLine("[Graphics+] Window placement wasn't saved: " + error.GetBaseException().Message); }
        }
        private static void Exiting(object sender, EventArgs args)
        {
            if (session != null) { session.Dispose(); session = null; }
            if (process != null) { process.Dispose(); process = null; }
        }
    }
}
