using System;
using System.IO;
using System.Linq;
using JumpKing;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmoothCamera;
using JumpKing.GameManager;
using JumpKing.Level;

internal static partial class CameraTests
{
    private static void PolicyRenderFixture(GraphicsDevice device, RenderTarget2D target, JumpGame jump)
    {
        var settings = Settings.Current; var rules = MapPolicy.Current;
        try
        {
            Settings.Current = new CameraSettings { Smooth = false };
            MapPolicy.Current = Rules("<SmoothCamera><Screens from='1' to='2' mode='smooth'/></SmoothCamera>", 2, strict: true);
            Renderer.SettingsChanged();
            RenderFrame(device, target, jump);
            Check(Renderer.Decision.Forced && Renderer.HighRefreshRequested && !Settings.Current.Smooth, "Real renderer forces camera and cadence without changing Enable");
            Check(Read(target)[100 * 480 + 10] == Color.DarkRed, "Forced camera stitches adjacent screens while disabled");
            MapPolicy.Current = Rules("<SmoothCamera><Screens from='1' mode='smooth'/><Screens from='2' mode='native'/></SmoothCamera>", 2, strict: true);
            Renderer.SettingsChanged(); RenderFrame(device, target, jump);
            Check(Renderer.Decision.Forced && Read(target)[100 * 480 + 10] == Color.DarkBlue, "Forced isolated screen never previews native neighbor");
            MapPolicy.Current = Rules("<SmoothCamera><Zone id='force' screen='1' x='200' y='0' width='80' height='100' mode='smooth'/></SmoothCamera>", 2, restricted: true);
            Renderer.SettingsChanged(); RenderFrame(device, target, jump);
            Check(Renderer.Decision.Forced, "Real renderer resolves forced rectangle using king center");
            var position = GameLoop.m_player.m_body.Position;
            GameLoop.m_player.m_body.Position = new Vector2(300, position.Y);
            RenderFrame(device, target, jump);
            Check(!Renderer.Decision.Active && !Renderer.HighRefreshRequested && !Settings.Current.Smooth, "Leaving forced zone restores Off and releases high-refresh request");
            GameLoop.m_player.m_body.Position = position;
            ForcedPortalTransition();
        }
        finally { Settings.Current = settings; MapPolicy.Current = rules; Renderer.SettingsChanged(); }
    }
    private static void ForcedPortalTransition()
    {
        var screens = Renderer.Screens.GetValue(null); var count = typeof(LevelManager).GetField("_total_screens", Flags);
        object oldCount = count.GetValue(null); int nativeScreen = (int)RenderContext.NativeScreen.GetValue(null);
        var position = GameLoop.m_player.m_body.Position;
        var rules = MapPolicy.Current;
        try
        {
            Settings.Current.Horizontal = true;
            Renderer.Screens.SetValue(null, new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2) }); count.SetValue(null, 3);
            MapPolicy.Current = Rules("<SmoothCamera><Screens from='1' mode='smooth'/><Screens from='2' mode='native'/><Screens from='3' mode='smooth'/></SmoothCamera>", 3, strict: true);
            Renderer.SettingsChanged(); RenderContext.NativeScreen.SetValue(null, 0);
            GameLoop.m_player.m_body.Position = new Vector2(469, 150);
            for (int i = 0; i < 180; i++) { Renderer.AfterUpdate(); Renderer.Horizontal.Advance(1f / 60); }
            float previous = 479 + Renderer.Horizontal.Translation;
            RenderContext.NativeScreen.SetValue(null, 2); GameLoop.m_player.m_body.Position = new Vector2(-9, 150 - 720);
            Renderer.AfterUpdate();
            Near(1 + Renderer.Horizontal.Translation - previous, 2, "Separate forced ranges preserve continuous portal rebase while Enable is Off");
            Check(Renderer.Horizontal.Left == 0 && Renderer.Motion.FirstScreen == 2 && Renderer.Motion.LastScreen == 2,
                "Forced one-way arrival retains departure and excludes native vertical neighbor");
        }
        finally
        {
            Settings.Current.Horizontal = false; Renderer.Screens.SetValue(null, screens); count.SetValue(null, oldCount);
            RenderContext.NativeScreen.SetValue(null, nativeScreen); GameLoop.m_player.m_body.Position = position;
            MapPolicy.Current = rules; Renderer.SettingsChanged();
        }
    }
    private static void SettingsMenuPreview(GraphicsDevice device, RenderTarget2D target, string output)
    {
        var stack = new UiPageStack(p => new CameraSettingsPage(p));
        var page = new CameraSettingsPage(stack);
        var initial = Settings.Current;
        page.OnOpen();
        try
        {
            foreach (string section in new[] { "General", "Vertical", "Horizontal", "Controls", "Map rules" })
            {
                page.Section = section;
                for (int i = 0; i < 120; i++) page.Update(new UiInput(), 1f / 60);
                device.SetRenderTarget(target); device.Clear(Color.Black); Game1.instance.StartBatch();
                page.Draw(); Game1.instance.EndBatch(); device.SetRenderTarget(null);
                Save(target, Path.Combine(output, "settings-" + section.ToLowerInvariant().Replace(' ', '-') + ".png"));
                var pixels = Read(target);
                Check(pixels.Any(c => c == UiTheme.Gold) && pixels.Any(c => c == UiTheme.Cyan), "Settings page draws native text and window preview: " + section);
            }
            Check(ReferenceEquals(initial, Settings.Current), "Settings preview never commits the draft");
            foreach (var action in CameraControls.Actions)
            {
                var binding = UIApi.GetBindings().Single(b => b.Id == CameraSettingsPage.DraftBinding(action));
                binding.Mode.Value = UiBindingMode.Press;
                binding.SetChords(new[] { new UiChord(85) });
                Check(page.Draft.Trigger(action) == CameraTrigger.Press && Settings.Current.Trigger(action) == initial.Trigger(action),
                    "Mode selector edits only the settings draft");
                Check(page.Draft.Bindings(action).Count > 0 && binding.GetChords()[0].Buttons[0] == 85,
                    "Draft rebinding applies independently to all camera actions");
            }

            page.Draft.Vertical.Focus = .7f;
            page.Update(new UiInput { Action = UiAction.Cancel }, 0);
            Check(page.WantsClose && Settings.Current.Vertical.Focus != .7f, "Cancel leaves saved focus unchanged");
        }
        finally { page.OnClose(); }
        Check(!UIApi.GetBindings().Any(b => CameraControls.Actions.Any(a => b.Id == CameraSettingsPage.DraftBinding(a))),
            "Closing settings releases every draft binding and mode provider");

        var profile = new CameraProfile(); double number = 50;
        var numeric = new CameraNumberPage("Focus (%)", "Percent from the top. 60% leaves more room above the king.", () => number, n => { number = n; profile.Vertical.Focus = (float)n / 100; }, 15, 85, 1, () => profile);
        numeric.OnOpen();
        numeric.Update(new UiInput { Action = UiAction.Right }, 1f / 60);
        Check(number == 51, "Numeric page edits through shared control");
        for (int i = 0; i < 120; i++) numeric.Update(new UiInput(), 1f / 60);
        device.SetRenderTarget(target); device.Clear(Color.Black); Game1.instance.StartBatch(); numeric.Draw(); Game1.instance.EndBatch(); device.SetRenderTarget(null);
        Save(target, Path.Combine(output, "settings-focus-editor.png"));
        numeric.Update(new UiInput { Action = UiAction.Cancel }, 0); numeric.OnClose();
        Check(number == 50, "Closing numeric editor cancels its draft adjustment");
        var keep = new CameraNumberPage("Focus (%)", "", () => number, n => number = n, 15, 85, 1, () => profile);
        keep.OnOpen(); keep.Update(new UiInput { Action = UiAction.Right }, 0); keep.Update(new UiInput { Action = UiAction.Confirm }, 0); keep.OnClose();
        Check(number == 51, "Keep accepts numeric value into draft");
    }
}
