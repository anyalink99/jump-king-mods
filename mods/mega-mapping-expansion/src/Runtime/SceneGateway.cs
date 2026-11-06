using System;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal sealed class SceneGateway:IMappingScene,IMappingMedia,IDisposable
    {
        private readonly Func<SceneProgram> resolve;
        private bool disposed;
        private SceneProgram scene{get{return disposed?null:resolve();}}
        internal SceneGateway(SceneProgram owner):this(()=>owner){}
        internal SceneGateway(Func<SceneProgram> resolver){resolve=resolver;}
        public bool Available{get{return scene!=null&&scene.Signals.Available;}}
        public long Generation{get{return Available?scene.Revision:0;}}
        public IMappingSignals Signals{get{Check();return scene.Signals;}}
        private void Check(){if(!Available)throw new ObjectDisposedException("Mapping scene capability");scene.Signals.Check();}
        public ISceneEffect Activate(string owner,string effectId){Check();if(JKRuntime.World.WorldControl.Following)throw new InvalidOperationException("A following world can't activate scene effects");return new EffectLease(scene,scene.Activate(owner,effectId));}
        public SceneValue ReadVariable(string id){Check();return scene.ReadVariable(id);}
        public void WriteVariable(string id,SceneValue value){Check();if(JKRuntime.World.WorldControl.Following)throw new InvalidOperationException("A following world can't write scene variables");scene.SetVariable(id,value);}
        public void Emit(string id,string source,string actor,int screen,params SignalSample[] payload)
        {Check();if(JKRuntime.World.WorldControl.Following)throw new InvalidOperationException("A following world can't emit scene events");scene.Signals.Emit(id,source,actor,screen,scene.GameplayTime,payload);}
        public ComponentInfo[] InspectComponents()
        {
            Check();return scene.Components.Values.OrderBy(c=>c.Id,StringComparer.Ordinal).Select(c=>new ComponentInfo(c.Id,c.Descriptor.Kind,c.Tags,Array.ConvertAll(c.Descriptor.Fields,f=>f.Name),Array.ConvertAll(c.Descriptor.Fields,f=>scene.Properties.Read(c.Id,f.Name)),Array.ConvertAll(c.Descriptor.Fields,f=>scene.Properties.Require(c.Id+"/"+f.Name).Owner))).ToArray();
        }
        public ComponentPropertyInfo[] DescribeProperties(string component)
        {
            Check();CompiledComponent value;if(!scene.Components.TryGetValue(component,out value))throw new ArgumentException("Unknown component: "+component);
            return Array.ConvertAll(value.Descriptor.Fields,f=>new ComponentPropertyInfo(f.Name,f.Default,f.Units,f.Minimum,f.Maximum,f.Bindable,f.Impact.ToString(),f.Reference.ToString(),f.Choices));
        }
        public EffectInfo[] InspectEffects(){Check();return scene.EffectInfo();}
        public string[] InspectErrors(){Check();return scene.Errors;}
        public IDisposable Register(string source,object texture,Action<bool> pause,Action stop)
        {Check();var current=NativeScene.Current;if(current==null||!ReferenceEquals(current.Program,scene))throw new InvalidOperationException("Media requires the active native scene");return current.Media.Register(source,texture,pause,stop);}
        public void Dispose(){disposed=true;}
        private sealed class EffectLease:ISceneEffect
        {
            private SceneProgram owner;
            private readonly long revision;
            public long Id{get;private set;}
            internal EffectLease(SceneProgram scene,long id){owner=scene;revision=scene.Revision;Id=id;}
            public bool Active{get{return owner!=null&&owner.Signals.Available&&owner.Revision==revision&&owner.EffectActive(Id);}}
            public double RemainingSeconds{get{return Active?owner.Remaining(Id):0;}}
            public void Dispose(){if(Active&&!JKRuntime.World.WorldControl.Following){owner.Signals.Check();owner.Cancel(Id);}owner=null;}
        }
    }
    internal sealed partial class SceneProgram
    {
        internal EffectInfo[] EffectInfo(){return activeEffects.Select(e=>new EffectInfo(e.Id,e.Definition,e.Owner,Remaining(e.Id))).ToArray();}
    }
}
