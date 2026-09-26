using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JKRuntime;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OverlayPlus.Api;

namespace OverlayPlus
{
    internal static class Adapters
    {
        private sealed class SourceState
        {
            internal OverlaySource Source;
            internal bool Available,Claimed,Failed;
            internal string Text="";
        }
        private static readonly Dictionary<string,SourceState> sources=new Dictionary<string,SourceState>();
        private static long generation=-1;
        private static NativeCapture jumpCapture;
        private static Type replayRuntime,replaySettings;
        private static MethodInfo replayWatch,replaySave,replayGhost;
        private static PropertyInfo replayViewer;
        private static FieldInfo replayHeader,replayId;
        internal static void Prepare(RuntimeScope scope)
        {
            sources.Clear();generation=-1;var h=scope.Own(new OwnedPatches(Hooks.Id));
            scope.Defer(delegate{ReleaseClaims();sources.Clear();replayRuntime=null;replayViewer=null;replaySettings=null;replayWatch=replaySave=replayGhost=null;replayHeader=replayId=null;});
            h.Add(typeof(JumpKing.Util.TextHelper).GetMethod("DrawString"),prefix:typeof(Adapters).GetMethod("CaptureText",Native.Flags));
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
                var jump=assembly.GetType("JumpKingLastJumpValue.Models.GameLoopDraw");
                if(jump!=null&&jumpCapture==null){var method=jump.GetMethod("DrawText",Native.Flags);if(method!=null){jumpCapture=scope.Own(new NativeCapture());scope.Defer(delegate{jumpCapture=null;});h.Add(method,prefix:typeof(Adapters).GetMethod("CaptureStart",Native.Flags),priority:Priority.First);h.Add(method,postfix:typeof(Adapters).GetMethod("CaptureEnd",Native.Flags),finalizer:typeof(Adapters).GetMethod("CaptureFinal",Native.Flags),priority:Priority.Last);}}
                var replay=assembly.GetType("Replays.ReplayRuntime");if(replay==null)continue;replayRuntime=replay;replaySettings=assembly.GetType("Replays.ReplaySettingsStore");replayWatch=replay.GetMethod("RequestWatch",Native.Flags);replaySave=replay.GetMethod("SaveCurrentReplay",Native.Flags);replayGhost=replay.GetMethod("RefreshGhost",Native.Flags);replayViewer=replay.GetProperty("ViewerActive",Native.Flags);
                var data=assembly.GetType("Replays.ReplayData");var repository=assembly.GetType("Replays.ReplayRepository");if(data!=null&&repository!=null){replayHeader=data.GetField("Header",Native.Flags);replayId=replayHeader.FieldType.GetField("Id",Native.Flags);var queue=repository.GetMethod("QueueSave",Native.Flags);if(queue!=null)h.Add(queue,postfix:typeof(Adapters).GetMethod("ReplayQueued",Native.Flags));}
            }
        }
        internal static void Activate(){generation=-1;Update();}
        internal static void BeginFrame(){if(jumpCapture!=null)jumpCapture.Reset();}
        internal static string[] Names(){var result=sources.Select(p=>p.Key).ToList();if(jumpCapture!=null)result.Insert(0,"jump-percent.native");return result.ToArray();}
        internal static string Name(string id){SourceState s;return id=="jump-percent.native"?"Jump% + Subframe Charge":sources.TryGetValue(id,out s)?s.Source.Name:id;}
        internal static void Update()
        {
            if(generation!=OverlayRegistry.Generation){var live=OverlayRegistry.Sources();foreach(var old in sources.Where(p=>!live.Contains(p.Value.Source)).ToArray()){Release(old.Value);sources.Remove(old.Key);}foreach(var source in live)if(!sources.ContainsKey(source.Id))sources.Add(source.Id,new SourceState{Source=source});generation=OverlayRegistry.Generation;}
            foreach(var s in sources.Values){if(s.Failed)continue;if(!Claimed(s.Source.Id)){Release(s);s.Available=false;continue;}try{s.Available=s.Source.Available==null||s.Source.Available();s.Text=s.Source.Text==null?"":s.Source.Text()??"";bool claim=Claimed(s.Source.Id)&&s.Available;if(!claim)Release(s);}catch(Exception e){s.Failed=true;Release(s);Controller.Status="Provider "+s.Source.Id+": "+e.Message;}}
        }
        private static bool Claimed(string id){return Controller.Active&&!Controller.Failed&&Store.Settings.Enabled&&Controller.Layout!=null&&Controller.Layout.Widgets.Any(w=>w.Enabled&&w.Kind==WidgetKind.External&&w.Source==id&&Widgets.Visible(w));}
        internal static bool Draw(Canvas c,Widget w,Rectangle r)
        {
            if(w.Source=="jump-percent.native"){
                if(jumpCapture==null)return false;
                if(jumpCapture.Ready)jumpCapture.Draw(c.Batch,r,w.Opacity);
                else c.Text("Jump%: waiting for a jump",r.X,r.Y,Math.Min(18,w.TextSize),Color.LightGray);
                return true;
            }
            SourceState s;if(!sources.TryGetValue(w.Source,out s)||!s.Available||s.Failed)return false;
            try{if(s.Source.Draw!=null)s.Source.Draw(c.Batch,r,w.Opacity);else c.Wrapped(s.Text,r,w.TextSize,Canvas.ColorOf(w.Foreground,w.Opacity),w.Font,w.Align,w.Shadow,w.Outline);if(!s.Claimed&&s.Source.SetNativeVisible!=null&&Claimed(w.Source)){s.Source.SetNativeVisible(false);s.Claimed=true;}return true;}
            catch(Exception e){s.Failed=true;Release(s);Controller.Status="Provider drawing failed: "+e.Message;return false;}
        }
        private static void Release(SourceState s){if(!s.Claimed)return;try{if(s.Source.SetNativeVisible!=null)s.Source.SetNativeVisible(true);}catch(Exception e){Console.WriteLine("[Overlay+] Restore "+s.Source.Id+": "+e.Message);}finally{s.Claimed=false;}}
        internal static void ReleaseClaims(){foreach(var s in sources.Values)Release(s);}
        private static void CaptureStart(out bool __state){__state=false;if(jumpCapture==null||!Claimed("jump-percent.native"))return;__state=jumpCapture.Begin();}
        private static void CaptureEnd(bool __state){if(__state&&jumpCapture!=null)jumpCapture.End();}
        private static Exception CaptureFinal(Exception __exception,bool __state){if(__state&&jumpCapture!=null)jumpCapture.End();return __exception;}
        private static bool CaptureText(SpriteFont p_font,string p_text,Vector2 p_position,Color p_color,Vector2 p_center,bool p_is_outlined)
        {if(jumpCapture==null||!jumpCapture.Capturing)return true;jumpCapture.Add(p_font,p_text,p_position,p_color,p_center,p_is_outlined);return false;}
        internal static bool PlaybackActive(){return replayViewer!=null&&(bool)replayViewer.GetValue(null,null);}
        private static void ReplayQueued(object replay,bool __result)
        {
            if(!__result||replay==null||!Controller.Active||Controller.Tracker==null||Store.Archive==null||PlaybackActive())return;
            // QueueSave is called on the game thread before its IO worker starts.
            var id=(string)replayId.GetValue(replayHeader.GetValue(replay));Controller.Tracker.Run.ReplayPath=id;Store.SaveRun(Controller.Tracker.Run);
        }
        internal static bool SaveReplay(){return replaySave!=null&&(bool)replaySave.Invoke(null,null);}
        internal static bool Watch(string id){if(replayWatch==null||string.IsNullOrEmpty(id))return false;Controller.AfterEditorClose=delegate{if(!(bool)replayWatch.Invoke(null,new object[]{id}))Controller.Notify("Replay is missing or incompatible");};Controller.CloseEditor();return true;}
        internal static bool Ghost(string id)
        {
            if(replaySettings==null||replayGhost==null||string.IsNullOrEmpty(id))return false;
            var commit=replaySettings.GetMethod("SetGhost",Native.Flags);
            if(commit==null)return false;
            commit.Invoke(null,new object[]{id});replayGhost.Invoke(null,null);return true;
        }
    }
    internal sealed class NativeCapture:IDisposable
    {
        private sealed class TextCommand { internal SpriteFont Font; internal string Text; internal Vector2 Position; internal Color Color; internal bool Outline; }
        private readonly List<TextCommand> commands=new List<TextCommand>();
        internal bool Ready;
        internal bool Capturing;
        internal void Reset(){if(!Capturing){Ready=false;commands.Clear();}}
        internal bool Begin()
        {
            if(Capturing)return false;commands.Clear();Capturing=true;return true;
        }
        internal void Add(SpriteFont font,string text,Vector2 position,Color color,Vector2 center,bool outline){commands.Add(new TextCommand{Font=font,Text=text,Position=position-font.MeasureString(text)*center,Color=color,Outline=outline});}
        internal void End(){if(!Capturing)return;Capturing=false;Ready=commands.Count>0;}
        internal void Draw(SpriteBatch batch,Rectangle bounds,float opacity)
        {
            if(commands.Count==0)return;float left=commands.Min(c=>c.Position.X),top=commands.Min(c=>c.Position.Y),right=commands.Max(c=>c.Position.X+c.Font.MeasureString(c.Text).X),bottom=commands.Max(c=>c.Position.Y+c.Font.MeasureString(c.Text).Y);
            float scale=Math.Min(bounds.Width/Math.Max(1,right-left),bounds.Height/Math.Max(1,bottom-top));foreach(var c in commands){Vector2 position=new Vector2(bounds.X,bounds.Y)+(c.Position-new Vector2(left,top))*scale;if(c.Outline)for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(x!=0||y!=0)batch.DrawString(c.Font,c.Text,position+new Vector2(x*scale,y*scale),new Color(128,128,128)*opacity,0,Vector2.Zero,scale,SpriteEffects.None,0);batch.DrawString(c.Font,c.Text,position,c.Color*opacity,0,Vector2.Zero,scale,SpriteEffects.None,0);}
        }
        public void Dispose(){commands.Clear();Capturing=Ready=false;}
    }
}
