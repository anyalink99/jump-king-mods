using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BehaviorTree;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Level;
using JumpKing.Player;

namespace SubframeCharge
{
    internal sealed partial class SubframeChargeState
    {
        private static readonly FieldInfo ControllerPadsField =
            typeof(ControllerManager).GetField(
                "m_pads",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type KeyboardPadType =
            typeof(ControllerManager).Assembly.GetType(
                "JumpKing.Controller.KeyboardPad");
        private static readonly Type XboxPadType =
            typeof(ControllerManager).Assembly.GetType(
                "JumpKing.Controller.XboxPad");
        private static readonly FieldInfo XboxPlayerIndexField =
            XboxPadType == null
                ? null
                : XboxPadType.GetField(
                    "m_index",
                    BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type SaveLubeType =
            typeof(ControllerManager).Assembly.GetType(
                "JumpKing.SaveThread.SaveLube");

        private int[][] configuredKeyboardBindings = new int[0][];
        private XInputJumpBinding[] configuredXInputBindings =
            new XInputJumpBinding[0];
        private DirectInputJumpBinding[] configuredDirectInputBindings = new DirectInputJumpBinding[0];
        private bool samplerConfigured;
        internal bool CanReplayTap(PadInstance pad)
        {
            if(observationOnly || sampler==null || !sampler.Enabled || pad==null) return false;
            int slot; Guid id;
            return IsKeyboardPad(pad) && sampler.CanMeasurePhysical(0)
                || TryGetXboxUserIndex(pad,out slot) && sampler.CanMeasurePhysical(slot+1)
                || DirectInputDevice.TryGetDeviceId(pad,out id) && sampler.CanMeasureDirectInput(id);
        }
        private readonly BindingSnapshot bindingSnapshot = new BindingSnapshot();
        private bool ConfigureSampler()
        {
            if (samplerConfigured && bindingSnapshot.Unchanged(Stopwatch.GetTimestamp())) return sampler.Enabled;
            string bindingSource;
            int[][] keyboardBindings = FindKeyboardJumpBindings(
                out bindingSource);
            XInputJumpBinding[] xinputBindings = FindXInputJumpBindings();
            DirectInputJumpBinding[] directBindings = FindDirectInputJumpBindings();
            bool requested = keyboardBindings.Length > 0
                || xinputBindings.Length > 0 || directBindings.Length > 0;
            if (!samplerConfigured
                || sampler.RequestedEnabled != requested
                || !SameAlternatives(
                    configuredKeyboardBindings,
                    keyboardBindings)
                || !SameXInputBindings(
                    configuredXInputBindings,
                    xinputBindings)
                || !SameDirectInputBindings(configuredDirectInputBindings, directBindings))
            {
                DiagnosticLog.Write(
                    "input configured source=" + bindingSource
                    + " sampler=" + requested
                    + " keys=" + FormatAlternatives(keyboardBindings)
                    + " xinput=" + FormatXInputBindings(xinputBindings)
                    + " directinput=" + FormatDirectInputBindings(directBindings));
                configuredKeyboardBindings = CloneAlternatives(
                    keyboardBindings);
                configuredXInputBindings = CloneXInputBindings(
                    xinputBindings);
                configuredDirectInputBindings = directBindings;
                sampler.Configure(
                    configuredKeyboardBindings,
                    configuredXInputBindings,
                    configuredDirectInputBindings,
                    Game1.instance == null ? IntPtr.Zero : Game1.instance.Window.Handle,
                    requested);
                samplerConfigured = true;
                if (nativeCharging)
                {
                    // A changed binding cannot supply the missing earlier edges.
                    InvalidateMeasurement("binding-changed-during-charge");
                    nativeChargeTimestamp = 0;
                }
                sampledPress = sampledRelease = false;
            }
            return requested && sampler.Enabled;
        }

        private static bool IsGameActive()
        {
            return Game1.instance != null && Game1.instance.IsActive;
        }

        private static int[][] FindKeyboardJumpBindings(out string source)
        {
            ControllerManager manager = ControllerManager.instance;
            if (manager != null)
            {
                PadInstance connected = FindKeyboardPad(
                    BindingSnapshot.Registered());
                int[] connectedKeys = GetJumpKeys(connected);
                if (connected != null)
                {
                    source = "connected keyboard";
                    return PhysicalBindings.ResolvePhysicalBinding(
                        connected,
                        connectedKeys);
                }

                List<PadInstance> allPads = ControllerPadsField == null
                    ? null
                    : ControllerPadsField.GetValue(manager)
                        as List<PadInstance>;
                PadInstance registered = FindKeyboardPad(allPads);
                int[] registeredKeys = GetJumpKeys(registered);
                if (registered != null)
                {
                    source = "registered keyboard";
                    return PhysicalBindings.ResolvePhysicalBinding(
                        registered,
                        registeredKeys);
                }
            }

            int[] profileKeys = LoadKeyboardProfileJumpKeys();
            source = profileKeys == null || profileKeys.Length == 0
                ? "none"
                : "keyboard profile";
            return ToSingleButtonAlternatives(profileKeys);
        }

        private static PadInstance FindKeyboardPad(List<PadInstance> pads)
        {
            if (pads == null)
            {
                return null;
            }
            for (int index = 0; index < pads.Count; index++)
            {
                PadInstance pad = pads[index];
                if (IsKeyboardPad(pad))
                {
                    return pad;
                }
            }
            return null;
        }

        private static bool IsKeyboardPad(PadInstance instance)
        {
            if (instance == null || instance.GetPad() == null)
            {
                return false;
            }
            object pad = instance.GetPad();
            if (KeyboardPadType != null
                && KeyboardPadType.IsInstanceOfType(pad))
            {
                return true;
            }
            MethodInfo identifier = pad.GetType().GetMethod(
                "GetSaveIdentifier",
                BindingFlags.Instance | BindingFlags.Public);
            return identifier != null
                && string.Equals(
                    identifier.Invoke(pad, null) as string,
                    "pc_keyboard_jump_king",
                    StringComparison.Ordinal);
        }

        private static int[] GetJumpKeys(PadInstance instance)
        {
            PadBinding binding = instance == null
                ? null
                : instance.GetBind();
            return instance == null || !instance.IsValid || binding == null || !binding.Enabled || binding.jump == null
                ? null
                : (int[])binding.jump.Clone();
        }

        private static XInputJumpBinding[] FindXInputJumpBindings()
        {
            ControllerManager manager = ControllerManager.instance;
            if (manager == null
                || XboxPadType == null
                || XboxPlayerIndexField == null)
            {
                return new XInputJumpBinding[0];
            }

            List<XInputJumpBinding> result =
                new List<XInputJumpBinding>();
            bool[] configuredSlots = new bool[4];
            List<PadInstance> connected = BindingSnapshot.Registered();
            for (int index = 0; index < connected.Count; index++)
            {
                PadInstance instance = connected[index];
                if (!instance.IsValid) continue;
                int userIndex;
                if (!TryGetXboxUserIndex(instance, out userIndex))
                {
                    continue;
                }

                PadBinding binding = instance.GetBind();
                int[] buttons = binding == null || !binding.Enabled
                    ? null
                    : binding.jump;
                int[][] alternatives =
                    PhysicalBindings.ResolvePhysicalBinding(
                        instance,
                        buttons);
                if (userIndex < 0
                    || userIndex >= configuredSlots.Length
                    || configuredSlots[userIndex]
                    || alternatives.Length == 0)
                {
                    continue;
                }

                result.Add(new XInputJumpBinding(
                    userIndex,
                    alternatives));
                configuredSlots[userIndex] = true;
            }
            return result.ToArray();
        }

        private static bool TryGetXboxUserIndex(
            PadInstance instance,
            out int userIndex)
        {
            userIndex = -1;
            object pad = instance == null ? null : instance.GetPad();
            if (pad == null)
            {
                return false;
            }
            if (XboxPadType != null
                && XboxPlayerIndexField != null
                && XboxPadType.IsInstanceOfType(pad))
            {
                userIndex = Convert.ToInt32(
                    XboxPlayerIndexField.GetValue(pad));
                return userIndex >= 0 && userIndex <= 3;
            }

            string identifier = instance.GetPad().GetSaveIdentifier();
            const string Prefix = "xbox_pad_jump_king";
            if (identifier == null
                || !identifier.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }
            string suffix = identifier.Substring(Prefix.Length);
            if (string.Equals(suffix, "One", StringComparison.Ordinal))
            {
                userIndex = 0;
            }
            else if (string.Equals(suffix, "Two", StringComparison.Ordinal))
            {
                userIndex = 1;
            }
            else if (string.Equals(suffix, "Three", StringComparison.Ordinal))
            {
                userIndex = 2;
            }
            else if (string.Equals(suffix, "Four", StringComparison.Ordinal))
            {
                userIndex = 3;
            }
            return userIndex >= 0;
        }

        private static int[] LoadKeyboardProfileJumpKeys()
        {
            if (KeyboardPadType == null)
            {
                return null;
            }
            object keyboard = Activator.CreateInstance(KeyboardPadType);
            MethodInfo getIdentifier = KeyboardPadType.GetMethod(
                "GetSaveIdentifier",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo getDefaultBind = KeyboardPadType.GetMethod(
                "GetDefaultBind",
                BindingFlags.Instance | BindingFlags.Public);
            string identifier = getIdentifier == null
                ? null
                : getIdentifier.Invoke(keyboard, null) as string;

            PadBinding binding = null;
            MethodInfo loadBinding = SaveLubeType == null
                ? null
                : SaveLubeType.GetMethod(
                    "LoadControllerBinding",
                    BindingFlags.Static | BindingFlags.Public);
            if (loadBinding != null && !string.IsNullOrEmpty(identifier))
            {
                binding = loadBinding.Invoke(
                    null,
                    new object[] { identifier }) as PadBinding;
            }
            if (binding == null && getDefaultBind != null)
            {
                binding = getDefaultBind.Invoke(keyboard, null)
                    as PadBinding;
            }
            return binding == null || !binding.Enabled || binding.jump == null
                ? null
                : (int[])binding.jump.Clone();
        }

        private bool HasUnsupportedJumpDown()
        {
            ControllerManager manager = ControllerManager.instance;
            if (manager == null)
            {
                return false;
            }
            foreach (PadInstance pad in BindingSnapshot.Registered())
            {
                if (!pad.IsValid) continue;
                int slot;
                Guid directId;
                if (pad.GetState().jump && !(IsKeyboardPad(pad) && sampler.CanMeasurePhysical(0))
                    && !(TryGetXboxUserIndex(pad, out slot) && sampler.CanMeasurePhysical(slot + 1))
                    && !(DirectInputDevice.TryGetDeviceId(pad, out directId)
                        && sampler.CanMeasureDirectInput(directId)))
                {
                    return true;
                }
            }
            return false;
        }

        private static DirectInputJumpBinding[] FindDirectInputJumpBindings()
        {
            List<DirectInputJumpBinding> result = new List<DirectInputJumpBinding>();
            Dictionary<Guid, List<int[]>> byDevice = new Dictionary<Guid, List<int[]>>();
            ControllerManager manager = ControllerManager.instance;
            if (manager == null) return result.ToArray();
            foreach (PadInstance pad in BindingSnapshot.Registered())
            {
                Guid id;
                if (!DirectInputDevice.TryGetDeviceId(pad, out id)) continue;
                int[][] alternatives = PhysicalBindings.ResolvePhysicalBinding(pad, GetJumpKeys(pad));
                if (alternatives.Length == 0) continue;
                List<int[]> combined;
                if (!byDevice.TryGetValue(id, out combined)) byDevice[id] = combined = new List<int[]>();
                combined.AddRange(alternatives);
            }
            List<Guid> ids = new List<Guid>(byDevice.Keys);
            ids.Sort();
            foreach (Guid id in ids) result.Add(new DirectInputJumpBinding(id, byDevice[id].ToArray()));
            return result.ToArray();
        }

        private static bool SameDirectInputBindings(DirectInputJumpBinding[] left, DirectInputJumpBinding[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i].DeviceId != right[i].DeviceId || !SameAlternatives(left[i].Alternatives, right[i].Alternatives)) return false;
            return true;
        }

        private static string FormatDirectInputBindings(DirectInputJumpBinding[] bindings)
        {
            List<string> items = new List<string>();
            foreach (DirectInputJumpBinding binding in bindings)
                items.Add(binding.DeviceId + "[" + FormatAlternatives(binding.Alternatives) + "]");
            return items.Count == 0 ? "none" : string.Join(";", items.ToArray());
        }

        private static bool SameKeys(int[] left, int[] right)
        {
            int leftLength = left == null ? 0 : left.Length;
            int rightLength = right == null ? 0 : right.Length;
            if (leftLength != rightLength)
            {
                return false;
            }
            for (int index = 0; index < leftLength; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameXInputBindings(
            XInputJumpBinding[] left,
            XInputJumpBinding[] right)
        {
            int leftLength = left == null ? 0 : left.Length;
            int rightLength = right == null ? 0 : right.Length;
            if (leftLength != rightLength)
            {
                return false;
            }
            for (int index = 0; index < leftLength; index++)
            {
                if (left[index].UserIndex != right[index].UserIndex
                    || !SameAlternatives(
                        left[index].Alternatives,
                        right[index].Alternatives))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameAlternatives(int[][] left, int[][] right)
        {
            int leftLength = left == null ? 0 : left.Length;
            int rightLength = right == null ? 0 : right.Length;
            if (leftLength != rightLength)
            {
                return false;
            }
            for (int index = 0; index < leftLength; index++)
            {
                if (!SameKeys(left[index], right[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static int[][] ToSingleButtonAlternatives(int[] buttons)
        {
            if (buttons == null || buttons.Length == 0)
            {
                return new int[0][];
            }
            int[][] result = new int[buttons.Length][];
            for (int index = 0; index < buttons.Length; index++)
            {
                result[index] = new[] { buttons[index] };
            }
            return result;
        }

        private static int[][] CloneAlternatives(int[][] alternatives)
        {
            if (alternatives == null || alternatives.Length == 0)
            {
                return new int[0][];
            }
            int[][] result = new int[alternatives.Length][];
            for (int index = 0; index < alternatives.Length; index++)
            {
                result[index] = alternatives[index] == null
                    ? new int[0]
                    : (int[])alternatives[index].Clone();
            }
            return result;
        }

        private static XInputJumpBinding[] CloneXInputBindings(
            XInputJumpBinding[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
            {
                return new XInputJumpBinding[0];
            }
            XInputJumpBinding[] result =
                new XInputJumpBinding[bindings.Length];
            for (int index = 0; index < bindings.Length; index++)
            {
                result[index] = new XInputJumpBinding(
                    bindings[index].UserIndex,
                    bindings[index].Alternatives);
            }
            return result;
        }

        private static string FormatKeys(int[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return "none";
            }
            string result = string.Empty;
            for (int index = 0; index < keys.Length; index++)
            {
                result += (index == 0 ? string.Empty : ",") + keys[index];
            }
            return result;
        }

        private static string FormatAlternatives(int[][] alternatives)
        {
            if (alternatives == null || alternatives.Length == 0)
            {
                return "none";
            }
            string result = string.Empty;
            for (int index = 0; index < alternatives.Length; index++)
            {
                result += index == 0 ? string.Empty : ",";
                result += FormatChord(alternatives[index]);
            }
            return result;
        }

        private static string FormatXInputBindings(
            XInputJumpBinding[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
            {
                return "none";
            }
            string result = string.Empty;
            for (int index = 0; index < bindings.Length; index++)
            {
                result += index == 0 ? string.Empty : ";";
                result += "slot" + bindings[index].UserIndex + "[";
                for (int alternativeIndex = 0;
                    alternativeIndex < bindings[index].Alternatives.Length;
                    alternativeIndex++)
                {
                    result += alternativeIndex == 0 ? string.Empty : ",";
                    result += FormatChord(
                        bindings[index].Alternatives[alternativeIndex]);
                }
                result += "]";
            }
            return result;
        }

        private static string FormatChord(int[] chord)
        {
            if (chord == null || chord.Length == 0)
            {
                return "none";
            }
            string result = string.Empty;
            for (int index = 0; index < chord.Length; index++)
            {
                result += (index == 0 ? string.Empty : "+") + chord[index];
            }
            return result;
        }

    }
}
