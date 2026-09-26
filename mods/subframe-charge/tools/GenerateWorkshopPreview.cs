using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

internal static class GenerateWorkshopPreview
{
    private const int OutputSize = 256;

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Usage: GenerateWorkshopPreview "
                + "<generated-base.png> <title-font.ttf> <output.png>");
            return 1;
        }

        string basePath = Path.GetFullPath(args[0]);
        string fontPath = Path.GetFullPath(args[1]);
        string outputPath = Path.GetFullPath(args[2]);
        if (!File.Exists(basePath) || !File.Exists(fontPath))
        {
            Console.Error.WriteLine("A preview input is missing");
            return 2;
        }

        using (Bitmap generated = new Bitmap(basePath))
        using (Bitmap output = new Bitmap(
            OutputSize,
            OutputSize,
            PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(output))
        using (PrivateFontCollection fonts = new PrivateFontCollection())
        {
            graphics.Clear(Color.FromArgb(5, 9, 27));
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                generated,
                new Rectangle(0, 0, OutputSize, OutputSize),
                0,
                0,
                generated.Width,
                generated.Height,
                GraphicsUnit.Pixel);

            NormalizeBackground(output);
            fonts.AddFontFile(fontPath);
            DrawTitle(graphics, fonts.Families[0]);

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            output.Save(outputPath, ImageFormat.Png);
        }

        Console.WriteLine("[OK] Workshop preview: " + outputPath);
        return 0;
    }

    private static void NormalizeBackground(Bitmap bitmap)
    {
        Color background = Color.FromArgb(2, 5, 13);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                bool generatedNavy = color.R <= 16
                    && color.G <= 32
                    && color.B >= 20
                    && color.B <= 64
                    && color.B >= color.G + 12;
                if (generatedNavy)
                {
                    bitmap.SetPixel(x, y, background);
                }
            }
        }
    }

    private static void DrawTitle(Graphics graphics, FontFamily family)
    {
        graphics.TextRenderingHint =
            TextRenderingHint.SingleBitPerPixelGridFit;
        using (Font font = new Font(
            family,
            25f,
            FontStyle.Regular,
            GraphicsUnit.Pixel))
        using (SolidBrush shadow = new SolidBrush(
            Color.FromArgb(61, 42, 24)))
        using (SolidBrush foreground = new SolidBrush(
            Color.FromArgb(255, 232, 177)))
        using (StringFormat format = new StringFormat())
        {
            const string Title = "SUBFRAME CHARGE";
            format.Alignment = StringAlignment.Near;
            format.LineAlignment = StringAlignment.Near;
            format.FormatFlags = StringFormatFlags.NoWrap
                | StringFormatFlags.MeasureTrailingSpaces;
            format.SetMeasurableCharacterRanges(
                new[] { new CharacterRange(0, Title.Length) });
            Region[] regions = graphics.MeasureCharacterRanges(
                Title,
                font,
                new RectangleF(0, 0, 512, 64),
                format);
            RectangleF measured = regions[0].GetBounds(graphics);
            regions[0].Dispose();
            float x = (OutputSize - measured.Width) / 2f
                - measured.X + 1f;
            float y = 11f - measured.Y;
            graphics.DrawString(
                Title,
                font,
                shadow,
                new PointF(x + 2f, y + 2f),
                format);
            graphics.DrawString(
                Title,
                font,
                foreground,
                new PointF(x, y),
                format);
        }
    }
}
