using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using EntityComponent;
using EntityComponent.BT;
using JumpKing.Player;

namespace SubframeCharge
{
    // Independent of BodyComp.Enabled (e.g. giant boots). Bracket the native
    // player update without patching the global loop or owning another DLL.
    internal sealed class ChargeFrameComponents : IDisposable
    {
        private static readonly FieldInfo ComponentsField = typeof(Entity).GetField(
            "m_components", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<Component> components;
        private readonly Observer begin;
        private readonly Observer end;
        private readonly ChargeTimeline timeline;

        internal ChargeFrameComponents(Entity player, BodyComp body,
            BehaviorTreeComp tree, ChargeTimeline timeline)
        {
            this.timeline = timeline;
            components = ComponentsField == null ? null : ComponentsField.GetValue(player) as List<Component>;
            if (components == null || components.IndexOf(body) < 0
                || components.IndexOf(tree) <= components.IndexOf(body))
                throw new InvalidOperationException("SFC requires the native body-before-behaviour-tree component order");
            begin = new Observer(delegate { timeline.BeginFrame(Stopwatch.GetTimestamp()); });
            end = new Observer(timeline.EndFrame);
            try
            {
                PauseClockObserver.Attach(timeline);
                player.AddComponents(begin, end);
                components.Remove(begin);
                components.Remove(end);
                components.Insert(components.IndexOf(body), begin);
                components.Insert(components.IndexOf(tree) + 1, end);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            begin.Enabled = end.Enabled = false;
            components.Remove(begin);
            components.Remove(end);
            PauseClockObserver.Detach(timeline);
        }

        private sealed class Observer : Component
        {
            private readonly Action observe;
            internal Observer(Action action) { observe = action; }
            protected override void Update(float delta) { observe(); }
        }
    }
}
