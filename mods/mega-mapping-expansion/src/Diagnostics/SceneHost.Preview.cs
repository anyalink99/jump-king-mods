using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private readonly bool previewAllowed = Array.Exists(Environment.GetCommandLineArgs(), delegate(string value) { return value == "-debug"; });
        private bool previewPaused, previewColliders;
        private bool captureRequested;
        private bool captureSceneOnly;
        private float previewStep;
        private string previewMode = "scene", previewLayer = "all", selectedId = "";
        private readonly HashSet<string> hiddenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> propIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Texture2D, Texture2D> alphaMasks = new Dictionary<Texture2D, Texture2D>();
        private DateTime nextPreviewPoll, lastPreviewWrite;

        private bool Visible(RenderCommand command)
        {
            if (hiddenIds.Contains(command.Id)) return false;
            if (previewMode != "alpha" && previewMode != "emission") return true;
            if (selectedId != "" && command.Id != selectedId) return false;
            return propIds.Contains(command.Id);
        }
        internal void CopyPreviewState(SceneHost previous)
        {
            time = previous.time; previewPaused = previous.previewPaused;
            captureRequested = previous.captureRequested;
            captureSceneOnly = previous.captureSceneOnly;
            previewMode = previous.previewMode; previewLayer = previous.previewLayer;
            selectedId = previous.selectedId; previewColliders = previous.previewColliders;
            foreach (string id in previous.hiddenIds) hiddenIds.Add(id);
            foreach (var entry in previous.screenEnteredAt) screenEnteredAt[entry.Key] = entry.Value;
            lastPreviewWrite = previous.lastPreviewWrite;
        }

        private void PollPreview()
        {
            if (!previewAllowed || DateTime.UtcNow < nextPreviewPoll) return;
            nextPreviewPoll = DateTime.UtcNow.AddMilliseconds(250);
            string file = Path.Combine(levelRoot, "props/mega-mapping-expansion/preview-control.xml");
            if (!File.Exists(file)) return;
            DateTime written = File.GetLastWriteTimeUtc(file);
            if (written == lastPreviewWrite || written <= ModEntry.LastPreviewRequest) return;
            lastPreviewWrite = written;
            ModEntry.LastPreviewRequest = written;
            try
            {
                XmlDocument document = new XmlDocument { XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
                XmlElement command = document.DocumentElement;
                if (command == null || command.Name != "Preview") throw new InvalidDataException("Expected Preview root");
                string mode = command.GetAttribute("mode");
                if (mode != "" && mode != "scene" && mode != "alpha" && mode != "emission" && mode != "light" && mode != "reflection")
                    throw new InvalidDataException("mode must be scene, alpha, emission, light or reflection");
                string layer = command.GetAttribute("layer");
                if (layer != "" && layer != "all" && layer != "background" && layer != "world" && layer != "foreground")
                    throw new InvalidDataException("layer must be all, background, world or foreground");
                int targetScreen = command.HasAttribute("screen") ? int.Parse(command.GetAttribute("screen"), CultureInfo.InvariantCulture) : 0;
                if (targetScreen < 0 || targetScreen > LevelManager.TotalScreens) throw new InvalidDataException("screen outside loaded map");
                float seek = Number(command, "seek", -1f, -1f, 86400f);
                float playerX = Number(command, "playerx", 64f, 0f, 462f);
                float playerY = Number(command, "playery", 220f, 0f, 334f);
                float step = Number(command, "step", 0f, 0f, .1f);
                bool pause = command.HasAttribute("paused") ? XmlConvert.ToBoolean(command.GetAttribute("paused")) : previewPaused;
                bool colliders = command.HasAttribute("colliders") ? XmlConvert.ToBoolean(command.GetAttribute("colliders")) : previewColliders;
                bool reload = command.HasAttribute("reload") && XmlConvert.ToBoolean(command.GetAttribute("reload"));
                bool restart = command.HasAttribute("restart") && XmlConvert.ToBoolean(command.GetAttribute("restart"));
                captureRequested = command.HasAttribute("capture") && XmlConvert.ToBoolean(command.GetAttribute("capture"));
                captureSceneOnly = command.HasAttribute("capturescene") && XmlConvert.ToBoolean(command.GetAttribute("capturescene"));
                captureRequested |= captureSceneOnly;
                if (mode != "") previewMode = mode;
                if (layer != "") previewLayer = layer;
                previewPaused = pause; previewColliders = colliders; previewStep = step;
                if (command.HasAttribute("select")) selectedId = command.GetAttribute("select");
                if (command.HasAttribute("hidden"))
                { hiddenIds.Clear(); foreach (string id in command.GetAttribute("hidden").Split(';')) if (id != "") hiddenIds.Add(id); }
                if (targetScreen > 0) NativeSceneAdapter.PreviewScreen(targetScreen, playerX, playerY);
                if (command.HasAttribute("topologycheck") && XmlConvert.ToBoolean(command.GetAttribute("topologycheck"))) TopologyProbe.Run(levelRoot);
                if (command.HasAttribute("resumegame") && XmlConvert.ToBoolean(command.GetAttribute("resumegame")))
                    NativeSceneAdapter.ResumePreview();
                if (seek >= 0f)
                {
                    time = seek; reactions.Clear(); waterSurfaces.Clear();
                    screenEnteredAt.Clear(); screenEnteredAt[Camera.CurrentScreenIndex1] = 0f;
                }
                bool success = !reload || ModEntry.Reload(levelRoot);
                (Current ?? this).WritePreviewStatus(success ? "ok" : "reload rejected; previous scene retained");
                if (restart) NativeSceneAdapter.RestartPreview();
            }
            catch (Exception error) { ModEntry.Log("Preview command rejected: " + error.Message); WritePreviewStatus(error.Message); }
        }

        private static float Number(XmlElement node, string name, float fallback, float min, float max)
        {
            float value = node.HasAttribute(name) ? float.Parse(node.GetAttribute(name), CultureInfo.InvariantCulture) : fallback;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max) throw new InvalidDataException("Preview @" + name + " outside " + min + ".." + max);
            return value;
        }

        internal bool WantsFinalCapture { get { return previewAllowed && captureRequested && !captureSceneOnly; } }
        internal void CapturePreview(RenderTarget2D frame)
        {
            if (!previewAllowed || !captureRequested) return;
            captureRequested = false;
            string directory = Path.Combine(levelRoot, "props/mega-mapping-expansion/preview");
            try
            {
                Directory.CreateDirectory(directory);
                string output = Path.Combine(directory, "frame-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".png");
                // The installed MonoGame PNG writer can produce truncated IDAT
                // data. A debug-only readback uses the system encoder instead.
                var pixels = new Color[frame.Width * frame.Height];
                frame.GetData(pixels);
                using (var bitmap = new System.Drawing.Bitmap(frame.Width, frame.Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var locked = bitmap.LockBits(new System.Drawing.Rectangle(0,0,frame.Width,frame.Height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        var bytes = new byte[locked.Stride * frame.Height];
                        for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)
                        {
                            Color c=pixels[y*frame.Width+x]; int offset=y*locked.Stride+x*4;
                            bytes[offset]=c.B;bytes[offset+1]=c.G;bytes[offset+2]=c.R;bytes[offset+3]=255;
                        }
                        System.Runtime.InteropServices.Marshal.Copy(bytes,0,locked.Scan0,bytes.Length);
                    }
                    finally { bitmap.UnlockBits(locked); }
                    bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);
                }
                ModEntry.Log("Native preview captured: " + output);
            }
            catch (Exception error) { ModEntry.Log("Native preview capture failed: " + error.Message); }
        }

        private void WritePreviewStatus(string result)
        {
            string file = Path.Combine(levelRoot, "props/mega-mapping-expansion/preview-status.xml");
            try
            {
                using (XmlWriter writer = XmlWriter.Create(file + ".tmp", new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartElement("PreviewStatus"); writer.WriteAttributeString("result", result);
                    writer.WriteAttributeString("screen", Camera.CurrentScreenIndex1.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("loadedScreens", LevelManager.TotalScreens.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("time", time.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("mode", previewMode); writer.WriteAttributeString("paused", XmlConvert.ToString(previewPaused));
                    writer.WriteStartElement("GroundShadow");
                    writer.WriteAttributeString("receivers",scene.ShadowSurfaces.Length.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("scanlines",shadowScanlines.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("foot",shadowFoot.ToString());
                    writer.WriteAttributeString("spriteOrigin",shadowSpriteOrigin.ToString());
                    writer.WriteAttributeString("source",shadowSource.ToString());
                    writer.WriteEndElement();
                    foreach (var entry in renderPlans)
                        foreach (string layer in new[] { "background", "world", "foreground" })
                            foreach (RenderCommand item in entry.Value.Layer(layer))
                            {
                                writer.WriteStartElement("Object"); writer.WriteAttributeString("id", item.Id);
                                writer.WriteAttributeString("screen", entry.Key.ToString(CultureInfo.InvariantCulture));
                                writer.WriteAttributeString("layer", layer); writer.WriteAttributeString("z", item.Z.ToString(CultureInfo.InvariantCulture));
                                writer.WriteAttributeString("visible", XmlConvert.ToString(Visible(item))); writer.WriteEndElement();
                            }
                    writer.WriteEndElement();
                }
                if (File.Exists(file)) File.Replace(file + ".tmp", file, null); else File.Move(file + ".tmp", file);
            }
            catch (Exception error) { ModEntry.Log("Preview status write failed: " + error.Message); }
        }

        private Texture2D AlphaMask(Texture2D original)
        {
            Texture2D mask;
            if (alphaMasks.TryGetValue(original, out mask)) return mask;
            Color[] pixels = new Color[original.Width * original.Height]; original.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++) { byte alpha = pixels[i].A; pixels[i] = new Color(alpha, alpha, alpha, (byte)255); }
            mask = new Texture2D(Game1.instance.GraphicsDevice, original.Width, original.Height); mask.SetData(pixels);
            alphaMasks.Add(original, mask); return mask;
        }

        private void DrawPreviewOverlay()
        {
            if (!previewAllowed) return;
            if (DebugRegions && behaviors != null)
            {
                foreach (RegionData region in behaviors.ResolvedRegions())
                    if (region.Screen == Camera.CurrentScreenIndex1) Outline(new Rectangle((int)region.X, (int)region.Y, (int)region.Width, (int)region.Height), JKRuntime.UI.UiTheme.Gold);
                if (behaviors.Actor.Present) Outline(behaviors.Actor.Bounds, Color.Cyan);
            }
            if (previewColliders)
            {
                foreach (SceneAnchor anchor in scene.Anchors)
                {
                    if (anchor.Screen != Camera.CurrentScreenIndex1) continue;
                    Outline(new Rectangle(anchor.X, anchor.Y, anchor.Width, anchor.Height), anchor.Kind == "water" ? Color.Cyan : Color.Yellow);
                }
                var player = GameLoopPlayer();
                if (player != null) Outline(Camera.TransformRect(player.m_body.GetHitbox()), Color.Magenta);
            }
        }
        private void Outline(Rectangle rectangle, Color color)
        {
            Vector2 a = new Vector2(rectangle.Left, rectangle.Top), b = new Vector2(rectangle.Right, rectangle.Top);
            Vector2 c = new Vector2(rectangle.Right, rectangle.Bottom), d = new Vector2(rectangle.Left, rectangle.Bottom);
            DrawLine(a, b, color, 1f); DrawLine(b, c, color, 1f); DrawLine(c, d, color, 1f); DrawLine(d, a, color, 1f);
        }
    }
}
