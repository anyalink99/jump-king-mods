using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MegaMappingExpansion
{
    [Serializable]
    [XmlRoot("MegaMapping")]
    public sealed class SceneFile
    {
        [XmlIgnore] internal System.Collections.Generic.Dictionary<string, string> SourceOrigins;
        public SceneFile()
        {
            Version = 1;
            Options = new SceneOptions();
            Textures = new TextureData[0];
            VectorAssets = new VectorAssetData[0];
            Props = new PropData[0];
            Nodes = new PropData[0];
            Lights = new LightData[0];
            Bushes = new BushData[0];
            Waters = new WaterData[0];
            Fogs = new FogData[0];
            Emitters = new EmitterData[0];
            Anchors = new SceneAnchor[0];
            ScreenLooks = new ScreenLook[0];
            Rains = new RainData[0];
            Puddles = new PuddleData[0];
            ShadowSurfaces = new ShadowSurfaceData[0];
            Surfs = new SurfData[0];
            Planets = new PlanetData[0];
            Effects = new Api.EffectDefinition[0]; Regions = new RegionData[0];
            BehaviorTrees = new BehaviorTreeData[0];
            LightTemplates = new LightData[0]; Rules = new RuleData[0]; Flags = new FlagData[0];
            Strings = new MapString[0]; Texts = new SceneText[0]; IntroPages = new IntroPage[0]; ResultPages = new ResultPage[0]; NativeActors = new NativeActorData[0];
        }

        [XmlAttribute("version")]
        public int Version { get; set; }

        [XmlElement("Options")]
        public SceneOptions Options { get; set; }

        [XmlArray("Textures"), XmlArrayItem("Texture")]
        public TextureData[] Textures { get; set; }

        [XmlArray("VectorAssets"), XmlArrayItem("Asset")]
        public VectorAssetData[] VectorAssets { get; set; }

        [XmlArray("Props"), XmlArrayItem("Prop")]
        public PropData[] Props { get; set; }

        [XmlArray("Nodes"), XmlArrayItem("Node")]
        public PropData[] Nodes { get; set; }

        [XmlArray("Lights"), XmlArrayItem("Light")]
        public LightData[] Lights { get; set; }

        [XmlArray("Bushes"), XmlArrayItem("Bush")]
        public BushData[] Bushes { get; set; }

        [XmlArray("Waters"), XmlArrayItem("Water")]
        public WaterData[] Waters { get; set; }

        [XmlArray("Fogs"), XmlArrayItem("Fog")]
        public FogData[] Fogs { get; set; }

        [XmlArray("Emitters"), XmlArrayItem("Emitter")]
        public EmitterData[] Emitters { get; set; }

        [XmlArray("Anchors"), XmlArrayItem("Anchor")]
        public SceneAnchor[] Anchors { get; set; }

        [XmlArray("ScreenLooks"), XmlArrayItem("Screen")]
        public ScreenLook[] ScreenLooks { get; set; }

        [XmlArray("Rains"), XmlArrayItem("Rain")]
        public RainData[] Rains { get; set; }

        [XmlArray("Puddles"), XmlArrayItem("Puddle")]
        public PuddleData[] Puddles { get; set; }

        [XmlArray("ShadowSurfaces"), XmlArrayItem("Surface")]
        public ShadowSurfaceData[] ShadowSurfaces { get; set; }

        [XmlArray("Surfs"), XmlArrayItem("Surf")]
        public SurfData[] Surfs { get; set; }
        [XmlArray("Planets"), XmlArrayItem("Planet")]
        public PlanetData[] Planets { get; set; }
        [XmlArray("Effects"), XmlArrayItem("Effect")] public Api.EffectDefinition[] Effects { get; set; }
        [XmlArray("Regions"), XmlArrayItem("Region")] public RegionData[] Regions { get; set; }
        [XmlArray("LightTemplates"), XmlArrayItem("Light")] public LightData[] LightTemplates { get; set; }
        [XmlArray("Rules"), XmlArrayItem("Rule")] public RuleData[] Rules { get; set; }
        [XmlArray("BehaviorTrees"), XmlArrayItem("Tree")] public BehaviorTreeData[] BehaviorTrees { get; set; }
        [XmlArray("Flags"), XmlArrayItem("Flag")] public FlagData[] Flags { get; set; }
        [XmlArray("Strings"), XmlArrayItem("String")] public MapString[] Strings { get; set; }
        [XmlArray("Texts"), XmlArrayItem("Text")] public SceneText[] Texts { get; set; }
        [XmlArray("Intro"), XmlArrayItem("Page")] public IntroPage[] IntroPages { get; set; }
        [XmlArray("Results"), XmlArrayItem("Page")] public ResultPage[] ResultPages { get; set; }
        [XmlArray("NativeActors"), XmlArrayItem("Actor")] public NativeActorData[] NativeActors { get; set; }
    }

    [Serializable]
    public sealed class ScreenLook
    {
        public ScreenLook() { AmbientScale = 1f; PlayerRimScale = 1f; AmbientLight=""; AmbientIntensity=-1; }
        [XmlAttribute("screen")] public int Screen { get; set; }
        [XmlAttribute("ambientScale"), DefaultValue(1f)] public float AmbientScale { get; set; }
        [XmlAttribute("playerRimScale"), DefaultValue(1f)] public float PlayerRimScale { get; set; }
        [XmlAttribute("ambientLight"), DefaultValue("")] public string AmbientLight { get; set; }
        [XmlAttribute("ambientIntensity"), DefaultValue(-1f)] public float AmbientIntensity { get; set; }
    }

    [Serializable]
    public sealed class SceneAnchor
    {
        public SceneAnchor() { LightOpacity = 1; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen")] public int Screen { get; set; }
        [XmlAttribute("x")] public int X { get; set; }
        [XmlAttribute("y")] public int Y { get; set; }
        [XmlAttribute("width")] public int Width { get; set; }
        [XmlAttribute("height")] public int Height { get; set; }
        [XmlAttribute("kind")] public string Kind { get; set; }
        [XmlAttribute("blocksLight"), DefaultValue(false)] public bool BlocksLight { get; set; }
        [XmlAttribute("lightOpacity"), DefaultValue(1f)] public float LightOpacity { get; set; }
    }

    [Serializable]
    public sealed class SceneOptions
    {
        [XmlAttribute("saveId")] public string SaveId { get; set; }
        public SceneOptions()
        {
            IntroText = "";
            Timer = "inherit";
            Mirror = "none";
            Tint = "#FFFFFF";
            TintOpacity = 0f;
            ExpectedScreens = 0;
            PlayerRimColor = "#91D8FF";
            PlayerRimOpacity = 0f;
            PlayerRimPixels = 2f;
            AdvancedLighting = false;
            AmbientLight = "#FFFFFF";
            AmbientIntensity = 1f;
        }

        [XmlAttribute("introText")]
        public string IntroText { get; set; }

        [XmlAttribute("timer"), DefaultValue("inherit")]
        public string Timer { get; set; }

        [XmlAttribute("mirror"), DefaultValue("none")]
        public string Mirror { get; set; }

        [XmlAttribute("tint"), DefaultValue("#FFFFFF")]
        public string Tint { get; set; }

        [XmlAttribute("tintOpacity"), DefaultValue(0f)]
        public float TintOpacity { get; set; }

        [XmlAttribute("expectedScreens"), DefaultValue(0)]
        public int ExpectedScreens { get; set; }

        [XmlAttribute("playerRimColor"), DefaultValue("#91D8FF")]
        public string PlayerRimColor { get; set; }

        [XmlAttribute("playerRimOpacity"), DefaultValue(0f)]
        public float PlayerRimOpacity { get; set; }

        [XmlAttribute("playerRimPixels"), DefaultValue(2f)]
        public float PlayerRimPixels { get; set; }

        [XmlAttribute("advancedLighting"), DefaultValue(false)]
        public bool AdvancedLighting { get; set; }
        [XmlAttribute("profiling"), DefaultValue(false)] public bool Profiling { get; set; }

        [XmlAttribute("ambientLight"), DefaultValue("#FFFFFF")]
        public string AmbientLight { get; set; }

        [XmlAttribute("ambientIntensity"), DefaultValue(1f)]
        public float AmbientIntensity { get; set; }
    }

    [Serializable]
    public sealed class TextureData
    {
        public TextureData() { Columns = 1; Rows = 1; Fps = 0f; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("path")] public string Path { get; set; }
        [XmlAttribute("columns"), DefaultValue(1)] public int Columns { get; set; }
        [XmlAttribute("rows"), DefaultValue(1)] public int Rows { get; set; }
        [XmlAttribute("fps"), DefaultValue(0f)] public float Fps { get; set; }
        [XmlAttribute("frames"), DefaultValue(0)] public int Frames { get; set; }
        private TexturePageData[] pages=new TexturePageData[0];
        // path is page zero; child pages follow it in playback order.
        [XmlElement("Page")] public TexturePageData[] Pages { get{return pages;} set{pages=value??new TexturePageData[0];} }
    }

    [Serializable]
    public sealed class TexturePageData
    {
        [XmlAttribute("path")] public string Path { get; set; }
    }

    [Serializable, XmlRoot("VectorAsset")]
    public sealed class VectorAssetData
    {
        public VectorAssetData() { Width = 64; Height = 64; Supersample = 2; Shapes = new VectorShapeData[0]; ClipPath = ""; ClipMode = "none"; }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("source")] public string Source { get; set; }
        [XmlAttribute("width"), DefaultValue(64)] public int Width { get; set; }
        [XmlAttribute("height"), DefaultValue(64)] public int Height { get; set; }
        [XmlAttribute("supersample"), DefaultValue(2)] public int Supersample { get; set; }
        [XmlAttribute("clipPath"), DefaultValue("")] public string ClipPath { get; set; }
        [XmlAttribute("clipMode"), DefaultValue("none")] public string ClipMode { get; set; }
        [XmlAttribute("edgeBleed"), DefaultValue(0f)] public float EdgeBleed { get; set; }
        [XmlAttribute("pixelSnap"), DefaultValue(false)] public bool PixelSnap { get; set; }
        [XmlAttribute("alphaCutoff"), DefaultValue(0f)] public float AlphaCutoff { get; set; }
        [XmlElement("Shape")] public VectorShapeData[] Shapes { get; set; }
    }

    [Serializable]
    public sealed class VectorShapeData
    {
        public VectorShapeData()
        {
            Type = "path"; Fill = "#FFFFFF"; Fill2 = ""; Gradient = "none";
            Stroke = ""; StrokeWidth = 0f; Opacity = 1f; Data = "";
        }
        [XmlAttribute("type"), DefaultValue("path")] public string Type { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("width")] public float Width { get; set; }
        [XmlAttribute("height")] public float Height { get; set; }
        [XmlAttribute("data"), DefaultValue("")] public string Data { get; set; }
        [XmlAttribute("fill"), DefaultValue("#FFFFFF")] public string Fill { get; set; }
        [XmlAttribute("fill2"), DefaultValue("")] public string Fill2 { get; set; }
        [XmlAttribute("gradient"), DefaultValue("none")] public string Gradient { get; set; }
        [XmlAttribute("angle"), DefaultValue(0f)] public float Angle { get; set; }
        [XmlAttribute("stroke"), DefaultValue("")] public string Stroke { get; set; }
        [XmlAttribute("strokeWidth"), DefaultValue(0f)] public float StrokeWidth { get; set; }
        [XmlAttribute("opacity"), DefaultValue(1f)] public float Opacity { get; set; }
    }

    [Serializable]
    public sealed class TrackData
    {
        public TrackData() { Property = "x"; Keys = "0:0;1:0"; Easing = "smooth"; Cycles = 1f; }
        [XmlAttribute("property"), DefaultValue("x")] public string Property { get; set; }
        [XmlAttribute("keys"), DefaultValue("0:0;1:0")] public string Keys { get; set; }
        [XmlAttribute("easing"), DefaultValue("smooth")] public string Easing { get; set; }
        [XmlAttribute("cycles"), DefaultValue(1f)] public float Cycles { get; set; }
        [XmlAttribute("phase"), DefaultValue(0f)] public float Phase { get; set; }
    }

    [Serializable]
    public sealed class PropData
    {
        public PropData()
        {
            Screen = 1; Layer = "world"; Trigger = "level-start"; Motion = "none"; Visible = true;
            Loop = "loop"; Duration = 2f; Scale = 1f; Opacity = 1f; Tint = "#FFFFFF";
            ScaleX = 1f; ScaleY = 1f; Depth = 1f; OriginX = -1f; OriginY = -1f;
            Path = ""; PathInterpolation = "linear"; OrientationOffset = 0f;
            FlexSlices = 10; Tracks = new TrackData[0]; RimColor = "#9AD9E8";
            FlutterHz = 0f; FlutterAmount = 0.7f; FlutterBodyStart = -1; FlutterBodyEnd = -1;
            GazeResponse = 7f; Sampling = "point";
        }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("texture")] public string Texture { get; set; }
        [XmlAttribute("visible"), DefaultValue(true)] public bool Visible { get; set; }
        [XmlAttribute("attach")] public string Attach { get; set; }
        [XmlAttribute("offsetX")] public float OffsetX { get; set; }
        [XmlAttribute("offsetY")] public float OffsetY { get; set; }
        [XmlAttribute("sampling"), DefaultValue("point")] public string Sampling { get; set; }
        [XmlAttribute("asset")] public string Asset { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("layer"), DefaultValue("world")] public string Layer { get; set; }
        [XmlAttribute("trigger"), DefaultValue("level-start")] public string Trigger { get; set; }
        [XmlAttribute("motion"), DefaultValue("none")] public string Motion { get; set; }
        [XmlAttribute("loop"), DefaultValue("loop")] public string Loop { get; set; }
        [XmlAttribute("duration"), DefaultValue(2f)] public float Duration { get; set; }
        [XmlAttribute("delay"), DefaultValue(0f)] public float Delay { get; set; }
        [XmlAttribute("amplitudeX"), DefaultValue(0f)] public float AmplitudeX { get; set; }
        [XmlAttribute("amplitudeY"), DefaultValue(0f)] public float AmplitudeY { get; set; }
        [XmlAttribute("degrees"), DefaultValue(0f)] public float Degrees { get; set; }
        [XmlAttribute("rotation"), DefaultValue(0f)] public float Rotation { get; set; }
        [XmlAttribute("scale"), DefaultValue(1f)] public float Scale { get; set; }
        [XmlAttribute("scaleX"), DefaultValue(1f)] public float ScaleX { get; set; }
        [XmlAttribute("scaleY"), DefaultValue(1f)] public float ScaleY { get; set; }
        [XmlAttribute("opacity"), DefaultValue(1f)] public float Opacity { get; set; }
        [XmlAttribute("tint"), DefaultValue("#FFFFFF")] public string Tint { get; set; }
        [XmlAttribute("depth"), DefaultValue(1f)] public float Depth { get; set; }
        [XmlAttribute("originX"), DefaultValue(-1f)] public float OriginX { get; set; }
        [XmlAttribute("originY"), DefaultValue(-1f)] public float OriginY { get; set; }
        [XmlAttribute("flipX"), DefaultValue(false)] public bool FlipX { get; set; }
        [XmlAttribute("flipY"), DefaultValue(false)] public bool FlipY { get; set; }
        [XmlAttribute("reactRadius"), DefaultValue(0f)] public float ReactRadius { get; set; }
        [XmlAttribute("reactStrength"), DefaultValue(0f)] public float ReactStrength { get; set; }
        [XmlAttribute("flex"), DefaultValue(false)] public bool Flex { get; set; }
        [XmlAttribute("flexSlices"), DefaultValue(10)] public int FlexSlices { get; set; }
        [XmlAttribute("wind"), DefaultValue(false)] public bool Wind { get; set; }
        [XmlAttribute("cloth"), DefaultValue(false)] public bool Cloth { get; set; }
        [XmlAttribute("clothStrength"), DefaultValue(0f)] public float ClothStrength { get; set; }
        [XmlAttribute("clothHeight"), DefaultValue(0f)] public float ClothHeight { get; set; }
        [XmlAttribute("windStrength"), DefaultValue(0f)] public float WindStrength { get; set; }
        [XmlAttribute("windHeight"), DefaultValue(0f)] public float WindHeight { get; set; }
        [XmlAttribute("reflect"), DefaultValue(false)] public bool Reflect { get; set; }
        [XmlAttribute("occludesReflection"), DefaultValue(false)] public bool OccludesReflection { get; set; }
        [XmlAttribute("rimColor"), DefaultValue("#9AD9E8")] public string RimColor { get; set; }
        [XmlAttribute("rimOpacity"), DefaultValue(0f)] public float RimOpacity { get; set; }
        [XmlAttribute("rimPixels"), DefaultValue(1f)] public float RimPixels { get; set; }
        [XmlAttribute("path"), DefaultValue("")] public string Path { get; set; }
        [XmlAttribute("pathInterpolation"), DefaultValue("linear")] public string PathInterpolation { get; set; }
        [XmlAttribute("orientToPath"), DefaultValue(false)] public bool OrientToPath { get; set; }
        [XmlAttribute("orientationOffset"), DefaultValue(0f)] public float OrientationOffset { get; set; }
        [XmlAttribute("closedPath"), DefaultValue(false)] public bool ClosedPath { get; set; }
        [XmlAttribute("flutterHz"), DefaultValue(0f)] public float FlutterHz { get; set; }
        [XmlAttribute("flutterAmount"), DefaultValue(0.7f)] public float FlutterAmount { get; set; }
        [XmlAttribute("flutterBodyStart"), DefaultValue(-1)] public int FlutterBodyStart { get; set; }
        [XmlAttribute("flutterBodyEnd"), DefaultValue(-1)] public int FlutterBodyEnd { get; set; }
        [XmlAttribute("z"), DefaultValue(0f)] public float Z { get; set; }
        [XmlAttribute("lookAtKing"), DefaultValue(false)] public bool LookAtKing { get; set; }
        [XmlAttribute("gazeX"), DefaultValue(0f)] public float GazeX { get; set; }
        [XmlAttribute("gazeY"), DefaultValue(0f)] public float GazeY { get; set; }
        [XmlAttribute("gazeResponse"), DefaultValue(7f)] public float GazeResponse { get; set; }
        [XmlElement("Track")] public TrackData[] Tracks { get; set; }
    }

    [Serializable]
    public sealed class LightData
    {
        internal LightData Clone() { return (LightData)MemberwiseClone(); }
        internal bool AttachmentVisible = true, IgnoreAttachmentActor;
        public LightData()
        {
            Screen = 1; Radius = 96f; Color = "#FFD27A"; Intensity = 0.75f; RimIntensity = 1f; Enabled = true;
            OccludeKing = true; ShadowColor = "#000000"; ShadowOpacity = 0.82f; Layer = "world"; Falloff = 1f;
        }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("radius"), DefaultValue(96f)] public float Radius { get; set; }
        [XmlAttribute("enabled"), DefaultValue(true)] public bool Enabled { get; set; }
        [XmlAttribute("attach")] public string Attach { get; set; }
        [XmlAttribute("offsetX")] public float OffsetX { get; set; }
        [XmlAttribute("offsetY")] public float OffsetY { get; set; }
        [XmlAttribute("falloff"), DefaultValue(1f)] public float Falloff { get; set; }
        [XmlAttribute("color"), DefaultValue("#FFD27A")] public string Color { get; set; }
        [XmlAttribute("intensity"), DefaultValue(0.75f)] public float Intensity { get; set; }
        [XmlAttribute("flicker"), DefaultValue(0f)] public float Flicker { get; set; }
        [XmlAttribute("pulsePeriod"), DefaultValue(0f)] public float PulsePeriod { get; set; }
        [XmlAttribute("pulseAmount"), DefaultValue(0f)] public float PulseAmount { get; set; }
        [XmlAttribute("pulsePhase"), DefaultValue(0f)] public float PulsePhase { get; set; }
        [XmlAttribute("coneWidth"), DefaultValue(0f)] public float ConeWidth { get; set; }
        [XmlAttribute("angle"), DefaultValue(0f)] public float Angle { get; set; }
        [XmlAttribute("sweepAngle"), DefaultValue(0f)] public float SweepAngle { get; set; }
        [XmlAttribute("sweepPeriod"), DefaultValue(0f)] public float SweepPeriod { get; set; }
        [XmlAttribute("scatter"), DefaultValue(0f)] public float Scatter { get; set; }
        [XmlAttribute("rimRadius"), DefaultValue(0f)] public float RimRadius { get; set; }
        [XmlAttribute("rimIntensity"), DefaultValue(1f)] public float RimIntensity { get; set; }
        [XmlAttribute("occludeKing"), DefaultValue(true)] public bool OccludeKing { get; set; }
        [XmlAttribute("shadowColor"), DefaultValue("#09101B")] public string ShadowColor { get; set; }
        [XmlAttribute("shadowOpacity"), DefaultValue(0.82f)] public float ShadowOpacity { get; set; }
        [XmlAttribute("layer"), DefaultValue("world")] public string Layer { get; set; }
        [XmlAttribute("z"), DefaultValue(0f)] public float Z { get; set; }
    }

    [Serializable]
    public sealed class BushData
    {
        public BushData()
        {
            Screen = 1; Width = 70; Height = 42; Depth = 0.8f; Sway = 3f;
            Speed = 0.7f; ReactRadius = 70f; ReactStrength = 9f;
            BackColor = "#173D38"; FrontColor = "#2D6A52"; HighlightColor = "#5B9367";
        }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("width"), DefaultValue(70)] public int Width { get; set; }
        [XmlAttribute("height"), DefaultValue(42)] public int Height { get; set; }
        [XmlAttribute("depth"), DefaultValue(0.8f)] public float Depth { get; set; }
        [XmlAttribute("sway"), DefaultValue(3f)] public float Sway { get; set; }
        [XmlAttribute("speed"), DefaultValue(0.7f)] public float Speed { get; set; }
        [XmlAttribute("reactRadius"), DefaultValue(70f)] public float ReactRadius { get; set; }
        [XmlAttribute("reactStrength"), DefaultValue(9f)] public float ReactStrength { get; set; }
        [XmlAttribute("backColor"), DefaultValue("#173D38")] public string BackColor { get; set; }
        [XmlAttribute("frontColor"), DefaultValue("#2D6A52")] public string FrontColor { get; set; }
        [XmlAttribute("highlightColor"), DefaultValue("#5B9367")] public string HighlightColor { get; set; }
    }

    [Serializable]
    public sealed class WaterData
    {
        public WaterData()
        {
            Screen = 1; Color = "#153B55"; Highlight = "#6CB9BA";
            Color2 = "#071827"; Opacity = 0.72f; Reflection = true;
            ReflectionOpacity = 0.42f; ReflectionScaleY = 0.38f; Ripple = 2f; Layer = "background";
            GlintX = -1f;
            ReflectionAssets = ""; SceneReflectionOpacity = 0.28f;
            CompositeReflection = false;
            Interactive = false; WaveSegments = 96; SurfaceTension = 19f;
            WaveSpread = 72f; WaveDamping = 2.8f; SplashStrength = 1f; WakeStrength = 0.35f;
        }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("x")] public int X { get; set; }
        [XmlAttribute("y")] public int Y { get; set; }
        [XmlAttribute("width")] public int Width { get; set; }
        [XmlAttribute("height")] public int Height { get; set; }
        [XmlAttribute("color"), DefaultValue("#153B55")] public string Color { get; set; }
        [XmlAttribute("color2"), DefaultValue("#071827")] public string Color2 { get; set; }
        [XmlAttribute("highlight"), DefaultValue("#6CB9BA")] public string Highlight { get; set; }
        [XmlAttribute("opacity"), DefaultValue(0.72f)] public float Opacity { get; set; }
        [XmlAttribute("reflection"), DefaultValue(true)] public bool Reflection { get; set; }
        [XmlAttribute("reflectionOpacity"), DefaultValue(0.42f)] public float ReflectionOpacity { get; set; }
        [XmlAttribute("ripple"), DefaultValue(2f)] public float Ripple { get; set; }
        [XmlAttribute("reflectionScaleY"), DefaultValue(0.38f)] public float ReflectionScaleY { get; set; }
        [XmlAttribute("layer"), DefaultValue("background")] public string Layer { get; set; }
        [XmlAttribute("z"), DefaultValue(0f)] public float Z { get; set; }
        [XmlAttribute("glintX"), DefaultValue(-1f)] public float GlintX { get; set; }
        [XmlAttribute("reflectionAssets"), DefaultValue("")] public string ReflectionAssets { get; set; }
        [XmlAttribute("reflectionObjects")] public string ReflectionObjects { get; set; }
        [XmlAttribute("sceneReflectionOpacity"), DefaultValue(0.28f)] public float SceneReflectionOpacity { get; set; }
        [XmlAttribute("compositeReflection"), DefaultValue(false)] public bool CompositeReflection { get; set; }
        [XmlAttribute("interactive"), DefaultValue(false)] public bool Interactive { get; set; }
        [XmlAttribute("waveSegments"), DefaultValue(96)] public int WaveSegments { get; set; }
        [XmlAttribute("surfaceTension"), DefaultValue(19f)] public float SurfaceTension { get; set; }
        [XmlAttribute("waveSpread"), DefaultValue(72f)] public float WaveSpread { get; set; }
        [XmlAttribute("waveDamping"), DefaultValue(2.8f)] public float WaveDamping { get; set; }
        [XmlAttribute("splashStrength"), DefaultValue(1f)] public float SplashStrength { get; set; }
        [XmlAttribute("wakeStrength"), DefaultValue(0.35f)] public float WakeStrength { get; set; }
    }

    [Serializable]
    public sealed class FogData
    {
        public FogData()
        {
            Screen = 1; Layer = "foreground"; Color = "#B7CDD0"; Opacity = 0.18f;
            Speed = 4f; Bands = 5; Height = 54;
        }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("x")] public int X { get; set; }
        [XmlAttribute("y")] public int Y { get; set; }
        [XmlAttribute("width")] public int Width { get; set; }
        [XmlAttribute("height"), DefaultValue(54)] public int Height { get; set; }
        [XmlAttribute("layer"), DefaultValue("foreground")] public string Layer { get; set; }
        [XmlAttribute("color"), DefaultValue("#B7CDD0")] public string Color { get; set; }
        [XmlAttribute("opacity"), DefaultValue(0.18f)] public float Opacity { get; set; }
        [XmlAttribute("speed"), DefaultValue(4f)] public float Speed { get; set; }
        [XmlAttribute("bands"), DefaultValue(5)] public int Bands { get; set; }
        [XmlAttribute("z"), DefaultValue(0f)] public float Z { get; set; }
    }

    [Serializable]
    public sealed class EmitterData
    {
        public EmitterData()
        {
            Screen = 1; Layer = "background"; Count = 12; Lifetime = 8f;
            DriftX = 8f; DriftY = -5f; Wander = 5f; Opacity = 0.6f;
            ScaleMin = 0.35f; ScaleMax = 0.8f; Depth = 0.5f;
        }
        [XmlAttribute("id")] public string Id { get; set; }
        [XmlAttribute("asset")] public string Asset { get; set; }
        [XmlAttribute("screen"), DefaultValue(1)] public int Screen { get; set; }
        [XmlAttribute("x")] public float X { get; set; }
        [XmlAttribute("y")] public float Y { get; set; }
        [XmlAttribute("width")] public float Width { get; set; }
        [XmlAttribute("height")] public float Height { get; set; }
        [XmlAttribute("layer"), DefaultValue("background")] public string Layer { get; set; }
        [XmlAttribute("z"), DefaultValue(0f)] public float Z { get; set; }
        [XmlAttribute("count"), DefaultValue(12)] public int Count { get; set; }
        [XmlAttribute("lifetime"), DefaultValue(8f)] public float Lifetime { get; set; }
        [XmlAttribute("driftX"), DefaultValue(8f)] public float DriftX { get; set; }
        [XmlAttribute("driftY"), DefaultValue(-5f)] public float DriftY { get; set; }
        [XmlAttribute("wander"), DefaultValue(5f)] public float Wander { get; set; }
        [XmlAttribute("opacity"), DefaultValue(0.6f)] public float Opacity { get; set; }
        [XmlAttribute("scaleMin"), DefaultValue(0.35f)] public float ScaleMin { get; set; }
        [XmlAttribute("scaleMax"), DefaultValue(0.8f)] public float ScaleMax { get; set; }
        [XmlAttribute("depth"), DefaultValue(0.5f)] public float Depth { get; set; }
        [XmlAttribute("tint"), DefaultValue("#FFFFFF")] public string Tint { get; set; }
    }
}
