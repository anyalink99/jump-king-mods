using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void NativeVerticalDrag(MethodInfo update, object teleport, BodyComp body)
    {
        var count = typeof(LevelManager).GetField("_total_screens", Flags);
        var lookup = typeof(BodyComp).GetField("m_blockBehaviourLookup", Flags);
        object oldCount = count.GetValue(null), oldLookup = lookup.GetValue(body);
        object oldScreens = Renderer.Screens.GetValue(null);
        var screens = Enumerable.Range(0, 16).Select(i => PortalScreen(i)).ToArray();
        foreach (int row in new[] { 0, 10 })
            SetWalls(screens[row], new IBlock[] { new BoxBlock(new Rectangle(0, 300 - row * 360, 480, 8)) });
        try
        {
            count.SetValue(null, screens.Length); Renderer.Screens.SetValue(null, screens);
            lookup.SetValue(body, new Dictionary<Type, IBlockBehaviour>());
            foreach (bool upward in new[] { true, false })
            {
                DebugDrag.Reset(); CameraControls.Gestures.Reset(); Renderer.Horizontal.Reset();
                int start = upward ? 0 : 10, destination = upward ? 10 : 0;
                RenderContext.NativeScreen.SetValue(null, start);
                Renderer.Motion.Options = new AxisSettings { Mode = FollowMode.Window, Window = 100 };
                Renderer.Motion.FirstScreen = 0; Renderer.Motion.LastScreen = 15;
                Renderer.View.Place(0, start * 360, 180 - start * 360);
                debugMouse = DebugMouse(480, 360, true);
                debugCursor = debugMouse.Position;
                update.Invoke(teleport, new object[] { 1f / 60 });
                DebugDrag.AfterUpdate(); DebugDrag.Frame(Renderer.View.Y); DebugDrag.Present(new Rectangle(0, 0, 960, 720));
                for (int tick = 0; tick < 600; tick++)
                {
                    // normal follow runs before debug input and can't follow a zero-speed climb
                    Camera.UpdateCameraWithVelocity(body.GetHitbox().Center, new Vector2(0, .3f));
                    debugMouse = DebugMouse(debugCursor.X, debugCursor.Y + (upward ? -12 : 12), true);
                    debugCursor = debugMouse.Position;
                    update.Invoke(teleport, new object[] { 1f / 60 });
                    DebugDrag.AfterUpdate(); DebugDrag.Frame(Renderer.View.Y); DebugDrag.Present(new Rectangle(0, 0, 960, 720));
                }
                Near(body.Position.Y + 13, 180 - destination * 360, "Long drag keeps the requested world position");
                Check(Camera.CurrentScreen == destination, "Long drag updates the physical screen before releasing the king");
                debugMouse = DebugMouse(debugCursor.X, debugCursor.Y, false);
                update.Invoke(teleport, new object[] { 1f / 60 });
                var context = new BehaviourContext(body);
                var handlers = new LinkedList<IBlockBehaviour>();
                var move = new UpdateYPositionFromVelocityBehaviour(handlers);
                var resolve = new ResolveYCollisionBehaviour(handlers);
                var gravity = new ApplyGravityBehaviour(handlers);
                for (int tick = 0; tick < 120; tick++)
                {
                    move.ExecuteBehaviour(context); resolve.ExecuteBehaviour(context); gravity.ExecuteBehaviour(context);
                    Camera.UpdateCameraWithVelocity(body.GetHitbox().Center, body.Velocity);
                }
                Near(body.Position.Y + 26, 300 - destination * 360, "Released king lands on the destination floor with native collision resolution");
                Check(body.IsOnGround, "Native physics retains ground contact after a long drag");
            }
        }
        finally
        {
            count.SetValue(null, oldCount); lookup.SetValue(body, oldLookup); Renderer.Screens.SetValue(null, oldScreens);
            DebugDrag.Reset(); Renderer.Horizontal.Reset();
        }
    }
}
