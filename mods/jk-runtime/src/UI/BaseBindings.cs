using System;
using System.Collections.Generic;
using JumpKing.Controller;
using System.Reflection;

namespace JKRuntime.UI
{
    internal static class BaseBindings
    {
        private static readonly JKpadButtons[] Buttons =
        {
            JKpadButtons.Up,
            JKpadButtons.Down,
            JKpadButtons.Left,
            JKpadButtons.Right,
            JKpadButtons.Jump,
            JKpadButtons.Confirm,
            JKpadButtons.Cancel,
            JKpadButtons.Pause,
            JKpadButtons.Boots,
            JKpadButtons.Snake,
            JKpadButtons.Restart
        };
        private static readonly HashSet<PadInstance> ConfiguredPads = new HashSet<PadInstance>();
        private static readonly MethodInfo SaveBindingMethod = FindSaveBindingMethod();
        private static bool registered;

        internal static void Register()
        {
            if (SaveBindingMethod == null)
                throw new InvalidOperationException(
                    "Jump King controller-binding persistence is unavailable");
            if (!registered)
            {
                registered = true;
                RegisterBase(JKpadButtons.Up, "Menu up");
                RegisterBase(JKpadButtons.Down, "Menu down");
                RegisterBase(JKpadButtons.Left, "Move left");
                RegisterBase(JKpadButtons.Right, "Move right");
                RegisterBase(JKpadButtons.Jump, "Jump");
                RegisterBase(JKpadButtons.Confirm, "Confirm");
                RegisterBase(JKpadButtons.Cancel, "Cancel");
                RegisterBase(JKpadButtons.Pause, "Pause");
                RegisterBase(JKpadButtons.Boots, "Toggle boots");
                RegisterBase(JKpadButtons.Snake, "Toggle ring");
                RegisterBase(JKpadButtons.Restart, "Restart");
                UIApi.RegisterBinding(new UiBindingDefinition(
                    "jk.runtime.interact",
                    "JK Runtime",
                    "Interact",
                    SettingsStore.GetInteractChords,
                    SettingsStore.SetInteractChords,
                    SettingsStore.ResetInteractChords));
            }
            RefreshDevices();
        }

        internal static void RefreshDevices()
        {
            foreach (PadInstance pad in ControllerManager.instance.GetConnectedPads())
            {
                if (ConfiguredPads.Contains(pad)) continue;
                foreach (JKpadButtons button in Buttons) ApplyRuntime(pad, button, GetChords(pad, button));
                ConfiguredPads.Add(pad);
            }
        }

        internal static void Disable()
        {
            foreach (PadInstance pad in ConfiguredPads)
            {
                PadBinding binding = pad.GetBind().CreateCopy();
                foreach (JKpadButtons button in Buttons)
                {
                    binding.SetButtonBind(button, ChordVirtualizer.NativeAlternatives(pad.GetPad(), GetChords(pad, button)));
                    ChordVirtualizer.Remove(pad, OwnerId(button));
                }
                pad.SetBind(binding);
            }
            ConfiguredPads.Clear();
        }

        private static void RegisterBase(JKpadButtons button, string label)
        {
            JKpadButtons captured = button;
            UIApi.RegisterBinding(new UiBindingDefinition(
                "jump-king." + button.ToString().ToLowerInvariant(),
                "Jump King",
                label,
                delegate { return GetChords(GetMain(), captured); },
                delegate(UiChord[] value) { SetChords(GetMain(), captured, value); },
                delegate { SetChords(GetMain(), captured, GetDefaultChords(GetMain(), captured)); }));
        }

        private static UiChord[] GetChords(PadInstance pad, JKpadButtons button)
        {
            UiChord[] defaults = UiChord.FromAlternatives(pad.GetBind().GetButtonBind(button));
            return SettingsStore.GetBindingChords(StorageId(pad, button), defaults);
        }

        private static UiChord[] GetDefaultChords(PadInstance pad, JKpadButtons button)
        {
            return UiChord.FromAlternatives(pad.GetPad().GetDefaultBind().GetButtonBind(button));
        }

        private static void SetChords(PadInstance pad, JKpadButtons button, UiChord[] chords)
        {
            SettingsStore.SetBindingChords(StorageId(pad, button), chords);
            UiChord[] saved = SettingsStore.GetBindingChords(StorageId(pad, button), new UiChord[0]);
            PadBinding safe = pad.GetBind().CreateCopy();
            foreach (JKpadButtons action in Buttons)
                safe.SetButtonBind(action, ChordVirtualizer.NativeAlternatives(pad.GetPad(), GetChords(pad, action)));
            SaveBinding(safe, pad.GetPad().GetSaveIdentifier());
            PadBinding runtime = pad.GetBind().CreateCopy();
            runtime.SetButtonBind(button, ChordVirtualizer.Apply(pad, OwnerId(button), saved));
            pad.SetBind(runtime);
            ConfiguredPads.Add(pad);
        }

        private static void ApplyRuntime(PadInstance pad, JKpadButtons button, UiChord[] chords)
        {
            PadBinding binding = pad.GetBind().CreateCopy();
            binding.SetButtonBind(button, ChordVirtualizer.Apply(pad, OwnerId(button), chords));
            pad.SetBind(binding);
        }

        private static string StorageId(PadInstance pad, JKpadButtons button)
        {
            return "jump-king." + pad.GetPad().GetSaveIdentifier() + "." + button.ToString().ToLowerInvariant();
        }

        private static string OwnerId(JKpadButtons button)
        {
            return "jump-king." + button.ToString().ToLowerInvariant();
        }

        private static void SaveBinding(PadBinding binding, string identifier)
        {
            SaveBindingMethod.Invoke(null, new object[] { binding, identifier });
        }

        private static MethodInfo FindSaveBindingMethod()
        {
            Type type = typeof(JumpKing.Game1).Assembly.GetType("JumpKing.SaveThread.SaveLube");
            return type == null ? null : type.GetMethod(
                "SaveControllerBinding",
                BindingFlags.Public | BindingFlags.Static);
        }

        private static PadInstance GetMain()
        {
            PadInstance main = ControllerManager.instance.GetMain();
            if (!main.IsValid || !main.IsConnected) throw new InvalidOperationException("No active input device");
            return main;
        }
    }
}
