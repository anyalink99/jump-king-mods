using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using JumpKing.JKMemory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WardrobePlus
{
    internal static class FitBaker
    {
        private sealed class Cell { internal int Group, Frame, X, Y; internal Sprite Sprite; internal FitAdjustment Fit; }
        // Bake translated pixels into padded frames so ordinary game sprites, reflections,
        // replay layers and flattened Ball King outfits all see the same adjustment.
        internal static void Apply(KingSprites sprites, Outfit outfit, string baseId, string sourceId, int item, List<Texture2D> owned)
        {
            var cells = new List<Cell>(); int x = 0, y = 0, row = 0;
            for (int group = 0; group < sprites.m_groups.Count; group++)
                foreach (var pair in NativeAppearance.Frames(sprites.m_groups[group]).OrderBy(p => p.Key))
                {
                    var fit = outfit.Fit(baseId, sourceId, item, group, pair.Key);
                    if (fit.X == 0 && fit.Y == 0) continue;
                    int width = pair.Value.source.Width + 64, height = pair.Value.source.Height + 64;
                    if (width > 2048) throw new InvalidOperationException("Frame too wide for fitting");
                    if (x + width > 2048) { x = 0; y += row; row = 0; }
                    cells.Add(new Cell { Group = group, Frame = pair.Key, X = x, Y = y, Sprite = pair.Value, Fit = fit });
                    x += width; row = Math.Max(row, height);
                }
            if (cells.Count == 0) return;
            int totalHeight = y + row;
            if (totalHeight > 8192) throw new InvalidOperationException("Fitted atlas exceeds the texture budget");
            var pixels = new Color[2048 * totalHeight];
            var input = cells[0].Sprite.texture;
            var original = new Color[input.Width * input.Height]; input.GetData(original);
            foreach (var cell in cells)
                CopyFrame(original, input.Width, cell.Sprite.source, pixels, 2048, new Point(cell.X + 32 + cell.Fit.X, cell.Y + 32 + cell.Fit.Y));
            var texture = new Texture2D(input.GraphicsDevice, 2048, totalHeight, false, SurfaceFormat.Color);
            owned.Add(texture); texture.SetData(pixels);
            foreach (var cell in cells)
            {
                var old = cell.Sprite;
                var rect = new Rectangle(cell.X, cell.Y, old.source.Width + 64, old.source.Height + 64);
                var center = new Vector2((old.source.Width * old.center.X + 32) / rect.Width, (old.source.Height * old.center.Y + 32) / rect.Height);
                var fitted = Sprite.CreateSpriteWithCenter(texture, rect, center);
                var crystal = old as CrystalSprite;
                var cosmic = old as CosmicSprite;
                NativeAppearance.Frames(sprites.m_groups[cell.Group])[cell.Frame] = cosmic != null ? cosmic.Fitted(fitted, 32 + cell.Fit.X, 32 + cell.Fit.Y)
                    : crystal == null ? fitted : crystal.Fitted(fitted, 32 + cell.Fit.X, 32 + cell.Fit.Y);
            }
        }
        internal static void CopyFrame(Color[] source, int sourceWidth, Rectangle frame, Color[] target, int targetWidth, Point destination)
        {
            for (int y = 0; y < frame.Height; y++)
                Array.Copy(source, (frame.Y + y) * sourceWidth + frame.X, target, (destination.Y + y) * targetWidth + destination.X, frame.Width);
        }
        internal static Point Nudge(Point current, int dx, int dy, bool flipped)
        {
            return new Point(Math.Max(-32, Math.Min(32, current.X + (flipped ? -dx : dx))), Math.Max(-32, Math.Min(32, current.Y + dy)));
        }
    }
}
