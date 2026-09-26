using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using JumpKing;

namespace JKRuntime
{
    // Tracks only objects that have been drawn in the current capture window.
    // No state is written back to the game. Reflection descriptors are prepared once.
    internal static class AnimationDiagnostics
    {
        private const BindingFlags Flags = DiagnosticHooks.Flags;
        private static readonly Type Prop = typeof(Game1).Assembly.GetType("JumpKing.Props.LoopingProp", true);
        private static readonly Type Layer = typeof(JumpKing.Level.ScrollingBackground).GetNestedType("Layer", BindingFlags.NonPublic);
        private static readonly FieldInfo Layers = typeof(JumpKing.Level.ScrollingBackground).GetField("m_layers", Flags);
        private static readonly FieldInfo PropScreen = Prop.GetField("m_screen", Flags);
        private sealed class Shape
        {
            internal readonly FieldInfo Container, Time, Scroll, Index;
            internal Shape(Type type, string container, string time, string scroll, string index)
            {
                if (container != null) { Container = type.GetField(container, Flags); type = Container.FieldType; }
                Time = type.GetField(time, Flags); Scroll = scroll == null ? null : type.GetField(scroll, Flags); Index = type.GetField(index, Flags);
            }
            internal State Read(object value)
            {
                if (Container != null) value = Container.GetValue(value);
                return new State { Time=(float)Time.GetValue(value), Scroll=Scroll==null?0:(float)Scroll.GetValue(value), Index=(int)Index.GetValue(value) };
            }
        }
        private struct State
        {
            internal float Time, Scroll;
            internal int Index;
            internal bool Same(State other) { return Time==other.Time && Scroll==other.Scroll && Index==other.Index; }
        }
        private sealed class Track
        {
            internal object Value;
            internal Shape Shape;
            internal string Name;
            internal long Started;
            internal int Updates, Draws, FrameChanges, OutsideUpdate, DuringDraw, LastDrawIndex;
            internal double Delta;
            internal float MinDelta=float.MaxValue, MaxDelta;
            internal State Expected, BeforeDraw;
        }
        private static readonly Shape PropShape = new Shape(Prop, null, "m_timer", null, "_index");
        private static readonly Shape WeatherShape = new Shape(typeof(WeatherInstance), "m_index", "m_timer", null, "_index");
        private static readonly FieldInfo LayerFrame = Layer.GetField("m_frame_index", Flags);
        private static readonly FieldInfo WrappedIndex = LayerFrame.FieldType.GetField("_index", Flags);
        // Layer's frame index is stored in a separate WrappedIndex object.
        private static readonly Dictionary<object, Track> tracks = new Dictionary<object, Track>();
        private static readonly FieldInfo LayerTime = Layer.GetField("m_frame_timer", Flags), LayerScroll = Layer.GetField("m_timer", Flags);
        private static State Read(Track t)
        {
            return t.Shape != null ? t.Shape.Read(t.Value) : new State {
                Time=(float)LayerTime.GetValue(t.Value), Scroll=(float)LayerScroll.GetValue(t.Value),
                Index=(int)WrappedIndex.GetValue(LayerFrame.GetValue(t.Value)) };
        }
        internal static void Install(DiagnosticHooks hooks)
        {
            foreach(var type in new[] { Prop, Layer, typeof(WeatherInstance) })
                hooks.Add(type, "Update", typeof(AnimationDiagnostics), null, "Updated");
            hooks.Add(Prop, "Draw", typeof(AnimationDiagnostics), "PropBefore", "PropAfter");
            hooks.Add(typeof(WeatherInstance), "GetTexture", typeof(AnimationDiagnostics), "WeatherBefore", "WeatherAfter");
            foreach(string method in new[] { "DrawBackground", "DrawForeground" })
                hooks.Add(typeof(JumpKing.Level.ScrollingBackground), method, typeof(AnimationDiagnostics), "LayersBefore", "LayersAfter");
        }
        internal static void Reset() { tracks.Clear(); }
        private static bool Recording { get { return PerformanceDiagnostics.IsRecording; } }
        private static void Updated(object __instance, float p_delta)
        {
            Track t; if (!Recording || !tracks.TryGetValue(__instance,out t)) return;
            t.Updates++; t.Delta+=p_delta; t.MinDelta=Math.Min(t.MinDelta,p_delta); t.MaxDelta=Math.Max(t.MaxDelta,p_delta); t.Expected=Read(t);
        }
        private static void Before(object value, Shape shape, string name)
        {
            if(!Recording) return;
            Track t;
            if(!tracks.TryGetValue(value,out t))
            {
                if(tracks.Count==512) return;
                t=new Track { Value=value, Shape=shape, Name=name, Started=Stopwatch.GetTimestamp() };
                t.Expected=Read(t); tracks.Add(value,t);
            }
            State now=Read(t);
            if(!now.Same(t.Expected)) t.OutsideUpdate++;
            if(t.Draws!=0 && now.Index!=t.LastDrawIndex) t.FrameChanges++;
            t.LastDrawIndex=now.Index; t.Draws++; t.BeforeDraw=now;
        }
        private static void After(object value)
        {
            Track t; if(!Recording || !tracks.TryGetValue(value,out t)) return;
            State now=Read(t); if(!now.Same(t.BeforeDraw)) t.DuringDraw++; t.Expected=now;
        }
        private static void PropBefore(object __instance)
        { if(Recording && (int)PropScreen.GetValue(__instance)==Camera.CurrentScreen) Before(__instance,PropShape,"prop screen="+(Camera.CurrentScreen+1)); }
        private static void PropAfter(object __instance)
        { if(Recording && (int)PropScreen.GetValue(__instance)==Camera.CurrentScreen) After(__instance); }
        private static void WeatherBefore(object __instance) { Before(__instance,WeatherShape,"weather"); }
        private static void WeatherAfter(object __instance) { After(__instance); }
        private static void LayersBefore(object __instance)
        { if(Recording) foreach(object layer in (Array)Layers.GetValue(__instance)) Before(layer,null,"scrolling layer"); }
        private static void LayersAfter(object __instance)
        { if(Recording) foreach(object layer in (Array)Layers.GetValue(__instance)) After(layer); }
        internal static string Capture()
        {
            var text=new StringBuilder("\r\nNative animation state audit (objects observed drawing, cap 512):\r\n");
            long now=Stopwatch.GetTimestamp();
            foreach(var t in tracks.Values)
                text.AppendFormat(CultureInfo.InvariantCulture,"{0}: wall={1:F3}s updates={2} supplied={3:F3}s deltaMin/Max={4:F6}/{5:F6} draws={6} visibleFrameChanges={7} outsideUpdate={8} duringDraw={9}\r\n",
                    t.Name,(now-t.Started)/(double)Stopwatch.Frequency,t.Updates,t.Delta,t.Updates==0?0:t.MinDelta,t.MaxDelta,t.Draws,t.FrameChanges,t.OutsideUpdate,t.DuringDraw);
            return text.ToString();
        }
    }
}
