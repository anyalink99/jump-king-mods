using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        internal void ComposeScenePass(RenderTarget2D frame)
        {
            ScreenRenderPlan plan;
            WaterData[] compositeWaters = renderPlans.TryGetValue(Camera.CurrentScreenIndex1, out plan)
                ? plan.CompositeWaters : EmptyWaters;
            PuddleData[] puddles = plan == null ? EmptyPuddles : plan.Puddles;
            bool reflections = false;
            foreach (WaterData water in compositeWaters) if (!hiddenIds.Contains(water.Id)) reflections = true;
            foreach (PuddleData puddle in puddles) if (!hiddenIds.Contains(puddle.Id) && puddle.Opacity > 0) reflections = true;
            Vector3 ambient = AmbientRadiance();
            bool lighting = scene.Options.AdvancedLighting && (ambient != Vector3.One || previewMode == "light");
            bool lightField = false;
            if (lighting) foreach (LightData light in work.Lights.At(Camera.CurrentScreenIndex1))
                if (LightGain(light) > 0) { lightField = true; break; }
            if ((!reflections && !lighting) || previewMode == "alpha" || previewMode == "emission")
            { DrawPreviewOverlay(); return; }

            // A uniform ambient multiplier does not need a copied world or a light map.
            if (!reflections && !lightField && previewMode == "scene")
            {
                DrawAmbient(frame.Width, frame.Height, ambient);
                if (captureSceneOnly && captureRequested) CapturePreview(frame);
                DrawPreviewOverlay(); return;
            }

            GraphicsDevice device = Game1.instance.GraphicsDevice;
            if (compositeTarget == null || compositeTarget.Width != frame.Width || compositeTarget.Height != frame.Height)
            {
                if (compositeTarget != null) compositeTarget.Dispose();
                compositeTarget = new RenderTarget2D(device, frame.Width, frame.Height, false,
                    SurfaceFormat.Color, DepthFormat.None);
            }
            if (lightField && (lightTarget == null || lightTarget.Width != frame.Width || lightTarget.Height != frame.Height))
            {
                if (lightTarget != null) lightTarget.Dispose();
                lightTarget = new RenderTarget2D(device, frame.Width, frame.Height, false,
                    SurfaceFormat.Color, DepthFormat.None);
            }

            // JumpGame keeps one SpriteBatch open while drawing the world.  Capture
            // the complete scene after foreground/player drawing, reconstruct the
            // main target with reflections, then leave a batch open for the stock
            // pause/menu/debug UI.  UI therefore never leaks into the water.
            Viewport viewport = device.Viewport;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            bool batchOpen = true;
            try
            {
            Game1.instance.EndBatch(); batchOpen = false;
            device.SetRenderTarget(compositeTarget);
            device.Clear(Color.Transparent);
            Game1.instance.StartBatch(); batchOpen = true;
            Game1.spriteBatch.Draw(frame, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);
            Game1.instance.EndBatch(); batchOpen = false;

            DrawReflectionSources(plan);
            if (lightField) BuildLightMap(frame.Width, frame.Height);

            device.SetRenderTarget(frame);
            device.Clear(Color.Transparent);
            Game1.instance.StartBatch(); batchOpen = true;
            if (previewMode != "reflection") Game1.spriteBatch.Draw(compositeTarget, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);
            long waterStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (WaterData water in compositeWaters)
            {
                if (hiddenIds.Contains(water.Id)) continue;
                DrawCompositeReflection(ReflectionFrame(water.Screen, water.ReflectionObjects), water);
                if (previewMode == "scene") DrawCompositeHighlights(water);
            }
            foreach (PuddleData puddle in puddles)
                if (!hiddenIds.Contains(puddle.Id)) DrawPuddle(ReflectionFrame(puddle.Screen, puddle.ReflectionObjects),puddle);
            waterProfileTicks += System.Diagnostics.Stopwatch.GetTimestamp() - waterStarted;
            if (previewMode == "scene") DrawReflectionOccluders();
            if (lighting && previewMode != "reflection")
            {
                Game1.instance.EndBatch(); batchOpen = false;
                Game1.spriteBatch.Begin(SpriteSortMode.Deferred, previewMode == "light" ? BlendState.Opaque : lightMultiply, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone);
                batchOpen = true;
                if (lightField) Game1.spriteBatch.Draw(lightTarget, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);
                else Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(0, 0, frame.Width, frame.Height), new Color(ambient));
                Game1.spriteBatch.End(); batchOpen = false;
            }
            }
            finally
            {
                if (batchOpen) Game1.instance.EndBatch();
                device.SetRenderTarget(frame);
                device.Viewport = viewport;
                Game1.instance.StartBatch();
            }
            compositorProfileTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            if (captureSceneOnly && captureRequested) CapturePreview(frame);
            DrawPreviewOverlay();
        }

        private void DrawReflectionOccluders()
        {
            ScreenRenderPlan plan;
            if (renderPlans.TryGetValue(Camera.CurrentScreenIndex1, out plan))
                foreach (RenderCommand command in plan.ReflectionOccluders) if (Visible(command)) command.Draw();
        }
        private Vector3 AmbientRadiance()
        {
            ScreenLook look = CurrentLook;
            Color color = prepared.Color(string.IsNullOrEmpty(look.AmbientLight) ? scene.Options.AmbientLight : look.AmbientLight);
            float intensity = look.AmbientIntensity < 0 ? scene.Options.AmbientIntensity : look.AmbientIntensity;
            return Vector3.Clamp(color.ToVector3() * intensity * look.AmbientScale, Vector3.Zero, Vector3.One);
        }
        private void DrawAmbient(int width, int height, Vector3 ambient)
        {
            Game1.instance.EndBatch();
            Game1.spriteBatch.Begin(SpriteSortMode.Deferred, lightMultiply, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            try { Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture, new Rectangle(0, 0, width, height), new Color(ambient)); }
            finally { Game1.spriteBatch.End(); Game1.instance.StartBatch(); }
        }
        private static readonly WaterData[] EmptyWaters = new WaterData[0];
        private static readonly PuddleData[] EmptyPuddles = new PuddleData[0];
    }
}
