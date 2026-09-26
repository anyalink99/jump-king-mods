using System;
using System.Linq;
using System.Reflection;
using JumpKing.Level;

namespace JKRuntime.State
{
    // Explicitly selected mod-owned state containers, not an arbitrary walk of
    // game objects. Mutable reference graphs must supply a custom participant.
    public sealed class StateFields
    {
        private readonly object target;
        private readonly FieldInfo[] fields;
        public StateFields(object instance, params string[] names)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            target = instance;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            fields = names.Length == 0 ? instance.GetType().GetFields(flags).Where(f => !f.IsInitOnly).OrderBy(f => f.Name).ToArray()
                : names.Select(n => instance.GetType().GetField(n, flags) ?? Missing(n)).ToArray();
            foreach (var field in fields)
                if (field.IsInitOnly || (!field.FieldType.IsValueType && field.FieldType != typeof(string) && !typeof(IBlock).IsAssignableFrom(field.FieldType)))
                    throw new InvalidOperationException("State field needs an explicit copier: " + field.DeclaringType + "." + field.Name);
        }
        private static FieldInfo Missing(string name) { throw new MissingFieldException("Registered state field: " + name); }
        internal object[] Capture() { return fields.Select(f => f.GetValue(target)).ToArray(); }
        internal void Validate(object[] values)
        {
            if (values == null || values.Length != fields.Length) throw new InvalidOperationException("State field schema changed");
            for (int i = 0; i < fields.Length; i++)
                if (values[i] != null && !fields[i].FieldType.IsInstanceOfType(values[i])) throw new InvalidOperationException("State field type mismatch");
        }
        internal void Restore(object[] values) { for (int i = 0; i < fields.Length; i++) fields[i].SetValue(target, values[i]); }
    }
    public sealed class FieldStateParticipant : IStateParticipant
    {
        private readonly StateFields[] groups;
        private readonly Action restored;
        public string Id { get; private set; }
        public int Version { get; private set; }
        public FieldStateParticipant(string id, int version, Action onRestore, params StateFields[] fields)
        { Id = id; Version = version; groups = (StateFields[])fields.Clone(); restored = onRestore; }
        public object Capture() { return groups.Select(g => g.Capture()).ToArray(); }
        public void Validate(object snapshot)
        {
            var values = snapshot as object[][];
            if (values == null || values.Length != groups.Length) throw new InvalidOperationException("State group schema changed");
            for (int i = 0; i < groups.Length; i++) groups[i].Validate(values[i]);
        }
        public void Restore(object snapshot)
        {
            Validate(snapshot); var values = (object[][])snapshot;
            for (int i = 0; i < groups.Length; i++) groups[i].Restore(values[i]);
            if (restored != null) restored();
        }
    }
}
