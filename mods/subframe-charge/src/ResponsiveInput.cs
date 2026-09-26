using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;

namespace SubframeCharge
{
    // One bounded latch per native device, on the game thread. Held state and
    // rising edges are independent: repeated reads cannot consume a press.
    internal sealed class InputLatch
    {
        internal int Held, Pending, NativeEdges, UiEdges, Blocked;
        internal bool KeyboardStream;
        private readonly JKRuntime.Input.KeyboardInputGate textInput = new JKRuntime.Input.KeyboardInputGate();
        internal bool SuppressText(int raw)
        {
            if (!KeyboardStream || !textInput.Suppress(raw != 0)) return false;
            Held |= raw; Suppress(); return true;
        }
        internal void Observe(int held, bool captureEdges = true)
        {
            Blocked &= held; held &= ~Blocked;
            UiEdges = captureEdges ? held & ~Held : 0; Pending |= UiEdges; Held = held;
        }
        internal void Commit() { NativeEdges = Pending; Pending = 0; }
        internal void Suppress() { Blocked |= Held; Held = Pending = NativeEdges = UiEdges = 0; }
    }
    internal static class ResponsiveInput
    {
        internal static string JumpDiagnostic()
        {
            var text = new System.Text.StringBuilder(" responsiveFocused=").Append(focused);
            foreach (var pair in devices)
            {
                var value = pair.Value;
                text.Append(" [latchJump held=").Append((value.Held & 16) != 0)
                    .Append(" pending=").Append((value.Pending & 16) != 0)
                    .Append(" nativeEdge=").Append((value.NativeEdges & 16) != 0)
                    .Append(" blocked=").Append((value.Blocked & 16) != 0).Append(']');
            }
            return text.ToString();
        }
        private static readonly FieldInfo Pads = AccessTools.Field(typeof(ControllerManager), "m_pads");
        private static readonly FieldInfo Main = AccessTools.Field(typeof(ControllerManager), "_current_main");
        private static readonly FieldInfo Overlay = AccessTools.Field(typeof(PadInstance), "_steam_overlay_active");
        private static readonly Func<PadInstance, PadState> Read = (Func<PadInstance, PadState>)Delegate.CreateDelegate(typeof(Func<PadInstance, PadState>), AccessTools.Method(typeof(PadInstance), "GetPadState"));
        private static readonly Dictionary<PadInstance, InputLatch> devices = new Dictionary<PadInstance, InputLatch>();
        private static readonly List<PadInstance> retired = new List<PadInstance>();
        private static readonly Dictionary<PadInstance,int> handoff = new Dictionary<PadInstance,int>();
        private static readonly Type Slim = typeof(Game1).Assembly.GetType("JumpKing.Controller.Slim.SlimJoystickManager", true);
        private static readonly FieldInfo SlimInstance = AccessTools.Field(Slim, "instance");
        private static readonly MethodInfo SlimUpdate = AccessTools.Method(Slim, "Update");
        private static bool focused;

        internal static int Bits(PadState p)
        { return (p.up?1:0)|(p.down?2:0)|(p.left?4:0)|(p.right?8:0)|(p.jump?16:0)|(p.pause?32:0)|(p.confirm?64:0)|(p.cancel?128:0)|(p.boots?256:0)|(p.snake?512:0)|(p.restart?1024:0); }
        internal static PadState State(int p)
        { return new PadState { up=(p&1)!=0, down=(p&2)!=0, left=(p&4)!=0, right=(p&8)!=0, jump=(p&16)!=0, pause=(p&32)!=0, confirm=(p&64)!=0, cancel=(p&128)!=0, boots=(p&256)!=0, snake=(p&512)!=0, restart=(p&1024)!=0 }; }
        internal static void Reset() { SubframeKeyboard.Stop(); devices.Clear(); handoff.Clear(); retired.Clear(); focused = false; }
        internal static void ClearPending() { SubframeKeyboard.Clear(); devices.Clear(); handoff.Clear(); retired.Clear(); focused=false; }
        internal static void ReleaseToNative()
        {
            handoff.Clear();
            bool keepKeyboard=false;
            foreach(var pair in devices)
            {
                int physical;
                bool keyboard=SubframeKeyboard.TryReadDown(pair.Key,out physical);
                int down=pair.Value.Held | physical;
                if(down!=0) handoff[pair.Key]=down;
                keepKeyboard |= keyboard && down!=0;
            }
            SubframeKeyboard.Clear(); devices.Clear(); retired.Clear(); focused=false;
            if(!keepKeyboard) SubframeKeyboard.Stop();
        }
        internal static void BeforeNativeController(ControllerManager __instance)
        {
            if(handoff.Count==0) return;
            var pads=(List<PadInstance>)Pads.GetValue(__instance);
            retired.Clear();
            foreach(var pair in handoff) if(!pads.Contains(pair.Key) || !pair.Key.IsValid || !pair.Key.IsConnected) retired.Add(pair.Key);
            foreach(var pad in retired)
            { int ignored; if(SubframeKeyboard.TryReadDown(pad,out ignored)) SubframeKeyboard.Stop(); handoff.Remove(pad); }
            if(handoff.Count==0) SubframeKeyboard.Stop();
        }
        internal static void PrepareKeyboard()
        {
            if (!PerformanceFeatures.InputsRequested || Game1.instance==null) return;
            foreach(var pad in JKRuntime.Input.BindingSnapshot.Registered())
                if(pad.GetPad().GetSaveIdentifier()=="pc_keyboard_jump_king")
                    SubframeKeyboard.Refresh(pad,Game1.instance.IsActive && !(bool)Overlay.GetValue(null));
        }
        internal static void Poll()
        {
            var manager = ControllerManager.instance;
            if (manager == null || Game1.instance == null) return;
            bool active = Game1.instance.IsActive && !(bool)Overlay.GetValue(null);
            handoff.Clear();
            var pads = (List<PadInstance>)Pads.GetValue(manager);
            // DirectInput's native IPad reads a cached Slim state. Refresh it on
            // this same thread; device connect/disconnect events remain native.
            object slim = SlimInstance.GetValue(null);
            if (active && slim != null) SlimUpdate.Invoke(slim, null);
            retired.Clear();
            foreach (var pair in devices) if (!pads.Contains(pair.Key)) retired.Add(pair.Key);
            foreach (var pad in retired) devices.Remove(pad);
            foreach (var pad in pads)
            {
                InputLatch latch;
                if (!devices.TryGetValue(pad, out latch)) { latch = new InputLatch(); devices.Add(pad, latch); }
                int raw = active && pad.IsValid && pad.IsConnected ? Bits(Read(pad)) : 0;
                if (!active || !focused) { latch.Held = raw; latch.Suppress(); }
                latch.KeyboardStream = pad.GetPad().GetSaveIdentifier() == "pc_keyboard_jump_king";
                if (latch.SuppressText(raw)) { SubframeKeyboard.Clear(); continue; }
                latch.Observe(raw,!latch.KeyboardStream);
                if (latch.KeyboardStream)
                {
                    if(SubframeKeyboard.Refresh(pad,active)) latch.Suppress();
                    int edges=SubframeKeyboard.Take(pad,raw) & ~latch.Blocked;
                    latch.Pending |= edges; latch.UiEdges |= edges;
                }
                if (latch.UiEdges != 0) Main.SetValue(manager, pad);
            }
            focused = active;
        }
        internal static void AfterPadUpdate(PadInstance __instance)
        {
            if (!FeatureClock.Active)
            {
                int blocked;
                if(handoff.TryGetValue(__instance,out blocked))
                {
                    int native=Bits(__instance.GetState()), physical;
                    bool keyboard=SubframeKeyboard.TryReadDown(__instance,out physical);
                    // Wait for both clocks to observe release. An early physical
                    // press must not return as a fresh native Confirm on handoff.
                    blocked &= native | physical;
                    JKRuntime.Input.NativeInputFrames.Publish(__instance, State(native), State(Bits(__instance.GetPressed()) & ~blocked));
                    if(blocked==0) { handoff.Remove(__instance); if(keyboard) SubframeKeyboard.Stop(); }
                    else handoff[__instance]=blocked;
                    if(handoff.Count==0) SubframeKeyboard.Stop();
                }
                return;
            }
            InputLatch latch;
            if (!devices.TryGetValue(__instance, out latch)) return;
            if (latch.SuppressText(Bits(__instance.GetState())))
            {
                SubframeKeyboard.Clear();
                JKRuntime.Input.NativeInputFrames.Publish(__instance, new PadState(), new PadState());
                return;
            }
            // The ordinary poll is still executed, preserving native and foreign
            // device updates. Merge observations made since the last native tick.
            int nativeJump = __instance.GetPressed().jump ? 16 : 0;
            latch.Observe(Bits(__instance.GetState()),!latch.KeyboardStream);
            if(latch.KeyboardStream) latch.Pending |= SubframeKeyboard.Take(__instance,latch.Held) & ~latch.Blocked;
            latch.Commit();
            int held = latch.Held;
            // SFC owns completed jump taps when its charge policy is active.
            // Without that policy a short jump is exposed for one native tick.
            int pulse = latch.NativeEdges & (SubframeChargeInstaller.OwnsCompletedTap(__instance) ? ~16 : -1);
            JKRuntime.Input.NativeInputFrames.Publish(__instance, State(held | pulse), State(latch.NativeEdges | nativeJump));
        }
        internal static PadState MenuEdges()
        {
            int edges = 0;
            foreach (var latch in devices.Values)
                if (!latch.SuppressText(latch.Held)) edges |= latch.UiEdges;
            return State(edges);
        }
        internal static IEnumerable<CodeInstruction> RewriteControllerUpdate(IEnumerable<CodeInstruction> source)
        {
            int count=0;
            foreach(var instruction in source)
            {
                if(instruction.Calls(AccessTools.Method(typeof(PadInstance),"Update")))
                {
                    // Retain native polling and publish before main-device selection
                    // and MenuController.Update, even if PadInstance.Update was inlined.
                    var copy=new CodeInstruction(OpCodes.Dup);
                    copy.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    copy.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    yield return copy; yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ResponsiveInput),"AfterPadUpdate"));
                    count++;
                }
                else yield return instruction;
            }
            if(count!=1) throw new NotSupportedException("Unsupported native controller publication boundary");
        }
        internal static void MenuConsumed()
        { foreach (var latch in devices.Values) { latch.Pending = latch.NativeEdges = latch.UiEdges = 0; } }
        internal static void SuppressGameplay()
        { SubframeKeyboard.Clear(); foreach (var latch in devices.Values) latch.Suppress(); }
    }
}
