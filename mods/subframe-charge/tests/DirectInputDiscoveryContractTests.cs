using JKRuntime.Input;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using JumpKing.Controller;

internal static class DirectInputDiscoveryContractTests
{
    private const BindingFlags Private = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private sealed class WrappedLegacy : IPad
    {
        public int[] GetPressedButtons() { throw new Exception("Discovery must not poll game input"); }
        public string ButtonToString(int button) { return "Chord"; }
        public PadBinding GetDefaultBind() { return new PadBinding { jump = new[] { -1000000 } }; }
        public string GetSaveIdentifier() { return "184ff590-b295-11f0-8002-444553540000"; }
        public string GetPrintName() { return "Wrapped legacy controller"; }
        public bool IsConnected() { return true; }
    }
    public static int[][] Resolve(PadInstance pad, int[] buttons)
    { return buttons == null ? new int[0][] : new[] { new[] { 2, 3 } }; }

    internal static void Run(Assembly mod)
    {
        ControllerManager saved = ControllerManager.instance;
        Type virtualizer = typeof(JKRuntime.UI.UIApi).Assembly.GetType("JKRuntime.UI.ChordVirtualizer", true);
        PadInstance pad = new PadInstance(new WrappedLegacy());
        object sampler = null;
        try
        {
            pad.GetBind().jump = (int[])virtualizer.GetMethod("Apply", Private).Invoke(null,
                new object[] { pad, "test.di", new[] { new JKRuntime.UI.UiChord(new[] { 2, 3 }) } });
            ControllerManager.instance = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", Private).SetValue(ControllerManager.instance, new List<PadInstance> { pad });
            Type stateType = mod.GetType("SubframeCharge.SubframeChargeState", true);
            Array bindings = (Array)stateType.GetMethod("FindDirectInputJumpBindings", Private).Invoke(null, null);
            if (bindings.Length != 1) throw new Exception("Wrapped native GUID not discovered");
            object binding = bindings.GetValue(0);
            Type bindingType = binding.GetType();
            int[][] physical = (int[][])bindingType.GetField("Alternatives", Private).GetValue(binding);
            if (physical.Length != 1 || physical[0].Length != 2 || physical[0][0] != 2 || physical[0][1] != 3)
                throw new Exception("DirectInput discovery bypassed Controls+ physical binding API");
            pad.GetBind().Enabled = false;
            if (((Array)stateType.GetMethod("FindDirectInputJumpBindings", Private).Invoke(null, null)).Length != 0)
                throw new Exception("Disabled legacy binding must not be sampled");
            pad.GetBind().Enabled = true;
            typeof(PadInstance).GetField("current_state", Private).SetValue(pad, new PadState { jump = true });
            Type samplerType = typeof(HighRateInputSampler);
            sampler = Activator.CreateInstance(samplerType, Private, null, new object[] { true }, null);
            samplerType.GetMethod("Start", Private).Invoke(sampler, null);
            Array xbox = Array.CreateInstance(typeof(XInputJumpBinding), 0);
            samplerType.GetMethod("Configure", Private, null,
                new[] { typeof(int[][]), xbox.GetType(), bindings.GetType(), typeof(IntPtr), typeof(bool) }, null)
                .Invoke(sampler, new object[] { new int[0][], xbox, bindings, IntPtr.Zero, true });
            object state = FormatterServices.GetUninitializedObject(stateType);
            stateType.GetField("sampler", Private).SetValue(state, sampler);
            MethodInfo unsupported = stateType.GetMethod("HasUnsupportedJumpDown", Private);
            if (!(bool)unsupported.Invoke(state, null)) throw new Exception("Unopened DI cannot be certified");
            long generation = (long)samplerType.GetProperty("ConfigurationGeneration", Private).GetValue(sampler, null);
            MethodInfo observe = samplerType.GetMethod("ObserveDirectInput", Private);
            Guid id = (Guid)bindingType.GetField("DeviceId", Private).GetValue(binding);
            observe.Invoke(sampler, new object[] { generation, id, false, true, 1L });
            observe.Invoke(sampler, new object[] { generation, id, true, true, 2L });
            if ((bool)unsupported.Invoke(state, null)) throw new Exception("Ready DI still rejected as unsupported");
            observe.Invoke(sampler, new object[] { generation, id, false, false, 3L });
            if (!(bool)unsupported.Invoke(state, null)) throw new Exception("Lost DI still certified");
            Console.WriteLine("[OK] Compiled DI discovery: wrapped GUID, Controls+ chord API, disabled bind, ready/lost gate");
        }
        finally
        {
            if (sampler != null) ((IDisposable)sampler).Dispose();
            virtualizer.GetMethod("Remove", Private).Invoke(null, new object[] { pad, "test.di" });
            ControllerManager.instance = saved;
        }
    }
}
