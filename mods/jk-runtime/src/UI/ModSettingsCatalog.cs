using System;
using System.Collections.Generic;
using System.Reflection;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;

namespace JKRuntime.UI
{
    public sealed class UiModSettingInfo
    {
        public string Id { get; private set; }
        public string ModName { get; private set; }
        public string Label { get; private set; }
        public bool IsToggle { get; private set; }
        public bool IsPinned { get { return SettingsStore.IsSettingPinned(Id); } }
        public bool IsBindable { get { return IsToggle && SettingsStore.IsSettingBindable(Id); } }

        internal UiModSettingInfo(string id, string modName, string label, bool isToggle)
        {
            Id = id;
            ModName = modName;
            Label = label;
            IsToggle = isToggle;
        }
    }

    internal sealed class ModSettingDescriptor
    {
        internal readonly UiModSettingInfo Info;
        internal readonly MethodInfo PauseMethod;

        internal ModSettingDescriptor(UiModSettingInfo info, MethodInfo pauseMethod)
        {
            Info = info;
            PauseMethod = pauseMethod;
        }
    }

    internal static class ModSettingsCatalog
    {
        private const string ToggleBindingPrefix = "jk.runtime.mod-toggle.";
        private static readonly Dictionary<string, ModSettingDescriptor> Settings =
            new Dictionary<string, ModSettingDescriptor>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> Order = new List<string>();
        private static readonly Dictionary<string, string> Labels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static WeakReference pauseFactory;
        private static GuiFormat pauseFormat;

        internal static void Refresh(object factory, GuiFormat format, bool isPause)
        {
            if (isPause && factory != null)
            {
                pauseFactory = new WeakReference(factory);
                pauseFormat = format;
            }

            foreach (ModAssembly mod in ModLoader.Instance.LoadedMods)
            {
                if (mod == null || mod.Assembly == typeof(ModSettingsCatalog).Assembly) continue;
                foreach (MethodInfo method in mod.PauseMenuItemSettingMethods)
                    Register(mod, JKRuntime.PackageHost.MenuImplementation(mod.Assembly, method));
            }
        }

        internal static IList<ModSettingDescriptor> GetDescriptors()
        {
            List<ModSettingDescriptor> result = new List<ModSettingDescriptor>();
            foreach (string id in Order)
            {
                ModSettingDescriptor descriptor;
                if (Settings.TryGetValue(id, out descriptor)) result.Add(descriptor);
            }
            result.Sort(CompareDescriptors);
            return result;
        }

        internal static ModSettingDescriptor Find(string id)
        {
            ModSettingDescriptor descriptor;
            return Settings.TryGetValue(id ?? string.Empty, out descriptor) ? descriptor : null;
        }

        internal static IList<UiModSettingInfo> GetInfos()
        {
            List<UiModSettingInfo> result = new List<UiModSettingInfo>();
            foreach (ModSettingDescriptor descriptor in GetDescriptors()) result.Add(descriptor.Info);
            return result;
        }

        internal static void SetPinned(string id, bool pinned)
        {
            if (Find(id) == null) throw new ArgumentException("Unknown mod setting", "id");
            SettingsStore.SetSettingPinned(id, pinned);
            ModMenuIntegration.RefreshPins();
        }

        internal static void SetBindable(string id, bool bindable)
        {
            ModSettingDescriptor descriptor = Find(id);
            if (descriptor == null) throw new ArgumentException("Unknown mod setting", "id");
            if (!descriptor.Info.IsToggle)
                throw new InvalidOperationException("Only toggle settings can be bindable");
            SettingsStore.SetSettingBindable(id, bindable);
            UpdateToggleRegistration(descriptor.Info);
        }

        internal static void RegisterLabel(string id, string label)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Setting id is required", "id");
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Setting label is required", "label");
            Labels[id] = label.Trim();
            ModSettingDescriptor descriptor = Find(id);
            if (descriptor != null) ReplaceLabel(descriptor, Labels[id]);
        }

        internal static void UnregisterLabel(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !Labels.Remove(id)) return;
            ModSettingDescriptor descriptor = Find(id);
            if (descriptor != null)
                ReplaceLabel(
                    descriptor,
                    SettingLabel(descriptor.PauseMethod.ReturnType, descriptor.PauseMethod));
        }

        private static void Register(ModAssembly mod, MethodInfo method)
        {
            if (method == null || method.GetParameters().Length != 2) return;
            Type returnType = method.ReturnType;
            if (!typeof(BehaviorTree.IBTSimpleMenuItem).IsAssignableFrom(returnType)) return;
            string id = SettingId(mod, method);
            bool isToggle = typeof(IToggle).IsAssignableFrom(returnType);
            UiModSettingInfo info = new UiModSettingInfo(
                id,
                string.IsNullOrWhiteSpace(mod.ModName) ? mod.Assembly.GetName().Name : mod.ModName,
                Labels.ContainsKey(id) ? Labels[id] : SettingLabel(returnType, method),
                isToggle);
            if (!Settings.ContainsKey(id)) Order.Add(id);
            Settings[id] = new ModSettingDescriptor(info, method);
            if (isToggle) UpdateToggleRegistration(info);
        }

        private static void ReplaceLabel(ModSettingDescriptor descriptor, string label)
        {
            UiModSettingInfo current = descriptor.Info;
            UiModSettingInfo updated = new UiModSettingInfo(
                current.Id,
                current.ModName,
                label,
                current.IsToggle);
            Settings[current.Id] = new ModSettingDescriptor(updated, descriptor.PauseMethod);
            if (updated.IsToggle) UpdateToggleRegistration(updated);
        }

        private static void UpdateToggleRegistration(UiModSettingInfo info)
        {
            string bindingId = ToggleBindingPrefix + info.Id;
            if (!SettingsStore.IsSettingBindable(info.Id))
            {
                UIApi.UnregisterInputAction(bindingId);
                UIApi.UnregisterBinding(bindingId);
                return;
            }
            UIApi.RegisterBinding(new UiBindingDefinition(
                bindingId,
                info.ModName,
                "Toggle " + info.Label,
                delegate { return SettingsStore.GetBindingChords(bindingId, new UiChord[0]); },
                delegate(UiChord[] value) { SettingsStore.SetBindingChords(bindingId, value); },
                delegate { SettingsStore.SetBindingChords(bindingId, new UiChord[0]); }));
            UIApi.RegisterInputAction(UiInputActionDefinition.FromChords(
                bindingId,
                "Toggle " + info.Label,
                40,
                delegate { return SettingsStore.GetBindingChords(bindingId, new UiChord[0]); },
                delegate
                {
                    return !UIApi.IsOpen
                        && !UiInputRouter.IsGamePaused();
                },
                delegate { Toggle(info.Id); }));
        }

        private static void Toggle(string id)
        {
            ModSettingDescriptor descriptor = Find(id);
            object factory = pauseFactory == null ? null : pauseFactory.Target;
            if (descriptor == null || factory == null) return;
            try
            {
                IToggle option = descriptor.PauseMethod.Invoke(
                    null,
                    new object[] { factory, pauseFormat }) as IToggle;
                if (option == null || !CanChange(option)) return;
                option.OverrideToggle(!option.toggle);
                MethodInfo onToggle = option.GetType().GetMethod(
                    "OnToggle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (onToggle == null) throw new InvalidOperationException("Toggle callback is unavailable");
                onToggle.Invoke(option, null);
            }
            catch (Exception error)
            {
                Console.WriteLine("[JK Runtime UI] Could not toggle " + id + ": " + Unwrap(error).Message);
            }
        }

        private static bool CanChange(IToggle option)
        {
            MethodInfo method = option.GetType().GetMethod(
                "CanChange",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return method == null || (bool)method.Invoke(option, null);
        }

        private static string SettingId(ModAssembly mod, MethodInfo method)
        {
            return mod.Assembly.GetName().Name + "." + method.DeclaringType.FullName + "." + method.Name;
        }

        private static string SettingLabel(Type returnType, MethodInfo method)
        {
            string value = returnType.Name;
            string[] suffixes = { "EnabledOption", "VisibilityOption", "ToggleOption", "Option" };
            foreach (string suffix in suffixes)
            {
                if (!value.EndsWith(suffix, StringComparison.Ordinal)) continue;
                value = value.Substring(0, value.Length - suffix.Length);
                break;
            }
            if (string.IsNullOrWhiteSpace(value)) value = method.Name;
            return Humanize(value);
        }

        private static string Humanize(string value)
        {
            string result = string.Empty;
            foreach (char character in value ?? string.Empty)
            {
                if (result.Length > 0 && char.IsUpper(character) && result[result.Length - 1] != ' ')
                    result += " ";
                result += character;
            }
            return string.IsNullOrWhiteSpace(result) ? "Setting" : result;
        }

        private static int CompareDescriptors(ModSettingDescriptor left, ModSettingDescriptor right)
        {
            int group = string.Compare(left.Info.ModName, right.Info.ModName, StringComparison.OrdinalIgnoreCase);
            return group != 0
                ? group
                : string.Compare(left.Info.Label, right.Info.Label, StringComparison.OrdinalIgnoreCase);
        }

        private static Exception Unwrap(Exception error)
        {
            TargetInvocationException invocation = error as TargetInvocationException;
            return invocation != null && invocation.InnerException != null
                ? invocation.InnerException
                : error;
        }
    }
}
