using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JKRuntime.Input;
using JKRuntime.UI;
using JumpKing;
using JumpKing.Controller;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OverlayPlus
{
    internal sealed class InputState
    {
        internal bool Down;
        internal double Started,Released,Duration;
        internal string Label="";
    }
    internal static class Inputs
    {
        internal static KeyboardState Keyboard, PreviousKeyboard;
        internal static MouseState Mouse,PreviousMouse;
        internal static GamePadState Pad,PreviousPad;
        internal static Vector2 Pointer;
        internal static double Now;
        private static readonly Dictionary<string,InputState> states=new Dictionary<string,InputState>();
        private static readonly Dictionary<PadInstance,int[]> physical=new Dictionary<PadInstance,int[]>();
        private static readonly Dictionary<IPad,int[]> nativePhysical=new Dictionary<IPad,int[]>();
        private static readonly int[] Empty=new int[0];
        private static readonly HashSet<int> XboxButtons=new HashSet<int>(Enum.GetValues(typeof(Buttons)).Cast<Buttons>().Select(b=>(int)b));
        private static readonly Dictionary<Keys,double> repeats=new Dictionary<Keys,double>();
        internal static bool Focused;
        internal static void Poll(Surface surface,double now)
        {
            Now=now;PreviousKeyboard=Keyboard;PreviousMouse=Mouse;PreviousPad=Pad;
            Focused=Game1.instance!=null&&Game1.instance.IsActive&&!SteamOverlay();
            Keyboard=Focused?Microsoft.Xna.Framework.Input.Keyboard.GetState():new KeyboardState();
            Mouse=Microsoft.Xna.Framework.Input.Mouse.GetState();Pad=Focused?NativeGamepad():new GamePadState();
            // Native mouse coordinates are client pixels, including pillarbox bars.
            var client=Game1.instance.Window.ClientBounds;var pp=Game1.instance.GraphicsDevice.PresentationParameters;
            Pointer=MapPointer(Mouse.X,Mouse.Y,client.Width,client.Height,pp.BackBufferWidth,pp.BackBufferHeight,surface.Scale);
            if(!Focused){foreach(var s in states.Values){s.Down=false;s.Duration=0;s.Released=0;}repeats.Clear();}
        }
        private static readonly System.Reflection.FieldInfo steam=typeof(PadInstance).GetField("_steam_overlay_active",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        private static bool SteamOverlay(){return steam!=null&&(bool)steam.GetValue(null);}
        internal static bool Ctrl {get{return Keyboard.IsKeyDown(Keys.LeftControl)||Keyboard.IsKeyDown(Keys.RightControl);}}
        internal static bool Shift {get{return Keyboard.IsKeyDown(Keys.LeftShift)||Keyboard.IsKeyDown(Keys.RightShift);}}
        internal static bool Alt {get{return Keyboard.IsKeyDown(Keys.LeftAlt)||Keyboard.IsKeyDown(Keys.RightAlt);}}
        internal static bool Press(Keys k){return Keyboard.IsKeyDown(k)&&PreviousKeyboard.IsKeyUp(k);}
        internal static bool Repeat(Keys k){if(!Keyboard.IsKeyDown(k)){repeats.Remove(k);return false;}double next;if(!repeats.TryGetValue(k,out next)){repeats[k]=Now+.35;return true;}if(Now<next)return false;repeats[k]=Now+.05;return true;}
        internal static bool PadPress(Buttons b){return Pad.IsButtonDown(b)&&PreviousPad.IsButtonUp(b);}
        internal static bool Click {get{return Focused&&Mouse.LeftButton==ButtonState.Pressed&&PreviousMouse.LeftButton==ButtonState.Released;}}
        internal static bool RightClick {get{return Focused&&Mouse.RightButton==ButtonState.Pressed&&PreviousMouse.RightButton==ButtonState.Released;}}
        internal static bool Release {get{return Mouse.LeftButton==ButtonState.Released&&PreviousMouse.LeftButton==ButtonState.Pressed;}}
        internal static bool AllReleased()
        {if(Keyboard.GetPressedKeys().Length!=0||Mouse.LeftButton==ButtonState.Pressed||Mouse.RightButton==ButtonState.Pressed)return false;foreach(var buttons in nativePhysical.Values)if(buttons.Length!=0)return false;return true;}
        internal static void BeginNativeSample(){nativePhysical.Clear();}
        internal static int[] CapturePhysical(IPad pad)
        {
            // This replaces the existing native read, not an additional driver poll.
            // Keep the game's exact return value; the observer owns a detached snapshot.
            var buttons=pad.GetPressedButtons();if(Controller.Active)nativePhysical[pad]=buttons==null||buttons.Length==0?Empty:(int[])buttons.Clone();return buttons;
        }
        internal static IEnumerable<CodeInstruction> ObserveNative(IEnumerable<CodeInstruction> instructions)
        {
            int count=0;var method=typeof(IPad).GetMethod("GetPressedButtons");
            foreach(var instruction in instructions){if(instruction.operand as MethodInfo==method){instruction.opcode=OpCodes.Call;instruction.operand=typeof(Inputs).GetMethod("CapturePhysical",Native.Flags);count++;}yield return instruction;}
            if(count!=1)throw new NotSupportedException("Overlay+: native input sample contract changed");
        }
        private static int[] NativeButtons(IPad pad){int[] buttons;return nativePhysical.TryGetValue(pad,out buttons)?buttons:Empty;}
        private static GamePadState NativeGamepad()
        {
            foreach(var p in BindingSnapshot.Registered())if(p.IsValid&&p.GetPad().GetSaveIdentifier()=="xbox_pad_jump_kingOne"){
                Buttons buttons=0;foreach(int code in NativeButtons(p.GetPad()))if(XboxButtons.Contains(code))buttons|=(Buttons)code;
                return new GamePadState(Vector2.Zero,Vector2.Zero,0,0,buttons);
            }
            return new GamePadState();
        }
        internal static bool HasWidgets(IEnumerable<Widget> widgets){return widgets.Any(w=>w.Enabled&&(w.Kind==WidgetKind.Inputs||w.Kind==WidgetKind.Button||w.Visibility==Visibility.Pressed));}
        internal static void Update(IEnumerable<Widget> widgets)
        {
            physical.Clear();
            foreach(var w in widgets){if(!w.Enabled)continue;if(w.Kind==WidgetKind.Inputs)foreach(var k in w.Keys)Observe(k.Source,w.Device);if(w.Kind==WidgetKind.Button||w.Visibility==Visibility.Pressed)Observe(w.Source,w.Device);}
        }
        private static string Key(string source,InputDevice device){return device+":"+source;}
        internal static InputState Get(string source,InputDevice device){InputState state;return states.TryGetValue(Key(source,device),out state)?state:new InputState {Label=source};}
        internal static void Observe(string source,InputDevice device)
        {
            string key=Key(source,device);InputState state;if(!states.TryGetValue(key,out state)){state=new InputState();states[key]=state;}
            string label=source;bool down=Focused&&Read(source,device,out label);if(!Focused)label=source;
            if(down&&!state.Down)state.Started=Now;
            if(!down&&state.Down){state.Released=Now;state.Duration=Now-state.Started;}
            state.Down=down;if(down)state.Duration=Now-state.Started;state.Label=label;
        }
        private static bool Read(string source,InputDevice device,out string label)
        {
            label=source??"";source=source??"";
            if(source.StartsWith("Key:",StringComparison.Ordinal)){Keys key;if(Enum.TryParse(source.Substring(4),true,out key)){label=key.ToString();return Keyboard.IsKeyDown(key);}return false;}
            if(source.StartsWith("Mouse:",StringComparison.Ordinal)){label=source.Substring(6);return label=="Left"?Mouse.LeftButton==ButtonState.Pressed:label=="Right"?Mouse.RightButton==ButtonState.Pressed:label=="Middle"?Mouse.MiddleButton==ButtonState.Pressed:label=="X1"?Mouse.XButton1==ButtonState.Pressed:label=="X2"&&Mouse.XButton2==ButtonState.Pressed;}
            if(source.StartsWith("Pad:",StringComparison.Ordinal)){Buttons button;label=source.Substring(4);return Enum.TryParse(label,true,out button)&&Pad.IsConnected&&Pad.IsButtonDown(button);}
            if(source.StartsWith("Device:",StringComparison.Ordinal)){int separator=source.LastIndexOf('|'),code;if(separator<7||!int.TryParse(source.Substring(separator+1),out code))return false;string id=source.Substring(7,separator-7);foreach(var p in BindingSnapshot.Registered())if(p.IsValid&&p.GetPad().GetSaveIdentifier()==id){label=p.GetPad().ButtonToString(code);return Array.IndexOf(Pressed(p),code)>=0;}return false;}
            if(source.StartsWith("Action:",StringComparison.Ordinal)){try{return ActionInputs.Read(source.Substring(7)).Down;}catch(KeyNotFoundException){return false;}}
            JKpadButtons action;if(!Enum.TryParse(source,true,out action))return false;
            bool result=false;
            foreach(var p in BindingSnapshot.Registered()){
                if(!p.IsValid||p.GetBind()==null||!p.GetBind().Enabled)continue;
                bool keyboard=p.GetPad().GetSaveIdentifier()=="pc_keyboard_jump_king";
                if(device==InputDevice.Keyboard&&!keyboard||device==InputDevice.Controller&&keyboard)continue;
                int[] pressed=Pressed(p);
                var bind=p.GetBind().GetButtonBind(action);result|=Matches(pressed,bind);
                if(bind!=null&&bind.Length>0)label=string.Join(" / ",bind.Select(p.GetPad().ButtonToString));
            }
            return result;
        }
        private static int[] Pressed(PadInstance pad){int[] pressed;if(!physical.TryGetValue(pad,out pressed)){var device=pad.GetPad();pressed=device.GetSaveIdentifier()=="pc_keyboard_jump_king"?device.GetPressedButtons()??Empty:NativeButtons(device);physical[pad]=pressed;}return pressed;}
        internal static Vector2 MapPointer(int x,int y,int clientWidth,int clientHeight,int bufferWidth,int bufferHeight,float scale)
        {return new Vector2(x*bufferWidth/(float)Math.Max(1,clientWidth)/scale,y*bufferHeight/(float)Math.Max(1,clientHeight)/scale);}
        internal static bool Matches(int[] pressed,int[] bindings){return bindings!=null&&pressed!=null&&bindings.Any(b=>Array.IndexOf(pressed,b)>=0);}
        internal static bool DisplayPressed(InputState state,float minimum){return Focused&&(state.Down||minimum>0&&state.Released>0&&Now-state.Started<minimum);}
        internal static void SampleForDraw(IEnumerable<Widget> widgets,double now)
        {
            Now=now;Focused=Game1.instance!=null&&Game1.instance.IsActive&&!SteamOverlay();
            var keyboard=Keyboard;var mouse=Mouse;var pad=Pad;
            try{Keyboard=Focused?Microsoft.Xna.Framework.Input.Keyboard.GetState():new KeyboardState();Mouse=Microsoft.Xna.Framework.Input.Mouse.GetState();Pad=Focused?NativeGamepad():new GamePadState();Update(widgets);}
            finally{Keyboard=keyboard;Mouse=mouse;Pad=pad;}
        }
        internal static string Learn()
        {
            foreach(var k in Keyboard.GetPressedKeys())if(PreviousKeyboard.IsKeyUp(k)&&k!=Keys.Escape)return "Key:"+k;
            if(Click)return "Mouse:Left";if(RightClick)return "Mouse:Right";if(Mouse.MiddleButton==ButtonState.Pressed&&PreviousMouse.MiddleButton==ButtonState.Released)return "Mouse:Middle";if(Mouse.XButton1==ButtonState.Pressed&&PreviousMouse.XButton1==ButtonState.Released)return "Mouse:X1";if(Mouse.XButton2==ButtonState.Pressed&&PreviousMouse.XButton2==ButtonState.Released)return "Mouse:X2";
            foreach(var p in BindingSnapshot.Registered())if(p.IsValid&&p.GetPad().GetSaveIdentifier()!="pc_keyboard_jump_king"){var buttons=NativeButtons(p.GetPad());if(buttons.Length>0)return "Device:"+p.GetPad().GetSaveIdentifier()+"|"+buttons[0];}
            return null;
        }
        internal static void Clear(){states.Clear();physical.Clear();nativePhysical.Clear();repeats.Clear();Keyboard=PreviousKeyboard=new KeyboardState();Pad=PreviousPad=new GamePadState();}
    }
    internal sealed class TextInputWindow:System.Windows.Forms.NativeWindow,IDisposable
    {
        internal readonly Queue<char> Characters=new Queue<char>();
        internal TextInputWindow(IntPtr handle){AssignHandle(handle);}
        protected override void WndProc(ref System.Windows.Forms.Message m){if(m.Msg==0x102&&Editor.Open&&Characters.Count<1024){char c=(char)m.WParam.ToInt32();if(c>=32||c=='\r')Characters.Enqueue(c);}base.WndProc(ref m);}
        public void Dispose(){ReleaseHandle();Characters.Clear();}
    }
}
