using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OverlayPlus
{
    [Serializable] public sealed class Area
    {
        public string Id, Name;
        public int Start, End, Unlock;
        public bool Contains(int screen){return screen>=Start&&screen<=End;}
    }
    [Serializable] public sealed class Route
    {
        public string World, Revision, Name, Key;
        public Campaign Campaign;
        public List<Area> Areas=new List<Area>();
    }
    [Serializable] public sealed class Times
    {
        public double Game, Real, Active;
        public double Value(ClockMode mode){return mode==ClockMode.RealTime?Real:mode==ClockMode.ActiveTime?Active:Game;}
        public Times Copy(){return new Times{Game=Game,Real=Real,Active=Active};}
        public static Times Subtract(Times a,Times b){return new Times{Game=Math.Max(0,a.Game-b.Game),Real=Math.Max(0,a.Real-b.Real),Active=Math.Max(0,a.Active-b.Active)};}
    }
    [Serializable] public sealed class Split
    {
        public string AreaId;
        public Times End = new Times(), Duration = new Times();
        public bool Eligible;
    }
    [Serializable] public sealed class Run
    {
        public string Id=Guid.NewGuid().ToString("N"), RouteKey, Category="Default", NativeAttempt, StartedUtc=DateTime.UtcNow.ToString("o"), EndedUtc="", Status="Running", Note="", ReplayPath="";
        public string RulesKey="", Rules="Native";
        public int Version=1, CurrentArea, Jumps, Falls;
        public bool Practice, Resumed;
        public List<string> Flags=new List<string>();
        public List<Split> Splits=new List<Split>();
        public Times Elapsed=new Times();
        public double NativeLast;
        public void Flag(string reason){Practice=true;if(!Flags.Contains(reason))Flags.Add(reason);}
        internal Run Copy()
        {
            var result=(Run)MemberwiseClone();result.Flags=new List<string>(Flags);result.Elapsed=Elapsed.Copy();result.Splits=new List<Split>(Splits.Count);
            foreach(var s in Splits)result.Splits.Add(new Split{AreaId=s.AreaId,Eligible=s.Eligible,End=s.End.Copy(),Duration=s.Duration.Copy()});return result;
        }
    }
    [Serializable] public sealed class RunArchive
    {
        public int Version=1;
        public Route Route;
        public List<Run> Runs=new List<Run>();
        internal RunArchive Copy()
        {
            var result=new RunArchive{Version=Version,Route=Route==null?null:new Route{World=Route.World,Revision=Route.Revision,Name=Route.Name,Key=Route.Key,Campaign=Route.Campaign}};
            if(Route!=null)foreach(var a in Route.Areas)result.Route.Areas.Add(new Area{Id=a.Id,Name=a.Name,Start=a.Start,End=a.End,Unlock=a.Unlock});
            result.Runs=new List<Run>(Runs.Count);foreach(var run in Runs)result.Runs.Add(run.Copy());return result;
        }
    }
    internal static class AreaRoutes
    {
        private static readonly string[] Main={"LOCATION_REDCROWN_WOODS","LOCATION_COLOSSAL_DRAIN","LOCATION_FALSE_KINGS_KEEP","LOCATION_BARGAINBURG","LOCATION_GREAT_FRONTIER","LOCATION_WINDSWEPT_BLUFF","LOCATION_STORMWALL_PASS","LOCATION_CHAPEL_PERILOUS","LOCATION_BLUE_RUIN","LOCATION_THE_TOWER"};
        private static readonly string[] Plus={"LOCATION_BRIGHTCROWN_WOODS","LOCATION_COLOSSAL_DUNGEON","LOCATION_FALSE_KINGS_CASTLE","LOCATION_UNDERBURG","LOCATION_LOST_FRONTIER","LOCATION_HIDDEN_KINGDOM","LOCATION_BLACK_SANCTUM","LOCATION_DEEP_RUIN","LOCATION_THE_DARK_TOWER"};
        private static readonly string[] Ghost={"LOCATION_PHILOSOPHERS_FOREST","LOCATION_BOG","LOCATION_MOULDING_MANOR","LOCATION_BUGSTALK","LOCATION_HOUSE_OF_NINE_LIVES","LOCATION_THE_PHANTOM_TOWER","LOCATION_HALTED_RUIN","LOCATION_THE_TOWER_OF_ANTUMBRA"};
        internal static Route Create(IEnumerable<Area> areas,string world,string revision,string name,Campaign campaign)
        {
            var source=areas.ToList();var route=new Route {World=world,Revision=revision,Name=name,Campaign=campaign};
            if(campaign==Campaign.Custom)route.Areas=source;
            else {string[] ids=campaign==Campaign.MainBabe?Main:campaign==Campaign.NewBabePlus?Plus:Ghost;foreach(string id in ids){var area=source.FirstOrDefault(a=>a.Id==id);if(area==null)throw new InvalidOperationException("Missing native Area: "+id);route.Areas.Add(area);} }
            var seen=new HashSet<string>();foreach(var a in route.Areas){if(a==null||string.IsNullOrWhiteSpace(a.Id)||!seen.Add(a.Id)||a.Start<1||a.End<a.Start||a.Unlock<a.Start||a.Unlock>a.End)throw new InvalidOperationException("Invalid or ambiguous native Area definition");}
            route.Key=Hash(world+"|"+revision+"|"+campaign+"|"+string.Join(";",route.Areas.Select(a=>a.Id+":"+a.Start+":"+a.End+":"+a.Unlock)));
            return route;
        }
        internal static string Hash(string text){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        internal static int Find(Route route,int screen,int current)
        {
            // Preserve authored order, including the GoTB 157 -> 102 transition.
            // Unlock disambiguates overlapping Areas without advancing on a fall.
            for(int i=Math.Max(0,current+1);i<route.Areas.Count;i++)if(route.Areas[i].Contains(screen)&&screen>=route.Areas[i].Unlock)return i;
            if(current>=0&&current<route.Areas.Count&&route.Areas[current].Contains(screen))return current;
            for(int i=0;i<route.Areas.Count;i++)if(route.Areas[i].Contains(screen))return i;
            return -1;
        }
    }
    internal sealed class RunTracker
    {
        internal readonly Route Route;
        internal readonly Run Run;
        private double lastWall;
        internal RunTracker(Route route,Run run,double now){Route=route;Run=run;lastWall=now;}
        internal bool Advance(double native,double now,bool paused,int screen,bool onGround)
        {
            if(Run.Status!="Running")return false;
            double wall=Math.Max(0,now-lastWall);lastWall=now;
            Run.Elapsed.Real+=wall;if(!paused)Run.Elapsed.Active+=wall;
            if(native+0.00001<Run.NativeLast)Run.Flag("Clock restored");
            Run.NativeLast=native;Run.Elapsed.Game=native;
            if(paused||!onGround||Route.Areas.Count==0)return false;
            int area=AreaRoutes.Find(Route,screen,Run.CurrentArea);
            if(area<=Run.CurrentArea)return false;
            if(area!=Run.CurrentArea+1)Run.Flag("Area skipped");
            while(Run.CurrentArea<area){CompleteArea(area==Run.CurrentArea+1&&!Run.Practice);Run.CurrentArea++;}
            return true;
        }
        private void CompleteArea(bool eligible)
        {
            if(Run.CurrentArea<0||Run.CurrentArea>=Route.Areas.Count)return;
            var start=Run.Splits.Count==0?new Times():Run.Splits[Run.Splits.Count-1].End;
            Run.Splits.Add(new Split { AreaId=Route.Areas[Run.CurrentArea].Id,End=Run.Elapsed.Copy(),Duration=Times.Subtract(Run.Elapsed,start),Eligible=eligible&&!Run.Practice });
        }
        internal void Finish(bool victory)
        {
            if(Run.Status!="Running")return;
            if(victory){if(Run.CurrentArea!=Route.Areas.Count-1)Run.Flag("Incomplete Area route");if(Route.Areas.Count>0)CompleteArea(!Run.Practice);Run.Status="Finished";}else Run.Status="Interrupted";
            Run.EndedUtc=DateTime.UtcNow.ToString("o");
        }
    }
    internal static class Records
    {
        internal static IEnumerable<Run> Category(RunArchive archive,string category,string rules=""){return archive.Runs.Where(r=>r.RouteKey==archive.Route.Key&&r.Category==category&&(r.RulesKey??"")==rules&&!r.Practice);}
        internal static Run Best(RunArchive archive,string category,ClockMode clock,string rules=""){return Category(archive,category,rules).Where(r=>r.Status=="Finished"&&r.Splits.Count==archive.Route.Areas.Count&&r.Splits.All(s=>s.Eligible)).OrderBy(r=>r.Elapsed.Value(clock)).FirstOrDefault();}
        internal static double? Gold(RunArchive archive,string category,string area,ClockMode clock,string rules=""){var values=Category(archive,category,rules).SelectMany(r=>r.Splits).Where(s=>s.AreaId==area&&s.Eligible).Select(s=>s.Duration.Value(clock)).ToArray();return values.Length==0?(double?)null:values.Min();}
        internal static double? Sum(RunArchive archive,string category,ClockMode clock,string rules=""){double sum=0;foreach(var a in archive.Route.Areas){var v=Gold(archive,category,a.Id,clock,rules);if(!v.HasValue)return null;sum+=v.Value;}return archive.Route.Areas.Count==0?(double?)null:sum;}
        internal static string Format(double? time,bool precise=true){if(!time.HasValue||double.IsNaN(time.Value)||double.IsInfinity(time.Value))return "--";double t=Math.Max(0,time.Value);int h=(int)(t/3600),m=(int)(t/60)%60,s=(int)t%60,cs=(int)(t*100)%100;return (h>0?h.ToString()+":"+m.ToString("00"):m.ToString())+":"+s.ToString("00")+(precise?"."+cs.ToString("00"):"");}
        internal static string Delta(double value){return (value>=0?"+":"-")+Format(Math.Abs(value));}
    }
}
