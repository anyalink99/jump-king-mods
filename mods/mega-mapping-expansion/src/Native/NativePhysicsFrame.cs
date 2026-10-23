using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JKRuntime;
using JKRuntime.State;
using JKRuntime.World;
using JumpKing.BodyCompBehaviours;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class PhysicsFrameState
    {
        [WorldField] internal bool Valid;
        [WorldField] internal int Screen,Offset;
        [WorldField] internal float WorldY,ReferenceY;
        internal PhysicsFrameState Copy(){return (PhysicsFrameState)MemberwiseClone();}
    }

    // keep the original float addition and integer cast, just in the authored frame
    internal sealed class NativePhysicsFrame : IDisposable,IStateParticipant
    {
        private readonly MapLayout layout;
        private static OwnedPatches hooks;
        private static NativePhysicsFrame prepared;
        private BodyComp body;
        private PhysicsFrameState state=new PhysicsFrameState();
        internal bool IsActive{get{return body!=null&&layout.PhysicsFrames.ContainsKey(Math.Max(0,-(int)Math.Floor(body.Position.Y/360.0)));}}
        [ThreadStatic] private static BodyComp executing;
        internal NativePhysicsFrame(MapLayout value)
        {
            if(prepared!=null)throw new InvalidOperationException("Physics frame preparation already exists");
            layout=value;Install();prepared=this;
        }
        internal static void Install()
        {
            if(hooks!=null)return;
            // GetHitbox is small enough to inline; install dormant hooks before player methods are JITed
            var pending=new OwnedPatches("mega-mapping-expansion.physics-frame");
            try
            {
                pending.Add(typeof(BodyComp).GetMethod("GetHitbox"),postfix:Method("Hitbox"));
                pending.Add(typeof(UpdateYPositionFromVelocityBehaviour).GetMethod("ExecuteBehaviour"),prefix:Method("Begin"),finalizer:Method("End"),transpiler:Method("AddY"));
                pending.Add(typeof(ResolveYCollisionBehaviour).GetMethod("ExecuteBehaviour"),prefix:Method("Begin"),finalizer:Method("End"),transpiler:Method("SnapY"));
                hooks=pending;
            }
            catch{pending.Dispose();throw;}
        }
        private static MethodInfo Method(string name){return typeof(NativePhysicsFrame).GetMethod(name,OwnedPatches.Members);}
        internal IDisposable Attach(BodyComp value,bool registerState=true)
        {
            if(body!=null)throw new InvalidOperationException("Physics frame already has a player");
            var scope=new RuntimeScope();body=value;state=new PhysicsFrameState();
            scope.Defer(delegate{body=null;state=new PhysicsFrameState();});
            try
            {
                if(registerState)
                {
                    scope.Own(GameState.Snapshots.Register(this));
                    scope.Own(WorldRegistry.Shared.Bind<PhysicsFrameState>("mega.mapping.physics-frame",()=>((PhysicsFrameState)Capture()),s=>Restore(s),s=>Validate(s)));
                }
                return scope;
            }
            catch{scope.Dispose();throw;}
        }
        private bool Synchronize(BodyComp value)
        {
            if(!ReferenceEquals(value,body)||body==null)return false;
            int screen=Math.Max(0,-(int)Math.Floor(body.Position.Y/360.0)),reference;
            if(!layout.PhysicsFrames.TryGetValue(screen,out reference)){state.Valid=false;return false;}
            int offset=(screen-reference)*360;
            // native teleports and foreign position writes invalidate the retained fraction
            if(!state.Valid||state.Screen!=screen||state.WorldY!=body.Position.Y)
            {state.Valid=true;state.Screen=screen;state.Offset=offset;state.WorldY=body.Position.Y;state.ReferenceY=body.Position.Y+offset;}
            return true;
        }
        private static void Begin(BehaviourContext __0,out BodyComp __state){__state=executing;executing=__0.BodyComp;}
        private static void End(BodyComp __state){executing=__state;}
        private static float Add(float world,float step)
        {
            var owner=prepared;if(owner==null||!owner.Synchronize(executing))return world+step;
            owner.state.ReferenceY+=step;owner.state.WorldY=owner.state.ReferenceY-owner.state.Offset;
            return owner.state.WorldY;
        }
        private static Point Snap(ref Vector2 position)
        {
            var owner=prepared;
            if(owner==null||!owner.Synchronize(executing))return position.ToPoint();
            owner.state.ReferenceY=(int)owner.state.ReferenceY;
            owner.state.WorldY=owner.state.ReferenceY-owner.state.Offset;
            return new Point((int)position.X,(int)owner.state.WorldY);
        }
        private static void Hitbox(BodyComp __instance,ref Rectangle __result)
        {
            var owner=prepared;if(owner!=null&&owner.Synchronize(__instance))__result.Y=(int)owner.state.ReferenceY-owner.state.Offset;
        }
        private static IEnumerable<CodeInstruction> AddY(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();var adds=code.Where(c=>c.opcode==OpCodes.Add).ToArray();
            if(adds.Length!=1)throw new NotSupportedException("Native Y integration no longer has one addition");
            adds[0].opcode=OpCodes.Call;adds[0].operand=Method("Add");return code;
        }
        private static IEnumerable<CodeInstruction> SnapY(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();var calls=code.Where(c=>Equals(c.operand,typeof(Vector2).GetMethod("ToPoint",Type.EmptyTypes))).ToArray();
            if(calls.Length!=1)throw new NotSupportedException("Native Y resolution no longer has one coordinate cast");
            calls[0].opcode=OpCodes.Call;calls[0].operand=Method("Snap");return code;
        }
        public string Id{get{return "mega.mapping.physics-frame";}}
        public int Version{get{return 1;}}
        public object Capture(){Synchronize(body);return state.Copy();}
        public void Validate(object snapshot)
        {
            var value=snapshot as PhysicsFrameState;int reference;
            if(value==null||float.IsNaN(value.WorldY)||float.IsInfinity(value.WorldY)||float.IsNaN(value.ReferenceY)||float.IsInfinity(value.ReferenceY)
                ||(value.Valid&&(!layout.PhysicsFrames.TryGetValue(value.Screen,out reference)||value.Offset!=(value.Screen-reference)*360||value.WorldY!=value.ReferenceY-value.Offset)))
                throw new ArgumentException("Invalid physics frame snapshot");
        }
        public void Restore(object snapshot){Validate(snapshot);state=((PhysicsFrameState)snapshot).Copy();}
        public void Dispose(){if(prepared==this)prepared=null;body=null;}
    }
}
