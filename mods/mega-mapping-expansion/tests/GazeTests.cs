using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static void GazeChecks()
        {
            Vector2 origin = new Vector2(100,100);
            Require(GazeMotion.Target(origin, origin, 2, 1) == Vector2.Zero, "gaze at eye centre is finite and neutral");
            Vector2 target = GazeMotion.Target(origin, new Vector2(-100,200), 2, 1);
            Require(target.X < 0 && target.Y > 0 && Math.Abs(target.X) <= 2 && Math.Abs(target.Y) <= 1,
                "gaze points at the King within the configured ellipse");
            Vector2 a = Vector2.Zero, b = Vector2.Zero;
            for(int i=0;i<60;i++) a=GazeMotion.Advance(a,target,7,1f/60);
            for(int i=0;i<30;i++) b=GazeMotion.Advance(b,target,7,1f/30);
            Require(Vector2.Distance(a,b)<.0001f, "gaze response is frame-rate independent");
            Require(GazeMotion.Advance(a,target,7,0)==a, "preview pause freezes gaze motion");
            var scene = new SceneFile { ScreenLooks = new[] {
                new ScreenLook { Screen=2, AmbientScale=.68f, PlayerRimScale=.12f } } };
            SceneValidation.Validate(scene, ".", 3);
            Require(scene.Options.AmbientIntensity==1f, "screen darkness does not mutate the shared lighting defaults");
            scene.ScreenLooks=new[]{new ScreenLook{Screen=2},new ScreenLook{Screen=2}};
            bool rejected=false;
            try { SceneValidation.Validate(scene,".",3); } catch(InvalidDataException) { rejected=true; }
            Require(rejected,"duplicate screen lighting overrides rejected");
        }
    }
}
