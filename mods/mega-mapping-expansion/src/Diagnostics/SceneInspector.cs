using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JKRuntime.UI;
using MegaMappingExpansion.Api;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class SceneInspector : ScopedUiPage
    {
        private readonly UiFrame frame = new UiFrame(new Rectangle(12, 12, 456, 336));
        private readonly UiList list = new UiList();
        private readonly Stack<Action> back = new Stack<Action>();
        private string title = "MAPPING INSPECTOR", message = "", objectId;
        private UiNumberControl numeric;
        private RegionData region;
        private Action current;
        private SceneHost owner;
        private bool refreshLive;
        private float refreshElapsed;
        private bool Editable { get { return Array.IndexOf(Environment.GetCommandLineArgs(), "-debug") >= 0; } }
        private SceneBehaviorEngine Engine { get { return owner.behaviors; } }
        protected override void OpenPage(JKRuntime.RuntimeScope resources)
        { owner = SceneHost.Current; Root(); }
        private void Show(string heading, IEnumerable<UiListItem> items, Action refresh)
        { UiPointer.CancelCapture(this); title = heading; numeric = null; region = null; refreshLive = false; current = refresh; list.SetItems(items); }
        private void Enter(Action next)
        {
            Action previous = current ?? Root; string selected = list.SelectedId;
            back.Push(() => { previous(); if (selected != null) try { list.Select(selected); } catch (ArgumentException) { /* The selected effect may have expired. */ } });
            next();
        }
        private void Run(Action action)
        { try { message = ""; action(); } catch (Exception error) { message = error.GetBaseException().Message; ModEntry.Log("Inspector: " + error); } }
        private void Root()
        {
            if (owner == null)
            { Show("MAPPING INSPECTOR", new[] { new UiListItem("none", "Load a map with a Mapping scene first.") }, Root); return; }
            Show("MAPPING INSPECTOR", new[] {
                new UiListItem("objects", "Objects and effective properties", () => Enter(Objects)),
                new UiListItem("active", "Active effects and remaining time", () => Enter(Active)),
                new UiListItem("library", "Effect library", () => Enter(Library)),
                new UiListItem("regions", "Regions and event simulation", () => Enter(Regions)),
                new UiListItem("flags", "Scene flags", () => Enter(Flags)),
                new UiListItem("trees", "Behavior trees and waiting nodes", () => Enter(Trees)),
                new UiListItem("errors", "Behavior diagnostics", () => Enter(Errors)),
                new UiListItem("step", "Advance scene effects by one second", () => Run(() => owner.StepBehaviorPreview(1)), "Advances scene clocks while the player remains paused.", Editable),
                new UiListItem("gizmos", "Toggle region outlines in the world", () => owner.DebugRegions = !owner.DebugRegions, "Shows region rectangles and the sampled player body.", Editable),
                new UiListItem("clear", "Clear inspector overrides", () => Run(() => Engine.ClearOwner("inspector")), "Only inspector-owned effects are removed.", Editable),
                new UiListItem("export", "Export current inspector recipe", () => Run(Export), "Writes a reusable Effect definition beside the map's preview files.", Editable),
                new UiListItem("reload", "Reload scene from authoring files", () => Run(() => { if (!ModEntry.Reload(owner.RootPath)) throw new InvalidOperationException("Reload rejected; inspect the log"); owner = SceneHost.Current; back.Clear(); Root(); }), "Keeps the old scene if validation/resource loading fails.", Editable)
            }, Root);
        }
        private void Trees()
        { Show("BEHAVIOR TREES", Engine.InspectTrees().Select((value, index) => new UiListItem("tree:" + index, value, null, value)), Trees); refreshLive = true; }
        private void Objects()
        { Show("SCENE OBJECTS", Engine.InspectObjects().Select(o => new UiListItem(o.Id, o.Id + "  [" + o.Kind + "]", () => { objectId = o.Id; Enter(Properties); }, "Screen " + o.Screen)), Objects); }
        private void Properties()
        {
            var properties = Engine.Properties.Values.Values.Where(p => p.Id == objectId).ToArray();
            Show(objectId.ToUpperInvariant(), properties.Select(p => new UiListItem(p.Name, p.Name + ": " + Convert.ToString(p.Read(), CultureInfo.InvariantCulture),
                () => Enter(() => Edit(p)), "Owner: " + p.Owner, Editable)), Properties);
        }
        private void Write(SceneProperty property, object value)
        {
            string key;
            using (var hash = System.Security.Cryptography.SHA256.Create()) key = "edit:" + BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(property.Id + "/" + property.Name))).Replace("-", "");
            Engine.Apply("inspector", new EffectDefinition { Id = key,
                Repeat = "replace", Group = key, Priority = 100000,
                Changes = new[] { new SceneChange { Target = property.Id, Property = property.Name, Value = SceneProperty.Format(value) } } });
        }
        private void Edit(SceneProperty property)
        {
            if (!Editable) return;
            title = property.Id + " / " + property.Name; current = () => Edit(property);
            if (property.Target is ScreenLook && property.Name == "ambientIntensity")
            {
                Show(title, new[] { "-1", "0", "0.25", "0.5", "0.75", "1" }.Select(v => new UiListItem(v, v == "-1" ? "Inherit from Options" : v, () => Run(() => Write(property, v)))), () => Edit(property));
                return;
            }
            if (property.Member.PropertyType == typeof(float) || property.Member.PropertyType == typeof(int))
            {
                double step = property.Member.PropertyType == typeof(int) || property.Max - property.Min > 100 ? 1 : .01;
                numeric = new UiNumberControl(property.Min, property.Max, step, () => Convert.ToDouble(property.Read()), value => Run(() => Write(property, value)));
                return;
            }
            IEnumerable<string> values = property.Choices;
            if (property.Member.PropertyType == typeof(bool)) values = new[] { "true", "false" };
            if (property.Color) values = new[] { "#FFFFFF", "#FFD27A", "#91D8FF", "#EB718F", "#7ACFA0", "#8075CE", "#000000" };
            Show(title, (values ?? new string[0]).Select(value => new UiListItem(value, value, () => Run(() => Write(property, value)))), () => Edit(property));
        }
        private void Active()
        { Show("ACTIVE EFFECTS", Engine.InspectEffects().Select(e => new UiListItem(e.Id.ToString(), e.Definition + " / " + (double.IsInfinity(e.RemainingSeconds) ? "until cancelled" : e.RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"),
            () => Run(() => { Engine.CancelEffect(e.Id); Active(); }), "Owner: " + e.Owner + ". Select to cancel.", Editable)), Active); refreshLive = true; }
        private void Library()
        { Show("EFFECT LIBRARY", owner.SceneData.Effects.Select(e => new UiListItem(e.Id, e.Id, () => Run(() => Engine.Activate("inspector", e.Id)), e.Clock + ", " + e.Repeat + ", " + e.Duration + "s", Editable)), Library); }
        private void Regions()
        {
            var items = Engine.ResolvedRegions().Select(r => new UiListItem("region:" + r.Id, r.Id + " / screen " + r.Screen, () => Enter(() => Region(r)), "Enter: " + r.Enter + "; exit: " + r.Exit)).ToList();
            foreach (string name in BehaviorTreeCompiler.Events(owner.SceneData).Distinct())
            { string id = name; items.Add(new UiListItem("event:" + id, "Emit: " + id, () => Run(() => Engine.Emit(id)), "Queues the same event used by authored rules and trees.", Editable)); }
            Show("REGIONS / EVENTS", items, Regions);
        }
        private void Region(RegionData selected)
        {
            Show("REGION: " + selected.Id, new UiListItem[0], () => Region(selected)); region = selected;
        }
        private void DrawRegion(Rectangle content)
        {
            float scale = Math.Min(content.Width / 480f, (content.Height - 25) / 360f);
            var map = new Rectangle(content.X + (content.Width - (int)(480 * scale)) / 2, content.Y, (int)(480 * scale), (int)(360 * scale));
            UiTheme.Panel(map, UiTheme.PanelFill, UiTheme.Border);
            Func<float, float, float, float, Rectangle> rect = (x, y, w, h) => new Rectangle(map.X + (int)Math.Round(x * scale), map.Y + (int)Math.Round(y * scale), Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale)));
            foreach (RegionData other in Engine.ResolvedRegions().Where(r => r.Screen == region.Screen))
                UiTheme.Panel(rect(other.X, other.Y, other.Width, other.Height), Color.Transparent, other.Id == region.Id ? UiTheme.Gold : UiTheme.Border);
            SceneActor actor = Engine.Actor;
            if (actor.Present && actor.Screen == region.Screen) UiTheme.Panel(rect(actor.Bounds.X, actor.Bounds.Y, actor.Bounds.Width, actor.Bounds.Height), UiTheme.Cyan, UiTheme.Cyan);
            string state = Engine.InspectRegions().FirstOrDefault(s => s.StartsWith(region.Id + ":", StringComparison.Ordinal)) ?? region.Id;
            UiTheme.TextLine(UiTheme.FitText(state, content.Width, true), new Vector2(content.X, map.Bottom + 6), UiTheme.Text, true);
        }
        private void Flags()
        { Show("SCENE FLAGS", owner.SceneData.Flags.Select(f => new UiListItem(f.Id, f.Id + " = " + Engine.GetFlag(f.Id),
            () => Run(() => { Engine.SetFlag(f.Id, Engine.GetFlag(f.Id) == "true" ? "false" : "true"); Flags(); }), f.Scope + " scope", Editable && (Engine.GetFlag(f.Id) == "true" || Engine.GetFlag(f.Id) == "false"))), Flags); }
        private void Errors() { Show("BEHAVIOR DIAGNOSTICS", Engine.InspectErrors().Select((e, i) => new UiListItem("error:" + i, e, null, e)), Errors); }
        private void Export()
        {
            var changes = Engine.Properties.Values.Values.Where(p => p.Owner.StartsWith("inspector/", StringComparison.Ordinal)).Select(p => new SceneChange { Target = p.Id, Property = p.Name, Value = SceneProperty.Format(p.Read()) }).ToArray();
            string path = Path.Combine(owner.RootPath, "props/mega-mapping-expansion/preview/inspector-effect.xml");
            JKRuntime.Settings.AtomicXmlFile.Save(path, new EffectDefinition { Id = "inspector-preset", Changes = changes });
            message = "Exported preview/inspector-effect.xml";
        }
        private void Back()
        { UiPointer.CancelCapture(this); if (back.Count == 0) WantsClose = true; else back.Pop()(); }
        private UiPageCommand[] Commands()
        { return new[] { new UiPageCommand(UiAction.Confirm, region != null ? "Emit enter event" : numeric == null ? "Select" : "Done", () => { if (region != null) Run(() => Engine.Emit("enter:" + region.Id)); else if (numeric != null) Back(); else Run(() => list.Update(new UiInput { Action = UiAction.Confirm, Confirm = true })); }, () => region == null || Editable),
            new UiPageCommand(UiAction.Secondary, "Refresh", () => { if (current != null) Run(current); }), new UiPageCommand(UiAction.Cancel, "Back", Back) }; }
        public override void Update(UiInput input, float delta)
        {
            if (owner != null && SceneHost.Current != owner) { UiPointer.CancelCapture(this); owner = SceneHost.Current; back.Clear(); Root(); }
            refreshElapsed += delta;
            if (refreshLive && refreshElapsed >= .25f) { refreshElapsed = 0; current(); }
            foreach (UiPageCommand command in Commands()) if (command.Handle(input)) return;
            if (numeric != null) numeric.Update(input); else if (region == null && input.Action != UiAction.None) Run(() => list.Update(input));
        }
        public override void Draw()
        {
            frame.Draw();
            var commands = Commands();
            var layout = new UiPageLayout(frame.Bounds, commands, 2);
            UiTheme.TextLine(UiTheme.FitText(title, layout.Title.Width, false), layout.Title.Location.ToVector2(), UiTheme.Gold, false);
            if (region != null) DrawRegion(layout.Content);
            else if (numeric != null) { numeric.Draw(layout.Content); UiTheme.WrappedText("Use Left/Right, the wheel, or drag the slider. Back keeps the preview edit.", new Rectangle(layout.Content.X, layout.Content.Y + 50, layout.Content.Width, 60), UiTheme.Muted); }
            else list.Draw(layout.Content);
            string status = region != null ? "Gold: region. Cyan: player. Emit runs event rules; it does not move the player." : list.Description;
            if (!Editable) status = status.Length == 0 ? "Read only. Launch with -debug to edit and simulate." : status + " (read only)";
            UiTheme.WrappedText(message.Length > 0 ? message : status, layout.Status, UiTheme.Muted);
            layout.DrawCommands();
        }
    }
}
