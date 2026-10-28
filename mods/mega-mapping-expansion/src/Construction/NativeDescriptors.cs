using System;
using System.IO;
using System.Linq;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal static partial class PrimitiveRegistry
    {
        private static void AddNativeAdapters(ComponentRegistry r)
        {
            r.Add(new ComponentDescriptor("endingPresentation",SceneStateScope.Attempt,
                T("role","main",ReferenceKind.None,"main","nbp","owl"),B("credits",true),B("picture",true),B("statistics",true)));
            r.Add(new ComponentDescriptor("endingActor",SceneStateScope.Attempt,
                T("role","king",ReferenceKind.None,"king","babe","cherub","hanging_babe","crow","gargoyle","bird"),B("visible",true)));
            r.Add(new ComponentDescriptor("media",SceneStateScope.Transient,T("source"),B("active")));
            r.Add(new ComponentDescriptor("intro",SceneStateScope.Attempt,I("order",0,-1024,1024),
                new FieldDescriptor("value",SceneValue.String("")),T("font","menu"),C("color"),
                N("fadeIn",.7,0,60,"seconds"),N("stay",2.5,0,300,"seconds"),N("fadeOut",.7,0,60,"seconds"),B("enabled",true)));
            r.Add(new ComponentDescriptor("audio", SceneStateScope.Attempt,
                T("path"), T("bus", "SFX", ReferenceKind.None, "SFX", "Ambience", "Music"),
                B("playing"), B("loop"), N("volume", 1, 0, 1), N("fadeIn", 0, 0, 60, "seconds"), N("fadeOut", 0, 0, 60, "seconds")));
            r.Add(new ComponentDescriptor("npc", SceneStateScope.Attempt,
                T("name"), T("voice", reference: ReferenceKind.Component), B("muted"), N("linePause", -1, -1, 60, "seconds"),
                B("quoteOverride"),new FieldDescriptor("quote",SceneValue.String("")),V("offset"),C("tint"),N("opacity",1,0,1),B("visible",true),B("textVisible",true)));
            r.Add(new ComponentDescriptor("item", SceneStateScope.Attempt, T("name")));
            r.Add(new ComponentDescriptor("hiddenWall", SceneStateScope.Attempt,
                T("texture"), V("position",impact:ResourceImpact.Structural), new FieldDescriptor("matchPosition",SceneValue.Boolean(false),impact:ResourceImpact.Structural)));
            r.Add(new ComponentDescriptor("camera", SceneStateScope.Attempt,
                I("screen", 1, 1, 4096), V("position", 240, 180), B("active"),
                T("transition", "cut", ReferenceKind.None, "cut", "smooth"), N("duration", 1, 0, 60, "seconds"),
                N("returnDuration", .5, 0, 60, "seconds"), I("priority", 0, -1000, 1000),
                T("fallback", "cut", ReferenceKind.None, "cut", "error")));
            r.Add(new ComponentDescriptor("ending", SceneStateScope.Attempt,
                T("role", "main", ReferenceKind.None, "main", "nbp", "owl"),
                T("mode", "replace", ReferenceKind.None, "replace", "and", "or"), B("condition"),
                I("screen",0,0,4096),V("position"),new FieldDescriptor("size",SceneValue.Vector(0,0),0,1000000,"px"),B("requireGrounded",true)));
            r.Add(new ComponentDescriptor("result",SceneStateScope.Attempt,T("page","RESULTS"),
                T("role","any",ReferenceKind.None,"any","main","nbp","owl"),I("order",0,-1024,1024),
                new FieldDescriptor("label",SceneValue.String("")),new FieldDescriptor("value",SceneValue.String("")),C("color"),new FieldDescriptor("background",SceneValue.Color(0,0,0,1)),B("enabled",true)));
        }
    }

    internal static class NativeDescriptors
    {
        internal static void Declare(SceneProgram scene)
        {
            foreach (var component in scene.Components.Values)
            {
                string id=component.Id;
                switch(component.Descriptor.Kind)
                {
                    case "npc":
                        Signal(scene,id,"present",SceneValue.Boolean(false));
                        Signal(scene,id,"talking",SceneValue.Boolean(false));
                        Signal(scene,id,"line",SceneValue.String(""));
                        Signal(scene,id,"revealed",SceneValue.Integer(0));
                        Signal(scene,id,"position",SceneValue.Vector(0,0),"px");
                        Signal(scene,id,"screen",SceneValue.Integer(1),"screen");break;
                    case "item":
                        Signal(scene,id,"count",SceneValue.Integer(0));
                        Signal(scene,id,"owned",SceneValue.Boolean(false));
                        Signal(scene,id,"equipped",SceneValue.Boolean(false));break;
                    case "hiddenWall":Signal(scene,id,"touching",SceneValue.Boolean(false));break;
                    case "audio":Signal(scene,id,"audible",SceneValue.Boolean(false));break;
                    case "camera":Signal(scene,id,"selected",SceneValue.Boolean(false));break;
                    case "text":
                        Signal(scene,id,"revealed",SceneValue.Integer(0));Signal(scene,id,"complete",SceneValue.Boolean(false));
                        Signal(scene,id,"content",SceneValue.String(""));break;
                }
            }
        }
        private static void Signal(SceneProgram scene,string id,string name,SceneValue value,string units="")
        { scene.Signals.Declare(new SignalDescriptor(id+"."+name,value.Kind,units,true),value); }
        internal static void Validate(SceneProgram scene)
        {
            NativeResults.Validate(scene);NativeNarrativeText.Validate(scene);
            foreach(string kind in new[]{"audio","camera","npc","item","hiddenWall"})
                if(scene.Components.Values.Count(c=>c.Descriptor.Kind==kind)>64)throw new InvalidDataException("At most 64 "+kind+" components are supported per scene");
            var endings=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var presentations=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var actors=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var component in scene.Components.Values)
            {
                string id=component.Id,kind=component.Descriptor.Kind;
                if(kind=="endingPresentation"&&!presentations.Add(scene.Properties.Read(id,"role").Text))throw new InvalidDataException("Only one presentation may own an ending role");
                if(kind=="endingActor"&&!actors.Add(scene.Properties.Read(id,"role").Text))throw new InvalidDataException("Only one presentation may own an actor role");
                if(kind=="media"&&string.IsNullOrWhiteSpace(scene.Properties.Read(id,"source").Text))throw new InvalidDataException(id+": media requires a source key");
                if(kind=="material"&&scene.Properties.Read(id,"source").Text!=""&&scene.Properties.Read(id,"capture").Text!="")throw new InvalidDataException(id+": material cannot use both a media source and a capture");
                if(kind=="capture")
                {
                    bool hold=scene.Properties.Read(id,"mode").Text=="hold";
                    if(hold&&string.IsNullOrWhiteSpace(scene.Properties.Read(id,"selection").Text))throw new InvalidDataException(id+": hold captures require an explicit selection");
                    if(!hold&&scene.Properties.Read(id,"onVictory").X!=0)throw new InvalidDataException(id+": onVictory requires hold mode");
                }
                if(kind=="npc"||kind=="item")
                    if(string.IsNullOrWhiteSpace(scene.Properties.Read(id,"name").Text))throw new InvalidDataException(id+": a native name is required");
                if(kind=="npc")
                {
                    string voice=scene.Properties.Read(id,"voice").Text;
                    if(voice!=""&&scene.Components[voice].Descriptor.Kind!="audio")throw new InvalidDataException(id+": voice must reference an audio component");
                }
                if(kind=="hiddenWall"&&string.IsNullOrWhiteSpace(scene.Properties.Read(id,"texture").Text))throw new InvalidDataException(id+": texture is required");
                if(kind=="audio")
                {
                    string path=scene.Properties.Read(id,"path").Text;
                    if(string.IsNullOrWhiteSpace(path)||!(path.EndsWith(".wav",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".xnb",StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException(id+": audio path must name a map-relative .wav or .xnb sound");
                }
                if(kind=="ending"&&!endings.Add(scene.Properties.Read(id,"role").Text))throw new InvalidDataException("Only one condition adapter may own an ending role");
                if(kind=="ending")
                {
                    var size=scene.Properties.Read(id,"size");
                    if((size.X!=0||size.Y!=0)&&(size.X<1||size.Y<1))throw new InvalidDataException(id+": ending area needs dimensions of at least one pixel, or 0,0 to omit it");
                }
            }
        }
    }
}
