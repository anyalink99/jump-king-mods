using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.MiscSystems.Achievements;
using JumpKing.Player;
using JKRuntime;
using JKRuntime.Gameplay;
using JKRuntime.Modules;
using JKRuntime.State;
using JKRuntime.UI;
using Steamworks;

namespace RunVerifier
{
    internal sealed class AreaDefinition { internal int start, end; internal string id, name; }
    internal sealed class Prepared
    { internal string mapId, mapName, revision,replayMapId,replayRevision; internal List<EnvironmentEntry> environment; internal List<AreaDefinition> areas; }
    [RuntimeModule("run-verifier", "Run Verifier", Requires = new[] { "replays.verification:1:0:optional" })]
    public static class ModEntry
    {
        internal static Repository Repository;
        internal static Client Client;
        internal static RunRecord Current, Last;
        private static Prepared prepared;
        private static ComponentAttachment attachment;
        private static Observer observer;
        private static ResultsSeal seal;
        private static Type replayBridge;
        private static int wins;
        internal static string Status = "";
        private static readonly FieldInfo ScreenField = typeof(JumpGame).GetField("m_stats_screen",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool ResultsVisible { get { var game=JumpGame.instance; var s=game==null?null:ScreenField.GetValue(game) as StatsScreen; return s!=null && s.IsRunning(); } }
        internal static void Initialize()
        {
            if(Repository!=null) return;
            string root=Path.Combine(Path.GetDirectoryName(typeof(Game1).Assembly.Location),"Content","RunVerifier");
            Repository=new Repository(root); Client=new Client(Repository);
            AppDomain.CurrentDomain.ProcessExit+=delegate { try { Repository.Flush(); } catch { } };
        }
        [BeforeLevelLoad]
        public static void Load()
        {
            Initialize();
        }
        [MainMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton MainHistory(object factory,JumpKing.PauseMenu.GuiFormat format)
        { return new JumpKing.PauseMenu.BT.TextButton("Run history",UIApi.CreateMenuPage(factory,new HistoryPage(delegate { UIApi.ContinueFromMainMenu(factory); }))); }
        [PauseMenuItemSetting]
        public static JumpKing.PauseMenu.BT.TextButton PauseHistory(object factory,JumpKing.PauseMenu.GuiFormat format)
        { return new JumpKing.PauseMenu.BT.TextButton("Run history",UIApi.CreateMenuPage(factory,new HistoryPage(UIApi.ClosePauseMenu))); }
        [BeforeAttempt]
        public static void Prepare(RuntimeScope scope)
        {
            Initialize();
            using(RuntimeApi.MeasureStartup("run-verifier.manifest"))
            {
                var level=Game1.instance.contentManager.level;
                string root=Game1.instance.contentManager.root;
                if(!Path.IsPathRooted(root))root=Path.Combine(Path.GetDirectoryName(typeof(Game1).Assembly.Location),root);
                root=Path.GetFullPath(root);
                var files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(p=>
                    (p.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)||p.EndsWith("level.xnb",StringComparison.OrdinalIgnoreCase)||p.EndsWith("slopes.xnb",StringComparison.OrdinalIgnoreCase))
                    && !p.Contains(Path.DirectorySeparatorChar+"Saves"+Path.DirectorySeparatorChar)
                    && !p.Contains(Path.DirectorySeparatorChar+"RunVerifier"+Path.DirectorySeparatorChar)
                    && !p.Contains(Path.DirectorySeparatorChar+"Replays"+Path.DirectorySeparatorChar)
                    && !new[] {"JKMods","SavesPerma","OverlayPlus","WardrobePlus","ControllerBinds"}.Any(d=>p.Contains(Path.DirectorySeparatorChar+d+Path.DirectorySeparatorChar))
                    && !p.EndsWith(".Settings.xml",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                var env=new List<EnvironmentEntry>();
                foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a=>a.FullName,StringComparer.Ordinal))
                {
                    if(assembly.IsDynamic) continue; string p; try { p=assembly.Location; } catch { continue; }
                    if(string.IsNullOrEmpty(p)||p.IndexOf("Microsoft.NET",StringComparison.OrdinalIgnoreCase)>=0||p.IndexOf("Windows\\assembly",StringComparison.OrdinalIgnoreCase)>=0) continue;
                    try { env.Add(new EnvironmentEntry { id=assembly.GetName().Name,version=assembly.GetName().Version.ToString(),hash=Json.FileHash(p) }); } catch { }
                }
                string manifest=string.Join("\n",files.Select(p=>p.Substring(root.Length).Replace('\\','/')+":"+Json.FileHash(p)));
                prepared=new Prepared { mapId=level!=null && level.ID!=0 ? "workshop:"+level.ID : "local:"+(level==null?"Jump King":level.Name),
                    mapName=level==null||string.IsNullOrEmpty(level.Name)?"Jump King":level.Name,revision=Json.Hash(manifest),environment=env,areas=ReadAreas() };
                string author=level==null||string.IsNullOrWhiteSpace(level.Author)?"Nexile":level.Author.Trim();
                prepared.replayMapId=level!=null&&level.ID!=0?"workshop:"+level.ID:"level:"+prepared.mapName.Trim().ToLowerInvariant()+"|"+author.ToLowerInvariant()+"|"+JumpKing.Level.LevelManager.TotalScreens;
                prepared.replayRevision=ReplayRevision(root);
                replayBridge=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Replays.VerificationBridge")).FirstOrDefault(t=>t!=null);
                if(replayBridge!=null)
                {
                    var e=replayBridge.GetEvent("Saved"); var handler=new Action<string,string,string,double,double>(ReplaySaved);
                    e.RemoveEventHandler(null,handler); e.AddEventHandler(null,handler);
                }
            }
            scope.Defer(delegate { prepared=null; });
        }
        private static List<AreaDefinition> ReadAreas()
        {
            var result=new List<AreaDefinition>();
            try
            {
                var type=typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.LocationText.LocationTextManager");
                var settings=type.GetProperty("SETTINGS",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).GetValue(null,null);
                var list=(Array)settings.GetType().GetField("locations").GetValue(settings); int i=0;
                foreach(var a in list) { var t=a.GetType(); result.Add(new AreaDefinition { id="area:"+i++,name=EnglishArea((string)t.GetField("name").GetValue(a)),start=(int)t.GetField("start").GetValue(a),end=(int)t.GetField("end").GetValue(a) }); }
            } catch(Exception e) { Status="Area definitions unavailable: "+e.Message; }
            return result;
        }
        private static string EnglishArea(string key)
        {
            if(string.IsNullOrEmpty(key))return "Unnamed area";
            try { return LanguageJK.language.ResourceManager.GetString(key,System.Globalization.CultureInfo.GetCultureInfo("en"))??key; }
            catch { return key; }
        }
        private static string ReplayRevision(string root)
        {
            using(var aggregate=new MemoryStream())using(var sha=System.Security.Cryptography.SHA256.Create())
            {
                foreach(string name in new[] {"level.xnb","slopes.xnb"})
                {
                    string path=Path.Combine(root,name);if(!File.Exists(path))continue;
                    byte[] label=System.Text.Encoding.UTF8.GetBytes(name+"\0");aggregate.Write(label,0,label.Length);
                    using(var file=File.OpenRead(path)){byte[] digest=sha.ComputeHash(file);aggregate.Write(digest,0,digest.Length);}
                }
                return Json.Hex(sha.ComputeHash(aggregate.ToArray()));
            }
        }
        internal static bool Playback { get { return replayBridge!=null && (bool)replayBridge.GetProperty("IsPlayback").GetValue(null,null); } }
        [OnLevelStart]
        public static void Start(ModuleContext context)
        {
            object bridge;replayBridge=context.TryGetCapability("replays.verification",out bridge)?bridge as Type:null;
            if(prepared==null || Playback) return;
            if(seal!=null && seal.IsAlive) seal.Destroy(); seal=new ResultsSeal();
            wins=GameClock.ReadWins();
            var evidence=RunModifiers.GetEvidence(); var clock=GameClock.ReadCurrent(); double time=clock.Time+clock.Ticks*Game1.instance.TargetElapsedTime.TotalSeconds;
            string steam="0"; try { steam=SteamUser.GetSteamID().m_SteamID.ToString(); } catch { }
            string continuity=Json.Hash(steam+"|"+prepared.mapId+"|"+(evidence==null?Guid.NewGuid().ToString():evidence.RunKey));
            Current=Repository.Resume(continuity,prepared.revision,time);
            if(Current==null)
            {
                Current=new RunRecord { continuity=continuity,steamId=steam,mapId=prepared.mapId,mapName=prepared.mapName,revision=prepared.revision,
                    gameVersion=typeof(Game1).Assembly.GetName().Version.ToString(),runtimeVersion=RuntimeApi.Version,environment=prepared.environment,time=time,replayMapId=prepared.replayMapId,replayRevision=prepared.replayRevision };
                if(time>1) Current.Reason("Observation started after the beginning of the attempt");
            }
            else
            {
                if(time>Current.time+0.1) Current.Reason("Unobserved interval since the last saved checkpoint");
                foreach(var old in Current.sessions.Where(s=>s.ended==null)) { old.ended=DateTime.UtcNow.ToString("o");old.end=Current.time;Current.Reason("Previous session did not close cleanly"); }
                if(Json.Hash(Json.Write(Current.environment))!=Json.Hash(Json.Write(prepared.environment))) Current.Reason("Loaded environment changed between sessions");
            }
            Current.time=time;
            CaptureTickDuration(Current,Game1.instance.TargetElapsedTime);
            var session=new Session { id=Guid.NewGuid().ToString("N"),started=DateTime.UtcNow.ToString("o"),start=time };
            Current.sessions.Add(session);
            if(replayBridge!=null) replayBridge.GetMethod("BindRun").Invoke(null,new object[] { Current.id+":"+session.id });
            observer=new Observer(Current,prepared.areas); attachment=new ComponentAttachment(GameLoop.m_player,observer);
            Client.Begin(Current); Repository.Save(Current);
        }
        [OnLevelEnd]
        public static void End()
        {
            if(Current==null) return;
            bool playback=Playback;
            if(!playback && GameClock.ReadWins()>wins)
            {
                var manager=typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager",true);
                object instance=manager.GetField("instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null);
                PlayerStats stats=(PlayerStats)manager.GetField("m_win_stats",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);
                Current.ticks=stats._ticks; Current.persistedSeconds=stats.time; Current.time=stats.timeSpan.TotalSeconds;
                Current.jumps=stats.jumps; Current.nativeFalls=stats.falls;
                Current.ending=JumpKing.GameManager.MultiEnding.GameEnding.GetEnding().ToString();
                Current.complete=true; Current.finished=DateTime.UtcNow.ToString("o");
                CaptureEvidence(Current); Last=Current;
            }
            CloseSession(playback);
        }
        [OnLevelUnload]
        public static void Unload() { if(Current!=null) CloseSession(Playback); }
        private static void CloseSession(bool playback)
        {
            if(attachment!=null) { attachment.Dispose(); attachment=null; }
            observer=null;
            if(!playback)
            {
                var session=Current.sessions.Last(); session.ended=DateTime.UtcNow.ToString("o"); session.end=Current.time;
                Current.wall=Current.sessions.Sum(s=>(DateTime.Parse(s.ended??DateTime.UtcNow.ToString("o"))-DateTime.Parse(s.started)).TotalSeconds);
                // Wall time without advancing game time includes pause menus and focus loss.
                Current.paused=Math.Max(0,Current.wall-Current.sessions.Sum(s=>Math.Max(0,s.end-s.start)));
                Repository.Save(Current); Client.End(Current);
            }
            Current=null;
        }
        internal static void CaptureEvidence(RunRecord record)
        {
            var e=RunModifiers.GetEvidence();
            if(e==null) { record.unknown=true; record.Reason("Modifier attribution unavailable"); return; }
            record.nativePeak=(int)e.NativePeak; record.unknown=e.Unknown;
            record.sources=e.Sources.Concat(e.InheritedSources).Select(s=>s.Id+": "+s.Name).Distinct().OrderBy(s=>s).ToList();
            record.category=e.NativePeak>0?"modified":e.Unknown?"unknown":"no-known-modifiers";
            foreach(var reason in e.UnknownReasons) if(!record.reasons.Contains(reason)) record.reasons.Add(reason);
            record.mechanics=RuntimeApi.Mechanics.Inspect().Select(m=>m.Id+" | "+m.Version+" | "+(m.State==null?"unknown":m.State.Enabled?"enabled":"disabled")+" | "+m.Effects).ToList();
        }
        internal static void CaptureTickDuration(RunRecord record,TimeSpan duration)
        {
            // Game1.MyInit uses a float 1/60 converted by .NET Framework to whole milliseconds.
            // Its native interval is 17 ms, not an exact mathematical 1/60 second.
            record.tickSeconds=duration.TotalSeconds;
            if(duration!=TimeSpan.FromSeconds(1f/60f))record.Reason("Nonstandard game tick duration");
        }
        private static void ReplaySaved(string binding,string id,string path,double start,double end)
        {
            string[] parts=binding.Split(':'); if(parts.Length!=2) return;
            var r=Repository.Get(parts[0]); if(r==null) return;
            if(!r.attachments.Any(a=>a.id==id)) r.attachments.Add(new Attachment { id=id,sessionId=parts[1],path=path,start=start,end=end,size=new FileInfo(path).Length,hash=Json.FileHash(path) });
            Repository.Save(r);
            if(r.complete && r.certificate!=null) Client.Publish(r);
        }
        internal static bool Watch(string id) { return replayBridge!=null && (bool)replayBridge.GetMethod("Watch").Invoke(null,new object[] { id }); }
        internal sealed class Observer : Component
        {
            private readonly RunRecord record; private readonly List<AreaDefinition> areas; private readonly Telemetry telemetry;
            private double previous, checkpoint, evidenceAt;
            internal Observer(RunRecord r,List<AreaDefinition> a) { record=r; areas=a; telemetry=new Telemetry(r); previous=r.time; }
            protected override void LateUpdate(float delta)
            {
                if(Playback) { record.Reason("Replay playback interrupted observation"); return; }
                CaptureTickDuration(record,Game1.instance.TargetElapsedTime);
                var clock=GameClock.ReadCurrent(); double time=clock.Time+clock.Ticks*record.tickSeconds;
                if(time<previous-0.05) { record.Reason("Game clock moved backwards; recording stopped until the next attempt"); CloseSession(false); return; }
                double dt=time-previous; previous=time; record.time=time; record.ticks=clock.Ticks; record.persistedSeconds=clock.Time;
                if(dt<=0) return;
                var body=GameLoop.m_player.m_body; int screen=Math.Max(1,Math.Min(JumpKing.Level.LevelManager.TotalScreens,1-(int)Math.Floor(body.Position.Y/360)));
                var area=areas.FirstOrDefault(a=>screen>=a.start && screen<=a.end);
                telemetry.Sample(dt,body.Position.Y,screen,body.IsOnGround,area==null?"unassigned":area.id,area==null?"Unassigned":area.name,!body.Enabled);
                checkpoint+=dt; evidenceAt+=dt;
                Client.Observe(record);
                if(evidenceAt>=5) { CaptureEvidence(record); evidenceAt=0; }
                if(checkpoint>=15) { Repository.Save(record); checkpoint=0; }
            }
        }
    }
}
