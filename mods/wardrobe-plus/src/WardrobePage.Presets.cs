using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WardrobePlus
{
    internal sealed partial class WardrobePage
    {
        private void Presets()
        {
            Show("SAVED OUTFITS", PresetRows(), "Load a complete look and its equipped items instantly.");
            var state = page; page.RefreshRows = () => PresetRows(state.Query);
        }
        private void SavePreset()
        {
            if (!Controller.Flush()) return;
            var preset = Controller.Snapshot();
            Name("OUTFIT NAME", "New outfit", value => {
                preset.Id = Guid.NewGuid().ToString("N"); preset.Name = value;
                SaveNamedPreset(data => data.Presets.Add(preset), "Outfit saved");
            });
        }
        private List<Row> PresetRows(string query = "")
        {
            var rows = new List<Row> { new Row("Save current look", SavePreset), SearchRow() };
            rows.AddRange(Controller.Data.Presets.Where(x => Matches(x.Name,query)).OrderByDescending(x => x.Favorite).ThenBy(x => x.Name)
                .Select(p => new Row((p.Favorite ? "* " : "") + p.Name, () => Preset(p.Id)) { SourceId = "preset:" + p.Id }));
            return rows;
        }
        private void Preset(string id)
        {
            Func<Outfit> preset = () => Controller.Data.Presets.Find(x => x.Id == id);
            if (preset() == null) { Back(); return; }
            Show(preset().Name.ToUpperInvariant(), new List<Row> {
                new Row("Load", () => { Controller.LoadPreset(preset()); history.Clear(); Root(false); }),
                new Row("Replace with current look", () => Show("REPLACE OUTFIT?", new List<Row> {
                    new Row("Keep saved outfit", Back), new Row("Replace", () => {
                        Controller.Request(data => { var next = Controller.Snapshot(); next.Id = id; next.Name = preset().Name; next.Favorite = preset().Favorite;
                            data.Presets.RemoveAll(x => x.Id == id); data.Presets.Add(next); }, false, "Preset updated"); Back();
                    }) })),
                new Row("Rename", () => Name("RENAME OUTFIT", preset().Name, value => SaveNamedPreset(data => data.Presets.Find(x => x.Id == id).Name = value, "Preset renamed"))),
                new Row("Delete", () => Show("DELETE OUTFIT?", new List<Row> { new Row("Keep outfit", Back), new Row("Delete", () => {
                    Controller.Request(data => { data.Presets.RemoveAll(x => x.Id == id); data.Maps.RemoveAll(x => x.OutfitId == id); }, false, "Preset deleted");
                    Back(); Back();
                }) })),
                new Row("More options", () => PresetOptions(id))
            }, preset().Equipment == null ? "Legacy outfit: changes appearance while keeping equipped items." : "Includes appearance, item positions and equipment.");
            page.RefreshTitle = () => preset() == null ? "DELETED OUTFIT" : preset().Name.ToUpperInvariant();
        }
        private void PresetOptions(string id)
        {
            Func<Outfit> preset = () => Controller.Data.Presets.Find(x => x.Id == id);
            Show("OUTFIT OPTIONS", new List<Row> {
                new Row("Duplicate", () => Name("DUPLICATE OUTFIT", preset().Name + " copy", value => { var next = preset().Copy(); next.Id = Guid.NewGuid().ToString("N"); next.Name = value;
                    SaveNamedPreset(data => data.Presets.Add(next), "Preset duplicated"); })),
                new Row("Favorite outfit", () => Controller.Request(data => { var p = data.Presets.Find(x => x.Id == id); p.Favorite = !p.Favorite; }, false, "Favorite updated")) { Text = () => preset().Favorite ? "Remove favorite" : "Favorite outfit" },
                new Row("Use automatically on this map", () => Controller.Request(data => { string map = NativeAppearance.MapId(); data.Maps.RemoveAll(x => x.MapId == map); data.Maps.Add(new MapOutfit { MapId = map, OutfitId = id }); }, false, "Map preference saved")),
                new Row("Export recipe", () => { string path = Controller.Store.Export(preset()); Controller.Status = "Recipe exported: " + Path.GetFileName(path); })
            }, "Optional tools for this saved outfit.");
        }
        private static void SaveNamedPreset(Action<WardrobeData> change, string message)
        {
            Controller.Request(change,false,message); Controller.Pump();
            if (Controller.LastError.Length > 0) throw new InvalidOperationException(Controller.LastError);
        }
    }
}
