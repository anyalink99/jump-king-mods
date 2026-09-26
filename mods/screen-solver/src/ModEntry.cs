using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.Settings;
using JKRuntime.UI;
using JumpKing.PauseMenu;

[assembly: AssemblyVersion("0.1.4.0")]
[assembly: AssemblyFileVersion("0.1.4.0")]

namespace ScreenSolver
{
    public sealed class Preferences
    {
        public int[][] SolveKeys { get; set; }
        public Preferences() { SolveKeys = new[] { new[] { 118 } }; }
    }
    [RuntimeModule("screen-solver", "Screen Solver")]
    public static class ModEntry
    {
        private static UiRegistrationScope scope;
        private static Preferences settings;
        private static SolverPage page;
        private static string SettingsPath { get { return Path.Combine(PackageHost.GetDataDirectory(typeof(ModEntry).Assembly), "ScreenSolver.Settings.xml"); } }
        private static UiChord[] Binding()
        { return (settings.SolveKeys ?? new int[0][]).Where(k => k != null).Select(k => new UiChord(k)).ToArray(); }
        private static void SaveBinding(UiChord[] chords)
        { settings.SolveKeys = (chords ?? new UiChord[0]).Select(c => c.Buttons).ToArray(); AtomicXmlFile.Save(SettingsPath, settings); }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            if (!RuntimeApi.Supports("simulation-sessions-v1")) throw new NotSupportedException("Screen Solver requires JK Runtime 1.2.0 or later");
            if (settings == null)
            {
                try { settings = AtomicXmlFile.Load<Preferences>(SettingsPath) ?? new Preferences(); }
                catch (Exception e) { Console.WriteLine("[Screen Solver] Settings could not be read: " + e.Message); settings = new Preferences(); }
            }
            scope = new UiRegistrationScope("screen-solver"); context.Track(scope);
            scope.RegisterBinding(new UiBindingDefinition("screen-solver.solve", "Screen Solver", "Solve screen", Binding,
                SaveBinding, () => SaveBinding(new[] { new UiChord(118) })));
            scope.RegisterInputAction(UiInputActionDefinition.FromChords("screen-solver.solve", "Solve screen", 160,
                Binding, () => !UIApi.IsOpen, () => Open()));
        }
        internal static bool Open()
        {
            if (UIApi.IsOpen) return false;
            SolveCapture capture = null; string error = null;
            try { capture = SolveCapture.Capture(JumpKing.GameManager.GameLoop.m_player); }
            catch (Exception e) { error = e.GetBaseException().Message; Console.WriteLine("[Screen Solver] " + error); }
            UIApi.ClosePauseMenu();
            page = new SolverPage(capture, error);
            if (UIApi.Open(page)) return true;
            page.OnClose(); page = null; return false;
        }
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton Menu(object factory, GuiFormat format)
        { return UIApi.CreateFeedbackButton("Solve screen", () => Open() ? UiMenuActionResult.Completed() : UiMenuActionResult.Rejected()); }
        [OnLevelUnload]
        public static void Stop() { if (page != null) page.OnClose(); page = null; if (scope != null) scope.Dispose(); scope = null; }
        [OnLevelEnd]
        public static void End() { Stop(); }
    }
}
