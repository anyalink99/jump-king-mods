using System;
using JKRuntime;
using JKRuntime.Input;
using JumpKing.Controller;

namespace SubframeCharge
{
    internal sealed partial class SubframeChargeState
    {
        private static InputDiagnostics preparedDiagnostics;
        private static object preparedInput;

        internal static void PrepareInput(RuntimeScope scope)
        {
            if(!SettingsStore.Current.Enabled && !SettingsStore.Current.ShowMeasurement
                && !JKRuntime.Gameplay.MapMechanics.HasRules(MapActivation.Id)) return;
            // keep the stream alive through handoff, without owning bindings or player edges
            var input=scope.Own(SharedActionSampler.Acquire("subframe-charge","native.jump"));
            var token=preparedInput=new object();
            scope.Defer(delegate {
                if(ReferenceEquals(preparedInput,token)) { preparedInput=null; preparedDiagnostics=null; }
            });
            using(RuntimeApi.MeasureStartup("subframe-charge.prepare-sampler")) input.Start();
            using(RuntimeApi.MeasureStartup("subframe-charge.prepare-devices"))
            {
                preparedDiagnostics=CreateDiagnostics(input);
                preparedDiagnostics.Observe();
            }
            using(RuntimeApi.MeasureStartup("subframe-charge.prepare-display")) JumpPercentIntegration.PrepareDisplayHook();
        }

        private static InputDiagnostics CreateDiagnostics(IHighRateInput input)
        {
            return new InputDiagnostics(IsKeyboardPad,delegate(PadInstance pad) {
                int slot; return TryGetXboxUserIndex(pad,out slot) ? slot : -1;
            },DiagnosticLog.Write,input.CanMeasureDirectInput);
        }
        private static InputDiagnostics TakeDiagnostics(IHighRateInput input)
        {
            var result=preparedDiagnostics;
            preparedDiagnostics=null;
            return result ?? CreateDiagnostics(input);
        }
    }
}
