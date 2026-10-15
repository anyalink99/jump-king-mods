using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;

namespace OverlayPlus
{
    public enum WidgetKind { Timer, Splits, Inputs, Button, Text, Stats, Charge, Equipment, Image, Panel, External }
    public enum AnchorSpace { Window, Game, LeftBar, RightBar, TopBar, BottomBar }
    public enum MissingSpace { MoveInside, Hide }
    public enum Visibility { Always, Running, Finished, Pressed }
    public enum ClockMode { GameTime, RealTime, ActiveTime }
    public enum Campaign { MainBabe, NewBabePlus, GhostOfTheBabe, Custom }
    public enum InputStyle { Row, Keyboard, Controller }
    public enum InputDevice { Auto, Keyboard, Controller, All }

    [Serializable] public sealed class InputKey
    {
        public string Source = "Jump", Label = "";
        public float X, Y, Width = 56, Height = 48;
    }
    [Serializable] public sealed class ControlSize
    { public InputStyle Style; public float Width,Height; }
    [Serializable] public sealed class Widget
    {
        public string Id = Guid.NewGuid().ToString("N"), Name = "Widget", Group = "";
        public WidgetKind Kind;
        public AnchorSpace Space = AnchorSpace.Window;
        public MissingSpace Missing = MissingSpace.MoveInside;
        public float AnchorX, AnchorY, X = 24, Y = 90, Width = 220, Height = 60;
        public float Opacity = 1, TextSize = 22, Padding = 16, Border = 1, MinimumPress = 0;
        public uint Foreground = 0xFFECE6D5, Background = 0xCC000000, Accent = 0xFFEDBB50;
        public bool Enabled = true, Locked, Shadow = true, Outline, ShowLabels = true, ShowBindings = true, ShowDuration;
        public string Text = "Overlay+", Source = "GameTime", ImagePath = "", Font = "Menu", Align = "Left";
        public Visibility Visibility;
        public InputStyle InputStyle;
        public List<ControlSize> ControlSizes = new List<ControlSize>();
        public InputDevice Device;
        public bool Compact = true, ShowDelta = true, SegmentTimes, GameFrame = true;
        public int Rows = 9;
        public List<InputKey> Keys = new List<InputKey>();
        public Widget Clone() { return XmlData.Clone(this); }
    }
    [Serializable] public sealed class Layout
    {
        public string Id = Guid.NewGuid().ToString("N"), Name = "Default", Map = "", Aspect = "Any";
        public bool FitPresetOnFirstUse;
        public List<Widget> Widgets = new List<Widget>();
        public Layout Clone() { return XmlData.Clone(this); }
    }
    [Serializable] public sealed class SettingsData
    {
        public int Version = 1;
        public int PresentationRevision;
        public bool ModuleEnabled = true;
        public bool Enabled = true, Grid = true, Snap = true;
        public int GridSize = 8, EditorKey = 121;
        public float UiScale = 1;
        public ClockMode Clock;
        public string SelectedLayout = "", Comparison = "PB", Category = "Default";
        public List<Layout> Layouts = new List<Layout>();
        public List<Widget> Templates = new List<Widget>();
        public List<Layout> GroupTemplates = new List<Layout>();
    }
    internal static class Defaults
    {
        internal static List<InputKey> Keys()
        { return new List<InputKey> { new InputKey { Source="Left" }, new InputKey { Source="Right",X=64 }, new InputKey { Source="Jump",X=0,Y=56,Width=120 }, new InputKey { Source="Boots",X=144 }, new InputKey { Source="Snake",Label="Ring",X=208 } }; }
        internal static Layout Preset(string name,Surface? surface=null)
        {
            var l = new Layout { Name = name, FitPresetOnFirstUse=!surface.HasValue };
            l.Widgets.Add(new Widget { Name="Game timer",Kind=WidgetKind.Timer,Width=220,Height=32,Space=AnchorSpace.Game,AnchorX=1,X=-16,Y=16,Padding=0,Border=0,GameFrame=false,Background=0 });
            if (name == "Minimal") return l;
            l.Widgets.Add(new Widget { Name="Area Splits",Kind=WidgetKind.Splits,Width=230,Height=420,Space=AnchorSpace.RightBar,AnchorX=1, X=-8,Y=90,TextSize=18,Font="Small",Padding=12 });
            l.Widgets.Add(new Widget { Name="Controls",Kind=WidgetKind.Inputs,Width=356,Height=name=="Controller"?180:80,Space=AnchorSpace.Game,AnchorX=.5f,AnchorY=1,X=0,Y=-20,TextSize=18,Font="Small",ShowBindings=false,Padding=8,GameFrame=false,Border=0,Background=0,Keys=Keys(),InputStyle=name=="Controller"?InputStyle.Controller:InputStyle.Row });
            if(name=="Practice") { l.Widgets.Add(new Widget { Name="Statistics",Kind=WidgetKind.Stats,Width=220,Height=150,Space=AnchorSpace.LeftBar,TextSize=18 }); l.Widgets.Add(new Widget {Name="Jump charge",Kind=WidgetKind.Charge,Width=220,Height=72,Y=270}); }
            if(surface.HasValue)FitPreset(l,surface.Value);
            return l;
        }
        internal static void FitPreset(Layout l,Surface surface)
        {
            foreach(var w in l.Widgets){
                if(w.Kind==WidgetKind.Splits){int bar=surface.Area(AnchorSpace.RightBar).Width;w.Width=bar>=140?Math.Min(260,bar-12):230;w.TextSize=w.Width<190?17:20;w.Y=76;w.Height=Math.Min(440,surface.Window.Height-124);}
                if(w.Kind==WidgetKind.Stats){int bar=surface.Area(AnchorSpace.LeftBar).Width;if(bar>=140){w.Width=bar-12;w.X=6;w.TextSize=16;w.Font="Small";}else{w.Space=AnchorSpace.Game;w.X=16;w.Y=80;}}
                var bounds=surface.Bounds(w);
                if(!surface.Window.Contains(bounds))surface.SetPosition(w,Math.Max(0,Math.Min(surface.Window.Width-bounds.Width,bounds.X)),Math.Max(0,Math.Min(surface.Window.Height-bounds.Height,bounds.Y)));
            }
        }
        internal static void SwitchControls(Widget w,InputStyle style)
        {
            if(w.InputStyle==style)return;
            w.ControlSizes=w.ControlSizes??new List<ControlSize>();
            var previous=w.ControlSizes.FirstOrDefault(s=>s.Style==w.InputStyle);
            if(previous==null){previous=new ControlSize{Style=w.InputStyle};w.ControlSizes.Add(previous);}
            previous.Width=w.Width;previous.Height=w.Height;
            var saved=w.ControlSizes.FirstOrDefault(s=>s.Style==style);
            w.InputStyle=style;w.Width=saved==null?(style==InputStyle.Keyboard?344:356):saved.Width;w.Height=saved==null?(style==InputStyle.Row?80:style==InputStyle.Keyboard?136:180):saved.Height;
        }
        internal static SettingsData Settings()
        { var s=new SettingsData{PresentationRevision=2}; s.Layouts.Add(Preset("Sidebars")); s.SelectedLayout=s.Layouts[0].Id; return s; }
        internal static Widget Make(WidgetKind kind)
        { var w = new Widget {Kind=kind,Name=kind.ToString()}; if(kind==WidgetKind.Timer){w.Name="Game timer";w.Width=220;w.Height=32;w.Padding=0;w.Border=0;w.GameFrame=false;w.Background=0;} if(kind==WidgetKind.Splits){w.Height=440;w.Width=250;w.TextSize=17;} if(kind==WidgetKind.Inputs){w.Width=356;w.Height=80;w.TextSize=18;w.Font="Small";w.Padding=8;w.GameFrame=false;w.Border=0;w.Background=0;w.ShowBindings=false;w.Keys=Keys();} if(kind==WidgetKind.Stats)w.Height=150;if(kind==WidgetKind.Equipment)w.Height=100;if(kind==WidgetKind.Charge)w.Height=72; if(kind==WidgetKind.Button){w.Width=80;w.Height=64;w.Source="Jump";} if(kind==WidgetKind.Image){w.Width=160;w.Height=160;} return w; }
    }
    internal static class XmlData
    {
        internal static T Clone<T>(T value) { using(var m=new MemoryStream()){new XmlSerializer(typeof(T)).Serialize(m,value);m.Position=0;return (T)new XmlSerializer(typeof(T)).Deserialize(m);} }
        internal static string Serialize<T>(T value){using(var w=new StringWriter()){new XmlSerializer(typeof(T)).Serialize(w,value);return w.ToString();}}
        internal static T Parse<T>(string text){using(var r=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings { DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=typeof(T)==typeof(RunArchive)?256000000:16000000 })){return (T)new XmlSerializer(typeof(T)).Deserialize(r);}}
    }
    internal struct Surface
    {
        internal Rectangle Window, Game;
        internal float Scale;
        internal Rectangle Area(AnchorSpace space)
        {
            switch(space){case AnchorSpace.Game:return Game;case AnchorSpace.LeftBar:return new Rectangle(0,0,Math.Max(0,Game.Left),Window.Height);case AnchorSpace.RightBar:return new Rectangle(Game.Right,0,Math.Max(0,Window.Right-Game.Right),Window.Height);case AnchorSpace.TopBar:return new Rectangle(Game.Left,0,Game.Width,Math.Max(0,Game.Top));case AnchorSpace.BottomBar:return new Rectangle(Game.Left,Game.Bottom,Game.Width,Math.Max(0,Window.Bottom-Game.Bottom));default:return Window;}
        }
        internal Rectangle Bounds(Widget w)
        {
            var a=Area(w.Space); if(a.Width<12||a.Height<12){if(w.Missing==MissingSpace.Hide)return Rectangle.Empty;a=Game;}
            int width=(int)Math.Round(w.Width),height=(int)Math.Round(w.Height);
            return new Rectangle((int)Math.Round(a.X+w.AnchorX*(a.Width-width)+w.X),(int)Math.Round(a.Y+w.AnchorY*(a.Height-height)+w.Y),width,height);
        }
        internal void SetPosition(Widget w, float x,float y)
        {var a=Area(w.Space);if(a.Width<12||a.Height<12)a=Game;w.X=x-a.X-w.AnchorX*(a.Width-w.Width);w.Y=y-a.Y-w.AnchorY*(a.Height-w.Height);}
        internal void Rescue(Widget w){var r=Bounds(w);if(r==Rectangle.Empty){w.Space=AnchorSpace.Game;r=Bounds(w);}SetPosition(w,Math.Max(0,Math.Min(Window.Width-Math.Min(r.Width,Window.Width),r.X)),Math.Max(48,Math.Min(Window.Height-40-Math.Min(r.Height,Window.Height-88),r.Y)));}
        internal string Aspect {get{return Window.Width/(float)Math.Max(1,Window.Height)>1.9f?"Ultrawide":Window.Width/(float)Math.Max(1,Window.Height)>1.5f?"Wide":"4:3";}}
    }
    internal sealed class EditHistory
    {
        private readonly Stack<Layout> undo=new Stack<Layout>(), redo=new Stack<Layout>();
        internal void Push(Layout l){undo.Push(l.Clone());redo.Clear();if(undo.Count>100){var items=undo.Take(100).Reverse().ToArray();undo.Clear();foreach(var i in items)undo.Push(i);}}
        internal Layout Undo(Layout l){if(undo.Count==0)return l;redo.Push(l.Clone());return undo.Pop();}
        internal Layout Redo(Layout l){if(redo.Count==0)return l;undo.Push(l.Clone());return redo.Pop();}
        internal void Clear(){undo.Clear();redo.Clear();}
    }
}
