using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Input;
using JKRuntime.UI;
using JumpKing.Controller;

internal static class MouseBindingContractTests
{
    private const BindingFlags Private = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static void Run(Assembly mod)
    {
        ControllerManager saved = ControllerManager.instance;
        Type virtualizer = typeof(UIApi).Assembly.GetType("JKRuntime.UI.ChordVirtualizer", true);
        IPad keyboard = (IPad)Activator.CreateInstance(typeof(ControllerManager).Assembly.GetType("JumpKing.Controller.KeyboardPad", true));
        PadInstance pad = new PadInstance(keyboard);
        bool mouse = false, shift = false;
        using (HighRateInputSampler sampler = new HighRateInputSampler(true,
            key => ((key == 1 && mouse) || (key == 16 && shift)) ? unchecked((short)0x8000) : (short)0, null))
        try
        {
            pad.GetBind().jump = (int[])virtualizer.GetMethod("Apply", Private).Invoke(null, new object[] {
                pad, "test.mouse", new[] { new UiChord(16, MouseButtons.Left), new UiChord(MouseButtons.X2) }
            });
            ControllerManager.instance = (ControllerManager)FormatterServices.GetUninitializedObject(typeof(ControllerManager));
            typeof(ControllerManager).GetField("m_pads", Private).SetValue(ControllerManager.instance, new List<PadInstance> { pad });
            Type stateType = mod.GetType("SubframeCharge.SubframeChargeState", true);
            MethodInfo discover = stateType.GetMethod("FindKeyboardJumpBindings", Private);
            object[] args = { null };
            int[][] bindings = (int[][])discover.Invoke(null, args);
            if (bindings.Length != 2 || bindings[0][0] != 16 || bindings[0][1] != MouseButtons.Left || bindings[1][0] != MouseButtons.X2)
                throw new Exception("Compiled SFC discovery lost Controls+ mouse bindings");
            sampler.Start();
            sampler.Configure(bindings, new XInputJumpBinding[0], true);
            sampler.PollSource(0); mouse = true; sampler.PollSource(0);
            JumpInputTransition edge;
            if (sampler.TryDequeue(out edge)) throw new Exception("Partial mouse chord sampled as complete");
            shift = true; sampler.PollSource(0);
            if (!sampler.TryDequeue(out edge) || !edge.Reliable || !edge.IsDown)
                throw new Exception("SFC-discovered mouse chord missing worker press");
            object state = FormatterServices.GetUninitializedObject(stateType);
            stateType.GetField("sampler", Private).SetValue(state, sampler);
            typeof(PadInstance).GetField("current_state", Private).SetValue(pad, new PadState { jump = true });
            if ((bool)stateType.GetMethod("HasUnsupportedJumpDown", Private).Invoke(state, null))
                throw new Exception("Ready mouse incorrectly labelled unsupported");
            mouse = false; sampler.PollSource(0);
            if (!sampler.TryDequeue(out edge) || !edge.Reliable || edge.IsDown)
                throw new Exception("SFC-discovered mouse chord missing release");
            pad.GetBind().Enabled = false;
            if (((int[][])discover.Invoke(null, args)).Length != 0)
                throw new Exception("Disabled keyboard/mouse binding still sampled");
            Console.WriteLine("[OK] Compiled SFC mouse discovery: installed keyboard, Controls+ layer/chords, Win32 mapping, supported gate, disabled profile");
        }
        finally
        {
            virtualizer.GetMethod("Remove", Private).Invoke(null, new object[] { pad, "test.mouse" });
            ControllerManager.instance = saved;
        }
    }
}
