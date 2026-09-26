using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace MegaMappingExpansion
{
    internal static class VectorGraphics
    {
        private static readonly Regex TokenPattern = new Regex(
            @"[MmLlCcQqZz]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static Texture2D Render(GraphicsDevice device, VectorAssetData asset)
        {
            byte[] png = RenderPngBytes(asset);
            using (MemoryStream stream = new MemoryStream(png, false))
            {
                Texture2D texture = Texture2D.FromStream(device, stream);
                try { Premultiply(texture); return texture; }
                catch { texture.Dispose(); throw; }
            }
        }

        internal static byte[] RenderPngBytes(VectorAssetData asset)
        {
            int sample = asset.PixelSnap ? 1 : Math.Max(1, Math.Min(4, asset.Supersample));
            using (Bitmap large = new Bitmap(asset.Width * sample, asset.Height * sample, PixelFormat.Format32bppPArgb))
            {
                using (Graphics graphics = Graphics.FromImage(large))
                {
                    graphics.Clear(System.Drawing.Color.Transparent);
                    graphics.SmoothingMode = asset.PixelSnap ? SmoothingMode.None : SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = asset.PixelSnap ? PixelOffsetMode.None : PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.ScaleTransform(sample, sample);
                    ApplyAssetClip(graphics, asset);
                    foreach (VectorShapeData shape in asset.Shapes ?? new VectorShapeData[0])
                    {
                        // Palette-traced neighbouring polygons need shared edge coverage;
                        // otherwise antialiasing opens thousands of transparent hairlines.
                        if (asset.EdgeBleed > 0f && shape.Type == "path" && !string.IsNullOrWhiteSpace(shape.Fill))
                            using (GraphicsPath edge = ParseGraphicsPath(shape.Data, asset.Id))
                            using (Pen pen = new Pen(ToDrawingColor(shape.Fill, shape.Opacity, asset.Id), asset.EdgeBleed))
                            { pen.LineJoin = LineJoin.Round; graphics.DrawPath(pen, edge); }
                        DrawShape(graphics, shape, asset.Id);
                    }
                }

                using (Bitmap final = new Bitmap(asset.Width, asset.Height, PixelFormat.Format32bppPArgb))
                using (Graphics graphics = Graphics.FromImage(final))
                using (MemoryStream stream = new MemoryStream())
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(large, new Rectangle(0, 0, final.Width, final.Height));
                    if (asset.AlphaCutoff > 0f)
                        for (int y = 0; y < final.Height; y++) for (int x = 0; x < final.Width; x++)
                        {
                            Color pixel = final.GetPixel(x, y);
                            final.SetPixel(x, y, pixel.A >= asset.AlphaCutoff * 255f
                                ? Color.FromArgb(255, pixel.R, pixel.G, pixel.B) : Color.Transparent);
                        }
                    final.Save(stream, ImageFormat.Png);
                    return stream.ToArray();
                }
            }
        }

        private static void ApplyAssetClip(Graphics graphics, VectorAssetData asset)
        {
            if (string.IsNullOrWhiteSpace(asset.ClipPath)
                || string.Equals(asset.ClipMode, "none", StringComparison.OrdinalIgnoreCase)) return;
            using (GraphicsPath path = ParseGraphicsPath(asset.ClipPath, asset.Id + ".clipPath"))
            using (Region region = new Region(new RectangleF(0f, 0f, asset.Width, asset.Height)))
            {
                path.FillMode = System.Drawing.Drawing2D.FillMode.Winding;
                if (string.Equals(asset.ClipMode, "exclude", StringComparison.OrdinalIgnoreCase)) region.Exclude(path);
                else region.Intersect(path);
                graphics.SetClip(region, CombineMode.Replace);
            }
        }

        internal static GraphicsPath ParseGraphicsPath(string data, string id)
        {
            MatchCollection matches = TokenPattern.Matches(data ?? "");
            if (matches.Count == 0) throw new InvalidDataException("Empty vector path: " + id);
            List<string> tokens = new List<string>();
            foreach (Match match in matches) tokens.Add(match.Value);
            GraphicsPath path = new GraphicsPath(System.Drawing.Drawing2D.FillMode.Alternate);
            int index = 0;
            char command = '\0';
            PointF current = new PointF();
            PointF start = new PointF();
            while (index < tokens.Count)
            {
                if (IsCommand(tokens[index])) command = tokens[index++][0];
                if (command == '\0') throw new InvalidDataException("Vector path command expected: " + id);
                bool relative = char.IsLower(command);
                char upper = char.ToUpperInvariant(command);
                if (upper == 'Z')
                {
                    path.CloseFigure(); current = start; command = '\0'; continue;
                }
                if (upper == 'M' || upper == 'L')
                {
                    PointF point = ReadPoint(tokens, ref index, relative ? current : new PointF(), id);
                    if (upper == 'M') { path.StartFigure(); current = start = point; command = relative ? 'l' : 'L'; }
                    else { path.AddLine(current, point); current = point; }
                }
                else if (upper == 'C')
                {
                    PointF c1 = ReadPoint(tokens, ref index, relative ? current : new PointF(), id);
                    PointF c2 = ReadPoint(tokens, ref index, relative ? current : new PointF(), id);
                    PointF end = ReadPoint(tokens, ref index, relative ? current : new PointF(), id);
                    path.AddBezier(current, c1, c2, end); current = end;
                }
                else if (upper == 'Q')
                {
                    PointF control = ReadPoint(tokens, ref index, relative ? current : new PointF(), id);
                    PointF end = ReadPoint(tokens, ref index, relative ? current : new PointF(), id);
                    PointF c1 = new PointF(current.X + (control.X - current.X) * 2f / 3f,
                        current.Y + (control.Y - current.Y) * 2f / 3f);
                    PointF c2 = new PointF(end.X + (control.X - end.X) * 2f / 3f,
                        end.Y + (control.Y - end.Y) * 2f / 3f);
                    path.AddBezier(current, c1, c2, end); current = end;
                }
                else throw new InvalidDataException("Unsupported vector path command '" + command + "': " + id);
            }
            return path;
        }

        private static void DrawShape(Graphics graphics, VectorShapeData shape, string assetId)
        {
            if (shape == null) return;
            using (GraphicsPath path = ShapePath(shape, assetId))
            {
                RectangleF bounds = path.GetBounds();
                if (!string.IsNullOrWhiteSpace(shape.Fill))
                    using (Brush brush = CreateBrush(shape, bounds, assetId)) graphics.FillPath(brush, path);
                if (!string.IsNullOrWhiteSpace(shape.Stroke) && shape.StrokeWidth > 0f)
                    using (Pen pen = new Pen(ToDrawingColor(shape.Stroke, shape.Opacity, assetId + ".stroke"), shape.StrokeWidth))
                    { pen.LineJoin = LineJoin.Round; pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; graphics.DrawPath(pen, path); }
            }
        }

        private static GraphicsPath ShapePath(VectorShapeData shape, string assetId)
        {
            string type = (shape.Type ?? "path").ToLowerInvariant();
            if (type == "path") return ParseGraphicsPath(shape.Data, assetId);
            GraphicsPath path = new GraphicsPath(System.Drawing.Drawing2D.FillMode.Alternate);
            RectangleF rect = new RectangleF(shape.X, shape.Y, shape.Width, shape.Height);
            if (type == "rect") path.AddRectangle(rect);
            else if (type == "ellipse") path.AddEllipse(rect);
            else if (type == "line")
            {
                string[] pair = (shape.Data ?? "").Split(',');
                float x2, y2;
                if (pair.Length != 2 || !float.TryParse(pair[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x2)
                    || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y2))
                    throw new InvalidDataException("Line data must be x2,y2: " + assetId);
                path.AddLine(shape.X, shape.Y, x2, y2);
            }
            else throw new InvalidDataException("Unknown vector shape type '" + shape.Type + "': " + assetId);
            return path;
        }

        private static Brush CreateBrush(VectorShapeData shape, RectangleF bounds, string id)
        {
            System.Drawing.Color first = ToDrawingColor(shape.Fill, shape.Opacity, id + ".fill");
            if (string.IsNullOrWhiteSpace(shape.Fill2) || string.Equals(shape.Gradient, "none", StringComparison.OrdinalIgnoreCase))
                return new SolidBrush(first);
            System.Drawing.Color second = ToDrawingColor(shape.Fill2, shape.Opacity, id + ".fill2");
            if (string.Equals(shape.Gradient, "radial", StringComparison.OrdinalIgnoreCase))
            {
                GraphicsPath ellipse = new GraphicsPath(); ellipse.AddEllipse(bounds);
                PathGradientBrush brush = new PathGradientBrush(ellipse);
                brush.CenterColor = first; brush.SurroundColors = new[] { second }; ellipse.Dispose(); return brush;
            }
            return new LinearGradientBrush(bounds, first, second, shape.Angle, true);
        }

        private static System.Drawing.Color ToDrawingColor(string text, float opacity, string id)
        {
            XnaColor color = SceneValidation.ParseColor(text, id);
            int alpha = (int)Math.Round(color.A * Math.Max(0f, Math.Min(1f, opacity)));
            return System.Drawing.Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static PointF ReadPoint(List<string> tokens, ref int index, PointF offset, string id)
        {
            if (index + 1 >= tokens.Count || IsCommand(tokens[index]) || IsCommand(tokens[index + 1]))
                throw new InvalidDataException("Incomplete vector path point: " + id);
            float x, y;
            if (!float.TryParse(tokens[index++], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                || !float.TryParse(tokens[index++], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                throw new InvalidDataException("Invalid vector path number: " + id);
            return new PointF(x + offset.X, y + offset.Y);
        }

        private static bool IsCommand(string token)
        { return token.Length == 1 && char.IsLetter(token[0]); }

        private static void Premultiply(Texture2D texture)
        {
            XnaColor[] pixels = new XnaColor[texture.Width * texture.Height]; texture.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                float alpha = pixels[i].A / 255f;
                pixels[i] = new XnaColor((byte)(pixels[i].R * alpha), (byte)(pixels[i].G * alpha),
                    (byte)(pixels[i].B * alpha), pixels[i].A);
            }
            texture.SetData(pixels);
        }
    }
}
