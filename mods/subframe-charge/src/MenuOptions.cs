namespace SubframeCharge
{
    public sealed class ShowMeasurementOption : JKRuntime.UI.SettingToggle
    { public ShowMeasurementOption() : base(Options.Measurement) { } }
    public sealed class EnabledOption : JKRuntime.UI.SettingToggle
    { public EnabledOption() : base(Options.Enabled) { } }
}
