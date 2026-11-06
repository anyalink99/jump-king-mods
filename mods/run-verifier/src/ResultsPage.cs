using System;
using System.Collections.Generic;
using System.Reflection;
using JKRuntime.UI;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.MiscSystems.Achievements;
using Microsoft.Xna.Framework;

namespace RunVerifier
{
    internal sealed class ResultsPage : ScopedUiPage
    {
        private static readonly FieldInfo LinesField=typeof(StatsScreen).GetField("m_lines",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly RunRecord record;
        private readonly StatsScreen screen=new StatsScreen();
        private readonly ResultsSeal seal=new ResultsSeal(false);
        internal ResultsPage(RunRecord value)
        {
            if(value==null||!value.complete)throw new ArgumentException("Completed run required");
            record=value.Copy();
            LinesField.SetValue(screen,ModEntry.Repository.GetResultLines(record.id)??BuildLines(record,null));
        }
        internal static string[] NativeLines(StatsScreen screen) { return (string[])LinesField.GetValue(screen); }
        internal static string[] BuildLines(RunRecord record,PlayerStats? stats)
        {
            var lines=new List<string> {record.mapName??"Jump King"};
            if(stats.HasValue)lines.Add("Completed on attempt "+stats.Value.attempts+" - session "+stats.Value.session);
            var time=TimeSpan.FromSeconds(record.time);
            lines.Add("Time : "+(int)time.TotalHours+"h "+time.Minutes+"m "+time.Seconds+"s "+time.Milliseconds+"ms");
            lines.Add("Jumps : "+record.jumps);lines.Add("Falls : "+record.nativeFalls);
            if(record.nativePeak>0)lines.Add("Player Behaviour Modifier(s) Detected");
            return lines.ToArray();
        }
        public override void Update(UiInput input,float delta) { if(input.Cancel)WantsClose=true; }
        public override void Draw()
        {
            // drawing alone skips the native completion/save callbacks
            UiPointer.BeginSurface(this);
            screen.Draw();seal.DrawRecord(record,record.status);
            UiPointer.Region(new Rectangle(0,0,480,360),delegate{},delegate{});
        }
    }
}
