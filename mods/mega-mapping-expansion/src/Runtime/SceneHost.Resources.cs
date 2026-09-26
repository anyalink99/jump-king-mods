using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed class SceneTexture : IDisposable
    {
        internal readonly TextureData Data;
        internal readonly Texture2D Texture;
        internal readonly Texture2D[] Pages;
        internal readonly int FrameWidth;
        internal readonly int FrameHeight;

        internal SceneTexture(TextureData data, Texture2D texture):this(data,new[]{texture}){}
        internal SceneTexture(TextureData data, Texture2D[] pages)
        {
            Data = data; Pages=pages;Texture = pages[0];
            foreach(Texture2D texture in pages)
            {
                if (texture.Width % data.Columns != 0 || texture.Height % data.Rows != 0
                    ||texture.Width!=Texture.Width||texture.Height!=Texture.Height)
                {Dispose();throw new InvalidDataException("Atlas pages must share dimensions divisible by their grid: " + data.Id);}
            }
            FrameWidth = Texture.Width / data.Columns;
            FrameHeight = Texture.Height / data.Rows;
        }
        private JKRuntime.RuntimeScope release;
        public void Dispose()
        {
            if (release == null) { release = new JKRuntime.RuntimeScope(); foreach (var page in Pages) release.Own(page); }
            release.Dispose();
        }
    }

    internal sealed partial class SceneHost
    {
        private static Texture2D ReadTexture(Stream stream)
        {
            Texture2D texture = Texture2D.FromStream(Game1.instance.GraphicsDevice, stream);
            try { Premultiply(texture); return texture; }
            catch { texture.Dispose(); throw; }
        }
        private static void Premultiply(Texture2D texture)
        {
            Color[] pixels = new Color[texture.Width * texture.Height]; texture.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = pixels[i].A / 255f;
                pixels[i] = new Color((byte)(pixels[i].R * a), (byte)(pixels[i].G * a), (byte)(pixels[i].B * a), pixels[i].A);
            }
            texture.SetData(pixels);
        }

        private static Texture2D CreateRadial(GraphicsDevice device, int size, int maximumAlpha)
        {
            Texture2D texture = new Texture2D(device, size, size);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center, dy = (y - center) / center;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float strength = MathHelper.Clamp(1f - distance, 0f, 1f);
                strength = strength * strength * (3f - 2f * strength);
                byte alpha = (byte)(strength * Math.Max(0, Math.Min(255, maximumAlpha)));
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
            try { texture.SetData(pixels); return texture; }
            catch { texture.Dispose(); throw; }
        }

        private JKRuntime.RuntimeScope release;
        private bool resourcesReleased;
        public void Dispose()
        {
            if (resourcesReleased) return;
            Exception narrativeError = null;
            if (release == null)
            {
                try { if (behaviors != null && Narrative != null) NativeNarrative.CaptureResults(this); }
                catch (Exception error) { narrativeError = error; }
                disposed = true;
                if (counted) { LiveInstances--; counted = false; }
                if (Current == this) Current = null;
                release = new JKRuntime.RuntimeScope();
                foreach (SceneTexture texture in textures.Values) release.Own(texture);
                foreach (SceneTexture texture in vectors.Values) release.Own(texture);
                foreach (Texture2D texture in silhouetteTextures.Values) release.Own(texture);
                foreach (Sprite sprite in playerAppearances.Values) release.Own(sprite.texture);
                foreach (Texture2D mask in alphaMasks.Values) release.Own(mask);
                foreach (ReflectionSource source in reflectionSources.Values) release.Own(source.Target);
                foreach (var frames in rimFrames.Values) foreach (RimFrame frame in frames.Values)
                    foreach (Texture2D direction in frame.Directions) if (direction != null) release.Own(direction);
                foreach (IDisposable resource in new IDisposable[] { radial, lightAdditive, lightMultiply,
                    canopyEffect, surfEffect, planetEffect, radianceTexture, compositeTarget, lightTarget, behaviors, gameplayEvents })
                    if (resource != null) release.Own(resource);
            }
            // The scope attempts every release and retains only failed resources
            // for a later attempt. Drawing stops as soon as teardown starts.
            release.Dispose();
            textures.Clear(); vectors.Clear(); silhouetteTextures.Clear();
            playerAppearances.Clear(); playerAppearanceVersions.Clear(); playerAtlasPixels.Clear();
            shadowReceivers.Clear(); alphaMasks.Clear(); rimFrames.Clear(); canopyMeshes.Clear();
            reflectionSources.Clear();
            surfMeshes.Clear(); scatterMeshes.Clear(); planetMeshes.Clear();
            compositeTarget = null; lightTarget = null;
            resourcesReleased = true;
            ModEntry.Log("Scene resources released; live instances=" + LiveInstances);
            if (narrativeError != null) throw new InvalidOperationException("Result page capture failed; scene resources were still released", narrativeError);
        }
    }
}
