using System;
using System.Collections.Generic;

namespace MegaMappingExpansion
{
    internal sealed class ScreenRenderPlan
    {
        internal readonly RenderCommand[] Background, World, Foreground, ReflectionOccluders;
        internal readonly WaterData[] CompositeWaters;
        internal readonly PuddleData[] Puddles;
        internal ScreenRenderPlan(RenderCommand[] background, RenderCommand[] world, RenderCommand[] foreground,
            RenderCommand[] occluders, WaterData[] waters, PuddleData[] puddles)
        { Background = background; World = world; Foreground = foreground; ReflectionOccluders = occluders; CompositeWaters = waters; Puddles = puddles; }
        internal RenderCommand[] Layer(string phase)
        { return phase == "background" ? Background : phase == "world" ? World : Foreground; }
    }

    internal sealed partial class SceneHost
    {
        private readonly Dictionary<int, ScreenRenderPlan> renderPlans = new Dictionary<int, ScreenRenderPlan>();
        private RenderCommand[] CreateLayerQueue(int current, string layer)
        {
            List<RenderCommand> queue = new List<RenderCommand>(); int order = 0;
            foreach (SceneText text in scene.Texts)
                if (text.Space == "world" && text.Screen == current && text.Layer == layer)
                { SceneText item = text; queue.Add(new RenderCommand(item.Z, order++, delegate { DrawText(item); }, item.Id)); }
            foreach(PlanetData planet in scene.Planets)
                if(planet.Screen==current&&string.Equals(planet.Layer,layer,StringComparison.OrdinalIgnoreCase))
                {PlanetData item=planet;queue.Add(new RenderCommand(item.Z,order++,delegate{DrawPlanet(item);},item.Id));}
            foreach (SurfData surf in scene.Surfs)
            {
                if(!surf.BakedBody && surf.Screen==current && string.Equals(surf.Layer,layer,StringComparison.OrdinalIgnoreCase))
                { SurfData item=surf;queue.Add(new RenderCommand(item.Z,order++,delegate{DrawSurf(item);},item.Id)); }
                if(surf.Screen==current)foreach(SurfImpactData impact in surf.Impacts)
                    if(string.Equals(impact.Layer,layer,StringComparison.OrdinalIgnoreCase))
                    {SurfData item=surf;SurfImpactData contact=impact;queue.Add(new RenderCommand(contact.Z,order++,delegate{DrawSurfImpact(item,contact);},item.Id+"-impact-"+order));}
            }
            foreach (RainData rain in scene.Rains)
                if (rain.Screen == current && string.Equals(rain.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { RainData item=rain; queue.Add(new RenderCommand(item.Z,order++,delegate { DrawRain(item); },item.Id)); }
            foreach (PropData prop in scene.Props ?? new PropData[0])
                if (prop.Screen == current && string.Equals(prop.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { PropData item = prop; queue.Add(new RenderCommand(prop.Z, order++, delegate { DrawProp(item, false); }, item.Id)); }
            foreach (PropData node in scene.Nodes ?? new PropData[0])
                if (node.Screen == current && string.Equals(node.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { PropData item = node; queue.Add(new RenderCommand(node.Z, order++, delegate { DrawProp(item, true); }, item.Id)); }
            foreach (WaterData water in scene.Waters ?? new WaterData[0])
                if (water.Screen == current && string.Equals(water.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { WaterData item = water; queue.Add(new RenderCommand(water.Z, order++, delegate { DrawWater(item); }, item.Id)); }
            foreach (FogData fog in scene.Fogs ?? new FogData[0])
                if (fog.Screen == current && string.Equals(fog.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { FogData item = fog; queue.Add(new RenderCommand(fog.Z, order++, delegate { DrawFog(item); }, item.Id)); }
            foreach (LightData light in scene.Lights ?? new LightData[0])
                if (light.Screen == current && string.Equals(light.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { LightData item = light; queue.Add(new RenderCommand(light.Z, order++, delegate { DrawLight(item); }, item.Id)); }
            foreach (EmitterData emitter in scene.Emitters ?? new EmitterData[0])
                if (emitter.Screen == current && string.Equals(emitter.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { EmitterData item = emitter; queue.Add(new RenderCommand(emitter.Z, order++, delegate { DrawEmitter(item); }, item.Id)); }
            if (string.Equals(layer, "world", StringComparison.OrdinalIgnoreCase))
                foreach (BushData bush in scene.Bushes ?? new BushData[0]) if (bush.Screen == current)
                { BushData item = bush; queue.Add(new RenderCommand(0f, order++, delegate { DrawBush(item); }, item.Id)); }
            queue.Sort();
            return queue.ToArray();
        }

        private void BuildRenderQueues()
        {
            HashSet<int> screens = new HashSet<int>();
            foreach (SceneText item in scene.Texts) screens.Add(item.Screen);
            foreach (PropData item in scene.Props) { screens.Add(item.Screen); propIds.Add(item.Id); }
            foreach (PropData item in scene.Nodes) { screens.Add(item.Screen); propIds.Add(item.Id); }
            foreach (WaterData item in scene.Waters) screens.Add(item.Screen);
            foreach (LightData item in scene.Lights) screens.Add(item.Screen);
            foreach (FogData item in scene.Fogs) screens.Add(item.Screen);
            foreach (EmitterData item in scene.Emitters) screens.Add(item.Screen);
            foreach (BushData item in scene.Bushes) screens.Add(item.Screen);
            foreach (RainData item in scene.Rains) screens.Add(item.Screen);
            foreach (SurfData item in scene.Surfs) screens.Add(item.Screen);
            foreach (PlanetData item in scene.Planets) screens.Add(item.Screen);
            foreach (PuddleData item in scene.Puddles) screens.Add(item.Screen);
            foreach (int current in screens)
            {
                List<RenderCommand> occluders = new List<RenderCommand>();
                foreach (PropData prop in scene.Props)
                    if (prop.Screen == current && prop.OccludesReflection)
                    { PropData item = prop; occluders.Add(new RenderCommand(item.Z, occluders.Count, delegate { DrawProp(item, false); }, item.Id)); }
                foreach (PropData prop in scene.Nodes)
                    if (prop.Screen == current && prop.OccludesReflection)
                    { PropData item = prop; occluders.Add(new RenderCommand(item.Z, occluders.Count, delegate { DrawProp(item, true); }, item.Id)); }
                occluders.Sort();
                renderPlans.Add(current, new ScreenRenderPlan(CreateLayerQueue(current, "background"),
                    CreateLayerQueue(current, "world"), CreateLayerQueue(current, "foreground"), occluders.ToArray(),
                    Array.FindAll(scene.Waters, delegate(WaterData water) { return water.Screen == current && water.CompositeReflection; }),
                    Array.FindAll(scene.Puddles, delegate(PuddleData puddle) { return puddle.Screen == current; })));
            }
        }
    }
}
