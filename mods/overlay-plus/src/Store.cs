using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;

namespace OverlayPlus
{
    internal static class Store
    {
        internal static string Root;
        internal static SettingsData Settings;
        internal static RunArchive Archive;
        internal static long ArchiveRevision;
        internal static string Warning="";
        private static readonly object disk=new object();
        private static readonly Dictionary<string,Func<string>> pending=new Dictionary<string,Func<string>>();
        private static bool worker;
        private static readonly JKRuntime.BackgroundWorkQueue saves=new JKRuntime.BackgroundWorkQueue("overlay-plus.saves",2);
        internal static void Initialize(string root){Root=root;Settings=Load<SettingsData>(Path.Combine(root,"Layouts.xml"))??Defaults.Settings();Validate(Settings);MigratePresentation(Settings);}
        internal static void MigratePresentation(SettingsData settings)
        {
            if(settings.PresentationRevision>=2)return;
            foreach(var w in settings.Layouts.SelectMany(l=>l.Widgets).Concat(settings.Templates).Concat(settings.GroupTemplates.SelectMany(l=>l.Widgets))){
                if(Math.Abs(w.MinimumPress-.08f)<.0001f)w.MinimumPress=0;
                if(w.Kind==WidgetKind.Timer){if(w.Source=="GameTime"){w.Padding=0;w.Background=0;w.GameFrame=false;w.Border=0;w.Name="Game timer";}else {w.Kind=WidgetKind.Text;w.Text=w.Source=="RealTime"?"{real_time}":w.Source=="ActiveTime"?"{active_time}":w.Source=="Segment"?"{segment}":"{session}";}}
            }
            settings.PresentationRevision=2;
        }
        internal static T Load<T>(string path) where T:class
        {
            lock(disk){foreach(string p in new[]{path,path+".bak"}){if(!File.Exists(p))continue;try{var value=XmlData.Parse<T>(File.ReadAllText(p));if(p!=path){Warning="Recovered backup: "+Path.GetFileName(path);File.Copy(p,p+".recovered-"+Guid.NewGuid().ToString("N"));}return value;}catch(Exception e){Warning="Could not read "+Path.GetFileName(p)+": "+e.Message;try{File.Copy(p,p+".unreadable-"+Guid.NewGuid().ToString("N"));}catch(Exception copy){Console.WriteLine("[Overlay+] Preserve unreadable file: "+copy.Message);}}}return null;}
        }
        internal static void Write(string path,string xml)
        {
            lock(disk){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] b=Encoding.UTF8.GetBytes(xml.Replace("encoding=\"utf-16\"","encoding=\"utf-8\""));f.Write(b,0,b.Length);f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
        }
        internal static void Queue<T>(string path,T value)
        {
            // Detach mutable data quickly on its owning game thread. XML encoding
            // and durable disk writes both belong to the coalescing worker.
            var archive=value as RunArchive;
            var snapshot=archive==null?XmlData.Clone(value):(T)(object)archive.Copy();
            lock(pending){
                if(!pending.ContainsKey(path)&&pending.Count>=128)throw new IOException("Overlay+ save queue is full; try again after pending saves complete");
                pending[path]=()=>XmlData.Serialize(snapshot);if(worker)return;
                JKRuntime.BackgroundWork work;
                if(!saves.TryEnqueue(Drain,out work))throw new IOException("Overlay+ save worker is unavailable");
                worker=true;
            }
        }
        private static void Drain()
        {
            while(true){KeyValuePair<string,Func<string>> entry;lock(pending){if(pending.Count==0){worker=false;Monitor.PulseAll(pending);return;}entry=pending.First();pending.Remove(entry.Key);}try{Write(entry.Key,entry.Value());}catch(Exception e){Warning="Save failed: "+e.Message;Console.WriteLine("[Overlay+] "+Warning);}}
        }
        internal static void Flush(){if(!saves.Drain(30000))throw new IOException("Overlay+ saves did not finish within 30 seconds");}
        internal static void SaveSettings(){Validate(Settings);Queue(Path.Combine(Root,"Layouts.xml"),Settings);}
        internal static void LoadArchive(Route route){Archive=Load<RunArchive>(Path.Combine(Root,"Runs",route.Key+".xml"))??new RunArchive{Route=route};ValidateArchive(Archive,route);ArchiveRevision++;}
        internal static void ValidateArchive(RunArchive archive,Route route)
        {
            if(archive.Version!=1||archive.Route==null||archive.Route.Key!=route.Key)throw new InvalidDataException("Unsupported run archive; original file preserved");
            archive.Runs=archive.Runs??new List<Run>();var ids=new HashSet<string>();
            foreach(var run in archive.Runs){if(run==null||string.IsNullOrEmpty(run.Id)||!ids.Add(run.Id)||run.Version!=1||run.RouteKey!=route.Key)throw new InvalidDataException("Invalid run identity; original file preserved");run.Flags=run.Flags??new List<string>();run.Splits=run.Splits??new List<Split>();run.RulesKey=run.RulesKey??"";run.Note=run.Note??"";run.ReplayPath=run.ReplayPath??"";
                if(!ValidTimes(run.Elapsed)||run.Splits.Count>route.Areas.Count||run.CurrentArea<0||run.CurrentArea>Math.Max(0,route.Areas.Count-1))throw new InvalidDataException("Invalid run times or Areas; original file preserved");
                Times previous=new Times();for(int i=0;i<run.Splits.Count;i++){var split=run.Splits[i];if(split==null||split.AreaId!=route.Areas[i].Id||!ValidTimes(split.End)||!ValidTimes(split.Duration))throw new InvalidDataException("Invalid Area split; original file preserved");if(split.End.Game<previous.Game||split.End.Real<previous.Real||split.End.Active<previous.Active)run.Flag("Non-monotonic saved clock");previous=split.End;}
            }
        }
        private static bool ValidTimes(Times t){return t!=null&&new[]{t.Game,t.Real,t.Active}.All(v=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0);}
        internal static void SaveRun(Run run){int i=Archive.Runs.FindIndex(r=>r.Id==run.Id);var copy=run.Copy();if(i<0)Archive.Runs.Add(copy);else Archive.Runs[i]=copy;ArchiveRevision++;Queue(Path.Combine(Root,"Runs",Archive.Route.Key+".xml"),Archive);}
        internal static Layout Current(Surface surface,string map)
        {
            var selected=Settings.Layouts.FirstOrDefault(l=>l.Id==Settings.SelectedLayout);
            var candidates=Settings.Layouts.Where(l=>(l.Map==""||l.Map==map)&&(l.Aspect=="Any"||l.Aspect==surface.Aspect)).ToArray();
            if(selected!=null&&candidates.Contains(selected)&&!candidates.Any(l=>(l.Map==map?2:0)+(l.Aspect==surface.Aspect?1:0)>(selected.Map==map?2:0)+(selected.Aspect==surface.Aspect?1:0)))return selected;
            return candidates.OrderByDescending(l=>(l.Map==map?2:0)+(l.Aspect==surface.Aspect?1:0)).FirstOrDefault()??selected??Settings.Layouts[0];
        }
        internal static void Validate(SettingsData s)
        {
            if(s==null||s.Version!=1)throw new InvalidDataException("Unsupported Overlay+ layout version");
            if(s.Layouts==null||s.Layouts.Count==0)s.Layouts=new List<Layout>{Defaults.Preset("Minimal")};
            if(s.Layouts.Count>128)throw new InvalidDataException("Too many layouts");
            s.Templates=s.Templates??new List<Widget>();s.GroupTemplates=s.GroupTemplates??new List<Layout>();s.UiScale=Finite(s.UiScale,.65f,1.15f,1);s.GridSize=Math.Max(1,Math.Min(64,s.GridSize));s.Comparison=s.Comparison??"PB";s.Category=s.Category??"Default";
            foreach(var t in s.GroupTemplates)ValidateLayout(t);foreach(var t in s.Templates)ValidateLayout(new Layout{Widgets=new List<Widget>{t}});
            var ids=new HashSet<string>();foreach(var l in s.Layouts){if(l==null)throw new InvalidDataException("Null layout");if(string.IsNullOrEmpty(l.Id)||!ids.Add(l.Id))throw new InvalidDataException("Duplicate layout ID");ValidateLayout(l);}
        }
        internal static void ValidateLayout(Layout l)
        {
            l.Widgets=l.Widgets??new List<Widget>();if(l.Widgets.Count>256)throw new InvalidDataException("A layout may contain at most 256 widgets");
            var ids=new HashSet<string>();foreach(var w in l.Widgets){if(w==null||string.IsNullOrEmpty(w.Id)||!ids.Add(w.Id))throw new InvalidDataException("Duplicate widget ID");w.Width=Finite(w.Width,16,4000,220);w.Height=Finite(w.Height,16,4000,60);w.X=Finite(w.X,-10000,10000,24);w.Y=Finite(w.Y,-10000,10000,90);w.AnchorX=Finite(w.AnchorX,0,1,0);w.AnchorY=Finite(w.AnchorY,0,1,0);w.Opacity=Finite(w.Opacity,0,1,1);w.TextSize=Finite(w.TextSize,8,160,22);w.Padding=Finite(w.Padding,0,64,8);w.Border=Finite(w.Border,0,16,1);w.MinimumPress=Finite(w.MinimumPress,0,1,0);w.Rows=Math.Max(1,Math.Min(40,w.Rows));w.Keys=w.Keys??Defaults.Keys();if(w.Keys.Count>64)throw new InvalidDataException("Too many input keys");ValidateKeys(w);w.Text=w.Text??"";if(w.Text.Length>16000)throw new InvalidDataException("Text is too long");}
        }
        private static float Finite(float v,float min,float max,float fallback){return float.IsNaN(v)||float.IsInfinity(v)?fallback:Math.Max(min,Math.Min(max,v));}
        internal static void ValidateKeys(Widget w){w.ControlSizes=w.ControlSizes??new List<ControlSize>();foreach(var size in w.ControlSizes){if(size==null)throw new InvalidDataException("Null controls preset size");size.Width=Finite(size.Width,16,4000,356);size.Height=Finite(size.Height,16,4000,80);}foreach(var k in w.Keys){if(k==null)throw new InvalidDataException("Null input key");k.X=Finite(k.X,-4000,4000,0);k.Y=Finite(k.Y,-4000,4000,0);k.Width=Finite(k.Width,16,4000,56);k.Height=Finite(k.Height,16,4000,48);k.Source=k.Source??"Jump";k.Label=k.Label??"";}}
        internal static string[] Files(string folder,string pattern){string path=Path.Combine(Root,folder);Directory.CreateDirectory(path);return Directory.GetFiles(path,pattern,SearchOption.TopDirectoryOnly).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();}
        internal static void Export(Layout l){Write(Path.Combine(Root,"Exports","Layout.xml"),XmlData.Serialize(l));}
        internal static Layout Import(string path=null){var l=Load<Layout>(path??Path.Combine(Root,"Imports","Layout.xml"));if(l==null)throw new InvalidDataException("No readable layout selected");ValidateLayout(l);l.Id=Guid.NewGuid().ToString("N");l.Name+=" (imported)";return l;}
        internal static void ExportRuns(){Directory.CreateDirectory(Path.Combine(Root,"Exports"));var csv=new StringBuilder("Run,Started,Status,Category,Practice,Area,GameTime,RealTime,ActiveTime\r\n");foreach(var r in Archive.Runs)foreach(var s in r.Splits)csv.AppendLine(string.Join(",",new[]{r.Id,r.StartedUtc,r.Status,r.Category,r.Practice.ToString(),s.AreaId,s.End.Game.ToString(System.Globalization.CultureInfo.InvariantCulture),s.End.Real.ToString(System.Globalization.CultureInfo.InvariantCulture),s.End.Active.ToString(System.Globalization.CultureInfo.InvariantCulture)}.Select(x=>"\""+x.Replace("\"","\"\"")+"\"")));File.WriteAllText(Path.Combine(Root,"Exports","Runs.csv"),csv.ToString(),Encoding.UTF8);Write(Path.Combine(Root,"Exports","Runs.xml"),XmlData.Serialize(Archive));}
    }
}
