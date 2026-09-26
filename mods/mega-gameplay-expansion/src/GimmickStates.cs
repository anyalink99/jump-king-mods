using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JumpKing.API;
using JumpKing.Player;

namespace MegaGameplayExpansion
{
    internal sealed class StateSlot
    {
        internal string Id, Label, Owner;
        internal object Target;
        internal FieldInfo Field;
        internal PropertyInfo Property;
        internal Type ValueType;
        internal bool ScreenSet, ScreenDictionary;
        internal MethodInfo Getter;
        internal object[] ScreenTargets;
        internal bool ContactOwned;
        internal string ReadOnlyReason { get { return ContactOwned || Target is IBlockBehaviour
            || GimmickStates.ContactType(Field != null ? Field.DeclaringType : Property.DeclaringType)
            ? "Read-only collision handler state. Forcing it can invent contact without a block. Apply a material from this provider instead." : null; } }
        internal void RequireWritable() { if (ReadOnlyReason != null) throw new InvalidOperationException(ReadOnlyReason); }
        internal string Contract { get { return (Field != null ? Field.Module : Property.Module).ModuleVersionId.ToString("D") + ":" + ValueType.FullName; } }
        internal object Get() { return Field != null ? Field.GetValue(Target) : Property.GetValue(Target, null); }
        internal void Set(object value) { if (Field != null) Field.SetValue(Target, value); else Property.SetValue(Target, value, null); }
        internal object Parse(string text)
        {
            if (ValueType == typeof(bool)) return string.IsNullOrEmpty(text) || bool.Parse(text);
            if (string.IsNullOrEmpty(text)) return Enum.GetValues(ValueType).GetValue(0);
            object parsed = Enum.Parse(ValueType, text); if (!Enum.IsDefined(ValueType, parsed)) throw new ArgumentException("Unknown enum value");
            return parsed;
        }
        internal string[] Choices { get { return ValueType == typeof(bool) ? new[] { "True", "False" } : Enum.GetNames(ValueType); } }
    }
    internal static class GimmickStates
    {
        internal static bool ContactType(Type type) { return typeof(IBlockBehaviour).IsAssignableFrom(type); }
        internal static Type[] PrepareSelected(string[] ids)
        {
            if (ids.Length == 0) return new Type[0];
            var result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types; try { types = assembly.GetTypes(); } catch { continue; }
                foreach (var type in types)
                {
                    if (!Foreign(type) || type.ContainsGenericParameters) continue;
                    string statics = "state:static:" + Gimmicks.TypeId(type) + ".", instances = "state:instance:" + Gimmicks.TypeId(type) + ":";
                    foreach (string id in ids)
                    {
                        string path;
                        if (id.StartsWith(statics, StringComparison.Ordinal)) path = id.Substring(statics.Length);
                        else if (id.StartsWith(instances, StringComparison.Ordinal))
                        { path = id.Substring(instances.Length); int dot = path.IndexOf('.'); if (dot < 0) continue; path = path.Substring(dot + 1); }
                        else continue;
                        if (!result.Contains(type)) result.Add(type);
                        Type current = type; var parts = path.Split('.');
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (ContactType(current)) break;
                            var field = current.GetField(parts[i], Gimmicks.Members);
                            if (i + 1 < parts.Length) { if (field == null) break; current = field.FieldType; continue; }
                            string name = parts[i].StartsWith("<", StringComparison.Ordinal) ? parts[i].Substring(1, parts[i].IndexOf('>') - 1) : parts[i];
                            var property = current.GetProperty(name, Gimmicks.Members);
                            if (property != null && Supported(property.PropertyType) && property.GetGetMethod(true) != null)
                                GimmickGetters.Prepare(property.GetGetMethod(true));
                        }
                    }
                }
            }
            return result.ToArray();
        }
        private static IEnumerable<Type> CatalogueTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types; try { types = assembly.GetTypes(); } catch { continue; }
                if (!types.Any(t => typeof(IBlockFactory).IsAssignableFrom(t) || typeof(IBodyCompBehaviour).IsAssignableFrom(t)
                    || t.GetCustomAttributesData().Any(a => a.AttributeType.Name == "JumpKingModAttribute"))) continue;
                foreach (var type in types) yield return type;
            }
        }
        private static bool Wanted(HashSet<string> selected, string path)
        { return selected == null || selected.Contains("state:" + path) || selected.Any(id => id.StartsWith("state:" + path + ".", StringComparison.Ordinal)); }
        internal static void Discover(PlayerEntity player, HashSet<string> selected = null, Type[] preparedTypes = null)
        {
            foreach (string id in Gimmicks.Entries.Where(p => p.Value.Slot != null && (selected == null || selected.Contains(p.Key))).Select(p => p.Key).ToArray()) Gimmicks.Entries.Remove(id);
            var roots = new List<object>(player.m_body.GetBehaviourList().Cast<object>());
            var screens = (JumpKing.Level.LevelScreen[])GimmickBlocks.Screens.GetValue(null);
            if (screens != null && screens.Length > 0)
                foreach (var field in typeof(JumpKing.Level.LevelScreen).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (!field.IsInitOnly && Supported(field.FieldType) && Wanted(selected, "screen:" + field.Name))
                        Add(new StateSlot { Id = "state:screen:" + field.Name, Label = "Level screen / " + Name(field.Name), Owner = "Jump King",
                            Target = screens[0], ScreenTargets = screens.Cast<object>().ToArray(), Field = field, ValueType = field.FieldType });
            roots.AddRange(((IEnumerable)typeof(EntityComponent.Entity).GetField("m_components", Gimmicks.Members).GetValue(player)).Cast<object>());
            var lookup = typeof(BodyComp).GetField("m_blockBehaviourLookup", Gimmicks.Members).GetValue(player.m_body) as IDictionary;
            if (lookup != null) foreach (object value in lookup.Values) if (!roots.Contains(value)) roots.Add(value);
            var ordinals = new Dictionary<Type, int>();
            foreach (object root in roots)
            {
                Type type = root.GetType(); if (!Foreign(type)) continue;
                int index; ordinals.TryGetValue(type, out index); ordinals[type] = index + 1;
                Visit(root, type, "instance:" + Gimmicks.TypeId(type) + ":" + index,
                    Gimmicks.Human(type.Name), type.Assembly.GetName().Name, 0, new HashSet<object>(), selected);
            }
            // Only loaded assemblies. Reading a type with a static constructor can
            // initialize a whole foreign subsystem, so do not probe those types.
            var initialized = new HashSet<Type>(roots.Select(r => r.GetType()).Concat(GimmickBlocks.ReadFactories().Select(f => f.GetType())));
            foreach (var type in preparedTypes ?? CatalogueTypes())
            {
                    if (!Foreign(type) || (type.TypeInitializer != null && !initialized.Contains(type)) || type.ContainsGenericParameters) continue;
                    string prefix = "static:" + Gimmicks.TypeId(type);
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsLiteral) continue;
                        try { AddField(null, field, prefix + "." + field.Name, Gimmicks.Human(type.Name) + " / " + Name(field.Name), type.Assembly.GetName().Name, 0, new HashSet<object>(), selected); }
                        catch { }
                    }
                    // Auto-properties are represented once by their backing field.
                    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (!Supported(property.PropertyType) || property.GetIndexParameters().Length != 0 || property.GetGetMethod() == null
                            || property.GetSetMethod() == null || type.GetField("<" + property.Name + ">k__BackingField", Gimmicks.Members) != null) continue;
                        if (!Wanted(selected, prefix + "." + property.Name)) continue;
                        try
                        {
                            property.GetValue(null, null);
                            Add(new StateSlot { Id = "state:" + prefix + "." + property.Name, Label = Gimmicks.Human(type.Name) + " / " + Gimmicks.Human(property.Name),
                                Owner = type.Assembly.GetName().Name, Property = property, Getter = property.GetGetMethod(), ValueType = property.PropertyType });
                        }
                        catch { }
                    }
            }
            Gimmicks.Generation++;
        }
        private static bool Foreign(Type type)
        {
            return type.Assembly != typeof(BodyComp).Assembly && type.Assembly != typeof(Gimmicks).Assembly
                && type.Assembly != typeof(JKRuntime.RuntimeApi).Assembly && !type.Assembly.GetName().Name.StartsWith("System", StringComparison.Ordinal)
                && type.Assembly != typeof(object).Assembly && type.Assembly != typeof(Microsoft.Xna.Framework.Vector2).Assembly;
        }
        private static bool Supported(Type type) { return type == typeof(bool) || type.IsEnum; }
        private static string Name(string field)
        { return Gimmicks.Human(field.StartsWith("<", StringComparison.Ordinal) ? field.Substring(1, field.IndexOf('>') - 1) : field.TrimStart('_')); }
        private static void Visit(object target, Type type, string path, string label, string owner, int depth, HashSet<object> visited, HashSet<string> selected, bool contactOwned = false)
        {
            if (!Wanted(selected, path) || target == null || depth > 2 || !Foreign(type) || !visited.Add(target)) return;
            contactOwned = contactOwned || target is IBlockBehaviour;
            for (Type current = type; current != null && Foreign(current); current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    try { AddField(target, field, path + "." + field.Name, label + " / " + Name(field.Name), owner, depth, visited, selected, contactOwned); }
                    catch { }
        }
        private static void AddField(object target, FieldInfo field, string path, string label, string owner, int depth, HashSet<object> visited, HashSet<string> selected, bool contactOwned = false)
        {
            if (!Wanted(selected, path)) return;
            contactOwned = contactOwned || ContactType(field.DeclaringType);
            if (Supported(field.FieldType) && !field.IsInitOnly)
            {
                PropertyInfo property = field.Name.StartsWith("<", StringComparison.Ordinal)
                    ? field.DeclaringType.GetProperty(field.Name.Substring(1, field.Name.IndexOf('>') - 1), Gimmicks.Members) : null;
                Add(new StateSlot { Id = "state:" + path, Label = label, Owner = owner, Target = target, Field = field, ContactOwned = contactOwned,
                    Getter = property == null ? null : property.GetGetMethod(true), ValueType = field.FieldType }); return;
            }
            object value;
            if (field.FieldType == typeof(HashSet<int>))
            {
                value = field.GetValue(target); if (value == null) return;
                Add(new StateSlot { Id = "state:" + path, Label = label + " (index set)", Owner = owner, Target = target, Field = field, ContactOwned = contactOwned, ScreenSet = true, ValueType = typeof(bool) }); return;
            }
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type[] args = field.FieldType.GetGenericArguments();
                if (args[0] == typeof(int) && Supported(args[1]) && field.GetValue(target) != null)
                { Add(new StateSlot { Id = "state:" + path, Label = label + " (index table)", Owner = owner, Target = target, Field = field, ContactOwned = contactOwned, ScreenDictionary = true, ValueType = args[1] }); return; }
            }
            if (depth < 2 && field.FieldType.IsClass && Foreign(field.FieldType))
            { value = field.GetValue(target); if (value != null) Visit(value, value.GetType(), path, label, owner, depth + 1, visited, selected, contactOwned); }
        }
        private static void Add(StateSlot slot)
        {
            if (Gimmicks.Entries.Count > 12000) return;
            Gimmicks.Add(new GimmickEntry { Id = slot.Id, Label = slot.Label, Owner = slot.Owner, Kind = "State", Slot = slot,
                Detail = slot.ReadOnlyReason ?? (slot.ScreenSet || slot.ScreenDictionary
                    ? "Advanced integer-indexed collection. Scope edits indices screen-1. Verify that this provider uses SCREEN indices before enabling."
                    : slot.ScreenTargets != null ? "Native screen state. Applied to every screen in the selected scope; released values return to their authored state."
                    : slot.Getter != null ? "Advanced state; intercepted property reads use the chosen value. Already inlined reads and direct field access cannot be intercepted."
                    : "Advanced raw field. Reapplied before each player tick; downstream code may overwrite it. No semantic enable API.") });
        }
    }
    internal sealed class StateOverride : IDisposable
    {
        private readonly StateSlot slot;
        private readonly object forced, original, collection;
        private readonly Dictionary<int, object> previous = new Dictionary<int, object>();
        private readonly HashSet<int> absent = new HashSet<int>();
        private readonly int[] indices;
        private bool disposed;
        private IDisposable getterLease;
        internal StateOverride(StateSlot value, GimmickRule rule, int screens)
        {
            value.RequireWritable();
            slot = value; forced = slot.Parse(rule.Value); original = slot.Get();
            indices = Enumerable.Range(0, screens).Where(i => GimmickSession.InRange(rule, i)).ToArray();
            if (slot.ScreenSet || slot.ScreenDictionary)
            {
                collection = original;
                foreach (int i in indices)
                {
                    if (slot.ScreenSet) { previous[i] = ((HashSet<int>)collection).Contains(i); }
                    else { var table = (IDictionary)collection; if (table.Contains(i)) previous[i] = table[i]; else absent.Add(i); }
                }
            }
            if (slot.ScreenTargets != null)
                foreach (int i in indices) previous[i] = slot.Field.GetValue(slot.ScreenTargets[i]);
            if (slot.Getter != null) getterLease = GimmickGetters.Hold(slot.Getter, slot.Target, forced);
        }
        internal void Tick()
        {
            if (disposed) return;
            if (collection != null && !ReferenceEquals(slot.Get(), collection)) throw new InvalidOperationException("State collection was replaced by its owner");
            if (slot.ScreenTargets != null) { foreach (int i in indices) slot.Field.SetValue(slot.ScreenTargets[i], forced); }
            else if (slot.ScreenSet) { foreach (int i in indices) { if ((bool)forced) ((HashSet<int>)collection).Add(i); else ((HashSet<int>)collection).Remove(i); } }
            else if (slot.ScreenDictionary) { foreach (int i in indices) ((IDictionary)collection)[i] = forced; }
            else slot.Set(forced);
        }
        public void Dispose()
        {
            if (disposed) return;
            if (getterLease != null) { getterLease.Dispose(); getterLease = null; }
            if (slot.ScreenTargets != null)
            {
                foreach (var item in previous) if (Equals(slot.Field.GetValue(slot.ScreenTargets[item.Key]), forced)) slot.Field.SetValue(slot.ScreenTargets[item.Key], item.Value);
            }
            else if (slot.ScreenSet)
            {
                if (ReferenceEquals(slot.Get(), collection)) foreach (var item in previous)
                {
                    var set = (HashSet<int>)collection;
                    if (set.Contains(item.Key) == (bool)forced) { if ((bool)item.Value) set.Add(item.Key); else set.Remove(item.Key); }
                }
            }
            else if (slot.ScreenDictionary)
            {
                if (ReferenceEquals(slot.Get(), collection))
                {
                    var table = (IDictionary)collection;
                    foreach (int key in previous.Keys.Concat(absent)) if (Equals(table[key], forced))
                    { if (absent.Contains(key)) table.Remove(key); else table[key] = previous[key]; }
                }
            }
            else if (Equals(slot.Get(), forced)) slot.Set(original);
            disposed = true;
        }
    }
}
