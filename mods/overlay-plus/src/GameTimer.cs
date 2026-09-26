using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OverlayPlus
{
    // Redirect only the game's timer call. Its clock, format, precision and visibility
    // remain owned by Jump King; foreign postfixes on the containing method still run.
    internal static class GameTimer
    {
        private static readonly NativeCapture capture = new NativeCapture();
        private static bool current;
        private static readonly PropertyInfo general = Native.Game.GetType("JumpKing.SaveThread.SaveLube",true).GetProperty("generalSettings",Native.Flags);
        internal static bool Enabled { get { return Setting("gui_use_timer"); } set { Setting("gui_use_timer",value); } }
        internal static bool Precise { get { return Setting("gui_timer_precise"); } set { Setting("gui_timer_precise",value); } }
        private static bool Setting(string name) { var settings=general.GetValue(null,null);return (bool)settings.GetType().GetField(name,Native.Flags).GetValue(settings); }
        private static void Setting(string name,bool value) { var settings=general.GetValue(null,null);settings.GetType().GetField(name,Native.Flags).SetValue(settings,value);general.SetValue(null,settings,null); }
        internal static void BeginFrame(){current=false;}
        internal static void Release(){capture.Reset();current=false;}
        internal static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            int replaced=0;var native=typeof(JumpKing.Util.TextHelper).GetMethod("DrawString");
            foreach(var instruction in instructions){if(instruction.operand as MethodInfo==native){instruction.opcode=OpCodes.Call;instruction.operand=typeof(GameTimer).GetMethod("Capture",Native.Flags);replaced++;}yield return instruction;}
            if(replaced!=1)throw new NotSupportedException("Native timer draw contract changed");
        }
        internal static void Capture(SpriteFont font,string text,Vector2 position,Color color,Vector2 center,bool outlined)
        {
            if(!Controller.OwnsNativeTimer){JumpKing.Util.TextHelper.DrawString(font,text,position,color,center,outlined);return;}
            capture.Begin();capture.Add(font,text,position,color,center,outlined);capture.End();current=true;
        }
        internal static bool HasDrawing { get { return current; } }
        internal static void Draw(Canvas canvas,Widget widget,Rectangle bounds)
        {
            if(current || Editor.Open&&capture.Ready)capture.Draw(canvas.Batch,bounds,widget.Opacity);
            else if(Editor.Open)canvas.Text("Native timer",bounds.X,bounds.Y,18,Color.White,"Small");
        }
    }
}
