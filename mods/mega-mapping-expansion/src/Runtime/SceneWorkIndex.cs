using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaMappingExpansion
{
    // Immutable screen membership. Dynamic attachment poses are resolved separately;
    // ordinary off-screen props do not need a pose merely to advance their clock.
    internal sealed class ScreenIndex<T>
    {
        private readonly Dictionary<int, T[]> screens;
        private static readonly T[] Empty = new T[0];
        internal ScreenIndex(IEnumerable<T> values, Func<T, int> screen)
        { screens = values.GroupBy(screen).ToDictionary(g => g.Key, g => g.ToArray()); }
        internal T[] At(int screen)
        { T[] result; return screens.TryGetValue(screen, out result) ? result : Empty; }
    }

    internal sealed class SceneWorkIndex
    {
        internal readonly ScreenIndex<PropData> Props;
        internal readonly ScreenIndex<WaterData> Waters;
        internal readonly ScreenIndex<PuddleData> Puddles;
        internal readonly ScreenIndex<LightData> Lights;
        internal ScreenIndex<SceneAnchor> LightBlockers { get; private set; }
        internal readonly PropData[] Attachments;
        internal SceneWorkIndex(SceneFile scene)
        {
            var props = scene.Props.Concat(scene.Nodes).ToArray();
            Props = new ScreenIndex<PropData>(props, p => p.Screen);
            Waters = new ScreenIndex<WaterData>(scene.Waters, w => w.Screen);
            Puddles = new ScreenIndex<PuddleData>(scene.Puddles, p => p.Screen);
            Lights = new ScreenIndex<LightData>(scene.Lights, l => l.Screen);
            RefreshBlockers(scene);
            Attachments = Array.FindAll(props, p => !string.IsNullOrEmpty(p.Attach));
        }
        internal void RefreshBlockers(SceneFile scene)
        { LightBlockers = new ScreenIndex<SceneAnchor>(scene.Anchors.Where(a => a.BlocksLight && a.LightOpacity > 0), a => a.Screen); }
    }
}
