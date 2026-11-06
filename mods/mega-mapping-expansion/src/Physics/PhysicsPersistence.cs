using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JKRuntime;
using JumpKing.Player;
using JumpKing.SaveThread;

namespace MegaMappingExpansion
{
    internal sealed class PhysicsSavePacket
    {
        internal SaveState Native;
        internal PhysicsCoordinateState Exact;
        internal Dictionary<string,string> Encode()
        {
            return new Dictionary<string,string>{{"version","1"},{"x",Exact.X.ToString(CultureInfo.InvariantCulture)},{"y",Exact.Y.ToString(CultureInfo.InvariantCulture)},
                {"px",F(Native.position.X)},{"py",F(Native.position.Y)},{"vx",F(Native.velocity.X)},{"vy",F(Native.velocity.Y)},
                {"tick",Native.time_stamp.ToString(CultureInfo.InvariantCulture)},{"ground",Native.is_on_ground?"1":"0"},{"direction",Native.direction.ToString(CultureInfo.InvariantCulture)},
                {"state",((int)Native.state).ToString(CultureInfo.InvariantCulture)}};
        }
        private static string F(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
        internal static PhysicsSavePacket Decode(Dictionary<string,string> data)
        {
            if(data.Count!=11||data["version"]!="1")throw new System.IO.InvalidDataException("Unsupported map physics save");
            var native=new SaveState{position=new Microsoft.Xna.Framework.Vector2(float.Parse(data["px"],CultureInfo.InvariantCulture),float.Parse(data["py"],CultureInfo.InvariantCulture)),
                velocity=new Microsoft.Xna.Framework.Vector2(float.Parse(data["vx"],CultureInfo.InvariantCulture),float.Parse(data["vy"],CultureInfo.InvariantCulture)),
                time_stamp=int.Parse(data["tick"],CultureInfo.InvariantCulture),is_on_ground=data["ground"]=="1",direction=int.Parse(data["direction"],CultureInfo.InvariantCulture),state=(SaveState.PlayerState)int.Parse(data["state"],CultureInfo.InvariantCulture)};
            return new PhysicsSavePacket{Native=native,Exact=new PhysicsCoordinateState{X=long.Parse(data["x"],CultureInfo.InvariantCulture),Y=long.Parse(data["y"],CultureInfo.InvariantCulture),PublishedX=native.position.X,PublishedY=native.position.Y}};
        }
    }
    internal sealed class PhysicsPersistence:IDisposable
    {
        private readonly object sync=new object();
        private readonly object writes=new object();
        private readonly Queue<PhysicsSavePacket> packets=new Queue<PhysicsSavePacket>();
        private ScenePersistence store;
        private Dictionary<string,string> loaded;
        private bool disposed;
        private int revision;
        private static readonly Type Save=typeof(BodyComp).Assembly.GetType("JumpKing.SaveThread.SaveLube",true);
        private static readonly Action<string,string,CombinedSaveFile,int> NativeSave=CreateNativeSave();
        internal PhysicsPersistence(string root,Func<string> directory)
        {
            SceneSaveCoordinator.Load("map-physics-v1:"+System.IO.Path.GetFullPath(root),directory,out store,out loaded,out revision);
        }
        internal static void Install(OwnedPatches hooks)
        {
            hooks.Add(typeof(PlayerEntity).GetMethod("GetSaveState",OwnedPatches.Members),postfix:Method("Capture"));
            hooks.Add(Save.GetMethod("SaveCombinedSaveFile",OwnedPatches.Members),transpiler:Method("SaveCall"));
        }
        private static MethodInfo Method(string name){return typeof(PhysicsPersistence).GetMethod(name,OwnedPatches.Members);}
        private static Action<string,string,CombinedSaveFile,int> CreateNativeSave()
        {
            var method=Save.GetMethod("Save",OwnedPatches.Members).MakeGenericMethod(typeof(CombinedSaveFile));
            var bridge=new DynamicMethod("MMENativeCombinedSave",typeof(void),new[]{typeof(string),typeof(string),typeof(CombinedSaveFile),typeof(int)},typeof(PhysicsPersistence),true);
            var il=bridge.GetILGenerator();for(int i=0;i<4;i++)il.Emit(OpCodes.Ldarg,i);il.Emit(OpCodes.Call,method);il.Emit(OpCodes.Ret);
            return (Action<string,string,CombinedSaveFile,int>)bridge.CreateDelegate(typeof(Action<string,string,CombinedSaveFile,int>));
        }
        private static IEnumerable<CodeInstruction> SaveCall(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();var calls=code.Where(c=>c.operand is MethodInfo&&((MethodInfo)c.operand).Name=="Save").ToArray();
            if(calls.Length!=1)throw new NotSupportedException("Native combined save no longer has one write");
            calls[0].operand=Method("WriteNative");return code;
        }
        private static void Capture(PlayerEntity __instance,SaveState __result)
        {
            var physics=PhysicsCoordinates.Current;
            if(physics!=null&&physics.Owns(__instance.m_body)&&physics.Persistence!=null&&!JKRuntime.World.WorldControl.Following)
                physics.Persistence.Publish(__result,(PhysicsCoordinateState)physics.Capture());
        }
        internal void Publish(SaveState native,PhysicsCoordinateState exact)
        {
            lock(sync)
            {
                if(disposed)return;
                if(revision!=System.Threading.Volatile.Read(ref SceneSaveCoordinator.Revision))
                {
                    var next=store==null?null:SceneSaveCoordinator.ResetStore(store,out revision);
                    packets.Clear();loaded=null;
                    if(next==null)return;
                    store=next;
                }
                if(store==null)return;
                packets.Enqueue(new PhysicsSavePacket{Native=native,Exact=exact.Copy()});while(packets.Count>256)packets.Dequeue();
            }
        }
        internal bool Restore(PhysicsCoordinates physics,SaveState native)
        {
            lock(sync)
            {
                if(loaded==null||loaded.Count==0||revision!=System.Threading.Volatile.Read(ref SceneSaveCoordinator.Revision))return false;
                var data=loaded;loaded=null;
                try
                {
                    var packet=PhysicsSavePacket.Decode(data);
                    if(!packet.Native.IsSame(native))return false;
                    physics.Validate(packet.Exact);physics.Restore(packet.Exact);return true;
                }
                catch(Exception error)
                {
                    // a bad auxiliary record mustn't prevent loading the native save
                    ModEntry.Log("Ignoring invalid exact map physics save: "+error.Message);return false;
                }
            }
        }
        private static void WriteNative(string folder,string file,CombinedSaveFile save,int mode)
        {
            var physics=PhysicsCoordinates.Current;var persistence=physics==null?null:physics.Persistence;
            PhysicsSavePacket packet=persistence==null?null:persistence.Find(save.player_position);
            NativeSave(folder,file,save,mode);
            // match the value actually handed to the native writer, never the next frame
            if(packet!=null)persistence.Write(packet);
        }
        internal PhysicsSavePacket Find(SaveState native)
        {lock(sync)return disposed?null:packets.LastOrDefault(p=>p.Native.IsSame(native));}
        internal void Write(PhysicsSavePacket packet)
        {
            if(packet==null)return;
            lock(writes)
            {
                ScenePersistence target;
                lock(sync)
                {
                    if(disposed||store==null||revision!=System.Threading.Volatile.Read(ref SceneSaveCoordinator.Revision))return;
                    target=store;
                }
                // don't hold the publication lock across disk I/O on the save worker
                try{target.Save(packet.Encode());}catch(Exception error){ModEntry.Log("Exact map physics save failed: "+error.Message);}
            }
        }
        public void Dispose(){lock(sync){disposed=true;packets.Clear();loaded=null;}}
    }
}
