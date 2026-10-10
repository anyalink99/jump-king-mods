using System;
using System.Diagnostics;
using HarmonyLib;
using JumpKing;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JKRuntime;
using JKRuntime.Modules;

namespace MultiplayerExpansion
{
    [RuntimeModule("anyalink.multiplayer-expansion.world", "Multiplayer Expansion")]
    public static class NativeMod
    {
        private static bool debugRegistered;
        internal static string Package { get { return PackageHost.GetDataDirectory(typeof(NativeMod).Assembly); } }
        internal static bool DebugEnabled { get { return LevelDebugState.instance != null; } }
        [BeforeLevelLoad]
        public static void Initialize()
        {
            AdvancedSession.Initialize();
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MPEX_ROLE"))) { NativeSession.Status = "Second client"; return; }
            if (!DebugEnabled) return;
            if (debugRegistered) return;
            debugRegistered = true;
            Game1.callbackManager.CreateRoutine(int.MaxValue, NativeSession.Tick);
        }
        [OnWorldReady]
        public static void Prepare(RuntimeScope scope) { WorldLifecycle.Prepare(scope); }
        [BeforeAttempt]
        public static void Attempt(RuntimeScope scope) { WorldLifecycle.Attempt(scope); }
        [OnLevelStart]
        public static void Activate(ModuleContext context)
        { WorldLifecycle.Activate(context);context.Track(FrameComposition.RegisterWorldDraw(RemotePresentation.WorldDraw)); }
        [MainMenuItemSetting]
        public static TextButton MainSettings(object factory, GuiFormat format) { return Settings(factory, format); }
        [PauseMenuItemSetting]
        public static TextButton PauseSettings(object factory, GuiFormat format) { return Settings(factory, format); }
        public static TextButton Settings(object factory, GuiFormat format)
        {
            var button=new TextButton("Multiplayer settings", JKRuntime.UI.UIApi.CreateMenuPage(factory, new JKRuntime.UI.UiPageStack(pages=>new MultiplayerPage(pages))));
            JKRuntime.UI.NativeMenuRows.After(button,TeleportRows);
            return button;
        }
        private static System.Collections.Generic.IEnumerable<JKRuntime.UI.NativeMenuRow> TeleportRows()
        {
            foreach(var peer in System.Linq.Enumerable.OrderBy(AdvancedSession.WorldPeers,p=>p.Id)) {
                ulong id=peer.Id;
                string name=AdvancedSession.InLab ? "Test Client "+id : Steamworks.SteamFriends.GetFriendPersonaName(new Steamworks.CSteamID(id));
                yield return new JKRuntime.UI.NativeMenuRow(id.ToString(),"Teleport to "+name,delegate {
                    if(AdvancedSession.TeleportTo(id)) JKRuntime.UI.UIApi.ClosePauseMenu();
                });
            }
        }
    }

}
