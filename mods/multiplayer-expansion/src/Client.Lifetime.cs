using System;
using System.Diagnostics;
using System.Threading;
using JumpKing;

namespace MultiplayerExpansion
{
    internal static partial class Client
    {
        private static void WatchParent()
        {
            int parentId;
            if (!int.TryParse(Environment.GetEnvironmentVariable("MPEX_PARENT_ID"), out parentId)) return;
            Process parent;
            try { parent = Process.GetProcessById(parentId); }
            catch (ArgumentException) { throw new InvalidOperationException("The main game has already closed"); }
            new Thread(delegate() {
                try { using (parent) parent.WaitForExit(); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                if (Game1.instance != null) Game1.instance.Exit();
                // the game normally saves and exits on its next tick; don't leave a stuck helper behind
                Thread.Sleep(8000);
                Environment.Exit(0);
            }) { IsBackground = true, Name = "Multiplayer parent lifetime" }.Start();
        }
    }
}
