using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JKRuntime;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace SmoothCamera
{
    internal sealed class CameraSettingsPage : ScopedUiPage
    {
        private const string DraftBinding = "smooth-camera.settings-focus";
        private readonly UiPageStack pages;
        private readonly UiFrame frame = new UiFrame(new Rectangle(8, 8, 464, 344));
        private readonly UiList list = new UiList();
        private readonly CameraPreview preview = new CameraPreview();
        private readonly UiPageCommand[] commands;
        internal CameraSettings Draft;
        internal string Section = "General";
        private bool editMap;
        private readonly string mapKey;
        private string error = "";
        internal CameraProfile Editing { get { return editMap ? Draft.ForMap(mapKey) : Draft; } }
        internal CameraSettingsPage(UiPageStack owner)
        {
            pages = owner; Settings.Load(); Draft = Settings.Current.Copy(); mapKey = MapPolicy.Current.Key;
            commands = new[] {
                new UiPageCommand(UiAction.Confirm, "Edit", () => list.Update(new UiInput { Action = UiAction.Confirm })),
                new UiPageCommand(UiAction.Secondary, "Apply", Apply),
                new UiPageCommand(UiAction.Cancel, "Cancel", () => WantsClose = true)
            };
            Refresh();
        }
        protected override void OpenPage(RuntimeScope resources)
        {
            var bindings = resources.Own(new UiRegistrationScope("smooth-camera.settings"));
            bindings.RegisterBinding(new UiBindingDefinition(DraftBinding, "Smooth Camera", "Focus Camera", DraftChords,
                chords => { string device = CameraControls.MainDevice(); if (device != null) Draft = Settings.WithFocus(Draft, device, chords); },
                () => { string device = CameraControls.MainDevice(); if (device != null) Draft = Settings.WithFocus(Draft, device,
                    device == CameraControls.KeyboardDevice ? new[] { new UiChord(70) } : new UiChord[0]); }));
        }
        private UiChord[] DraftChords()
        {
            string device = CameraControls.MainDevice();
            foreach (var binding in Draft.FocusBindings)
                if (binding.Device == device)
                {
                    var chords = new List<UiChord>();
                    foreach (var chord in binding.Chords) if (chord.Buttons != null) chords.Add(new UiChord(chord.Buttons));
                    return chords.ToArray();
                }
            return device == CameraControls.KeyboardDevice ? new[] { new UiChord(70) } : new UiChord[0];
        }
        internal void Apply()
        {
            try
            {
                // Enable can change through its hotkey while this draft is open
                Draft.Smooth = Settings.Current.Smooth; Draft.AutomaticallyDisabled = Settings.Current.AutomaticallyDisabled;
                Draft.DisabledByMap = Settings.Current.DisabledByMap;
                Settings.Save(Draft.Copy());
                JKRuntime.Settings.Commands.Enqueue(Settings.ApplyPresentationSetting);
                WantsClose = true;
            }
            catch (Exception failure) { error = failure.GetBaseException().Message; UiSounds.Play(UiSound.Error); }
        }
        private void Number(string title, string description, Func<double> read, Action<double> write, double min, double max, double step)
        {
            string previousPreset = Editing.Preset;
            pages.Push(new CameraNumberPage(title, description, read, value => { write(value); Editing.Preset = "Custom"; }, min, max, step, () => Editing, () => Editing.Preset = previousPreset));
        }
        private UiListItem Row(string id, string label, string description, Action action, bool enabled = true)
        { return new UiListItem(id, label, () => { error = ""; action(); Refresh(); }, description, enabled); }
        private static string On(bool value) { return value ? "On" : "Off"; }
        private static string Value(float value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
        private void ChangeSection(string name) { UiPointer.CancelCapture(this); Section = name; Refresh(); list.Select("section"); }
        private void Refresh()
        {
            var rows = new List<UiListItem>();
            string[] sections = { "General", "Vertical", "Horizontal", "Controls", "Map rules" };
            rows.Add(Row("section", "Page: " + Section + " >", "Select to change page. All edits stay in a draft until Apply.",
                () => ChangeSection(sections[(Array.IndexOf(sections, Section) + 1) % sections.Length])));
            var p = Editing;
            if (Section == "General")
            {
                rows.Add(Row("scope", "Editing: " + (editMap ? "This map" : "Global"), "A personal map profile overrides author recommendations. Enable and 240 Hz remain global.", () => {
                    if (!editMap && ReferenceEquals(Draft.ForMap(mapKey), Draft)) Draft.MapProfiles.Add(new SavedMapProfile { Key = mapKey, Profile = Draft.CopyProfile() });
                    editMap = !editMap;
                }, mapKey.Length > 0));
                rows.Add(Row("preset", "Preset: " + p.Preset, "Classic preserves the original camera. Presets keep horizontal enable, bindings and map rules.", () => {
                    string name = p.Preset == "Classic" ? "Centered" : p.Preset == "Centered" ? "Platformer Window" : "Classic";
                    bool horizontal = p.Horizontal; float margin = p.LookMargin, hold = p.HoldMilliseconds;
                    p.TakeProfile(CameraProfile.FromPreset(name)); p.Horizontal = horizontal; p.LookMargin = margin; p.HoldMilliseconds = hold;
                }));
                rows.Add(Row("refresh", "240 Hz: " + On(Draft.HighRefresh), "Extra camera presentation frames; native physics and input timing stay unchanged.", () => Draft.HighRefresh = !Draft.HighRefresh));
                rows.Add(Row("recommend", "Map recommendations: " + On(Draft.UseMapRecommendations), "Use author motion profiles unless you have a personal map profile. Forced areas and boundaries always apply.", () => Draft.UseMapRecommendations = !Draft.UseMapRecommendations));
                rows.Add(Row("diagnostics", "Diagnostics: " + On(Draft.Diagnostics), "Draw the focus, tracking window, authored zones and active camera rule in gameplay.", () => Draft.Diagnostics = !Draft.Diagnostics));
                rows.Add(Row("vertical", "Vertical settings >", "Configure vertical tracking.", () => ChangeSection("Vertical")));
                rows.Add(Row("horizontal", "Horizontal settings >", "Configure horizontal tracking near connected side portals.", () => ChangeSection("Horizontal")));
                rows.Add(Row("controls", "Camera controls >", "Configure tap/hold, manual views and Focus binding.", () => ChangeSection("Controls")));
                rows.Add(Row("rules", "Map rules >", "Inspect the policy and forced areas in the current map.", () => ChangeSection("Map rules")));
                rows.Add(Row("remove-map", "Remove this map profile", "Apply to return this map to global settings and author recommendations.", () => { Draft.MapProfiles.RemoveAll(m => m.Key == mapKey); editMap = false; }, mapKey.Length > 0 && !ReferenceEquals(Draft.ForMap(mapKey), Draft)));
                rows.Add(Row("reset", "Reset motion profile", "Restore Classic motion for the selected scope. Keep Enable, bindings, 240 Hz and map policy.", () => p.TakeProfile(new CameraProfile())));
            }
            else if (Section == "Vertical" || Section == "Horizontal")
            {
                bool horizontal = Section == "Horizontal";
                var a = horizontal ? p.HorizontalAxis : p.Vertical;
                if (horizontal) rows.Add(Row("enabled", "Horizontal: " + On(p.Horizontal), "Horizontal views require a usable native side portal and a compatible neighboring area.", () => { p.Horizontal = !p.Horizontal; p.Preset = "Custom"; }));
                rows.Add(Row("mode", "Follow: " + ModeName(a.Mode), "Direct follows the focus; Window follows its viewport edges; Jump King retains framing; Screen uses native framing.", () => { a.Mode = (FollowMode)(((int)a.Mode + 1) % 4); p.Preset = "Custom"; }));
                rows.Add(Row("focus", "Focus: " + Value(a.Focus * 100) + "%", horizontal ? "Percent from the left. 50% centers the king." : "Percent from the top. 50% centers the king; 60% leaves more room above.", () => Number("Focus (%)", "Measured from the top or left of the viewport.", () => a.Focus * 100, v => a.Focus = (float)v / 100, 15, 85, 1)));
                rows.Add(Row("window", "Window size: " + Value(a.Window), "Total size in game pixels. Used by Window mode.", () => Number("Window size", "The king moves freely inside this window.", () => a.Window, v => a.Window = (float)v, 0, 240, 2), a.Mode == FollowMode.Window));
                rows.Add(Row("negative-band", (horizontal ? "Left" : "Upper") + " band: " + Value(a.NegativeBand), "Jump King mode: retained framing threshold in game pixels.", () => Number("Negative band", "Free movement toward the top or left.", () => a.NegativeBand, v => a.NegativeBand = (float)v, 0, 120, 1), a.Mode == FollowMode.JumpKing));
                rows.Add(Row("positive-band", (horizontal ? "Right" : "Lower") + " band: " + Value(a.PositiveBand), "Jump King mode: use a larger lower band to absorb small descents and landings.", () => Number("Positive band", "Free movement toward the bottom or right.", () => a.PositiveBand, v => a.PositiveBand = (float)v, 0, 120, 1), a.Mode == FollowMode.JumpKing));
                rows.Add(Row("negative-response", (horizontal ? "Left" : "Up") + " response: " + Value(a.NegativeResponse), "Higher values catch up faster. Lower values feel softer.", () => Number("Negative response", "Response toward the top or left; larger is faster.", () => a.NegativeResponse, v => a.NegativeResponse = (float)v, 2, 60, 1)));
                rows.Add(Row("positive-response", (horizontal ? "Right" : "Down") + " response: " + Value(a.PositiveResponse), "Independent response for the opposite direction.", () => Number("Positive response", "Response toward the bottom or right; larger is faster.", () => a.PositiveResponse, v => a.PositiveResponse = (float)v, 2, 60, 1)));
                rows.Add(Row("assist", "Fast movement assist: " + On(a.FastAssist), "Speed up following during fast travel. The visibility guard always remains active.", () => { a.FastAssist = !a.FastAssist; p.Preset = "Custom"; }));
                rows.Add(Row("ahead", "Look ahead: " + Value(a.LookAhead), "Show extra space in the direction of motion. Zero disables anticipation.", () => Number("Look ahead", "Extra visible space in the direction of travel, in game pixels.", () => a.LookAhead, v => a.LookAhead = (float)v, 0, 96, 2)));
                rows.Add(Row("look-delay", "Direction delay: " + Value(a.LookDelay) + " s", "Wait before changing anticipation direction to avoid small left/right twitches.", () => Number("Direction delay", "Delay before reversing the look-ahead direction.", () => a.LookDelay, v => a.LookDelay = (float)v, 0, 1, .05)));
                rows.Add(Row("recenter", "Idle recenter: " + On(a.Recenter), "Optional return to the focus after the king stops. Off preserves the landing frame.", () => { a.Recenter = !a.Recenter; p.Preset = "Custom"; }));
                rows.Add(Row("idle-delay", "Idle delay: " + Value(a.RecenterDelay) + " s", "How long to keep the frame after stopping before recentering.", () => Number("Idle delay", "Wait after stopping before returning to the focus.", () => a.RecenterDelay, v => a.RecenterDelay = (float)v, 0, 5, .1), a.Recenter));
                rows.Add(Row("reset", "Reset this axis", "Restore this axis to Classic. Other settings and bindings are preserved.", () => { if (horizontal) p.HorizontalAxis = AxisSettings.HorizontalDefault(); else p.Vertical = new AxisSettings(); p.Preset = "Custom"; }));
            }
            else if (Section == "Controls")
            {
                rows.Add(Row("focus-binding", "Bind Focus >", "Tap to latch the native screen view. Hold for a temporary view. Binding edits are saved with Apply.", () => pages.Push(new UiBindingsPage("Bind Focus", "Draft bindings: use Apply in camera settings to save.", DraftBinding))));
                rows.Add(Row("margin", "Look edge margin: " + Value(p.LookMargin), "Distance between the king and the viewport edge during Look Up/Down.", () => Number("Look edge margin", "Smaller margins show farther above or below the king.", () => p.LookMargin, v => p.LookMargin = (float)v, 24, 144, 2)));
                rows.Add(Row("hold", "Hold threshold: " + Value(p.HoldMilliseconds) + " ms", "A shorter press toggles the view; a longer hold restores the previous view on release.", () => Number("Hold threshold (ms)", "Tap below this duration; hold at or above it.", () => p.HoldMilliseconds, v => p.HoldMilliseconds = (float)v, 100, 750, 10)));
                rows.Add(Row("reset", "Reset view controls", "Restore view distance and hold timing. Keep bindings.", () => { p.LookMargin = 48; p.HoldMilliseconds = 250; }));
            }
            else
            {
                var rules = MapPolicy.Current;
                rows.Add(Row("status", Renderer.Status, "Current effective camera state; forced areas override Enable.", () => { }));
                rows.Add(Row("policy", rules.DisableOnEnter ? "Policy: Disable on entry" : rules.ExplicitOnly ? "Policy: Explicit areas only" : "Policy: User settings", "A strict map disables Enable persistently. Turn it back on after entering another map.", () => { }));
                rows.Add(Row("map", "Map: " + (mapKey.Length == 0 ? "None" : Path.GetFileName(mapKey)), mapKey.Length == 0 ? "Start an attempt to load map rules." : mapKey, () => { }));
                if (rules.Error.Length > 0) rows.Add(Row("error", "Map rules could not load", rules.Error, () => { }));
                if (Draft.AutomaticallyDisabled) rows.Add(Row("disabled-by", "Disabled by previous map", Draft.DisabledByMap ?? "", () => { }));
                ScreenRule previous = null;
                foreach (var rule in rules.Screens)
                {
                    if (rule == null || ReferenceEquals(previous, rule)) continue;
                    previous = rule;
                    rows.Add(Row("screen-" + rule.From, "Screens " + rule.From + "-" + rule.To + ": " + rule.Mode,
                        "Profile: " + (rule.Profile ?? "user") + ". Entry: " + rule.Transition + ".", () => { }));
                }
                foreach (var zone in rules.Zones)
                    rows.Add(Row("zone-" + zone.Id, "Zone " + zone.Id + ": " + zone.Mode, "Screen " + zone.Screen + ", priority " + zone.Priority + ". Profile: " + (zone.Profile ?? "inherited") + ".", () => { }));
            }
            list.SetItems(rows);
        }
        internal static string ModeName(FollowMode mode)
        { return mode == FollowMode.JumpKing ? "Jump King" : mode == FollowMode.Direct ? "Direct Follow" : mode.ToString(); }
        public override void Update(UiInput input, float delta)
        {
            preview.Update(Editing, delta); Refresh();
            foreach (var command in commands) if (command.Handle(input)) return;
            if (input.Action == UiAction.Left || input.Action == UiAction.Right)
            {
                string[] names = { "General", "Vertical", "Horizontal", "Controls", "Map rules" };
                ChangeSection(names[(Array.IndexOf(names, Section) + (input.Action == UiAction.Left ? 4 : 1)) % 5]); return;
            }
            list.Update(input);
        }
        public override void Draw()
        {
            frame.Draw();
            var layout = new UiPageLayout(frame.Bounds, commands, 3);
            UiTheme.TextLine("SMOOTH CAMERA / " + Section.ToUpperInvariant(), layout.Title.Location.ToVector2(), UiTheme.Gold, true);
            var content = layout.Content;
            list.Draw(new Rectangle(content.X, content.Y, content.Width - 162, content.Height), rowHeight: 22);
            var area = new Rectangle(content.Right - 150, content.Y + 18, 150, 113);
            UiTheme.TextLine("PREVIEW", new Vector2(area.X, content.Y), UiTheme.Muted, true);
            preview.Draw(area, Editing);
            UiTheme.WrappedText((editMap ? "This map profile" : "Global profile") + "\n" + Renderer.Status,
                new Rectangle(area.X, area.Bottom + 8, area.Width, Math.Max(16, content.Bottom - area.Bottom - 8)), UiTheme.Muted);
            UiTheme.WrappedText(error.Length > 0 ? error : list.Description, layout.Status, error.Length > 0 ? UiTheme.Red : UiTheme.Muted);
            layout.DrawCommands();
        }
    }

    internal sealed class CameraNumberPage : ScopedUiPage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(8, 8, 464, 344));
        private readonly string title, description;
        private readonly UiNumberControl control;
        private readonly UiPageCommand[] commands;
        private readonly CameraPreview preview = new CameraPreview();
        private readonly Func<CameraProfile> profile;
        private readonly Action<double> write;
        private readonly double original;
        private bool accepted;
        private readonly Action cancel;
        internal CameraNumberPage(string heading, string text, Func<double> read, Action<double> set, double min, double max, double step, Func<CameraProfile> getProfile, Action onCancel = null)
        {
            title = heading; description = text; profile = getProfile; write = set; original = read(); cancel = onCancel;
            control = new UiNumberControl(min, max, step, read, set);
            commands = new[] { new UiPageCommand(UiAction.Confirm, "Keep", () => { accepted = true; WantsClose = true; }),
                new UiPageCommand(UiAction.Secondary, "Undo", () => control.Set(original)),
                new UiPageCommand(UiAction.Cancel, "Cancel", () => WantsClose = true) };
        }
        protected override void OpenPage(RuntimeScope resources) { resources.Defer(() => { if (!accepted) { write(original); if (cancel != null) cancel(); } }); }
        public override void Update(UiInput input, float delta)
        { preview.Update(profile(), delta); foreach (var command in commands) if (command.Handle(input)) return; control.Update(input); }
        public override void Draw()
        {
            frame.Draw(); var layout = new UiPageLayout(frame.Bounds, commands, 3);
            UiTheme.TextLine(title.ToUpperInvariant(), layout.Title.Location.ToVector2(), UiTheme.Gold, true);
            var area = new Rectangle(layout.Content.Right - 224, layout.Content.Y, 224, 168);
            preview.Draw(area, profile());
            control.Draw(new Rectangle(layout.Content.X, layout.Content.Y + 28, 176, 44));
            UiTheme.WrappedText("Adjust with navigation, wheel or drag.\n\nKeep returns to the draft. Apply saves all settings.",
                new Rectangle(layout.Content.X, layout.Content.Y + 90, 176, 90), UiTheme.Muted);
            UiTheme.WrappedText(description, layout.Status, UiTheme.Muted); layout.DrawCommands();
        }
    }
}
