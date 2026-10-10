using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void DebugDragTests()
    {
        var viewport = new DebugViewport(new Rectangle(120, 60, 960, 720), new Point(1200, 900), new Point(1200, 900));
        var profile = new AxisSettings { Mode = FollowMode.Window, Window = 100, Focus = .6f };
        var state = new DebugDragState();
        var view = new Vector2(0, 720);
        var mouse = viewport.Client(new Vector2(200, 200));
        var world = state.Move(mouse, viewport, view, 16);
        Near(world.X, 200, "Debug click uses logical X at enlarged resolution");
        Near(world.Y, -520, "Debug click inverts the visible camera, not the native screen");
        Near(DebugDragState.Frame(view.Y, world.Y, profile, 360, 0, 5400, 720), 720, "Dragging inside the window leaves the camera still");

        mouse.Y -= 100;
        world = state.Move(mouse, viewport, view, 16);
        view.Y = DebugDragState.Frame(view.Y, world.Y, profile, 360, 0, 5400, 720);
        Near(world.Y, -570, "Mouse delta moves the king by the same logical distance");
        Near(world.Y + view.Y, 166, "Upward drag holds the configured upper window edge");
        mouse = state.Reproject(mouse, viewport, view);
        Check(mouse == viewport.Client(world + view), "Cursor follows the king when the camera moves");
        for (int tick = 0; tick < 240; tick++)
        {
            state.Move(mouse, viewport, view, 16);
            view.Y = DebugDragState.Frame(view.Y, state.World.Y, profile, 360, 0, 5400, 720);
            for (int frame = 0; frame < 4; frame++) mouse = state.Reproject(mouse, viewport, view);
        }
        Near(state.World.Y, world.Y, "Stationary mouse at the edge cannot feed camera movement back into teleporting");
        mouse.Y -= 200;
        state.Move(mouse, viewport, view, 16);
        view.Y = DebugDragState.Frame(view.Y, state.World.Y, profile, 360, 0, 5400, 720);
        Near(state.World.Y, world.Y - 100, "Further physical movement keeps dragging upward");
        Near(state.World.Y + view.Y, 166, "Cursor stays at the upper edge through continued dragging");
        mouse = state.Reproject(mouse, viewport, view);
        mouse.Y += 40;
        state.Move(mouse, viewport, view, 16);
        float previous = view.Y;
        view.Y = DebugDragState.Frame(view.Y, state.World.Y, profile, 360, 0, 5400, 720);
        Near(view.Y, previous, "Reversing into the window immediately stops camera movement");
        mouse = state.Reproject(mouse, viewport, view);
        mouse.Y += 220;
        state.Move(mouse, viewport, view, 16);
        view.Y = DebugDragState.Frame(view.Y, state.World.Y, profile, 360, 0, 5400, 720);
        Near(state.World.Y + view.Y, 266, "Downward drag holds the configured lower edge");

        state.Reset(); view = new Vector2(0, 720);
        mouse = viewport.Client(new Vector2(240, 50));
        state.Move(mouse, viewport, view, 16);
        Near(state.World.Y, -670, "Click outside the frame first chooses the visible world point");
        view.Y = DebugDragState.Frame(view.Y, state.World.Y, profile, 360, 0, 5400, 720);
        mouse = state.Reproject(mouse, viewport, view);
        Near(viewport.Logical(mouse).Y, 166, "Outside click relocates cursor with the camera");
        var pending = new Point(mouse.X + 6, mouse.Y - 12);
        var projected = state.Reproject(pending, viewport, view);
        state.Move(projected, viewport, view, 16);
        Near(state.World.X, 243, "Presentation retains horizontal motion after the input sample");
        Near(state.World.Y, -676, "Presentation retains vertical motion after the input sample");

        var fractional = new DebugViewport(new Rectangle(20, 30, 1001, 751), new Point(1500, 1000), new Point(1000, 700));
        state.Reset(); view = new Vector2(13.25f, 723.75f); mouse = new Point(210, 240);
        world = state.Move(mouse, fractional, view, 16);
        for (int i = 0; i < 1000; i++)
        {
            view.Y += .13f;
            mouse = state.Reproject(mouse, fractional, view);
            state.Move(mouse, fractional, view, 16);
        }
        Near(state.World.Y, world.Y, "Fractional camera and DPI rounding cannot accumulate world drift");
        Near(state.World.X, world.X, "Horizontal camera offset does not change the selected world point");

        var classic = new AxisSettings();
        Near(DebugDragState.Frame(720, -600, classic, 360, 0, 5400, 720), 768, "Classic upper band matches its displayed frame");
        Near(DebugDragState.Frame(720, -420, classic, 360, 0, 5400, 720), 664, "Classic lower band matches its displayed frame");
        Near(DebugDragState.Frame(0, 350, profile, 360, 0, 5400, 0), 0, "Bottom map boundary takes priority over framing");
        Near(DebugDragState.Frame(5400, -5390, profile, 360, 0, 5400, 5400), 5400, "Top map boundary takes priority over framing");
        Near(DebugDragState.Frame(720, -600, new AxisSettings { Mode = FollowMode.Direct }, 360, 0, 5400, 720), 780, "Direct mode pins dragging to focus");
        Near(DebugDragState.Frame(720, -600, new AxisSettings { Mode = FollowMode.Screen }, 360, 0, 5400, 1080), 1080, "Screen mode retains native framing");
        state.Reset();
        Check(!state.Active, "Release or lifecycle reset clears the drag");

        var motion = new CameraMotion { Options = profile };
        motion.Observe(-900, 16, false); motion.Place(1066);
        motion.Observe(-900, 16, false); motion.Advance(1f / 60);
        Near(motion.Translation, 1066, "Release inherits dragged framing without spring recoil");
        var cameraView = new CameraView();
        cameraView.Advance(CameraMode.Normal, 0, 720, -500, 0, 2, 16, 0, false);
        cameraView.Place(0, 1066, -900);
        cameraView.Advance(CameraMode.Normal, 0, 1066, -900, 0, 3, 16, 1f / 60, false);
        Near(cameraView.Y, 1066, "Release after a long drag does not trigger teleport centering");
    }

    private static void DebugDragNativeContract()
    {
        var update = AccessTools.Method(DebugDrag.Click.DeclaringType, "Update");
        var calls = PatchProcessor.GetOriginalInstructions(update).Select(x => x.operand).ToList();
        Check(calls.Contains(DebugDrag.Click), "Installed native debug update calls the patched Click method");
        Check(calls.IndexOf(DebugDrag.Click) < calls.IndexOf(AccessTools.Method(DebugDrag.Click.DeclaringType, "Scroll")),
            "Native wheel teleport still runs after debug dragging");
        Check(Harmony.GetPatchInfo(DebugDrag.Click).Owners.Contains(Hooks.Id), "Debug drag hook shares the camera lifecycle");
        DebugDragNativeFixture(update);
    }

    private static MouseState debugMouse;
    private static Point debugCursor;
    private static bool debugAvailable;
    private static bool debugWindowFocused;
    private static bool DebugMouseSample(ref MouseState __result) { __result = debugMouse; return false; }
    private static bool DebugAvailability(ref bool __result) { __result = debugAvailable; return false; }
    private static bool DebugWindowFocus(ref bool __result) { __result = debugWindowFocused; return false; }
    private static bool DebugCursorPosition(int x, int y) { debugCursor = new Point(x, y); return false; }
    private static bool DebugViewportFixture(ref DebugViewport __result)
    { __result = new DebugViewport(new Rectangle(0, 0, 960, 720), new Point(960, 720), new Point(960, 720)); return false; }
    private static MouseState DebugMouse(int x, int y, bool pressed)
    { return new MouseState(x, y, 0, pressed ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released); }

    private static void DebugDragNativeFixture(System.Reflection.MethodInfo update)
    {
        var fixture = new Harmony("smooth-camera.debug-fixture");
        var instance = typeof(Game1).GetField("_instance", Flags);
        var oldGame = instance.GetValue(null); var oldPlayer = GameLoop.m_player;
        var debugInstance = typeof(LevelDebugState).GetField("_instance", Flags);
        var oldDebug = debugInstance.GetValue(null);
        var oldScreens = Renderer.Screens.GetValue(null); var oldScreen = RenderContext.NativeScreen.GetValue(null);
        var game = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
        var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        typeof(BodyComp).GetField("m_width", Flags).SetValue(body, 18);
        typeof(BodyComp).GetField("m_height", Flags).SetValue(body, 26);
        var player = (PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity)); player.m_body = body;
        var teleport = (Component)FormatterServices.GetUninitializedObject(DebugDrag.Click.DeclaringType);
        typeof(Component).GetField("m_owner", Flags).SetValue(teleport, player);
        typeof(Entity).GetField("m_components", Flags).SetValue(player, new List<Component> { body, teleport });
        try
        {
            instance.SetValue(null, game); GameLoop.m_player = player;
            debugInstance.SetValue(null, FormatterServices.GetUninitializedObject(typeof(LevelDebugState)));
            Renderer.Screens.SetValue(null, new LevelScreen[16]); RenderContext.NativeScreen.SetValue(null, 2);
            typeof(Game1).GetField("_game_rect", Flags).SetValue(game, new Rectangle(0, 0, 960, 720));
            fixture.Patch(AccessTools.PropertyGetter(typeof(DebugDrag), "Available"), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("DebugAvailability", Flags)));
            fixture.Patch(AccessTools.PropertyGetter(typeof(DebugDrag), "WindowFocused"), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("DebugWindowFocus", Flags)));
            fixture.Patch(AccessTools.Method(typeof(DebugDrag), "Viewport"), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("DebugViewportFixture", Flags)));
            fixture.Patch(AccessTools.Method(typeof(Mouse), "GetState", new Type[0]), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("DebugMouseSample", Flags)));
            fixture.Patch(AccessTools.Method(typeof(Mouse), "SetPosition"), prefix: new HarmonyMethod(typeof(CameraTests).GetMethod("DebugCursorPosition", Flags)));
            Renderer.Start(); Renderer.Decision = new CameraDecision { Active = true, First = 0, Last = 15, Profile = new CameraProfile() };
            typeof(Renderer).GetField("canPresent", Flags).SetValue(null, true);
            Renderer.Motion.Options = new AxisSettings { Mode = FollowMode.Window, Focus = .6f, Window = 100 };
            Renderer.Motion.FirstScreen = 0; Renderer.Motion.LastScreen = 15;
            Renderer.View.Place(0, 720, -520);
            debugWindowFocused = debugAvailable = true; debugMouse = DebugMouse(480, 100, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -683, "Real native debug update places body under visible click");
            Near(body.Position.X, 231, "Real native debug update keeps hitbox centered under cursor");
            DebugDrag.AfterUpdate(); DebugDrag.Frame(720); DebugDrag.Present(game.GetGameRect());
            Check(debugCursor == new Point(480, 332), "Real presenter moves OS cursor to the configured edge");
            debugMouse = DebugMouse(debugCursor.X, debugCursor.Y, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -683, "Real native repeated click does not feed cursor warp back into body");
            debugMouse = DebugMouse(debugCursor.X, debugCursor.Y - 20, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -693, "Native held drag consumes physical mouse delta after warp");
            debugAvailable = false; debugMouse = DebugMouse(100, 100, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -693, "Focus loss or menu suppresses native placement too");
            Check(!DebugDrag.State.Active, "Unavailable input clears native drag state");
            debugAvailable = true; debugMouse = DebugMouse(480, 200, false);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Check(!DebugDrag.State.Active, "Button release ends native dragging");

            // Focus keeps the game's original click without taking over its view
            var gestures = CameraControls.Gestures;
            gestures.SetTriggers(CameraTrigger.Both, CameraTrigger.Both, CameraTrigger.Both);
            gestures.Update(false, false, false, 0, true);
            gestures.Update(false, false, true, 1, true);
            debugMouse = DebugMouse(480, 200, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -633, "Held Focus uses the native debug teleport");
            Check(!DebugDrag.State.Active, "Held Focus cannot start framing drag");
            gestures.Update(false, false, false, 1.1, true);
            debugMouse = DebugMouse(480, 300, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -583, "Latched Focus uses the native debug teleport");
            Check(!DebugDrag.State.Active, "Latched Focus cannot start framing drag");
            gestures.Reset();
            update.Invoke(teleport, new object[] { 1f / 60 });
            Check(DebugDrag.State.Active, "Normal mode can start dragging again");
            gestures.Update(false, false, false, 2, true); gestures.Update(false, false, true, 3, true);
            float oldView = Renderer.View.Y;
            Check(!DebugDrag.Frame(oldView), "Entering Focus during a drag yields to the camera view");
            Near(Renderer.View.Y, oldView, "Focus does not snap the view to the drag window");
            Check(!DebugDrag.State.Active, "Entering Focus clears the old drag");
            gestures.Reset(); update.Invoke(teleport, new object[] { 1f / 60 });
            gestures.Update(false, false, false, 4, true); gestures.Update(false, false, true, 5, true);
            debugCursor = new Point(-1, -1);
            DebugDrag.Present(game.GetGameRect());
            Check(debugCursor == new Point(-1, -1) && !DebugDrag.State.Active, "Focus prevents cursor warps even between update and presentation");
            gestures.Reset(); update.Invoke(teleport, new object[] { 1f / 60 });

            // stale MonoGame availability must not beat the real foreground window
            debugWindowFocused = false; debugCursor = new Point(-1, -1);
            DebugDrag.Present(game.GetGameRect());
            Check(debugCursor == new Point(-1, -1) && !DebugDrag.State.Active, "Background presentation cancels drag without moving the OS cursor");
            var beforeBackground = body.Position;
            debugMouse = DebugMouse(20, 20, true);
            typeof(Renderer).GetField("canPresent", Flags).SetValue(null, false);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Check(body.Position == beforeBackground, "Background guard also blocks the native camera fallback");
            typeof(Renderer).GetField("canPresent", Flags).SetValue(null, true);
            debugWindowFocused = true;
            update.Invoke(teleport, new object[] { 1f / 60 });
            Check(body.Position == beforeBackground && !DebugDrag.State.Active, "Activation click waits for release instead of grabbing the king");
            debugMouse = DebugMouse(20, 20, false); update.Invoke(teleport, new object[] { 1f / 60 });
            debugMouse = DebugMouse(480, 200, true); update.Invoke(teleport, new object[] { 1f / 60 });
            Check(DebugDrag.State.Active && body.Position != beforeBackground, "Fresh click after activation resumes normal dragging");
            debugMouse = DebugMouse(480, 200, false); update.Invoke(teleport, new object[] { 1f / 60 });
            debugWindowFocused = false; DebugDrag.ObserveWindow(); debugWindowFocused = true;
            debugMouse = DebugMouse(20, 20, true); beforeBackground = body.Position;
            update.Invoke(teleport, new object[] { 1f / 60 });
            Check(body.Position == beforeBackground && !DebugDrag.State.Active, "Inactive window is observed even when no drag was in progress");
            NativeVerticalDrag(update, teleport, body);
            NativeHorizontalDrag(update, teleport, body);
            RenderContext.NativeScreen.SetValue(null, 2);
            Renderer.Stop(); debugMouse = DebugMouse(480, 200, true);
            update.Invoke(teleport, new object[] { 1f / 60 });
            Near(body.Position.Y, -633, "Stopped camera preserves the original debug teleport");
        }
        finally
        {
            Renderer.Stop(); fixture.UnpatchAll(fixture.Id);
            instance.SetValue(null, oldGame); GameLoop.m_player = oldPlayer;
            debugInstance.SetValue(null, oldDebug);
            Renderer.Screens.SetValue(null, oldScreens); RenderContext.NativeScreen.SetValue(null, oldScreen);
        }
    }
}
