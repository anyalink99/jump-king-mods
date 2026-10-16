using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Serialization;
using EntityComponent;

namespace JKRuntime
{
    internal static class PerformanceDiagnosticsTests
    {
        private sealed class Counter : Component
        {
            internal int Updates, Late;
            internal bool Slow;
            protected override void Update(float delta) { Updates++; if (Slow) Thread.Sleep(2); }
            protected override void LateUpdate(float delta) { Late++; }
        }
        private sealed class Subject : Entity { internal Subject() : base(false) { } }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Dispatch(Entity entity) { entity.UpdateComponents(1f/60); }
        private static bool SkipSwitch(ref BehaviorTree.BTresult __result) { __result=BehaviorTree.BTresult.Success; return false; }
        private static bool HasDiagnosticHooks(Assembly engine)
        {
            Type harmony = engine.GetType("HarmonyLib.Harmony");
            var all = (IEnumerable)harmony.GetMethod("GetAllPatchedMethods").Invoke(null, null);
            foreach(MethodBase method in all)
            {
                object info = harmony.GetMethod("GetPatchInfo").Invoke(null, new object[] { method });
                foreach(string name in new[] { "Prefixes", "Postfixes" })
                    foreach(object patch in (IEnumerable)info.GetType().GetField(name).GetValue(info))
                    {
                        string owner = (string)patch.GetType().GetField("owner").GetValue(patch);
                        if (owner == "jk-runtime.performance" || owner == "jk-runtime.startup-diagnostics") return true;
                    }
            }
            return false;
        }
        private static void Main(string[] args)
        {
            var engine = Assembly.LoadFrom(args[0]);
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostic-fixture"); Directory.CreateDirectory(directory);
            PerformanceDiagnostics.OutputDirectory = directory;
            var captures = new List<PerformanceDiagnostics.Capture>();
            var writer = PerformanceDiagnostics.Sink; PerformanceDiagnostics.Sink = captures.Add;
            var legacy = (UI.UIApiSettings)new XmlSerializer(typeof(UI.UIApiSettings)).Deserialize(new StringReader("<UIApiSettings />"));
            Check(!legacy.DiagnosticMode, "Existing settings leave profiling disabled");
            var entity = new Subject(); var counter = new Counter(); entity.AddComponents(counter);
            using(RuntimeApi.MeasurePerformance("fixture.disabled")) Dispatch(entity);
            Check(!HasDiagnosticHooks(engine) && captures.Count==0, "Disabled measurement installs no hooks and produces no capture");
            PerformanceDiagnostics.Configure(true);
            using(var switchFixture=new DiagnosticHooks("fixture.map-switch"))
            {
                var type=typeof(JumpKing.Workshop.Nodes.SetContentNode);
                var target=type.GetMethod("MyRun",BindingFlags.Instance|BindingFlags.NonPublic);
                switchFixture.Add(target,typeof(PerformanceDiagnosticsTests),"SkipSwitch",null);
                var node=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
                type.GetField("m_dir",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(node,"fixture/map");
                type.GetField("m_title",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(node,"Fixture\nMap");
                object result=target.Invoke(node,new object[]{default(BehaviorTree.TickData)});
                Check((BehaviorTree.BTresult)result==BehaviorTree.BTresult.Success,"Loading diagnostics preserve a skipped native transition result");
            }
            using(RuntimeApi.MeasurePerformance("fixture.work")) for(int i=0;i<30;i++) Dispatch(entity);
            var unfinished = RuntimeApi.MeasurePerformance("fixture.old-window");
            PerformanceDiagnostics.ExportWindow(); unfinished.Dispose();
            Check(captures.Count==1 && counter.Updates==31 && counter.Late==31, "Profiling preserves native update and late-update counts");
            Check(captures[0].Metrics.Any(m=>m.Name.EndsWith("Counter.LowUpdate") && m.Count==30), "Actual component costs are attributed by type");
            Check(captures[0].Metrics.Any(m=>m.Name=="fixture.work"), "Shared module substage is captured");
            Check(captures[0].Inventory.Contains("fixture/map; title=Fixture Map")
                && captures[0].Metrics.Any(m=>m.Name.EndsWith("SetContentNode.MyRun")),"Map identity and inclusive duration are captured without a native load or multiline injection");
            Dispatch(entity); PerformanceDiagnostics.Configure(false);
            Check(!HasDiagnosticHooks(engine), "Disabling removes every owned measurement hook");
            Check(!captures[1].Metrics.Any(m=>m.Name=="fixture.old-window"), "An active scope cannot contaminate a replacement window");
            int count = captures.Count; Dispatch(entity); PerformanceDiagnostics.Configure(false);
            Check(captures.Count==count && counter.Updates==33, "Repeated disable and disabled dispatch are inert");
            PerformanceDiagnostics.Configure(true);
            for(int i=0;i<300;i++) using(RuntimeApi.MeasurePerformance("fixture."+i)) { }
            PerformanceDiagnostics.Configure(false);
            Check(captures.Last().Metrics.Length==256 && captures.Last().Dropped==44, "Named-stage growth is bounded and overflow is reported");
            new EntityManager();
            var propType=typeof(JumpKing.Game1).Assembly.GetType("JumpKing.Props.LoopingProp",true);
            var sprites=Enumerable.Range(0,4).Select(i=>JumpKing.Sprite.CreateSprite(null,new Microsoft.Xna.Framework.Rectangle(0,0,1,1))).ToArray();
            var prop=(Entity)Activator.CreateInstance(propType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
                new object[]{sprites,.1f,Microsoft.Xna.Framework.Point.Zero,1,false,false,null},null);
            PerformanceDiagnostics.Configure(true); prop.Draw();
            for(int i=0;i<12;i++) { prop.UpdateComponents(1f/60f); for(int j=0;j<4;j++) prop.Draw(); }
            string animation=AnimationDiagnostics.Capture();
            Check(animation.Contains("updates=12 supplied=0.200s") && animation.Contains("draws=49") && animation.Contains("duringDraw=0"),
                "Animation audit distinguishes native elapsed time from repeated presentation without mutating the prop");
            PerformanceDiagnostics.Configure(false); prop.Destroy();
            PerformanceDiagnostics.Sink=writer;
            for(int i=0;i<5;i++)
            { PerformanceDiagnostics.Configure(true); Dispatch(entity); PerformanceDiagnostics.Configure(false); PerformanceDiagnostics.Flush(); }
            Check(Directory.GetFiles(directory,"JKRuntime.Performance.*.txt").Length==4, "Report rotation bounds retained output");
            Check(Directory.GetFiles(directory,"*.tmp").Length==0, "Reports commit atomically without abandoned temporary output");
            Check(File.ReadAllText(Path.Combine(directory,"JKRuntime.Performance.1.txt")).Contains("Includes cold frames"), "Report states timing and cold-start limitations");
            bool parent = false, value = false;
            var setting = new Settings.Setting<bool>("fixture.dependent", "Dependent", ()=>value, v=>value=v);
            var option = new UI.SettingToggle(setting, null, ()=>parent);
            var canChange = typeof(UI.SettingToggle).GetMethod("CanChange", BindingFlags.Instance|BindingFlags.NonPublic);
            Check(!(bool)canChange.Invoke(option,null), "Dependent native toggle rejects input while parent is disabled");
            parent=true; Check((bool)canChange.Invoke(option,null), "Dependency refreshes without rebuilding the menu");
            var startup = new List<StartupTrace.Snapshot>();
            StartupTrace.ConfigureForTest(startup.Add);
            StartupDiagnosticHooks.Configure(true);
            counter.Slow = true;
            StartupTrace.BeginAttempt(); Dispatch(entity); StartupTrace.Finish("fixture");
            StartupTrace.SetDiagnosticMode(false);
            Check(startup.Count==1 && startup[0].Entries.Length>0, "Live startup hook group observes named native work");
            Check(!HasDiagnosticHooks(engine), "Startup hooks are removed without disturbing pointer hooks");
            Console.WriteLine("[OK] Runtime diagnostic lifecycle, bounded capture/export and native dependency checks");
        }
    }
}
