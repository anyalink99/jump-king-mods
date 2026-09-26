using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Controller;
using JumpKing.Mods;

namespace JKRuntime.UI
{
    internal static class AutomaticBindings
    {
        private static readonly Dictionary<string, LegacyBindingAdapter> Adapters =
            new Dictionary<string, LegacyBindingAdapter>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> InspectedContainers =
            new HashSet<string>(StringComparer.Ordinal);
        private sealed class ContainerCandidate
        {
            internal string Key;
            internal Type Holder, Container;
            internal Func<object> Read;
        }
        private static readonly Dictionary<Assembly, ContainerCandidate[]> Candidates =
            new Dictionary<Assembly, ContainerCandidate[]>();
        private static readonly HashSet<Assembly> MetadataWarnings = new HashSet<Assembly>();
        private static bool exitHooked;
        private static PadInstance activePad;

        internal static void Refresh()
        {
            // Native ModLoader sees SDK shells. Discover their implementations
            // before scanning settings, including when Controls+ opens first.
            PackageHost.Discover();
            if (!exitHooked)
            {
                exitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += delegate { Disable(); };
            }
            foreach (ModAssembly mod in ModLoader.Instance.LoadedMods)
            {
                if (mod.Assembly == typeof(UIApi).Assembly) continue;
                Discover(mod);
            }
        }

        internal static void Disable()
        {
            PadInstance pad = activePad;
            // Discovery alone does not install virtual bindings. A title-only
            // session has no device state to restore or settings to rewrite.
            if (pad == null) return;
            foreach (LegacyBindingAdapter adapter in Adapters.Values) adapter.RestoreSafe(pad);
            activePad = null;
        }

        internal static void RefreshDevice()
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected || object.ReferenceEquals(main, activePad)) return;
            if (activePad != null)
                foreach (LegacyBindingAdapter adapter in Adapters.Values)
                    ChordVirtualizer.Remove(activePad, adapter.Id);
            activePad = main;
            foreach (LegacyBindingAdapter adapter in Adapters.Values) adapter.ApplyRuntime(main);
        }

        private static void Discover(ModAssembly mod)
        {
            string group = FriendlyModName(mod.ModName);
            foreach (var candidate in GetCandidates(PackageHost.BindingAssembly(mod.Assembly)))
            {
                if (InspectedContainers.Contains(candidate.Key)) continue;
                if (TryRegisterContainer(mod, group, candidate.Holder, candidate.Container, candidate.Read))
                    InspectedContainers.Add(candidate.Key);
            }
        }

        // Cache metadata, not settings objects. A null/throwing settings getter
        // is still retried; adapters retain live getters for replaced objects.
        private static ContainerCandidate[] GetCandidates(Assembly assembly)
        {
            ContainerCandidate[] cached;
            if (Candidates.TryGetValue(assembly, out cached)) return cached;
            var result = new List<ContainerCandidate>();
            bool complete;
            Type[] holders;
            try { holders = SafeTypes(assembly, out complete); }
            catch (Exception error)
            {
                MetadataFailure(assembly, error);
                return new ContainerCandidate[0];
            }
            foreach (Type holder in holders)
            {
                BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo[] properties;
                FieldInfo[] fields;
                try { properties = holder.GetProperties(flags); }
                catch (Exception error) { MetadataFailure(assembly, error); complete = false; properties = new PropertyInfo[0]; }
                try { fields = holder.GetFields(flags); }
                catch (Exception error) { MetadataFailure(assembly, error); complete = false; fields = new FieldInfo[0]; }
                foreach (PropertyInfo property in properties)
                {
                    try
                    {
                        if (property.GetIndexParameters().Length != 0 || property.GetGetMethod(true) == null) continue;
                        AddCandidate(result, holder, property.PropertyType, "|P|" + property.Name,
                            delegate { return property.GetValue(null, null); });
                    }
                    catch (Exception error) { MetadataFailure(assembly, error); complete = false; }
                }
                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        AddCandidate(result, holder, field.FieldType, "|F|" + field.Name,
                            delegate { return field.GetValue(null); });
                    }
                    catch (Exception error) { MetadataFailure(assembly, error); complete = false; }
                }
            }
            cached = result.ToArray();
            // Retry partially loadable assemblies, and do not freeze an emitted
            // assembly whose types/resources may still be under construction.
            if (complete && !assembly.IsDynamic) Candidates.Add(assembly, cached);
            return cached;
        }
        private static void MetadataFailure(Assembly assembly, Exception error)
        {
            if (!(error is TypeLoadException || error is ReflectionTypeLoadException || error is System.IO.FileNotFoundException
                || error is System.IO.FileLoadException || error is BadImageFormatException || error is NotSupportedException
                || error is MemberAccessException || error is AmbiguousMatchException))
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            if (MetadataWarnings.Add(assembly))
                Console.WriteLine("[JK Runtime UI] Incomplete bindings metadata for " + assembly.FullName + ": " + error.Message + "; healthy members remain available and incomplete scans retry.");
        }
        private static void AddCandidate(List<ContainerCandidate> result, Type holder, Type container, string suffix, Func<object> read)
        {
            var property = container.GetProperty("KeyBindings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.GetGetMethod(true) == null) return;
            result.Add(new ContainerCandidate { Key = holder.AssemblyQualifiedName + suffix, Holder = holder, Container = container, Read = read });
        }

        private static bool TryRegisterContainer(ModAssembly mod, string group, Type holder, Type containerType, Func<object> getter)
        {
            PropertyInfo keyBindings = containerType.GetProperty(
                "KeyBindings",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (keyBindings == null || keyBindings.GetGetMethod(true) == null) return false;
            object container;
            IDictionary dictionary;
            try
            {
                container = getter();
                dictionary = container == null ? null : keyBindings.GetValue(container, null) as IDictionary;
            }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Could not read " + group + " bindings: " + error.Message);
                return false;
            }
            if (dictionary == null) return false;

            foreach (object key in new ArrayList(dictionary.Keys))
            {
                int[] value = dictionary[key] as int[];
                if (value == null) continue;
                object capturedKey = key;
                int[] capturedDefault = (int[])value.Clone();
                string label = Humanize(key.ToString());
                string id = "auto." + mod.Assembly.GetName().Name + "." + key.ToString();
                LegacyBindingAdapter adapter;
                if (Adapters.TryGetValue(id, out adapter))
                {
                    ApplyToCurrentPadIfAvailable(adapter);
                    continue;
                }
                adapter = new LegacyBindingAdapter(
                    id,
                    holder,
                    keyBindings,
                    getter,
                    capturedKey,
                    capturedDefault);
                Adapters[id] = adapter;
                UIApi.RegisterBinding(new UiBindingDefinition(
                    id,
                    group,
                    label,
                    adapter.GetChords,
                    adapter.SetChords,
                    adapter.Reset));
                ApplyToCurrentPadIfAvailable(adapter);
            }
            return true;
        }

        private static void Persist(Type holder, object container)
        {
            MethodInfo force = container.GetType().GetMethod(
                "ForceUpdate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (force != null)
            {
                force.Invoke(container, null);
                return;
            }
            MethodInfo save = holder.GetMethod(
                "Save",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (save != null) save.Invoke(null, null);
            else Console.WriteLine("[JK Runtime UI] " + holder.FullName
                + " exposes bindings but no ForceUpdate or Save method; changes are session-only.");
        }

        private static Type[] SafeTypes(Assembly assembly, out bool complete)
        {
            complete = true;
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error)
            {
                complete = false;
                Console.WriteLine("[JK Runtime UI] Could not inspect " + assembly.GetName().Name + ": " + error.Message);
                List<Type> types = new List<Type>();
                foreach (Type type in error.Types) if (type != null) types.Add(type);
                return types.ToArray();
            }
        }

        internal static string FriendlyModName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Mods";
            int dot = value.LastIndexOf('.');
            return dot >= 0 && dot + 1 < value.Length ? value.Substring(dot + 1) : value;
        }

        private static string Humanize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Binding";
            string text = value.Replace('_', ' ').Replace('-', ' ');
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]) && text[i - 1] != ' ' && !char.IsUpper(text[i - 1]))
                {
                    text = text.Insert(i, " ");
                    i++;
                }
            }
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static PadInstance GetMain()
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected)
                throw new InvalidOperationException("No active input device");
            activePad = main;
            return main;
        }

        private static void ApplyToCurrentPadIfAvailable(
            LegacyBindingAdapter adapter)
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected) return;
            activePad = main;
            adapter.ApplyRuntime(main);
        }

        private sealed class LegacyBindingAdapter
        {
            private readonly string id;
            private readonly Type holder;
            private readonly PropertyInfo bindingsProperty;
            private readonly Func<object> getContainer;
            private readonly object key;
            private readonly UiChord[] defaults;
            internal string Id { get { return id; } }

            internal LegacyBindingAdapter(
                string bindingId,
                Type bindingHolder,
                PropertyInfo property,
                Func<object> container,
                object bindingKey,
                int[] defaultBindings)
            {
                id = bindingId;
                holder = bindingHolder;
                bindingsProperty = property;
                getContainer = container;
                key = bindingKey;
                defaults = UiChord.FromAlternatives(defaultBindings);
            }

            internal UiChord[] GetChords()
            {
                return SettingsStore.GetBindingChords(id, ReadPhysical());
            }

            internal void SetChords(UiChord[] chords)
            {
                SettingsStore.SetBindingChords(id, chords);
                UiChord[] saved = SettingsStore.GetBindingChords(id, new UiChord[0]);
                object container;
                IDictionary bindings;
                if (!TryGetBindings(out container, out bindings)) return;
                bindings[key] = ChordVirtualizer.NativeAlternatives(AutomaticBindings.GetMain().GetPad(), saved);
                Persist(holder, container);
                bindings[key] = ChordVirtualizer.Apply(AutomaticBindings.GetMain(), id, saved);
            }

            internal void Reset()
            {
                SetChords(defaults);
            }

            internal void ApplyRuntime(PadInstance pad)
            {
                object container;
                IDictionary bindings;
                if (!TryGetBindings(out container, out bindings)) return;
                bindings[key] = ChordVirtualizer.Apply(pad, id, GetChords());
            }

            internal void RestoreSafe(PadInstance pad)
            {
                try
                {
                    object container;
                    IDictionary bindings;
                    if (!TryGetBindings(out container, out bindings)) return;
                    bindings[key] = ChordVirtualizer.NativeAlternatives(pad.GetPad(), GetChords());
                    Persist(holder, container);
                    ChordVirtualizer.Remove(pad, id);
                }
                catch (Exception error)
                {
                    Console.WriteLine("[JK Runtime UI] Could not restore " + id + ": " + error.Message);
                }
            }

            private UiChord[] ReadPhysical()
            {
                object container;
                IDictionary bindings;
                if (!TryGetBindings(out container, out bindings)) return defaults;
                int[] values = bindings[key] as int[];
                UiChord[] result = UiChord.FromAlternatives(values);
                return result.Length == 0 ? defaults : result;
            }

            private bool TryGetBindings(out object container, out IDictionary bindings)
            {
                container = getContainer();
                bindings = container == null ? null : bindingsProperty.GetValue(container, null) as IDictionary;
                return bindings != null;
            }

        }
    }
}
