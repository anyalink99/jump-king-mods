using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Props.RaymanWall;
using Microsoft.Xna.Framework;

namespace JKRuntime.World
{
    public sealed class NativeItemPickup
    {
        public string Item {get;internal set;}
        public Vector2 Position {get;internal set;}
        public int Screen {get;internal set;}
    }
    public sealed class HiddenWallContact
    {
        public string Texture {get;internal set;}
        public bool Touching {get;internal set;}
        public int Screen {get;internal set;}
        public string Id {get;internal set;}
        public Vector2 Position {get;internal set;}
    }

    public static class NativeWorldInteractions
    {
        private static readonly Type wall=typeof(Game1).Assembly.GetType("JumpKing.Props.RaymanWall.RaymanWallEntity",true);
        private static readonly Type item=typeof(Game1).Assembly.GetType("JumpKing.MiscEntities.WorldItems.WorldItemComp",true);
        private static readonly FieldInfo data=ForeignMembers.Field(wall,"m_data"),fade=ForeignMembers.Field(wall,"m_fade"),touch=ForeignMembers.Field(wall,"m_last_touch"),sfx=ForeignMembers.Field(wall,"m_do_play_sfx");
        private static readonly FieldInfo alpha=ForeignMembers.Field(fade.FieldType,"m_alpha"),color=ForeignMembers.Field(fade.FieldType,"m_color");
        private sealed class WallState
        {
            [WorldField] public float Alpha;
            [WorldField] public bool Touch, Sfx;
        }
        private sealed class State {[WorldField] public Dictionary<string,WallState> Walls=new Dictionary<string,WallState>();}
        private static State received;
        private static int users;
        private static RuntimeScope resources;
        private static readonly List<Action<HiddenWallContact>> observers=new List<Action<HiddenWallContact>>();
        private static readonly List<Action<NativeItemPickup>> pickups=new List<Action<NativeItemPickup>>();
        private sealed class PickupContext : IDisposable
        {
            internal IDisposable Scope;
            internal bool Given;
            public void Dispose(){if(Scope!=null){Scope.Dispose();Scope=null;}}
        }
        private static readonly Dictionary<string,int> contactScreens=new Dictionary<string,int>(StringComparer.Ordinal);
        public static IDisposable SubscribeHiddenWalls(Action<HiddenWallContact> observer)
        {RuntimeApi.Kernel.CheckThread();if(observer==null)throw new ArgumentNullException("observer");observers.Add(observer);return new ActionLease(()=>observers.Remove(observer));}
        public static IDisposable SubscribeItemPickups(Action<NativeItemPickup> observer)
        {RuntimeApi.Kernel.CheckThread();if(observer==null)throw new ArgumentNullException("observer");pickups.Add(observer);return new ActionLease(()=>pickups.Remove(observer));}
        public static HiddenWallContact[] ReadHiddenWalls()
        {
            RuntimeApi.Kernel.CheckThread();
            return Walls().Select(entity=>{
                var value=(RaymanData)data.GetValue(entity);
                return new HiddenWallContact{Texture=value.texture_name,Touching=(bool)touch.GetValue(entity),Screen=0,
                    Id=Key(entity),Position=value.Position.ToVector2()};
            }).ToArray();
        }
        public static void Prepare(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();if(scope==null)throw new ArgumentNullException("scope");
            if(users++>0){scope.Defer(Release);return;}scope.Defer(Release);resources=new RuntimeScope();
            var hooks=resources.Own(new OwnedPatches("jk-runtime.world-interactions"));
            hooks.Add(ForeignMembers.Method(wall,"TouchPlayer"),postfix:Method("Touch"));
            hooks.Add(ForeignMembers.Method(wall,"Update"),prefix:Method("UpdateWall"));
            hooks.Add(ForeignMembers.Method(item,"Update"),prefix:Method("ItemAllowed"));
            hooks.Add(ForeignMembers.Method(item,"OnPickup"),prefix:Method("PickupBegin"),postfix:Method("PickupDone"),finalizer:Method("PickupEnd"));
            resources.Own(WorldRegistry.Shared.Bind<State>("native.hidden-walls",Capture,Apply,Validate));
        }
        private static MethodInfo Method(string name) {return ForeignMembers.Method(typeof(NativeWorldInteractions),name);}
        private static void Release()
        {if(users>1){users--;return;}if(resources!=null){resources.Dispose();resources=null;}received=null;contactScreens.Clear();users=0;}
        internal static void ResetReceived() {received=null;contactScreens.Clear();}
        private static Entity[] Walls()
        {return EntityManager.instance==null?new Entity[0]:EntityManager.instance.Entities.Where(e=>e.GetType()==wall&&e.IsAlive).ToArray();}
        private static string Key(object entity)
        {var value=(RaymanData)data.GetValue(entity);return value.texture_name+"@"+value.Position.X+","+value.Position.Y;}
        private static State Capture()
        {
            if(WorldControl.Following&&received!=null)return received;
            var state=new State();foreach(var entity in Walls())state.Walls.Add(Key(entity),new WallState{Alpha=(float)alpha.GetValue(fade.GetValue(entity)),Touch=(bool)touch.GetValue(entity),Sfx=(bool)sfx.GetValue(entity)});return state;
        }
        private static void Validate(State state)
        {if(state.Walls==null||state.Walls.Count>512||state.Walls.Any(p=>p.Value==null||p.Value.Alpha<0||p.Value.Alpha>1))throw new ArgumentException("Invalid hidden-wall state");}
        private static void Apply(State state)
        {received=state;foreach(var entity in Walls()){WallState value;if(state.Walls.TryGetValue(Key(entity),out value))Set(entity,value);}}
        private static void Set(object entity,WallState value)
        {var target=fade.GetValue(entity);alpha.SetValue(target,value.Alpha);var c=(Color)color.GetValue(target);c.A=(byte)(value.Alpha*255);color.SetValue(target,c);touch.SetValue(entity,value.Touch);sfx.SetValue(entity,value.Sfx);}
        private static bool UpdateWall(object __instance)
        {
            if(!WorldControl.Following)return true;
            WallState value;if(received!=null&&received.Walls.TryGetValue(Key(__instance),out value))Set(__instance,value);
            return false;
        }
        private static void Touch(RaymanData ___m_data,bool ___m_last_touch,ref bool __result)
        {
            int screen=Camera.CurrentScreenIndex1;
            if(WorldControl.CurrentRole==WorldRole.Host&&!__result){
                var peer=WorldControl.ActorSamples.FirstOrDefault(a=>___m_data.hitboxes.Any(b=>b.Intersects(new Rectangle((int)a.Position.X,(int)a.Position.Y,a.Width,a.Height))));
                if(peer!=null){__result=true;screen=peer.Screen;}
            }
            if(__result==___m_last_touch||WorldControl.Following)return;
            string key=___m_data.texture_name+"@"+___m_data.Position.X+","+___m_data.Position.Y;
            if(__result){if(contactScreens.Count<512)contactScreens[key]=screen;}
            else{int prior;if(contactScreens.TryGetValue(key,out prior)){screen=prior;contactScreens.Remove(key);}}
            var value=new HiddenWallContact{Texture=___m_data.texture_name,Touching=__result,Screen=screen,Id=key,Position=___m_data.Position.ToVector2()};
            foreach(var observer in observers.ToArray())try{observer(value);}catch(Exception error){observers.Remove(observer);RuntimeJournal.Record("runtime.hidden-walls","observer",error.GetBaseException().Message);}
        }
        private static bool ItemAllowed() {return WorldControl.CurrentRole!=WorldRole.Playback;}
        private static bool PickupBegin(bool ___gave_item,out PickupContext __state)
        {__state=null;if(!ItemAllowed())return false;__state=new PickupContext{Scope=WorldControl.EnterLocal(WorldPhase.Personal),Given=___gave_item};return true;}
        private static void PickupDone(JumpKing.MiscEntities.WorldItems.WorldItemState ___m_state,bool ___gave_item,PickupContext __state)
        {
            if(__state==null||__state.Given||!___gave_item||pickups.Count==0)return;
            var value=new NativeItemPickup{Item=___m_state.item.ToString(),Position=___m_state.position.ToVector2(),Screen=Camera.CurrentScreenIndex1};
            foreach(var observer in pickups.ToArray())try{observer(value);}catch(Exception error){pickups.Remove(observer);RuntimeJournal.Record("runtime.items","observer",error.GetBaseException().Message);}
        }
        private static void PickupEnd(PickupContext __state) {if(__state!=null)__state.Dispose();}
    }
}
