using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BehaviorTree;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    /// <summary>A stable native menu row. Reuse its ID when its label or action changes.</summary>
    public sealed class NativeMenuRow
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public Action Activate { get; private set; }
        public NativeMenuRow(string id, string label, Action activate)
        {
            if (string.IsNullOrEmpty(id) || label == null || activate == null) throw new ArgumentException("Menu row requires ID, label and action");
            Id=id;Label=label;Activate=activate;
        }
    }

    /// <summary>Dynamic sibling rows after a native mod setting. Refreshed when its menu runs.</summary>
    public static class NativeMenuRows
    {
        private sealed class Rows
        {
            internal Func<IEnumerable<NativeMenuRow>> Read;
            internal readonly Dictionary<string,TextButton> Buttons=new Dictionary<string,TextButton>();
        }
        private sealed class Invoke : IBTnode
        {
            internal NativeMenuRow Row;
            private string inputLabel,fittedLabel;
            private Microsoft.Xna.Framework.Graphics.SpriteFont font;
            internal string Label(NativeMenuRow row,Microsoft.Xna.Framework.Graphics.SpriteFont value)
            {
                if(inputLabel!=row.Label || !ReferenceEquals(font,value)) {
                    inputLabel=row.Label;font=value;fittedLabel=Fit(inputLabel,font);
                }
                return fittedLabel;
            }
            protected override BTresult MyRun(TickData data) { Row.Activate();return BTresult.Success; }
        }
        private static readonly ConditionalWeakTable<TextButton,Rows> anchors=new ConditionalWeakTable<TextButton,Rows>();
        private static readonly ConditionalWeakTable<TextButton,Invoke> actions=new ConditionalWeakTable<TextButton,Invoke>();

        public static void After(TextButton anchor, Func<IEnumerable<NativeMenuRow>> read)
        {
            RuntimeApi.Kernel.CheckThread();
            if(anchor==null || read==null) throw new ArgumentNullException(anchor==null ? "anchor" : "read");
            anchors.Remove(anchor);anchors.Add(anchor,new Rows{Read=read});
        }

        internal static void Refresh(MenuSelector menu)
        {
            var previous=VanillaMenuAdapter.ChildrenViewForEdit(menu);
            bool present=false;
            foreach(var node in previous) {
                var button=node as TextButton;Rows rows;Invoke action;
                if(button!=null && (anchors.TryGetValue(button,out rows) || actions.TryGetValue(button,out action))) {present=true;break;}
            }
            if(!present) return;
            var next=new List<IBTnode>();
            bool labelsChanged=false;
            foreach(var node in previous) {
                var button=node as TextButton;Invoke ignored;
                if(button!=null && actions.TryGetValue(button,out ignored)) continue;
                next.Add(node);
                Rows rows;if(button==null || !anchors.TryGetValue(button,out rows)) continue;
                var seen=new HashSet<string>();
                foreach(var row in rows.Read()) {
                    if(row==null || !seen.Add(row.Id)) throw new InvalidOperationException("Duplicate or missing native menu row");
                    TextButton child;Invoke action;
                    if(!rows.Buttons.TryGetValue(row.Id,out child)) {
                        action=new Invoke{Row=row};child=new TextButton(action.Label(row,button.Font),action,button.Font);
                        rows.Buttons.Add(row.Id,child);actions.Add(child,action);
                    } else {
                        actions.TryGetValue(child,out action);action.Row=row;string label=action.Label(row,button.Font);
                        if(child.Text!=label) {labelsChanged=true;child.Text=label;}
                    }
                    next.Add(child);
                }
                var expired=new List<string>();foreach(var id in rows.Buttons.Keys) if(!seen.Contains(id)) expired.Add(id);
                foreach(var id in expired) rows.Buttons.Remove(id);
            }
            bool changed=labelsChanged || next.Count!=previous.Length;
            for(int i=0;!changed && i<next.Count;i++) changed=!ReferenceEquals(next[i],previous[i]);
            if(changed) VanillaMenuAdapter.SetChildren(menu,next.ToArray());
        }
        private static string Fit(string label,Microsoft.Xna.Framework.Graphics.SpriteFont font)
        {
            var text=new System.Text.StringBuilder();
            char fallback=font.DefaultCharacter ?? (font.Characters.Contains('?') ? '?' : font.Characters[0]);
            foreach(char c in label) text.Append(font.Characters.Contains(c) ? c : fallback);
            string result=text.ToString();
            if(font.MeasureString(result).X<=416) return result;
            while(result.Length>0 && font.MeasureString(result+"...").X>416) result=result.Substring(0,result.Length-1);
            return result+"...";
        }
    }
}
