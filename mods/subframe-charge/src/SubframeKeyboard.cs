using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using JKRuntime.Input;
using JumpKing;
using JumpKing.Controller;

namespace SubframeCharge
{
    // Keyboard edges must survive a blocked Present/VSync, not merely be
    // sampled several times after that wait. One worker serves all actions.
    internal static class SubframeKeyboard
    {
        private static KeyboardActionEdges worker;
        private static PadInstance owner;
        private static int fingerprint;
        private static IntPtr ownerWindow;
        private static readonly Func<bool> SuppressMouse = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),
            HarmonyLib.AccessTools.PropertyGetter(typeof(JKRuntime.UI.UiPointer),"SuppressMouseBindings"));
        private static int Add(int hash,int[] values) { unchecked { if(values==null)return hash*31; foreach(int value in values) hash=hash*31+value; return hash*31+values.Length; } }
        internal static bool Refresh(PadInstance pad,bool active)
        {
            var binding=pad.GetBind();
            if(!active || !pad.IsValid || binding==null || !binding.Enabled) { Stop(); return true; }
            IntPtr handle=Game1.instance.Window.Handle;
            int hash=PhysicalBindings.Epoch; bool suppressMouse=SuppressMouse();
            hash=Add(hash,binding.up);hash=Add(hash,binding.down);hash=Add(hash,binding.left);hash=Add(hash,binding.right);hash=Add(hash,binding.jump);hash=Add(hash,binding.pause);
            hash=Add(hash,binding.confirm);hash=Add(hash,binding.cancel);hash=Add(hash,binding.boots);hash=Add(hash,binding.snake);hash=Add(hash,binding.restart);
            hash=hash*31+(suppressMouse?1:0);
            if(worker!=null && ReferenceEquals(owner,pad) && fingerprint==hash && ownerWindow==handle) return false;
            Stop();
            int[][] logical={binding.up,binding.down,binding.left,binding.right,binding.jump,binding.pause,binding.confirm,binding.cancel,binding.boots,binding.snake,binding.restart};
            var actions=new int[11][][];
            for(int i=0;i<actions.Length;i++)
            {
                var alternatives=new List<int[]>();
                foreach(var chord in PhysicalBindings.ResolvePhysicalBinding(pad,logical[i]))
                {
                    var copy=new int[chord.Length]; bool valid=chord.Length!=0;
                    for(int j=0;j<chord.Length;j++)
                    {
                        bool mouse=MouseButtons.IsMouseButton(chord[j]);
                        copy[j]=MouseButtons.ToVirtualKey(chord[j]);
                        valid &= copy[j]>=0 && copy[j]<256 && !(mouse && suppressMouse);
                    }
                    if(valid) alternatives.Add(copy);
                }
                actions[i]=alternatives.ToArray();
            }
            fingerprint=hash;owner=pad;ownerWindow=handle;
            worker=new KeyboardActionEdges(actions,handle,null,null,true);
            return true;
        }
        internal static int Take(PadInstance pad,int nativeState)
        {
            if(worker==null || !ReferenceEquals(owner,pad)) return 0;
            long now=Stopwatch.GetTimestamp();
            // WinForms/MonoGame's keyboard snapshot can lag GetAsyncKeyState.
            // Never publish that older snapshot into the physical edge history:
            // it would look like release/press chatter while a key stays held.
            // Both pollers read the same physical source; Sample has no shared
            // mutable scratch buffer and Publish orders their observations.
            worker.Sample(now);
            return worker.Take(now);
        }
        internal static void Clear() { if(worker!=null)worker.Clear(); }
        internal static bool TryReadDown(PadInstance pad, out int down)
        {
            down=0;
            if(worker==null || !ReferenceEquals(owner,pad)) return false;
            worker.Sample(Stopwatch.GetTimestamp()); down=worker.Down; return true;
        }
        internal static void Stop() { if(worker!=null)worker.Dispose();worker=null;owner=null; }
    }
}
