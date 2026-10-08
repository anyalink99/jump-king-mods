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
    private static Assembly expansionAssembly;
    private static IBlock MultiWarp(Rectangle bounds, byte target, byte offset = 0)
    {
        return (IBlock)Activator.CreateInstance(expansionAssembly.GetType("JumpKing_Expansion_Blocks.Blocks.MultiWarp", true),
            new object[] { bounds, target, offset });
    }
    private static void ExpansionWarp(BodyComp body, IBlock block)
    {
        var context = new BehaviourContext(body);
        var collision = new AdvCollisionInfo(new List<IBlock> { block }, false, SlopeType.None, Vector2.Zero);
        var aggregate = typeof(BehaviourContextCollisionInfo).GetMethod("AggregateCollisionInfo", Flags);
        aggregate.Invoke(context.CollisionInfo, new object[] { collision });
        aggregate.Invoke(context.LastFrameCollisionInfo, new object[] { collision });
        var behaviour = (IBlockBehaviour)Activator.CreateInstance(expansionAssembly.GetType("JumpKing_Expansion_Blocks.Behaviours.MultiWarp", true));
        behaviour.ExecuteBlockBehaviour(context);
    }
    private static void ExpansionPortalTests()
    {
        if (expansionAssembly == null) return;
        var screens = Enumerable.Range(0, 260).Select(i => PortalScreen(i)).ToArray();
        foreach (bool left in new[] { true, false })
        {
            var first = MultiWarp(new Rectangle(left ? 0 : 472, -360, 8, 120), 3);
            var second = MultiWarp(new Rectangle(left ? 0 : 472, -200, 8, 160), 4, 1);
            SetWalls(screens[1], new[] { first, second });
            Check(PortalViews.Destination(screens, 1, left, -300) == 2, "Expansion selects destination at the king's height");
            Check(PortalViews.Destination(screens, 1, left, -150) == 258, "Expansion decodes the 255-screen offset");
            Check(PortalViews.Destination(screens, 1, !left, -300) == -1, "Expansion block only opens its own edge");
            Check(PortalViews.Destination(screens, 1, left, -220) == -1, "Gap between MultiWarp bands cannot reveal a destination");
            Check(PortalViews.Destination(screens, 1, left) == -1, "Ambiguous destinations without height stay hidden");
            Check(PortalOpenings.Destination(screens, 1, left, -300) == 2, "Nonblocking MultiWarp doesn't seal its own edge");
            var motion = new HorizontalMotion();
            for (int i = 0; i < 120; i++) { motion.Observe(left ? 10 : 470, 1, screens, true, false, 1, 1, 0, -300); motion.Advance(1f / 60); }
            Check(Math.Abs(motion.Translation) > 180, "Normal camera follows Expansion side portals without debug dragging");
            motion.Observe(left ? 10 : 470, 1, screens, true, false, 1, 1, 0, -150);
            Check((left ? motion.Left : motion.Right) == 2, "Changing MultiWarp bands retains the visible destination while it retreats");
            bool switched = false;
            for (int i = 0; i < 600; i++)
            {
                motion.Advance(1f / 60);
                float previous = motion.Translation;
                motion.Observe(left ? 10 : 470, 1, screens, true, false, 1, 1, 0, -150);
                if ((left ? motion.Left : motion.Right) == 258)
                { Check(Math.Abs(previous) <= .001f, "Expansion destination changes only once the previous side is hidden"); switched = true; break; }
            }
            Check(switched, "Camera eventually reveals the other MultiWarp band");
            SetWalls(screens[1], new[] { first }.Concat(EdgeWalls(1, left)).ToArray());
            Check(PortalOpenings.Destination(screens, 1, left, -300) == -1, "Solid native wall still suppresses Expansion side framing");
            SetWalls(screens[1], new[] { first, MultiWarp(new Rectangle(left ? 0 : 472, -360, 8, 120), 4) });
            Check(PortalViews.Destination(screens, 1, left, -300) == -1, "Overlapping conflicting MultiWarps don't display an arbitrary destination");
            Check(PortalViews.Connects(screens, 1, left, -300, 2) && PortalViews.Connects(screens, 1, left, -300, 3),
                "Actual crossing remains recognizable for either overlapping MultiWarp");
            SetWalls(screens[1], new[] { MultiWarp(new Rectangle(left ? 0 : 472, -360, 8, 120), 255, 255) });
            Check(PortalViews.Destination(screens, 1, left, -300) == -1, "Out-of-map Expansion destination is ignored");
            SetWalls(screens[1], new[] { MultiWarp(new Rectangle(200, -360, 8, 120), 3) });
            Check(PortalViews.Destination(screens, 1, left, -300) == -1, "Interior MultiWarp cannot preview an edge");
        }
        var native = PortalScreen(0, 3);
        SetWalls(native, new[] { MultiWarp(new Rectangle(0, 0, 8, 360), 4) });
        screens[0] = native;
        Check(PortalViews.Destination(screens, 0, true, 100) == 2, "Native side link takes precedence like the native behaviour order");
        Console.WriteLine("[OK] Installed Expansion Blocks portal contracts: " + expansionAssembly.GetName().Version);
        PortalOpenings.Reset();
    }
}
