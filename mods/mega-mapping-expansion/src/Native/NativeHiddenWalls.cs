using System;
using System.Linq;
using JKRuntime;
using JKRuntime.World;

namespace MegaMappingExpansion
{
    internal static class NativeHiddenWalls
    {
        private static RuntimeScope scope;
        private static System.Collections.Generic.HashSet<string> requested=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        internal static bool IsEvent(string name)
        {return name!=null&&(name.StartsWith("hiddenwallenter:",StringComparison.Ordinal)||name.StartsWith("hiddenwallexit:",StringComparison.Ordinal));}
        internal static void Configure(SceneFile scene)
        {
            var events=scene==null?new string[0]:BehaviorTreeCompiler.Events(scene).Where(IsEvent).ToArray();
            if(events.Length==0){Release();return;}
            requested=new System.Collections.Generic.HashSet<string>(events,StringComparer.Ordinal);
            if(scope!=null)return;
            var owned=new RuntimeScope();
            try{NativeWorldInteractions.Prepare(owned);owned.Own(NativeWorldInteractions.SubscribeHiddenWalls(Observe));scope=owned;}
            catch{owned.Dispose();throw;}
        }
        private static void Observe(HiddenWallContact value)
        {
            var host=SceneHost.Current;if(!MappingSettings.Enabled||host==null)return;
            string name=(value.Touching?"hiddenwallenter:":"hiddenwallexit:")+value.Texture;
            if(requested.Contains(name))host.behaviors.Emit(name,value.Screen);
        }
        internal static void Release() {if(scope!=null){scope.Dispose();scope=null;}requested.Clear();}
    }
}
