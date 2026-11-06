using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Controller;

namespace JKRuntime.Input
{
    public struct PlayerPadFrame
    {
        public readonly PadInstance Pad;
        public readonly PadState Held, Pressed;
        public PlayerPadFrame(PadInstance pad,PadState held,PadState pressed)
        { if(pad==null) throw new ArgumentNullException("pad"); Pad=pad; Held=held; Pressed=pressed; }
    }
    /// <summary>Publishes complete native input frames. call on the game thread after native device polling</summary>
    public static class NativeInputFrames
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Current = typeof(PadInstance).GetField("current_state", Flags);
        private static readonly FieldInfo Previous = typeof(PadInstance).GetField("last_state", Flags);
        private static readonly FieldInfo MenuState = typeof(MenuController).GetField("_menu_state", Flags);
        private static MenuFrame activeMenu;
        private static PlayerFrame activePlayer;

        /// <summary>Held includes any one-tick pulses. Pressed is restricted to those held actions
        /// native getters and already compiled consumers see the same snapshot without getter hooks</summary>
        public static void Publish(PadInstance pad, PadState held, PadState pressed)
        {
            RuntimeApi.Kernel.CheckThread();
            if (pad == null) throw new ArgumentNullException("pad");
            if (UI.InputContextLease.BlocksPublication(pad)) held = pressed = new PadState();
            PadState previous = held;
            previous.up &= !pressed.up; previous.down &= !pressed.down;
            previous.left &= !pressed.left; previous.right &= !pressed.right;
            previous.jump &= !pressed.jump; previous.pause &= !pressed.pause;
            previous.confirm &= !pressed.confirm; previous.cancel &= !pressed.cancel;
            previous.boots &= !pressed.boots; previous.snake &= !pressed.snake;
            previous.restart &= !pressed.restart;
            Previous.SetValue(pad, previous);
            Current.SetValue(pad, held);
        }

        /// <summary>Owns one menu update's input. Consumed input is never restored on exit
        /// Nested frames are rejected before changing native state. dispose on the game thread</summary>
        public static IDisposable BeginMenu(MenuController menu, PadState pressed)
        {
            RuntimeApi.Kernel.CheckThread();
            if (menu == null) throw new ArgumentNullException("menu");
            if (activeMenu != null) throw new InvalidOperationException("A native menu input frame is already active");
            var frame = new MenuFrame(menu);
            MenuState.SetValue(menu, UI.InputContextLease.Active ? new PadState() : pressed);
            activeMenu = frame;
            return frame;
        }
        /// <summary>Expose one coherent player input frame during a synchronous simulation step, then restore the native pad snapshots. Does not poll devices or consume the world's pending actions.</summary>
        public static IDisposable BeginPlayerFrame(PadState held,PadState pressed)
        {
            var frames=new List<PlayerPadFrame>();
            foreach(var pad in BindingSnapshot.Registered())
                if(pad.IsValid && pad.IsConnected) { frames.Add(new PlayerPadFrame(pad,held,pressed)); break; }
            return BeginPlayerFrame(frames);
        }
        /// <summary>Publish a per-device player step. Device capture guards remain independent; unspecified registered pads are neutral during the step.</summary>
        public static IDisposable BeginPlayerFrame(IEnumerable<PlayerPadFrame> states)
        {
            RuntimeApi.Kernel.CheckThread();
            if(states==null) throw new ArgumentNullException("states");
            if(activePlayer!=null) throw new InvalidOperationException("A player input frame is already active");
            var values=new Dictionary<PadInstance,PlayerPadFrame>();
            foreach(var state in states) { if(state.Pad==null) throw new ArgumentException("Invalid player pad"); values.Add(state.Pad,state); }
            var frame=new PlayerFrame();
            try
            {
                foreach(var pad in BindingSnapshot.Registered())
                {
                    frame.Pads.Add(pad); frame.Current.Add((PadState)Current.GetValue(pad)); frame.Previous.Add((PadState)Previous.GetValue(pad));
                    PlayerPadFrame state;
                    bool supplied=values.TryGetValue(pad,out state) && pad.IsValid && pad.IsConnected;
                    Publish(pad,supplied ? state.Held : new PadState(),supplied ? state.Pressed : new PadState());
                }
                activePlayer=frame; return frame;
            }
            catch { frame.Dispose(); throw; }
        }
        private sealed class PlayerFrame : IDisposable
        {
            internal readonly List<PadInstance> Pads=new List<PadInstance>();
            internal readonly List<PadState> Current=new List<PadState>(), Previous=new List<PadState>();
            private bool disposed;
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread(); if(disposed)return; disposed=true;
                for(int i=0;i<Pads.Count;i++)
                {
                    bool captured=UI.InputContextLease.BlocksPublication(Pads[i]);
                    NativeInputFrames.Current.SetValue(Pads[i],captured ? new PadState() : Current[i]);
                    NativeInputFrames.Previous.SetValue(Pads[i],captured ? new PadState() : Previous[i]);
                }
                if(ReferenceEquals(activePlayer,this)) activePlayer=null;
            }
        }
        private sealed class MenuFrame : IDisposable
        {
            private MenuController menu;
            internal MenuFrame(MenuController value) { menu = value; }
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();
                if (menu == null) return;
                try { menu.ConsumePadPresses(); }
                finally { menu = null; activeMenu = null; }
            }
        }
    }
}
