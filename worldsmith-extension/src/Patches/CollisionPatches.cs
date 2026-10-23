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
            var layout = Layout.Read(root, Collision.SourceScreens(root));
            if(layout.Pixel||File.Exists(Path.Combine(root,"level+.png")))return false;
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
            int fallback = Collision.SourceScreens(root);
            var layout = Layout.Read(root, fallback);
            bool legacyPlus=Collision.LegacyLayout(root,layout);
            // use the selected source, timestamps don't get to override that
            // keep a strip in memory for the native preview's slope coordinates
            Bitmap value;
            if(layout.Pixel)
            {
                value=Collision.Read(Path.Combine(root,"level+.png"));
                try{Collision.ValidatePixel(value,layout);}catch{value.Dispose();throw;}
                Engine.Set(__instance,"Filter","level+.png");
            }
            else if(legacyPlus)
            {using(var image=Collision.Read(Path.Combine(root,"level+.png")))value=Collision.SourceStrip(image,layout);}
            else if (layout.Source == "atlas")
            {
                using (var b = Collision.Read(atlas))
                    value = Collision.SourceStrip(b, layout);
            }
            else
                value = Collision.Read(strip);
            Engine.Set(__instance,"Filter",layout.Pixel||legacyPlus?"level+.png":layout.Source=="atlas"?"level.png":"visual_level.png");
            try
            {
                if(!layout.Pixel)using (var b = Collision.Atlas(value, layout))
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
                    int ox=0,oy=value.Height-(i+1)*45;
                    if(layout.Pixel)layout.PixelLayout().Origin(i,out ox,out oy);
                    frames[i] = new CroppedBitmap(image, new Int32Rect(ox,oy,layout.Pixel?layout.CellWidth:60,layout.Pixel?layout.CellHeight:45));
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
            string activeRoot=(string)Engine.Get(__instance,"Folder");
            var active=File.Exists(Layout.Metadata(activeRoot))?Layout.Read(activeRoot,169):null;
            if(active==null&&File.Exists(Path.Combine(activeRoot,"level+.png")))active=Layout.Read(activeRoot,169);
            bool legacy=active!=null&&Collision.LegacyLayout(activeRoot,active);
            if(active!=null&&(active.Pixel||legacy))
            {
                int width=active.Pixel?active.PixelLayout().ImageWidth:active.Side*60,height=active.Pixel?active.PixelLayout().ImageHeight:active.Side*45;
                var pixelPicker=new Microsoft.Win32.OpenFileDialog{Filter="Collision image (*.png)|*.png",Title="Import level+.png ("+width+" x "+height+")"};
                if(pixelPicker.ShowDialog()!=true)return false;
                try
                {
                    using(var image=Collision.Read(pixelPicker.FileName))
                    {
                        if(active.Pixel)Collision.ValidatePixel(image,active);
                        else if(image.Width!=width||image.Height!=height)throw new InvalidDataException("Imported legacy level+ dimensions must match the current atlas and sizes.png.");
                        Collision.Png(image,Path.Combine(activeRoot,"level+.png"));
                    }
                    Engine.Call(__instance,"InitializeHitbox");
                }
                catch(Exception error){Panel.Error(error);}
                return false;
            }
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
            string root=(string)Engine.Get(__instance,"Folder");
            var layout=File.Exists(Layout.Metadata(root))?Layout.Read(root,169):null;
            bool pixel=layout!=null&&layout.Pixel;
            if(!pixel&&File.Exists(Path.Combine(root,"level+.png"))&&File.Exists(Path.Combine(root,"sizes.png")))
            {__result=LegacySizesPreview(original,root,source);return false;}
            var bounds = Rectangle.Intersect(new Rectangle(source.X - 1, source.Y - 1, source.Width + 2, source.Height + 2), new Rectangle(0, 0, original.Width, original.Height));
            int dx = source.X - bounds.X, dy = source.Y - bounds.Y;
            using (var sample = pixel?PixelSample(original,layout,source):original.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                if(pixel){dx=1;dy=1;source=new Rectangle(0,0,sample.Width-2,sample.Height-2);}
                var slopes = CollisionPreview.Slopes(sample, new Rectangle(dx, dy, source.Width, source.Height));
                var result = new Bitmap(480, 360);
                try
                {
                    using (var graphics = Graphics.FromImage(result))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        graphics.DrawImage(sample, new Rectangle(0, 0, 480, 360), new Rectangle(dx, dy, source.Width, source.Height), GraphicsUnit.Pixel);
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                        foreach (var point in slopes)
                        {
                            int size=480/source.Width;
                            DrawSlope(graphics,sample,point.X+dx,point.Y+dy,new Rectangle(point.X*size,point.Y*size,size,size));
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

        private static Bitmap PixelSample(Bitmap image,Layout layout,Rectangle source)
        {
            var format=layout.PixelLayout();
            int screen=format.Source=="strip"?format.Screens-1-source.Y/format.CellHeight:(source.X/format.CellWidth)*format.Rows+source.Y/format.CellHeight;
            return CollisionPreview.Sample(image,format,screen);
        }

        private static Bitmap LegacySizesPreview(Bitmap strip,string root,Rectangle source)
        {
            using(var atlas=Collision.Read(Path.Combine(root,"level+.png")))
            using(var sizes=Collision.Read(Path.Combine(root,"sizes.png")))
            {
                if(sizes.Size!=atlas.Size)throw new InvalidDataException("More Block Sizes: sizes.png must match level+.png dimensions.");
                int side=atlas.Width/60,screen=strip.Height/45-1-source.Y/45;
                int ox=(screen/side)*60,oy=(screen%side)*45;
                var result=new Bitmap(480,360);
                try
                {
                    using(var graphics=Graphics.FromImage(result))
                    using(var sample=PixelSample(atlas,new Layout{Version=2,Screens=strip.Height/45,Columns=side,Rows=side,CellWidth=60,CellHeight=45,Source="atlas"},new Rectangle(ox,oy,60,45)))
                    {
                        foreach(var cell in LegacyCollision.LegacyCells(sample,sizes,ox,oy))
                        {
                            Rectangle rect=cell.Bounds;
                            if(cell.Colour.ToArgb()==Color.Red.ToArgb()&&DrawSlope(graphics,sample,cell.X+1,cell.Y+1,rect))continue;
                            using(var brush=new SolidBrush(cell.Colour))graphics.FillRectangle(brush,rect);
                        }
                    }
                    return result;
                }
                catch{result.Dispose();throw;}
            }
        }

        private static bool DrawSlope(Graphics graphics,Bitmap sample,int x,int y,Rectangle target)
        {
            var slope=AccessTools.Method(Engine.Type("JKWorldsmith.Shared.SlopeCompatibleBlocks"),"GetSlopeType");
            object kind=slope.Invoke(null,new object[]{sample,x,y});
            if(kind.ToString()=="None")return false;
            var state=graphics.Save();
            try
            {
                // native markers draw an 8x8 cell, keep scaling local to this one
                graphics.SetClip(target);graphics.Clear(Color.Transparent);graphics.ResetClip();
                graphics.TranslateTransform(target.X,target.Y);
                graphics.ScaleTransform(target.Width/8f,target.Height/8f);
                var marker=Activator.CreateInstance(Engine.Type("JKWorldsmith.Models.SlopeIndication"),new[]{(object)0,0,kind});
                Engine.Call(marker,"DrawSlope",graphics);return true;
            }
            finally{graphics.Restore(state);}
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
