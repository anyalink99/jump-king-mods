using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager.TitleScreen;

namespace JKRuntime.Input
{
    internal static class InputContract
    {
        internal static FieldInfo Field(Type type,string name) { for(;type!=null;type=type.BaseType){var field=type.GetField(name,OwnedPatches.Members);if(field!=null)return field;}return null; }
        internal static PropertyInfo Property(Type type,string name) { return type.GetProperty(name,OwnedPatches.Members); }
        internal static MethodInfo PropertyGetter(Type type,string name) { return Property(type,name).GetGetMethod(true); }
        internal static MethodInfo Method(Type type,string name) { return type.GetMethod(name,OwnedPatches.Members); }
    }
    /// <summary>One high-rate input/menu stream. Consumers may opt into Jump buffering; ordinary visual consumers keep native Jump snapshots.</summary>
    public static class ResponsiveControls
    {
        private sealed class Consumer : IDisposable
        {
            internal string Id;internal Func<bool> Request;internal Func<PadInstance,bool> Tap;internal bool Jump,Disposed;
            public void Dispose() { RuntimeApi.Kernel.CheckThread();if(Disposed)return;if(consumers.Count==1)Release();consumers.Remove(this);Disposed=true; }
        }
        private static readonly List<Consumer> consumers=new List<Consumer>();
        private static PresentationScheduling.Client clock;
        private static OwnedPatches hooks;
        private static bool requested,jump,inputOwner;
        public static bool Requested { get { foreach(var consumer in consumers)if(!consumer.Disposed && consumer.Request())return true;return false; } }
        public static bool Active { get { return clock!=null && clock.Active; } }
        internal static bool SampleJump { get { return jump; } }
        public static IDisposable Register(string owner,Func<bool> request,bool sampleJump=false,Func<PadInstance,bool> ownsCompletedTap=null)
        {
            RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(owner);
            if(request==null)throw new ArgumentNullException("request");
            if(consumers.Exists(c=>c.Id==owner))throw new InvalidOperationException("Responsive input consumer already registered: "+owner);
            Install();var value=new Consumer{Id=owner,Request=request,Jump=sampleJump,Tap=ownsCompletedTap};consumers.Add(value);return value;
        }
        private static PresentationRequest Request()
        {
            requested=jump=false;
            foreach(var consumer in consumers)if(!consumer.Disposed && consumer.Request()){requested=true;jump|=consumer.Jump;}
            if(inputOwner && !requested)ResponsiveInput.ReleaseToNative();inputOwner=requested;
            return new PresentationRequest(requested,false);
        }
        internal static bool OwnsCompletedTap(PadInstance pad)
        { foreach(var consumer in consumers)if(consumer.Jump && consumer.Tap!=null && consumer.Request() && consumer.Tap(pad))return true;return false; }
        private static void Install()
        {
            if(hooks!=null)return;var pending=new OwnedPatches("jk-runtime.responsive-controls");
            try {
                pending.Add(typeof(ControllerManager).GetMethod("Update",OwnedPatches.Members),prefix:InputContract.Method(typeof(ResponsiveInput),"BeforeNativeController"));
                pending.ReplaceCalls(typeof(ControllerManager).GetMethod("Update",OwnedPatches.Members),typeof(PadInstance).GetMethod("Update"),InputContract.Method(typeof(ResponsiveInput),"UpdatePad"),1);
                pending.Add(InputContract.Method(MenuCadence.Pause,"PauseUpdate"),prefix:InputContract.Method(typeof(MenuCadence),"NativePauseUpdate"));
                pending.Add(InputContract.Method(typeof(UI.UiPointer),"Update"),prefix:InputContract.Method(typeof(MenuCadence),"NativePointerUpdate"));
                pending.Add(InputContract.Method(typeof(GameTitleScreen),"OnNewRun"),prefix:InputContract.Method(typeof(MenuCadence),"TitleStart"));
                pending.ReplaceCalls(InputContract.Method(typeof(GameTitleScreen),"MyRun"),typeof(BehaviorTree.BTmanager).GetMethod("Run",new[]{typeof(float)}),InputContract.Method(typeof(MenuCadence),"RunTitle"),1);
                clock=PresentationScheduling.Register("jk-runtime.responsive-controls",Request,delta=>{
                    using(RuntimeApi.MeasurePerformance("runtime.input.poll"))ResponsiveInput.Poll();
                    using(RuntimeApi.MeasurePerformance("runtime.input.menu"))MenuCadence.Pump(delta);
                },()=>{if(inputOwner)ResponsiveInput.ReleaseToNative();inputOwner=false;});
                hooks=pending;
            }catch{pending.Dispose();throw;}
        }
        public static void PrepareKeyboard() { ResponsiveInput.PrepareKeyboard(); }
        public static void ClearPending() { ResponsiveInput.ClearPending(); }
        public static void PhysicsFrames(long timestamp,bool pulseJump,List<PlayerPadFrame> frames) { ResponsiveInput.PhysicsFrames(timestamp,pulseJump,frames); }
        public static void ResetPhysics() { ResponsiveInput.ResetPhysics(); }
        public static bool HasPlayerInput { get { return ResponsiveInput.HasPlayerInput; } }
        public static string JumpDiagnostic() { return ResponsiveInput.JumpDiagnostic(); }
        private static void Release()
        {
            if(clock!=null){clock.Dispose();clock=null;}if(hooks!=null){hooks.Dispose();hooks=null;}
            ResponsiveInput.Reset();MenuCadence.Reset();requested=jump=inputOwner=false;
        }
    }
}
