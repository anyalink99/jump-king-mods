using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Linq;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal struct TrackKey
    {
        internal float Time;
        internal float Value;
        internal TrackKey(float time, float value) { Time = time; Value = value; }
    }

    internal static partial class SceneValidation
    {
        internal const string RelativeScenePath = "props/mega-mapping-expansion/scene.xml";

        internal static string ResolveLevelRoot(string contentRoot)
        { return ResolveLevelRoot(contentRoot, Environment.GetCommandLineArgs()); }

        internal static string ResolveLevelRoot(string contentRoot, string[] commandLine)
        {
            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(contentRoot)) candidates.Add(contentRoot);
            foreach (string argument in Array.IndexOf(commandLine ?? new string[0], "-debug") >= 0 ? commandLine : new string[0])
            {
                string candidate = (argument ?? "").Trim().Trim('"');
                if (candidate.Length > 0 && Directory.Exists(candidate)) candidates.Add(candidate);
            }
            foreach (string candidate in candidates)
            {
                string root;
                try { root = Path.GetFullPath(candidate); }
                catch { continue; }
                string scene = Path.Combine(root, RelativeScenePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(scene)||File.Exists(Path.Combine(root,MapLayout.RelativePath)) || Directory.Exists(Path.Combine(root, "ending"))) return root;
            }
            return Path.GetFullPath(contentRoot);
        }

        internal static SceneFile Load(string levelRoot, int totalScreens)
        {
            string root = Path.GetFullPath(levelRoot);
            SceneFile scene = Read(root);
            if (scene == null) return null;
            Validate(scene, root, totalScreens);
            return scene;
        }

        internal static SceneFile Read(string levelRoot)
        {
            string root = Path.GetFullPath(levelRoot);
            string path = Path.Combine(root, RelativeScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            return SceneAuthoring.Read(path);
        }

        internal static void Validate(SceneFile scene, string root, int totalScreens)
        { Validate(scene, root, totalScreens, true); }

        internal static void Validate(SceneFile scene, string root, int totalScreens, bool loadVectorSources)
        {
            try { ValidateCore(scene, root, totalScreens, loadVectorSources); }
            catch (InvalidDataException error)
            {
                string source = Path.Combine(root, RelativeScenePath);
                if (scene != null && scene.SourceOrigins != null)
                    foreach (var origin in scene.SourceOrigins.OrderByDescending(o => o.Key.Length))
                        if (error.Message.Contains("id='" + origin.Key + "'") || error.Message.StartsWith(origin.Key + ":", StringComparison.Ordinal)
                            || error.Message.StartsWith(origin.Key + ".", StringComparison.Ordinal) || error.Message.EndsWith(": " + origin.Key, StringComparison.Ordinal))
                        { source = origin.Value; break; }
                throw new InvalidDataException(source + ": " + error.Message, error);
            }
        }
        private static void ValidateCore(SceneFile scene, string root, int totalScreens, bool loadVectorSources)
        {
            if (scene == null) throw new InvalidDataException("Mega Mapping scene is empty");
            FiniteNumbers.Validate(scene, "scene.xml");
            if (scene.Version != 1) throw new InvalidDataException("Unsupported Mega Mapping XML version: " + scene.Version);
            if (scene.Options == null) scene.Options = new SceneOptions();
            if (scene.Options.ExpectedScreens > 0 && scene.Options.ExpectedScreens > totalScreens)
                throw new InvalidDataException("expectedScreens=" + scene.Options.ExpectedScreens
                    + " but the Jump King collision atlas exposes only " + totalScreens + " screen slots");
            ValidateOptions(scene.Options);
            MapLayout layout=MapLayout.Read(root);
            if(layout!=null)
            {
                if(scene.Options.ExpectedScreens>0&&scene.Options.ExpectedScreens!=layout.Screens)
                    throw new InvalidDataException("expectedScreens="+scene.Options.ExpectedScreens+" disagrees with collision-derived map.xml screens="+layout.Screens);
                totalScreens=Math.Min(totalScreens,layout.Screens);
            }
            NarrativeValidation.Validate(scene, totalScreens);
            var lookScreens = new HashSet<int>();
            foreach (ScreenLook look in scene.ScreenLooks ?? new ScreenLook[0])
            {
                Screen(look.Screen, totalScreens, "ScreenLooks");
                if (!lookScreens.Add(look.Screen)) throw new InvalidDataException("Duplicate ScreenLook: " + look.Screen);
                Unit(look.AmbientScale, "ScreenLook.ambientScale");
                Unit(look.PlayerRimScale, "ScreenLook.playerRimScale");
                if (!string.IsNullOrEmpty(look.AmbientLight)) ParseColor(look.AmbientLight,"ScreenLook.ambientLight");
                if (look.AmbientIntensity != -1) Unit(look.AmbientIntensity,"ScreenLook.ambientIntensity");
            }

            HashSet<string> anchorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SceneAnchor anchor in scene.Anchors ?? new SceneAnchor[0])
            {
                NeedId(anchor.Id, anchorIds, "anchor"); Screen(anchor.Screen, totalScreens, anchor.Id);
                Bounds(anchor.X, anchor.Y, anchor.Width, anchor.Height, anchor.Id);
                Unit(anchor.LightOpacity, anchor.Id + ".lightOpacity");
                if (anchor.BlocksLight && !scene.Options.AdvancedLighting) throw new InvalidDataException(anchor.Id + ": blocksLight requires advancedLighting");
            }

            HashSet<string> textureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TextureData texture in scene.Textures ?? new TextureData[0])
            {
                NeedId(texture == null ? null : texture.Id, textureIds, "texture");
                if (string.IsNullOrWhiteSpace(texture.Path)) throw new InvalidDataException("Texture path is required: " + texture.Id);
                if (texture.Columns < 1 || texture.Rows < 1 || texture.Columns > 64 || texture.Rows > 64)
                    throw new InvalidDataException("Texture grid must be 1..64: " + texture.Id);
                if (float.IsNaN(texture.Fps) || texture.Fps < 0f || texture.Fps > 240f) throw new InvalidDataException("Texture fps must be 0..240: " + texture.Id);
                if(texture.Pages.Length>15)throw new InvalidDataException("Texture supports up to 16 atlas pages: "+texture.Id);
                if(texture.Frames<0||texture.Frames>texture.Columns*texture.Rows*(1+texture.Pages.Length))
                    throw new InvalidDataException("Texture frames exceed atlas capacity: "+texture.Id);
                ResolveAsset(root, texture.Path);
                foreach(var page in texture.Pages)
                {if(page==null||string.IsNullOrWhiteSpace(page.Path))throw new InvalidDataException("Texture page path is required: "+texture.Id);ResolveAsset(root,page.Path);}
            }

            HashSet<string> vectorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, VectorAssetData> vectorSources = new Dictionary<string, VectorAssetData>(StringComparer.OrdinalIgnoreCase);
            int shapeCount = 0;
            foreach (VectorAssetData asset in scene.VectorAssets ?? new VectorAssetData[0])
            {
                NeedId(asset == null ? null : asset.Id, vectorIds, "vector asset");
                if (!string.IsNullOrWhiteSpace(asset.Source) && loadVectorSources) LoadVectorSource(asset, root, vectorSources);
                if (asset.Width < 1 || asset.Height < 1 || asset.Width > 2048 || asset.Height > 2048)
                    throw new InvalidDataException("Vector asset size must be 1..2048: " + asset.Id);
                if (asset.Supersample < 1 || asset.Supersample > 4)
                    throw new InvalidDataException("Vector supersample must be 1..4: " + asset.Id);
                ValidateChoice(asset.ClipMode, asset.Id + ".clipMode", "none", "include", "exclude");
                if (!string.IsNullOrWhiteSpace(asset.ClipPath) && string.Equals(asset.ClipMode, "none", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(asset.Id + ".clipPath requires clipMode='include' or 'exclude'");
                Unit(asset.AlphaCutoff, asset.Id + ".alphaCutoff");
                if (float.IsNaN(asset.EdgeBleed) || asset.EdgeBleed < 0f || asset.EdgeBleed > 2f)
                    throw new InvalidDataException("Vector edgeBleed must be 0..2: " + asset.Id);
                if (!string.Equals(asset.ClipMode, "none", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(asset.ClipPath))
                        throw new InvalidDataException("Clipped vector asset needs clipPath: " + asset.Id);
                    using (System.Drawing.Drawing2D.GraphicsPath ignored = VectorGraphics.ParseGraphicsPath(
                        asset.ClipPath, asset.Id + ".clipPath")) { }
                }
                foreach (VectorShapeData shape in asset.Shapes ?? new VectorShapeData[0])
                {
                    ValidateChoice(shape.Type, asset.Id + ".shape.type", "path", "rect", "ellipse", "line");
                    ValidateChoice(shape.Gradient, asset.Id + ".shape.gradient", "none", "linear", "radial");
                    Unit(shape.Opacity, asset.Id + ".shape.opacity");
                    if (!string.IsNullOrWhiteSpace(shape.Fill)) ParseColor(shape.Fill, asset.Id + ".shape.fill");
                    if (!string.IsNullOrWhiteSpace(shape.Fill2)) ParseColor(shape.Fill2, asset.Id + ".shape.fill2");
                    if (!string.IsNullOrWhiteSpace(shape.Stroke)) ParseColor(shape.Stroke, asset.Id + ".shape.stroke");
                    if (shape.StrokeWidth < 0f || shape.StrokeWidth > 128f)
                        throw new InvalidDataException("Invalid vector stroke width: " + asset.Id);
                    using (System.Drawing.Drawing2D.GraphicsPath ignored = VectorGraphics.ParseGraphicsPath(
                        string.Equals(shape.Type, "path", StringComparison.OrdinalIgnoreCase) ? shape.Data : "M0 0 L1 1", asset.Id)) { }
                    shapeCount++;
                }
            }

            HashSet<string> objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (PropData prop in scene.Props ?? new PropData[0])
            {
                NeedId(prop == null ? null : prop.Id, objectIds, "object");
                if (!textureIds.Contains(prop.Texture ?? "")) throw new InvalidDataException("Unknown texture '" + prop.Texture + "': " + prop.Id);
                ValidateProp(prop, totalScreens);
                count++;
            }
            foreach (PropData node in scene.Nodes ?? new PropData[0])
            {
                NeedId(node == null ? null : node.Id, objectIds, "object");
                if (!vectorIds.Contains(node.Asset ?? "")) throw new InvalidDataException("Unknown vector asset '" + node.Asset + "': " + node.Id);
                ValidateProp(node, totalScreens);
                count++;
            }
            foreach (LightData light in (scene.Lights ?? new LightData[0]).Concat(scene.LightTemplates))
            {
                NeedId(light == null ? null : light.Id, objectIds, "object"); Screen(light.Screen, totalScreens, light.Id);
                if (light.Radius < 4f || light.Radius > 1000f) throw new InvalidDataException("Light radius must be 4..1000: " + light.Id);
                if (float.IsNaN(light.Intensity) || light.Intensity < 0f || light.Intensity > 4f)
                    throw new InvalidDataException("Light intensity must be 0..4: " + light.Id);
                if (float.IsNaN(light.Falloff) || light.Falloff < .5f || light.Falloff > 8f)
                    throw new InvalidDataException("Light falloff must be 0.5..8: " + light.Id);
                Unit(light.ShadowOpacity, light.Id + ".shadowOpacity");
                Range(light.Flicker, 0, 1, light.Id + ".flicker");
                Range(light.PulsePeriod,0,86400,light.Id+".pulsePeriod");
                Range(light.ConeWidth,0,180,light.Id+".coneWidth");Range(light.Angle,-360,360,light.Id+".angle");
                Range(light.SweepAngle,0,360,light.Id+".sweepAngle");Range(light.SweepPeriod,0,86400,light.Id+".sweepPeriod");
                Unit(light.Scatter,light.Id+".scatter");
                Range(light.RimRadius,0,2000,light.Id+".rimRadius");
                Unit(light.RimIntensity,light.Id+".rimIntensity");
                if(light.SweepAngle>0 && light.SweepPeriod<=0)throw new InvalidDataException("Light sweep requires a positive period: "+light.Id);
                if(light.Scatter>0 && light.ConeWidth<=0)throw new InvalidDataException("Light scatter requires a cone: "+light.Id);
                Unit(light.PulseAmount,light.Id+".pulseAmount");
                Range(light.PulsePhase,0,1,light.Id+".pulsePhase");
                if(light.PulseAmount>0 && light.PulsePeriod<=0)throw new InvalidDataException("Light pulse requires a positive period: "+light.Id);
                ParseColor(light.Color, light.Id + ".color"); ParseColor(light.ShadowColor, light.Id + ".shadowColor");
                ValidateChoice(light.Layer, light.Id + ".layer", "background", "world", "foreground"); count++;
            }
            foreach (BushData bush in scene.Bushes ?? new BushData[0])
            {
                NeedId(bush == null ? null : bush.Id, objectIds, "object"); Screen(bush.Screen, totalScreens, bush.Id);
                if (bush.Width < 8 || bush.Height < 8 || bush.Width > 1000 || bush.Height > 1000)
                    throw new InvalidDataException("Bush size must be 8..1000: " + bush.Id);
                ParseColor(bush.BackColor, bush.Id + ".backColor"); ParseColor(bush.FrontColor, bush.Id + ".frontColor");
                ParseColor(bush.HighlightColor, bush.Id + ".highlightColor"); count++;
            }
            foreach (WaterData water in scene.Waters ?? new WaterData[0])
            {
                NeedId(water == null ? null : water.Id, objectIds, "object"); Screen(water.Screen, totalScreens, water.Id);
                Bounds(water.X, water.Y, water.Width, water.Height, water.Id); Unit(water.Opacity, water.Id + ".opacity");
                Unit(water.ReflectionOpacity, water.Id + ".reflectionOpacity"); ParseColor(water.Color, water.Id + ".color");
                Unit(water.SceneReflectionOpacity, water.Id + ".sceneReflectionOpacity");
                ParseColor(water.Color2, water.Id + ".color2"); ParseColor(water.Highlight, water.Id + ".highlight");
                if (water.ReflectionScaleY <= 0.05f || water.ReflectionScaleY > 1f)
                    throw new InvalidDataException("reflectionScaleY must be >0.05 and <=1: " + water.Id);
                if (water.WaveSegments < 12 || water.WaveSegments > 256)
                    throw new InvalidDataException("waveSegments must be 12..256: " + water.Id);
                if (water.SurfaceTension <= 0f || water.WaveSpread < 0f || water.WaveDamping < 0f
                    || water.SplashStrength < 0f || water.WakeStrength < 0f)
                    throw new InvalidDataException("Interactive water coefficients must be non-negative: " + water.Id);
                ValidateChoice(water.Layer, water.Id + ".layer", "background", "world", "foreground"); count++;
            }
            foreach (FogData fog in scene.Fogs ?? new FogData[0])
            {
                NeedId(fog == null ? null : fog.Id, objectIds, "object"); Screen(fog.Screen, totalScreens, fog.Id);
                Bounds(fog.X, fog.Y, fog.Width, fog.Height, fog.Id); Unit(fog.Opacity, fog.Id + ".opacity");
                ParseColor(fog.Color, fog.Id + ".color"); ValidateChoice(fog.Layer, fog.Id + ".layer", "background", "world", "foreground");
                if (fog.Bands < 1 || fog.Bands > 64) throw new InvalidDataException("Fog bands must be 1..64: " + fog.Id); count++;
            }
            foreach (EmitterData emitter in scene.Emitters ?? new EmitterData[0])
            {
                NeedId(emitter == null ? null : emitter.Id, objectIds, "object"); Screen(emitter.Screen, totalScreens, emitter.Id);
                if (!vectorIds.Contains(emitter.Asset ?? "")) throw new InvalidDataException("Unknown emitter asset '" + emitter.Asset + "': " + emitter.Id);
                ValidateChoice(emitter.Layer, emitter.Id + ".layer", "background", "world", "foreground");
                if (emitter.Width <= 0f || emitter.Height <= 0f || emitter.Width > 2480f || emitter.Height > 2360f)
                    throw new InvalidDataException("Invalid emitter area: " + emitter.Id);
                if (emitter.Count < 1 || emitter.Count > 512) throw new InvalidDataException("Emitter count must be 1..512: " + emitter.Id);
                if (emitter.Lifetime <= 0.1f || emitter.Lifetime > 3600f) throw new InvalidDataException("Emitter lifetime must be >0.1: " + emitter.Id);
                if (emitter.ScaleMin <= 0f || emitter.ScaleMax < emitter.ScaleMin || emitter.ScaleMax > 16f)
                    throw new InvalidDataException("Invalid emitter scale range: " + emitter.Id);
                Unit(emitter.Opacity, emitter.Id + ".opacity"); ParseColor(emitter.Tint, emitter.Id + ".tint"); count++;
            }
            count += ValidateWeather(scene, totalScreens, objectIds);
            ReflectionSelection.Validate(scene);
            SceneResourceBudget.Validate(scene,root,loadVectorSources);
            foreach(var planet in scene.Planets)
            {
                NeedId(planet.Id,objectIds,"planet");Screen(planet.Screen,totalScreens,planet.Id);
                ValidateChoice(planet.Layer,planet.Id+".layer","background","world","foreground");
                if(!vectorIds.Contains(planet.SurfaceAsset??"") || (!string.IsNullOrEmpty(planet.CloudAsset)&&!vectorIds.Contains(planet.CloudAsset)))
                    throw new InvalidDataException("Unknown planet surface/cloud asset: "+planet.Id);
                Range(planet.X,-8192,8192,planet.Id+".x");Range(planet.Y,-8192,8192,planet.Id+".y");Range(planet.Radius,20,8192,planet.Id+".radius");
                Range(planet.Period,1,86400,planet.Id+".period");Range(planet.CloudPeriod,1,86400,planet.Id+".cloudPeriod");Unit(planet.CloudOpacity,planet.Id+".cloudOpacity");
                Range(planet.ViewTilt,-90,90,planet.Id+".viewTilt");Range(planet.AxisTilt,-180,180,planet.Id+".axisTilt");Range(planet.Longitude,-360,360,planet.Id+".longitude");
                Range(planet.Z,-10000,10000,planet.Id+".z");count++;
            }
            foreach (ShadowSurfaceData surface in scene.ShadowSurfaces)
            {
                NeedId(surface.Id,objectIds,"shadow surface"); Screen(surface.Screen,totalScreens,surface.Id);
                Unit(surface.Opacity,surface.Id+".opacity"); ParseColor(surface.Color,surface.Id+".color");
                Range(surface.PlaneY,0,359,surface.Id+".planeY"); Range(surface.ScaleY,.05f,3,surface.Id+".scaleY");
                Range(surface.ShearX,-4,4,surface.Id+".shearX"); Range(surface.MaxHeight,1,360,surface.Id+".maxHeight");
                var polygon=ParsePath(surface.Outline,surface.Id+".outline");
                if(polygon.Length<3 || polygon.Length>64) throw new InvalidDataException("Shadow outline needs 3..64 vertices: "+surface.Id);
                foreach(var point in polygon) { Range(point.X,0,480,surface.Id+".x"); Range(point.Y,surface.PlaneY,360,surface.Id+".y"); }
                if(new PuddleGeometry(polygon).Spans.Length==0) throw new InvalidDataException("Empty shadow receiver: "+surface.Id);
                count++;
            }
            // This is a whole-map authoring budget, not a per-frame draw count.
            // Detailed independent rooms compile into cached textures once.
            if (count > 4096 || shapeCount > 262144) throw new InvalidDataException(
                "Mega Mapping scene complexity limit exceeded: " + count + "/4096 objects, " + shapeCount + "/262144 shapes");
            BehaviorValidation.Validate(scene, totalScreens);
        }

        private static void ValidateProp(PropData prop, int totalScreens)
        {
            Screen(prop.Screen, totalScreens, prop.Id);
            if (float.IsNaN(prop.GazeX) || float.IsNaN(prop.GazeY) || float.IsNaN(prop.GazeResponse)
                || prop.GazeX < 0f || prop.GazeX > 32f || prop.GazeY < 0f || prop.GazeY > 32f
                || prop.GazeResponse <= 0f || prop.GazeResponse > 60f)
                throw new InvalidDataException("Gaze ranges must be 0..32 and response >0..60: " + prop.Id);
            ValidateChoice(prop.Layer, prop.Id + ".layer", "background", "world", "foreground");
            ValidateChoice(prop.Sampling, prop.Id + ".sampling", "point", "linear");
            ValidateChoice(prop.Trigger, prop.Id + ".trigger", "level-start", "screen-enter", "always");
            ValidateChoice(prop.Motion, prop.Id + ".motion", "none", "rotate", "orbit", "linear", "bob", "sway", "path");
            ValidateChoice(prop.PathInterpolation, prop.Id + ".pathInterpolation", "linear", "spline");
            ValidateChoice(prop.Loop, prop.Id + ".loop", "loop", "pingpong", "once");
            if (prop.Duration <= 0f || prop.Duration > 86400f) throw new InvalidDataException("Duration must be >0: " + prop.Id);
            if (prop.Scale <= 0f || prop.Scale > 32f || prop.ScaleX <= 0f || prop.ScaleX > 32f || prop.ScaleY <= 0f || prop.ScaleY > 32f)
                throw new InvalidDataException("Scale must be >0 and <=32: " + prop.Id);
            if (prop.FlexSlices < 2 || prop.FlexSlices > 64) throw new InvalidDataException("flexSlices must be 2..64: " + prop.Id);
            if (prop.Wind && (float.IsNaN(prop.WindHeight) || prop.WindHeight < 8f || prop.WindHeight > 4096f
                || float.IsNaN(prop.WindStrength) || prop.WindStrength < 0f || prop.WindStrength > 12f))
                throw new InvalidDataException("Wind requires windHeight 8..4096 and windStrength 0..12: " + prop.Id);
            if (prop.Wind && (prop.Flex || prop.FlutterHz > 0f))
                throw new InvalidDataException("Wind, strip flex and wing flutter cannot share one node: " + prop.Id);
            if(prop.Cloth)
            {
                Range(prop.ClothHeight,1,512,prop.Id+".clothHeight");Range(prop.ClothStrength,0,12,prop.Id+".clothStrength");
                if(prop.Wind || prop.Flex || prop.FlutterHz>0)throw new InvalidDataException("Cloth requires its own deformation node: "+prop.Id);
            }
            if (prop.FlutterHz < 0f || prop.FlutterHz > 120f || prop.FlutterAmount < 0f || prop.FlutterAmount > 0.95f)
                throw new InvalidDataException("Invalid wing flutter settings: " + prop.Id);
            if ((prop.FlutterBodyStart >= 0 || prop.FlutterBodyEnd >= 0)
                && (prop.FlutterBodyStart < 1 || prop.FlutterBodyEnd <= prop.FlutterBodyStart))
                throw new InvalidDataException("flutter body range must be ordered positive pixels: " + prop.Id);
            Unit(prop.Opacity, prop.Id + ".opacity"); ParseColor(prop.Tint, prop.Id + ".tint");
            Unit(prop.RimOpacity, prop.Id + ".rimOpacity"); ParseColor(prop.RimColor, prop.Id + ".rimColor");
            if (prop.RimPixels < 0f || prop.RimPixels > 12f) throw new InvalidDataException("rimPixels must be 0..12: " + prop.Id);
            if (string.Equals(prop.Motion, "path", StringComparison.OrdinalIgnoreCase)) ParsePath(prop.Path, prop.Id);
            foreach (TrackData track in prop.Tracks ?? new TrackData[0])
            {
                ValidateChoice(track.Property, prop.Id + ".track.property", "x", "y", "rotation", "scale", "scaleX", "scaleY", "opacity", "brightness");
                ValidateChoice(track.Easing, prop.Id + ".track.easing", "linear", "smooth", "sine");
                if (track.Cycles <= 0f || track.Cycles > 1000f || float.IsNaN(track.Cycles)
                    || track.Phase < -1000f || track.Phase > 1000f || float.IsNaN(track.Phase))
                    throw new InvalidDataException("Invalid track cycle settings: " + prop.Id);
                ParseTrack(track.Keys, prop.Id + "." + track.Property);
                if (string.Equals(track.Property, "brightness", StringComparison.OrdinalIgnoreCase))
                    foreach (TrackKey key in ParseTrack(track.Keys, prop.Id + ".brightness"))
                        if (float.IsNaN(key.Value) || key.Value < 0f || key.Value > 2f)
                            throw new InvalidDataException("Brightness keys must be 0..2: " + prop.Id);
            }
        }

        internal static Color ParseColor(string text, string name)
        {
            string value = string.IsNullOrWhiteSpace(text) ? "#FFFFFF" : text.Trim();
            if (value.StartsWith("#")) value = value.Substring(1);
            if (value.Length != 6 && value.Length != 8) throw new InvalidDataException("Color must be #RRGGBB or #RRGGBBAA: " + name);
            uint packed;
            if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packed))
                throw new InvalidDataException("Invalid color: " + name);
            if (value.Length == 6) return new Color((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed, (byte)255);
            return new Color((byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
        }

        internal static void ValidateOptions(SceneOptions options)
        {
            if (options == null) throw new InvalidDataException("Mega Mapping Options are missing");
            ValidateChoice(options.Timer, "timer", "inherit", "hidden");
            ValidateChoice(options.Mirror, "mirror", "none", "horizontal", "vertical", "both");
            ParseColor(options.Tint, "Options.tint");
            Unit(options.TintOpacity, "Options.tintOpacity");
            ParseColor(options.PlayerRimColor, "Options.playerRimColor");
            Unit(options.PlayerRimOpacity, "Options.playerRimOpacity");
            if (options.PlayerRimPixels < 0f || options.PlayerRimPixels > 12f)
                throw new InvalidDataException("playerRimPixels must be 0..12");
            ParseColor(options.AmbientLight, "Options.ambientLight");
            Unit(options.AmbientIntensity, "Options.ambientIntensity");
        }

        internal static string ResolveAsset(string root, string relative)
        {
            if (Path.IsPathRooted(relative)) throw new InvalidDataException("Texture path must be relative: " + relative);
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Texture escapes the level folder: " + relative);
            if (!string.Equals(Path.GetExtension(full), ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only PNG scene textures are supported: " + relative);
            if (!File.Exists(full)) throw new FileNotFoundException("Scene texture not found", full);
            return full;
        }

        internal static string ResolveVectorSource(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("Vector source path must be relative: " + relative);
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Vector source escapes the level folder: " + relative);
            if (!string.Equals(Path.GetExtension(full), ".xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Vector source must be descriptive XML: " + relative);
            if (!File.Exists(full)) throw new FileNotFoundException("Vector source not found", full);
            return full;
        }

        private static void LoadVectorSource(VectorAssetData target, string root, Dictionary<string, VectorAssetData> sources)
        {
            string path = ResolveVectorSource(root, target.Source);
            VectorAssetData source;
            if (!sources.TryGetValue(path, out source))
            {
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                XmlSerializer serializer = new XmlSerializer(typeof(VectorAssetData));
                serializer.UnknownAttribute += delegate(object sender, XmlAttributeEventArgs args) {
                    throw new InvalidDataException(path + ": @" + args.Attr.Name + " at line " + args.LineNumber + ": unknown attribute");
                };
                serializer.UnknownElement += delegate(object sender, XmlElementEventArgs args) {
                    throw new InvalidDataException(path + ": " + args.Element.Name + " at line " + args.LineNumber + ": unknown element");
                };
                using (FileStream stream = File.OpenRead(path))
                using (XmlReader reader = XmlReader.Create(stream, settings)) source = serializer.Deserialize(reader) as VectorAssetData;
                sources.Add(path, source);
            }
            if (source == null) throw new InvalidDataException("Empty vector source: " + target.Source);
            target.Width = source.Width; target.Height = source.Height; target.Supersample = source.Supersample;
            target.PixelSnap = target.PixelSnap || source.PixelSnap;
            if (target.AlphaCutoff == 0f) target.AlphaCutoff = source.AlphaCutoff;
            Unit(target.AlphaCutoff, target.Id + ".alphaCutoff");
            target.Shapes = source.Shapes ?? new VectorShapeData[0];
            if (target.ClipMode == "include" && !string.IsNullOrWhiteSpace(target.ClipPath))
            {
                using (var clip = VectorGraphics.ParseGraphicsPath(target.ClipPath, target.Id))
                using (var region = new System.Drawing.Region(clip))
                {
                    var selected = new List<VectorShapeData>();
                    foreach (VectorShapeData shape in target.Shapes)
                    {
                        if (shape.Type != "path") { selected.Add(shape); continue; }
                        using (var contour = VectorGraphics.ParseGraphicsPath(shape.Data, target.Id))
                        {
                            var bounds = contour.GetBounds(); bounds.Inflate(1f, 1f);
                            if (region.IsVisible(bounds)) selected.Add(shape);
                        }
                    }
                    target.Shapes = selected.ToArray();
                }
            }
        }

        internal static Microsoft.Xna.Framework.Vector2[] ParsePath(string text, string id)
        {
            string[] pairs = (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (pairs.Length < 2 || pairs.Length > 128) throw new InvalidDataException("Path needs 2..128 x,y points: " + id);
            Microsoft.Xna.Framework.Vector2[] result = new Microsoft.Xna.Framework.Vector2[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] values = pairs[i].Split(','); float x, y;
                if (values.Length != 2 || !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                    || !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                    throw new InvalidDataException("Invalid path point in " + id + ": " + pairs[i]);
                result[i] = new Microsoft.Xna.Framework.Vector2(x, y);
            }
            return result;
        }

        internal static TrackKey[] ParseTrack(string text, string id)
        {
            string[] pairs = (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (pairs.Length < 2 || pairs.Length > 128) throw new InvalidDataException("Track needs 2..128 time:value keys: " + id);
            TrackKey[] result = new TrackKey[pairs.Length];
            float previous = -1f;
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] values = pairs[i].Split(':'); float time, value;
                if (values.Length != 2 || !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out time)
                    || !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(time) || time < 0f || time > 1f || time <= previous)
                    throw new InvalidDataException("Invalid or unordered track key in " + id + ": " + pairs[i]);
                result[i] = new TrackKey(time, value); previous = time;
            }
            if (result[0].Time != 0f || result[result.Length - 1].Time != 1f)
                throw new InvalidDataException("Track must start at 0 and end at 1: " + id);
            return result;
        }

        private static void NeedId(string id, HashSet<string> ids, string kind)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Mega Mapping " + kind + " id is required");
            if (!ids.Add(id.Trim())) throw new InvalidDataException("Duplicate Mega Mapping " + kind + " id: " + id);
        }
        private static void Screen(int value, int total, string id)
        { if (value < 1 || value > total) throw new InvalidDataException("Screen out of range for " + id + ": " + value + " (loaded " + total + ")"); }
        private static void Unit(float value, string name)
        { if (value < 0f || value > 1f || float.IsNaN(value)) throw new InvalidDataException(name + " must be 0..1"); }
        private static void Bounds(int x, int y, int width, int height, string id)
        { if (width < 1 || height < 1 || x < -2000 || y < -2000 || x + width > 2480 || y + height > 2360) throw new InvalidDataException("Invalid rectangle: " + id); }
        private static void ValidateChoice(string value, string name, params string[] choices)
        {
            foreach (string choice in choices) if (string.Equals(value ?? "", choice, StringComparison.OrdinalIgnoreCase)) return;
            throw new InvalidDataException("Invalid " + name + ": " + value + " (expected " + string.Join("/", choices) + ")");
        }
    }
}
