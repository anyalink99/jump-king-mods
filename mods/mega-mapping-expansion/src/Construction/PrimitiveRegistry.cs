using MegaMappingExpansion.Api;

namespace MegaMappingExpansion.Construction
{
    internal static partial class PrimitiveRegistry
    {
        private static FieldDescriptor N(string name, double value = 0, double min = -1000000, double max = 1000000,
            string units = "", ResourceImpact impact = ResourceImpact.None)
        { return new FieldDescriptor(name, SceneValue.Number(value), min, max, units, impact); }
        private static FieldDescriptor V(string name, double x = 0, double y = 0, string units = "px", ResourceImpact impact = ResourceImpact.None)
        { return new FieldDescriptor(name, SceneValue.Vector(x, y), -1000000, 1000000, units, impact); }
        private static FieldDescriptor T(string name, string value = "", ReferenceKind reference = ReferenceKind.None, params string[] choices)
        { return new FieldDescriptor(name, SceneValue.String(value), impact: ResourceImpact.Structural, reference: reference, choices: choices); }
        private static FieldDescriptor B(string name, bool value = false) { return new FieldDescriptor(name, SceneValue.Boolean(value)); }
        private static FieldDescriptor I(string name,int value,int min,int max,string units="",ResourceImpact impact=ResourceImpact.Structural){return new FieldDescriptor(name,SceneValue.Integer(value),min,max,units,impact);}
        private static FieldDescriptor Z(){return N("z",impact:ResourceImpact.Membership);}
        private static FieldDescriptor C(string name) { return new FieldDescriptor(name, SceneValue.Color(1, 1, 1, 1), impact: ResourceImpact.Material); }
        internal static ComponentRegistry Create()
        {
            var r = new ComponentRegistry();
            r.Add(new ComponentDescriptor("transform", SceneStateScope.Attempt,
                V("position", impact: ResourceImpact.Pose), V("scale", 1, 1, "ratio", ResourceImpact.Pose), N("rotation", units: "degrees", impact: ResourceImpact.Pose),
                T("parent", reference: ReferenceKind.Component), I("screen", 1, 0, 4096, "screen", ResourceImpact.Membership), B("visible", true), B("pixelSnap", true)));
            r.Add(new ComponentDescriptor("sprite", SceneStateScope.Attempt, T("transform", reference: ReferenceKind.Component),
                T("asset", reference: ReferenceKind.Asset), T("material", reference: ReferenceKind.Component), V("origin"),
                N("frame", 0, 0, 65535, "frame"), N("frameTransition", 0, 0, 2, "seconds"), T("transition", "none", ReferenceKind.None, "none", "slide", "crossfade"),
                T("playback", "loop", ReferenceKind.None, "manual", "loop", "once", "pingpong"), T("clock", "gameplay", ReferenceKind.None, "gameplay", "presentation"),
                T("layer", "world", ReferenceKind.None, "background", "world", "surface", "foreground", "composite", "hud"), Z(), B("visible", true)));
            r.Add(new ComponentDescriptor("material", SceneStateScope.Attempt, C("color"), N("opacity", 1, 0, 1), N("emission", 0, 0, 8),
                T("sampling", "point", ReferenceKind.None, "point", "linear"), T("blend", "alpha", ReferenceKind.None, "alpha", "add", "multiply"),
                T("address", "clamp", ReferenceKind.None, "clamp", "wrap"), T("channel", "rgba", ReferenceKind.None, "rgba", "alpha"),
                T("mask", reference: ReferenceKind.Asset), T("maskSpace", "local", ReferenceKind.None, "local", "screen"), T("capture", reference: ReferenceKind.Component), V("uvOffset", units: "turns"), V("uvScale", 1, 1, "ratio"),
                N("ambient",1,0,8),N("diffuse",0,0,8),V("normalLight",0,0,"ratio")));
            r.Add(new ComponentDescriptor("mesh", SceneStateScope.Attempt, T("transform", reference: ReferenceKind.Component),
                T("asset", reference: ReferenceKind.Asset), T("material", reference: ReferenceKind.Component), T("geometry", "grid", ReferenceKind.None, "grid", "polygon", "disc"),
                V("size", 64, 64, impact:ResourceImpact.Geometry), T("outline"), I("columns", 16, 1, 128), I("rows", 16, 1, 128),
                T("projection", "planar", ReferenceKind.None, "planar", "spherical", "cylindrical", "perspective"), V("projectionTilt", units: "degrees",impact:ResourceImpact.Geometry),
                T("deformation", reference: ReferenceKind.Component), B("visible", true), T("layer", "world", ReferenceKind.None, "background", "world", "surface", "foreground", "composite"), Z()));
            r.Add(new ComponentDescriptor("light", SceneStateScope.Attempt, T("transform", reference: ReferenceKind.Component), C("color"),
                N("intensity", 1, 0, 8), N("radius", 64, 1, 2000, "px"), N("cone", 360, .1, 360, "degrees"), N("softness", .5, 0, 1), B("enabled", true),
                T("occluders", reference: ReferenceKind.Selection), N("scatter", 0, 0, 8), T("layer", "world", ReferenceKind.None, "background", "world", "foreground"), Z()));
            r.Add(new ComponentDescriptor("deformation", SceneStateScope.Attempt, T("field", reference: ReferenceKind.Component),
                V("pin"), V("extent", 1, 1), N("strength", 1), N("falloff", 2, .01, 16), T("axis", "both", ReferenceKind.None, "x", "y", "both"),
                T("space", "local", ReferenceKind.None, "local", "world"), T("next", reference: ReferenceKind.Component)));
            r.Add(new ComponentDescriptor("field", SceneStateScope.Attempt, T("function", "sine", ReferenceKind.None, "sine", "noise", "curve", "spring"),
                V("amplitude", 1, 1), V("frequency", 1, 1, "cycles/px"), V("origin"), N("phase", units: "turns"), N("decay", 0, 0, 100), T("curve"), I("seed", 0, 0, 2147483647),
                I("segments",64,2,256),N("length",480,.01,100000,"px"),V("direction",1,0,"ratio"),N("stiffness",32,.1,2000),N("propagation",120,0,2000),N("damping",4,0,100),N("limit",64,.001,10000,"px"),N("impulseRadius",.04,0,1),T("clock","gameplay",ReferenceKind.None,"gameplay","presentation")));
            r.Add(new ComponentDescriptor("emitter", SceneStateScope.Transient, T("transform", reference: ReferenceKind.Component), T("asset", reference: ReferenceKind.Asset),
                T("material", reference: ReferenceKind.Component), N("rate", 10, 0, 4096, "particles/s"), N("lifetime", 1, .001, 120, "seconds"),
                V("velocity"), V("acceleration"), V("spread"), V("area"), N("size", 1, .01, 64), I("seed", 0, 0, 2147483647),
                I("capacity", 256, 1, 16384), T("shape", "point", ReferenceKind.None, "point", "box", "line", "disc"), T("clock", "gameplay", ReferenceKind.None, "gameplay", "presentation"),
                T("layer", "world", ReferenceKind.None, "background", "world", "surface", "foreground", "composite"), Z(), B("enabled", true)));
            r.Add(new ComponentDescriptor("sensor", SceneStateScope.Attempt, T("transform", reference: ReferenceKind.Component),
                T("shape", "box", ReferenceKind.None, "box", "disc", "polygon", "surface"), V("size", 8, 8), T("outline"), T("selection", reference: ReferenceKind.Selection),
                T("actor", "player"), T("phase", "contact", ReferenceKind.None, "contact", "scene"), N("hysteresis", 0, 0, 64), N("dwell", 0, 0, 60, "seconds"),
                N("minimumSpeed", 0, 0, 10000, "px/s"), T("initial", "baseline", ReferenceKind.None, "baseline", "fire"), B("enabled", true)));
            r.Add(new ComponentDescriptor("stamp", SceneStateScope.Transient, T("asset", reference: ReferenceKind.Asset), T("material", reference: ReferenceKind.Component),
                T("mask", reference: ReferenceKind.Asset), N("duration", 1, .001, 120, "seconds"), I("capacity", 8, 1, 256), T("clock","gameplay",ReferenceKind.None,"gameplay","presentation"),
                T("layer", "world", ReferenceKind.None, "background", "world", "surface", "foreground", "composite"), Z()));
            r.Add(new ComponentDescriptor("capture", SceneStateScope.Transient, T("selection", reference: ReferenceKind.Selection),
                T("stage", "world", ReferenceKind.None, "background", "world", "foreground"), B("enabled", true)));
            r.Add(new ComponentDescriptor("text", SceneStateScope.Attempt, T("transform", reference: ReferenceKind.Component), T("material", reference: ReferenceKind.Component),
                T("font", "menu"), T("characters", "0123456789+- .:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"), N("spacing", 0, -64, 64), new FieldDescriptor("value", SceneValue.String("")), N("visibleCharacters",65535,0,65535,"characters"), N("width", 440, 1, 4096), T("align", "center", ReferenceKind.None, "left", "center", "right"),
                T("layer", "world", ReferenceKind.None, "background", "world", "surface", "foreground", "composite", "hud"), Z()));
            r.Add(new ComponentDescriptor("receiver", SceneStateScope.Attempt, T("transform", reference: ReferenceKind.Component), T("material", reference: ReferenceKind.Component),
                T("outline"), T("mode", "shadow", ReferenceKind.None, "shadow", "reflection", "occlusion"),
                V("projection", 0, .35, "ratio"), N("opacity", .5, 0, 1), T("layer", "surface", ReferenceKind.None, "background", "world", "surface", "foreground", "composite"), Z()));
            r.Add(new ComponentDescriptor("view",SceneStateScope.Attempt,I("screen",0,0,4096),C("ambient"),C("tint"),N("tintAmount",0,0,1),N("exposure",1,0,8),
                T("mirror","none",ReferenceKind.None,"none","horizontal","vertical","both"),T("timer","inherit",ReferenceKind.None,"inherit","show","hide"),T("intro"),B("animateWhilePaused",false)));
            AddNativeAdapters(r);
            r.DrawWith("sprite",(renderer,component)=>renderer.SpriteCommand(component));
            r.DrawWith("mesh",(renderer,component)=>renderer.MeshCommand(component));
            r.DrawWith("receiver",(renderer,component)=>renderer.ReceiverCommand(component));
            r.DrawWith("emitter",(renderer,component)=>renderer.EmitterCommand(component));
            r.DrawWith("light",(renderer,component)=>renderer.LightCommand(component));
            r.DrawWith("text",(renderer,component)=>renderer.TextCommand(component));
            r.DrawWith("stamp",(renderer,component)=>renderer.StampCommand(component));
            return r;
        }
    }
}
