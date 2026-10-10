using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using HarmonyLib;

namespace WorldsmithExtension
{
    internal static class CollisionPatches
    {
        static bool Generate(object __instance)
        {
            string root = (string)Engine.Get(__instance, "Folder");
            var strip = (Bitmap)Engine.Get(__instance, "GdiImage");
            var layout = Layout.Read(root, strip.Height / 45);
            if (!File.Exists(Path.Combine(root, "worldsmith-extension.xml")) && !File.Exists(Path.Combine(root, "visual_level.png")))
                layout.Source = "atlas";
            // atlas mode means this is the author's source
            // don't let preview generation overwrite it, even the padding cells may have art
            if (layout.Source == "atlas") return false;
            using (var atlas = Collision.Atlas(strip, layout))
                Collision.Png(atlas, Path.Combine(root, "level.png"));
            return false;
        }

        static bool InitializeCollision(object __instance)
        {
            string root = (string)Engine.Get(__instance, "Folder");
            string strip = Path.Combine(root, "visual_level.png"), atlas = Path.Combine(root, "level.png");
            if (!File.Exists(strip) && !File.Exists(atlas))
                return true;
            int fallback;
            using (var b = Collision.Read(File.Exists(strip) ? strip : atlas))
                fallback = File.Exists(strip) ? b.Height / 45 : (b.Width / 60) * (b.Height / 45);
            var layout = Layout.Read(root, fallback);
            if (!File.Exists(Path.Combine(root, "worldsmith-extension.xml")) && !File.Exists(strip))
                layout.Source = "atlas";
            // use the selected source, timestamps don't get to override that
            // keep a strip in memory for the native preview's slope coordinates
            Bitmap value;
            if (layout.Source == "atlas")
            {
                using (var b = Collision.Read(atlas))
                    value = Collision.SourceStrip(b, layout);
            }
            else
                value = Collision.Read(strip);
            try
            {
                using (var b = Collision.Atlas(value, layout))
                { /* validate dimensions before changing the view */
                }

                var image = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    value.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                }

                var frames = new CroppedBitmap[layout.Screens];
                for (int i = 0; i < frames.Length; i++)
                {
                    frames[i] = new CroppedBitmap(image, new Int32Rect(0, value.Height - (i + 1) * 45, 60, 45));
                    frames[i].Freeze();
                }

                var old = Engine.Get(__instance, "GdiImage") as Bitmap;
                Engine.Set(__instance, "CurrentHitbox", image);
                Engine.Set(__instance, "GdiImage", value);
                value = null;
                Engine.Set(__instance, "HitboxFrames", frames);
                if (old != null)
                    old.Dispose();
                foreach (string name in new[]{"CurrentFrame", "PreviewCurrentFrame", "PrevFrame", "PreviewPrevFrame", "NextFrame", "PreviewNextFrame"})
                    AccessTools.Method(__instance.GetType(), "OnPropertyChanged", new[]{typeof(string)}).Invoke(__instance, new object[]{name});
                var loaded = AccessTools.Field(__instance.GetType(), "OnLoad").GetValue(__instance) as EventHandler;
                if (loaded != null)
                    loaded(__instance, EventArgs.Empty);
            }
            finally
            {
                if (value != null)
                    value.Dispose();
            }

            return false;
        }

        static bool Import(object __instance)
        {
            var picker = new Microsoft.Win32.OpenFileDialog{Filter = "Collision strip (*.png)|*.png", Title = "Import collision strip (60 x N*45)"};
            if (picker.ShowDialog() != true)
                return false;
            try
            {
                using (var image = Collision.Read(picker.FileName))
                {
                    Layout.AtlasSide(image.Height / 45);
                    if (image.Width != 60 || image.Height % 45 != 0)
                        throw new InvalidDataException("Expected 60 x N*45 pixels.");
                    string root = (string)Engine.Get(__instance, "Folder");
                    var layout = Layout.Read(root, image.Height / 45);
                    if (File.Exists(Layout.Metadata(root)) && layout.Screens > image.Height / 45)
                        throw new InvalidDataException("Imported strip has fewer screens than map.xml. Set the authored screen count first.");
                    Collision.Png(image, Path.Combine(root, "visual_level.png"));
                    Engine.Call(__instance, "InitializeHitbox");
                }
            }
            catch (Exception e)
            {
                Panel.Error(e);
            }

            return false;
        }

        static bool CanUp(object __instance, ref bool __result)
        {
            __result = (int)Engine.Get(__instance, "FrameNumber") < ((Array)Engine.Get(__instance, "HitboxFrames")).Length;
            return false;
        }

        static bool Preview(object __instance, Rectangle source, ref Bitmap __result)
        {
            var original = (Bitmap)Engine.Get(__instance, "GdiImage");
            var bounds = Rectangle.Intersect(new Rectangle(source.X - 1, source.Y - 1, source.Width + 2, source.Height + 2), new Rectangle(0, 0, original.Width, original.Height));
            int dx = source.X - bounds.X, dy = source.Y - bounds.Y;
            using (var sample = original.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var result = new Bitmap(480, 360);
                try
                {
                    using (var graphics = Graphics.FromImage(result))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        graphics.DrawImage(sample, new Rectangle(0, 0, 480, 360), new Rectangle(dx, dy, source.Width, source.Height), GraphicsUnit.Pixel);
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                        var slope = AccessTools.Method(Engine.Type("JKWorldsmith.Shared.SlopeCompatibleBlocks"), "GetSlopeType");
                        var indication = Engine.Type("JKWorldsmith.Models.SlopeIndication");
                        for (int y = 0; y < source.Height; y++)
                            for (int x = 0; x < source.Width; x++)
                                if (sample.GetPixel(x + dx, y + dy).ToArgb() == System.Drawing.Color.Red.ToArgb())
                                {
                                    object kind = slope.Invoke(null, new object[]{sample, x + dx, y + dy});
                                    if (kind.ToString() == "None")
                                        continue;
                                    graphics.SetClip(new Rectangle(x * 8, y * 8, 8, 8));
                                    graphics.Clear(System.Drawing.Color.Transparent);
                                    graphics.ResetClip();
                                    object marker = Activator.CreateInstance(indication, new[]{(object)x, y, kind});
                                    Engine.Call(marker, "DrawSlope", graphics);
                                }
                    }

                    __result = result;
                    return false;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }

        static bool ScreenValid(object __instance, object value, ref bool __result)
        {
            string root = Engine.ProjectRoot;
            if (String.IsNullOrEmpty(root) || !File.Exists(Layout.Metadata(root)))
                return true;
            int number;
            var layout = Layout.Read(root, 169);
            __result = value != null && Int32.TryParse(value.ToString(), out number) && number >= (int)Engine.Get(__instance, "Min") && number <= layout.Screens && !((IEnumerable)Engine.Get(Engine.Type("JKWorldsmith.ViewModels.Level.LocationViewModel"), "Taken")).Cast<int>().Contains(number);
            return false;
        }

    }
}
