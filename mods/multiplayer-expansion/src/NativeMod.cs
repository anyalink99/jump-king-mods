using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Windows.Forms;
using BehaviorTree;
using HarmonyLib;
using JumpKing;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT;
using JumpKingMultiplayer.Models;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace MultiplayerExpansion
{
    [JumpKingMod("Multiplayer Expansion")]
    public static class NativeMod
    {
        private static Harmony debugHooks;
        private static readonly RenderCadence cadence = new RenderCadence();
        private static readonly Stopwatch renderClock = Stopwatch.StartNew();
        internal static bool DebugEnabled { get { return LevelDebugState.instance != null; } }
        [BeforeLevelLoad]
        public static void Initialize()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MPEX_ROLE"))) { NativeSession.Status = "Second client"; return; }
            if (!DebugEnabled) return;
            if (debugHooks != null) return;
            debugHooks = new Harmony("anyalink.multiplayer-expansion.debug");
            debugHooks.Patch(AccessTools.Method(typeof(Microsoft.Xna.Framework.Game), "DoDraw"), prefix: new HarmonyMethod(typeof(NativeMod), "LimitDraw"));
            Game1.callbackManager.CreateRoutine(int.MaxValue, NativeSession.Tick);
        }
        private static bool LimitDraw(Microsoft.Xna.Framework.GameTime gameTime)
        { return cadence.Allow(renderClock.Elapsed.Ticks, 60); }
        [MainMenuItemSetting, PauseMenuItemSetting]
        public static TextButton Steam(object factory, GuiFormat format)
        { return DebugEnabled ? new TextButton("Two clients: Steam", new MenuAction(() => NativeSession.Start("Steam"))) : null; }
        [MainMenuItemSetting, PauseMenuItemSetting]
        public static TextButton Local(object factory, GuiFormat format)
        { return DebugEnabled ? new TextButton("Two clients: Local", new MenuAction(() => NativeSession.Start("Local"))) : null; }
        [MainMenuItemSetting, PauseMenuItemSetting]
        public static TextButton Switch(object factory, GuiFormat format)
        { return DebugEnabled ? new TextButton("Switch input client", new MenuAction(NativeSession.Switch)) : null; }
        [MainMenuItemSetting, PauseMenuItemSetting]
        public static TextButton Stop(object factory, GuiFormat format)
        { return DebugEnabled ? new TextButton("Stop / cancel client 2", new MenuAction(NativeSession.Stop)) : null; }
        [MainMenuItemSetting, PauseMenuItemSetting]
        public static TextButton Status(object factory, GuiFormat format) { return DebugEnabled ? new StatusButton() : null; }
        private sealed class MenuAction : IBTnode
        {
            private readonly Action action;
            internal MenuAction(Action value) { action = value; }
            protected override BTresult MyRun(TickData data) { action(); return BTresult.Success; }
        }
        private sealed class StatusButton : TextButton, UnSelectable
        {
            internal StatusButton() : base("Status: idle", new MenuAction(delegate { })) { }
            // menu bounds are cached before Draw, so every status needs the same reserved space
            public override Microsoft.Xna.Framework.Point GetSize() { return Font.MeasureString("MMMMMMMMMMMMMMMMMMMM").ToPoint(); }
            public override void Draw(int x, int y, bool selected)
            {
                Text = NativeSession.Status;
                float width = GetSize().X;
                while (Text.Length > 0 && Font.MeasureString(Text).X > width) Text = Text.Substring(0, Text.Length - 1);
                base.Draw(x, y, selected);
            }
        }
    }

}
