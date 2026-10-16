using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BehaviorTree;
using JKRuntime;
using JumpKing;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using MegaMappingExpansion.Api;
using JumpKing.GameManager;
using JumpKing.GameManager.MultiEnding;

namespace MegaMappingExpansion.Construction
{
    internal sealed class ResultPageSnapshot
    {
        internal string Title,Role;
        internal string[] Lines;
        internal SceneValue Color,Background;
    }
    internal sealed class ResultCursor
    {
        internal readonly ResultPageSnapshot[] Pages;
        internal int Page=-1;
        private double elapsed;
        internal ResultCursor(ResultPageSnapshot[] pages){Pages=pages;}
        internal bool Advance(double delta,bool confirmed)
        {
            elapsed+=Math.Max(0,delta);
            if(!confirmed)return false;
            if(elapsed<.3)return true;
            if(Page+1>=Pages.Length)return false;
            Page++;elapsed=0;return true;
        }
    }
    internal sealed class NativeResults:IDisposable
    {
        private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NativeResults current;
        private readonly RuntimeScope resources=new RuntimeScope();
        private readonly string root;
        private SceneProgram source;
        private ResultPageSnapshot[] frozen=new ResultPageSnapshot[0];
        private ConditionalWeakTable<StatsScreen,ResultCursor> sessions=new ConditionalWeakTable<StatsScreen,ResultCursor>();
        private bool prepared;
        internal NativeResults(string mapRoot)
        {if(current!=null)throw new InvalidOperationException("Previous result pages must leave with their world");root=Path.GetFullPath(mapRoot);current=this;}
        internal static void Validate(SceneProgram scene)
        {
            var rows=Rows(scene);if(rows.Length>64)throw new InvalidDataException("Scene supports at most 64 result rows");
            var pages=rows.GroupBy(c=>Text(scene,c.Id,"page"),StringComparer.Ordinal).ToArray();
            if(pages.Length>8)throw new InvalidDataException("Scene supports at most 8 result pages");
            foreach(var page in pages)
            {
                CheckText(page.Key,80,"Result page title");
                if(string.IsNullOrWhiteSpace(page.Key))throw new InvalidDataException("Result page title is required");
                if(page.Count()>8)throw new InvalidDataException(page.Key+": a result page supports at most 8 rows");
                if(page.Select(c=>Text(scene,c.Id,"role")).Distinct().Count()!=1)throw new InvalidDataException(page.Key+": result rows must agree on their ending role");
                foreach(var row in page){CheckText(Text(scene,row.Id,"label"),80,row.Id+" label");CheckText(Text(scene,row.Id,"value"),160,row.Id+" value");}
            }
        }
        private static void CheckText(string text,int limit,string name)
        {if(text.Length>limit||text.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new InvalidDataException(name+": use one bounded line");}
        private static CompiledComponent[] Rows(SceneProgram scene)
        {return scene.Components.Values.Where(c=>c.Descriptor.Kind=="result").ToArray();}
        private static string Text(SceneProgram scene,string id,string field){return scene.Properties.Read(id,field).Text;}
        internal static ResultPageSnapshot[] Capture(SceneProgram scene)
        {
            scene.ComposeCurrentValues();Validate(scene);
            return Rows(scene).Where(c=>scene.Properties.Read(c.Id,"enabled").X!=0)
                .GroupBy(c=>Text(scene,c.Id,"page"),StringComparer.Ordinal)
                .OrderBy(g=>g.Min(c=>scene.Properties.Read(c.Id,"order").X)).ThenBy(g=>g.Key,StringComparer.Ordinal)
                .Select(g=>new ResultPageSnapshot{Title=g.Key,Role=Text(scene,g.First().Id,"role"),
                    Color=scene.Properties.Read(g.OrderBy(c=>scene.Properties.Read(c.Id,"order").X).ThenBy(c=>c.Id,StringComparer.Ordinal).First().Id,"color"),
                    Background=scene.Properties.Read(g.OrderBy(c=>scene.Properties.Read(c.Id,"order").X).ThenBy(c=>c.Id,StringComparer.Ordinal).First().Id,"background"),
                    Lines=new[]{g.Key}.Concat(g.OrderBy(c=>scene.Properties.Read(c.Id,"order").X).ThenBy(c=>c.Id,StringComparer.Ordinal)
                        .Select(c=>{string label=Text(scene,c.Id,"label"),value=Text(scene,c.Id,"value");return label==""?value:value==""?label:label+": "+value;})).ToArray()}).ToArray();
        }
        internal static void Prepare(SceneProgram scene)
        {
            var owner=current;if(owner==null||owner.prepared||Rows(scene).Length==0)return;
            var begin=typeof(StatsScreen).GetMethod("OnNewRun",All);var run=typeof(StatsScreen).GetMethod("MyRun",All);var draw=typeof(StatsScreen).GetMethod("DrawStats",All);
            var lines=typeof(StatsScreen).GetField("m_lines",All);var tree=typeof(StatsScreen).GetField("m_BT",All);
            if(begin==null||run==null||run.ReturnType!=typeof(BTresult)||draw==null||lines==null||lines.FieldType!=typeof(string[])||tree==null||tree.FieldType!=typeof(BTmanager))
                throw new NotSupportedException("Native statistics page contract changed");
            var hooks=owner.resources.Own(new OwnedPatches("mega-mapping-expansion.results"));
            hooks.Add(begin,postfix:typeof(NativeResults).GetMethod("Begin",All));
            hooks.Add(run,postfix:typeof(NativeResults).GetMethod("Advance",All));
            // Draw's wrapper can be inlined before map loading; hook the actual text pass
            hooks.Add(draw,prefix:typeof(NativeResults).GetMethod("BeforeDraw",All));
            owner.prepared=true;
        }
        internal static void Attach(SceneProgram scene,RuntimeScope scope)
        {
            var owner=current;if(owner==null)return;
            owner.source=scene;owner.frozen=new ResultPageSnapshot[0];owner.sessions=new ConditionalWeakTable<StatsScreen,ResultCursor>();
            scope.Defer(delegate{if(ReferenceEquals(owner.source,scene)){if(scene.Signals.Read("scene.completed").Value.X!=0)owner.frozen=Capture(scene);owner.source=null;}});
        }
        internal static void Freeze(SceneProgram scene)
        {var owner=current;if(owner!=null&&ReferenceEquals(owner.source,scene))owner.frozen=Capture(scene);}
        internal static string EndingRole(EndingType ending)
        {return ending==EndingType.Ghost?"owl":ending==EndingType.NewBabePlus?"nbp":"main";}
        private static void Begin(StatsScreen __instance)
        {
            var owner=current;if(owner==null||!MappingSettings.Enabled||!string.Equals(owner.root,MappingPaths.ResolveLevelRoot(Game1.instance.contentManager.root),StringComparison.OrdinalIgnoreCase))return;
            if(owner.source!=null)owner.frozen=Capture(owner.source);
            string role=EndingRole(GameEnding.GetEnding());
            var font=Game1.instance.contentManager.font.MenuFont;
            var pages=owner.frozen.Where(p=>p.Role=="any"||p.Role==role).Where(p=>{
                bool fits=p.Lines.All(line=>font.MeasureString(line).X<=440);
                if(!fits)ModEntry.Log("Result page skipped: text exceeds 440 pixels: "+p.Title);return fits;}).ToArray();
            owner.sessions.Remove(__instance);if(pages.Length!=0)owner.sessions.Add(__instance,new ResultCursor(pages));
        }
        private static void Advance(StatsScreen __instance,TickData p_data,BTmanager ___m_BT,ref BTresult __result)
        {
            ResultCursor cursor;var owner=current;
            if(owner==null||!MappingSettings.Enabled||!owner.sessions.TryGetValue(__instance,out cursor))return;
            int before=cursor.Page;
            if(cursor.Advance(p_data.delta_time,__result==BTresult.Success))
            {if(cursor.Page!=before)___m_BT.Reset();__result=BTresult.Running;}
        }
        private static Color Tint(SceneValue value){return new Color((float)value.X,(float)value.Y,(float)value.Z,(float)value.W);}
        private static bool BeforeDraw(StatsScreen __instance)
        {
            ResultCursor cursor;var owner=current;
            if(owner==null||!MappingSettings.Enabled||!owner.sessions.TryGetValue(__instance,out cursor)||cursor.Page<0||cursor.Page>=cursor.Pages.Length)return true;
            var page=cursor.Pages[cursor.Page];
            Game1.spriteBatch.Draw(Game1.instance.contentManager.Pixel.texture,JumpGame.GAME_RECT,Tint(page.Background));
            var style=new TextStyle{color=Tint(page.Color),layered=false};
            for(int i=0;i<page.Lines.Length;i++)TextHelper.DrawStyleString(Game1.instance.contentManager.font.MenuFont,page.Lines[i],new Vector2(240,130+i*20),Vector2.One/2,style);
            return false;
        }
        public void Dispose(){resources.Dispose();if(ReferenceEquals(current,this))current=null;source=null;frozen=new ResultPageSnapshot[0];sessions=new ConditionalWeakTable<StatsScreen,ResultCursor>();}
    }
}
