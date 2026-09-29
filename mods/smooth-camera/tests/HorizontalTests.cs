using System;
using System.IO;
using System.Xml.Serialization;
using JumpKing.Level;
using SmoothCamera;

internal static partial class CameraTests
{
    private static LevelScreen PortalScreen(int index, params int[] destinations)
    {
        var links = new TeleportLink[destinations.Length];
        for (int i = 0; i < links.Length; i++) links[i] = new TeleportLink(destinations[i]);
        return new LevelScreen(index, new IBlock[0], new LevelScreen.Graphics(), false, links, 0, null);
    }
    private static void HorizontalTests()
    {
        var serializer = new XmlSerializer(typeof(CameraSettings));
        using (var reader = new StringReader("<CameraSettings><Smooth>true</Smooth></CameraSettings>"))
            Check(!((CameraSettings)serializer.Deserialize(reader)).Horizontal, "Old settings retain horizontal default off");
        Check(!new CameraSettings().Horizontal, "New horizontal checkbox defaults off");
        var screens = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2, 1), PortalScreen(3, 1, 3) };
        Check(PortalViews.Destination(screens, 0, true) == 2 && PortalViews.Destination(screens, 0, false) == 2, "Single link matches both native exits");
        Check(PortalViews.Destination(screens, 3, true) == 0 && PortalViews.Destination(screens, 3, false) == 2, "Two links match native exit direction");
        Check(PortalViews.Destination(new[] { PortalScreen(0, 999) }, 0, true) == -1, "Out of range links do not render invalid screens");
        int anchor; float influence;
        Renderer.FindPortalNeighborhood(screens, 1, 180, out anchor, out influence);
        Check(anchor == 0, "A neighboring visible teleport screen contributes before physical entry");
        Near(influence, .5f, "Half-visible portal has half influence");
        Renderer.FindPortalNeighborhood(screens, 1, 360, out anchor, out influence);
        Near(influence, 0, "No horizontal influence when only a non-portal screen is visible");
        foreach (int rate in new[] { 60, 144, 240 })
        {
            var motion = new HorizontalMotion();
            for (int i = 0; i < rate; i++) { motion.Observe(430, 0, screens, false, false); motion.Advance(1f / rate); }
            Near(motion.Translation, 0, "Off setting never pans even at a portal");
            for (int i = 0; i < rate; i++) { motion.Observe(430, 1, screens, true, false, 1, 0); motion.Advance(1f / rate); }
            Near(motion.Translation, 0, "Non-portal neighborhood stays fixed");
            for (int i = 0; i < rate; i++)
            {
                float before = motion.Translation;
                motion.Observe(430, 1, screens, true, false, 0, .5f); motion.Advance(1f / rate);
                Check(motion.Translation <= before && Math.Abs(motion.Translation - before) < 8, "Neighbor activation is smooth at " + rate);
            }
            Check(motion.Translation < -65 && motion.Translation > -95, "Neighbor influence follows partially");
            float frozen = motion.Translation; motion.Observe(40, 1, screens, true, true, 0, .5f); motion.Advance(1);
            Near(motion.Translation, frozen, "Pause freezes horizontal motion");
            for (int i = 0; i < rate * 2; i++) { motion.Observe(430, 1, screens, true, false, 1, 0); motion.Advance(1f / rate); }
            Near(motion.Translation, 0, "Leaving portal neighborhood smoothly returns to full-width framing");
        }
        foreach (bool right in new[] { false, true })
        {
            var motion = new HorizontalMotion(); float oldX = right ? 479 : 1, newX = right ? 1 : 479;
            for (int i = 0; i < 240; i++) { motion.Observe(oldX, 0, screens, true, false); motion.Advance(1f / 120); }
            float oldView = oldX + motion.Translation;
            float rebase = motion.Observe(newX, 2, screens, true, false);
            Near(rebase, 720, "Side teleport rebases vertical camera coordinates");
            Near(newX + motion.Translation - oldView, right ? 2 : -2, "Teleport retains continuous player position in the viewport");
            Near(motion.Observe(newX, 2, screens, true, false), 0, "Repeated presentation observations do not reapply teleport");
            Check(right ? motion.Left == 0 : motion.Right == 0, "Departure view remains available after crossing");
        }
        var oneWay = new[] { PortalScreen(0, 2), PortalScreen(1) };
        var departure = new HorizontalMotion();
        for (int i = 0; i < 120; i++) { departure.Observe(479, 0, oneWay, true, false); departure.Advance(1f / 60); }
        departure.Observe(1, 1, oneWay, true, false, 1, 0);
        Check(departure.Left == 0 && departure.Translation > 240, "One-way arrival retains departing screen without an artificial reverse teleport");
        for (int i = 0; i < 240; i++) { departure.Observe(100, 1, oneWay, true, false, 1, 0); departure.Advance(1f / 120); }
        Near(departure.Translation, 0, "One-way arrival settles without enabling tracking on unrelated screens");
        var graphs = new[] { PortalScreen(0, 3), PortalScreen(1, 1), PortalScreen(2) };
        var switching = new HorizontalMotion();
        for (int i = 0; i < 120; i++) { switching.Observe(440, 0, graphs, true, false); switching.Advance(1f / 60); }
        bool switched = false;
        for (int i = 0; i < 600; i++)
        {
            float before = switching.Translation;
            switching.Observe(440, 1, graphs, true, false, 1, .6f);
            if (switching.Anchor == 1 && !switched) { Check(Math.Abs(before) < .002f, "Inconsistent portal layout switches only when side view is hidden"); switched = true; }
            switching.Advance(1f / 120);
        }
        Check(switched, "Neighbor portal layout eventually activates after settling");
        Console.WriteLine("[OK] Horizontal camera: native links, neighboring visibility, pause, both exits, one-way transition and disabled settings");
    }
}
