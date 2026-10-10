using System;
using System.Collections.Generic;
using System.Linq;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Level;

namespace JKRuntime.Inspection
{
    internal sealed partial class GimmickPage
    {
        private void LoadedWorld()
        {
            var rows = new List<UiListItem> {
                Row("coverage", "Observation coverage", () => Navigate(() => DiagnosticLines("OBSERVATION", new[] { GimmickBlocks.HookStatus, BlockObservation.Status })), BlockObservation.Status),
                Row("factories", "Registered block factories", () => Navigate(Factories), "Registered instances only. This view doesn't call their factory methods.")
            };
            var screens = GimmickBlocks.Screens.GetValue(null) as LevelScreen[];
            int index = Camera.CurrentScreen;
            if (screens != null && index >= 0 && index < screens.Length) {
                var blocks = (IBlock[])GimmickBlocks.Hitboxes.GetValue(screens[index]);
                for (int i = 0; i < Math.Min(blocks.Length, 256); i++) {
                    var block = blocks[i];
                    rows.Add(Row("block:" + i, i + ". " + block.GetType().Name, () => Navigate(() => LiveBlock(block)), block.GetType().FullName));
                }
                if (blocks.Length > 256) rows.Add(new UiListItem("limit", "Showing first 256 of " + blocks.Length, null, "The live list is bounded; catalogue browsing groups materials separately.", false));
            }
            Show("LOADED WORLD / SCREEN " + (index + 1), rows);
        }
        private void Factories()
        {
            Show("REGISTERED FACTORIES", GimmickBlocks.ReadFactories().Select((factory, index) =>
                Row("factory:" + index, factory.GetType().Name, () => Navigate(() => DiagnosticLines("FACTORY", new[] {
                    factory.GetType().Assembly.GetName().Name, factory.GetType().FullName,
                    "Observed calls this load: " + BlockObservation.Records.Count(r => ReferenceEquals(r.Factory, factory))
                })), factory.GetType().Assembly.GetName().Name)));
        }
        private void LiveBlock(IBlock block)
        {
            var lines = new List<string> { block.GetType().Assembly.GetName().Name, block.GetType().FullName, "Bounds: " + block.GetRect() };
            var record = BlockObservation.Records.FirstOrDefault(r => ReferenceEquals(r.Block, block));
            lines.Add(record == null ? "Origin unknown: no observed loader result for this object." : "Factory: " + record.Factory.GetType().FullName);
            if (record != null) lines.Add("RGB: " + GimmickBlocks.RGB(record.Colour) + "; source screen: " + (record.Screen + 1));
            if (block is SlopeBlock) {
                // read stored lines, including foreign corrections. don't reconstruct from SlopeType
                var stored = (ErikMaths.Line[])typeof(SlopeBlock).GetField("m_lines", Gimmicks.Members).GetValue(block);
                for (int i = 0; stored != null && i < stored.Length; i++) lines.Add("Line " + i + ": " + stored[i].p0 + " -> " + stored[i].p1);
            }
            DiagnosticLines("LIVE BLOCK", lines);
        }
        private void DiagnosticLines(string name, IEnumerable<string> text)
        {
            var rows = new List<UiListItem>();
            foreach (var paragraph in text.Where(t => !string.IsNullOrEmpty(t))) {
                string remaining = paragraph;
                while (remaining.Length > 0) {
                    int length = remaining.Length;
                    while (length > 1 && UiTheme.FitText(remaining.Substring(0, length), 402, true) != remaining.Substring(0, length)) length--;
                    rows.Add(new UiListItem("line:" + rows.Count, remaining.Substring(0, length), null, "", false));
                    remaining = remaining.Substring(length);
                }
            }
            Show(name, rows);
        }
    }
}
