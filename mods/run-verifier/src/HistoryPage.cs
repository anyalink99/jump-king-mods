using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JKRuntime.UI;
using Microsoft.Xna.Framework;
using JumpKing;

namespace RunVerifier
{
    internal sealed class HistoryPage : ScopedUiPage
    {
        private readonly Action continueGame;
        private readonly UiFrame frame=new UiFrame(new Rectangle(15,12,450,336));
        private List<RunRecord> runs;
        private RunRecord selected;
        private int index,top,tab;
        private bool favorites,settings;
        private int Visible { get { return (selected==null&&!settings)||(selected!=null&&(tab==1||tab==2||tab==4))?5:9; } }
        private string filter="",sort="date";
        private IUiPage entry;
        private readonly string[] tabs={"Overview","Areas","Falls","Mods","Replays","Actions"};
        internal HistoryPage(Action resume) { continueGame=resume; }
        protected override void OpenPage(JKRuntime.RuntimeScope resources)
        {
            selected=null;settings=false;tab=0;entry=null;
            resources.Defer(delegate { if(entry!=null){entry.OnClose();entry=null;} });
            ModEntry.Initialize();Refresh();
        }
        private void Refresh()
        {
            runs=ModEntry.Repository.List().Where(r=>(!favorites||r.favorite)&&(filter.Length==0||r.mapName.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
            if(sort=="time") runs=runs.OrderBy(r=>r.time).ToList();
            if(sort=="map") runs=runs.OrderBy(r=>r.mapName).ThenBy(r=>r.time).ToList();
            index=0;top=0;
        }
        private List<string> Rows()
        {
            if(selected==null)
            {
                if(!settings)return runs.Count==0?new List<string>{"No completed runs yet"}:runs.Select(r=>r.mapName).ToList();
                var rows=new List<string> { "Sign in to website", "Online + auto upload (unlisted): "+(ModEntry.Client.Online?"ON":"OFF"), "Retry pending uploads", "Search maps: "+(filter==""?"all":filter), "Favorites: "+(favorites?"only":"all"), "Sort: "+sort, "Upload map completion marker", "Upload current attempt (unverified)" };

                return rows;
            }
            var result=new List<string>();
            if(tab==0)
            {
                result.AddRange(new[] { selected.mapName, "Time: "+Time(selected.time),"Steam: "+selected.steamId,"Evidence: "+selected.status,"Category: "+selected.category,
                    "Jumps / falls: "+selected.jumps+" / "+selected.nativeFalls,"Sessions: "+selected.sessions.Count,"Observed: "+Time(selected.observed),"Ending: "+selected.ending,"Revision: "+selected.revision.Substring(0,16),"ID: "+selected.id });
                var pb=runs.Where(r=>r.mapId==selected.mapId&&r.revision==selected.revision&&r.category==selected.category).OrderBy(r=>r.time).FirstOrDefault();
                if(pb!=null)result.Add("Personal best: "+Time(pb.time)+" ("+(selected.time-pb.time).ToString("+0.000;-0.000;0")+"s)");
                result.AddRange(selected.reasons);
            }
            if(tab==1) result.AddRange(selected.areas.Select(a=>Trim(a.name,22)+" "+Time(a.seconds)+" / "+a.visits+" visits; first "+Time(a.first)));
            if(tab==2) result.AddRange(selected.falls.OrderByDescending(f=>f.height).Select(f=>Time(f.at)+" "+f.kind+" "+f.height.ToString("0")+"px "+f.from+">"+f.to+" recovery "+(f.recovery<0?"--":Time(f.recovery))));
            if(tab==3) { result.Add("Native flags: "+selected.nativePeak+"; unknown: "+selected.unknown);result.Add("Registration does not prove use.");result.AddRange(selected.sources);result.AddRange(selected.mechanics);result.AddRange(selected.environment.Select(e=>e.id+" "+e.version+" "+e.hash.Substring(0,8))); }
            if(tab==4) result.AddRange(selected.attachments.Select(a=>"Watch "+Time(a.start)+" - "+Time(a.end)+" ("+(a.size/1024)+" KB)"));
            if(tab==5) result.AddRange(new[] { "Upload / retry (unlisted)","Open result on website","Export certificate and statistics","Favorite: "+(selected.favorite?"yes":"no"),"Open local history folder" });
            if(result.Count==0)result.Add("No recorded data");return result;
        }
        public override void Update(UiInput input,float delta)
        {
            if(entry!=null) { entry.Update(input,delta); if(entry.WantsClose) { entry.OnClose();entry=null;Refresh(); } return; }
            int count=Rows().Count;
            if(input.Cancel) { if(selected==null){if(settings){settings=false;Refresh();}else WantsClose=true;}else {selected=null;Refresh();}return; }
            if(selected==null&&input.Secondary){settings=!settings;Refresh();return;}
            if(selected!=null&&(input.Left||input.Right)) { tab=(tab+(input.Right?1:tabs.Length-1))%tabs.Length;index=top=0;return; }
            if(input.Up)index=(index+count-1)%count;
            if(input.Down)index=(index+1)%count;
            if(index<top)top=index;if(index>=top+Visible)top=index-Visible+1;
            if(input.Confirm)Activate();
        }
        private void Activate()
        {
            try
            {
                UiSounds.Play(UiSound.Confirm);
                if(selected==null)
                {
                    if(!settings){if(runs.Count>0){selected=runs[index];index=top=tab=0;}return;}
                    if(index==0)ModEntry.Client.Pair();
                    else if(index==1)ModEntry.Client.ToggleOnline();
                    else if(index==2)ModEntry.Client.Retry();
                    else if(index==3){entry=new UiTextEntryPage("Search maps",filter,s=>filter=s,64,true);entry.OnOpen();}
                    else if(index==4){favorites=!favorites;Refresh();}
                    else if(index==5){sort=sort=="date"?"map":sort=="map"?"time":"date";Refresh();}
                    else if(index==6){entry=new MarkerPage();entry.OnOpen();}
                    else if(index==7)ModEntry.Client.PublishAttempt(ModEntry.Current);

                    return;
                }
                if(tab==4&&index<selected.attachments.Count)
                {if(ModEntry.Watch(selected.attachments[index].id)){WantsClose=true;if(continueGame!=null)continueGame();}else ModEntry.Status="Load the matching map revision before watching";}
                if(tab!=5)return;
                if(index==0)ModEntry.Client.Publish(selected);
                if(index==1)Process.Start(Client.Site+"/jumpking/run/"+selected.id);
                if(index==2){string path=Path.Combine(ModEntry.Repository.Root,selected.id+".export.json");Repository.Atomic(path,Json.Write(selected));Process.Start("explorer.exe","/select,\""+path+"\"");}
                if(index==3){selected.favorite=!selected.favorite;ModEntry.Repository.Save(selected);}
                if(index==4)Process.Start(ModEntry.Repository.Root);
            }catch(Exception e){ModEntry.Status=e.Message;}
        }
        public override void Draw()
        {
            if(entry!=null){entry.Draw();return;}
            UiPointer.BeginSurface(this);frame.Draw();
            Text(settings?"RUN VERIFIER / SETTINGS":"RUN HISTORY",31,28,UiTheme.Text);
            Small(selected==null?(settings?"Connection, uploads and library filters":runs.Count+" completions  /  "+sort+(favorites?"  /  favorites":"")):selected.mapName,31,50,UiTheme.Muted,410);
            if(selected!=null)for(int t=0;t<tabs.Length;t++)
            {
                int next=t;var bounds=new Rectangle(31+t*69,67,68,19);
                UiTheme.Tab(tabs[t],bounds,t==tab);
                UiPointer.Region(bounds,delegate{},delegate{tab=next;index=top=0;});
            }
            var rows=Rows();int start=selected==null?76:94,step=Visible==5?38:19;
            for(int i=top;i<Math.Min(rows.Count,top+Visible);i++)
            {
                int row=i;var bounds=new Rectangle(31,start+(i-top)*step,418,step-2);
                if(selected==null||!(tab==1&&i<selected.areas.Count||tab==2&&i<selected.falls.Count))Small((i==index?"> ":"  ")+rows[i],33,bounds.Y,i==index?UiTheme.Gold:UiTheme.Text,408);
                if(selected==null&&!settings&&runs.Count>0)
                {
                    var r=runs[i];Small(Time(r.time)+"  /  "+r.status+(r.favorite?"  *":""),46,bounds.Y+15,UiTheme.Muted,394);
                }
                if(selected!=null&&tab==1&&i<selected.areas.Count){var area=selected.areas[i];Small((i==index?"> ":"  ")+area.name,33,bounds.Y,i==index?UiTheme.Gold:UiTheme.Text,220);Small(Time(area.seconds),320,bounds.Y,UiTheme.Text,120);Small(area.visits+" visits  /  first "+Time(area.first),46,bounds.Y+15,UiTheme.Muted,394);}
                if(selected!=null&&tab==2&&i<selected.falls.Count){var fall=selected.falls.OrderByDescending(f=>f.height).ElementAt(i);Small(fall.height.ToString("0")+" px  /  "+fall.kind+"  /  at "+Time(fall.at),33,bounds.Y,i==index?UiTheme.Gold:UiTheme.Text,408);Small("Duration "+fall.duration.ToString("0.00")+"s  /  recovery "+(fall.recovery<0?"not regained":Time(fall.recovery)),46,bounds.Y+15,UiTheme.Muted,394);}
                UiPointer.Region(bounds,delegate{index=row;},Activate);
            }
            UiPointer.ScrollRegion(new Rectangle(31,start,418,Visible*step),delegate(int amount){index=Math.Max(0,Math.Min(rows.Count-1,index+(amount>0?-1:1)));top=Math.Max(0,Math.Min(index,rows.Count-Visible));});
            Small(rows.Count>Visible?(top+1)+"-"+Math.Min(rows.Count,top+Visible)+" / "+rows.Count:"",365,28,UiTheme.Muted,75);
            Small(ModEntry.Repository.Error??(string.IsNullOrEmpty(ModEntry.Status)?ModEntry.Client.Status:ModEntry.Status),31,279,UiTheme.Muted,418);
            if(selected==null)UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
                UiInputHints.Command(UiAction.Confirm,"Select"),UiInputHints.Command(UiAction.Secondary,settings?"History":"Settings"),UiInputHints.Command(UiAction.Cancel,"Back"));
            else UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),UiInputHints.Command(UiAction.Confirm,"Select"),
                UiInputHints.Command(UiAction.Left,"Tab"),UiInputHints.Command(UiAction.Right,"Tab"),UiInputHints.Command(UiAction.Cancel,"Back"));
        }
        private static void Small(string s,int x,int y,Color color,int width)
        { UiTheme.TextLine(UiTheme.FitText(s,width,true),new Vector2(x,y),color,true); }
        internal static string Time(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return ((int)t.TotalHours).ToString("00")+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00")+"."+t.Milliseconds.ToString("000");}
        internal static string Trim(string s,int length){s=s??"";return s.Length>length?s.Substring(0,length-3)+"...":s;}
        internal static void Text(string s,int x,int y,Color color){UiTheme.TextLine(s,new Vector2(x,y),color,false);}
    }
}
