using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;

namespace JKRuntime.Gameplay
{
    /// <summary>Owns one native entity component. Attach/dispose on the game thread outside that entity's component iteration.</summary>
    public sealed class ComponentAttachment : IDisposable
    {
        private static readonly FieldInfo Components = typeof(Entity).GetField("m_components", BindingFlags.Instance | BindingFlags.NonPublic);
        private Entity entity;
        private Component component;
        private readonly List<Component> original;
        public ComponentAttachment(Entity target, Component value)
        {
            RuntimeApi.Kernel.CheckThread();
            if (target == null || value == null) throw new ArgumentNullException(target == null ? "target" : "value");
            original = Components == null ? null : Components.GetValue(target) as List<Component>;
            if (original == null || original.Contains(value)) throw new InvalidOperationException("Native component ownership is unavailable or duplicated");
            entity = target; component = value;
            try { target.AddComponents(value); }
            catch { value.Enabled = false; original.Remove(value); entity = null; component = null; throw; }
        }
        public void Dispose()
        {
            RuntimeApi.Kernel.CheckThread();
            if (component == null) return;
            component.Enabled = false;
            var current = Components.GetValue(entity) as List<Component>;
            if (current == null) throw new InvalidOperationException("Native component collection disappeared");
            current.Remove(component);
            if (!ReferenceEquals(current, original)) original.Remove(component);
            component = null; entity = null;
        }
    }
}
