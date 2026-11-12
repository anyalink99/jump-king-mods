using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JKRuntime.Settings;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.Presentation
{
    public sealed class SmoothRenderingPreferences { public bool Enabled; }

    /// <summary>One shared high-refresh player presentation. Predicts drawing only; never writes player position, input or charge.</summary>
    public static class SmoothRendering
    {
        private sealed class Consumer : IDisposable
        {
            internal string Id;internal Func<bool> Allowed,Actual;internal bool Disposed;
            public void Dispose()
            {
                RuntimeApi.Kernel.CheckThread();if(Disposed)return;
                if(consumers.Count==1)Release();
                consumers.Remove(this);Disposed=true;
            }
        }
        private static readonly List<Consumer> consumers=new List<Consumer>();
        private static readonly HashSet<MethodInfo> adapted=new HashSet<MethodInfo>();
        private static readonly PositionHistory history=new PositionHistory();
        private static readonly PredictionPath path=new PredictionPath();
        private static BodyComp body;
        private static SettingsFile<SmoothRenderingPreferences> file;
        private static OwnedPatches hooks;
        private static PresentationScheduling.Client clock;
        private static IDisposable input;
        private static bool actual,foreignUnsupported;
        public static readonly Setting<bool> Setting=new Setting<bool>("jk-runtime.smooth-rendering","Smooth Rendering",()=>Enabled,SetEnabled);
        public static bool Enabled { get { EnsureLoaded();return file.Value.Enabled; } }
        public static bool Active { get { return !foreignUnsupported && clock!=null && clock.Active && clock.HighRefresh; } }
        private static void EnsureLoaded()
        { if(file==null)Load(Path.Combine(Path.GetDirectoryName(typeof(RuntimeApi).Assembly.Location),"JKRuntime.SmoothRendering.xml")); }
        internal static void Load(string path)
        { file=new SettingsFile<SmoothRenderingPreferences>(path,()=>new SmoothRenderingPreferences()); }
        private static void SetEnabled(bool enabled)
        {
            EnsureLoaded();if(enabled && consumers.Count>0)Prepare();
            file.Save(new SmoothRenderingPreferences{Enabled=enabled});Reset();
            if(enabled)Input.ResponsiveControls.PrepareKeyboard();
        }
        /// <summary>Import SFC's former preference once. An existing shared choice always wins.</summary>
        public static void ImportLegacy(bool enabled)
        { EnsureLoaded();if(file.Status==SettingsReadStatus.Missing)SetEnabled(enabled); }
        /// <summary>Register a menu-capable feature lifetime. Actual-position providers suppress prediction while their fractional simulation runs.</summary>
        public static IDisposable Register(string owner,Func<bool> allowed=null,Func<bool> actualPosition=null)
        {
            RuntimeApi.Kernel.CheckThread();ModuleDefinition.ValidId(owner);
            if(consumers.Exists(c=>c.Id==owner))throw new InvalidOperationException("Smooth rendering consumer already registered: "+owner);
            EnsureLoaded();Install();
            var consumer=new Consumer{Id=owner,Allowed=allowed,Actual=actualPosition};consumers.Add(consumer);
            try { if(Enabled)Prepare();return consumer; }catch{consumer.Dispose();throw;}
        }
        private static PresentationRequest Request()
        {
            bool wanted=false;actual=false;
            if(Enabled)foreach(var consumer in consumers)if(!consumer.Disposed && (consumer.Allowed==null || consumer.Allowed())) {
                wanted=true;actual|=consumer.Actual!=null && consumer.Actual();
            }
            return new PresentationRequest(false,wanted);
        }
        private static void Install()
        {
            if(hooks!=null)return;
            var pending=new OwnedPatches("jk-runtime.smooth-rendering");
            try {
                pending.Add(typeof(JumpGame).GetMethod("Update",OwnedPatches.Members),postfix:typeof(SmoothRendering).GetMethod("AfterUpdate",OwnedPatches.Members));
                Adapt(pending,typeof(PlayerEntity).GetMethod("Draw"));
                clock=PresentationScheduling.Register("jk-runtime.smooth-rendering",Request,null,Reset);
                input=Input.ResponsiveControls.Register("smooth-rendering",()=>Request().Draw);hooks=pending;
            }catch{
                if(input!=null){input.Dispose();input=null;}
                if(clock!=null){clock.Dispose();clock=null;}
                pending.Dispose();throw;
            }
        }
        private static void Adapt(OwnedPatches pending,MethodInfo method)
        {
            var field=typeof(BodyComp).GetField("Position",OwnedPatches.Members);
            var replacement=typeof(SmoothRendering).GetMethod("Position");
            if(field!=null)pending.ReplaceFieldReads(method,field,replacement);
            else pending.ReplaceCalls(method,typeof(BodyComp).GetProperty("Position").GetGetMethod(),replacement,3);
        }
        /// <summary>Prepare native queries and reviewed draw adapters before an attempt or on an explicit setting change.</summary>
        public static void Prepare()
        {
            RuntimeApi.Kernel.CheckThread();PredictionCollision.Prepare();
            Input.ResponsiveControls.PrepareKeyboard();
            if(hooks==null)return;
            foreignUnsupported=false;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if(assembly.GetName().Name!="HitboxResizer")continue;
                try{Compatibility.CompatibilityEvidence.RequireReviewed("runtime.smooth-rendering.hitbox",assembly,"draw-position-v1",
                    new[]{"14A3DB84E635C5A1144DDD2AB59F737F21D6A7C3A47176D8ABF25870C17D6049"},"Use native player presentation");}
                catch(NotSupportedException){foreignUnsupported=true;continue;}
                var type=assembly.GetType("HitboxResizer.Patches.PrefixPlayerEntityDraw");
                var draw=type==null?null:type.GetMethod("Prefix",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(PlayerEntity)},null);
                if(draw==null || draw.ReturnType!=typeof(bool)){
                    foreignUnsupported=true;Compatibility.CompatibilityEvidence.Record("runtime.smooth-rendering.hitbox","HitboxResizer","draw-position-v1",Compatibility.CompatibilityOutcome.Unsupported,"Missing bool Prefix(PlayerEntity)","Use native player presentation");continue;
                }
                if(adapted.Contains(draw))continue;
                Adapt(hooks,draw);adapted.Add(draw);
            }
        }
        public static void Reset() { body=null;history.Reset();path.Begin(Vector2.Zero,Vector2.Zero,0,0,false); }
        private static void AfterUpdate()
        {
            var player=GameLoop.m_player;
            if(!Active || actual || player==null || !player.IsAlive || JumpGame.instance==null || !JumpGame.instance.IsPlaying()) {Reset();return;}
            if(!ReferenceEquals(body,player.m_body)){history.Reset();body=player.m_body;}
            bool custom=Gameplay.JumpSlot.ChargePolicySuspended || Gameplay.GameFeatures.IsMorphed || Gameplay.GameFeatures.IsAttached
                || Gameplay.GameFeatures.ThrustActive || Gameplay.GameFeatures.Movement==Gameplay.MovementMode.VariableJump;
            history.Observe(body.Position,body.Velocity,Camera.CurrentScreenIndex1,
                custom || Gameplay.NativePause.IsPaused || !Game1.instance.IsActive,body.IsOnGround,body.LastVelocity);
            using(RuntimeApi.MeasurePerformance("runtime.presentation.collision-path"))
                path.Prepare(history.Current,history.Velocity,body.GetHitbox(),Camera.CurrentScreen,
                    history.CanPredict?(Vector2?)body.Velocity:null,body.IsOnGround);
        }
        public static Vector2 Position(BodyComp value)
        {
            if(value==null)throw new ArgumentNullException("value");
            return Active && !actual && ReferenceEquals(body,value) && value.Position==history.Current && !Gameplay.PresentationActivity.IsActive(value)
                ?path.At(PresentationScheduling.Alpha):value.Position;
        }
        private static void Release()
        {
            if(input!=null){input.Dispose();input=null;}
            if(clock!=null){clock.Dispose();clock=null;}
            if(hooks!=null){hooks.Dispose();hooks=null;}
            adapted.Clear();Reset();actual=false;
        }
    }
}
