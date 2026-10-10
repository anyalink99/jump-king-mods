using System;
using System.IO;
using System.Xml.Serialization;
using SmoothCamera;
using JKRuntime.Settings;
using JKRuntime.UI;
using ModEntry = SmoothCamera.ModEntry;

internal static partial class CameraTests
{
    private static MapRules Rules(string xml, int count = 6, bool restricted = false, bool strict = false)
    { return MapRules.Parse(xml == null ? null : new StringReader(xml), count, restricted, strict); }
    private static void RejectRules(string xml)
    {
        bool rejected = false;
        try { Rules(xml); } catch { rejected = true; }
        Check(rejected, "Invalid map contract is rejected: " + xml);
    }
    private static void SettingsPolicyTests()
    {
        ManualControlPolicyTests();
        var serializer = new XmlSerializer(typeof(CameraSettings));
        CameraSettings old;
        using (var text = new StringReader("<CameraSettings><Smooth>false</Smooth><Horizontal>true</Horizontal><HighRefresh>false</HighRefresh></CameraSettings>"))
            old = (CameraSettings)serializer.Deserialize(text);
        CameraSettings.Validate(old);
        Check(!old.Smooth && old.Horizontal && !old.HighRefresh && old.Vertical.NegativeBand == 12 && old.Vertical.PositiveBand == 64,
            "Legacy settings retain switches and Classic tracking");
        Check(old.UpTrigger == CameraTrigger.Hold && old.DownTrigger == CameraTrigger.Hold && old.FocusTrigger == CameraTrigger.Hold,
            "Legacy XML adopts Hold for all three actions");
        var modes = Settings.WithBinding(old, CameraMode.Up, "keyboard", new[] { new UiChord(85) });
        modes = Settings.WithBinding(modes, CameraMode.Down, "controller", new UiChord[0]);
        modes.UpTrigger = CameraTrigger.Press; modes.DownTrigger = CameraTrigger.Both;
        string modesXml; using (var writer = new StringWriter()) { serializer.Serialize(writer, modes); modesXml = writer.ToString(); }
        CameraSettings restored; using (var reader = new StringReader(modesXml)) restored = (CameraSettings)serializer.Deserialize(reader);
        Check(restored.UpTrigger == CameraTrigger.Press && restored.DownTrigger == CameraTrigger.Both && restored.FocusTrigger == CameraTrigger.Hold
            && restored.UpBindings[0].Chords[0].Buttons[0] == 85 && restored.DownBindings[0].Chords.Count == 0,
            "Modes, custom directions and explicit device clears survive reload");
        var modeDraft = restored.Copy(); modeDraft.UpBindings[0].Chords[0].Buttons[0] = 99; modeDraft.DownTrigger = CameraTrigger.Hold;
        Check(restored.UpBindings[0].Chords[0].Buttons[0] == 85 && restored.DownTrigger == CameraTrigger.Both, "New binding and mode drafts are isolated");
        var source = Settings.WithFocus(old, "keyboard", new[] { new UiChord(12, 13) });
        source.Vertical.LookAhead = 22; source.MapProfiles.Add(new SavedMapProfile { Key = "map", Profile = CameraProfile.FromPreset("Centered") });
        var draft = source.Copy(); draft.Vertical.LookAhead = 55; draft.MapProfiles[0].Profile.Vertical.Focus = .7f;
        draft.FocusBindings[0].Chords[0].Buttons[0] = 99;
        Near(source.Vertical.LookAhead, 22, "Draft axis edits are isolated");
        Near(source.MapProfiles[0].Profile.Vertical.Focus, .5f, "Draft map edits are isolated");
        Check(source.FocusBindings[0].Chords[0].Buttons[0] == 12, "Draft binding edits are isolated");
        var rebound = Settings.WithFocus(source, "keyboard", new[] { new UiChord(14) });
        Check(rebound.MapProfiles.Count == 1 && rebound.Vertical.LookAhead == 22 && rebound.Horizontal && !rebound.HighRefresh, "Rebinding preserves all extended settings");
        var suspended = Settings.DisableForMap(source, "strict-map");
        Check(!suspended.Smooth && suspended.AutomaticallyDisabled && suspended.DisabledByMap == "strict-map", "Strict entry produces persistent disabled state");
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-policy-" + Guid.NewGuid().ToString("N") + ".xml");
        var file = new SettingsFile<CameraSettings>(path, () => new CameraSettings(), CameraSettings.Validate);
        file.Save(suspended);
        var loaded = new SettingsFile<CameraSettings>(path, () => new CameraSettings(), CameraSettings.Validate);
        Check(!loaded.Value.Smooth && loaded.Value.AutomaticallyDisabled && loaded.Value.MapProfiles.Count == 1, "Strict disable and map profiles survive process reload");
        var invalid = loaded.Value.Copy(); invalid.Vertical.Focus = float.NaN;
        bool badSettings = false; try { loaded.Save(invalid); } catch { badSettings = true; }
        Check(badSettings && !new SettingsFile<CameraSettings>(path, () => new CameraSettings(), CameraSettings.Validate).Value.Smooth, "Invalid numeric settings do not overwrite persisted data");
        File.Delete(path);

        var settings = new CameraSettings();
        var plain = Rules(null);
        Check(plain.Resolve(settings, 2, 240, 180, 6).Active, "Unmarked map follows Enable");
        var soft = Rules("<SmoothCamera><Screens from='2' to='4' mode='smooth'/><Screens from='5' mode='native'/></SmoothCamera>", restricted: true);
        Check(!soft.Resolve(settings, 0, 240, 180, 6).Active && soft.Resolve(settings, 1, 240, 180, 6).Active, "Soft policy suppresses unmarked screens");
        settings.Smooth = false;
        var forced = soft.Resolve(settings, 2, 240, 180, 6);
        Check(forced.Active && forced.Forced && forced.First == 1 && forced.Last == 3, "Forced range overrides manual Off and limits coverage");
        Check(!soft.Resolve(settings, 4, 240, 180, 6).Active && !settings.Smooth, "Leaving forced range restores manual Off without rewriting preference");
        var hard = Rules("<SmoothCamera><Screens from='2' mode='smooth'/></SmoothCamera>", strict: true);
        Check(hard.Resolve(settings, 1, 240, 180, 6).Active && !hard.Resolve(settings, 0, 240, 180, 6).Active, "Forced screen overrides persistent map disable");
        Check(!plain.Resolve(suspended, 1, 240, 180, 6).Active, "Strict disable carries into ordinary maps");
        settings.Smooth = true;
        Check(!hard.Resolve(settings, 0, 240, 180, 6).Active && plain.Resolve(settings, 0, 240, 180, 6).Active, "Strict map cannot be bypassed by stored Enable");
        var zones = Rules("<SmoothCamera><Screens from='2' mode='native'/><Zone id='force' screen='2' x='50' y='40' width='150' height='100' mode='smooth'/><Zone id='quiet' screen='2' x='100' y='40' width='20' height='20' priority='2' mode='native'/></SmoothCamera>");
        Check(zones.Resolve(settings, 1, 50, 40, 6).Forced, "Zone includes top/left edges");
        Check(!zones.Resolve(settings, 1, 200, 40, 6).Active && !zones.Resolve(settings, 1, 70, 140, 6).Active, "Zone excludes bottom/right edges");
        Check(!zones.Resolve(settings, 1, 105, 45, 6).Active, "Higher priority native zone wins over forced zone");
        var isolated = zones.Resolve(settings, 1, 70, 60, 6);
        Check(isolated.First == 1 && isolated.Last == 1, "Zone exception on native screen never exposes its neighbors");
        var recommended = Rules("<SmoothCamera><Profile id='shaft'><Settings><Vertical mode='Direct' focus='0.6'/></Settings></Profile><Screens from='1' to='6' profile='shaft'/></SmoothCamera>");
        recommended.Key = "shaft-map";
        Near(recommended.Resolve(settings, 2, 20, 50, 6).Profile.Vertical.Focus, .6f, "Author profile is applied when requested");
        settings.UseMapRecommendations = false;
        Near(recommended.Resolve(settings, 2, 20, 50, 6).Profile.Vertical.Focus, .5f, "Author profile can be declined without dropping rules");
        settings.UseMapRecommendations = true;
        settings.MapProfiles.Add(new SavedMapProfile { Key = "shaft-map", Profile = new CameraProfile { Vertical = new AxisSettings { Focus = .7f } } });
        Near(recommended.Resolve(settings, 2, 20, 50, 6).Profile.Vertical.Focus, .7f, "Personal map profile takes priority over recommendation");
        Check(ReferenceEquals(settings.ForMap("another-map"), settings), "Personal profile cannot leak into another map");
        foreach (var xml in new[] {
            "<SmoothCamera version='2'/>", "<SmoothCamera><Screens from='0'/></SmoothCamera>",
            "<SmoothCamera><Screens from='1' to='7'/></SmoothCamera>",
            "<SmoothCamera><Screens from='1' to='3'/><Screens from='2'/></SmoothCamera>",
            "<SmoothCamera><Screens from='1' profile='missing'/></SmoothCamera>",
            "<SmoothCamera><Zone id='bad' screen='1' x='470' y='0' width='20' height='20'/></SmoothCamera>",
            "<SmoothCamera><Zone id='a' screen='1' x='0' y='0' width='20' height='20'/><Zone id='b' screen='1' x='1' y='1' width='20' height='20'/></SmoothCamera>",
            "<SmoothCamera><Profile id='bad'><Settings><Vertical focus='NaN'/></Settings></Profile></SmoothCamera>",
            "<SmoothCamera><Screens from='1' typo='smooth'/></SmoothCamera>",
            "<!DOCTYPE SmoothCamera [<!ENTITY v '1'>]><SmoothCamera version='&v;'/>",
            "<SmoothCamera><Screens from='1' mode='force'/></SmoothCamera>" }) RejectRules(xml);
        Settings.Load(); var before = Settings.Current; var beforeRules = MapPolicy.Current;
        try
        {
            Settings.Current = new CameraSettings { Smooth = false }; MapPolicy.Current = soft;
            Check(MapPolicy.NeedsHooks, "Forced areas retain observation/render hooks when Enable is Off");
            MapPolicy.Current = plain; Check(!MapPolicy.NeedsHooks, "Ordinary disabled maps need no camera hooks");
            MapPolicy.Current = Rules("<SmoothCamera><Screens from='1' mode='smooth'/><Screens from='3' mode='native'/></SmoothCamera>");
            var portal = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2, 1) };
            Check(MapPolicy.PortalDestination(portal, 0, true) < 0, "Forced source cannot expose a native portal destination");
        }
        finally { Settings.Current = before; MapPolicy.Current = beforeRules; }
        ForcedStartupTests();
        int entries = 0;
        Action<string> disableEntry = key => entries++;
        MapPolicy.Adopt(hard, "strict-map", disableEntry); MapPolicy.Adopt(hard, "STRICT-MAP", disableEntry);
        Check(entries == 1, "Restart and path casing do not repeat persistent disabling");
        MapPolicy.Adopt(plain, "ordinary-map", disableEntry); MapPolicy.Adopt(hard, "strict-map", disableEntry);
        Check(entries == 2, "Returning from another map is a fresh strict entry");
        MapPolicy.Reset();
        string example = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../mods/smooth-camera/docs/examples/camera.xml"));
        using (var reader = File.OpenText(example)) Check(MapRules.Parse(reader, 6, true, false).HasForced, "Shipped author example loads and forces its range");
        Console.WriteLine("[OK] Camera settings migration, draft isolation, persistence, forced rules, zones and author/personal profiles");
    }

    private static void ManualControlPolicyTests()
    {
        var settings = new CameraSettings { UseMapRecommendations = false };
        var plain = Rules(null).Resolve(settings, 0, 20, 20, 3);
        Check(plain.AllowFocus && plain.AllowLook, "Existing maps keep both manual camera controls");
        var rules = Rules("<SmoothCamera allow-focus='false'><Screens from='1' mode='smooth' allow-look='false'/>"
            + "<Zone id='still-locked' screen='1' x='10' y='10' width='50' height='50' allow-focus='true' allow-look='true'/>"
            + "<Zone id='no-look' screen='2' x='10' y='10' width='50' height='50' allow-look='false'/></SmoothCamera>");
        var locked = rules.Resolve(settings, 0, 20, 20, 6);
        Check(locked.Active && !locked.AllowFocus && !locked.AllowLook, "Map and screen restrictions survive zone allowances and declined recommendations");
        Check(!rules.Resolve(settings, 1, 20, 20, 6).AllowLook && rules.Resolve(settings, 1, 100, 100, 6).AllowLook,
            "Camera zone restriction ends at its boundary");
        Check(!rules.Resolve(settings, 2, 20, 20, 6).AllowFocus, "Root camera restriction applies across screens");
        RejectRules("<SmoothCamera allow-focus='yes'/>");
        RejectRules("<SmoothCamera><Screens from='1' allow-look='no'/></SmoothCamera>");

        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var clear = typeof(JKRuntime.Gameplay.MapMechanics).GetMethod("ClearWorld", flags);
        var parse = typeof(JKRuntime.Gameplay.MapMechanics).GetMethod("Parse", flags);
        clear.Invoke(null, null);
        try
        {
            ModEntry.Prepare();
            var xml = System.Xml.Linq.XElement.Parse("<Mechanics version='1'><Mechanic id='smooth-camera.tracking'>"
                + "<Screens from='1' mode='on'><Parameter name='allow-focus' value='false'/><Parameter name='allow-look' value='false'/></Screens>"
                + "<Zone id='look' screen='1' x='10' y='10' width='50' height='50'><Parameter name='allow-look' value='true'/></Zone>"
                + "<Screens from='2' mode='local'/><Zone id='quiet' screen='2' x='10' y='10' width='50' height='50'><Parameter name='allow-focus' value='false'/></Zone>"
                + "</Mechanic></Mechanics>");
            parse.Invoke(null, new object[] { xml, new[] { "JKRuntime.MapControlled:smooth-camera.tracking" }, 6 });
            var common = Rules(null);
            var outside = common.Resolve(settings, 0, 100, 100, 6);
            var inside = common.Resolve(settings, 0, 20, 20, 6);
            Check(outside.Active && !outside.AllowFocus && !outside.AllowLook, "Common XML screen restricts both controls");
            Check(inside.Active && !inside.AllowFocus && inside.AllowLook, "Common XML zone overrides only its named control parameter");
            Check(!rules.Resolve(settings, 0, 20, 20, 6).AllowLook, "Common allowance cannot bypass camera.xml denial");
            var local = common.Resolve(settings, 1, 20, 20, 6);
            Check(local.Active && !local.AllowFocus && local.AllowLook && !common.Resolve(settings, 1, 100, 100, 6).Active,
                "Common Local zone applies restrictions only inside permitted area");
            bool rejected = false;
            try { JKRuntime.Gameplay.MapMechanics.DeclareScreen(MapPolicy.MechanicId, 0, JKRuntime.Gameplay.MapMechanicMode.On, "invalid", "allow-focus", "yes"); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Common camera parameters reject invalid boolean values");
            clear.Invoke(null, null);
            // installed Restless Knight policy, including the native gap
            xml = System.Xml.Linq.XElement.Parse("<Mechanics version='1'><Mechanic id='smooth-camera.tracking'>"
                + "<Screens from='1' to='8' mode='on'><Parameter name='allow-focus' value='false'/><Parameter name='allow-look' value='false'/></Screens>"
                + "<Screens from='11' to='24' mode='on'><Parameter name='allow-focus' value='false'/><Parameter name='allow-look' value='false'/></Screens>"
                + "</Mechanic></Mechanics>");
            parse.Invoke(null, new object[] { xml, new[] { "JKRuntime.MapControlled:smooth-camera.tracking" }, 24 });
            common = MapRules.Parse(null, 24, false, true);
            foreach (CameraTrigger trigger in Enum.GetValues(typeof(CameraTrigger)))
            foreach (CameraMode action in CameraControls.Actions)
            {
                var gesture = new CameraGestures(); gesture.SetTriggers(trigger, trigger, trigger);
                Action<bool, double> input = (held, time) => gesture.Update(held && action == CameraMode.Up,
                    held && action == CameraMode.Down, held && action == CameraMode.Focus, time, true);
                input(false, 0); input(true, 1); input(false, 1.1);
                for (int screen = 0; screen < 24; screen++)
                {
                    var decision = common.Resolve(settings, screen, 100, 100, 24);
                    bool smooth = screen < 8 || screen >= 10;
                    Check(decision.Active == smooth, "Restless Knight keeps authored smooth ranges and native gap");
                    gesture.SetPermissions(decision.AllowFocus, decision.AllowLook);
                    input(false, 2); input(true, 3); input(true, 3.5); input(false, 4);
                    if (smooth) Check(!decision.AllowFocus && !decision.AllowLook && gesture.Current == CameraMode.Normal
                        && gesture.Latched == CameraMode.Normal, "Map restrictions override " + trigger + " " + action + " on screen " + (screen + 1));
                }
            }
        }
        finally { clear.Invoke(null, null); CameraControls.Unload(); }
    }

    private static void ForcedStartupTests()
    {
        var original = Settings.Current;
        try
        {
            Settings.Current = new CameraSettings { Smooth = false };
            using (var world = new JKRuntime.RuntimeScope())
            {
                ModEntry.PrepareWorld(world);
                MapPolicy.Current = Rules("<SmoothCamera><Screens from='1' mode='smooth'/></SmoothCamera>");
                using (var attempt = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareAttempt(attempt);
                    Check(Hooks.Installed && !Renderer.Running && !Renderer.HighRefreshRequested, "Forced map prepares dormant hooks with Enable Off");
                    ModEntry.Start(); Settings.ApplyPresentationSetting();
                    Check(Hooks.Installed && !Settings.Current.Smooth, "Off setting retains hooks needed by forced areas");
                    ModEntry.End();
                }
                Check(Hooks.Installed && !Renderer.Running, "Forced map retains hooks across attempts without rendering intro");
            }
            Check(!Hooks.Installed && !MapPolicy.Current.HasForced, "World exit releases forced policy and hooks");
        }
        finally { Settings.Current = original; MapPolicy.Reset(); }
    }

    private static void ConfigurableMotionTests()
    {
        foreach (int rate in new[] { 60, 144, 240 })
        {
            var direct = new CameraMotion { Options = new AxisSettings { Mode = FollowMode.Direct, Focus = .6f } };
            direct.Step(-400, 10, 0, false);
            for (int i = 0; i < rate * 2; i++) direct.Step(-500, 10, 1f / rate, false);
            Near(direct.Translation - 500, 216, "Direct focus uses desired height at " + rate);
            var window = new CameraMotion { Options = new AxisSettings { Mode = FollowMode.Window, Window = 100 } };
            window.Step(-400, 10, 0, false); float origin = window.Translation;
            for (int i = 0; i < rate; i++) window.Step(-440, 10, 1f / rate, false);
            Near(window.Translation, origin, "Inside window leaves camera still at " + rate);
            for (int i = 0; i < rate * 2; i++) window.Step(-520, 10, 1f / rate, false);
            Near(window.Translation, 650, "Window catches its upper edge at " + rate);
            window.Observe(-600, 10, false); window.Advance(1f / rate);
            float windowStop = window.Translation;
            window.Observe(180 - windowStop, 10, false);
            for (int i = 0; i < rate; i++) window.Advance(1f / rate);
            Near(window.Translation, windowStop, "Window stops pulling after the king returns inside at " + rate);
            var returning = new CameraMotion { Options = new AxisSettings { Recenter = true, RecenterDelay = .5f } };
            returning.Step(-400, 10, 0, false);
            for (int i = 0; i < rate * 3; i++) returning.Step(-430, 10, 1f / rate, false);
            Near(returning.Translation, 610, "Optional idle return centers after delay at " + rate);
            var ahead = new AxisMotion { Options = new AxisSettings { Mode = FollowMode.Direct, LookAhead = 40, LookDelay = .2f } };
            ahead.Observe(-400, -100, 360, 0, 3600, 360, false, false);
            for (int i = 0; i < rate * 3; i++) ahead.Advance(1f / rate);
            Near(ahead.Value, 620, "Look ahead creates room in travel direction at " + rate);
            float previous = ahead.Value;
            ahead.Observe(-400, 100, 360, 0, 3600, 360, false, false);
            for (int i = 0; i < rate / 10; i++) ahead.Advance(1f / rate);
            Near(ahead.Value, previous, "Direction debounce ignores brief reversals at " + rate);
        }
        var native = new CameraMotion { Options = new AxisSettings { Mode = FollowMode.Screen } };
        native.Observe(-430, 10, false, 0, 2); native.Advance(1f / 60);
        Near(native.Translation, 720, "Screen mode uses actual native screen, not player-derived guess");
        var limited = new CameraMotion { FirstScreen = 2, LastScreen = 4 };
        limited.Step(1000, 10, 0, false); Near(limited.Translation, 720, "Authored lower boundary wins over focus");
        limited.Step(-5000, 10, 0, false); Near(limited.Translation, 1440, "Authored upper boundary wins over teleport snap");
        var slow = new CameraMotion { Options = new AxisSettings { Mode = FollowMode.Direct, NegativeResponse = 2, PositiveResponse = 40 } };
        slow.Step(-400, 10, 0, false); slow.Observe(-430, 10, false, -20); slow.Advance(.1f); float ascent = slow.Translation - 580;
        slow.Reset(); slow.Step(-400, 10, 0, false); slow.Observe(-370, 10, false, 20); slow.Advance(.1f); float descent = 580 - slow.Translation;
        Check(descent > ascent * 2, "Positive and negative response are independent");
        var view = new CameraView(); view.TransitionFrom(0, 720);
        view.Advance(CameraMode.Normal, 0, 800, -600, 0, 2, 10, 1f / 60, false);
        Check(view.Y > 720 && view.Y < 800, "Smooth region entry starts from prior safe framing");
        var gestures = new CameraGestures { HoldThreshold = .5 };
        gestures.SetTriggers(CameraTrigger.Both, CameraTrigger.Both, CameraTrigger.Both);
        gestures.Update(false, false, false, 0, true); gestures.Update(true, false, false, .1, true);
        gestures.Update(false, false, false, .45, true);
        Check(gestures.Latched == CameraMode.Up, "Custom hold threshold classifies a 350ms press as a tap");
        Console.WriteLine("[OK] Configurable axes: focus, window, screen mode, response, anticipation, recenter, boundaries and gestures");
    }
}
