using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EntityComponent;
using JumpKing.API;

namespace JKRuntime.World
{
    public sealed class WorldCoverage
    {
        public string Type {get;internal set;}
        public string Assembly {get;internal set;}
        public string Status {get;internal set;}
        public string[] Reasons {get;internal set;}
    }

    /// <summary>Prepared foreign boundary inventory. discovering a handler never certifies deterministic simulation</summary>
    public static class WorldObservation
    {
        private static WorldCoverage[] coverage=new WorldCoverage[0];
        public static string BoundaryStatus {get;private set;}
        private static readonly OpCode[] one=new OpCode[256],two=new OpCode[256];
        private static readonly Dictionary<Type,bool> mechanics=new Dictionary<Type,bool>();
        static WorldObservation()
        {foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)){var op=(OpCode)f.GetValue(null);ushort value=unchecked((ushort)op.Value);if(value<256)one[value]=op;else if((value&0xff00)==0xfe00)two[value&255]=op;}}
        public static WorldCoverage[] Inspect()
        {RuntimeApi.Kernel.CheckThread();return coverage.Select(c=>new WorldCoverage{Type=c.Type,Assembly=c.Assembly,Status=c.Status,Reasons=(string[])c.Reasons.Clone()}).ToArray();}
        public static WorldCoverage Analyze(Type type)
        {
            if(type==null)throw new ArgumentNullException("type");
            var reasons=new HashSet<string>();
            var members=type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static));
            if(type.TypeInitializer!=null)members=members.Concat(new[]{type.TypeInitializer}).Distinct();
            foreach(var method in members) {
                if((method.Attributes&MethodAttributes.PinvokeImpl)!=0)reasons.Add("native interop");
                try {var body=method.GetMethodBody();if(body==null)continue;byte[] bytes=body.GetILAsByteArray();
                for(int p=0;p<bytes.Length;) {
                    byte first=bytes[p++];var op=first==0xfe?two[bytes[p++]]:one[first];
                    if(op.Size==0)throw new InvalidOperationException("Unknown IL opcode");
                    int size=OperandBytes(op.OperandType,bytes,p);
                    if(size<0||p>bytes.Length-size)throw new InvalidOperationException("Invalid IL operand");
                    if(op.OperandType==OperandType.InlineField) {
                        var field=method.Module.ResolveField(BitConverter.ToInt32(bytes,p),type.GetGenericArguments(),method.GetGenericArguments());
                        if(field.IsStatic&&!field.IsLiteral&&!field.IsInitOnly)reasons.Add("mutable static fields; ownership unresolved");
                        if(field.DeclaringType.Assembly==typeof(JumpKing.Game1).Assembly)reasons.Add("direct native field access");
                    }
                    if(op.OperandType==OperandType.InlineMethod) {
                        var called=method.Module.ResolveMethod(BitConverter.ToInt32(bytes,p),type.GetGenericArguments(),method.GetGenericArguments());
                        string name=called.DeclaringType==null?"":called.DeclaringType.FullName;
                        if(name.StartsWith("System.IO.",StringComparison.Ordinal))reasons.Add("file IO");
                        if(name.StartsWith("System.Threading.",StringComparison.Ordinal))reasons.Add("threading");
                        if(name.StartsWith("System.Reflection.",StringComparison.Ordinal))reasons.Add("reflection");
                        if(name=="System.Random"||name=="System.DateTime"||name=="System.Diagnostics.Stopwatch")reasons.Add("external clock or random source");
                    }
                    p+=size;
                }}catch(Exception){reasons.Add("incomplete IL inspection");}
            }
            return new WorldCoverage{Type=type.FullName,Assembly=type.Assembly.FullName,
                Status=Attribute.IsDefined(type,typeof(WorldMechanicAttribute))?"declared world boundary; state and determinism require verification":"observed native boundary; semantics unresolved",Reasons=reasons.OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
        }
        private static int OperandBytes(OperandType type,byte[] bytes,int offset)
        {
            switch(type) {
                case OperandType.InlineNone:return 0;
                case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:return 1;
                case OperandType.InlineVar:return 2;
                case OperandType.InlineI8:case OperandType.InlineR:return 8;
                case OperandType.InlineSwitch:if(offset>bytes.Length-4)return -1;int count=BitConverter.ToInt32(bytes,offset);return count<0||count>(bytes.Length-offset-4)/4?-1:4+count*4;
                default:return 4;
            }
        }
        internal static void Prepare(IEnumerable<Assembly> assemblies)
        {
            var result=new List<WorldCoverage>();
            foreach(var assembly in assemblies.Where(a=>a!=null).Select(PackageHost.BindingAssembly).Distinct()) {
                Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException e){types=e.Types.Where(t=>t!=null).ToArray();}
                foreach(var type in types.Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters&&(typeof(IBlockBehaviour).IsAssignableFrom(t)||typeof(Entity).IsAssignableFrom(t))))
                    if(result.Count<4096)result.Add(Analyze(type));
            }
            coverage=result.OrderBy(x=>x.Assembly,StringComparer.Ordinal).ThenBy(x=>x.Type,StringComparer.Ordinal).ToArray();
        }
        internal static IDisposable Install()
        {
            var hooks=new OwnedPatches("jk-runtime.world-boundaries");
            try {
                hooks.Add(typeof(EntityManager).GetMethod("Update",OwnedPatches.Members),prefix:Method("Begin"),finalizer:Method("End"),priority:800);
                hooks.Add(typeof(Entity).GetMethod("UpdateComponents",OwnedPatches.Members),prefix:Method("EntityAllowed"));
                hooks.Add(typeof(JumpKing.Player.BodyComp).GetMethod("UpdateInternal",OwnedPatches.Members),prefix:Method("BodyBegin"),finalizer:Method("End"),priority:800);
                return hooks;
            }catch{hooks.Dispose();throw;}
        }
        internal static void TryInstall(RuntimeScope scope)
        {
            try {scope.Own(Install());BoundaryStatus="native update boundaries installed";}
            catch(Exception error) {BoundaryStatus="unavailable: "+error.GetBaseException().Message;RuntimeJournal.Record("runtime.world","install",BoundaryStatus);}
        }
        private static MethodInfo Method(string name) {return typeof(WorldObservation).GetMethod(name,OwnedPatches.Members);}
        private static void Begin(out IDisposable __state) {__state=WorldControl.BeforeUpdate();}
        private static void BodyBegin(out IDisposable __state) {__state=WorldControl.EnterLocal(WorldPhase.Contact);}
        private static void End(IDisposable __state) {if(__state!=null)__state.Dispose();}
        private static bool EntityAllowed(Entity __instance)
        {
            if(WorldExecution.CanSimulate)return true;
            var type=__instance.GetType();bool shared;
            if(!mechanics.TryGetValue(type,out shared)){shared=Attribute.IsDefined(type,typeof(WorldMechanicAttribute));mechanics.Add(type,shared);}
            return !shared;
        }
    }
}
