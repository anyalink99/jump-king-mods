using System;
using Microsoft.Xna.Framework.Input;

namespace MultiplayerExpansion
{
    internal static class FrameKeyboardTests
    {
        internal static void Run()
        {
            int polls=0;bool down=true;
            var keyboard=new FrameKeyboard(key=>{polls++;return down ? unchecked((short)0x8000) : (short)0;},key=>key==ClientSwitchKey.Key || SecondaryControls.Reserved(key));
            var first=keyboard.Read(true);int once=polls;
            Check(first.IsKeyDown(Keys.Space) && !first.IsKeyDown(Keys.D6) && !first.IsKeyDown(Keys.OemOpenBrackets),"Keyboard snapshot lost native or reserved keys");
            for(int i=0;i<100;i++) Check(keyboard.Read(true).IsKeyDown(Keys.Space),"Repeated poll changed the same simulation snapshot");
            Check(polls==once,"UI and native polling repeated the full OS scan in one step");
            down=false;keyboard.Begin();Check(keyboard.Read(true).GetPressedKeys().Length==0 && polls==once*2,"Next simulation step didn't refresh released keys");
            down=true;Check(keyboard.Read(false).GetPressedKeys().Length==0 && polls==once*2,"Inactive keyboard polled the OS or retained pressed keys");
            Check(keyboard.Read(true).IsKeyDown(Keys.Space) && polls==once*3,"Focus return reused the old keyboard snapshot");
            Console.WriteLine("[OK] Frame keyboard: one OS scan across 100 consumers, fresh steps, focus loss and reserved controls");
        }
        private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    }
}
