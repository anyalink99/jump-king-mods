using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.Controller;
using SubframeCharge;

internal static class InputDiagnosticsTests
{
    private sealed class FakePad : IPad
    {
        internal int RawReads;
        internal bool BrokenName;
        public int[] GetPressedButtons() { RawReads++; throw new Exception("Diagnostics must not poll buttons"); }
        public string ButtonToString(int button) { return "button" + button; }
        public PadBinding GetDefaultBind() { return new PadBinding { jump = new[] { 32 } }; }
        public string GetSaveIdentifier() { return "test-device"; }
        public string GetPrintName() { if (BrokenName) throw new InvalidOperationException(); return "Test\r\ndevice"; }
        public bool IsConnected() { return true; }
    }

    internal static void Run()
    {
        List<string> lines = new List<string>();
        FakePad keyboard = new FakePad();
        FakePad xboxWrapper = new FakePad();
        FakePad unknown = new FakePad();
        PadInstance key = new PadInstance(keyboard);
        PadInstance xbox = new PadInstance(xboxWrapper);
        PadInstance other = new PadInstance(unknown);
        InputDiagnostics diagnostics = new InputDiagnostics(p => p == key,
            p => p == xbox ? 2 : -1, lines.Add);
        List<PadInstance> pads = new List<PadInstance> { key, xbox, other };
        TestBindingCache(pads);
        diagnostics.ObservePads(pads, 0);
        Require(Contains(lines, "backend=keyboard"), "keyboard classification");
        Require(Contains(lines, "backend=xinput xinputSlot=2"), "wrapped Xbox slot");
        Require(Contains(lines, "reason=unrecognized-backend"), "unknown backend reason");
        Require(Contains(lines, "Test\\r\\ndevice"), "escape untrusted device names");
        Require(Contains(lines, "jumpPhysical=\"32\""), "resolved binding");
        int count = lines.Count;
        diagnostics.ObservePads(pads, 1);
        Require(lines.Count == count, "no idle per-frame spam");
        SetJump(key, true);
        diagnostics.ObservePads(pads, 2);
        string firstDevice = diagnostics.ActiveDevices;
        Require(firstDevice != "none" && Contains(lines, "edge=down initial=False"), "source on press");
        SetJump(xbox, true);
        diagnostics.ObservePads(pads, 3);
        Require(diagnostics.ActiveDevices.Contains(","), "mixed held devices retained");
        SetJump(key, false);
        diagnostics.ObservePads(pads, 4);
        Require(diagnostics.ActiveDevices != firstDevice && !diagnostics.ActiveDevices.Contains(","), "source switch");
        key.GetBind().jump = new[] { 161 };
        diagnostics.ObservePads(pads, Stopwatch.Frequency + 1);
        Require(Contains(lines, "jumpPhysical=\"161\""), "binding change discovered");
        key.GetBind().Enabled = false;
        keyboard.BrokenName = true;
        diagnostics.ObservePads(pads, Stopwatch.Frequency * 2 + 2);
        Require(Contains(lines, "reason=binding-disabled"), "disabled binding reason");
        Require(Contains(lines, "unavailable:InvalidOperationException"), "metadata failure isolated");
        pads.Remove(xbox);
        diagnostics.ObservePads(pads, Stopwatch.Frequency * 2 + 3);
        Require(Contains(lines, "connected=False wasJumpDown=True"), "disconnect during hold");
        Require(diagnostics.ActiveDevices == "none", "disconnect clears active source");
        Require(keyboard.RawReads + xboxWrapper.RawReads + unknown.RawReads == 0, "no physical polling");

        // Actual installed game's legacy type, without opening hardware.
        Type legacyType = typeof(PadInstance).Assembly.GetType("JumpKing.Controller.LegacyPad", true);
        IPad legacy = (IPad)FormatterServices.GetUninitializedObject(legacyType);
        pads.Add(new PadInstance(legacy, new PadBinding { jump = new[] { 3 } }));
        diagnostics.ObservePads(pads, Stopwatch.Frequency * 3 + 4);
        Require(Contains(lines, "backend=legacy-directinput"), "native legacy classification");
        Require(Contains(lines, "reason=directinput-id-unavailable"), "legacy reason even without hardware metadata");
        DiagnosticLog.Write("diagnostic test flush sentinel");
        DiagnosticLog.Flush();
        string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubframeCharge.log");
        Require(System.IO.File.ReadAllText(path).Contains("diagnostic test flush sentinel"), "queued log flush");
        string rotation = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "rotation-test-" + Guid.NewGuid().ToString("N") + ".log");
        for (int i = 0; i < 4; i++)
        {
            using (System.IO.FileStream file = System.IO.File.Create(rotation)) file.SetLength(DiagnosticLog.MaximumBytes);
            DiagnosticLog.Rotate(rotation);
        }
        Require(System.IO.File.Exists(rotation + ".1") && System.IO.File.Exists(rotation + ".2")
            && !System.IO.File.Exists(rotation + ".3"), "bounded rotation generations");
        Require(System.IO.File.ReadAllText(rotation).Contains("buildId="), "rotation retains session/build identity");
        Console.WriteLine("[OK] Input diagnostics: devices, bindings, source changes, errors, no polling, log flush");
    }

    private static void TestBindingCache(List<PadInstance> pads)
    {
        ControllerManager previous = ControllerManager.instance;
        ControllerManager manager = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
        typeof(ControllerManager).GetField("m_pads", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, pads);
        ControllerManager.instance = manager;
        try
        {
            BindingSnapshot snapshot = new BindingSnapshot();
            long now = Stopwatch.GetTimestamp();
            Require(!snapshot.Unchanged(now) && snapshot.Unchanged(now + 1), "unchanged bindings should use cached physical resolution");
            int original = pads[0].GetBind().jump[0];
            pads[0].GetBind().jump[0] = original + 1;
            Require(!snapshot.Unchanged(now + 2), "in-place rebind must invalidate immediately");
            pads[0].GetBind().jump[0] = original;
            Require(!snapshot.Unchanged(now + 3), "restored rebind must invalidate immediately");
            pads[0].GetBind().Enabled = false;
            Require(!snapshot.Unchanged(now + 4), "disabled binding must invalidate immediately");
            pads[0].GetBind().Enabled = true;
            Require(!snapshot.Unchanged(now + 5), "enabled binding must invalidate immediately");
            Require(!snapshot.Unchanged(now + Stopwatch.Frequency), "opaque physical mappings must be periodically rediscovered");
        }
        finally { ControllerManager.instance = previous; }
    }

    private static void SetJump(PadInstance pad, bool down)
    {
        typeof(PadInstance).GetField("current_state", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(pad, new PadState { jump = down });
    }

    private static bool Contains(List<string> lines, string expected)
    {
        return lines.Exists(line => line.Contains(expected));
    }

    private static void Require(bool value, string description)
    {
        if (!value) throw new Exception("Input diagnostics: " + description);
    }
}
