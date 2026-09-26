using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing;
using JumpKing.MiscSystems.Achievements;
using RunVerifier;
internal static class RunVerifierTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Main()
    {
        var data=SealCodec.Payload("00112233445566778899aabbccddeeff",76561198000000000,1234567,3);
        var bits=SealCodec.Encode(data);Check(bits.Length==592,"Sector capacity");
        Check(SealCodec.Decode(bits).SequenceEqual(data),"Seal roundtrip");
        for(int i=32;i<bits.Length;i+=7){var copy=(bool[])bits.Clone();copy[i]=!copy[i];Check(SealCodec.Decode(copy).SequenceEqual(data),"Single-bit correction");}
        var corrupt=(bool[])bits.Clone();corrupt[32]=!corrupt[32];corrupt[33]=!corrupt[33];bool rejected=false;try{SealCodec.Decode(corrupt);}catch(InvalidDataException){rejected=true;}Check(rejected,"Multi-bit corruption rejected");
        var sectors=RoundSeal.Positions;Check(sectors.SelectMany(v=>v).Select(v=>v[0]+":"+v[1]).Distinct().Count()==1776,"Independent round sectors");
        var raster=RoundSeal.Raster(data);
        foreach(var sector in sectors)Check(SealCodec.Decode(sector.Select(c=>raster[c[0],c[1]]).ToArray()).SequenceEqual(data),"Each round sector independently decodes");
        var signature=RoundSeal.Signature(76561198000000000);var other=RoundSeal.Signature(76561198000000001);
        var signatureBytes=new byte[RoundSeal.Size*RoundSeal.Size];for(int y=0;y<RoundSeal.Size;y++)for(int x=0;x<RoundSeal.Size;x++)signatureBytes[y*RoundSeal.Size+x]=signature[x,y]?(byte)1:(byte)0;
        using(var sha=System.Security.Cryptography.SHA256.Create())Check(Json.Hex(sha.ComputeHash(signatureBytes))=="a91fa8260e76ba0de4f8e64158e8724bbe68408c77fedb031313dbb01cac3ad4","C#/TypeScript personal signature vector");
        Check(!signature.Cast<bool>().SequenceEqual(other.Cast<bool>()),"Steam users have different signatures");
        Check(signature.Cast<bool>().SequenceEqual(RoundSeal.Signature(76561198000000000).Cast<bool>()),"Steam signature is stable across runs");
        string root=Path.Combine(Path.GetTempPath(),"jk-verifier-"+Guid.NewGuid().ToString("N"));
        var repo=new Repository(root);var r=new RunRecord { continuity="a",revision="b",time=10 };
        repo.Save(r);repo.Flush();var restored=new Repository(root);Check(restored.Resume("a","b",10)!=null,"Continue resumes");Check(restored.Resume("a","b",5)==null,"Rollback does not merge");Check(restored.Resume("a","c",10)==null,"Map revision does not merge");
        r.complete=true;r.finished=DateTime.UtcNow.ToString("o");repo.Save(r);repo.Flush();Check(new Repository(root).List().Count==1,"Completed history persists");
        var telemetry=new Telemetry(new RunRecord());telemetry.Record.time=1;telemetry.Sample(.5,0,1,true,"a","A",false);telemetry.Record.time=2;telemetry.Sample(.5,50,1,false,"a","A",false);telemetry.Record.time=3;telemetry.Sample(.5,100,1,true,"b","B",false);
        Check(telemetry.Record.areas.Sum(a=>a.seconds)==telemetry.Record.observed,"Area totals reconcile");Check(telemetry.Record.falls.Count==1&&telemetry.Record.falls[0].height==100,"Fall height");
        File.WriteAllText(Path.Combine(root,"bad.run.json"),"broken");Check(new Repository(root).List().Count==1&&File.Exists(Path.Combine(root,"bad.run.json")),"Corruption preserves other history");
        ProfilePersistence(root);
        NativeTickDuration();
        NativeCompletion(root);
        OptionalReplayGraph();
        HistoryReopens();
        Console.WriteLine("[OK] Native TextButton: repeat open, held confirm, release, idle frames and Back");
        Console.WriteLine("[OK] Run Verifier codec, telemetry, persistence and continuity tests");
        Console.WriteLine("Seal vector: "+Convert.ToBase64String(data));
    }
    static void NativeTickDuration()
    {
        var native=TimeSpan.FromSeconds(1f/60f);
        Check(native.Ticks==170000,"Installed .NET Framework rounds Jump King's native target to 17 ms");
        var regular=new RunRecord();ModEntry.CaptureTickDuration(regular,native);
        Check(!regular.partial&&regular.reasons.Count==0&&Math.Abs(regular.tickSeconds-.017)<1e-12,"Native timing retains complete evidence");
        ModEntry.CaptureTickDuration(regular,TimeSpan.FromMilliseconds(20));
        Check(regular.partial&&regular.reasons.Contains("Nonstandard game tick duration"),"Changed timing still marks partial evidence");
        ModEntry.CaptureTickDuration(regular,native);
        Check(regular.partial,"Returning to native timing cannot erase a real interruption");
        var interrupted=new RunRecord();interrupted.Reason("Unobserved interval since the last saved checkpoint");
        ModEntry.CaptureTickDuration(interrupted,native);
        Check(interrupted.partial&&interrupted.reasons.Count==1,"Native timing does not clear unrelated evidence gaps");
        Console.WriteLine("[OK] Native 17 ms timing, changed interval detection and preserved evidence gaps");
    }
    static void ProfilePersistence(string root)
    {
        var repo=new Repository(Path.Combine(root,"profile"));var client=new Client(repo);
        Check(!client.Paired,"New installation starts without a profile");
        const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
        var state=(Connection)typeof(Client).GetField("connection",f).GetValue(client);
        state.token=new string('a',64);state.profileId=new string('b',32);state.online=true;
        typeof(Client).GetMethod("SaveConnection",f).Invoke(client,null);
        var restored=new Client(repo);Check(restored.Paired&&restored.Online,"Protected profile survives restart");
        string file=Path.Combine(repo.Root,"profile.json");Check(!File.ReadAllText(file).Contains(state.token),"Credential is encrypted on disk");
        state.pending=true;typeof(Client).GetMethod("SaveConnection",f).Invoke(client,null);
        restored=new Client(repo);Check(!restored.Paired,"Pending registration cannot upload");
        var restoredState=(Connection)typeof(Client).GetField("connection",f).GetValue(restored);
        Check(restoredState.token==state.token,"Registration retry preserves saved device secret");
        File.WriteAllText(file,"damaged profile");restored=new Client(repo);restored.ToggleOnline();
        Check(File.ReadAllText(file)=="damaged profile"&&!restored.Paired,"Unreadable credentials are preserved without creating a replacement profile");
        Console.WriteLine("[OK] DPAPI profile persistence, pending registration and unreadable credential preservation");
    }
    public sealed class FakeFactory
    {
        public void AddDrawable(object drawable) { }
    }
    private sealed class FakePad : JumpKing.Controller.IPad
    {
        internal int[] Pressed=new int[0];
        public int[] GetPressedButtons(){return Pressed;}
        public string ButtonToString(int button){return button.ToString();}
        public JumpKing.Controller.PadBinding GetDefaultBind(){return new JumpKing.Controller.PadBinding();}
        public string GetSaveIdentifier(){return "test";}public string GetPrintName(){return "Test";}public bool IsConnected(){return true;}
    }
    static void HistoryReopens()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var previous=JumpKing.Controller.ControllerManager.instance;var previousMenu=JumpKing.Controller.MenuController.instance;
        var manager=(JumpKing.Controller.ControllerManager)FormatterServices.GetUninitializedObject(typeof(JumpKing.Controller.ControllerManager));
        var pad=new FakePad();JumpKing.Controller.ControllerManager.instance=manager;
        var menu=new JumpKing.Controller.MenuController(manager);
        typeof(JumpKing.Controller.ControllerManager).GetField("_menu_controller",flags).SetValue(manager,menu);
        typeof(JumpKing.Controller.ControllerManager).GetField("m_pads",flags).SetValue(manager,new System.Collections.Generic.List<JumpKing.Controller.PadInstance>{new JumpKing.Controller.PadInstance(pad)});
        var state=typeof(JumpKing.Controller.MenuController).GetField("_menu_state",flags);
        var page=new HistoryPage(null);
        var node=JKRuntime.UI.UIApi.CreateMenuPage(new FakeFactory(),page);
        var button=new JumpKing.PauseMenu.BT.TextButton("Run history",node,(Microsoft.Xna.Framework.Graphics.SpriteFont)null,Microsoft.Xna.Framework.Color.White);
        try
        {
            int tick=1;
            for(int attempt=0;attempt<3;attempt++)
            {
                pad.Pressed=new[]{1};state.SetValue(menu,new JumpKing.Controller.PadState{confirm=true});
                Check(button.Run(new BehaviorTree.TickData(.016f,tick++))==BehaviorTree.BTresult.Running,"History opens on confirmation");
                pad.Pressed=new int[0];state.SetValue(menu,new JumpKing.Controller.PadState());
                Check(button.Run(new BehaviorTree.TickData(.016f,tick++))==BehaviorTree.BTresult.Running,"History survives confirmation release");
                for(int idle=0;idle<3;idle++)Check(button.Run(new BehaviorTree.TickData(.016f,tick++))==BehaviorTree.BTresult.Running&&!page.WantsClose,"Reopened history remains open on idle frames");
                state.SetValue(menu,new JumpKing.Controller.PadState{cancel=true});
                Check(button.Run(new BehaviorTree.TickData(.016f,tick++))==BehaviorTree.BTresult.Success,"History exits on Back");
                button.ResetResult();
            }
        }
        finally {page.OnClose();JumpKing.Controller.ControllerManager.instance=previous;JumpKing.Controller.MenuController.instance=previousMenu;}
    }
    public sealed class PlaybackBridge { public static bool IsPlayback {get{return true;}} }
    static void OptionalReplayGraph()
    {
        const BindingFlags f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var type=typeof(JKRuntime.RuntimeApi).Assembly.GetType("JKRuntime.RuntimeKernel");
        var metadata=(JKRuntime.Modules.RuntimeModuleAttribute)Attribute.GetCustomAttribute(typeof(ModEntry),typeof(JKRuntime.Modules.RuntimeModuleAttribute));
        foreach(bool present in new[] {false,true})foreach(bool providerFirst in new[] {false,true})
        {
            var host=Activator.CreateInstance(type,true);bool activated=false,provider=false;
            var requires=metadata.Requires.Select(s=>{var p=s.Split(':');return new JKRuntime.CapabilityRequirement(p[0],int.Parse(p[1]),int.Parse(p[2]),p.Length==4&&p[3]=="optional");}).ToArray();
            var consumer=new JKRuntime.ModuleDefinition(metadata.Id,new Version(1,0),delegate(JKRuntime.ModuleContext c){object service;Check(c.TryGetCapability("replays.verification",out service)==present,"Optional replay capability availability");Check(!present||provider,"Replay provider activates first");activated=true;},requires,after:metadata.After,before:metadata.Before);
            var source=new JKRuntime.ModuleDefinition("replays",new Version(2,2),delegate(JKRuntime.ModuleContext c){provider=true;c.Publish("replays.verification",typeof(PlaybackBridge));},provides:new[]{new JKRuntime.CapabilityDefinition("replays.verification",1,0)});
            if(present&&providerFirst)type.GetMethod("Register",f).Invoke(host,new object[]{source});
            type.GetMethod("Register",f).Invoke(host,new object[]{consumer});
            if(present&&!providerFirst)type.GetMethod("Register",f).Invoke(host,new object[]{source});
            Check((bool)type.GetMethod("Activate",f).Invoke(host,null)&&activated,"Verifier starts with or without optional Replays");
            type.GetMethod("Deactivate",f).Invoke(host,null);
        }
    }
    static void NativeCompletion(string root)
    {
        const BindingFlags f=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        var game=(Game1)FormatterServices.GetUninitializedObject(typeof(Game1));GC.SuppressFinalize(game);
        typeof(Game1).GetField("_instance",f).SetValue(null,game);
        typeof(Microsoft.Xna.Framework.Game).GetField("_targetElapsedTime",f).SetValue(game,TimeSpan.FromSeconds(1f/60f));
        var managerType=typeof(Game1).Assembly.GetType("JumpKing.MiscSystems.Achievements.AchievementManager");
        var manager=FormatterServices.GetUninitializedObject(managerType);managerType.GetField("instance",f).SetValue(null,manager);
        var repo=new Repository(Path.Combine(root,"native"));ModEntry.Repository=repo;ModEntry.Client=new Client(repo);
        Func<RunRecord> fresh=delegate {var r=new RunRecord();r.sessions.Add(new Session {id=Guid.NewGuid().ToString("N"),started=DateTime.UtcNow.ToString("o")});return r;};
        ModEntry.Current=fresh();ModEntry.End();repo.Flush();Check(repo.List().Count==0,"Exit without victory does not certify");
        managerType.GetField("m_all_time_stats",f).SetValue(manager,new PlayerStats {times_won=1});
        managerType.GetField("m_win_stats",f).SetValue(manager,new PlayerStats {_ticks=600,jumps=123,falls=7});
        ModEntry.Current=fresh();ModEntry.End();repo.Flush();var completed=repo.List().Single();
        Check(completed.complete&&completed.jumps==123&&completed.nativeFalls==7&&Math.Abs(completed.time-10.2)<.002,"Completion freezes installed native win statistics at the native 17 ms interval");
        ModEntry.Current=fresh();typeof(ModEntry).GetField("replayBridge",f).SetValue(null,typeof(PlaybackBridge));ModEntry.End();repo.Flush();
        Check(repo.List().Count==1,"Replay playback cannot publish victory");typeof(ModEntry).GetField("replayBridge",f).SetValue(null,null);
    }
}
