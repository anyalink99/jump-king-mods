using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    // Fill tiny tracing seams and enclosed holes, then illuminate only the exterior.
    // The original artwork remains untouched: a crack is never a light emitter.
    internal sealed class RimContour
    {
        internal readonly int Width, Height, Padding;
        private readonly bool[] solid;
        internal RimContour(Color[] pixels, int width, int height, int padding)
        {
            Padding = padding; Width = width + padding * 2; Height = height + padding * 2;
            bool[] raw = new bool[Width * Height], expanded = new bool[raw.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                raw[(y + padding) * Width + x + padding] = pixels[y * width + x].A > 24;
            for (int y = 1; y < Height - 1; y++) for (int x = 1; x < Width - 1; x++)
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    expanded[y * Width + x] |= raw[(y + dy) * Width + x + dx];
            solid = new bool[raw.Length];
            for (int y = 1; y < Height - 1; y++) for (int x = 1; x < Width - 1; x++)
            {
                bool filled = true;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    filled &= expanded[(y + dy) * Width + x + dx];
                solid[y * Width + x] = filled || raw[y * Width + x];
            }
            bool[] exterior = new bool[raw.Length]; var queue = new Queue<int>();
            exterior[0] = true; queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % Width, y = i / Width;
                if (x > 0) Visit(i - 1, exterior, queue);
                if (x + 1 < Width) Visit(i + 1, exterior, queue);
                if (y > 0) Visit(i - Width, exterior, queue);
                if (y + 1 < Height) Visit(i + Width, exterior, queue);
            }
            for (int i = 0; i < solid.Length; i++) solid[i] = !exterior[i];
        }
        private void Visit(int i, bool[] exterior, Queue<int> queue)
        { if (!solid[i] && !exterior[i]) { exterior[i] = true; queue.Enqueue(i); } }
        internal Color[] Direction(float angle, int radius)
        {
            var result = new Color[solid.Length];
            int dx = (int)Math.Round(Math.Cos(angle) * radius), dy = (int)Math.Round(Math.Sin(angle) * radius);
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                int sx = x - dx, sy = y - dy;
                if (!solid[y * Width + x] && sx >= 0 && sx < Width && sy >= 0 && sy < Height && solid[sy * Width + sx])
                    result[y * Width + x] = Color.White;
            }
            return result;
        }
    }
}
