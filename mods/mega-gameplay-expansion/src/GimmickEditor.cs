using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Level;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal sealed partial class GimmickPage
    {
        private GimmickRule draft;
        private bool draftSetting;
        private UiNumberControl number;
        private IBlock[][] preview;
        private int previewScreen;
        private Color[][] previewColours;
        private void Details(string id)
        {
            GimmickEntry item;
            if (!Gimmicks.Entries.TryGetValue(id, out item)) { Show("UNAVAILABLE", new[] { new UiListItem("gone", "Provider is no longer available", null, id, false) }); return; }
            if (item.Kind == "Block") GimmickRecipes.Prepare(item);
            GimmickClassification.Inspect(item);
            if (item.Kind == "Colour") { Information(item); return; }
            if (item.Slot != null && item.Slot.ReadOnlyReason != null) {
                var diagnostics = new List<UiListItem> {
                    new UiListItem("readonly", "Read-only collision state", null, item.Slot.ReadOnlyReason, false),
                    Row("materials", "Browse this mod's materials", () => Navigate(() => ProviderMaterials(item.Owner)), "Choose a block and use Auto placement to apply its real contact effect."),
                    Row("provider", "Provider and classification details", () => Navigate(() => Information(item)))
                };
                if (Gimmicks.Rule(id).Enabled) diagnostics.Add(Row("disable", "Remove saved override", () => {
                    var rule = Gimmicks.Copy(Gimmicks.Rule(id)); rule.Enabled = false; Gimmicks.Configure(rule); Details(id);
                }));
                if (Gimmicks.Pins.Contains(id)) diagnostics.Add(Row("unpin", "Unpin collision state", () => { Gimmicks.Pin(id); Details(id); }));
                Show("DIAGNOSTIC: " + item.Label, diagnostics); return;
            }
            if (draft == null || draft.Id != id) {
                draft = Gimmicks.Copy(Gimmicks.Rule(id)); draftSetting = Gimmicks.Enabled(item);
                if (item.Slot != null && draft.Value == null) draft.Value = item.Slot.Choices[0];
            }
            var rows = new List<UiListItem> {
                Row("apply", item.Kind == "Setting" ? "Save to owning mod" : "Apply draft", () => ApplyDraft(item), item.Kind == "Setting" ? item.Classification : "Only Apply commits the configuration. Back discards unapplied edits when you return to the library."),
                Row("enabled", (item.Kind == "Setting" ? "Mod setting: " : "Enabled: ") + (item.Write != null ? draftSetting : draft.Enabled), () => { if (item.Write != null) draftSetting = !draftSetting; else draft.Enabled = !draft.Enabled; Details(id); }, item.Kind == "Setting" ? item.Classification : null),
                Row("pin", (Gimmicks.Pins.Contains(id) ? "Unpin " : "Pin ") + (item.Kind == "Setting" ? "mod setting" : "saved configuration"), () => { Gimmicks.Pin(id); Details(id); }, item.Kind == "Setting" ? "This shortcut changes the owning mod's persistent setting. Restore leaves it unchanged." : "Pins use the last applied configuration, including its mode and target screens.")
            };
            if (item.Kind == "Block") {
                rows.Add(Row("mode", "Placement: " + Mode(draft.Application), () => Navigate(() => Modes(id)), item.Classification));
                rows.Add(Row("shape", "Convert slopes: " + draft.ConvertSlopes, () => Change(id, r => r.ConvertSlopes = !r.ConvertSlopes), "Advanced replacement only. Surface mode preserves ordinary rectangular terrain and leaves slopes intact."));
                rows.Add(Row("preview", "Preview target geometry", () => Navigate(() => Preview(id)), "Builds a temporary geometry plan without installing it. Apply also validates player intersection and handlers."));
            }
            if (item.Kind == "Wind") {
                rows.Add(new UiListItem("status", Gimmicks.Session == null ? "Load a map to apply wind" : Gimmicks.Session.WindStatus(id), null, "Native gate status at menu inspection. Third-party patches may add their own conditions.", false));
                rows.Add(Row("pattern", "Direction: " + draft.Value, () => Change(id, r => r.Value = GimmickWind.Patterns[(Array.IndexOf(GimmickWind.Patterns, r.Value) + 1) % GimmickWind.Patterns.Length]), "Alternating uses the native wind cycle without resetting its clock."));
                rows.Add(Row("strength", "Strength: " + draft.WindStrength.ToString("0.###") + "x", () => Navigate(() => WindStrength(id)), "1x is normal native wind; 0x disables it."));
                rows.Add(Row("timing", "Activation: " + (draft.WindImmediate ? "Immediate" : "Native entry"), () => Change(id, r => r.WindImmediate = !r.WindImmediate), "Immediate also activates midair. Native Snow and NoWind suppression still apply."));
            }
            if (item.Slot != null) {
                rows.Add(Row("value", "Force value: " + draft.Value, () => Change(id, r => r.Value = item.Slot.Choices[(Array.IndexOf(item.Slot.Choices, r.Value) + 1) % item.Slot.Choices.Length]), item.Detail));
            }
            if (item.Kind == "Block" || item.Kind == "Wind" || item.Slot != null)
                rows.Add(Row("scope", "Target: " + Range(draft), () => Navigate(() => Scope(id)), "Target in the CURRENT map; independent of source map search filters."));
            if (item.Kind == "Block" || item.Kind == "Wind")
                rows.Add(Row("duplicate", "Create another configuration...", () => EditText("CONFIGURATION NAME", item.Label + " copy", value => {
                    if (string.IsNullOrWhiteSpace(value)) return;
                    var copy = Gimmicks.Copy(draft); copy.SourceId = copy.SourceId ?? item.Id; copy.Id = "config:" + Guid.NewGuid().ToString("N"); copy.Name = value; copy.Enabled = false;
                    Gimmicks.AddConfiguration(copy); Gimmicks.Configure(copy); draft = copy;
                    current = () => Details(copy.Id); current();
                }), "Creates an independent, disabled configuration. Assign its target scope, then Apply. Overlapping replacements or wind rules are refused."));
            rows.Add(Row("discard", "Discard draft changes", () => { draft = null; Details(id); }));
            if (id.StartsWith("config:", StringComparison.Ordinal)) rows.Add(Row("delete", "Delete this saved copy", () => {
                Gimmicks.DeleteConfiguration(id); draft = null; RefreshCatalogue(); GoHome();
            }, "Disables this copy, removes its pin and returns to the library."));
            rows.Add(Row("provider", "Provider and classification details", () => Navigate(() => Information(item)), item.Error ?? item.Classification));
            rows.Add(Row("source", "Find source maps and regions", () => Navigate(() => Sources(id))));
            if (item.Factory != null && item.Template == null && item.ConstructionAttempted)
                rows.Add(Row("retry", "Retry preparation in current context", () => { item.ConstructionAttempted = false; Details(id); }, item.Error));
            Show((item.Kind == "Setting" ? "MOD SETTING: " : "DRAFT: ") + item.Label, rows);
        }
        private void ApplyDraft(GimmickEntry item)
        {
            if (item.Write != null) { item.Write(draftSetting); Gimmicks.Generation++; }
            else Gimmicks.Configure(Gimmicks.Copy(draft));
            RefreshCatalogue(); Details(item.Id); message = item.Kind == "Setting" ? "Saved in the owning mod. Restore does not undo this setting." : "Applied. The saved configuration is used by its pin.";
        }
        private void ProviderMaterials(string owner)
        {
            var rows = Gimmicks.Entries.Values.Where(e => e.Kind == "Block" && e.Owner == owner)
                .OrderBy(e => e.Label).Select(e => Row(e.Id, e.Label, () => Navigate(() => Details(e.Id)), e.Detail)).ToList();
            if (rows.Count == 0) rows.Add(new UiListItem("empty", "No material colours discovered yet", null,
                "Use source map search or colour search to discover this provider's palette.", false));
            Show("MATERIALS: " + owner, rows);
        }
        private void Change(string id, Action<GimmickRule> edit) { edit(draft); Details(id); }
        private void Modes(string id)
        {
            Show("PLACEMENT", Enum.GetValues(typeof(GimmickApplication)).Cast<GimmickApplication>().OrderBy(m => m == GimmickApplication.Auto ? -1 : (int)m).Select(mode =>
                Row(mode.ToString(), (draft.Application == mode ? "[x] " : "[ ] ") + Mode(mode), () => { draft.Application = mode; Back(); }, ModeDescription(mode))));
        }
        private static string Mode(GimmickApplication mode)
        {
            switch (mode) {
                case GimmickApplication.Auto: return "Auto (recommended)";
                case GimmickApplication.Surface: return "Ordinary surface, preserve shape";
                case GimmickApplication.FillEmpty: return "Fill empty space";
                case GimmickApplication.ReplaceMedium: return "Replace nonblocking zones";
                case GimmickApplication.OrdinarySolid: return "Ordinary solid (legacy)";
                case GimmickApplication.SolidTerrain: return "All blocking terrain";
                case GimmickApplication.ExistingBlocks: return "All existing blocks + zones";
                case GimmickApplication.FillSpace: return "Entire space, including air";
                default: return "Overlay whole screens";
            }
        }
        private static string ModeDescription(GimmickApplication mode)
        {
            if (mode == GimmickApplication.Auto) return "Confirmed surfaces replace ordinary rectangular terrain; inferred nonblocking media fill empty space. Unknown/contact effects need an explicit choice.";
            if (mode == GimmickApplication.Surface) return "Preserve ordinary rectangular terrain bounds. Existing material blocks and slopes remain unchanged.";
            if (mode == GimmickApplication.FillEmpty) return "Fill only unoccupied pixels; retain solids, slopes and existing zones. Existing nonblocking effects can overlap the new medium.";
            if (mode == GimmickApplication.ReplaceMedium) return "Replace confirmed nonblocking volumes, retaining blocking and unknown geometry.";
            if (mode == GimmickApplication.FillSpace) return "Replace the complete screen with one selected block, including air. Blocking materials can obstruct the entire map.";
            if (mode == GimmickApplication.Overlay) return "Add one nonblocking volume over the full screen, including terrain. Existing effects can overlap.";
            return "Advanced replacement. Selected existing rectangles are replaced; converting slopes requires explicit consent.";
        }
        private static string Range(GimmickRule rule)
        {
            if (rule.Screens != null) return rule.Screens.Length == 0 ? "No screens selected" : string.Join(",", rule.Screens);
            return rule.FirstScreen == 0 && rule.LastScreen == 0 ? "Entire map" : "Screens " + rule.FirstScreen + " - " + rule.LastScreen;
        }
        private void WindStrength(string id)
        {
            Show("WIND STRENGTH", new UiListItem[0]);
            number = new UiNumberControl(0, 8, .125, () => draft.WindStrength, v => draft.WindStrength = (float)v);
        }
        private void Preview(string id)
        {
            if (Gimmicks.Session == null) throw new InvalidOperationException("Load a map to preview geometry.");
            var geometry = Gimmicks.Session.Preview(draft);
            PreviewGeometry(geometry);
        }
        private void PreviewGeometry(IBlock[][] geometry)
        {
            Show("GEOMETRY PREVIEW", new UiListItem[0]);
            preview = geometry; previewScreen = Math.Max(0, Math.Min(preview.Length - 1, Camera.CurrentScreen));
            previewColours = geometry.Select(screen => screen.Select(block => {
                bool? blocking = GimmickClassification.Blocking(block);
                return blocking == true ? UiTheme.Muted : blocking == false ? new Color(39, 110, 156) : Color.Orange;
            }).ToArray()).ToArray();
        }
        private void PreviewDraw()
        {
            UiTheme.TextLine("Screen " + (previewScreen + 1) + " / " + preview.Length, new Vector2(28, 59), UiTheme.Text, true);
            var bounds = new Rectangle(28, 89, 240, 180); UiTheme.Panel(bounds, UiTheme.PanelFill, UiTheme.Border);
            for (int i = 0; i < preview[previewScreen].Length; i++) {
                var block = preview[previewScreen][i];
                Rectangle r = block.GetRect(); r.Y += previewScreen * 360;
                r = Rectangle.Intersect(r, new Rectangle(0, 0, 480, 360)); if (r.Width <= 0 || r.Height <= 0) continue;
                var draw = new Rectangle(bounds.X + r.X / 2, bounds.Y + r.Y / 2, Math.Max(1, r.Width / 2), Math.Max(1, r.Height / 2));
                Color color = previewColours[previewScreen][i];
                UiTheme.Panel(draw, color, color);
            }
            UiTheme.WrappedText("Gray: blocking\nBlue: nonblocking\nOrange: unknown\n\nRectangle bounds shown; slopes retain native collision.\n\nNo live changes.", new Rectangle(284, 89, 163, 180), UiTheme.Muted);
            Button(new Rectangle(28, 280, 95, 25), "Previous", () => previewScreen = Math.Max(0, previewScreen - 1));
            Button(new Rectangle(130, 280, 95, 25), "Next", () => previewScreen = Math.Min(preview.Length - 1, previewScreen + 1));
        }
    }
}
