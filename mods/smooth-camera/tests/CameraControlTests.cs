using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using JKRuntime.UI;
using JumpKing.Controller;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void GestureTests()
    {
        foreach (CameraMode mode in new[] { CameraMode.Up, CameraMode.Down, CameraMode.Focus })
        {
            var g = new CameraGestures();
            Action<bool, double> input = (held, time) => g.Update(held && mode == CameraMode.Up,
                held && mode == CameraMode.Down, held && mode == CameraMode.Focus, time, true);
            input(true, 0); input(false, .1);
            Check(g.Current == CameraMode.Normal, "Entry with a held key requires release");
            input(true, 1); Check(g.Current == mode, "Camera responds immediately on press");
            input(false, 1.1); Check(g.Latched == mode && g.Current == mode, "Tap latches " + mode);
            input(true, 2); Check(g.Current == mode, "Press on a latch never flashes normal framing before hold classification");
            input(false, 2.1); Check(g.Current == CameraMode.Normal, "Second tap cancels " + mode);
            input(true, 3); input(true, 3.3); input(false, 3.4);
            Check(g.Current == CameraMode.Normal, "Hold returns to automatic tracking " + mode);
            input(true, 4); input(false, 4.3);
            Check(g.Current == CameraMode.Normal, "Delayed release without intermediate updates remains a hold");
            input(true, 5); input(false, 5.25);
            Check(g.Current == CameraMode.Normal, "Exact hold threshold is not a tap");
            input(true, 6); g.Update(false, false, false, 6.1, false); input(false, 7);
            Check(g.Current == CameraMode.Normal, "Pause/focus loss cancels unfinished gesture");
            input(true, 8); input(false, 8.1); g.Reset(); input(false, 9);
            Check(g.Latched == CameraMode.Normal, "Restart clears mode latch");
        }
        var directional = new CameraGestures();
        directional.Update(false, false, false, 0, true);
        directional.Update(true, false, false, 1, true); directional.Update(false, false, false, 1.1, true);
        directional.Update(false, true, false, 2, true); directional.Update(false, false, false, 2.1, true);
        Check(directional.Current == CameraMode.Normal, "Opposite directional tap cancels instead of switching");
        directional.Update(true, false, false, 3, true); directional.Update(false, false, false, 3.1, true);
        directional.Update(false, true, false, 4, true); directional.Update(false, true, false, 4.3, true);
        Check(directional.Current == CameraMode.Down, "Opposite hold temporarily looks down");
        directional.Update(false, false, false, 4.4, true);
        Check(directional.Current == CameraMode.Up, "Opposite hold restores earlier up latch");
        directional.Update(false, false, true, 5, true); directional.Update(false, false, true, 5.3, true);
        directional.Update(false, false, false, 5.4, true);
        Check(directional.Current == CameraMode.Up, "Focus hold restores earlier directional latch");
        directional.Update(false, false, true, 6, true); directional.Update(false, false, false, 6.1, true);
        Check(directional.Current == CameraMode.Focus, "Focus tap replaces directional latch");
        directional.Update(true, true, false, 7, true); directional.Update(false, false, false, 7.1, true);
        Check(directional.Current == CameraMode.Focus, "Ambiguous simultaneous directions preserve latch");
        directional.Update(true, false, false, 8, true); directional.Update(false, false, false, 8.1, false);
        directional.Update(true, false, false, 9, true); directional.Update(false, false, false, 9.1, true);
        Check(directional.Current == CameraMode.Focus, "Returning from a menu while held cannot latch");
    }

    private static void CameraViewTests()
    {
        foreach (int rate in new[] { 30, 60, 144, 240 })
        {
            var view = new CameraView();
            view.Advance(CameraMode.Normal, 100, 700, -520, 0, 2, 6, 0, false);
            Near(view.Y, 700, "Normal view retains original framing");
            view.Advance(CameraMode.Up, 100, 700, -520, 0, 2, 6, 0, false);
            Near(view.Y, 700, "Mode switch does not snap");
            for (int i = 0; i < rate * 2; i++) view.Advance(CameraMode.Up, 100, 700, -520, 0, 2, 6, 1f / rate, false);
            Near(view.Y - 520, 312, "Up places the king near the bottom at " + rate);
            Near(view.X, 100, "Look preserves horizontal following");
            float before = view.Y;
            view.Advance(CameraMode.Down, 100, 700, -520, 0, 2, 6, .1f, true);
            Near(view.Y, before, "Pause freezes manual framing");
            for (int i = 0; i < rate * 2; i++) view.Advance(CameraMode.Down, 100, 700, -520, 0, 2, 6, 1f / rate, false);
            Near(view.Y - 520, 48, "Down places the king near the top");
            for (int i = 0; i < rate * 2; i++) view.Advance(CameraMode.Focus, 100, 700, -520, 0, 2, 6, 1f / rate, false);
            Near(view.Y, 720, "Focus uses active native screen even when player-derived screen differs"); Near(view.X, 0, "Focus removes horizontal offset");
            view.Advance(CameraMode.Focus, 100, 700, -530, 0, 3, 6, 1f / rate, false);
            Near(view.Y, 1080, "Active focus follows native screen changes exactly");
            for (int i = 0; i < rate * 2; i++) view.Advance(CameraMode.Normal, 110, 712, -530, 0, 3, 6, 1f / rate, false);
            Near(view.Y, 712, "Release restores retained automatic view"); Near(view.X, 110, "Release restores horizontal following");
            for (int i = 0; i < rate; i++) view.Advance(CameraMode.Down, 0, 0, 300, 0, 0, 1, 1f / rate, false);
            Near(view.Y, 0, "Single screen and map bounds remain clamped");
        }
        var a = new CameraView(); var b = new CameraView();
        a.Advance(CameraMode.Normal, 0, 700, -520, 0, 2, 6, 0, false);
        b.Advance(CameraMode.Normal, 0, 700, -520, 0, 2, 6, 0, false);
        a.Advance(CameraMode.Up, 0, 700, -520, 0, 2, 6, 1f / 30, false);
        b.Advance(CameraMode.Up, 0, 700, -520, 0, 2, 6, 1f / 60, false);
        b.Advance(CameraMode.Up, 0, 700, -520, 0, 2, 6, 1f / 60, false);
        Near(a.Y, b.Y, "Manual transition is frame-subdivision invariant");
        float y = a.Y; a.Rebase(480, 720);
        a.Advance(CameraMode.Up, 480, 1420, -1240, 0, 4, 6, 0, false);
        Near(a.Y, y + 720, "Manual view rebases across portal without a false teleport snap");
        a.Advance(CameraMode.Down, 0, 1000, -1500, 0, 4, 6, float.NaN, false);
        Check(!float.IsNaN(a.Y), "Invalid presentation time does not poison manual camera");
        foreach (CameraMode mode in new[] { CameraMode.Up, CameraMode.Down })
        {
            var fast = new CameraView(); float player = -6000, largest = 0;
            fast.Advance(CameraMode.Normal, 0, 6180, player, 0, 17, 30, 0, false);
            for (int tick = 0; tick < 180; tick++)
            {
                player += 15;
                for (int frame = 0; frame < 4; frame++)
                {
                    float before = fast.Y;
                    fast.Advance(mode, 0, 180 - player, player, 900, 17, 30, 1f / 240, false);
                    if (tick > 30) largest = Math.Max(largest, Math.Abs(fast.Y - before));
                    Check(player + fast.Y >= 15.99f && player + fast.Y <= 344.01f, "Manual view keeps king visible in terminal fall");
                }
            }
            Check(largest < 8, "Manual terminal fall avoids simulation-sized camera steps");
        }
        var portal = new CameraView();
        portal.Advance(CameraMode.Normal, 100, 700, -520, 0, 2, 6, 0, false);
        portal.Advance(CameraMode.Focus, 100, 700, -520, 0, 2, 6, .1f, false);
        portal.Advance(CameraMode.Normal, 0, 700, -520, 0, 2, 6, .01f, false);
        Near(portal.X, 0, "Return from Focus never exposes a portal column after its offset disappears");
        Console.WriteLine("[OK] Camera tap/hold transitions, latches, focus, pause, bounds and portal rebasing");
    }

    private sealed class CameraTestPad : IPad
    {
        internal string Id; internal int Reads; internal int[] Buttons = new int[0];
        public int[] GetPressedButtons() { Reads++; return Buttons; }
        public string GetSaveIdentifier() { return Id; }
        public string GetPrintName() { return Id; }
        public string ButtonToString(int button) { return button.ToString(); }
        public PadBinding GetDefaultBind() { return new PadBinding(); }
        public bool IsConnected() { throw new Exception("Camera must not poll device connectivity"); }
    }
    private static void CameraBindingTests()
    {
        var original = Settings.Current;
        var manager = ControllerManager.instance;
        try
        {
            var legacy = (CameraSettings)new XmlSerializer(typeof(CameraSettings)).Deserialize(new StringReader("<CameraSettings><Smooth>false</Smooth><Horizontal>true</Horizontal></CameraSettings>"));
            Check(legacy.FocusBindings.Count == 0, "Legacy settings have no explicit Focus override");
            Check(legacy.HighRefresh, "Legacy XML preserves the previously enabled high refresh mode");
            legacy.HighRefresh = false;
            var updated = Settings.WithFocus(legacy, "keyboard", new[] { new UiChord(70), new UiChord(17, 38) });
            updated = Settings.WithFocus(updated, "controller", new[] { new UiChord(256) });
            Check(!updated.Smooth && updated.Horizontal && legacy.FocusBindings.Count == 0, "Binding changes preserve toggles and prior snapshot");
            Check(!updated.HighRefresh, "Editing Focus bindings preserves a disabled 240 Hz option");
            var xml = new XmlSerializer(typeof(CameraSettings)); var writer = new StringWriter(); xml.Serialize(writer, updated);
            Settings.Current = (CameraSettings)xml.Deserialize(new StringReader(writer.ToString()));
            Check(Settings.Current.FocusBindings.Count == 2, "Focus alternatives/chords and device profiles survive XML roundtrip");
            Check(!Settings.Current.HighRefresh, "Disabled 240 Hz option survives XML roundtrip");
            var keyboard = new CameraTestPad { Id = "keyboard", Buttons = new[] { 17, 38 } };
            var pad = new PadInstance(keyboard); var controller = new PadInstance(new CameraTestPad { Id = "controller" });
            typeof(PadInstance).GetField("current_state", Flags).SetValue(pad, new PadState { up = true, jump = true });
            ControllerManager.instance = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", Flags).SetValue(ControllerManager.instance, new List<PadInstance> { pad, controller });
            bool up, down, focus;
            var stored = Settings.Current; Settings.Current = legacy;
            keyboard.Id = CameraControls.KeyboardDevice; keyboard.Buttons = new[] { 70 };
            CameraControls.ReadPads(out up, out down, out focus);
            Check(focus, "Legacy settings use F by default on the native keyboard profile");
            Settings.Current = Settings.WithFocus(legacy, CameraControls.KeyboardDevice, new UiChord[0]);
            CameraControls.ReadPads(out up, out down, out focus);
            Check(!focus, "Explicitly clearing the keyboard binding overrides default F");
            Settings.Current = stored; keyboard.Id = "keyboard"; keyboard.Buttons = new[] { 17, 38 }; keyboard.Reads = 0;
            CameraControls.ReadPads(out up, out down, out focus);
            Check(up && !down && focus && pad.GetState().jump, "Reads rebound native directions and Focus chord without consuming gameplay input");
            Check(keyboard.Reads == 1, "Focus reads a device only once per control update");
            keyboard.Buttons = new[] { 17 }; CameraControls.ReadPads(out up, out down, out focus);
            Check(!focus, "Partial chord is not Focus");
            keyboard.Buttons = new[] { 70 }; CameraControls.ReadPads(out up, out down, out focus);
            Check(focus, "Alternative Focus binding works");
            pad.GetBind().Enabled = false; CameraControls.ReadPads(out up, out down, out focus);
            Check(!up && !focus, "Disabled input device cannot control camera");
            CameraControls.Register(); CameraControls.Register();
            var bindings = UIApi.GetBindings().Where(x => x.Id == CameraControls.FocusId).ToArray();
            Check(bindings.Length == 1 && bindings[0].Label == "Focus Camera" && bindings[0].SupportsChords, "One standard Runtime binding supports chords");
            Check(ReferenceEquals(bindings[0], CameraControls.Binding), "Bind Focus and Controls share one binding definition");
            CameraControls.Unload();
            Check(!UIApi.GetBindings().Any(x => x.Id == CameraControls.FocusId), "Unload releases camera binding ownership");
        }
        finally { Settings.Current = original; ControllerManager.instance = manager; CameraControls.Unload(); }
    }
}
