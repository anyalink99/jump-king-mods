using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using SmoothCamera;

internal static partial class CameraTests
{
    private static void MappingPortalFixture(Assembly mapping)
    {
        var layoutType = mapping.GetType("MegaMappingExpansion.MapLayout", true);
        var layout = Activator.CreateInstance(layoutType, true);
        var links = (Dictionary<int, int[]>)layoutType.GetField("Links", Flags).GetValue(layout);
        links.Add(0, new[] { 257, 289 });
        var screens = Enumerable.Range(0, 289).Select(i => PortalScreen(i, -1, -1)).ToArray();
        mapping.GetType("MegaMappingExpansion.NativeMapLayout", true).GetMethod("Apply", Flags)
            .Invoke(null, new object[] { layout, 0, screens[0].teleport });
        var count = typeof(LevelManager).GetField("_total_screens", Flags);
        object oldCount = count.GetValue(null), oldScreens = Renderer.Screens.GetValue(null), oldScreen = RenderContext.NativeScreen.GetValue(null);
        var body = (BodyComp)FormatterServices.GetUninitializedObject(typeof(BodyComp));
        typeof(BodyComp).GetField("m_width", Flags).SetValue(body, 18);
        typeof(BodyComp).GetField("m_height", Flags).SetValue(body, 26);
        try
        {
            count.SetValue(null, screens.Length); Renderer.Screens.SetValue(null, screens);
            foreach (bool left in new[] { true, false })
            {
                int destination = left ? 256 : 288;
                Check(JKRuntime.Geometry.MapTopology.Destination(screens, 0, left) == destination, "Camera reads actual MME integer side links above screen 255");
                RenderContext.NativeScreen.SetValue(null, 0);
                body.Position = new Vector2(left ? -10 : 472, 100);
                var motion = new HorizontalMotion();
                motion.Observe(body.GetHitbox().Center.X, 0, screens, true, false);
                new HandlePlayerTeleportBehaviour().ExecuteBehaviour(new BehaviourContext(body));
                Check(Camera.CurrentScreen == destination, "Native teleport follows the MME side link to its distant destination");
                Near(motion.Observe(body.GetHitbox().Center.X, destination, screens, true, false), destination * 360,
                    "Camera preserves the MME side crossing's vertical rebase");
                Check((left ? motion.Right : motion.Left) == 0, "MME one-way arrival retains the departing view");
            }
        }
        finally
        { count.SetValue(null, oldCount); Renderer.Screens.SetValue(null, oldScreens); RenderContext.NativeScreen.SetValue(null, oldScreen); JKRuntime.Geometry.MapTopology.Invalidate(); }
    }
}
