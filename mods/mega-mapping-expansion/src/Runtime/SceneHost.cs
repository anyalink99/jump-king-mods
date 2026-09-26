using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost : IDisposable
    {
        private readonly SceneFile scene;
        private readonly PreparedScene prepared;
        private SceneWorkIndex work;
        private readonly string levelRoot;
        internal EntityManager EntityOwner { get; private set; }
        private readonly Dictionary<string, SceneTexture> textures = new Dictionary<string, SceneTexture>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SceneTexture> vectors = new Dictionary<string, SceneTexture>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, float> screenEnteredAt = new Dictionary<int, float>();
        private readonly Dictionary<string, ReactionState> reactions = new Dictionary<string, ReactionState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WaterSurfaceState> waterSurfaces = new Dictionary<string, WaterSurfaceState>(StringComparer.OrdinalIgnoreCase);
        private readonly Texture2D radial;
        private readonly BlendState lightAdditive;
        private readonly BlendState lightMultiply;
        private RenderTarget2D compositeTarget;
        private RenderTarget2D lightTarget;
        private Texture2D radianceTexture;
        private Color[] radiancePixels;
        private readonly Dictionary<Texture2D, Texture2D> silhouetteTextures = new Dictionary<Texture2D, Texture2D>();
        private float time;
        private float frameDelta = 1f / 60f;
        private int screen;
        private bool disposed;
        private bool counted;
        internal static int LiveInstances { get; private set; }

        internal static SceneHost Current { get; private set; }
        internal SceneOptions Options { get { return scene.Options; } }
        internal bool UsedCompiledCache { get; private set; }

        internal SceneHost(LoadedScene value, string root)
        {
            scene = SceneBehaviorEngine.Copy(value.Data);
            levelRoot = root;
            try
            {
            prepared = new PreparedScene(scene);
            work = new SceneWorkIndex(scene);
            foreach (ScreenLook look in scene.ScreenLooks) screenLooks.Add(look.Screen, look);
            foreach (TextureData data in scene.Textures ?? new TextureData[0])
            {
                var pages=new List<Texture2D>();
                try
                {
                    for(int page=0;page<=data.Pages.Length;page++)
                    {
                        string path=SceneValidation.ResolveAsset(root,page==0?data.Path:data.Pages[page-1].Path);
                        using(FileStream stream=File.OpenRead(path))pages.Add(ReadTexture(stream));
                    }
                }
                catch{foreach(var page in pages)page.Dispose();throw;}
                textures.Add(data.Id, new SceneTexture(data, pages.ToArray()));
            }
            Dictionary<string, byte[]> compiled = value.CompiledAssets;
            UsedCompiledCache = compiled != null;
            foreach (VectorAssetData data in scene.VectorAssets ?? new VectorAssetData[0])
            {
                Texture2D texture;
                byte[] png;
                if (UsedCompiledCache)
                {
                    png = compiled[data.Id];
                    using (MemoryStream stream = new MemoryStream(png, false))
                        texture = ReadTexture(stream);
                }
                else texture = VectorGraphics.Render(Game1.instance.GraphicsDevice, data);
                TextureData metadata = new TextureData { Id = data.Id, Columns = 1, Rows = 1, Fps = 0f };
                vectors.Add(data.Id, new SceneTexture(metadata, texture));
            }
            if (scene.Fogs.Length + scene.Bushes.Length + scene.Waters.Length + scene.Lights.Length + scene.LightTemplates.Length > 0)
                radial = CreateRadial(Game1.instance.GraphicsDevice, 128, 92);
            lightAdditive = CreateEmissionBlend();
            if (scene.Options.AdvancedLighting) lightMultiply = new BlendState {
                ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero,
                AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.Zero
            };

            BuildRenderQueues();
            PrepareWeather();
            PrepareReflectionSources();
            foreach (ShadowSurfaceData surface in scene.ShadowSurfaces) shadowReceivers.Add(surface, new PuddleGeometry(SceneValidation.ParsePath(surface.Outline, surface.Id)));
            foreach (PlanetData planet in scene.Planets) planetMeshes.Add(planet.Id, new PlanetMesh(planet));
            InitializeBehaviors();
            PrepareTexts();
            LiveInstances++; counted = true;
            }
            catch { Dispose(); throw; }
        }

        internal void Activate()
        {
            if (disposed) throw new ObjectDisposedException("SceneHost");
            EntityOwner = EntityManager.instance;
            screen = Camera.CurrentScreenIndex1;
            if (!screenEnteredAt.ContainsKey(screen)) screenEnteredAt[screen] = time;
            Current = this; PublishPersistentFlags();
            ActivateBehaviorEvents();
            NativeNarrative.Activate(this);
        }

        internal void Tick(float delta)
        {
            if (disposed || Current != this) return;
            frameDelta = Math.Min(Math.Max(delta, 0f), 0.1f);
            if (JKRuntime.Gameplay.NativePause.IsPaused) frameDelta = 0;
            if (previewPaused) { frameDelta = previewStep; previewStep = 0f; }
            time += frameDelta;
            int current = Camera.CurrentScreenIndex1;
            if (current != screen)
            {
                screen = current;
                screenEnteredAt[current] = time;
                foreach (WaterData water in work.Waters.At(current))
                { WaterSurfaceState state; if (waterSurfaces.TryGetValue(water.Id, out state)) state.ContactKnown = false; }
                foreach (PuddleData puddle in work.Puddles.At(current))
                { PuddleState state = puddleStates[puddle.Id]; state.WasTouching = false; state.Footsteps.Clear(); state.Cooldown = 0; }
            }
            if (frameDelta > 0f) { UpdateReactions(current); UpdateWaterSurfaces(current); UpdateGazes(current); UpdatePuddles(current); }
            TickBehaviors(delta);
        }

        private void UpdateReactions(int currentScreen)
        {
            PlayerEntity player = GameLoopPlayer();
            if (player == null) return;
            float playerX = Camera.TransformVector2(player.m_body.GetHitbox().Center.ToVector2()).X;
            foreach (PropData prop in work.Props.At(currentScreen))
            {
                if (prop.Screen != currentScreen || prop.ReactRadius <= 0f) continue;
                ReactionState state;
                if (!reactions.TryGetValue(prop.Id, out state)) { state = new ReactionState(); reactions[prop.Id] = state; }
                float deltaX = prop.X - playerX;
                float distance = Math.Abs(deltaX);
                float target = 0f;
                if (distance < prop.ReactRadius)
                {
                    float normalized = deltaX / prop.ReactRadius;
                    float envelope = 1f - Math.Abs(normalized);
                    target = prop.ReactStrength * normalized * envelope * 2.15f;
                }
                state.Velocity += (target - state.Angle) * 38f * frameDelta;
                state.Velocity *= (float)Math.Exp(-7.5f * frameDelta);
                state.Angle += state.Velocity * frameDelta;
            }
        }

        internal void DrawBackground()
        {
            PollPreview();
            if (!disposed) DrawLayer("background");
            else if (Current != null) Current.DrawLayer("background");
        }
        internal void DrawWorld() { DrawLayer("world"); }
        internal void DrawForeground() { DrawLayer("foreground"); }

        private void DrawLayer(string layer)
        {
            int current = Camera.CurrentScreenIndex1;
            ScreenRenderPlan plan;
            if (current < 1 || !renderPlans.TryGetValue(current, out plan)) return;
            if (previewLayer != "all" && previewLayer != layer) return;
            bool profiling = scene.Options.Profiling;
            long started = profiling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            foreach (RenderCommand command in plan.Layer(layer)) if (Visible(command))
            {
                if(!profiling){command.Draw();continue;}
                long stamp=System.Diagnostics.Stopwatch.GetTimestamp();command.Draw();
                command.ProfileTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-stamp;
            }
            if (profiling) queueProfileTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        }

        private void DrawProp(PropData prop, bool vector)
        {
            PreparedProp definition = prepared.Prop(prop);
            SceneTexture asset = vector ? vectors[prop.Asset] : textures[prop.Texture];
            float age = Timeline(prop);
            if (age < 0f) return;
            float phase = SceneAnimation.Phase(age, prop.Duration, prop.Loop);
            PropPose pose = ResolvePropPose(prop);
            if (!pose.Visible) return;
            Vector2 position = pose.Position;
            float rotation = pose.Rotation, scale = pose.Scale, scaleX = pose.ScaleX, scaleY = pose.ScaleY;
            float opacity = pose.Opacity, brightness = pose.Brightness;
            int pageIndex,frame;AtlasPlayback.Frame(asset.Data,age,out pageIndex,out frame);
            Texture2D pageTexture=asset.Pages[pageIndex];
            Rectangle source = new Rectangle((frame % asset.Data.Columns) * asset.FrameWidth,
                (frame / asset.Data.Columns) * asset.FrameHeight, asset.FrameWidth, asset.FrameHeight);
            Vector2 origin = new Vector2(prop.OriginX < 0f ? asset.FrameWidth / 2f : prop.OriginX,
                prop.OriginY < 0f ? asset.FrameHeight / 2f : prop.OriginY);
            Color baseTint = MultiplyAlpha(definition.Tint, opacity);
            SpriteEffects effects = SpriteEffects.None;
            if (prop.FlipX) effects |= SpriteEffects.FlipHorizontally;
            if (prop.FlipY) effects |= SpriteEffects.FlipVertically;
            Vector2 drawScale = new Vector2(scale * scaleX, scale * scaleY);
            Texture2D drawTexture = previewMode == "alpha" ? AlphaMask(pageTexture) : pageTexture;
            if (previewMode == "alpha") { baseTint = Color.White; brightness = 1f; }
            if (prop.RimOpacity > 0f && prop.RimPixels > 0f && previewMode == "scene")
                DrawAssetRim(pageTexture, source, position, origin, drawScale, rotation, effects,
                    definition.RimColor, prop.RimOpacity * opacity, prop.RimPixels);
            // SpriteBatch vertex colours clamp at one. Emission above one needs
            // a separate additive RGB pass, not another alpha-blended copy.
            for (int pass = previewMode == "emission" ? 1 : 0; pass < (brightness > 1f ? 2 : 1); pass++)
            {
                bool emission = pass == 1;
                bool customBatch = emission || definition.LinearSampling;
                Color tint = MultiplyBrightness(baseTint, BrightnessPassGain(brightness, pass));
                if (customBatch)
                {
                    Game1.instance.EndBatch();
                    Game1.spriteBatch.Begin(SpriteSortMode.Deferred, emission ? lightAdditive : BlendState.AlphaBlend,
                        definition.LinearSampling ? SamplerState.LinearClamp : SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone);
                }
                try
                {
                    if (prop.FlutterHz > 0f && prop.FlutterBodyStart > 0 && prop.FlutterBodyEnd > prop.FlutterBodyStart)
                        DrawWingFlutter(drawTexture, source, position, origin, drawScale, rotation, tint, effects, prop);
                    else if (prop.Wind || prop.Cloth) DrawCanopy(drawTexture, source, position, origin, drawScale, rotation, tint, effects, prop, age, emission);
                    else if (prop.Flex) DrawFlexible(drawTexture, source, position, origin, drawScale, rotation, tint, effects, prop.FlexSlices);
                    else Game1.spriteBatch.Draw(drawTexture, position, source, tint, rotation, origin, drawScale, effects, 0f);
                }
                finally
                {
                    if (customBatch) { Game1.instance.EndBatch(); Game1.instance.StartBatch(); }
                }
            }
            if (previewAllowed && selectedId == prop.Id)
            {
                Vector2 topLeft = position - origin * drawScale;
                Outline(new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)(source.Width * Math.Abs(drawScale.X)),
                    (int)(source.Height * Math.Abs(drawScale.Y))), Color.Magenta);
                DrawLine(position - new Vector2(3, 0), position + new Vector2(3, 0), Color.Yellow, 1f);
                DrawLine(position - new Vector2(0, 3), position + new Vector2(0, 3), Color.Yellow, 1f);
            }
        }

        private void DrawWingFlutter(Texture2D texture, Rectangle source, Vector2 position, Vector2 origin,
            Vector2 scale, float rotation, Color tint, SpriteEffects effects, PropData prop)
        {
            int bodyStart = Math.Max(1, Math.Min(source.Width - 2, prop.FlutterBodyStart));
            int bodyEnd = Math.Max(bodyStart + 1, Math.Min(source.Width - 1, prop.FlutterBodyEnd));
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * prop.FlutterHz * MathHelper.TwoPi);
            float wingScaleY = 1f - prop.FlutterAmount * pulse;
            float sweep = MathHelper.ToRadians(5.5f) * pulse;
            bool flipX = (effects & SpriteEffects.FlipHorizontally) != 0;
            bool flipY = (effects & SpriteEffects.FlipVertically) != 0;
            float directionX = flipX ? -1f : 1f, directionY = flipY ? -1f : 1f;

            Rectangle left = new Rectangle(source.X, source.Y, bodyStart, source.Height);
            Rectangle body = new Rectangle(source.X + bodyStart, source.Y, bodyEnd - bodyStart, source.Height);
            Rectangle right = new Rectangle(source.X + bodyEnd, source.Y, source.Width - bodyEnd, source.Height);
            Vector2 leftHinge = position + RotateVector(new Vector2((bodyStart - origin.X) * scale.X * directionX,
                (source.Height * 0.5f - origin.Y) * scale.Y * directionY), rotation);
            Vector2 rightHinge = position + RotateVector(new Vector2((bodyEnd - origin.X) * scale.X * directionX,
                (source.Height * 0.5f - origin.Y) * scale.Y * directionY), rotation);
            Vector2 wingScale = new Vector2(scale.X, scale.Y * wingScaleY);
            Game1.spriteBatch.Draw(texture, leftHinge, left, tint, rotation - sweep * directionX,
                new Vector2(bodyStart, source.Height * 0.5f), wingScale, effects, 0f);
            Game1.spriteBatch.Draw(texture, rightHinge, right, tint, rotation + sweep * directionX,
                new Vector2(0f, source.Height * 0.5f), wingScale, effects, 0f);
            Game1.spriteBatch.Draw(texture, position, body, tint, rotation,
                new Vector2(origin.X - bodyStart, origin.Y), scale, effects, 0f);
        }

        private static Vector2 RotateVector(Vector2 value, float radians)
        {
            float cosine = (float)Math.Cos(radians), sine = (float)Math.Sin(radians);
            return new Vector2(value.X * cosine - value.Y * sine, value.X * sine + value.Y * cosine);
        }

        private float Timeline(PropData prop)
        {
            if (string.Equals(prop.Trigger, "screen-enter", StringComparison.OrdinalIgnoreCase))
            {
                float entered;
                if (!screenEnteredAt.TryGetValue(prop.Screen, out entered)) return -1f;
                return time - entered - prop.Delay;
            }
            return time - prop.Delay;
        }

        private static PlayerEntity GameLoopPlayer()
        { return JumpKing.GameManager.GameLoop.m_player; }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 edge = end - start; float length = edge.Length();
            if (length < 0.01f) return;
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, start, null, color,
                (float)Math.Atan2(edge.Y, edge.X), new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);
        }

        internal static BlendState CreateEmissionBlend()
        {
            return new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
                AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One };
        }

        internal static float BrightnessPassGain(float brightness, int pass)
        {
            return MathHelper.Clamp(brightness - pass, 0f, 1f);
        }

        internal static Color MultiplyBrightness(Color color, float brightness)
        {
            brightness = MathHelper.Clamp(brightness, 0f, 1f);
            return new Color((byte)(color.R * brightness), (byte)(color.G * brightness),
                (byte)(color.B * brightness), color.A);
        }

        internal static Color MultiplyAlpha(Color color, float alpha)
        {
            float amount = MathHelper.Clamp(alpha, 0f, 1f);
            return new Color((byte)(color.R * amount), (byte)(color.G * amount),
                (byte)(color.B * amount), (byte)(color.A * amount));
        }

        private static float PositiveModulo(float value, float modulo)
        { float result = value % modulo; return result < 0f ? result + modulo : result; }

        private static int StableHash(string text)
        {
            unchecked { int hash = 17; foreach (char c in text ?? "") hash = hash * 31 + c; return hash & int.MaxValue; }
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
                return (x & 0x00FFFFFFU) / 16777215f;
            }
        }

    }

    internal sealed class RenderCommand : IComparable<RenderCommand>
    {
        internal readonly float Z; internal readonly int Order; internal readonly Action Draw;
        internal readonly string Id;
        internal long ProfileTicks;
        internal RenderCommand(float z, int order, Action draw, string id = "") { Z = z; Order = order; Draw = draw; Id = id; }
        public int CompareTo(RenderCommand other)
        { int value = Z.CompareTo(other.Z); return value != 0 ? value : Order.CompareTo(other.Order); }
    }

    internal sealed class ReactionState
    { internal float Angle; internal float Velocity; }
}
