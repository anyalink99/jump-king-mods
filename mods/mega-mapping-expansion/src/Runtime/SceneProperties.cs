using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using MegaMappingExpansion.Api;

namespace MegaMappingExpansion
{
    internal sealed class SceneProperty
    {
        internal object Target, Baseline;
        internal string Id, Name, Owner = "authored";
        internal PropertyInfo Member;
        internal double Min, Max;
        internal string[] Choices;
        internal bool Color;
        internal object Read() { return Member.GetValue(Target, null); }
        internal void Write(object value) { Member.SetValue(Target, value, null); }
        internal static string Format(object value)
        { return value is bool ? System.Xml.XmlConvert.ToString((bool)value) : Convert.ToString(value, CultureInfo.InvariantCulture); }
        internal object Parse(string text, string mode = "set")
        {
            if (Member.PropertyType == typeof(bool)) return System.Xml.XmlConvert.ToBoolean(text);
            if (Member.PropertyType == typeof(string))
            {
                if (Color && !(Target is ScreenLook && Name == "ambientLight" && text == "")) SceneValidation.ParseColor(text, Id + "." + Name);
                if (Choices != null && !Array.Exists(Choices, v => string.Equals(v, text, StringComparison.Ordinal)))
                    throw new InvalidDataException(Id + "." + Name + ": unknown value '" + text + "'");
                return text;
            }
            double number = double.Parse(text, CultureInfo.InvariantCulture);
            double lower = mode == "set" ? Min : -1000000, upper = mode == "set" ? Max : 1000000;
            if (double.IsNaN(number) || double.IsInfinity(number) || number < lower || number > upper)
                throw new InvalidDataException(Id + "." + Name + ": expected " + lower + ".." + upper);
            if (Target is ScreenLook && Name == "ambientIntensity" && number < 0 && number != -1)
                throw new InvalidDataException(Id + "." + Name + ": use -1 to inherit, or 0..1");
            if (Member.PropertyType == typeof(int))
            { if (number != Math.Truncate(number)) throw new InvalidDataException(Id + "." + Name + ": integer required"); return (int)number; }
            return (float)number;
        }
        internal object Blend(object current, object operand, string mode, double gain)
        {
            if (gain <= 0) return current;
            if (!(current is float) && !(current is int)) return gain > 0 ? operand : current;
            double a = Convert.ToDouble(current), b = Convert.ToDouble(operand);
            double result = mode == "add" ? a + b * gain : mode == "multiply" ? a * (1 + (b - 1) * gain) : a + (b - a) * gain;
            result = Math.Max(Min, Math.Min(Max, result));
            return current is int ? (object)(int)Math.Round(result) : (float)result;
        }
    }

    internal sealed class SceneProperties
    {
        internal readonly Dictionary<string, SceneProperty> Values = new Dictionary<string, SceneProperty>(StringComparer.Ordinal);
        private readonly SceneFile scene;
        internal SceneProperties(SceneFile value)
        {
            scene = value;
            foreach (PropData p in scene.Props) Prop(p, false);
            foreach (PropData p in scene.Nodes) Prop(p, true);
            foreach (SceneText t in scene.Texts)
            { Add(t, t.Id, "Visible"); Add(t, t.Id, "Opacity", 0, 1); Add(t, t.Id, "Color", true); Add(t, t.Id, "X", -4096, 4096); Add(t, t.Id, "Y", -4096, 4096); }
            foreach (NativeActorData a in scene.NativeActors)
            { Add(a, a.Id, "Visible"); Add(a, a.Id, "TextVisible"); Add(a, a.Id, "Opacity", 0, 1); Add(a, a.Id, "Tint", true); Add(a, a.Id, "OffsetX", -4096, 4096); Add(a, a.Id, "OffsetY", -4096, 4096); }
            foreach (LightData l in scene.Lights)
            {
                Add(l, l.Id, "Enabled"); Add(l, l.Id, "Intensity", 0, 4); Add(l, l.Id, "Radius", 4, 1000);
                Add(l, l.Id, "Color", true); Add(l, l.Id, "X", -4096, 4096); Add(l, l.Id, "Y", -4096, 4096);
                Add(l, l.Id, "OffsetX", -4096, 4096); Add(l, l.Id, "OffsetY", -4096, 4096);
                Add(l, l.Id, "Angle", -360, 360); Add(l, l.Id, "RimIntensity", 0, 1);
            }
            foreach (FogData f in scene.Fogs) { Add(f, f.Id, "Opacity", 0, 1); Add(f, f.Id, "Speed", -300, 300); Add(f, f.Id, "Color", true); }
            foreach (RainData r in scene.Rains) { Add(r, r.Id, "Opacity", 0, 1); Add(r, r.Id, "Speed", 1, 1000); Add(r, r.Id, "Wind", -300, 300); Add(r, r.Id, "Color", true); }
            foreach (EmitterData e in scene.Emitters) { Add(e, e.Id, "Opacity", 0, 1); Add(e, e.Id, "DriftX", -300, 300); Add(e, e.Id, "DriftY", -300, 300); Add(e, e.Id, "Tint", true); }
            if (scene.Options.AdvancedLighting) foreach (SceneAnchor a in scene.Anchors)
            { Add(a, "anchor:" + a.Id, "BlocksLight"); Add(a, "anchor:" + a.Id, "LightOpacity", 0, 1); }
            foreach (WaterData w in scene.Waters)
            {
                Add(w, w.Id, "Opacity", 0, 1); Add(w, w.Id, "Color", true); Add(w, w.Id, "Color2", true); Add(w, w.Id, "Highlight", true);
                Add(w, w.Id, "Reflection"); Add(w, w.Id, "ReflectionOpacity", 0, 1); Add(w, w.Id, "SceneReflectionOpacity", 0, 1);
                Add(w, w.Id, "ReflectionScaleY", .051, 1); Add(w, w.Id, "Ripple", 0, 32); Add(w, w.Id, "Interactive");
                Add(w, w.Id, "SurfaceTension", .001, 200); Add(w, w.Id, "WaveSpread", 0, 300); Add(w, w.Id, "WaveDamping", 0, 50);
                Add(w, w.Id, "SplashStrength", 0, 10); Add(w, w.Id, "WakeStrength", 0, 10);
            }
            foreach (PuddleData p in scene.Puddles)
            {
                Add(p, p.Id, "Opacity", 0, 1); Add(p, p.Id, "Color", true); Add(p, p.Id, "ReflectionScaleY", .08, 1);
                Add(p, p.Id, "Ripple", 0, 4); Add(p, p.Id, "RainRings", 0, 64); Add(p, p.Id, "Perspective", 0, .02);
            }
            foreach (BushData b in scene.Bushes)
            {
                Add(b, b.Id, "Sway", 0, 32); Add(b, b.Id, "Speed", 0, 10); Add(b, b.Id, "ReactRadius", 0, 480); Add(b, b.Id, "ReactStrength", 0, 64);
                Add(b, b.Id, "BackColor", true); Add(b, b.Id, "FrontColor", true); Add(b, b.Id, "HighlightColor", true);
            }
            foreach (SurfData s in scene.Surfs)
            {
                Add(s, s.Id, "Period", .5, 300); Add(s, s.Id, "Height", 0, 180); Add(s, s.Id, "Wind", -300, 300);
                Add(s, s.Id, "Spray", 0, 512); Add(s, s.Id, "Break", 0, 1.6); Add(s, s.Id, "Chop", 0, 12); Add(s, s.Id, "FoamDetail", 0, 1);
                Add(s, s.Id, "Color", true); Add(s, s.Id, "CrestColor", true);
            }
            foreach (PlanetData p in scene.Planets)
            { Add(p, p.Id, "Period", 1, 86400); Add(p, p.Id, "CloudPeriod", 1, 86400); Add(p, p.Id, "CloudOpacity", 0, 1); }
            foreach (ShadowSurfaceData s in scene.ShadowSurfaces)
            { Add(s, s.Id, "Opacity", 0, 1); Add(s, s.Id, "Color", true); Add(s, s.Id, "ScaleY", .05, 3); Add(s, s.Id, "ShearX", -4, 4); }
            Add(scene.Options, "options", "AmbientIntensity", 0, 1); Add(scene.Options, "options", "AmbientLight", true);
            Add(scene.Options, "options", "Tint", true); Add(scene.Options, "options", "TintOpacity", 0, 1);
            Add(scene.Options, "options", "PlayerRimOpacity", 0, 1);
            foreach (ScreenLook look in scene.ScreenLooks)
            { string id = "screen:" + look.Screen; Add(look, id, "AmbientScale", 0, 1); Add(look, id, "AmbientLight", true); Add(look, id, "AmbientIntensity", -1, 1); Add(look, id, "PlayerRimScale", 0, 1); }
        }
        private void Prop(PropData p, bool vector)
        {
            Add(p, p.Id, "Visible"); Add(p, p.Id, "Opacity", 0, 1); Add(p, p.Id, "Tint", true);
            Add(p, p.Id, "X", -4096, 4096); Add(p, p.Id, "Y", -4096, 4096); Add(p, p.Id, "Rotation", -36000, 36000);
            Add(p, p.Id, "OffsetX", -4096, 4096); Add(p, p.Id, "OffsetY", -4096, 4096);
            Add(p, p.Id, "Scale", .001, 32); Add(p, p.Id, "ScaleX", .001, 32); Add(p, p.Id, "ScaleY", .001, 32);
            Add(p, p.Id, "AmplitudeX", -4096, 4096); Add(p, p.Id, "AmplitudeY", -4096, 4096); Add(p, p.Id, "Degrees", -36000, 36000);
            Add(p, p.Id, "WindStrength", 0, 12); Add(p, p.Id, "Duration", .001, 86400);
            Add(p, p.Id, "Motion", false, new[] { "none", "rotate", "orbit", "linear", "bob", "sway", "path" });
            Add(p, p.Id, vector ? "Asset" : "Texture", false, vector ? Array.ConvertAll(scene.VectorAssets, a => a.Id) : Array.ConvertAll(scene.Textures, a => a.Id));
        }
        private void Add(object target, string id, string member, double min, double max)
        { Add(target, id, member, false, null, min, max); }
        private void Add(object target, string id, string member, bool color = false, string[] choices = null, double min = 0, double max = 1)
        {
            var p = new SceneProperty { Target = target, Id = id, Member = target.GetType().GetProperty(member),
                Name = char.ToLowerInvariant(member[0]) + member.Substring(1), Color = color, Choices = choices, Min = min, Max = max };
            p.Baseline = p.Read(); Values.Add(id + "/" + p.Name, p);
        }
        internal SceneProperty Require(SceneChange change)
        {
            SceneProperty value;
            if (change == null || !Values.TryGetValue(change.Target + "/" + change.Property, out value))
                throw new InvalidDataException("Unsupported scene property: " + (change == null ? "null" : change.Target + "/" + change.Property));
            return value;
        }
    }
}
