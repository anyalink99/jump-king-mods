using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace OverlayPlus
{
    internal static class Widgets
    {
        internal static Run Comparison;
        private static Run best;
        private static double? sum;
        private static readonly Dictionary<string,double?> golds=new Dictionary<string,double?>();
        internal static readonly string[] Variables={"time","game_time","real_time","active_time","segment","session","attempt","area","screen","height","jumps","falls","pb","sum_best","delta","map","campaign","category","status"};
        private static long refreshed=-1;
        private static string recordSettings;
        internal static void Refresh()
        {
            if(Store.Archive==null||Controller.Tracker==null)return;
            var run=Controller.Tracker.Run;var clock=Store.Settings.Clock;
            string key=run.Id+"|"+run.RulesKey+"|"+run.Category+"|"+clock+"|"+Store.Settings.Comparison;
            if(refreshed==Store.ArchiveRevision&&recordSettings==key)return;refreshed=Store.ArchiveRevision;recordSettings=key;
            best=Records.Best(Store.Archive,run.Category,clock,run.RulesKey);sum=Records.Sum(Store.Archive,run.Category,clock,run.RulesKey);golds.Clear();foreach(var a in Controller.Route.Areas)golds[a.Id]=Records.Gold(Store.Archive,run.Category,a.Id,clock,run.RulesKey);
            string comparison=Store.Settings.Comparison;Comparison=comparison=="PB"?best:comparison=="Previous"?Store.Archive.Runs.LastOrDefault(r=>r.Id!=run.Id&&r.Category==run.Category&&r.RulesKey==run.RulesKey&&r.Status=="Finished"):Store.Archive.Runs.FirstOrDefault(r=>r.Id==comparison&&r.RulesKey==run.RulesKey);
        }
        internal static bool Visible(Widget w,bool requireTimerDrawing=true)
        {
            if(!w.Enabled)return false;if(Editor.Open)return true;if(requireTimerDrawing&&w.Kind==WidgetKind.Timer&&!GameTimer.HasDrawing)return false;if(Controller.Surface.Bounds(w)==Rectangle.Empty)return false;
            if(Controller.Tracker==null)return w.Visibility==Visibility.Always;
            switch(w.Visibility){case Visibility.Running:return Controller.Tracker.Run.Status=="Running";case Visibility.Finished:return Controller.Tracker.Run.Status=="Finished";case Visibility.Pressed:return Inputs.Get(w.Source,w.Device).Down;default:return true;}
        }
        internal static void Draw(Canvas c,Widget w)
        {
            if(!Visible(w))return;var r=Controller.Surface.Bounds(w);if(r==Rectangle.Empty)return;var visible=Rectangle.Intersect(r,Controller.Surface.Window);if(visible.Width<=0||visible.Height<=0)return;c.Clip(visible);
            try{
                c.Fill(r,Canvas.ColorOf(w.Background,w.Opacity));if(w.GameFrame)c.Frame(r,w.Opacity,false);else if(w.Border>0)c.Border(r,Canvas.ColorOf(w.Accent,w.Opacity*.5f),(int)Math.Max(1,w.Border));
                int pad=(int)w.Padding;var content=new Rectangle(r.X+pad,r.Y+pad,Math.Max(1,r.Width-2*pad),Math.Max(1,r.Height-2*pad));Color fg=Canvas.ColorOf(w.Foreground,w.Opacity);
                switch(w.Kind){
                    case WidgetKind.Panel:break;
                    case WidgetKind.Timer:GameTimer.Draw(c,w,content);break;
                    case WidgetKind.Text:c.Wrapped(Expand(w.Text),content,w.TextSize,fg,w.Font,w.Align,w.Shadow,w.Outline);break;
                    case WidgetKind.Splits:DrawSplits(c,w,content);break;
                    case WidgetKind.Inputs:DrawInputs(c,w,content);break;
                    case WidgetKind.Button:DrawKey(c,w,new InputKey{Source=w.Source,Label=w.Text=="Overlay+"?"":w.Text},content);break;
                    case WidgetKind.Stats:c.Wrapped(Expand("Attempt {attempt}\nJumps {jumps}   Falls {falls}\nArea {area}\nScreen {screen}   Height {height}"),content,w.TextSize,fg,w.Font,w.Align,w.Shadow,w.Outline);break;
                    case WidgetKind.Charge:DrawCharge(c,w,content);break;
                    case WidgetKind.Equipment:c.Wrapped("Boots  "+(Editor.Preview||Native.Equipped("Boots")?"ON":"OFF")+"\nRing    "+(Editor.Preview||Native.Equipped("Snake")?"ON":"OFF"),content,w.TextSize,fg,w.Font,w.Align,w.Shadow,w.Outline);break;
                    case WidgetKind.Image:if(!c.Image(w.ImagePath,content,w.Opacity)&&Editor.Open)c.Wrapped("Image: "+w.ImagePath+"\nPlace files in OverlayPlus/Images",content,16,Color.LightGray,w.Font,"Left",false,false);break;
                    case WidgetKind.External:if(!Adapters.Draw(c,w,content)&&Editor.Open)c.Wrapped("Source unavailable\n"+w.Source,content,16,Color.LightGray,w.Font,"Left",false,false);break;
                }
            }finally{c.Unclip();}
        }
        internal static string Timer(string source)
        {
            if(Editor.Preview)return source=="Segment"?"0:42.83":"1:23:45.67";
            if(Controller.Tracker==null)return "0:00.00";var run=Controller.Tracker.Run;
            switch(source){case "RealTime":return Records.Format(run.Elapsed.Real);case "ActiveTime":return Records.Format(run.Elapsed.Active);case "Segment":return Records.Format(Segment());case "Session":return Records.Format(Controller.Now-Controller.SessionStart);default:return Records.Format(run.Elapsed.Game);}
        }
        private static double Segment(){var run=Controller.Tracker.Run;return Math.Max(0,run.Elapsed.Value(Store.Settings.Clock)-(run.Splits.Count==0?0:run.Splits.Last().End.Value(Store.Settings.Clock)));}
        internal static string Expand(string text){return Regex.Replace(text??"",@"\{([a-z_]+)\}",m=>Variable(m.Groups[1].Value));}
        private static string Variable(string key)
        {
            var tracker=Controller.Tracker;if(tracker==null)return "--";var r=tracker.Run;var route=tracker.Route;var clock=Store.Settings.Clock;
            switch(key){case "time":return Records.Format(r.Elapsed.Value(clock));case "game_time":return Timer("GameTime");case "real_time":return Timer("RealTime");case "active_time":return Timer("ActiveTime");case "segment":return Timer("Segment");case "session":return Timer("Session");case "attempt":return Store.Archive.Runs.Count.ToString();case "area":return route.Areas.Count>r.CurrentArea?route.Areas[Math.Max(0,r.CurrentArea)].Name:"No Areas";case "screen":return JumpKing.Camera.CurrentScreenIndex1.ToString();case "height":return JumpKing.GameManager.GameLoop.m_player==null?"--":Math.Max(0,360-JumpKing.GameManager.GameLoop.m_player.m_body.Position.Y).ToString("0");case "jumps":return r.Jumps.ToString();case "falls":return r.Falls.ToString();case "pb":return Records.Format(best==null?(double?)null:best.Elapsed.Value(clock));case "sum_best":return Records.Format(sum);case "map":return route.Name;case "campaign":return route.Campaign.ToString();case "category":return r.Category;case "status":return r.Practice?"Practice":r.Status;case "delta":if(Comparison==null||r.Splits.Count==0||Comparison.Splits.Count<r.Splits.Count)return "--";return Records.Delta(r.Splits.Last().End.Value(clock)-Comparison.Splits[r.Splits.Count-1].End.Value(clock));default:return "{"+key+"}";}
        }
        private static void DrawSplits(Canvas c,Widget w,Rectangle r)
        {
            var tracker=Controller.Tracker;if(tracker==null)return;var areas=tracker.Route.Areas;var run=tracker.Run;float size=w.TextSize,line=size*1.6f;Color fg=Canvas.ColorOf(w.Foreground,w.Opacity),accent=Canvas.ColorOf(w.Accent,w.Opacity);
            c.Text("AREA SPLITS",r.X,r.Y,size,accent,w.Font);float y=r.Y+line;c.Text(c.Fit(tracker.Route.Campaign==Campaign.Custom?tracker.Route.Name:CampaignLabel(tracker.Route.Campaign),r.Width,size*.8f,w.Font),r.X,y,size*.8f,fg,w.Font);y+=line;
            if(areas.Count==0){c.Wrapped("This map has no native Areas.",new Rectangle(r.X,(int)y,r.Width,Math.Max(1,r.Bottom-(int)y)),size,fg,w.Font,"Left",false,false);return;}
            bool narrow=r.Width<210;float rowHeight=narrow?size*2.8f:line;
            int rows=Math.Max(1,Math.Min(w.Rows,(int)((r.Bottom-y-line*3)/rowHeight))),start=w.Compact?Math.Max(0,Math.Min(areas.Count-rows,run.CurrentArea-rows/2)):0;
            for(int i=start;i<areas.Count&&i<start+rows;i++){
                if(y+rowHeight>r.Bottom-line*2.3f)break;bool current=i==run.CurrentArea&&run.Status=="Running";if(current)c.Fill(new Rectangle(r.X,(int)y,r.Width,(int)rowHeight),Canvas.ColorOf(w.Accent,w.Opacity*.12f));
                Split split=i<run.Splits.Count?run.Splits[i]:null;string time=split==null?(current?Records.Format(w.SegmentTimes?Segment():run.Elapsed.Value(Store.Settings.Clock)):"--"):Records.Format((w.SegmentTimes?split.Duration:split.End).Value(Store.Settings.Clock));if(Editor.Preview)time="12:34.56";
                Color color=current?accent:fg;double? gold;golds.TryGetValue(areas[i].Id,out gold);if(split!=null&&split.Eligible&&gold.HasValue&&split.Duration.Value(Store.Settings.Clock)<=gold.Value+.00001)color=accent;
                float valueWidth=c.Measure(time,size,w.Font);string name=c.Fit(areas[i].Name,Math.Max(20,narrow?r.Width-6:r.Width-valueWidth-12),size,w.Font);c.Text(name,r.X+3,y,size,color,w.Font);c.Text(time,r.Right-valueWidth,y+(narrow?size*1.2f:0),size,color,w.Font);y+=rowHeight;
                if(w.ShowDelta&&split!=null&&Comparison!=null&&i<Comparison.Splits.Count&&y+size*.8f<r.Bottom-line*2){double delta=(w.SegmentTimes?split.Duration:split.End).Value(Store.Settings.Clock)-(w.SegmentTimes?Comparison.Splits[i].Duration:Comparison.Splits[i].End).Value(Store.Settings.Clock);string deltaText=Records.Delta(delta);c.Text(deltaText,r.Right-c.Measure(deltaText,size*.75f,w.Font),y-4,size*.75f,delta<=0?new Color(101,213,170):new Color(241,121,119),w.Font);y+=size*.8f;}
            }
            y=Math.Max(y+4,r.Bottom-line*2.3f);if(y+line*2<r.Bottom+2){c.Text("PB  "+Records.Format(best==null?(double?)null:best.Elapsed.Value(Store.Settings.Clock)),r.X,y,size*.8f,fg,w.Font);c.Text("Best segments  "+Records.Format(sum),r.X,y+line*.7f,size*.8f,fg,w.Font);c.Text(run.Practice?"PRACTICE · "+Store.Settings.Clock:Store.Settings.Clock.ToString(),r.X,y+line*1.4f,size*.7f,run.Practice?new Color(244,161,113):accent,w.Font);}
        }
        internal static string CampaignLabel(Campaign c){return c==Campaign.MainBabe?"Main Babe":c==Campaign.NewBabePlus?"New Babe Plus":c==Campaign.GhostOfTheBabe?"Ghost of the Babe":"Custom map";}
        private static void DrawInputs(Canvas c,Widget w,Rectangle r)
        {
            if(w.Keys.Count==0)return;
            if(w.InputStyle==InputStyle.Row){float cell=r.Width/(float)w.Keys.Count;for(int i=0;i<w.Keys.Count;i++)DrawKey(c,w,w.Keys[i],new Rectangle(r.X+(int)(i*cell),r.Y,(int)cell-5,r.Height));return;}
            if(w.InputStyle==InputStyle.Controller){c.Fill(new Rectangle(r.X+r.Width/10,r.Y+r.Height/4,r.Width*8/10,r.Height/2),new Color(22,23,20)*w.Opacity);for(int i=0;i<w.Keys.Count;i++)DrawKey(c,w,w.Keys[i],ControllerKeyBounds(r,i,w.Keys.Count));return;}
            float width=Math.Max(1,w.Keys.Max(k=>k.X+k.Width)),height=Math.Max(1,w.Keys.Max(k=>k.Y+k.Height)),scale=Math.Min(r.Width/width,r.Height/height);foreach(var k in w.Keys)DrawKey(c,w,k,new Rectangle(r.X+(int)(k.X*scale),r.Y+(int)(k.Y*scale),(int)(k.Width*scale)-3,(int)(k.Height*scale)-3));
        }
        internal static Rectangle ControllerKeyBounds(Rectangle r,int index,int count)
        {
            if(count>5){int columns=(int)Math.Ceiling(Math.Sqrt(count*r.Width/(float)Math.Max(1,r.Height))),rows=(int)Math.Ceiling(count/(float)columns);int width=r.Width/columns,height=r.Height/rows;return new Rectangle(r.X+(index%columns)*width,r.Y+(index/columns)*height,Math.Max(1,width-3),Math.Max(1,height-3));}
            int size=Math.Max(1,(int)Math.Min(r.Height/3f,r.Width/8f));float x=index==0?.08f:index==1?.28f:index==2?.73f:index==3?.57f:.86f,y=index==3?.02f:index==4?.64f:.33f;
            return new Rectangle(r.X+(int)(r.Width*x),r.Y+(int)(r.Height*y),size,size);
        }
        private static void DrawKey(Canvas c,Widget w,InputKey k,Rectangle r)
        {
            var state=Inputs.Get(k.Source,w.Device);bool pressed=Editor.Preview||Inputs.DisplayPressed(state,w.MinimumPress);var accent=Canvas.ColorOf(w.Accent,w.Opacity);c.Fill(r,pressed?accent:new Color(26,25,20,235)*w.Opacity);c.Border(r,pressed?new Color(255,222,130)*w.Opacity:new Color(112,104,80)*w.Opacity);c.Fill(new Rectangle(r.X+1,r.Bottom-3,Math.Max(1,r.Width-2),2),new Color(8,7,5)*w.Opacity);Color fg=pressed?new Color(28,24,17)*w.Opacity:Canvas.ColorOf(w.Foreground,w.Opacity);float size=Math.Min(w.TextSize,Math.Max(10,r.Height*.32f));string label=string.IsNullOrEmpty(k.Label)?k.Source:k.Label;
            string text=w.ShowLabels?label:"";if(w.ShowBindings&&state.Label!=text){if(text!="")text+="\n";text+=state.Label;}if(w.ShowDuration){if(text!="")text+="\n";text+=(state.Duration*1000).ToString("0")+" ms";}
            var lines=text.Split('\n');size=Math.Min(size,Math.Max(8,(r.Height-10)/(lines.Length*1.3f)));float y=r.Y+Math.Max(3,(r.Height-lines.Length*size*1.3f)/2);foreach(string line in lines){string fit=c.Fit(line,Math.Max(1,r.Width-8),size,w.Font);c.Text(fit,r.X+(r.Width-c.Measure(fit,size,w.Font))/2,y,size,fg,w.Font);y+=size*1.3f;}
        }
        private static void DrawCharge(Canvas c,Widget w,Rectangle r)
        {float value=Editor.Preview?.72f:Controller.Charge;var fill=new Rectangle(r.X,r.Bottom-10,(int)(r.Width*Math.Max(0,Math.Min(1,value))),8);c.Fill(new Rectangle(r.X,r.Bottom-10,r.Width,8),new Color(46,53,64)*w.Opacity);c.Fill(fill,Canvas.ColorOf(w.Accent,w.Opacity));c.Text("CHARGE  "+(value*100).ToString("0")+"%",r.X,r.Y,w.TextSize,Canvas.ColorOf(w.Foreground,w.Opacity),w.Font);}
    }
}
