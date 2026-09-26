using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmoothCamera;

internal static partial class CameraTests
{
    private static IBlock[] EdgeWalls(int screen, bool left, params int[] gaps)
    {
        var result = new List<IBlock>();
        for (int row = 0; row < 45; row++)
            if (Array.IndexOf(gaps, row) < 0)
                result.Add(new BoxBlock(new Rectangle(left ? 0 : 472, -screen * 360 + row * 8, 8, 8)));
        return result.ToArray();
    }
    private static void SetWalls(LevelScreen screen, IBlock[] blocks)
    {
        typeof(LevelScreen).GetField("m_hitboxes", Flags).SetValue(screen, blocks);
        PortalOpenings.Reset();
    }
    private sealed class ForeignEdge : IBlock
    {
        public Rectangle GetRect() { throw new Exception("Foreign geometry callback must not run"); }
        public BlockCollisionType Intersects(Rectangle box, out Rectangle intersection)
        { throw new Exception("Foreign collision callback must not run"); }
    }
    private static void PortalOpeningTests()
    {
        var screens = new[] { PortalScreen(0, 2), PortalScreen(1, 1) };
        foreach (int source in new[] { 0, 1 })
        foreach (bool left in new[] { true, false })
        {
            foreach (var gaps in new[] { new int[0], new[] { 20 }, new[] { 4, 30 }, new[] { 20, 21 }, new[] { 0, 1 }, new[] { 43, 44 } })
            {
                SetWalls(screens[source], EdgeWalls(source, left, gaps));
                bool expected = gaps.Length == 2 && gaps[1] == gaps[0] + 1;
                Check((PortalOpenings.Destination(screens, source, left) >= 0) == expected,
                    "Edge needs two adjacent collision cells, including screen boundaries and negative world Y");
                Check(PortalOpenings.Destination(screens, source, !left) == 1 - source,
                    "Wall on one edge does not hide the opposite destination");
                Check(PortalViews.Destination(screens, source, left) == 1 - source,
                    "Visual wall filtering never changes native teleport identity");
            }
        }
        foreach (var block in new IBlock[] { new WaterBlock(new Rectangle(0, 0, 8, 360)),
            new SandBlock(new Rectangle(0, 0, 8, 360)), new QuarkBlock(new Rectangle(0, 0, 8, 360)), new ForeignEdge() })
        {
            SetWalls(screens[0], new[] { block });
            Check(PortalOpenings.Destination(screens, 0, true) == 1, "Nonblocking or unknown material does not prove a solid wall");
        }
        foreach (var block in new IBlock[] { new IceBlock(new Rectangle(0, 0, 8, 360)), new SnowBlock(new Rectangle(0, 0, 8, 360)) })
        {
            SetWalls(screens[0], new[] { block });
            Check(PortalOpenings.Destination(screens, 0, true) == -1, "Solid ice and snow edges hide the destination");
        }
        SetWalls(screens[0], new IBlock[] { new SlopeBlock(new Rectangle(0, 0, 480, 360), SlopeType.TopLeft) });
        Check(PortalOpenings.Destination(screens, 0, true) == 1 && PortalOpenings.Destination(screens, 0, false) == -1,
            "Slope edge shape is used instead of treating its whole bounding box as a wall");
        SetWalls(screens[0], EdgeWalls(0, true));
        Check(PortalOpenings.Destination(screens, 0, true) == -1, "Closed edge cached");
        typeof(LevelScreen).GetField("m_hitboxes", Flags).SetValue(screens[0], EdgeWalls(0, true, 20, 21));
        Check(PortalOpenings.Destination(screens, 0, true) == -1, "Extra presentation frames reuse the same geometry observation");
        Renderer.AfterUpdate();
        Check(PortalOpenings.Destination(screens, 0, true) == 1, "Next native update observes changed edge geometry");
        SetWalls(screens[0], EdgeWalls(0, true));
        var motion = new HorizontalMotion();
        for (int i = 0; i < 240; i++) { motion.Observe(5, 0, screens, true, false); motion.Advance(1f / 120); }
        Near(motion.Translation, 0, "Camera does not pan toward a sealed edge");
        for (int i = 0; i < 240; i++) { motion.Observe(475, 0, screens, true, false); motion.Advance(1f / 120); }
        Check(motion.Translation < -200, "Camera still pans toward the open edge");
        SetWalls(screens[0], EdgeWalls(0, false));
        for (int i = 0; i < 240; i++) { motion.Observe(475, 0, screens, true, false); motion.Advance(1f / 120); }
        Near(motion.Translation, 0, "Closing a visible side smoothly returns the camera to native framing");
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++) { PortalOpenings.Reset(); PortalOpenings.Destination(screens, 0, false); }
        timer.Stop();
        Console.WriteLine("[PERF] 1000 edge observations: " + timer.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
        PortalOpenings.Reset();
    }

    private static void PortalWallPixels(GraphicsDevice device, RenderTarget2D target, LevelScreen[] screens, string output)
    {
        var links = typeof(LevelScreen).GetField("m_teleport", Flags);
        var blocks = typeof(LevelScreen).GetField("m_hitboxes", Flags);
        var graphics = typeof(LevelScreen).GetField("m_graphics", Flags);
        object oldLinks = links.GetValue(screens[0]), oldBlocks = blocks.GetValue(screens[0]);
        var oldGraphics = (LevelScreen.Graphics)graphics.GetValue(screens[0]);
        try
        {
            links.SetValue(screens[0], new[] { new TeleportLink(3) });
            var withArt = oldGraphics; withArt.background = withArt.backbackground;
            graphics.SetValue(screens[0], withArt);
            SetWalls(screens[0], EdgeWalls(0, true));
            using (var compositor = new ScreenCompositor())
            foreach (bool openSide in new[] { false, true })
            {
                var motion = new HorizontalMotion();
                for (int i = 0; i < 240; i++) { motion.Observe(openSide ? 475 : 5, 0, screens, true, false); motion.Advance(1f / 120); }
                device.SetRenderTarget(target); Game1.instance.StartBatch();
                compositor.Draw(0, screens.Length, index => screens[index].Draw(), false, -1,
                    motion.Translation, motion.Left, motion.Right, motion.Anchor);
                Game1.instance.EndBatch(); device.SetRenderTarget(null);
                var pixels = Read(target);
                Check(pixels[180 * 480 + 10] == Color.DarkBlue &&
                    pixels[180 * 480 + 470] == (openSide ? Color.DarkGreen : Color.DarkBlue),
                    "Rendered destination is visible only toward the open edge");
                Save(target, Path.Combine(output, openSide ? "portal-open-right.png" : "portal-closed-left.png"));
            }
        }
        finally { links.SetValue(screens[0], oldLinks); blocks.SetValue(screens[0], oldBlocks); graphics.SetValue(screens[0], oldGraphics); PortalOpenings.Reset(); }
    }
}
