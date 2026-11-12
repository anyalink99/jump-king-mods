using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace God
{
    internal enum Tool { Select, Draw, Erase, Sample }
    internal sealed class Editor : IDisposable
    {
        private readonly SpriteBatch batch;
        private readonly Texture2D pixel;
        private readonly List<Button> buttons = new List<Button>();
        private ScreenEdit edit;
        private KeyboardState keyboard, oldKeyboard;
        private MouseState mouse, oldMouse;
        private Point start, end;
        private Region selected;
        private Tool tool = Tool.Draw;
        private bool dragging, resizing, panelHidden;
        private int donor;
        private Rectangle panel, selectionBounds;
        private float uiScale;
        private int windowWidth;
        private Rectangle game;
        private Vector2 offset;
        private List<Region> regions;
        private int[] dragBefore;
        private Point lastApplied;
        private bool applied, copying;
        private JumpKing.Level.SlopeType shape = JumpKing.Level.SlopeType.None;
        private bool suppressPrompt;
        internal bool PromptPointerAvailable { get { return !suppressPrompt && !dragging && !KingDrag.Active && WheelAvailable; } }
        internal bool WheelAvailable { get { return !panel.Contains(new Point((int)(mouse.X / uiScale), (int)(mouse.Y / uiScale))); } }
        private readonly Color gold = new Color(229, 184, 68), cyan = new Color(88, 218, 235);
        private enum ControlKind { Mode, Setting, Action }
        private sealed class Button
        {
            internal Rectangle Bounds;
            internal Action Click;
            internal string Label, Value;
            internal bool Selected;
            internal bool ShapeIcon;
            internal ControlKind Kind;
        }
        internal Editor()
        {
            batch = new SpriteBatch(Game1.instance.GraphicsDevice);
            pixel = new Texture2D(Game1.instance.GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        }
        internal void Begin(int index)
        {
            edit = Entry.World.Get(index); donor = 0; selected = null; Cancel(); Refresh(); Layout();
        }
        internal void Seed(KeyboardState keys, MouseState pointer) { oldKeyboard = keys; oldMouse = pointer; }
        internal void Cancel()
        {
            KingDrag.End();
            if (dragBefore != null && !edit.Cells.SequenceEqual(dragBefore))
            {
                try { edit.Apply(dragBefore, true); Entry.Touch(); Refresh(); }
                catch (Exception error) { FinishDrag(); Entry.Status = "Can't restore terrain over the king; kept the last safe shape. " + error.Message; }
            }
            dragging = resizing = applied = false; dragBefore = null;
        }
        private void FinishDrag()
        {
            if (dragBefore != null && !edit.Cells.SequenceEqual(dragBefore))
            { edit.History.Push(dragBefore); Entry.Save(edit); Refresh(); }
            dragging = resizing = applied = false; dragBefore = null;
        }
        private bool Press(Keys key) { return keyboard.IsKeyDown(key) && oldKeyboard.IsKeyUp(key); }
        private int Grid { get { return edit.CellSize; } }
        private Point Pointer()
        {
            return new Point((int)Math.Floor((mouse.X - game.X) * 480.0 / game.Width - offset.X),
                (int)Math.Floor((mouse.Y - game.Y) * 360.0 / game.Height - offset.Y));
        }
        private void Layout()
        {
            var pp = Game1.instance.GraphicsDevice.PresentationParameters;
            uiScale = Math.Max(.25f, Math.Min(pp.BackBufferHeight / 720f, pp.BackBufferWidth / 1050f)); windowWidth = (int)(pp.BackBufferWidth / uiScale);
            game = Game1.instance.GetGameRect();
            // the top edge belongs to the next screen in topology queries
            var center = new Vector2(Terrain.Width * .5f, Terrain.Height * .5f);
            offset = JKRuntime.FrameComposition.ProjectWorld(center - new Vector2(0, edit.Index * Terrain.Height)) - center;
            if (JumpGame.screenShakeManager != null) game.Location += JumpGame.screenShakeManager.GetOffset();
            buttons.Clear(); panel = Rectangle.Empty;
            if (panelHidden) return;
            int width = Math.Min(windowWidth - 28, 976);
            panel = new Rectangle((windowWidth - width) / 2, 12, width, string.IsNullOrEmpty(Entry.Status) ? 52 : 78);
            int x = panel.X + 66, y = panel.Y + 12;
            foreach (Tool value in Enum.GetValues(typeof(Tool)))
            {
                var t = value;
                Add(value + " [" + ((int)value + 1) + "]", x, y, 74, () => { Cancel(); tool = t; }, ControlKind.Mode, tool == t, ""); x += 74;
            }
            x = panel.X + 378;
            Add("Shape", x, y, 94, CycleShape, ControlKind.Setting, false, ""); buttons[buttons.Count - 1].ShapeIcon = true;
            x = panel.X + 486;
            Add("Sample", x, y, 110, () => donor = 0, ControlKind.Setting, false, "Sample a platform with [4]. Click here to use automatic selection.", donor == 0 ? "Auto" : "#" + donor);
            x = panel.X + 608;
            Add("Undo", x, y, 78, () => History(false), ControlKind.Action, false, "");
            Add("Redo", x + 84, y, 78, () => History(true), ControlKind.Action, false, "");
            Add("Reset", x + 168, y, 78, ResetAll, ControlKind.Action, false, "");
            Add("Close", x + 252, y, 78, Entry.Close, ControlKind.Action, false, "");
        }
        private void Add(string label, int x, int y, int width, Action action, ControlKind kind, bool selected, string hint, string value = null)
        { buttons.Add(new Button { Bounds = new Rectangle(x, y, width, 28), Label = label, Click = action, Selected = selected, Kind = kind, Value = value }); }
        private void CycleShape()
        {
            Cancel(); donor = 0;
            var values = new[] { JumpKing.Level.SlopeType.None, JumpKing.Level.SlopeType.TopLeft, JumpKing.Level.SlopeType.TopRight,
                JumpKing.Level.SlopeType.BottomLeft, JumpKing.Level.SlopeType.BottomRight };
            shape = values[(Array.IndexOf(values, shape) + 1) % values.Length];
        }
        private void Refresh() { regions = edit.Regions; }
        private Region Pick(Point p)
        {
            if (!Terrain.Screen.Contains(p) || edit.VisualCells[p.Y * Terrain.Width + p.X] == 0) return null;
            int i = p.Y * Terrain.Width + p.X; return regions.FirstOrDefault(r => r.Bounds.Contains(p) && r.Pixels.Contains(i));
        }
        internal void Update(KeyboardState keys, MouseState pointer)
        {
            keyboard = keys; mouse = pointer;
            try
            {
                var previousGame = game; Layout();
                if (Camera.CurrentScreen != edit.Index)
                {
                    if (KingDrag.Active) { edit = Entry.World.Get(Camera.CurrentScreen); donor = 0; selected = null; Refresh(); Layout(); }
                    else { FinishDrag(); Begin(Camera.CurrentScreen); Seed(keys, pointer); return; }
                }
                if (previousGame != game && !KingDrag.Active) { FinishDrag(); return; }
                if (Press(Keys.Escape)) { if (dragging || KingDrag.Active) Cancel(); else Entry.Close(); return; }
                if (Press(Keys.Tab)) { Cancel(); panelHidden = !panelHidden; Layout(); }
                bool control = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
                if (control && Press(Keys.Z)) { Cancel(); History(keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift)); }
                else if (control && Press(Keys.Y)) { Cancel(); History(true); }
                else if (Press(Keys.D1)) { Cancel(); tool = Tool.Select; }
                else if (Press(Keys.D2)) { Cancel(); tool = Tool.Draw; }
                else if (Press(Keys.D3)) { Cancel(); tool = Tool.Erase; }
                else if (Press(Keys.D4)) { Cancel(); tool = Tool.Sample; }
                if (Press(Keys.Delete) && selected != null)
                {
                    Cancel();
                    var next = (int[])edit.Cells.Clone(); foreach (int i in selected.Pixels) next[i] = 0; Commit(next); selected = null;
                }
                var p = Pointer(); var ui = new Point((int)(pointer.X / uiScale), (int)(pointer.Y / uiScale));
                bool press = pointer.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released;
                bool release = pointer.LeftButton == ButtonState.Released && oldMouse.LeftButton == ButtonState.Pressed;
                if (pointer.RightButton == ButtonState.Released) suppressPrompt = false;
                if (pointer.RightButton == ButtonState.Pressed && oldMouse.RightButton == ButtonState.Released)
                { suppressPrompt = dragging || KingDrag.Active; Cancel(); }
                if (press && panel.Contains(ui)) { Cancel(); var button = buttons.FirstOrDefault(b => b.Bounds.Contains(ui)); if (button != null) button.Click(); return; }
                if (KingDrag.Active)
                {
                    if (release || pointer.LeftButton != ButtonState.Pressed) KingDrag.End();
                    return;
                }
                if (press && game.Contains(pointer.X, pointer.Y) && KingDrag.Bounds(edit.Index).Contains(p))
                {
                    Cancel(); selected = null; KingDrag.Begin(pointer.Position, p, edit.Index); return;
                }
                if (press && game.Contains(pointer.X, pointer.Y) && Terrain.Screen.Contains(p))
                {
                    start = end = Terrain.Snap(p, Grid);
                    if (tool == Tool.Sample)
                    {
                        donor = edit.VisualCells[p.Y * Terrain.Width + p.X];
                        var sampled = Pick(p);
                        if (sampled != null && sampled.Slope != JumpKing.Level.SlopeType.None)
                        {
                            var full = edit.Materials.FirstOrDefault(m => m.Composite && m.Bounds == sampled.Bounds && SlopePlatforms.Type(m) == sampled.Slope);
                            donor = full != null ? full.Id : SlopePlatforms.Choose(edit, sampled.Bounds, 0, sampled.Slope, edit.Cells);
                        }
                        shape = donor == 0 ? JumpKing.Level.SlopeType.None : SlopePlatforms.Type(edit.Materials[donor - 1]);
                        Entry.Status = "";
                        tool = Tool.Draw;
                    }
                    else if (tool == Tool.Select)
                    {
                        // the resize handle can sit just outside the selected solid
                        bool handle = selected != null && new Rectangle(selected.Bounds.Right - Grid, selected.Bounds.Bottom - Grid, Grid * 2, Grid * 2).Contains(p);
                        if (!handle) selected = Pick(p);
                        resizing = handle; copying = control && !handle; dragging = selected != null;
                    }
                    else dragging = true;
                    if (dragging) { dragBefore = (int[])edit.Cells.Clone(); applied = false; }
                }
                if (dragging)
                {
                    end = Terrain.Snap(p, Grid);
                    if ((pointer.LeftButton == ButtonState.Pressed || release) && (!applied || end != lastApplied))
                    {
                        var rectangle = DragBounds(); int[] next;
                        if (tool == Tool.Select)
                        {
                            var target = SlopePlatforms.ResizeBounds(selected, new Point(end.X - start.X, end.Y - start.Y), Grid);
                            next = SlopePlatforms.Move(edit, dragBefore, selected, new Point(end.X - start.X, end.Y - start.Y), resizing, target, copying);
                        }
                        else
                        {
                            next = (int[])dragBefore.Clone();
                            int material = tool == Tool.Erase ? 0 : SlopePlatforms.Choose(edit, rectangle, donor, shape, dragBefore);
                            if (tool == Tool.Erase) SlopePlatforms.Erase(edit, next, rectangle);
                            else SlopePlatforms.Paint(edit, next, rectangle, material);
                        }
                        if (!next.SequenceEqual(edit.Cells))
                        {
                            try { edit.Apply(next, true); Entry.Touch(); lastApplied = end; applied = true; }
                            catch (Exception error) { Entry.Status = error.Message; }
                        }
                    }
                    if (release) { FinishDrag(); selected = tool == Tool.Select ? Pick(end) : null; }
                }
            }
            catch (Exception error) { FinishDrag(); Entry.Status = error.Message; }
            finally { oldKeyboard = keys; oldMouse = pointer; }
        }
        private Rectangle DragBounds()
        { return tool == Tool.Draw && shape != JumpKing.Level.SlopeType.None ? SlopePlatforms.DrawBounds(start, end, Grid) : Terrain.DragRect(start, end, Grid); }
        private void Commit(int[] next)
        {
            if (next.SequenceEqual(edit.Cells)) return;
            var before = edit.Cells; edit.Apply(next, true); edit.History.Push(before); selected = null; Refresh(); Entry.Changed(edit);
        }
        private void ResetAll()
        {
            Cancel(); Entry.World.ResetAll(); selected = null; Refresh(); Entry.Touch(); Entry.Status = "";
        }
        private void History(bool redo)
        {
            if (Entry.World.HistoryReset(redo)) { selected = null; Refresh(); Entry.Touch(); Entry.Status = ""; return; }
            // put the history cursor back if the king now blocks this restore
            int[] before = edit.Cells; var next = redo ? edit.History.Redo(before) : edit.History.Undo(before);
            if (ReferenceEquals(next, before)) return;
            try { edit.Apply(next, true); }
            catch { if (redo) edit.History.Undo(next); else edit.History.Redo(next); throw; }
            if (!next.SequenceEqual(before)) { selected = null; Refresh(); Entry.Changed(edit); }
        }
        private Rectangle ToWindow(Rectangle r)
        {
            float sx = game.Width / 480f / uiScale, sy = game.Height / 360f / uiScale;
            return new Rectangle((int)(game.X / uiScale + (r.X + offset.X) * sx), (int)(game.Y / uiScale + (r.Y + offset.Y) * sy), Math.Max(1, (int)Math.Round(r.Width * sx)), Math.Max(1, (int)Math.Round(r.Height * sy)));
        }
        internal void Draw()
        {
            Layout(); var device = Game1.instance.GraphicsDevice;
            var viewport = device.Viewport; var scissor = device.ScissorRectangle; var blend = device.BlendState; var sampler = device.SamplerStates[0]; var raster = device.RasterizerState; var depth = device.DepthStencilState;
            try
            {
                device.Viewport = new Viewport(0, 0, device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateScale(uiScale));
                try
                {
                    if (edit.Untextured)
                        foreach (var region in regions) Outline(region.Bounds, edit.Cells[region.Pixels[0]], cyan * .5f, 1, region.Slope);
                    var king = KingDrag.Bounds(edit.Index);
                    bool kingHover = king.Contains(Pointer());
                    if (KingDrag.Active || kingHover) Border(ToWindow(king), KingDrag.Active ? gold : cyan, 2);
                    var hover = dragging || KingDrag.Active || kingHover ? null : Pick(Pointer());
                    if (hover != null) Outline(hover.Bounds, edit.Cells[hover.Pixels[0]], cyan * .8f, 1, hover.Slope);
                    if (donor > 0 && donor <= edit.Materials.Count) Border(ToWindow(edit.Materials[donor - 1].Bounds), gold, 2);
                    if (selected != null)
                    {
                        selectionBounds = selected.Bounds;
                        if (dragging && tool == Tool.Select)
                        {
                            if (resizing) selectionBounds = SlopePlatforms.ResizeBounds(selected, new Point(end.X - start.X, end.Y - start.Y), Grid);
                            else
                            {
                                if (selected.Slope != JumpKing.Level.SlopeType.None) selectionBounds.Width = selectionBounds.Height = Math.Max(selectionBounds.Width, selectionBounds.Height);
                                selectionBounds.Location += new Point(end.X - start.X, end.Y - start.Y);
                            }
                        }
                        var clippedSelection = Rectangle.Intersect(Terrain.Screen, selectionBounds);
                        if (clippedSelection.Width > 0 && clippedSelection.Height > 0)
                        {
                            var r = ToWindow(clippedSelection); Outline(selectionBounds, (dragBefore ?? edit.Cells)[selected.Pixels[0]], gold, 2, selected.Slope); Fill(new Rectangle(r.Right - 5, r.Bottom - 5, 10, 10), gold);
                        }
                    }
                    if (dragging && tool != Tool.Select)
                    {
                        var rectangle = DragBounds(); var r = ToWindow(rectangle); Color color = tool == Tool.Erase ? new Color(230, 80, 80) : cyan;
                        if (tool == Tool.Draw && shape != JumpKing.Level.SlopeType.None)
                            Outline(rectangle, SlopePlatforms.Choose(edit, rectangle, donor, shape, dragBefore), color, 2, shape);
                        else { Fill(r, color * .18f); Border(r, color, 2); }
                        Text((rectangle.Width / Grid) + " x " + (rectangle.Height / Grid), r.X + 4, r.Y + 4, 15, Color.White);
                        if (tool == Tool.Draw)
                        {
                            int preview = SlopePlatforms.Choose(edit, rectangle, donor, shape, dragBefore);
                            if (preview > 0 && !edit.Materials[preview - 1].Untextured) Border(ToWindow(edit.Materials[preview - 1].Bounds), gold, 2);
                        }
                    }
                    if (!panelHidden) DrawPanel();
                }
                finally { batch.End(); }
            }
            finally { device.Viewport = viewport; device.ScissorRectangle = scissor; device.BlendState = blend; device.SamplerStates[0] = sampler; device.RasterizerState = raster; device.DepthStencilState = depth; }
        }
        private void DrawPanel()
        {
            Frame(panel);
            Text("GOD", panel.X + 18, panel.Y + 16, 18, gold, true);
            Fill(new Rectangle(panel.X + 368, panel.Y + 12, 1, 28), new Color(114, 114, 114));
            Fill(new Rectangle(panel.X + 602, panel.Y + 12, 1, 28), new Color(114, 114, 114));
            var pointer = new Point((int)(mouse.X / uiScale), (int)(mouse.Y / uiScale));
            var hovered = buttons.FirstOrDefault(b => b.Bounds.Contains(pointer));
            foreach (var b in buttons)
            {
                bool hot = ReferenceEquals(b, hovered);
                Color color = b.Selected || hot ? gold : Color.White;
                if (b.Kind == ControlKind.Action) Frame(b.Bounds);
                if (b.Kind == ControlKind.Setting)
                {
                    Text(b.Label, b.Bounds.X, b.Bounds.Y + 6, 16, Color.White);
                    if (b.ShapeIcon) DrawShapeIcon(new Rectangle(b.Bounds.Right - 18, b.Bounds.Y + 7, 14, 14), color);
                    else Text(b.Value, b.Bounds.Right - Measure(b.Value, 16), b.Bounds.Y + 6, 16, color);
                }
                else Text(b.Label, b.Bounds.X + (b.Bounds.Width - Measure(b.Label, 15)) / 2, b.Bounds.Y + 6, 15, color);
                if (b.Kind == ControlKind.Mode && b.Selected) Fill(new Rectangle(b.Bounds.X + 8, b.Bounds.Bottom - 2, b.Bounds.Width - 16, 2), gold);
            }
            if (!string.IsNullOrEmpty(Entry.Status)) Text(Fit(Entry.Status, panel.Width - 44, 14), panel.X + 22, panel.Y + 56, 14, Color.White);
        }
        private void Frame(Rectangle bounds)
        {
            var sprites = Game1.instance.contentManager.gui.FrameSprites;
            int cell = Math.Max(2, Math.Min(sprites[0, 0].source.Width * 2, Math.Min(bounds.Width, bounds.Height) / 2));
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
            {
                var sprite = sprites[x, y];
                int left = x == 0 ? bounds.X : x == 1 ? bounds.X + cell : bounds.Right - cell;
                int top = y == 0 ? bounds.Y : y == 1 ? bounds.Y + cell : bounds.Bottom - cell;
                var destination = new Rectangle(left, top, x == 1 ? bounds.Width - cell * 2 : cell, y == 1 ? bounds.Height - cell * 2 : cell);
                if (destination.Width > 0 && destination.Height > 0) batch.Draw(sprite.texture, destination, sprite.source, Color.White);
            }
        }
        private void DrawShapeIcon(Rectangle bounds, Color color)
        {
            if (shape == JumpKing.Level.SlopeType.None) { Fill(bounds, color); return; }
            for (int y = 0; y < bounds.Height; y++)
            {
                int left = 0, width = bounds.Width;
                switch (shape)
                {
                    case JumpKing.Level.SlopeType.TopLeft: left = bounds.Width - y - 1; width = y + 1; break;
                    case JumpKing.Level.SlopeType.TopRight: width = y + 1; break;
                    case JumpKing.Level.SlopeType.BottomLeft: left = y; width -= y; break;
                    case JumpKing.Level.SlopeType.BottomRight: width -= y; break;
                }
                Fill(new Rectangle(bounds.X + left, bounds.Y + y, width, 1), color);
            }
        }
        private void Outline(Rectangle bounds, int id, Color color, int thickness, JumpKing.Level.SlopeType grouped = JumpKing.Level.SlopeType.None)
        {
            var template = edit.Materials[id - 1].Template;
            if (grouped == JumpKing.Level.SlopeType.None && template.Source.GetType() != typeof(JumpKing.Level.SlopeBlock)) { Border(ToWindow(Rectangle.Intersect(Terrain.Screen, bounds)), color, thickness); return; }
            var shape = grouped == JumpKing.Level.SlopeType.None ? JKRuntime.Geometry.VisualGeometry.ReadSlope((JumpKing.Level.SlopeBlock)template.Copy(bounds))
                : JKRuntime.Geometry.VisualGeometry.DeclaredSlope(bounds, grouped);
            shape = SlopePlatforms.Clip(shape);
            for (int i = 0; i < shape.Length; i++)
            {
                var a = ToWindow(new Rectangle((int)shape[i].X, (int)shape[i].Y, 1, 1)).Location.ToVector2();
                var b = ToWindow(new Rectangle((int)shape[(i + 1) % shape.Length].X, (int)shape[(i + 1) % shape.Length].Y, 1, 1)).Location.ToVector2();
                var d = b - a;
                batch.Draw(pixel, a, null, color, (float)Math.Atan2(d.Y, d.X), Vector2.Zero, new Vector2(d.Length(), thickness), SpriteEffects.None, 0);
            }
        }
        private void Fill(Rectangle r, Color color) { if (r.Width > 0 && r.Height > 0) batch.Draw(pixel, r, color); }
        private void Border(Rectangle r, Color color, int thickness)
        { Fill(new Rectangle(r.X, r.Y, r.Width, thickness), color); Fill(new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color); Fill(new Rectangle(r.X, r.Y, thickness, r.Height), color); Fill(new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color); }
        private string Fit(string text, int width, float size)
        {
            var font = Game1.instance.contentManager.font.MenuFontSmall;
            while (text.Length > 0 && font.MeasureString(text).X * size / font.LineSpacing > width) text = text.Substring(0, text.Length - 1);
            return text;
        }
        private int Measure(string text, float size)
        { var font = Game1.instance.contentManager.font.MenuFontSmall; return (int)Math.Ceiling(font.MeasureString(text).X * size / font.LineSpacing); }
        private void Text(string text, int x, int y, float size, Color color, bool title = false)
        { var font = title ? Game1.instance.contentManager.font.MenuFont : Game1.instance.contentManager.font.MenuFontSmall; batch.DrawString(font, text, new Vector2(x, y), color, 0, Vector2.Zero, size / font.LineSpacing, SpriteEffects.None, 0); }
        public void Dispose() { batch.Dispose(); pixel.Dispose(); }
    }
}
