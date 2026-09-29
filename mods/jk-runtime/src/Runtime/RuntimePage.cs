using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using JKRuntime.UI;

namespace JKRuntime
{
    internal sealed class RuntimePage : IUiPage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(34, 24, 412, 310));
        private readonly List<string> lines = new List<string>();
        private int scroll;
        private static IDisposable observations;
        private int profileLine;
        private string exportStatus = "ENTER: EXPORT REPORT";
        public bool WantsClose { get; private set; }
        public void OnOpen()
        {
            WantsClose = false; scroll = 0; lines.Clear();
            GameContract contract = RuntimeHost.Contract;
            lines.Add("Runtime " + RuntimeApi.Version + " / UIApi+ " + UIApi.Version);
            lines.Add("Diagnostics: " + PerformanceDiagnostics.Status);
            lines.Add("Report: " + PerformanceDiagnostics.LastReport);
            lines.Add("Modules: " + RuntimeApi.State);
            lines.Add("Game contract: " + (contract.Available ? "available" : "UNAVAILABLE"));
            lines.Add("Game fingerprint: " + (contract.KnownFingerprint ? "known" : "unrecognized"));
            lines.Add("UIApi+ and Controls+ are built in.");
            profileLine = lines.Count; lines.Add(ProfileText());
            lines.Add("Physical action streams: " + Input.SharedActionSampler.ActiveStreams);
            lines.Add("Outstanding resources: " + RuntimeResources.Inspect().Length);
            foreach (var mechanic in RuntimeApi.Mechanics.Inspect())
            {
                lines.Add(mechanic.Id + ": " + (mechanic.Error != null ? "READ FAILED" : mechanic.State.Active ? "active" : mechanic.State.Available ? "inactive" : "unavailable"));
                if (mechanic.State != null) lines.Add("  " + mechanic.State.Reason);
                if (mechanic.Conflicts.Length != 0) lines.Add("  conflicts: " + string.Join(", ", mechanic.Conflicts));
            }
            lines.Add("Modifier observer: " + Gameplay.ModifierRegistrationObserver.Status);
            lines.Add("MoreTextOptions: " + Compatibility.MoreTextOptionsCompatibility.Status);
            lines.Add("Jump King Manager: " + Compatibility.JumpKingManagerCompatibility.Status);
            foreach (ModuleStatus module in RuntimeApi.GetModules())
            {
                lines.Add(module.Id + ": " + module.State);
                if (!string.IsNullOrEmpty(module.Detail)) lines.Add(module.Detail);
            }
            foreach (string error in RuntimeApi.GetErrors()) lines.Add(error.Split('\n')[0]);
            var harmony = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "0Harmony").ToArray();
            lines.Add(harmony.Length == 0 ? "Harmony: not loaded (not required)." : "Loaded Harmony:");
            foreach (var assembly in harmony) lines.Add("  " + assembly.GetName().Version);
            foreach (string error in PackageHost.Errors) lines.Add(error.Split('\n')[0]);
            if (JKRuntime.Settings.Commands.LastError != null) lines.Add("Command failure: " + JKRuntime.Settings.Commands.LastError.Split('\n')[0]);
            lines.Add("External mods: observed, not reordered.");
            lines.Add("Native charge and body registrations: runtime-owned.");
        }
        public void OnClose() { }
        private static string ProfileText() { return "LEFT/RIGHT: callback trace " + (observations != null ? "ON" : "OFF"); }
        internal static void StopCapture()
        { if (observations != null) { observations.Dispose(); observations = null; } RuntimeJournal.ProfileCallbacks = PerformanceDiagnostics.Enabled; }
        public void Update(UiInput input, float delta)
        {
            if (input.Cancel) { WantsClose = true; UiSounds.Play(UiSound.Back); }
            if (input.Up) UiSounds.Select(ref scroll, Math.Max(0, scroll - 1));
            if (input.Down) UiSounds.Select(ref scroll, Math.Min(Math.Max(0, lines.Count - 10), scroll + 1));
            if (input.Left || input.Right)
            {
                if (observations != null) StopCapture();
                else { observations = Gameplay.GameplayEvents.Subscribe("jk-runtime.diagnostics", delegate { }); RuntimeJournal.ProfileCallbacks = true; }
                lines[profileLine] = ProfileText();
                UiSounds.Play(UiSound.Change);
            }
            if (input.Confirm)
            {
                try { RuntimeApi.ExportDiagnostics(); exportStatus = "REPORT SAVED BESIDE MOD DLL"; UiSounds.Play(UiSound.Confirm); }
                catch (Exception error) { exportStatus = "EXPORT FAILED"; lines.Add(error.Message); UiSounds.Play(UiSound.Error); }
            }
        }
        public void Draw()
        {
            frame.Draw();
            UiTheme.TextLine("JK RUNTIME", new Vector2(52, 40), UiTheme.Gold, false);
            for (int i = 0; i < 10 && scroll + i < lines.Count; i++)
                UiTheme.TextLine(UiTheme.FitText(lines[scroll + i], 372, true), new Vector2(52, 78 + i * 20), UiTheme.Text, true);
            UiTheme.TextLine(exportStatus, new Vector2(52, 294), UiTheme.Muted, true);
            UiTheme.TextLine("UP/DOWN: SCROLL   ESC: BACK", new Vector2(52, 311), UiTheme.Muted, true);
        }
    }
}
