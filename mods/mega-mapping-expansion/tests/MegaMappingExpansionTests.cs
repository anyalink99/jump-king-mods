using System;
using System.IO;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    internal static partial class Tests
    {
        private static int checks;
        private static int preparationContractChecks;
        private static void CountPreparationContracts() { preparationContractChecks++; }
        private static void Require(bool value, string message)
        { checks++; if (!value) throw new Exception("FAILED: " + message); }

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length > 3 && args[2] == "ending-compatibility")
                {
                    EndingCompatibilityChecks(args[0], args[3]);
                    Console.WriteLine("[OK] " + checks + " installed ending-provider compatibility checks");
                    return 0;
                }
                NativeHooks.AssertContracts();
                NativeHooks.ConfigureForScene(true);
                Require(HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.Level.LevelScreen).GetMethod("DrawForeground")).Owners.Contains("mega-mapping-expansion.render"), "A scene installs its native rendering adapter");
                NativeHooks.ConfigureForScene(false);
                NativeHooks.ConfigureForScene(false);
                var dormantPatches = HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.Level.LevelScreen).GetMethod("DrawForeground"));
                Require(dormantPatches == null || !dormantPatches.Owners.Contains("mega-mapping-expansion.render"), "Maps without scene.xml retain no Mapping render hooks across restarts");
                var resetSave = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube").GetMethod("DeleteSaves");
                Require(HarmonyLib.Harmony.GetPatchInfo(resetSave).Owners.Contains("mega-mapping-expansion.render.save"), "Scene-free maps retain persistent save reset semantics");
                NativeHooks.Uninstall();
                var entryFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                using (var preparation = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareAttempt(preparation, args[1], 169);
                    Require((bool)typeof(ModEntry).GetField("prepared", entryFlags).GetValue(null),
                        "Scene and hooks can prepare before any native player or scene entity exists");
                    Require(typeof(ModEntry).GetField("preparedScene", entryFlags).GetValue(null) != null,
                        "Preparation retains the validated scene for activation");
                }
                Require(typeof(ModEntry).GetField("preparedScene", entryFlags).GetValue(null) == null,
                    "Cancelled preparation drops scene bytes");
                var cancelledPatches = HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.Level.LevelScreen).GetMethod("DrawForeground"));
                Require(cancelledPatches == null || !cancelledPatches.Owners.Contains("mega-mapping-expansion.render"),
                    "Cancelled preparation releases early rendering hooks");
                var preparationFixture = new HarmonyLib.Harmony("mapping.preparation-fixture");
                preparationFixture.Patch(typeof(NativeHooks).GetMethod("AssertContracts", entryFlags),
                    prefix: new HarmonyLib.HarmonyMethod(typeof(Tests).GetMethod("CountPreparationContracts", entryFlags)));
                using (var world = new JKRuntime.RuntimeScope())
                {
                    ModEntry.PrepareWorld(world);
                    int firstCheck = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        using (var attempt = new JKRuntime.RuntimeScope())
                        {
                            ModEntry.PrepareAttempt(attempt, args[1], 169);
                            if (i == 0) firstCheck = preparationContractChecks;
                            Require(firstCheck > 0 && preparationContractChecks == firstCheck, "Same-world restart does not recompile native rendering hooks");
                            ModEntry.Unload();
                        }
                        Require(SceneHost.Current == null, "Dormant Mapping hooks have no scene to render or update");
                    }
                }
                preparationFixture.UnpatchAll(preparationFixture.Id);
                var exitedPatches = HarmonyLib.Harmony.GetPatchInfo(typeof(JumpKing.Level.LevelScreen).GetMethod("DrawForeground"));
                Require(exitedPatches == null || !exitedPatches.Owners.Contains("mega-mapping-expansion.render"), "World exit releases Mapping hooks");
                Phases();
                AtlasChecks();
                SoftMotionChecks();
                WeatherChecks();
                SurfaceShadowChecks();
                WaterSimulation();
                Occlusion();
                GazeChecks();
                CanopyAndRims();
                LargeMapAndMotions(args[0]);
                MapLayoutChecks(args[0]);
                CompiledCacheRoundTrip(args[0]);
                InvalidDataIsRejected(args[0]);
                AuthoringChecks(args[0]);
                BehaviorChecks();
                BehaviorTreeChecks(args[0]);
                EndingChecks(args[0]);
                InfrastructureChecks();
                NarrativeChecks();
                PersistenceChecks(args[0]);
                ToggleChecks(args[0]);
                if (args.Length > 2 && args[2] == "scene")
                {
                    ActualExample(args[1]);
                    BackgroundGrounding(args[1], args[0]);
                    SolidStoneAndLamp(args[1], args[0]);
                }
                MappingState.Load(args[1]);
                MappingState.Reset();
                Require(MappingState.Pending == null && MappingState.Options == null,
                    "Reset releases presentation state; restart preparation belongs only to BeforeAttempt");
                Console.WriteLine("[OK] Mega Mapping Expansion: " + checks + " XML, motion, 300-screen and native hook checks");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Phases()
        {
            Require(SceneAnimation.Phase(0.5f, 2f, "once") == 0.25f, "once phase");
            Require(SceneAnimation.Phase(3f, 2f, "once") == 1f, "once clamp");
            Require(Math.Abs(SceneAnimation.Phase(2.5f, 2f, "loop") - 0.25f) < 0.0001f, "loop wrap");
            Require(Math.Abs(SceneAnimation.Phase(3f, 2f, "pingpong") - 0.5f) < 0.0001f, "pingpong reverse");
            Require(SceneValidation.ParseColor("#123456", "test").A == 255, "RGB alpha");
            Require(SceneValidation.ParseColor("#12345678", "test").A == 0x78, "RGBA alpha");
            Require(SceneValidation.ParsePath("0,0;10,-2;20,4", "route").Length == 3, "path parser");
            Require(SceneValidation.ParseTrack("0:1;0.5:0.6;1:1", "wing").Length == 3, "keyframe parser");
            Microsoft.Xna.Framework.Color half = SceneHost.MultiplyAlpha(new Microsoft.Xna.Framework.Color(200, 100, 50, 255), 0.5f);
            Require(half.R == 100 && half.G == 50 && half.B == 25 && half.A == 127,
                "opacity remains premultiplied for XNA alpha blending");
            Microsoft.Xna.Framework.Vector2[] closed = SceneValidation.ParsePath("0,0;10,0;10,10", "closed");
            Require(SceneAnimation.PathPosition(closed, 0.999f, true).X < 0.1f, "closed path returns seamlessly to its first point");
            Microsoft.Xna.Framework.Vector2 splineStart = SceneAnimation.SplinePathPosition(closed, 0f, true);
            Microsoft.Xna.Framework.Vector2 splineEnd = SceneAnimation.SplinePathPosition(closed, 1f, true);
            Require(Microsoft.Xna.Framework.Vector2.Distance(splineStart, splineEnd) < 0.0001f,
                "closed spline position has a seamless wrap");
            Microsoft.Xna.Framework.Vector2 tangentBefore = SceneAnimation.SplinePathTangent(closed, 0.9999f, true);
            Microsoft.Xna.Framework.Vector2 tangentAfter = SceneAnimation.SplinePathTangent(closed, 0.0001f, true);
            tangentBefore.Normalize(); tangentAfter.Normalize();
            Require(Microsoft.Xna.Framework.Vector2.Dot(tangentBefore, tangentAfter) > 0.99f,
                "closed spline direction remains continuous across the wrap");
        }

        private static void WaterSimulation()
        {
            WaterSurfaceSimulation surface = new WaterSurfaceSimulation(96, 19f, 72f, 2.8f);
            surface.Impulse(0.5f, 18f, 0.06f);
            float initial = surface.Energy();
            for (int i = 0; i < 30; i++) surface.Step(1f / 60f);
            Require(initial > 0f && Math.Abs(surface.Sample(0.5f)) > 0.01f,
                "interactive water converts a contact impulse into a visible height field");
            for (int i = 0; i < 720; i++) surface.Step(1f / 60f);
            Require(surface.Energy() < initial * 0.15f,
                "interactive water waves propagate and damp instead of looping forever");
        }

        private static void Occlusion()
        {
            var source = new Microsoft.Xna.Framework.Vector2(0, 10);
            var blocker = new Microsoft.Xna.Framework.Rectangle(10, 0, 10, 20);
            Require(LightVisibility.Sample(source, new Microsoft.Xna.Framework.Vector2(30, 10), blocker, 1f) == 0f,
                "opaque King removes lamp irradiance behind him");
            Require(LightVisibility.Sample(source, new Microsoft.Xna.Framework.Vector2(5, 10), blocker, 1f) == 1f,
                "a receiver before the King is not shadowed");
            Require(LightVisibility.Sample(source, new Microsoft.Xna.Framework.Vector2(30, 60), blocker, 1f) == 1f,
                "rays beside the King remain lit");
            Require(!LightVisibility.Blocked(source, new Microsoft.Xna.Framework.Vector2(15, 10), blocker),
                "the King's own surface receives direct light");
            var covered = new Microsoft.Xna.Framework.Vector2(15, 10);
            Require(LightVisibility.Blocked(covered, source, blocker),
                "a ray originating inside the King is blocked, not omnidirectional");
            Require(LightVisibility.SourceVisibility(covered, blocker) == 0f,
                "fully covered soft emitter suppresses direct glow and local rims");
            bool extinguished = true;
            for (int y = -20; y <= 40; y += 5) for (int x = -20; x <= 50; x += 5)
                foreach (float opacity in new[] { 0f, .94f, 1f })
                    if (LightVisibility.Sample(covered, new Microsoft.Xna.Framework.Vector2(x, y), blocker, opacity) != 0f)
                        extinguished = false;
            Require(extinguished, "covered emitter contributes zero in every direction and on the King, independent of shadow softness");
            var edge = new Microsoft.Xna.Framework.Vector2(10, 10);
            Require(Math.Abs(LightVisibility.SourceVisibility(edge, blocker) - .2f) < .00001f
                && Math.Abs(LightVisibility.Sample(edge, source, blocker, .94f) - .2f) < .00001f,
                "partially covered emitter retains only its exposed sample without a light burst");
            Require(LightVisibility.Sample(covered, source, Microsoft.Xna.Framework.Rectangle.Empty, 1f) == 1f,
                "removing the King restores source illumination");
        }

        private static void CanopyAndRims()
        {
            var root = new Microsoft.Xna.Framework.Vector2(100, 180);
            Require(CanopyWind.Bend(root, root, 140, 7, 1, 14, .3f) == root,
                "wind pins the root exactly");
            var left = new Microsoft.Xna.Framework.Vector2(70, 75);
            var right = new Microsoft.Xna.Framework.Vector2(130, 75);
            var a = CanopyWind.Bend(left, root, 140, 7, 1, 14, .3f);
            var b = CanopyWind.Bend(right, root, 140, 7, 1, 14, .3f);
            Require(Microsoft.Xna.Framework.Vector2.Distance(a-left,b-right) > .01f,
                "different branches on the same row do not translate as one strip");
            Require(Microsoft.Xna.Framework.Vector2.Distance(a,CanopyWind.Bend(left,root,140,7.001f,1,14,.3f)) < .01f,
                "branch motion is temporally continuous");
            Require(CanopyWind.Bend(left,root,140,7,0,14,.3f) == left, "zero wind preserves art exactly");
            var pose = new CanopyWind.Pose(); pose.Update(7,1,14,.3f);
            var binding = CanopyWind.Bind(left,root,140);
            Require(Microsoft.Xna.Framework.Vector2.Distance(CanopyWind.Evaluate(binding,pose),a)<.0001f,
                "cached skinning weights preserve reference wind deformation");
            bool visibilityMatches = true;
            var random = new Random(8041);
            var lamp = new Microsoft.Xna.Framework.Vector2(104,170);
            for (int i=0;i<3000;i++)
            {
                var king = new Microsoft.Xna.Framework.Rectangle(random.Next(480),random.Next(360),18,26);
                var receiver = new Microsoft.Xna.Framework.Vector2(random.Next(480),random.Next(360));
                float transmitted=0f;
                foreach(var delta in new[]{new Microsoft.Xna.Framework.Vector2(0,0),new Microsoft.Xna.Framework.Vector2(-2,0),
                    new Microsoft.Xna.Framework.Vector2(2,0),new Microsoft.Xna.Framework.Vector2(0,-2),new Microsoft.Xna.Framework.Vector2(0,2)})
                    if(!king.Contains(lamp+delta))
                        transmitted += LightVisibility.Blocked(lamp+delta,receiver,king) ? .06f : 1f;
                if(Math.Abs(LightVisibility.Sample(lamp,receiver,king,.94f)-transmitted/5f)>.00001f)visibilityMatches=false;
            }
            Require(visibilityMatches,"conservative shadow rejection matches five-ray visibility across 3000 positions");
            var data = new Microsoft.Xna.Framework.Color[81];
            for (int y=1;y<8;y++) for(int x=1;x<8;x++) data[y*9+x]=Microsoft.Xna.Framework.Color.White;
            data[4*9+4]=Microsoft.Xna.Framework.Color.Transparent;
            var contour=new RimContour(data,9,9,3);
            var rim=contour.Direction(0,1);
            Require(rim[(4+3)*contour.Width+4+3].A==0 && rim[(3+3)*contour.Width+3+3].A==0,
                "rim does not fill internal cracks or recolor the material body");
            Require(rim[(4+3)*contour.Width+8+3].A==255,
                "rim illuminates the outside edge facing its source");
            Require(LightVisibility.Attenuation(100,134,2.2f)<.025f
                && LightVisibility.Attenuation(0,134,2.2f)==1f
                && LightVisibility.Attenuation(134,134,2.2f)==0f,
                "lantern preserves its bright center with a delicate zero-at-radius falloff");
            var timer=System.Diagnostics.Stopwatch.StartNew();
            Microsoft.Xna.Framework.Vector2 sink=Microsoft.Xna.Framework.Vector2.Zero;
            for(int i=0;i<20000;i++)sink+=CanopyWind.Bend(left,root,140,i/20000f,1,14,.3f);
            double reference=timer.Elapsed.TotalMilliseconds;timer.Restart();
            for(int frame=0;frame<10;frame++)
            { pose.Update(frame/60f,1,14,.3f);for(int i=0;i<2000;i++)sink+=CanopyWind.Evaluate(binding,pose); }
            Console.WriteLine("[PERF] 20k canopy vertices: reference="+reference.ToString("F2")+"ms, cached="
                +timer.Elapsed.TotalMilliseconds.ToString("F2")+"ms (math only; not a game FPS claim)");
            GC.KeepAlive(sink);
        }

        private static void LargeMapAndMotions(string sampleRoot)
        {
            SceneFile scene = new SceneFile();
            scene.Options.ExpectedScreens = 300;
            scene.Options.Timer = "hidden";
            scene.Options.Mirror = "both";
            scene.Textures = new[] { new TextureData { Id = "art", Path = "art.png", Columns = 4, Rows = 1, Fps = 8 } };
            scene.Props = new[] {
                new PropData { Id="rotor", Texture="art", Screen=300, X=240, Y=180, Motion="rotate", Degrees=360, Trigger="screen-enter" },
                new PropData { Id="route", Texture="art", Screen=170, X=20, Y=20, Motion="path", Path="0,0;10,4;20,0", Loop="pingpong" }
            };
            scene.Lights = new[] { new LightData { Id="lamp", Screen=300, X=100, Y=100 } };
            scene.Waters = new[] { new WaterData { Id="lake", Screen=300, X=20, Y=270, Width=400, Height=70 } };
            string root = Path.Combine(sampleRoot, "validation-root"); Directory.CreateDirectory(root);
            using(var image=new System.Drawing.Bitmap(4,1))image.Save(Path.Combine(root,"art.png"),System.Drawing.Imaging.ImageFormat.Png);
            SceneValidation.Validate(scene, root, 300);
            Require(scene.Props.Length == 2, "all prop trajectories accepted");
            Require(scene.Options.ExpectedScreens > 169, "legacy 169-screen capacity exceeded");
        }

        private static void InvalidDataIsRejected(string sampleRoot)
        {
            string root = Path.Combine(sampleRoot, "validation-root");
            SceneFile scene = new SceneFile(); scene.Options.ExpectedScreens = 301;
            bool rejected = false;
            try { SceneValidation.Validate(scene, root, 300); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "screen count mismatch rejected");

            scene = new SceneFile();
            scene.Textures = new[] { new TextureData { Id = "escape", Path = "../outside.png" } };
            rejected = false;
            try { SceneValidation.Validate(scene, root, 1); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "asset traversal rejected");

            scene = new SceneFile(); scene.Options.Mirror = "diagonal";
            rejected = false;
            try { SceneValidation.Validate(scene, root, 1); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "unknown filter rejected");
        }

        private static void CompiledCacheRoundTrip(string sampleRoot)
        {
            string root = Path.Combine(sampleRoot, "compiled-cache-root");
            string sceneDirectory = Path.Combine(root, "props", "mega-mapping-expansion");
            Directory.CreateDirectory(sceneDirectory);
            SceneFile scene = new SceneFile();
            scene.VectorAssets = new[] {
                new VectorAssetData {
                    Id = "tile", Width = 8, Height = 8, Supersample = 2,
                    Shapes = new[] { new VectorShapeData { Type="rect", X=0, Y=0, Width=8, Height=8, Fill="#336699" } }
                }
            };
            scene.Nodes = new[] { new PropData { Id="tile-node", Asset="tile", Screen=1, X=4, Y=4 } };
            string scenePath = Path.Combine(sceneDirectory, "scene.xml");
            XmlSerializer serializer = new XmlSerializer(typeof(SceneFile));
            using (FileStream stream = File.Create(scenePath)) serializer.Serialize(stream, scene);
            scene = SceneValidation.Load(root, 1);
            string cache = Path.Combine(sceneDirectory, "scene.mmgfx");
            CompiledSceneCache.Write(root, scene, cache);
            System.Collections.Generic.Dictionary<string, byte[]> loaded;
            Require(CompiledSceneCache.TryLoad(root, scene, out loaded) && loaded.ContainsKey("tile"),
                "compiled MMGFX cache round-trip");
            File.AppendAllText(scenePath, " ");
            Require(CompiledSceneCache.TryLoad(root, scene, out loaded), "formatting-only changes preserve semantic cache");
            File.WriteAllText(scenePath, File.ReadAllText(scenePath).Replace("#336699", "#446699"));
            bool staleRejected = false;
            try { CompiledSceneCache.TryLoad(root, scene, out loaded); }
            catch (InvalidDataException error) { staleRejected = error.Message.Contains("rebuild scene.mmgfx"); }
            Require(staleRejected, "stale compiled cache fails explicitly with a rebuild instruction");
        }
    }
}
