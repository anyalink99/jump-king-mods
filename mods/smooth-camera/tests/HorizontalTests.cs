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
        HorizontalFramingTests();
        var serializer = new XmlSerializer(typeof(CameraSettings));
        using (var reader = new StringReader("<CameraSettings><Smooth>true</Smooth></CameraSettings>"))
            Check(!((CameraSettings)serializer.Deserialize(reader)).Horizontal, "Old settings retain horizontal default off");
        Check(!new CameraSettings().Horizontal, "New horizontal checkbox defaults off");
        var screens = new[] { PortalScreen(0, 3), PortalScreen(1), PortalScreen(2, 1), PortalScreen(3, 1, 3) };
        Check(JKRuntime.Geometry.MapTopology.Destination(screens, 0, true) == 2 && JKRuntime.Geometry.MapTopology.Destination(screens, 0, false) == 2, "Single link matches both native exits");
        Check(JKRuntime.Geometry.MapTopology.Destination(screens, 3, true) == 0 && JKRuntime.Geometry.MapTopology.Destination(screens, 3, false) == 2, "Two links match native exit direction");
        Check(JKRuntime.Geometry.MapTopology.Destination(new[] { PortalScreen(0, 999) }, 0, true) == -1, "Out of range links do not render invalid screens");
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
        Check(100 + departure.Translation >= 216 && 100 + departure.Translation <= 264.01f, "One-way arrival keeps the stationary subject in its framing band");
        for (int i = 0; i < 240; i++) { departure.Observe(300, 1, oneWay, true, false, 1, 0); departure.Advance(1f / 120); }
        Near(departure.Translation, 0, "Departure scenery disappears after entering the destination");
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
        foreach(bool right in new[]{false,true}) {
            var self=new[]{PortalScreen(0,1)};var motion=new HorizontalMotion();
            float x=right ? 479 : 1;
            for(int i=0;i<240;i++) {motion.Observe(x,0,self,true,false);motion.Advance(1f/120);}
            for(int wrap=0;wrap<40;wrap++) {
                float view=x+motion.Translation;float next=right ? 1 : 479;
                Near(motion.Observe(next,0,self,true,false),0,"Self-link has no vertical camera cut");
                Near(next+motion.Translation-view,right ? 2 : -2,"Self-link preserves continuous infinite camera movement");
                x=next;
                // finish another lap in small native-sized steps
                for(int tick=0;tick<239;tick++) {x+=right ? 2 : -2;motion.Observe(x,0,self,true,false);motion.Advance(1f/120);}
            }
        }
        var oldScreens=Renderer.Screens.GetValue(null);
        var centerField=typeof(Renderer).GetField("targetCenter",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var oldCenter=centerField.GetValue(null);float oldViewX=Renderer.View.X,oldViewY=Renderer.View.Y;
        try {
            Renderer.Screens.SetValue(null,new[]{PortalScreen(0,1)});JKRuntime.Geometry.MapTopology.Invalidate();
            Renderer.Horizontal.Reset();Renderer.Horizontal.Observe(470,0,(LevelScreen[])Renderer.Screens.GetValue(null),true,false);
            centerField.SetValue(null,new Microsoft.Xna.Framework.Vector2(470,113));Renderer.View.Place(-180,0,113);
            var projected=Renderer.ProjectWorld(new Microsoft.Xna.Framework.Vector2(10,100));
            Near(projected.X,310+JumpKing.Camera.Offset.X,"Late multiplayer overlay uses the visible self-link image");
            Near(projected.Y,100+JumpKing.Camera.Offset.Y,"Late overlay retains vertical world alignment");
            Renderer.Screens.SetValue(null,new[]{PortalScreen(0,3),PortalScreen(1),PortalScreen(2,1)});JKRuntime.Geometry.MapTopology.Invalidate();
            Renderer.Horizontal.Reset();Renderer.Horizontal.Observe(470,0,(LevelScreen[])Renderer.Screens.GetValue(null),true,false);
            projected=Renderer.ProjectWorld(new Microsoft.Xna.Framework.Vector2(20,-620));
            Near(projected.X,320+JumpKing.Camera.Offset.X,"Linked remote actor projects through the horizontal camera pan");
            Near(projected.Y,100+JumpKing.Camera.Offset.Y,"Remote screen offset is applied once");
        } finally {Renderer.Horizontal.Reset();Renderer.Screens.SetValue(null,oldScreens);centerField.SetValue(null,oldCenter);Renderer.View.Place(oldViewX,oldViewY,0);JKRuntime.Geometry.MapTopology.Invalidate();}
        Console.WriteLine("[OK] Horizontal camera: shared links, repeated self loops, world overlay projection, neighboring visibility, pause, both exits and one-way transition");
    }

    private static void HorizontalFramingTests()
    {
        foreach (int rate in new[] { 60, 144, 240 })
        foreach (bool self in new[] { false, true })
        foreach (FollowMode mode in new[] { FollowMode.JumpKing, FollowMode.Direct, FollowMode.Window })
        foreach (bool centered in new[] { false, true })
        foreach (bool legacy in new[] { false, true })
        foreach (int direction in new[] { -1, 1 })
        {
            bool continuous = true;
            var screens = self ? new[] { PortalScreen(0, 1) } : new[] { PortalScreen(0, 2), PortalScreen(1, 1) };
            var options = AxisSettings.HorizontalDefault(); options.Mode = mode;
            options.ContinuousFraming = legacy;
            options.Focus = centered ? .5f : direction < 0 ? .35f : .65f;
            options.LookAhead = 32;
            var motion = new HorizontalMotion { Options = options };
            var reference = new AxisMotion { Options = options };
            float world = 240, x = world; int screen = 0;
            motion.Observe(x, screen, screens, true, false);
            float limit = continuous ? 100000 : 240;
            reference.Observe(world, 0, 480, -limit, limit, 0, false, false, true, 1, false);
            int tick = 0, crossings = 0;
            for (int frame = 0; frame < rate * 12; frame++)
            {
                int nextTick = frame * 60 / rate;
                while (tick < nextTick)
                {
                    // native physics still moves at 60 Hz, even on extra camera frames
                    int heading = tick < 360 ? direction : -direction;
                    world += heading * 4; x += heading * 4; tick++;
                    if (x < 0 || x >= 480)
                    {
                        x -= heading * 480;
                        if (!continuous) reference.Rebase(heading * 480);
                        screen = self ? 0 : 1 - screen; crossings++;
                    }
                    motion.Observe(x, screen, screens, true, false, screen, 1, heading * 240);
                    reference.Observe(continuous ? world : x, heading * 240, 480, -limit, limit, 0, false, false, true, 1, false);
                }
                motion.Advance(1f / rate); reference.Advance(1f / rate);
                float error = Math.Abs(x + motion.Translation - (continuous ? world : x) - reference.Value);
                Check(error < .02f, "Side crossing retains " + (continuous ? "continuous" : "legacy") + " " + mode + " framing at " + rate + " Hz: " + error);
            }
            Check(crossings >= 5, "Framing check crosses several linked screens");
        }
    }
}
