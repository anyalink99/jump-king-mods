using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal sealed class CrystalMaps : IDisposable
    {
        internal readonly Texture2D Surface, Facets;
        internal CrystalMaps(GraphicsDevice device, int width, int height, List<Texture2D> owned)
        {
            Surface = new Texture2D(device, width, height);
            if (owned != null) owned.Add(Surface);
            try { Facets = new Texture2D(device, width, height); }
            catch { Surface.Dispose(); throw; }
            if (owned != null) owned.Add(Facets);
        }
        internal static void Pack(CrystalPixel pixel, out Color surface, out Color facet)
        {
            surface = new Color(pixel.Surface.R, pixel.Surface.G, pixel.Surface.B, (byte)Math.Round(pixel.Coverage * 255));
            facet = new Color(pixel.Dx + 4, pixel.Dy + 3, (int)Math.Round(pixel.Transmission * 255), 255);
        }
        internal static CrystalMaps FromPixels(GraphicsDevice device, int width, int height, CrystalPixel[] pixels)
        {
            var surface = new Color[width * height]; var facets = new Color[width * height];
            foreach (var pixel in pixels) Pack(pixel, out surface[pixel.Y * width + pixel.X], out facets[pixel.Y * width + pixel.X]);
            var result = new CrystalMaps(device, width, height, null);
            try { result.Surface.SetData(surface); result.Facets.SetData(facets); return result; }
            catch { result.Dispose(); throw; }
        }
        public void Dispose() { Surface.Dispose(); Facets.Dispose(); }
    }
}
