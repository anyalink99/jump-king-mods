using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OverlayPlus
{
    internal static class Editor
    {
        private sealed class Hit { internal Rectangle Bounds; internal Action Action; }
        private sealed class Field
        {
            internal string Label,Value;
            internal Action<string> Set;
            internal bool Multiline,All;
            internal int Caret;
        }
        internal static bool Open,Preview;
        private static bool panels=true,dragging,resizing,marquee,learn,learnReady;
        private static readonly HashSet<string> selection=new HashSet<string>();
        private static readonly List<Hit> hits=new List<Hit>();
        private static readonly EditHistory history=new EditHistory();
        private static Layout dragStart;
        private static Vector2 startPointer;
        private static Rectangle selectionBox;
        private static Field field;
        private static IDisposable textCapture;
        private static string tab="Layers",popup="";
        private static float leftScroll,rightScroll,leftMax,rightMax;
        private static int keyIndex,focusIndex,propertyTab,dragPanel;
        private static Vector2 leftOffset,rightOffset,panelStart;
        private static double lastClick;
        private static string lastClicked;
        private static Vector2 lastClickPointer;
        private static Rectangle leftPanel,rightPanel;
        private static string chosenRun="";
        private static string[] browserFiles=new string[0];
        private static int browserPage;
        private static Action<string> chooseFile;
        private static readonly Color Ink=new Color(7,9,11),Raised=new Color(13,17,19),Line=new Color(114,114,114),Text=new Color(232,226,204),Muted=new Color(124,132,130),Gold=new Color(229,184,68),Blue=new Color(229,184,68);
        private static Layout Layout {get{return Controller.Layout;}}
        internal static void Begin(){ClearField();lastClicked=null;dragPanel=0;dragging=resizing=marquee=false;Open=true;panels=true;Preview=false;popup="";hits.Clear();history.Clear();selection.Clear();leftScroll=rightScroll=0;focusIndex=0;}
        internal static void Finish(){CommitField();CancelDrag();Open=false;Preview=false;learn=false;popup="";hits.Clear();Adapters.ReleaseClaims();}
        internal static void Abort(){ClearField();CancelDrag();Open=false;Preview=false;learn=false;popup="";hits.Clear();}
        private static void ClearField(){field=null;if(textCapture!=null){textCapture.Dispose();textCapture=null;}}
        internal static void Character(char c)
        {
            if(field==null||Inputs.Ctrl||Inputs.Alt)return;
            if(c=='\r'){if(field.Multiline&&Inputs.Shift)Insert("\n");return;}
            if(c>=32)Insert(c.ToString());
        }
        private static void Insert(string s){if(field.All){field.Value="";field.Caret=0;field.All=false;}if(field.Value.Length+s.Length>16000)return;field.Value=field.Value.Insert(field.Caret,s);field.Caret+=s.Length;}
        private static void CommitField()
        {if(field==null)return;var f=field;ClearField();var before=Layout.Clone();try{f.Set(f.Value);Changed();}catch(Exception e){int i=Store.Settings.Layouts.FindIndex(l=>l.Id==before.Id);if(i>=0)Store.Settings.Layouts[i]=before;Controller.Layout=before;Controller.Status="Invalid "+f.Label+": "+e.Message;}}
        private static void Edit(string label,string value,Action<string> setter,bool multiline=false){CommitField();history.Push(Layout);textCapture=JKRuntime.UI.UIApi.AcquireTextInput();field=new Field{Label=label,Value=value??"",Set=setter,Multiline=multiline,Caret=(value??"").Length,All=true};popup="";}
        private static List<Widget> Selected(){return Layout.Widgets.Where(w=>selection.Contains(w.Id)).ToList();}
        private static void Changed(){Store.ValidateLayout(Layout);Store.SaveSettings();Controller.Canvas.PrepareImages(Layout.Widgets);Widgets.Refresh();}
        private static void Change(Action action){CommitField();var before=Layout.Clone();history.Push(Layout);try{action();Changed();}catch(Exception e){int index=Store.Settings.Layouts.FindIndex(l=>l.Id==before.Id);if(index>=0)Store.Settings.Layouts[index]=before;Controller.Layout=before;Controller.Status="Change rejected: "+e.Message;}}
        internal static void Update()
        {
            if(!Open)return;
            if(field!=null){UpdateField();return;}
            if(learn){if(Inputs.Press(Keys.Escape)){learn=false;return;}if(!learnReady){learnReady=Inputs.AllReleased();return;}string source=Inputs.Learn();if(source!=null){var w=Selected().FirstOrDefault();if(w!=null)Change(delegate{if(w.Kind==WidgetKind.Inputs&&w.Keys.Count>0)w.Keys[Math.Min(keyIndex,w.Keys.Count-1)].Source=source;else w.Source=source;});learn=false;Controller.Status="Input source: "+source;}return;}
            if(Inputs.Press(Keys.Escape)){if(popup!=""){popup="";return;}if(dragging||marquee){CancelDrag();return;}Controller.CloseEditor();return;}
            if(Inputs.Press(Keys.Tab)){panels=!panels;hits.Clear();return;}
            if(Inputs.Ctrl&&Inputs.Press(Keys.Z)){Replace(history.Undo(Layout));return;}if(Inputs.Ctrl&&Inputs.Press(Keys.Y)){Replace(history.Redo(Layout));return;}
            if(Inputs.Ctrl&&Inputs.Press(Keys.A)){selection.Clear();foreach(var w in Layout.Widgets)selection.Add(w.Id);return;}
            if(Inputs.Ctrl&&Inputs.Press(Keys.D)){Duplicate();return;}if(Inputs.Ctrl&&Inputs.Press(Keys.G)){Group(Inputs.Shift);return;}
            if(Inputs.Press(Keys.Delete)){Delete();return;}if(Inputs.Press(Keys.PageUp)){Order(1);return;}if(Inputs.Press(Keys.PageDown)){Order(-1);return;}
            if(Inputs.Press(Keys.F2)){Preview=!Preview;return;}if(Inputs.Press(Keys.F3)){popup=popup=="Add"?"":"Add";return;}
            int wheel=(Inputs.Mouse.ScrollWheelValue-Inputs.PreviousMouse.ScrollWheelValue)/120;
            if(wheel!=0&&panels){if(rightPanel.Contains(Inputs.Pointer.ToPoint()))rightScroll=Math.Max(0,Math.Min(rightMax,rightScroll-wheel*65));else if(leftPanel.Contains(Inputs.Pointer.ToPoint()))leftScroll=Math.Max(0,Math.Min(leftMax,leftScroll-wheel*65));}
            if(Inputs.PadPress(Buttons.LeftShoulder)||Inputs.PadPress(Buttons.RightShoulder)){CycleSelection(Inputs.PadPress(Buttons.LeftShoulder)?-1:1);return;}
            if(Inputs.PadPress(Buttons.Y)){panels=!panels;return;}
            if(Inputs.PadPress(Buttons.X)){popup=popup=="Add"?"":"Add";return;}
            if(Inputs.PadPress(Buttons.B)){if(popup!="")popup="";else Controller.CloseEditor();return;}
            if(Inputs.Pad.IsButtonDown(Buttons.RightStick)){UpdatePadFocus();return;}
            float step=Inputs.Shift?10:1;float dx=0,dy=0;
            if(Inputs.Repeat(Keys.Left)||Inputs.PadPress(Buttons.DPadLeft))dx=-step;if(Inputs.Repeat(Keys.Right)||Inputs.PadPress(Buttons.DPadRight))dx=step;
            if(Inputs.Repeat(Keys.Up)||Inputs.PadPress(Buttons.DPadUp))dy=-step;if(Inputs.Repeat(Keys.Down)||Inputs.PadPress(Buttons.DPadDown))dy=step;
            if(dx!=0||dy!=0){Change(delegate{foreach(var w in Selected().Where(w=>!w.Locked)){if(Inputs.Ctrl||Inputs.Pad.IsButtonDown(Buttons.LeftStick)){w.Width+=dx;w.Height+=dy;}else{w.X+=dx;w.Y+=dy;}}});return;}
            if(Inputs.RightClick){popup=popup=="Add"?"":"Add";return;}
            if(dragPanel!=0){if(Inputs.Mouse.LeftButton==ButtonState.Pressed){Vector2 offset=panelStart+Inputs.Pointer-startPointer;if(dragPanel==1)leftOffset=offset;else rightOffset=offset;}else dragPanel=0;return;}
            if(Inputs.Click){
                var hit=hits.LastOrDefault(h=>h.Bounds.Contains(Inputs.Pointer.ToPoint()));if(hit!=null){hit.Action();return;}
                if(panels&&(new Rectangle(leftPanel.X,leftPanel.Y,leftPanel.Width,30).Contains(Inputs.Pointer.ToPoint())||new Rectangle(rightPanel.X,rightPanel.Y,rightPanel.Width,30).Contains(Inputs.Pointer.ToPoint()))){dragPanel=leftPanel.Contains(Inputs.Pointer.ToPoint())?1:2;startPointer=Inputs.Pointer;panelStart=dragPanel==1?leftOffset:rightOffset;return;}
                if(panels&&(leftPanel.Contains(Inputs.Pointer.ToPoint())||rightPanel.Contains(Inputs.Pointer.ToPoint())))return;
                popup="";Widget picked=Layout.Widgets.LastOrDefault(w=>w.Enabled&&Controller.Surface.Bounds(w).Contains(Inputs.Pointer.ToPoint()));
                if(picked!=null){
                    if(!selection.Contains(picked.Id)){if(!Inputs.Ctrl&&!Inputs.Shift)selection.Clear();selection.Add(picked.Id);if(!Inputs.Alt&&!string.IsNullOrEmpty(picked.Group))foreach(var w in Layout.Widgets.Where(w=>w.Group==picked.Group))selection.Add(w.Id);}
                    else if(Inputs.Ctrl){selection.Remove(picked.Id);return;}
                    var pickedBounds=Controller.Surface.Bounds(picked);bool corner=new Rectangle(pickedBounds.Right-14,pickedBounds.Bottom-14,20,20).Contains(Inputs.Pointer.ToPoint());
                    if(!corner&&lastClicked==picked.Id&&Vector2.Distance(lastClickPointer,Inputs.Pointer)<6&&Controller.Now-lastClick<.32&&picked.Kind==WidgetKind.Text){Edit("Text",picked.Text,s=>picked.Text=s,true);lastClicked=null;return;}
                    lastClicked=picked.Id;lastClick=Controller.Now;lastClickPointer=Inputs.Pointer;
                    if(!picked.Locked){dragStart=Layout.Clone();startPointer=Inputs.Pointer;dragging=true;Rectangle r=Controller.Surface.Bounds(picked);resizing=selection.Count==1&&new Rectangle(r.Right-14,r.Bottom-14,20,20).Contains(Inputs.Pointer.ToPoint());}
                }else{if(!Inputs.Ctrl&&!Inputs.Shift)selection.Clear();marquee=true;startPointer=Inputs.Pointer;selectionBox=Rectangle.Empty;}
            }
            if(dragging&&Inputs.Mouse.LeftButton==ButtonState.Pressed)Drag();
            if(marquee){selectionBox=Rect(startPointer,Inputs.Pointer);}
            if(Inputs.Release){if(dragging){history.Push(dragStart);dragging=false;dragStart=null;Changed();}if(marquee){foreach(var w in Layout.Widgets)if(w.Enabled&&selectionBox.Intersects(Controller.Surface.Bounds(w)))selection.Add(w.Id);marquee=false;}}
        }
        private static void UpdateField()
        {
            if(Inputs.Press(Keys.Escape)){ClearField();return;}if(Inputs.Press(Keys.Enter)&&!(field.Multiline&&Inputs.Shift)){CommitField();return;}
            if(Inputs.Ctrl&&Inputs.Press(Keys.A)){field.All=true;return;}
            if(Inputs.Ctrl&&Inputs.Press(Keys.V)){try{Insert(System.Windows.Forms.Clipboard.GetText());}catch(Exception e){Controller.Status=e.Message;}return;}
            if(Inputs.Ctrl&&Inputs.Press(Keys.C)){try{System.Windows.Forms.Clipboard.SetText(field.Value);}catch(Exception e){Controller.Status=e.Message;}return;}
            if(Inputs.Repeat(Keys.Back)){if(field.All){field.Value="";field.Caret=0;field.All=false;}else if(field.Caret>0){field.Value=field.Value.Remove(field.Caret-1,1);field.Caret--;}return;}
            if(Inputs.Repeat(Keys.Delete)){if(field.All){field.Value="";field.Caret=0;field.All=false;}else if(field.Caret<field.Value.Length)field.Value=field.Value.Remove(field.Caret,1);return;}
            if(Inputs.Repeat(Keys.Left)){field.Caret=Math.Max(0,field.Caret-1);field.All=false;}if(Inputs.Repeat(Keys.Right)){field.Caret=Math.Min(field.Value.Length,field.Caret+1);field.All=false;}
            if(Inputs.Press(Keys.Home)){field.Caret=0;field.All=false;}if(Inputs.Press(Keys.End)){field.Caret=field.Value.Length;field.All=false;}
            if(Inputs.Click){CommitField();}
        }
        private static void UpdatePadFocus(){if(hits.Count==0)return;if(Inputs.PadPress(Buttons.DPadDown))focusIndex=(focusIndex+1)%hits.Count;if(Inputs.PadPress(Buttons.DPadUp))focusIndex=(focusIndex+hits.Count-1)%hits.Count;if(Inputs.PadPress(Buttons.A))hits[Math.Min(focusIndex,hits.Count-1)].Action();}
        private static void CycleSelection(int delta){if(Layout.Widgets.Count==0)return;int i=Layout.Widgets.FindIndex(w=>selection.Contains(w.Id));i=(i+delta+Layout.Widgets.Count)%Layout.Widgets.Count;selection.Clear();selection.Add(Layout.Widgets[i].Id);}
        private static Rectangle Rect(Vector2 a,Vector2 b){return new Rectangle((int)Math.Min(a.X,b.X),(int)Math.Min(a.Y,b.Y),Math.Max(1,(int)Math.Abs(a.X-b.X)),Math.Max(1,(int)Math.Abs(a.Y-b.Y)));}
        internal static void CancelDrag(){if(dragging&&dragStart!=null)Replace(dragStart);dragging=marquee=false;dragStart=null;}
        private static void Drag()
        {
            var delta=Inputs.Pointer-startPointer;foreach(var w in Selected().Where(w=>!w.Locked)){var old=dragStart.Widgets.First(v=>v.Id==w.Id);if(resizing){w.Width=Math.Max(16,old.Width+delta.X);w.Height=Math.Max(16,old.Height+delta.Y);continue;}w.X=old.X+delta.X;w.Y=old.Y+delta.Y;}
            if(Inputs.Alt||!Store.Settings.Snap)return;
            var chosen=Selected().FirstOrDefault(w=>!w.Locked);if(chosen==null)return;int grid=Store.Settings.GridSize;
            if(resizing){chosen.Width=Math.Max(16,(float)Math.Round(chosen.Width/grid)*grid);chosen.Height=Math.Max(16,(float)Math.Round(chosen.Height/grid)*grid);return;}
            var r=Controller.Surface.Bounds(chosen);float tx=(float)Math.Round(r.X/(float)grid)*grid,ty=(float)Math.Round(r.Y/(float)grid)*grid;
            var targets=Layout.Widgets.Where(w=>!selection.Contains(w.Id)&&w.Enabled).Select(Controller.Surface.Bounds).ToList();targets.Add(Controller.Surface.Game);targets.Add(Controller.Surface.Window);
            foreach(var t in targets){foreach(int edge in new[]{t.Left,t.Right,t.Center.X})foreach(int offset in new[]{0,r.Width/2,r.Width})if(Math.Abs(r.X+offset-edge)<5)tx=edge-offset;foreach(int edge in new[]{t.Top,t.Bottom,t.Center.Y})foreach(int offset in new[]{0,r.Height/2,r.Height})if(Math.Abs(r.Y+offset-edge)<5)ty=edge-offset;}
            foreach(var w in Selected().Where(w=>!w.Locked)){w.X+=tx-r.X;w.Y+=ty-r.Y;}
        }
        private static void Replace(Layout replacement){int index=Store.Settings.Layouts.FindIndex(l=>l.Id==Layout.Id);if(index>=0)Store.Settings.Layouts[index]=replacement;Controller.Layout=replacement;Changed();}
        private static void Add(WidgetKind kind){Change(delegate{var w=Defaults.Make(kind);var area=Controller.Surface.Game;w.X=area.Center.X-w.Width/2;w.Y=area.Center.Y-w.Height/2;if(kind==WidgetKind.External)w.Source=Adapters.Names().FirstOrDefault()??"provider.widget";Layout.Widgets.Add(w);selection.Clear();selection.Add(w.Id);});popup="";rightScroll=0;}
        private static void Delete(){Change(delegate{Layout.Widgets.RemoveAll(w=>selection.Contains(w.Id)&&!w.Locked);selection.RemoveWhere(id=>!Layout.Widgets.Any(w=>w.Id==id));});}
        private static void Duplicate(){Change(delegate{var originals=Selected();selection.Clear();foreach(var w in originals){var copy=w.Clone();copy.Id=Guid.NewGuid().ToString("N");copy.Group="";copy.Name+=" copy";copy.X+=16;copy.Y+=16;copy.Locked=false;Layout.Widgets.Add(copy);selection.Add(copy.Id);}});}
        private static void Group(bool ungroup){Change(delegate{string group=ungroup?"":Guid.NewGuid().ToString("N");foreach(var w in Selected())w.Group=group;});}
        private static void Order(int direction){Change(delegate{var items=Selected();if(direction>0)items.Reverse();foreach(var w in items){int i=Layout.Widgets.IndexOf(w),j=Math.Max(0,Math.Min(Layout.Widgets.Count-1,i+direction));Layout.Widgets.RemoveAt(i);Layout.Widgets.Insert(j,w);}});}
        private static void Align(string mode)
        {
            Change(delegate{var items=Selected().Where(w=>!w.Locked).ToList();if(items.Count<2)return;var boxes=items.Select(Controller.Surface.Bounds).ToArray();int l=boxes.Min(r=>r.Left),rgt=boxes.Max(r=>r.Right),t=boxes.Min(r=>r.Top),b=boxes.Max(r=>r.Bottom);
                if(mode=="Distribute X"||mode=="Distribute Y"){bool horizontal=mode.EndsWith("X");items=items.OrderBy(w=>horizontal?Controller.Surface.Bounds(w).X:Controller.Surface.Bounds(w).Y).ToList();float total=items.Sum(w=>horizontal?w.Width:w.Height);float gap=((horizontal?rgt-l:b-t)-total)/Math.Max(1,items.Count-1),cursor=horizontal?l:t;foreach(var w in items){var box=Controller.Surface.Bounds(w);Controller.Surface.SetPosition(w,horizontal?cursor:box.X,horizontal?box.Y:cursor);cursor+=(horizontal?w.Width:w.Height)+gap;}return;}
                foreach(var w in items){var box=Controller.Surface.Bounds(w);float x=box.X,y=box.Y;if(mode=="Left")x=l;if(mode=="Right")x=rgt-w.Width;if(mode=="Center X")x=(l+rgt-w.Width)/2;if(mode=="Top")y=t;if(mode=="Bottom")y=b-w.Height;if(mode=="Center Y")y=(t+b-w.Height)/2;Controller.Surface.SetPosition(w,x,y);}
            });popup="";
        }
        internal static void Draw(Canvas c)
        {
            hits.Clear();var surface=Controller.Surface;
            if(Store.Settings.Grid&&dragging){int g=Math.Max(8,Store.Settings.GridSize);for(int x=0;x<surface.Window.Width;x+=g)c.Fill(new Rectangle(x,50,1,surface.Window.Height-80),new Color(108,132,160,18));for(int y=50;y<surface.Window.Height-30;y+=g)c.Fill(new Rectangle(0,y,surface.Window.Width,1),new Color(108,132,160,18));}
            c.Border(surface.Game,new Color(105,160,209,120));
            foreach(var w in Layout.Widgets){if(!w.Enabled)continue;var r=surface.Bounds(w);if(selection.Contains(w.Id)){c.Border(r,w.Locked?Muted:Gold,2);c.Fill(new Rectangle(r.Right-7,r.Bottom-7,14,14),w.Locked?Muted:Gold);if(dragging){c.Fill(new Rectangle(r.Center.X,50,1,surface.Window.Height-80),new Color(238,191,81,90));c.Fill(new Rectangle(0,r.Center.Y,surface.Window.Width,1),new Color(238,191,81,90));c.Text(r.X+", "+r.Y+"   "+r.Width+" × "+r.Height,r.X,r.Bottom+8,14,Gold);}}}
            if(marquee){c.Fill(selectionBox,new Color(60,110,160,45));c.Border(selectionBox,Blue);}
            c.Frame(new Rectangle(2,2,surface.Window.Width-4,48));c.Text("OVERLAY+",18,10,24,Gold,"Location");
            Button(c,"+ Add",155,8,78,()=>popup=popup=="Add"?"":"Add");Button(c,"Undo",241,8,65,()=>Replace(history.Undo(Layout)));Button(c,"Redo",314,8,65,()=>Replace(history.Redo(Layout)));Button(c,Preview?"Preview ON":"Preview",387,8,108,()=>Preview=!Preview);Button(c,"Align",503,8,72,()=>popup=popup=="Align"?"":"Align");Button(c,"Rescue",583,8,80,()=>Change(delegate{foreach(var w in Layout.Widgets)Controller.Surface.Rescue(w);}));Button(c,"Done",surface.Window.Width-92,8,76,Controller.CloseEditor);
            int lh=Math.Min(560,surface.Window.Height-115),rh=Math.Min(590,surface.Window.Height-115);
            leftPanel=new Rectangle(Math.Max(0,Math.Min(surface.Window.Width-218,10+(int)leftOffset.X)),Math.Max(52,Math.Min(surface.Window.Height-42-lh,62+(int)leftOffset.Y)),218,lh);rightPanel=new Rectangle(Math.Max(0,Math.Min(surface.Window.Width-282,surface.Window.Width-292+(int)rightOffset.X)),Math.Max(52,Math.Min(surface.Window.Height-42-rh,62+(int)rightOffset.Y)),282,rh);
            if(panels){DrawLeft(c);DrawInspector(c);}else{c.Fill(new Rectangle(12,60,270,32),Ink);c.Text("Tab / Y: show panels · F2: preview",20,66,15,Muted);}
            DrawPopup(c);
            if(field!=null)DrawField(c);
            c.Frame(new Rectangle(2,surface.Window.Height-36,surface.Window.Width-4,34));string status=learn?"Press a key or controller button to display. Escape cancels.":field!=null?"Enter: apply  /  Escape: cancel  /  Ctrl+A/C/V  /  Shift+Enter: new line":!string.IsNullOrEmpty(Store.Warning)?Store.Warning:Controller.Status;c.Text(c.Fit(status,surface.Window.Width-28,16),14,surface.Window.Height-26,16,Muted,"Small");
            if(Inputs.Pad.IsButtonDown(Buttons.RightStick)&&hits.Count>0)c.Border(hits[Math.Min(focusIndex,hits.Count-1)].Bounds,Gold,2);

        }
        private static void Button(Canvas c,string label,float x,float y,float width,Action action,float height=30)
        {var r=new Rectangle((int)x,(int)y,(int)width,(int)height);bool hover=r.Contains(Inputs.Pointer.ToPoint());float size=Math.Max(14,Math.Min(17,17*(width-16)/Math.Max(1,c.Measure(label,17))));if(hover){c.Fill(new Rectangle(r.X+2,r.Y+2,r.Width-4,r.Height-4),new Color(39,36,22));c.Text(">",x+3,y+6,17,Gold);}c.Text(c.Fit(label,width-16,size),x+13,y+6,size,hover?Gold:Text);hits.Add(new Hit{Bounds=r,Action=action});}
        private static void DrawLeft(Canvas c)
        {
            c.Frame(leftPanel);c.Text("OVERLAY TOOLS",leftPanel.X+18,leftPanel.Y+12,20,Gold,"Location");Button(c,"Layers",leftPanel.X+3,leftPanel.Y+37,71,()=>{tab="Layers";leftScroll=0;});Button(c,"Layouts",leftPanel.X+71,leftPanel.Y+37,83,()=>{tab="Layouts";leftScroll=0;});Button(c,"Runs",leftPanel.X+153,leftPanel.Y+37,61,()=>{tab="Runs";leftScroll=0;});
            var bounds=new Rectangle(leftPanel.X+10,leftPanel.Y+77,leftPanel.Width-20,leftPanel.Height-89);c.Clip(bounds);int count=hits.Count;float y=bounds.Y-leftScroll;
            if(tab=="Layers"){
                foreach(var w in Layout.Widgets.AsEnumerable().Reverse()){var item=w;if(selection.Contains(w.Id))c.Fill(new Rectangle(bounds.X,(int)y,bounds.Width,35),new Color(74,65,38));Button(c,(w.Enabled?"● ":"○ ")+w.Name+(w.Locked?" [L]":""),bounds.X,y,bounds.Width,()=>{if(!Inputs.Ctrl)selection.Clear();selection.Add(item.Id);rightScroll=0;},33);y+=38;}
                y+=8;Button(c,"Duplicate",bounds.X,y,96,Duplicate);Button(c,"Delete",bounds.X+103,y,99,Delete);y+=36;Button(c,"Group",bounds.X,y,96,()=>Group(false));Button(c,"Ungroup",bounds.X+103,y,99,()=>Group(true));y+=36;
                Button(c,"Save as template",bounds.X,y,bounds.Width,()=>{var items=Selected();if(items.Count==0)return;if(items.Count==1)Store.Settings.Templates.Add(items[0].Clone());else Store.Settings.GroupTemplates.Add(new Layout{Name=items[0].Name+" group",Widgets=items.Select(w=>w.Clone()).ToList()});Store.SaveSettings();Controller.Notify("Template saved");});y+=36;
                foreach(var t in Store.Settings.Templates.ToArray()){var template=t;Button(c,"+ "+t.Name,bounds.X,y,bounds.Width,()=>Change(delegate{var w=template.Clone();w.Id=Guid.NewGuid().ToString("N");w.Group="";Layout.Widgets.Add(w);}));y+=34;}
                foreach(var t in Store.Settings.GroupTemplates.ToArray()){var template=t;Button(c,"+ "+t.Name,bounds.X,y,bounds.Width,()=>Change(delegate{string group=Guid.NewGuid().ToString("N");selection.Clear();foreach(var item in template.Widgets){var w=item.Clone();w.Id=Guid.NewGuid().ToString("N");w.Group=group;Layout.Widgets.Add(w);selection.Add(w.Id);}}));y+=34;}
            }else if(tab=="Layouts")y=DrawLayouts(c,bounds,y);else y=DrawRuns(c,bounds,y);
            leftMax=Math.Max(0,y+leftScroll-bounds.Bottom);ClampHits(count,bounds);c.Unclip();
        }
        private static float DrawLayouts(Canvas c,Rectangle b,float y)
        {
            foreach(var l in Store.Settings.Layouts.ToArray()){var item=l;Button(c,(l.Id==Layout.Id?"✓ ":"")+l.Name,b.X,y,b.Width,()=>{CommitField();Store.Settings.SelectedLayout=item.Id;Controller.Layout=item;selection.Clear();history.Clear();Changed();});y+=35;}
            y+=10;foreach(string preset in new[]{"Minimal","Speedrun","Practice","Controller","Sidebars"}){string name=preset;Button(c,"New: "+preset,b.X,y,b.Width,()=>{var l=Defaults.Preset(name,Controller.Surface);Store.Settings.Layouts.Add(l);Controller.Layout=l;Store.Settings.SelectedLayout=l.Id;history.Clear();selection.Clear();Changed();});y+=34;}
            Button(c,"Duplicate layout",b.X,y,b.Width,()=>{var l=Layout.Clone();l.Id=Guid.NewGuid().ToString("N");l.Name+=" copy";Store.Settings.Layouts.Add(l);Store.Settings.SelectedLayout=l.Id;Controller.Layout=l;history.Clear();Changed();});y+=35;
            Button(c,"Rename layout",b.X,y,b.Width,()=>Edit("Layout name",Layout.Name,s=>Layout.Name=s));y+=35;
            Button(c,Layout.Map==""?"Scope: all maps":"Scope: this map",b.X,y,b.Width,()=>Change(()=>Layout.Map=Layout.Map==""?Controller.Route.World:""));y+=35;
            Button(c,"Aspect: "+Layout.Aspect,b.X,y,b.Width,()=>Change(()=>Layout.Aspect=Next(Layout.Aspect,new[]{"Any","4:3","Wide","Ultrawide"})));y+=35;
            Button(c,"Export layout",b.X,y,98,()=>{Store.Export(Layout);Controller.Notify("Exported to OverlayPlus/Exports/Layout.xml");});Button(c,"Import",b.X+104,y,98,()=>Browse(Store.Files("Imports","*.xml").Concat(Store.Files("Exports","*.xml")).ToArray(),path=>{var l=Store.Import(path);Store.Settings.Layouts.Add(l);Controller.Layout=l;Store.Settings.SelectedLayout=l.Id;history.Clear();selection.Clear();Changed();}));y+=40;
            Button(c,"Hide HUD: "+(!Store.Settings.Enabled),b.X,y,b.Width,()=>{Store.Settings.Enabled=!Store.Settings.Enabled;Changed();});y+=35;
            Button(c,"Grid: "+Store.Settings.Grid+"  Snap: "+Store.Settings.Snap,b.X,y,b.Width,()=>{Store.Settings.Grid=!Store.Settings.Grid;Store.Settings.Snap=Store.Settings.Grid;Changed();});y+=35;
            Button(c,"Clock: "+Store.Settings.Clock,b.X,y,b.Width,()=>{Store.Settings.Clock=(ClockMode)(((int)Store.Settings.Clock+1)%3);Changed();});y+=35;
            Button(c,"Category: "+Store.Settings.Category,b.X,y,b.Width,()=>Edit("Category (next attempt)",Store.Settings.Category,s=>Store.Settings.Category=s));y+=35;
            Button(c,"Editor key: "+((Keys)Store.Settings.EditorKey),b.X,y,b.Width,()=>Edit("Editor key",((Keys)Store.Settings.EditorKey).ToString(),s=>Store.Settings.EditorKey=(int)(Keys)Enum.Parse(typeof(Keys),s,true)));y+=35;
            Button(c,"Interface size: "+Store.Settings.UiScale.ToString("0.##"),b.X,y,b.Width,()=>Edit("Interface scale 0.65 - 1.15",Store.Settings.UiScale.ToString(CultureInfo.InvariantCulture),s=>Store.Settings.UiScale=float.Parse(s,CultureInfo.InvariantCulture)));y+=35;
            Button(c,"Delete layout",b.X,y,b.Width,()=>{if(Store.Settings.Layouts.Count<2){Controller.Status="Keep at least one layout";return;}Store.Settings.Layouts.Remove(Layout);Controller.Layout=Store.Settings.Layouts[0];Store.Settings.SelectedLayout=Layout.Id;history.Clear();selection.Clear();Changed();});return y+38;
        }
        private static float DrawRuns(Canvas c,Rectangle b,float y)
        {
            var archive=Store.Archive;if(archive==null)return y;c.Text(Widgets.CampaignLabel(archive.Route.Campaign),b.X,y,16,Gold);y+=29;
            Button(c,"Compare: "+(Store.Settings.Comparison.Length>12?"Selected run":Store.Settings.Comparison),b.X,y,b.Width,()=>{Store.Settings.Comparison=Store.Settings.Comparison=="PB"?"Previous":"PB";Changed();});y+=35;
            Button(c,"Export history",b.X,y,b.Width,()=>{Store.ExportRuns();Controller.Notify("Runs exported as XML and CSV");});y+=35;
            Button(c,"Save current replay",b.X,y,b.Width,()=>Controller.Notify(Adapters.SaveReplay()?"Replay snapshot queued":"Replays recording is unavailable"));y+=40;
            foreach(var run in archive.Runs.AsEnumerable().Reverse()){
                var item=run;string label=(run.Practice?"P ":"")+Records.Format(run.Elapsed.Value(Store.Settings.Clock))+"  "+run.Status;Button(c,label,b.X,y,b.Width,()=>{chosenRun=item.Id;});y+=34;
                if(chosenRun==run.Id){c.Text(c.Fit(run.StartedUtc, b.Width,12),b.X,y,12,Muted);y+=20;foreach(string flag in run.Flags){c.Text(c.Fit(flag,b.Width,12),b.X,y,12,Muted);y+=18;}foreach(var split in run.Splits){var area=archive.Route.Areas.FirstOrDefault(a=>a.Id==split.AreaId);c.Text(c.Fit((area==null?split.AreaId:area.Name)+" "+Records.Format(split.Duration.Value(Store.Settings.Clock)),b.Width,12),b.X,y,12,Text);y+=20;}
                    Button(c,"Compare",b.X,y,98,()=>{Store.Settings.Comparison=item.Id;Changed();});Button(c,"Note",b.X+104,y,98,()=>Edit("Run note",item.Note,s=>{item.Note=s;Store.Queue(System.IO.Path.Combine(Store.Root,"Runs",archive.Route.Key+".xml"),archive);},true));y+=34;
                    if(!string.IsNullOrEmpty(run.ReplayPath)){Button(c,"Watch",b.X,y,98,()=>Controller.Notify(Adapters.Watch(item.ReplayPath)?"Opening replay":"Replay is missing or incompatible"));Button(c,"Ghost",b.X+104,y,98,()=>Controller.Notify(Adapters.Ghost(item.ReplayPath)?"Ghost selected":"Ghost unavailable"));y+=34;}
                    if(!string.IsNullOrEmpty(run.Note)){c.Text(c.Fit(run.Note,b.Width,12),b.X,y,12,Muted);y+=20;}
                }
            }
            return y;
        }
        private static void DrawInspector(Canvas c)
        {
            c.Frame(rightPanel);c.Text("EDIT WIDGET",rightPanel.X+18,rightPanel.Y+12,20,Gold,"Location");
            for(int i=0;i<3;i++){int index=i;Button(c,new[]{"Place","Style","Content"}[i],rightPanel.X+8+i*87,rightPanel.Y+37,86,()=>{propertyTab=index;rightScroll=0;});}
            var b=new Rectangle(rightPanel.X+14,rightPanel.Y+77,rightPanel.Width-28,rightPanel.Height-89);c.Clip(b);int count=hits.Count;float y=b.Y-rightScroll;var selected=Selected();
            if(selected.Count==0){c.Wrapped("Select a widget on the canvas.\n\nF3 / right-click: add\nCtrl+D: duplicate\nCtrl+G: group\nCtrl+Shift+G: ungroup\nCtrl+Z / Y: undo / redo\nTab: hide panels\nAlt: bypass snapping\n\nController:\nLB / RB: select widget\nD-pad: move\nHold LS: resize\nHold RS + D-pad: UI focus\nRS + A: activate control\nX: add · Y: panels\nBack + Start: editor",b,16,Muted,"Menu","Left",false,false);c.Unclip();return;}
            if(selected.Count>1){c.Text(selected.Count+" widgets selected",b.X,y,18,Text);y+=35;Button(c,"Group",b.X,y,122,()=>Group(false));Button(c,"Ungroup",b.X+130,y,122,()=>Group(true));y+=38;foreach(string a in new[]{"Left","Right","Center X","Top","Bottom","Center Y","Distribute X","Distribute Y"}){string mode=a;Button(c,a,b.X,y,b.Width,()=>Align(mode));y+=35;}rightMax=Math.Max(0,y+rightScroll-b.Bottom);ClampHits(count,b);c.Unclip();return;}
            var w=selected[0];if(propertyTab==0){Row(c,b,ref y,"Name",w.Name,s=>w.Name=s);Toggle(c,b,ref y,"Visible",w.Enabled,()=>w.Enabled=!w.Enabled);Toggle(c,b,ref y,"Locked",w.Locked,()=>w.Locked=!w.Locked);
            Cycle(c,b,ref y,"Space",w.Space.ToString(),()=>{var r=Controller.Surface.Bounds(w);w.Space=(AnchorSpace)(((int)w.Space+1)%6);Controller.Surface.SetPosition(w,r.X,r.Y);});
            Cycle(c,b,ref y,"Anchor",AnchorName(w),()=>{var r=Controller.Surface.Bounds(w);int index=(int)Math.Round(w.AnchorY*2)*3+(int)Math.Round(w.AnchorX*2);index=(index+1)%9;w.AnchorX=(index%3)*.5f;w.AnchorY=(index/3)*.5f;Controller.Surface.SetPosition(w,r.X,r.Y);});
            Number(c,b,ref y,"X",w.X,v=>w.X=v);Number(c,b,ref y,"Y",w.Y,v=>w.Y=v);Number(c,b,ref y,"Width",w.Width,v=>w.Width=v);Number(c,b,ref y,"Height",w.Height,v=>w.Height=v);
            Cycle(c,b,ref y,"If no bar",w.Missing.ToString(),()=>w.Missing=w.Missing==MissingSpace.Hide?MissingSpace.MoveInside:MissingSpace.Hide);
            Cycle(c,b,ref y,"Show when",w.Visibility.ToString(),()=>w.Visibility=(Visibility)(((int)w.Visibility+1)%4));
            }
            if(propertyTab==1){
            Toggle(c,b,ref y,"Game frame",w.GameFrame,()=>w.GameFrame=!w.GameFrame);Number(c,b,ref y,"Opacity",w.Opacity,v=>w.Opacity=v);Number(c,b,ref y,"Padding",w.Padding,v=>w.Padding=v);Number(c,b,ref y,"Border",w.Border,v=>w.Border=v);
            ColorRow(c,b,ref y,"Text color",w.Foreground,v=>w.Foreground=v);ColorRow(c,b,ref y,"Background",w.Background,v=>w.Background=v);ColorRow(c,b,ref y,"Accent",w.Accent,v=>w.Accent=v);
            if(w.Kind!=WidgetKind.Panel&&w.Kind!=WidgetKind.Image&&w.Kind!=WidgetKind.Timer){Number(c,b,ref y,"Text size",w.TextSize,v=>w.TextSize=v);Cycle(c,b,ref y,"Font",w.Font,()=>w.Font=Next(w.Font,new[]{"Menu","Small","Location","Story","Gargoyle"}));Cycle(c,b,ref y,"Alignment",w.Align,()=>w.Align=Next(w.Align,new[]{"Left","Center","Right"}));Toggle(c,b,ref y,"Shadow",w.Shadow,()=>w.Shadow=!w.Shadow);Toggle(c,b,ref y,"Outline",w.Outline,()=>w.Outline=!w.Outline);}
            }
            if(propertyTab==2){
            if(w.Kind==WidgetKind.Text){Row(c,b,ref y,"Text",w.Text,s=>w.Text=s,true);Cycle(c,b,ref y,"Insert variable","Choose...",()=>popup="Variables");}
            if(w.Kind==WidgetKind.Timer)NativeTimerOptions(c,b,ref y);
            if(w.Kind==WidgetKind.Splits){Toggle(c,b,ref y,"Follow Area",w.Compact,()=>w.Compact=!w.Compact);Toggle(c,b,ref y,"Show delta",w.ShowDelta,()=>w.ShowDelta=!w.ShowDelta);Toggle(c,b,ref y,"Segment time",w.SegmentTimes,()=>w.SegmentTimes=!w.SegmentTimes);Number(c,b,ref y,"Visible rows",w.Rows,v=>w.Rows=(int)v);}
            if(w.Kind==WidgetKind.Image){Button(c,"Choose image",b.X,y,b.Width,()=>Browse(Store.Files("Images","*.*").Where(p=>new[]{".png",".jpg",".jpeg",".bmp"}.Contains(System.IO.Path.GetExtension(p).ToLowerInvariant())).ToArray(),p=>Change(()=>w.ImagePath=System.IO.Path.GetFileName(p))));y+=36;Row(c,b,ref y,"Image file",w.ImagePath,s=>w.ImagePath=s);c.Text("Local folder: OverlayPlus/Images",b.X,y,12,Muted);y+=24;}
            if(w.Kind==WidgetKind.External){Cycle(c,b,ref y,"Provider",Adapters.Name(w.Source),()=>{var names=Adapters.Names();if(names.Length>0)w.Source=Next(w.Source,names);});Row(c,b,ref y,"Source ID",w.Source,s=>w.Source=s);}
            if(w.Kind==WidgetKind.Button||w.Kind==WidgetKind.Inputs){Cycle(c,b,ref y,"Device",w.Device.ToString(),()=>w.Device=(InputDevice)(((int)w.Device+1)%4));Toggle(c,b,ref y,"Labels",w.ShowLabels,()=>w.ShowLabels=!w.ShowLabels);Toggle(c,b,ref y,"Bound keys",w.ShowBindings,()=>w.ShowBindings=!w.ShowBindings);Toggle(c,b,ref y,"Hold time",w.ShowDuration,()=>w.ShowDuration=!w.ShowDuration);Number(c,b,ref y,"Min flash (s)",w.MinimumPress,v=>w.MinimumPress=v);
                if(w.Kind==WidgetKind.Inputs){Cycle(c,b,ref y,"Layout",w.InputStyle.ToString(),()=>Defaults.SwitchControls(w,(InputStyle)(((int)w.InputStyle+1)%3)));Cycle(c,b,ref y,"Edit key",w.Keys.Count==0?"No keys":(Math.Min(keyIndex,w.Keys.Count-1)+1)+" / "+w.Keys.Count,()=>keyIndex=(keyIndex+1)%Math.Max(1,w.Keys.Count));Button(c,"+ key",b.X,y,122,()=>Change(()=>{w.Keys.Add(new InputKey{X=w.Keys.Count*62});keyIndex=w.Keys.Count-1;}));Button(c,"Remove key",b.X+130,y,122,()=>Change(()=>{if(w.Keys.Count>0)w.Keys.RemoveAt(Math.Min(keyIndex,w.Keys.Count-1));}));y+=36;
                    if(w.Keys.Count>0){var key=w.Keys[Math.Min(keyIndex,w.Keys.Count-1)];ActionRow(c,b,ref y,key.Source,s=>key.Source=s);Row(c,b,ref y,"Label",key.Label,s=>key.Label=s);Number(c,b,ref y,"Key X",key.X,v=>key.X=v);Number(c,b,ref y,"Key Y",key.Y,v=>key.Y=v);Number(c,b,ref y,"Key width",key.Width,v=>key.Width=v);Number(c,b,ref y,"Key height",key.Height,v=>key.Height=v);}
                }else {ActionRow(c,b,ref y,w.Source,s=>w.Source=s);Row(c,b,ref y,"Label",w.Text,s=>w.Text=s);}
                Button(c,"Learn physical key",b.X,y,b.Width,()=>{learn=true;learnReady=false;});y+=36;c.Wrapped("Actions: Left, Right, Jump, Boots, Snake\nPhysical: Key:A, Pad:A, Mouse:Left\nCustom: Action:mod.action",new Rectangle(b.X,(int)y,b.Width,80),13,Muted,"Menu","Left",false,false);y+=90;
            }
            }
            rightMax=Math.Max(0,y+rightScroll-b.Bottom);ClampHits(count,b);c.Unclip();
        }
        private static void NativeTimerOptions(Canvas c,Rectangle b,ref float y){Toggle(c,b,ref y,"Game timer",GameTimer.Enabled,()=>GameTimer.Enabled=!GameTimer.Enabled);Toggle(c,b,ref y,"Milliseconds",GameTimer.Precise,()=>GameTimer.Precise=!GameTimer.Precise);c.Wrapped("Uses the game timer and its own display settings.",new Rectangle(b.X,(int)y,b.Width,80),16,Muted,"Small","Left",false,false);y+=80;}
        private static string AnchorName(Widget w){return new[]{"Left","Center","Right"}[(int)Math.Round(w.AnchorX*2)]+" / "+new[]{"Top","Center","Bottom"}[(int)Math.Round(w.AnchorY*2)];}
        private static string Next(string current,string[] values){return values[(Array.IndexOf(values,current)+1)%values.Length];}
        private static void Row(Canvas c,Rectangle b,ref float y,string label,string value,Action<string> set,bool multi=false){c.Text(label,b.X,y+6,14,Muted);Button(c,(value??"").Replace('\n',' '),b.X+102,y,b.Width-102,()=>Edit(label,value,set,multi));y+=35;}
        private static void Number(Canvas c,Rectangle b,ref float y,string label,float value,Action<float> set){c.Text(label,b.X,y+6,14,Muted);float step=label=="Opacity"?.05f:label=="Min flash (s)"?.01f:Inputs.Shift?10:1;Button(c,"-",b.X+100,y,28,()=>Change(()=>set(value-step)));Button(c,value.ToString("0.###",CultureInfo.InvariantCulture),b.X+126,y,b.Width-157,()=>Edit(label,value.ToString(CultureInfo.InvariantCulture),s=>set(float.Parse(s,CultureInfo.InvariantCulture))));Button(c,"+",b.Right-29,y,29,()=>Change(()=>set(value+step)));y+=35;}
        private static void ActionRow(Canvas c,Rectangle b,ref float y,string source,Action<string> set){Cycle(c,b,ref y,"Action",source=="Snake"?"Ring":source,()=>set(Next(source,new[]{"Left","Right","Jump","Boots","Snake","Up","Down"})));Row(c,b,ref y,"Custom bind",source,set);}
        private static void Cycle(Canvas c,Rectangle b,ref float y,string label,string value,Action action){c.Text(label,b.X,y+6,14,Muted);Button(c,value,b.X+102,y,b.Width-102,()=>Change(action));y+=35;}
        private static void Toggle(Canvas c,Rectangle b,ref float y,string label,bool value,Action action){Cycle(c,b,ref y,label,value?"ON":"OFF",action);}
        private static void ColorRow(Canvas c,Rectangle b,ref float y,string label,uint value,Action<uint> set){Row(c,b,ref y,label,value.ToString("X8"),s=>set(uint.Parse(s.TrimStart('#'),NumberStyles.HexNumber)));uint[] colors={0xFFECE6D5,0xFFEDBB50,0xFF76BBF2,0xFF70D9AC,0xFFEF8995,0xCC181C24,0x00000000};for(int i=0;i<colors.Length;i++){uint color=colors[i];var r=new Rectangle(b.X+i*35,(int)y,29,18);c.Fill(r,Canvas.ColorOf(color));c.Border(r,Line);hits.Add(new Hit{Bounds=r,Action=()=>Change(()=>set(color))});}y+=26;}
        private static void ClampHits(int start,Rectangle bounds){for(int i=hits.Count-1;i>=start;i--){hits[i].Bounds=Rectangle.Intersect(hits[i].Bounds,bounds);if(hits[i].Bounds.Width<=0||hits[i].Bounds.Height<=0)hits.RemoveAt(i);}}
        private static void DrawPopup(Canvas c)
        {
            if(popup=="")return;int x=popup=="Variables"?Controller.Surface.Window.Width-535:155,y=50;var items=new List<Tuple<string,Action>>();
            if(popup=="Files"){x=Math.Max(20,(Controller.Surface.Window.Width-480)/2);c.Frame(new Rectangle(x-12,58,500,520));c.Text("CHOOSE FILE",x+8,72,23,Gold,"Location");if(browserFiles.Length==0)c.Wrapped("No files yet. Add images to OverlayPlus/Images or shared layouts to OverlayPlus/Imports.",new Rectangle(x+8,120,450,170),20,Text,"Menu","Left",false,false);for(int i=browserPage*10;i<Math.Min(browserFiles.Length,(browserPage+1)*10);i++){string path=browserFiles[i];Button(c,System.IO.Path.GetFileName(path),x+8,112+(i%10)*38,450,()=>{try{chooseFile(path);popup="";}catch(Exception e){Controller.Status=e.Message;}});}Button(c,"< Previous",x+8,514,150,()=>browserPage=Math.Max(0,browserPage-1));Button(c,"Next >",x+170,514,150,()=>browserPage=Math.Min(Math.Max(0,(browserFiles.Length-1)/10),browserPage+1));Button(c,"Close",x+335,514,118,()=>popup="");return;}
            if(popup=="Add")foreach(WidgetKind kind in Enum.GetValues(typeof(WidgetKind))){WidgetKind k=kind;items.Add(Tuple.Create(kind.ToString(),(Action)(()=>Add(k))));}
            else if(popup=="Align")foreach(string mode in new[]{"Left","Right","Center X","Top","Bottom","Center Y","Distribute X","Distribute Y"}){string m=mode;items.Add(Tuple.Create(mode,(Action)(()=>Align(m))));}
            else if(popup=="Variables")foreach(string variable in Widgets.Variables){string v=variable;items.Add(Tuple.Create("{"+v+"}",(Action)(()=>{var w=Selected().FirstOrDefault();if(w!=null)Change(()=>w.Text+="{"+v+"}");popup="";})));}
            int columns=items.Count>14?2:1,rows=(int)Math.Ceiling(items.Count/(float)columns);c.Frame(new Rectangle(x-8,y-4,columns*202+16,rows*34+12));for(int i=0;i<items.Count;i++)Button(c,items[i].Item1,x+(i/rows)*202,y+(i%rows)*34,196,items[i].Item2);
        }
        private static void Browse(string[] paths,Action<string> choose){browserFiles=paths;browserPage=0;chooseFile=choose;popup="Files";}
        private static void DrawField(Canvas c)
        {
            // Inline text inspector; the scene remains visible and editable after commit.
            int width=Math.Min(520,Controller.Surface.Window.Width-32),height=field.Multiline?220:100;var r=new Rectangle(Controller.Surface.Window.Width-width-16,64,width,height);c.Frame(r);c.Text(field.Label,r.X+12,r.Y+8,20,Gold,"Location");
            var content=new Rectangle(r.X+12,r.Y+39,r.Width-24,r.Height-48);if(field.All)c.Fill(content,new Color(41,70,100));string value=field.Value.Insert(Math.Min(field.Caret,field.Value.Length),((int)(Controller.Now*2)%2)==0?"|":"");c.Clip(content);c.Wrapped(value,content,20,Text,"Small","Left",false,false);c.Unclip();
        }
    }
}
