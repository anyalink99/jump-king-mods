using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EntityComponent;
using JKRuntime;
using JKRuntime.Gameplay;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace JKRuntime.World
{
    // foreign modules keep their own data and logic; this adapter owns replication
    public static class SwitchBlocksWorld
    {
        private sealed class Root
        {
            internal FieldInfo Field;
            internal WorldStateCodec Codec;
            internal object Canonical;
            internal object Value {get{return Field.GetValue(null);}}
        }
        private sealed class Actor
        {
            internal ulong Id,Epoch,Sequence,Jump;
            internal BodyComp Body;
            internal BehaviourContext Context;
            internal List<IBlockBehaviour> Behaviours;
            internal readonly Dictionary<FieldInfo,object> Edges=new Dictionary<FieldInfo,object>();
        }
        private static readonly List<Root> roots=new List<Root>();
        private static readonly Dictionary<ulong,Actor> actors=new Dictionary<ulong,Actor>();
        private static readonly FieldInfo blockBehaviours=ForeignMembers.Field(typeof(BodyComp),"m_blockBehaviours");
        private static readonly FieldInfo ground=ForeignMembers.Field(typeof(BodyComp),"_is_on_ground");
        private static readonly MethodInfo clone=ForeignMembers.Method(typeof(object),"MemberwiseClone");
        private static readonly MethodInfo aggregate=ForeignMembers.Method(typeof(BehaviourContextCollisionInfo),"AggregateCollisionInfo");
        private static readonly FieldInfo jumpCallbacks=ForeignMembers.Field(typeof(PlayerEntity),"OnJumpCall");
        private static readonly MethodInfo clearCollisions=ForeignMembers.Method(typeof(BehaviourContext),"ClearCollisionForNewFrame");
        private static readonly MethodInfo startCollisions=ForeignMembers.PropertySetter(typeof(BehaviourContextCollisionInfo),"StartOfFrameCollisionInfo");
        private static FieldInfo[] scratchFields=new FieldInfo[0];
        private static FieldInfo previousVelocity;
        private static MethodInfo switchPressed;
        private static Assembly assembly;
        private static BodyComp sourceBody;
        private static OwnedPatches hooks;
        private static int users;
        private static RuntimeScope resources;
        public static bool SwitchPressed
        {
            get{return switchPressed!=null && (bool)switchPressed.Invoke(null,null);}
        }
        public static void Prepare(RuntimeScope scope)
        {
            RuntimeApi.Kernel.CheckThread();if(scope==null)throw new ArgumentNullException("scope");
            if(users++>0){scope.Defer(ReleaseUser);return;}
            scope.Defer(ReleaseUser);
            resources=new RuntimeScope();
            roots.Clear();assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="SwitchBlocks");
            if(assembly==null)return;
            var scratch=assembly.GetType("SwitchBlocks.Behaviours.Dummy.BehaviourPost",true);
            scratchFields=scratch.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(f=>!f.IsLiteral&&!f.IsInitOnly).ToArray();
            previousVelocity=ForeignMembers.Field(scratch,"<PrevVelocity>k__BackingField");
            switchPressed=ForeignMembers.PropertyGetter(assembly.GetType("SwitchBlocks.Patches.PatchControllerManager"),"IsPressed");
            if(hooks==null){
                hooks=new OwnedPatches("jk-runtime.switch-blocks-world");
                hooks.Add(ForeignMembers.Method(typeof(BodyComp),"UpdateInternal"),prefix:ForeignMembers.Method(typeof(SwitchBlocksWorld),"Restore"),postfix:ForeignMembers.Method(typeof(SwitchBlocksWorld),"Restore"));
                hooks.Add(ForeignMembers.Method(typeof(Entity),"UpdateComponents"),prefix:ForeignMembers.Method(typeof(SwitchBlocksWorld),"UpdateEntity"));
                hooks.Add(ForeignMembers.Method(typeof(EntityManager),"Update"),prefix:ForeignMembers.Method(typeof(SwitchBlocksWorld),"HostPlayers"));
            }
            // explicit profile, not a scan of arbitrary singleton or preference objects
            foreach(string name in new[]{"Auto","Basic","Countdown","Group","Jump","Sand","Sequence","Threshold"}){
                Type t=assembly.GetType("SwitchBlocks.Data.Data"+name,true);var field=ForeignMembers.Field(t,"instance");
                if(field==null || field.FieldType!=t)throw new InvalidDataException("Unsupported world data layout: "+t.FullName);
                var root=new Root{Field=field,Codec=new WorldStateCodec(t)};roots.Add(root);
                resources.Own(WorldRegistry.Shared.Register("mod."+t.FullName,root.Codec.Schema,()=>root.Codec.Capture(root.Value),bytes=>{
                    object value=root.Codec.Decode(bytes),target=root.Value;
                    if((target==null)!=(value==null))throw new InvalidDataException("Different active world data: "+t.FullName);
                    return delegate{
                        root.Canonical=value;if(target!=null)root.Codec.Apply(target,value);
                    };
                }));
                var save=ForeignMembers.Method(t,"SaveToFile");
                if(save!=null)hooks.Add(save,prefix:ForeignMembers.Method(typeof(SwitchBlocksWorld),"SaveAllowed"));
            }

        }
        private static void ReleaseUser()
        {
            if(users>1){users--;return;}
            Release();if(resources!=null){resources.Dispose();resources=null;}if(hooks!=null){hooks.Dispose();hooks=null;}roots.Clear();assembly=null;switchPressed=null;previousVelocity=null;scratchFields=new FieldInfo[0];users=0;
        }
        private static bool UpdateEntity(Entity __instance) {return !WorldControl.Following||!IsWorldLogic(__instance.GetType());}
        public static bool IsWorldLogic(Type type)
        {return assembly!=null && type.Assembly==assembly && (type.Name.StartsWith("EntityLogic",StringComparison.Ordinal)||type.Name.StartsWith("EntityGroupLogic",StringComparison.Ordinal));}
        public static void ResetActors(){actors.Clear();sourceBody=null;}
        public static void Release()
        {
            foreach(var root in roots)root.Canonical=null;
            ResetActors();
        }
        private static bool SaveAllowed(object __instance)
        {return !WorldControl.Following&&WorldExecution.CanPersist;}
        private static void Restore()
        {if(WorldControl.Following)foreach(var root in roots)if(root.Canonical!=null && root.Value!=null)root.Codec.Apply(root.Value,root.Canonical);}
        private static List<IBlockBehaviour> CopyBehaviours(BodyComp body)
        {
            var result=new List<IBlockBehaviour>();
            foreach(var item in (IEnumerable)blockBehaviours.GetValue(body)){
                Type type=item.GetType();if(type.Assembly!=assembly || type.Namespace.EndsWith(".Dummy",StringComparison.Ordinal))continue;
                var copy=(IBlockBehaviour)clone.Invoke(item,null);
                // leaving a platform is per actor; the group's state itself stays shared
                var touched=ForeignMembers.Field(type,"<Touched>k__BackingField");
                if(touched!=null && touched.FieldType==typeof(HashSet<int>))touched.SetValue(copy,new HashSet<int>());
                result.Add(copy);
            }
            return result;
        }
        internal static void HostPlayers()
        {
            if(WorldControl.CurrentRole!=WorldRole.Host || assembly==null || GameLoop.m_player==null)return;
            var body=GameLoop.m_player.m_body;if(body==null)return;
            if(!ReferenceEquals(sourceBody,body)){ResetActors();sourceBody=body;}
            var alive=new HashSet<ulong>();
            foreach(var f in WorldControl.ActorSamples){
                alive.Add(f.Id);Actor actor;
                if(!actors.TryGetValue(f.Id,out actor)||actor.Epoch!=f.Attempt){
                    var proxy=new BodyComp(f.Position,f.Width,f.Height);
                    actor=new Actor{Id=f.Id,Epoch=f.Attempt,Jump=f.Jump,Body=proxy,Context=new BehaviourContext(proxy),Behaviours=CopyBehaviours(body)};actors[f.Id]=actor;
                }
                if(actor.Sequence==f.Sequence)continue;actor.Sequence=f.Sequence;
                actor.Body.Position=f.Position;actor.Body.Velocity=f.Velocity;ground.SetValue(actor.Body,f.Grounded);
                actor.Context.FrameDelta=1f/60;
                var rect=actor.Body.GetHitbox();rect.Inflate(1,1);
                var collision=LevelManager.GetCollisionInfo(rect);if(collision==null)continue;
                clearCollisions.Invoke(actor.Context,null);
                startCollisions.Invoke(actor.Context.CollisionInfo,new object[]{collision});
                aggregate.Invoke(actor.Context.CollisionInfo,new object[]{collision});
                using(WorldControl.EnterActor(f.Id,WorldPhase.Input))Buttons(actor.Body,f.SwitchPressed,f.JumpPressed && f.Jump==actor.Jump);
                using(WorldControl.EnterActor(f.Id,WorldPhase.Contact))RunActor(actor,f.Jump);
            }
            foreach(var id in actors.Keys.Where(id=>!alive.Contains(id)).ToArray())actors.Remove(id);
        }
        private static void Buttons(BodyComp body,bool button,bool jump)
        {
            if(!button&&!jump)return;
            foreach(var entity in EntityManager.instance.Entities){
                Type type=entity.GetType();if(type.Assembly!=assembly)continue;
                if(button && type.Name=="EntityLogicBasic" && (bool)ForeignMembers.PropertyGetter(type,"CanSwitchOnPress").Invoke(entity,null))Request("Basic");
                if(jump && !body.IsOnGround && type.Name=="EntityLogicJump" && (bool)ForeignMembers.PropertyGetter(type,"CanJumpInAir").Invoke(entity,null)){
                    var root=roots.Find(r=>r.Field.FieldType.Name=="DataJump");if(root==null||root.Value==null)continue;
                    int tick=(int)ForeignMembers.Method(assembly.GetType("SwitchBlocks.Patches.PatchAchievementManager"),"GetTick").Invoke(null,null);
                    int cooldown=(int)ForeignMembers.PropertyGetter(type,"Cooldown").Invoke(entity,null);
                    var property=root.Value.GetType().GetProperty("CooldownTick");int last=(int)property.GetValue(root.Value,null);
                    if((long)tick>=(long)last+cooldown){Request("Jump");property.SetValue(root.Value,tick,null);}
                }
            }
        }
        private static void Request(string name)
        {var root=roots.Find(r=>r.Field.FieldType.Name=="Data"+name);if(root!=null&&root.Value!=null)root.Value.GetType().GetProperty("SwitchOnceSafe").SetValue(root.Value,true,null);}
        private static void RunActor(Actor actor,ulong jump)
        {
            var saved=new List<Tuple<object,FieldInfo,object>>();
            // the old mod stores contact edges globally; isolate them while evaluating a peer
            foreach(var root in roots){object value=root.Value;if(value==null)continue;
                foreach(string name in new[]{"HasSwitched","HasEntered"}){var f=ForeignMembers.Field(value.GetType(),"<"+name+">k__BackingField");if(f==null)continue;
                    saved.Add(Tuple.Create(value,f,f.GetValue(value)));object prior;f.SetValue(value,actor.Edges.TryGetValue(f,out prior)?prior:false);
                }
            }
            var globals=scratchFields;
            var previous=globals.Select(f=>f.GetValue(null)).ToArray();
            if(previousVelocity!=null)previousVelocity.SetValue(null,actor.Body.Velocity);
            try{
                if(jump>actor.Jump){
                    var callbacks=jumpCallbacks.GetValue(null) as Delegate;
                    if(callbacks!=null)foreach(var call in callbacks.GetInvocationList())if(call.Method.DeclaringType.Assembly==assembly)call.DynamicInvoke();
                }
                actor.Jump=jump;
                foreach(var behaviour in actor.Behaviours)behaviour.ExecuteBlockBehaviour(actor.Context);
            }finally{
                foreach(var item in saved){actor.Edges[item.Item2]=item.Item2.GetValue(item.Item1);item.Item2.SetValue(item.Item1,item.Item3);}
                for(int i=0;i<globals.Length;i++)globals[i].SetValue(null,previous[i]);
            }
        }
    }
}
