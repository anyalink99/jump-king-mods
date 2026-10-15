using System;
using JumpKing;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void HorizontalDragTests()
    {
        var screens = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2) };
        var options = new AxisSettings { Mode = FollowMode.Window, Window = 80 };
        var viewport = new DebugViewport(new Rectangle(50, 30, 960, 720), new Point(1100, 800), new Point(1100, 800));
        foreach (int direction in new[] { -1, 1 })
        foreach (int rate in new[] { 60, 144, 240 })
        {
            var motion = new HorizontalMotion { Options = options };
            var state = new DebugDragState();
            var view = Vector2.Zero;
            var mouse = viewport.Client(new Vector2(240, 180));
            state.Move(mouse, viewport, view, 3);
            motion.Observe(state.World.X, 0, screens, true, false);
            motion.Drag(view.X, state.World.X);
            Near(motion.Translation, 0, "Horizontal drag inside the window leaves camera still");
            mouse.X += direction * 200;
            state.Move(mouse, viewport, view, 3);
            motion.Observe(state.World.X, 0, screens, true, false);
            motion.Advance(1f / rate); motion.Drag(view.X, state.World.X); view.X = motion.Translation;
            Near(view.X, -direction * 60, "Horizontal drag moves camera immediately at either edge");
            Near(state.World.X + view.X, 240 + direction * 40, "King stays at the configured horizontal window edge");
            mouse = state.Reproject(mouse, viewport, view);
            Check(mouse == viewport.Client(state.World + view), "Horizontal cursor is reprojected over the king");
            var world = state.World;
            for (int i = 0; i < rate * 2; i++)
            {
                state.Move(mouse, viewport, view, 3);
                motion.Observe(state.World.X, 0, screens, true, false);
                motion.Advance(1f / rate); motion.Drag(view.X, state.World.X); view.X = motion.Translation;
                mouse = state.Reproject(mouse, viewport, view);
            }
            Near(state.World.X, world.X, "Stationary horizontal mouse has no world drift at " + rate + " Hz");
            Near(view.X, -direction * 60, "Spring cannot pull the held king away from horizontal frame");
            mouse.X += direction * 120;
            state.Move(mouse, viewport, view, 3);
            motion.Observe(state.World.X, 0, screens, true, false);
            motion.Drag(view.X, state.World.X); view.X = motion.Translation;
            Near(view.X, -direction * 120, "Further dragging continues horizontal travel");
            mouse = state.Reproject(mouse, viewport, view); mouse.X -= direction * 40;
            state.Move(mouse, viewport, view, 3);
            motion.Observe(state.World.X, 0, screens, true, false);
            motion.Drag(view.X, state.World.X);
            Near(motion.Translation, view.X, "Reversing into the horizontal window stops camera motion immediately");
            motion.Observe(state.World.X, 0, screens, true, false); motion.Advance(1f / rate);
            Near(motion.Translation, view.X, "Releasing horizontal drag preserves framing without recoil");

            state.Reset(); view = Vector2.Zero; motion.Reset();
            mouse = viewport.Client(new Vector2(240 + direction * 150, 40));
            state.Move(mouse, viewport, view, 3);
            motion.Observe(state.World.X, 0, screens, true, false, 0, .5f);
            motion.Drag(view.X, state.World.X); view.X = motion.Translation;
            view.Y = DebugDragState.Frame(view.Y, state.World.Y, options, 360, 0, 720, 0);
            mouse = state.Reproject(mouse, viewport, view);
            Near(viewport.Logical(mouse).X, 240 + direction * 40, "Outside horizontal click uses the full configured frame even near a partial portal view");
            Near(viewport.Logical(mouse).Y, 140, "Diagonal click applies both axes together");
            Near(state.World.X, 240 + direction * 150, "Outside click preserves its selected world X");
        }

        var disabled = new HorizontalMotion { Options = options };
        disabled.Observe(470, 0, screens, false, false); disabled.Drag(0, 470);
        Near(disabled.Translation, 0, "Horizontal off never captures a narrow horizontal frame");
        disabled.Observe(470, 1, screens, true, false); disabled.Drag(0, 470);
        Near(disabled.Translation, 0, "No adjacent portal means full-width debug dragging");
        disabled.Options = new AxisSettings { Mode = FollowMode.Screen };
        disabled.Observe(470, 0, screens, true, false); disabled.Drag(0, 470);
        Near(disabled.Translation, 0, "Horizontal Screen mode stays fixed during drag");
        var direct = new HorizontalMotion { Options = new AxisSettings { Mode = FollowMode.Direct } };
        direct.Observe(450, 0, screens, true, false); direct.Drag(0, 450);
        Near(direct.Translation, -210, "Horizontal Direct mode keeps the king at focus");
        direct.Observe(450, 0, screens, true, true); direct.Drag(-210, 100);
        Near(direct.Translation, -210, "Paused horizontal drag cannot move camera");
        var classic = new HorizontalMotion { Options = new AxisSettings { NegativeBand = 20, PositiveBand = 60 } };
        classic.Observe(100, 0, screens, true, false); classic.Drag(0, 100);
        Near(classic.Translation, 120, "Asymmetric left band uses the drawn frame");
        classic.Observe(400, 0, screens, true, false); classic.Drag(0, 400);
        Near(classic.Translation, -100, "Asymmetric right band uses the drawn frame");
        SetWalls(screens[0], EdgeWalls(0, true));
        var wall = new HorizontalMotion { Options = options };
        wall.Observe(10, 0, screens, true, false); wall.Drag(0, 10);
        Near(wall.Translation, 0, "Closed left edge cannot reveal a side through debug dragging");
        wall.Observe(470, 0, screens, true, false); wall.Drag(0, 470);
        Near(wall.Translation, -190, "Open right edge still supports horizontal dragging");
        SetWalls(screens[0], new IBlock[0]);
        foreach (bool right in new[] { true, false })
        {
            var motion = new HorizontalMotion { Options = options };
            float before = right ? 481 : -1, after = right ? 1 : 479;
            motion.Observe(before, 0, screens, true, false); motion.Drag(0, before);
            float onScreen = before + motion.Translation;
            motion.Observe(after, 2, screens, true, false, 2, 0);
            motion.Drag(motion.Translation, after);
            Near(after + motion.Translation, onScreen, "One-way crossing keeps the departing side and cursor continuous");
            float retained = motion.Translation;
            motion.Observe(right ? -20 : 500, 2, screens, true, false, 2, 0);
            motion.Drag(retained, right ? -20 : 500);
            Near(motion.Translation, retained, "Retained one-way side cannot expand without a usable exit");
            motion.Observe(240, 2, screens, true, false, 2, 0); motion.Drag(retained, 240);
            Check(Math.Abs(motion.Translation) < Math.Abs(retained), "Moving into the destination shrinks the retained departure view");
        }
    }

    private static void NativeHorizontalDrag(System.Reflection.MethodInfo update, object teleport, BodyComp body)
    {
        var screens = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2) };
        Renderer.Screens.SetValue(null, screens);
        var count = typeof(LevelManager).GetField("_total_screens", Flags);
        object oldCount = count.GetValue(null); count.SetValue(null, 3);
        try
        {
            foreach (bool expansion in expansionAssembly == null ? new[] { false } : new[] { false, true })
            foreach (bool right in new[] { true, false })
            {
                screens[0] = expansion ? PortalScreen(0) : PortalScreen(0, 3);
                IBlock warp = expansion ? MultiWarp(new Rectangle(0, 0, 480, 360), 3) : null;
                if (expansion) SetWalls(screens[0], new[] { warp });
                DebugDrag.Reset(); CameraControls.Gestures.Reset(); Renderer.Horizontal.Reset();
                RenderContext.NativeScreen.SetValue(null, 0);
                Renderer.Horizontal.Options = new AxisSettings { Mode = FollowMode.Window, Window = 80 };
                Renderer.Horizontal.Observe(240, 0, screens, true, false);
                Renderer.View.Place(0, 0, 180);
                debugMouse = DebugMouse(right ? 940 : 20, 360, true);
                update.Invoke(teleport, new object[] { 1f / 60 });
                DebugDrag.AfterUpdate(); DebugDrag.Frame(0); DebugDrag.Present(new Rectangle(0, 0, 960, 720));
                Near(Renderer.View.X, right ? -190 : 190, "Native debug frame applies horizontal translation");
                Check(debugCursor.X == (right ? 560 : 400), "Native presenter moves cursor to horizontal frame edge");
                debugMouse = DebugMouse(debugCursor.X + (right ? 40 : -40), debugCursor.Y, true);
                update.Invoke(teleport, new object[] { 1f / 60 });
                float beforeX = body.Position.X;
                Renderer.Horizontal.Observe(beforeX + 9, 0, screens, true, false);
                if (expansion) ExpansionWarp(body, warp);
                else new HandlePlayerTeleportBehaviour().ExecuteBehaviour(new BehaviourContext(body));
                Check(Camera.CurrentScreen == 2, "Native side teleport reaches the linked screen during drag");
                var landed = body.Position;
                update.Invoke(teleport, new object[] { 1f / 60 });
                Near(body.Position.X, landed.X, "Held debug click cannot overwrite the native horizontal rebase");
                Near(body.Position.Y, landed.Y, "Held debug click preserves native vertical rebase too");
                float oldX = Renderer.Horizontal.Translation;
                float rebase = Renderer.Horizontal.Observe(body.Position.X + 9, Camera.CurrentScreen, screens, true, false, 2, 0);
                Renderer.View.Rebase(Renderer.Horizontal.Translation - oldX, rebase);
                DebugDrag.AfterUpdate(); DebugDrag.Frame(Renderer.View.Y);
                Near(body.Position.X + 9 + Renderer.View.X, right ? 280 : 200, "King stays on horizontal frame edge after native crossing");
                DebugDrag.Present(new Rectangle(0, 0, 960, 720));
                debugMouse = DebugMouse(debugCursor.X, debugCursor.Y, true);
                update.Invoke(teleport, new object[] { 1f / 60 });
                Near(body.Position.X, landed.X, "Stationary cursor cannot repeat a side teleport");
            }
        }
        finally { count.SetValue(null, oldCount); DebugDrag.Reset(); Renderer.Horizontal.Reset(); }
    }
}
