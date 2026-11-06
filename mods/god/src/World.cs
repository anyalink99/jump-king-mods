using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JKRuntime;
using JKRuntime.Geometry;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace God
{
    internal sealed class World : IDisposable
    {
        private readonly LevelScreen[] screens;
        private readonly Dictionary<int, ScreenEdit> edits = new Dictionary<int, ScreenEdit>();
        private readonly SavedWorld saved;
        private readonly string path;
        private Dictionary<int, int[]> resetBefore;
        private List<SavedScreen> resetRecords;
        private bool resetUndone;
        internal long Generation;
        internal bool Changed { get { return edits.Values.Any(e => e.Changed); } }
        internal World()
        {
            ScreenEdit.ValidateContracts(); screens = NativeWorldGeometry.ReadScreens();
            string root = Game1.instance.contentManager.root;
            if (!Path.IsPathRooted(root)) root = Path.Combine(Path.GetDirectoryName(typeof(Game1).Assembly.Location), root);
            path = Path.Combine(Storage.Root, Storage.WorldKey(Path.GetFullPath(root)) + ".xml");
            saved = Storage.Read<SavedWorld>(path) ?? new SavedWorld();
            if (saved.Version != 1 || saved.Screens == null || saved.Screens.Count > screens.Length || saved.Screens.Select(s => s.Index).Distinct().Count() != saved.Screens.Count)
                throw new InvalidDataException("Unsupported or duplicate God screen data.");
        }
        internal void RestoreSaved()
        {
            foreach (var record in saved.Screens)
            {
                try
                {
                    var edit = Get(record.Index);
                    if (record.Baseline != edit.Signature) { Entry.Status = "Map art or geometry changed. Saved edits were left untouched."; continue; }
                    edit.Apply(Storage.Decode(record.Cells, edit.Materials.Count), false); Generation++;
                }
                catch (Exception error) { Entry.Status = "Saved screen " + (record.Index + 1) + ": " + error.Message; Console.WriteLine("[God] " + Entry.Status); }
            }
        }
        internal ScreenEdit Get(int index)
        {
            if (index < 0 || index >= screens.Length) throw new InvalidOperationException("This screen is outside the loaded map.");
            ScreenEdit value;
            if (!edits.TryGetValue(index, out value))
            {
                value = new ScreenEdit(screens[index]); edits.Add(index, value);
            }
            return value;
        }
        internal void Save(ScreenEdit edit)
        {
            var records = new List<SavedScreen>(saved.Screens.Where(s => s.Index != edit.Index));
            if (edit.Changed) records.Add(new SavedScreen { Index = edit.Index, Baseline = edit.Signature, Cells = Storage.Encode(edit.Cells) });
            var next = new SavedWorld { Screens = records.OrderBy(s => s.Index).ToList() };
            Storage.Write(path, next); saved.Screens = next.Screens;
            resetBefore = null; resetRecords = null;
        }
        internal void ResetAll()
        {
            var before = edits.ToDictionary(p => p.Key, p => (int[])p.Value.Cells.Clone());
            var records = new List<SavedScreen>(saved.Screens);
            ApplyBatch(edits.ToDictionary(p => p.Key, p => p.Value.Baseline), new List<SavedScreen>());
            resetBefore = before; resetRecords = records; resetUndone = false;
        }
        internal void RefreshArtwork()
        {
            foreach (var edit in edits.Values) edit.Apply(edit.Cells, false);
        }
        internal bool HistoryReset(bool redo)
        {
            if (resetBefore == null || redo != resetUndone) return false;
            ApplyBatch(redo ? resetBefore.ToDictionary(p => p.Key, p => Get(p.Key).Baseline) : resetBefore,
                redo ? new List<SavedScreen>() : new List<SavedScreen>(resetRecords));
            resetUndone = !redo; return true;
        }
        private void ApplyBatch(Dictionary<int, int[]> next, List<SavedScreen> records)
        {
            var changed = new List<KeyValuePair<ScreenEdit, int[]>>();
            try
            {
                foreach (var pair in next)
                {
                    var edit = Get(pair.Key); if (edit.Cells.SequenceEqual(pair.Value)) continue;
                    var old = edit.Cells; edit.Apply(pair.Value, true); changed.Add(new KeyValuePair<ScreenEdit, int[]>(edit, old));
                }
                Storage.Write(path, new SavedWorld { Screens = records }); saved.Screens = records;
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                for (int i = changed.Count - 1; i >= 0; i--)
                    try { changed[i].Key.Apply(changed[i].Value, false); } catch (Exception recovery) { errors.Add(recovery); }
                if (errors.Count > 1) throw new AggregateException("Map reset and rollback failed", errors);
                throw;
            }
        }
        public void Dispose()
        {
            foreach (var edit in edits.Values) edit.Dispose(); edits.Clear();
        }
    }

    internal sealed class ScreenEdit : IDisposable
    {
        internal static readonly FieldInfo Blocks = typeof(LevelScreen).GetField("m_hitboxes", OwnedPatches.Members);
        internal static readonly FieldInfo Graphics = typeof(LevelScreen).GetField("m_graphics", OwnedPatches.Members);
        internal static readonly FieldInfo Screens = typeof(LevelManager).GetField("m_screens", OwnedPatches.Members);
        internal readonly LevelScreen Screen;
        internal readonly int Index;
        internal readonly int[] Baseline = new int[Terrain.Count];
        internal readonly List<Material> Materials = new List<Material>();
        internal int CellSize = 8;
        private readonly HashSet<IBlock> editable = new HashSet<IBlock>();
        internal int Kind(int id) { return id == 0 ? 0 : Materials[id - 1].Kind; }
        internal readonly EditHistory History = new EditHistory();
        internal int[] Cells, VisualCells;
        internal readonly int[] VisualBaseline;
        private string signature;
        private readonly string[] signatureMaterials;
        internal string Signature { get { PrepareArtwork(); return signature; } }
        private List<Region> regions;
        private int[] regionCells, regionVisualCells;
        internal List<Region> Regions
        {
            get
            {
                if (!ReferenceEquals(regionCells, Cells) || !ReferenceEquals(regionVisualCells, VisualCells))
                {
                    regions = SlopePlatforms.Regions(this, Cells, VisualCells);
                    regionCells = Cells; regionVisualCells = VisualCells;
                }
                return regions;
            }
        }
        private readonly IBlock[] originalBlocks;
        private IBlock[] installedBlocks;
        private readonly LevelScreen.Graphics originalGraphics;
        private Color[] midground;
        private Texture2D editedMidground, editedForeground;
        private LayerSurface midSurface, frontSurface;
        private Texture2D borrowedForeground;
        private bool appliedTransferForeground;
        private int appliedForegroundHeight;
        private int[] rebuildScratch;
        private readonly List<OriginalBlock> originalEntries = new List<OriginalBlock>();
        private bool[] unchanged;
        private Dictionary<long, IBlock> generated = new Dictionary<long, IBlock>(), generatedScratch = new Dictionary<long, IBlock>();
        private sealed class OriginalBlock { internal IBlock Block; internal Rectangle Bounds; internal int[] Materials; }
        private bool[] reserved;
        internal bool Untextured { get { return originalGraphics.background == null && originalGraphics.foreground == null; } }
        internal bool Changed { get { return !ReferenceEquals(Cells, Baseline) && !Cells.SequenceEqual(Baseline); } }
        internal static void ValidateContracts()
        {
            if (Blocks == null || Blocks.FieldType != typeof(IBlock[]) || Graphics == null || Graphics.FieldType != typeof(LevelScreen.Graphics)
                || Screens == null || Screens.FieldType != typeof(LevelScreen[])) throw new NotSupportedException("Installed screen contracts changed.");
            NativeWorldGeometry.ValidateContract();
        }
        internal ScreenEdit(LevelScreen screen)
        {
            Screen = screen; Index = screen.GetIndex0(); originalBlocks = installedBlocks = (IBlock[])Blocks.GetValue(screen);
            originalGraphics = (LevelScreen.Graphics)Graphics.GetValue(screen);
            var templates = new Dictionary<int, BlockTemplate>();
            var kinds = new Dictionary<string, int>();
            kinds[new BlockTemplate(new BoxBlock(new Rectangle(0, 0, 8, 8))).Key] = 1;
            foreach (var block in originalBlocks)
            {
                Rectangle box = block.GetRect(); box.Y += Index * Terrain.Height;
                if (box.Width <= 0 || box.Height <= 0) continue;
                CellSize = Gcd(CellSize, Gcd(Gcd(box.Width, box.Height), Gcd(Math.Abs(box.X), Math.Abs(box.Y))));
                var template = new BlockTemplate(block);
                var clipped = Rectangle.Intersect(Terrain.Screen, box);
                bool overlap = false;
                for (int y = clipped.Top; y < clipped.Bottom; y++) for (int x = clipped.Left; x < clipped.Right; x++)
                    if (Baseline[y * Terrain.Width + x] != 0) overlap = true;
                if (template.Error != null || overlap)
                {
                    if (reserved == null) reserved = new bool[Terrain.Count];
                    for (int y = clipped.Top; y < clipped.Bottom; y++) for (int x = clipped.Left; x < clipped.Right; x++) reserved[y * Terrain.Width + x] = true;
                    continue;
                }
                int kind;
                if (!kinds.TryGetValue(template.Key, out kind)) { kind = kinds.Count + 1; kinds.Add(template.Key, kind); }
                templates[kind] = template;
                Terrain.Paint(Baseline, box, kind); editable.Add(block);
            }
            var sourceKinds = (int[])Baseline.Clone();
            var sourceRegions = Terrain.Regions(sourceKinds, id => id);
            foreach (var region in sourceRegions)
            {
                int id = Materials.Count + 1, kind = sourceKinds[region.Pixels[0]];
                foreach (int i in region.Pixels) Baseline[i] = id;
                Materials.Add(new Material(id, region, Baseline) { Template = templates[kind], Kind = kind });
            }
            // a bare collision map still has a drawable solid material
            var blank = new Region { Bounds = new Rectangle(0, 0, CellSize, CellSize) };
            Materials.Add(new Material(Materials.Count + 1, blank, Baseline) { Untextured = true, Template = new BlockTemplate(new BoxBlock(blank.Bounds)) });
            Cells = Baseline;
            VisualCells = VisualBaseline = Visualize(Baseline);
            if (ReferenceEquals(VisualBaseline, Baseline))
            {
                regions = sourceRegions; regionCells = Cells; regionVisualCells = VisualCells;
            }
            foreach (var material in Materials) material.RebuildSamples(VisualBaseline);
            signatureMaterials = Materials.Select(m => m.Template.Key ?? "").ToArray();
            SlopePlatforms.Prepare(this);
            unchanged = new bool[Materials.Count + 1];
            foreach (var block in editable)
            {
                var r = block.GetRect(); r.Y += Index * Terrain.Height; r = Rectangle.Intersect(r, Terrain.Screen);
                var ids = new HashSet<int>();
                for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) ids.Add(Baseline[y * Terrain.Width + x]);
                originalEntries.Add(new OriginalBlock { Block = block, Bounds = r, Materials = ids.ToArray() });
            }
        }
        private void PrepareArtwork()
        {
            if (signature != null) return;
            // browsing screens doesn't need GPU readback or a saved-art fingerprint
            var mid = Read(originalGraphics.background); var foreground = Read(originalGraphics.foreground);
            using (var bytes = new MemoryStream()) using (var writer = new BinaryWriter(bytes))
            {
                writer.Write("typed-walls-decor-v2");
                var buffer = new byte[Terrain.Count * 4];
                Buffer.BlockCopy(Baseline, 0, buffer, 0, buffer.Length); writer.Write(buffer);
                foreach (var key in signatureMaterials) writer.Write(key);
                WriteColors(writer, mid, buffer); WriteColors(writer, foreground, buffer);
                foreach (var block in originalBlocks) { writer.Write(block.GetType().AssemblyQualifiedName); var r = block.GetRect(); writer.Write(r.X); writer.Write(r.Y); writer.Write(r.Width); writer.Write(r.Height); }
                writer.Flush(); signature = Storage.Hash(bytes.ToArray());
            }
            midground = mid;
        }
        private static void WriteColors(BinaryWriter writer, Color[] colors, byte[] buffer)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                uint value = colors[i].PackedValue; int offset = i * 4;
                buffer[offset] = (byte)value; buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16); buffer[offset + 3] = (byte)(value >> 24);
            }
            writer.Write(buffer);
        }
        internal int[] Visualize(int[] cells, List<Rectangle> rectangles = null)
        {
            if (!Materials.Any(m => m.Template.Source.GetType() == typeof(SlopeBlock))) return cells;
            var visible = (int[])cells.Clone();
            foreach (var rectangle in rectangles ?? Rectangles(cells))
            {
                int id = cells[rectangle.Y * Terrain.Width + rectangle.X];
                var template = Materials[id - 1].Template;
                if (template == null || template.Source.GetType() != typeof(SlopeBlock)) continue;
                var slope = (SlopeBlock)template.Copy(rectangle);
                var polygon = slope.GetSlopeType() == SlopeType.None ? null : new VisualShape(VisualGeometry.ReadSlope(slope));
                // collision records stay triangular blocks; this mask is only their visible coverage
                for (int y = rectangle.Top; y < rectangle.Bottom; y++) for (int x = rectangle.Left; x < rectangle.Right; x++)
                    if (polygon == null || !polygon.Contains(new Vector2(x + .5f, y + .5f))) visible[y * Terrain.Width + x] = 0;
            }
            return visible;
        }
        internal List<Rectangle> Rectangles(int[] cells)
        {
            var result = new List<Rectangle>();
            foreach (var r in Terrain.Rectangles(cells))
            {
                if (!Materials[cells[r.Y * Terrain.Width + r.X] - 1].TiledSlope) { result.Add(r); continue; }
                // neighbouring ramp cells mustn't merge into one stretched collider
                for (int y = r.Top; y < r.Bottom; y += CellSize) for (int x = r.Left; x < r.Right; x += CellSize)
                    result.Add(new Rectangle(x, y, Math.Min(CellSize, r.Right - x), Math.Min(CellSize, r.Bottom - y)));
            }
            return result;
        }
        private static int Gcd(int a, int b) { while (b != 0) { int n = a % b; a = b; b = n; } return a; }
        private static Color[] Read(Texture2D texture)
        {
            var pixels = new Color[Terrain.Count]; if (texture == null) return pixels;
            if (texture.Width != Terrain.Width || texture.Height != Terrain.Height) throw new NotSupportedException("God currently edits native 480 x 360 screen layers.");
            texture.GetData(pixels); return pixels;
        }
        private void CheckOwnership()
        {
            var loaded = (LevelScreen[])Screens.GetValue(null);
            if (loaded == null || Index >= loaded.Length || !ReferenceEquals(loaded[Index], Screen)) throw new InvalidOperationException("The loaded map changed.");
            if (!ReferenceEquals(Blocks.GetValue(Screen), installedBlocks)) throw new InvalidOperationException("Another mod changed this screen. Reload the map before editing it.");
            var art = (LevelScreen.Graphics)Graphics.GetValue(Screen);
            if (!ReferenceEquals(art.background, editedMidground ?? originalGraphics.background) || (editedForeground != null && !ReferenceEquals(art.foreground, editedForeground)))
                throw new InvalidOperationException("Another mod changed this screen's art. Reload the map before editing it.");
        }
        internal void Apply(int[] next, bool protectPlayer)
        {
            CheckOwnership();
            if (next == null || next.Length != Terrain.Count) throw new ArgumentException("Invalid terrain mask.");
            bool geometryChanged = false, baseline = true;
            for (int i = 0; i < next.Length; i++)
            {
                if (next[i] < 0 || next[i] > Materials.Count) throw new ArgumentException("Invalid terrain mask.");
                geometryChanged |= next[i] != Cells[i]; baseline &= next[i] == Baseline[i];
            }
            bool transfer = Storage.Preferences != null && Storage.Preferences.TransferForeground;
            var player = JumpKing.GameManager.GameLoop.m_player;
            int height = player == null ? 26 : player.m_body.GetHitbox().Height;
            bool artChanged = transfer != appliedTransferForeground || (transfer && height != appliedForegroundHeight);
            if (!geometryChanged && !artChanged) return;
            if (!baseline) PrepareArtwork();
            var rectangles = geometryChanged ? Rectangles(next) : null;
            if (rectangles != null && rectangles.Count > 8192) throw new InvalidOperationException("This screen has too many separate rectangles.");
            var visible = geometryChanged ? Visualize(next, rectangles) : VisualCells;
            Rectangle body = player == null ? Rectangle.Empty : player.m_body.GetHitbox(); body.Y += Index * Terrain.Height;
            for (int i = 0; i < next.Length; i++)
                if (next[i] != 0 && next[i] != Cells[i])
                {
                    if (reserved != null && reserved[i]) throw new InvalidOperationException("This block has overlapping or external state that God cannot relocate.");
                    if (protectPlayer && visible[i] != 0 && Materials[next[i] - 1].Template.Blocking && body.Contains(i % Terrain.Width, i / Terrain.Width)) throw new InvalidOperationException("The new platform intersects the king. Draw clear of him.");
                }
            IBlock[] installed = installedBlocks;
            if (geometryChanged)
            {
                generatedScratch.Clear();
                if (baseline) installed = originalBlocks;
                else
                {
                    var blocks = originalBlocks.Where(b => !editable.Contains(b)).ToList();
                    for (int i = 0; i < unchanged.Length; i++) unchanged[i] = true;
                    for (int i = 0; i < next.Length; i++) if (next[i] != Baseline[i]) { unchanged[next[i]] = false; unchanged[Baseline[i]] = false; }
                    if (rebuildScratch == null) rebuildScratch = new int[Terrain.Count];
                    Array.Copy(next, rebuildScratch, next.Length);
                    foreach (var entry in originalEntries)
                    {
                        bool keep = true; foreach (int id in entry.Materials) if (!unchanged[id]) { keep = false; break; }
                        if (keep) { blocks.Add(entry.Block); Terrain.Paint(rebuildScratch, entry.Bounds, 0); }
                    }
                    foreach (var rectangle in Rectangles(rebuildScratch))
                    {
                        int id = next[rectangle.Y * Terrain.Width + rectangle.X];
                        long key = ((((long)id * 512 + rectangle.X) * 512 + rectangle.Y) * 512 + rectangle.Width) * 512 + rectangle.Height;
                        IBlock block;
                        if (!generated.TryGetValue(key, out block))
                        {
                            var r = rectangle; r.Y -= Index * Terrain.Height; block = Materials[id - 1].Template.Copy(r);
                        }
                        blocks.Add(block); generatedScratch.Add(key, block);
                    }
                    installed = blocks.ToArray();
                }
            }
            var graphics = (LevelScreen.Graphics)Graphics.GetValue(Screen);
            var dirty = LayerSurface.Dirty(VisualCells, visible);
            bool stagedMid = false, stagedFront = false;
            Texture2D mid = null, front = null;
            if (!baseline)
            {
                if (originalGraphics.background != null)
                {
                    if (midSurface == null) midSurface = new LayerSurface(midground, VisualBaseline, Materials);
                    mid = midSurface.Stage(Game1.instance.GraphicsDevice, visible, dirty, 0); stagedMid = true;
                }
                if (transfer)
                {
                    if (editedForeground == null)
                    {
                        if (frontSurface != null) frontSurface.Dispose(); frontSurface = null;
                        borrowedForeground = graphics.foreground;
                    }
                    if (borrowedForeground != null)
                    {
                        if (frontSurface == null) frontSurface = new LayerSurface(Read(borrowedForeground), VisualBaseline, Materials);
                        var frontDirty = artChanged ? Terrain.Screen : dirty;
                        if (frontDirty.Height > 0) { frontDirty.Y -= height; frontDirty.Height += height; frontDirty = Rectangle.Intersect(frontDirty, Terrain.Screen); }
                        front = frontSurface.Stage(Game1.instance.GraphicsDevice, visible, frontDirty, height); stagedFront = true;
                    }
                }
            }
            graphics.background = mid ?? originalGraphics.background;
            if (front != null) graphics.foreground = front;
            else if (editedForeground != null) graphics.foreground = borrowedForeground;
            // stage both layers before publishing; a failed upload leaves the visible buffers intact
            Blocks.SetValue(Screen, installed);
            try { Graphics.SetValue(Screen, graphics); }
            catch { Blocks.SetValue(Screen, installedBlocks); throw; }
            if (stagedMid) midSurface.Commit(); else if (midSurface != null) midSurface.Reset();
            if (stagedFront) frontSurface.Commit(); else if (frontSurface != null) frontSurface.Reset();
            installedBlocks = installed;
            if (geometryChanged)
            {
                Cells = baseline ? Baseline : (int[])next.Clone(); VisualCells = visible;
                var previous = generated; generated = generatedScratch; generatedScratch = previous;
                RuntimeApi.Geometry.Invalidate(); MapTopology.Invalidate();
            }
            editedMidground = mid; editedForeground = front;
            appliedTransferForeground = transfer; appliedForegroundHeight = height;
        }
        public void Dispose()
        {
            if (ReferenceEquals(Blocks.GetValue(Screen), installedBlocks)) Blocks.SetValue(Screen, originalBlocks);
            var graphics = (LevelScreen.Graphics)Graphics.GetValue(Screen);
            if (ReferenceEquals(graphics.background, editedMidground)) graphics.background = originalGraphics.background;
            if (editedForeground != null && ReferenceEquals(graphics.foreground, editedForeground)) graphics.foreground = borrowedForeground;
            Graphics.SetValue(Screen, graphics);
            if (midSurface != null) midSurface.Dispose(); if (frontSurface != null) frontSurface.Dispose();
            editedMidground = editedForeground = null;
        }
    }
}
