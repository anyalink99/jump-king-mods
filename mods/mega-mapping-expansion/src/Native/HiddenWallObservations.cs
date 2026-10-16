using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JKRuntime.World;
using JKRuntime.Gameplay;
using JumpKing;

namespace MegaMappingExpansion.Construction
{
    internal sealed class HiddenWallObservations:IDisposable
    {
        private readonly IDisposable subscription;
        private readonly SceneProgram scene;
        private readonly Func<bool> enabled;
        private readonly HashSet<string> requested;
        private static IEnumerable<string> Events(ControlNodeData node)
        {if(node.Operation=="waitEvent")yield return node.Target;foreach(var child in node.Nodes)foreach(string id in Events(child))yield return id;}
        private static HashSet<string> Requests(SceneDocument document)
        {
            var ids=document.Rules.Select(r=>r.Event).Concat(document.Trees.Select(t=>t.Event)).Concat(document.Trees.Select(t=>t.StopEvent)).Concat(document.Trees.SelectMany(t=>t.Nodes.SelectMany(Events)));
            return new HashSet<string>(ids.Where(id=>id!=null&&(id.StartsWith("hiddenwallenter:",StringComparison.Ordinal)||id.StartsWith("hiddenwallexit:",StringComparison.Ordinal))),StringComparer.Ordinal);
        }
        internal static void Validate(SceneDocument document)
        {
            var requested=Requests(document);if(requested.Count==0)return;
            var textures=new HashSet<string>(Game1.instance.contentManager.props.RaymanScreens.Values.SelectMany(s=>s.walls).Select(w=>w.texture_name),StringComparer.Ordinal);
            foreach(string id in requested)if(!textures.Contains(id.Substring(id.IndexOf(':')+1)))throw new InvalidDataException(id+": no native hidden wall has this texture_name");
        }
        internal HiddenWallObservations(SceneProgram owner,Func<bool> active)
        {scene=owner;enabled=active;requested=Requests(owner.Definition);if(requested.Count>0)subscription=NativeWorldInteractions.SubscribeHiddenWalls(Observe);}
        private void Observe(HiddenWallContact contact)
        {
            if(!enabled()||NativePause.IsPaused||JKRuntime.UI.UIApi.IsOpen||WorldControl.Following)return;string id=(contact.Touching?"hiddenwallenter:":"hiddenwallexit:")+contact.Texture;
            if(requested.Contains(id))scene.Signals.Emit(id,contact.Texture,"player",contact.Screen,scene.GameplayTime);
        }
        public void Dispose(){if(subscription!=null)subscription.Dispose();}
    }
}
