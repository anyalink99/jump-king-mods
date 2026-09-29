using System;
using System.Collections.Generic;
using System.Linq;
using JumpKing.SaveThread;
using JumpKing.Workshop;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

namespace RunVerifier
{
    internal sealed class CompletionMarker
    {
        internal string MapId,Name;
        internal bool Complete;
    }
    internal sealed class MarkerPage : ScopedUiPage
    {
        private readonly UiFrame frame=new UiFrame(new Rectangle(15,12,450,336));
        private readonly List<CompletionMarker> markers=new List<CompletionMarker>();
        private int index,top;
        private bool review;
        protected override void OpenPage(JKRuntime.RuntimeScope resources)
        {
            index=top=0;review=false;markers.Clear();
            var flags=EventFlagsSave.Save.saved_flags;
            markers.Add(new CompletionMarker {MapId="local:Jump King",Name="Jump King",Complete=flags.Contains(StoryEventFlags.CompletedNormalGame)});
            markers.Add(new CompletionMarker {MapId="builtin:new-babe-plus",Name="New Babe+",Complete=flags.Contains(StoryEventFlags.CompletedNBP)});
            markers.Add(new CompletionMarker {MapId="builtin:ghost-of-the-babe",Name="Ghost of the Babe",Complete=flags.Contains(StoryEventFlags.CompletedGhost)});
            var completed=CompletedUGCSave.Save;
            if(WorkshopManager.instance!=null)foreach(var level in WorkshopManager.instance.levels.OrderBy(l=>l.Name))
                markers.Add(new CompletionMarker {MapId="workshop:"+level.ID,Name=level.Name,Complete=completed.ContainsKey(level.ID)});
        }
        public override void Update(UiInput input,float delta)
        {
            if(input.Cancel){if(review)review=false;else WantsClose=true;return;}
            if(!review){if(input.Up)index=(index+markers.Count-1)%markers.Count;if(input.Down)index=(index+1)%markers.Count;top=Math.Max(0,Math.Min(top,index));if(index>=top+6)top=index-5;}
            if(input.Confirm)Activate();
        }
        private void Activate(){if(review){ModEntry.Client.PublishMarker(markers[index]);WantsClose=true;}else review=true;}
        public override void Draw()
        {
            UiPointer.BeginSurface(this);frame.Draw();HistoryPage.Text(review?"SUBMIT COMPLETION MARKER":"MAP COMPLETION MARKERS",31,28,UiTheme.Text);
            if(review){var marker=markers[index];HistoryPage.Text(UiTheme.FitText(marker.Name,410,true),31,78,UiTheme.Gold);HistoryPage.Text(marker.Complete?"Completed":"Not completed",31,115,UiTheme.Text);HistoryPage.Text("Unverified / no run time",31,145,UiTheme.Muted);HistoryPage.Text("Unlisted; awaits website review",31,175,UiTheme.Muted);HistoryPage.Text("Submit",31,225,UiTheme.Gold);UiPointer.Region(new Rectangle(31,217,150,30),delegate{},Activate);}
            else for(int i=top;i<Math.Min(markers.Count,top+6);i++){int row=i;var marker=markers[i];UiPointer.Region(new Rectangle(31,76+(i-top)*30,418,29),delegate{index=row;},Activate);UiTheme.TextLine(UiTheme.FitText((i==index?"> ":"  ")+marker.Name,290,true),new Vector2(31,76+(i-top)*30),i==index?UiTheme.Gold:UiTheme.Text,true);UiTheme.TextLine(marker.Complete?"Completed":"Not completed",new Vector2(333,76+(i-top)*30),UiTheme.Muted,true);}
            UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),UiInputHints.Command(UiAction.Confirm,review?"Submit":"Review"),UiInputHints.Command(UiAction.Cancel,"Back"));
        }
    }
}
