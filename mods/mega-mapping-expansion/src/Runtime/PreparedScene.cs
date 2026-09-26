using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MegaMappingExpansion
{
    internal enum MotionKind { None, Rotate, Orbit, Linear, Bob, Sway, Path }
    internal enum TrackProperty { X, Y, Rotation, Scale, ScaleX, ScaleY, Opacity, Brightness }

    internal sealed class PreparedTrack
    {
        internal readonly TrackProperty Property;
        internal readonly float Cycles, Phase;
        private readonly TrackKey[] keys;
        private readonly string easing;
        internal PreparedTrack(TrackData data, string id)
        {
            Property = (TrackProperty)Enum.Parse(typeof(TrackProperty), data.Property, true);
            Cycles = data.Cycles; Phase = data.Phase; easing = data.Easing;
            keys = SceneValidation.ParseTrack(data.Keys, id + "." + data.Property);
        }
        internal float Evaluate(float phase) { return SceneAnimation.Interpolate(keys, easing, phase); }
    }

    internal sealed class PreparedProp
    {
        internal readonly PropData Data;
        internal readonly MotionKind Motion;
        internal readonly bool LinearSampling;
        internal readonly Vector2[] Path;
        internal readonly PreparedTrack[] Tracks;
        internal readonly Color Tint, RimColor;
        internal PreparedProp(PropData data)
        {
            Data = data;
            LinearSampling = string.Equals(data.Sampling, "linear", StringComparison.OrdinalIgnoreCase);
            Motion = (MotionKind)Enum.Parse(typeof(MotionKind), data.Motion, true);
            Path = Motion == MotionKind.Path ? SceneValidation.ParsePath(data.Path, data.Id) : new Vector2[0];
            Tracks = Array.ConvertAll(data.Tracks ?? new TrackData[0], delegate(TrackData track) { return new PreparedTrack(track, data.Id); });
            Tint = SceneValidation.ParseColor(data.Tint, data.Id + ".tint");
            RimColor = SceneValidation.ParseColor(data.RimColor, data.Id + ".rimColor");
        }
    }

    // Scene-owned derived values. Behavior changes refresh affected props;
    // authored source DTOs are copied before this cache is constructed.
    internal sealed class PreparedScene
    {
        private readonly Dictionary<PropData, PreparedProp> props = new Dictionary<PropData, PreparedProp>();
        private readonly Dictionary<string, Color> colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        internal PreparedScene(SceneFile scene)
        {
            foreach (PropData prop in scene.Props) props.Add(prop, new PreparedProp(prop));
            foreach (PropData prop in scene.Nodes) props.Add(prop, new PreparedProp(prop));
            Add("#000000"); Add(scene.Options.Tint); Add(scene.Options.PlayerRimColor); Add(scene.Options.AmbientLight);
            foreach (LightData item in scene.Lights) { Add(item.Color); Add(item.ShadowColor); }
            foreach (WaterData item in scene.Waters) { Add(item.Color); Add(item.Color2); Add(item.Highlight); }
            foreach (FogData item in scene.Fogs) Add(item.Color);
            foreach (RainData item in scene.Rains) Add(item.Color);
            foreach (SurfData item in scene.Surfs) {Add(item.Color);Add(item.CrestColor);}
            foreach (PuddleData item in scene.Puddles) Add(item.Color);
            foreach (ShadowSurfaceData item in scene.ShadowSurfaces) Add(item.Color);
            foreach (ScreenLook item in scene.ScreenLooks) if(!string.IsNullOrEmpty(item.AmbientLight)) Add(item.AmbientLight);
            foreach (EmitterData item in scene.Emitters) Add(item.Tint);
            foreach (BushData item in scene.Bushes) { Add(item.BackColor); Add(item.FrontColor); Add(item.HighlightColor); }
        }
        private void Add(string text)
        {
            if (colors.ContainsKey(text)) return;
            if (colors.Count >= 4096) colors.Clear();
            colors.Add(text, SceneValidation.ParseColor(text, "material"));
        }
        internal PreparedProp Prop(PropData data) { return props[data]; }
        internal Color Color(string text) { Add(text); return colors[text]; }
        internal void Refresh(PropData prop) { props[prop] = new PreparedProp(prop); }
    }

    internal sealed class LoadedScene
    {
        internal readonly SceneFile Data;
        internal readonly Dictionary<string, byte[]> CompiledAssets;
        internal LoadedScene(SceneFile data, Dictionary<string, byte[]> assets) { Data = data; CompiledAssets = assets; }
        internal static LoadedScene Load(string root, int screens)
        {
            SceneFile data = SceneValidation.Read(root);
            if (data == null) return null;
            Dictionary<string, byte[]> assets;
            bool cached = CompiledSceneCache.TryLoad(root, data, out assets);
            SceneValidation.Validate(data, root, screens, !cached);
            if (cached) SceneResourceBudget.Validate(data, root, true, assets);
            return new LoadedScene(data, cached ? assets : null);
        }
    }
}
