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
using JumpKing.API;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal sealed class PhysicsCoordinateState
    {
        [WorldField] internal long X,Y;
        [WorldField] internal float PublishedX,PublishedY;
        internal PhysicsCoordinateState Copy(){return (PhysicsCoordinateState)MemberwiseClone();}
    }
    internal sealed class PhysicsCoordinates:IDisposable,IStateParticipant
    {
        internal const long Unit=16777216;
        private const long Limit=4000000*Unit;
        private static OwnedPatches hooks;
        internal static PhysicsCoordinates Current;
        private BodyComp body;
        private PhysicsCoordinateState state=new PhysicsCoordinateState();
        private int width,height;
        internal PhysicsPersistence Persistence;
        [ThreadStatic] private static BodyComp executing;
        [ThreadStatic] private static bool vertical;
        private struct Execution {internal BodyComp Body;internal bool Vertical;}
        private static readonly MethodInfo CurrentScreen=typeof(BodyComp).Assembly.GetType("JumpKing.Level.IScreenQuery",true).GetMethod("GetCurrentScreen");
        private static readonly PropertyInfo Blocking=typeof(BoxBlock).GetProperty("canBlockPlayer",OwnedPatches.Members);
        internal string Coverage{get{return "Native rectangles; slopes and external controllers retain their own collision rules";}}
        internal PhysicsCoordinates(string root=null)
        {
            if(Current!=null)throw new InvalidOperationException("Map physics already prepared");Install();
            if(root!=null)Persistence=new PhysicsPersistence(root,ScenePersistence.NativeDirectory);
            Current=this;
        }
        internal static void Install()
        {
            if(hooks!=null)return;
            NativePhysicsFrame.Install();
            var pending=new OwnedPatches("mega-mapping-expansion.map-physics");
            try
            {
                pending.Add(typeof(BodyComp).GetMethod("GetHitbox"),postfix:Method("Hitbox"));
                Stage(pending,typeof(UpdateXPositionFromVelocityBehaviour),"BeginX","AddX");
                Stage(pending,typeof(ResolveXCollisionBehaviour),"BeginX","SnapPosition");
                pending.Add(typeof(CapPositionBehaviour).GetMethod("ExecuteBehaviour"),prefix:Method("Cap"));
                pending.Add(typeof(HandlePlayerTeleportBehaviour).GetMethod("ExecuteBehaviour"),prefix:Method("BeforeTeleport"),postfix:Method("AfterTeleport"));
                pending.Add(typeof(CollisionCheckEarlyExitBehaviour).GetMethod("ExecuteBehaviour"),prefix:Method("EarlyExit"));
                pending.Add(Intersection(typeof(BoxBlock)),prefix:Method("Box"));
                PhysicsPersistence.Install(pending);
                hooks=pending;
            }
            catch{pending.Dispose();throw;}
        }
        private static MethodInfo Intersection(Type type)
        {var map=type.GetInterfaceMap(typeof(IBlock));return map.TargetMethods[Array.FindIndex(map.InterfaceMethods,m=>m.Name=="Intersects")];}
        private static MethodInfo Method(string name){return typeof(PhysicsCoordinates).GetMethod(name,OwnedPatches.Members);}
        private static void Stage(OwnedPatches target,Type type,string begin,string transpiler)
        {target.Add(type.GetMethod("ExecuteBehaviour"),prefix:Method(begin),finalizer:Method("End"),transpiler:Method(transpiler));}
        internal IDisposable Attach(BodyComp value,bool register=true)
        {
            if(body!=null)throw new InvalidOperationException("Map physics already attached");
            var size=value.GetHitbox();width=size.Width;height=size.Height;body=value;
            state=new PhysicsCoordinateState{X=Fixed(value.Position.X),Y=Fixed(value.Position.Y)};
            value.Position=new Vector2(state.PublishedX=(float)((double)state.X/Unit),state.PublishedY=(float)((double)state.Y/Unit));
            var scope=new RuntimeScope();scope.Defer(delegate{body=null;state=new PhysicsCoordinateState();});
            try
            {
                if(register)
                {
                    if(Persistence!=null&&!WorldControl.Following&&JumpKing.SaveThread.SaveManager.instance!=null)
                        {
                            var saved=JumpKing.SaveThread.SaveManager.instance.GetSaveFile();
                            if(body.Position==saved.position&&body.Velocity==saved.velocity)Persistence.Restore(this,saved);
                        }
                    scope.Own(GameState.Snapshots.Register(this));
                    scope.Own(WorldRegistry.Shared.Bind<PhysicsCoordinateState>(Id,()=>((PhysicsCoordinateState)Capture()),s=>Restore(s),s=>Validate(s)));
                }
                return scope;
            }
            catch{scope.Dispose();throw;}
        }
        internal static long Fixed(double value)
        {
            if(double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value)>4000000)throw new ArgumentOutOfRangeException("value","Map physics coordinates exceed +/-4000000 pixels");
            return checked((long)Math.Round(value*Unit,MidpointRounding.ToEven));
        }
        private void Sync()
        {
            if(body==null)return;
            // an external write invalidates only that axis, not its untouched fraction
            if(body.Position.X!=state.PublishedX){state.X=Fixed(body.Position.X);body.Position.X=state.PublishedX=(float)((double)state.X/Unit);}
            if(body.Position.Y!=state.PublishedY){state.Y=Fixed(body.Position.Y);body.Position.Y=state.PublishedY=(float)((double)state.Y/Unit);}
        }
        internal bool Owns(BodyComp value){return body!=null&&ReferenceEquals(body,value);}
        internal static object BeforeCarry(BodyComp target)
        {var owner=Current;return owner!=null&&owner.Owns(target)?owner.Capture():null;}
        internal static void RestoreCarry(BodyComp target,object saved)
        {var owner=Current;if(saved!=null&&owner!=null&&owner.Owns(target))owner.Restore(saved);}
        internal static void Carry(BodyComp target,int dx,int dy)
        {
            var owner=Current;
            if(owner==null||!owner.Owns(target)){target.Position+=new Vector2(dx,dy);return;}
            owner.Sync();owner.state.X=checked(owner.state.X+dx*Unit);owner.state.Y=checked(owner.state.Y+dy*Unit);
            target.Position=new Vector2(owner.state.PublishedX=(float)((double)owner.state.X/Unit),owner.state.PublishedY=(float)((double)owner.state.Y/Unit));
        }
        private Rectangle IntegerBox()
        {return new Rectangle((int)Math.Round((double)state.X/Unit,MidpointRounding.ToEven),PhysicsBox.Floor(state.Y),width,height);}
        internal PhysicsBox Query(Rectangle box)
        {
            Sync();
            return body!=null&&IntegerBox()==box?new PhysicsBox(state.X,state.Y,width,height):new PhysicsBox(box);
        }
        private float Move(float world,float step,bool y)
        {
            if(!Owns(executing))return world+step;
            Sync();long result=checked((y?state.Y:state.X)+Fixed(step));
            if(Math.Abs(result)>Limit)throw new InvalidOperationException("Map physics coordinate limit exceeded");
            float published=(float)((double)result/Unit);
            if(y){state.Y=result;state.PublishedY=published;}else{state.X=result;state.PublishedX=published;}
            return published;
        }
        internal static float ReferenceAddY(float position,float step,BodyComp target)
        {
            var previous=executing;executing=target;
            try{return SumY(position,step);}finally{executing=previous;}
        }
        internal static Point ReferenceSnapY(ref Vector2 position,BodyComp target)
        {
            var previous=executing;bool axis=vertical;executing=target;vertical=true;
            try{return Snap(ref position);}finally{executing=previous;vertical=axis;}
        }
        private static float SumX(float position,float step){return Current==null?position+step:Current.Move(position,step,false);}
        private static float SumY(float position,float step){return Current==null?position+step:Current.Move(position,step,true);}
        private static void BeginX(BehaviourContext __0,out Execution __state){Begin(__0,false,out __state);}
        private static void Begin(BehaviourContext context,bool y,out Execution previous)
        {previous=new Execution{Body=executing,Vertical=vertical};executing=context.BodyComp;vertical=y;}
        private static void End(Execution __state){executing=__state.Body;vertical=__state.Vertical;}
        private static Point Snap(ref Vector2 position)
        {
            var owner=Current;if(owner==null||!owner.Owns(executing))return position.ToPoint();
            owner.Sync();long coordinate=vertical?owner.state.Y:owner.state.X;
            float speed=vertical?executing.Velocity.Y:executing.Velocity.X;
            int value=speed>0?PhysicsBox.Floor(coordinate):PhysicsBox.Ceiling(coordinate);
            if(vertical){owner.state.Y=value*Unit;owner.state.PublishedY=value;}else{owner.state.X=value*Unit;owner.state.PublishedX=value;}
            return vertical?new Point((int)position.X,value):new Point(value,(int)position.Y);
        }
        private static void Hitbox(BodyComp __instance,ref Rectangle __result)
        {
            var owner=Current;if(owner==null||!owner.Owns(__instance))return;
            owner.width=__result.Width;owner.height=__result.Height;owner.Sync();__result=owner.IntegerBox();
        }
        private static bool Box(BoxBlock __instance,Rectangle __0,ref Rectangle __1,ref BlockCollisionType __result)
        {
            var owner=Current;if(owner==null||__instance.GetType().Assembly!=typeof(BoxBlock).Assembly)return true;
            __result=PhysicsGeometry.Intersects(owner.Query(__0),((IBlock)__instance).GetRect(),out __1)
                ?((bool)Blocking.GetValue(__instance,null)?BlockCollisionType.Collision_Blocking:BlockCollisionType.Collision_NonBlocking):BlockCollisionType.NoCollision;
            return false;
        }
        private static bool Cap(BehaviourContext __0,object ___m_screenQuery,ref bool __result)
        {
            var owner=Current;if(owner==null||!owner.Owns(__0.BodyComp))return true;
            __result=true;var screen=(LevelScreen)CurrentScreen.Invoke(___m_screenQuery,null);if(screen==null||screen.CanTeleport)return false;
            owner.Sync();long half=owner.width*Unit/2,limit=480*Unit-half;
            long x=Math.Max(-half,Math.Min(limit,owner.state.X));
            if(x!=owner.state.X){owner.state.X=x;owner.body.Position.X=owner.state.PublishedX=(float)((double)x/Unit);__0["CappedXPosition"]=new object();}
            return false;
        }
        private static void BeforeTeleport(BehaviourContext __0,out PhysicsCoordinateState __state)
        {var owner=Current;__state=owner!=null&&owner.Owns(__0.BodyComp)?(PhysicsCoordinateState)owner.Capture():null;}
        private static void AfterTeleport(BehaviourContext __0,PhysicsCoordinateState __state)
        {
            var owner=Current;if(__state==null||owner==null||!owner.Owns(__0.BodyComp))return;
            var position=__0.BodyComp.Position;
            int dx=position.X==__state.PublishedX+480?480:position.X==__state.PublishedX-480?-480:0;
            float dy=position.Y-(int)__state.PublishedY;
            if(dx==0||dy%360!=0)return;
            // let native routers choose and commit the link, then retain its lost fractions
            owner.Restore(__state);Carry(owner.body,dx,(int)dy);
        }
        private static bool EarlyExit(BehaviourContext __0,ICollisionQuery ___m_collisionQuery,ref bool __result)
        {
            var owner=Current;if(owner==null||!owner.Owns(__0.BodyComp))return true;
            Rectangle overlap;AggregateCollisionInfo info;
            __result=!___m_collisionQuery.CheckCollision(owner.body.GetHitbox(),out overlap,out info);
            if(!__result)
            {
                Execution previous;Begin(__0,false,out previous);
                try{owner.body.Position.X=SumX(owner.body.Position.X,owner.body.Velocity.X);owner.body.Position.Y=SumY(owner.body.Position.Y,owner.body.Velocity.Y);}
                finally{End(previous);}
            }
            return false;
        }
        private static IEnumerable<CodeInstruction> AddX(IEnumerable<CodeInstruction> source){return ReplaceAdd(source,"SumX");}
        private static IEnumerable<CodeInstruction> ReplaceAdd(IEnumerable<CodeInstruction> source,string name)
        {
            var code=source.ToList();var adds=code.Where(c=>c.opcode==OpCodes.Add).ToArray();
            if(adds.Length!=1)throw new NotSupportedException("Native movement no longer has one addition");
            adds[0].opcode=OpCodes.Call;adds[0].operand=Method(name);return code;
        }
        private static IEnumerable<CodeInstruction> SnapPosition(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();var calls=code.Where(c=>Equals(c.operand,typeof(Vector2).GetMethod("ToPoint",Type.EmptyTypes))).ToArray();
            if(calls.Length!=1)throw new NotSupportedException("Native collision no longer has one coordinate cast");
            calls[0].opcode=OpCodes.Call;calls[0].operand=Method("Snap");return code;
        }
        public string Id{get{return "mega.mapping.physics.symmetric.v1";}}
        public int Version{get{return 1;}}
        public object Capture(){Sync();return state.Copy();}
        public void Validate(object snapshot)
        {
            var value=snapshot as PhysicsCoordinateState;
            if(value==null||value.X < -Limit||value.X>Limit||value.Y < -Limit||value.Y>Limit
                ||value.PublishedX!=(float)((double)value.X/Unit)||value.PublishedY!=(float)((double)value.Y/Unit))throw new ArgumentException("Invalid symmetric physics snapshot");
        }
        public void Restore(object snapshot){Validate(snapshot);state=((PhysicsCoordinateState)snapshot).Copy();if(body!=null)body.Position=new Vector2(state.PublishedX,state.PublishedY);}
        public void Dispose(){if(Current==this)Current=null;body=null;if(Persistence!=null)Persistence.Dispose();}
    }
}
